#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum StaffGachaOfflineCase { SingleNew, Eleven, SingleDuplicate, ElevenDuplicates, Unique, Special }

/// <summary>Editor-owned memory transport around the real purchase service. No UserInfo/PaymentInfo writes.</summary>
public sealed class StaffGachaOfflineSession : IDisposable
{
    private readonly MemoryTransport _transport;
    private readonly IReadOnlyList<StaffData> _catalog;
    private string[] _fixedIds;
    private int _drawIndex;
    private bool _disposed;
    public BackendManager Owner { get; private set; }
    public StaffPurchaseDiamondWallet Wallet { get; }
    public int Diamonds { get; private set; } = 110;
    public int DrawCount => _drawIndex;
    public int RecordNotifications { get; private set; }
    public int WalletNotifications { get; private set; }
    public int PurchaseWrites => _transport.Writes.Count == 0 ? 0 : 1;
    public int FollowupWrites => Math.Max(0, _transport.Writes.Count - 1);
    public int MockReads => _transport.Reads;
    public string PurchasePayload => _transport.Writes.FirstOrDefault()?.Json;
    public string FollowupPayload => _transport.Writes.Count > 1 ? _transport.Writes.Last().Json : null;
    public StaffGachaPurchaseExecution Request => Owner == null ? null : Owner.CurrentStaffPurchaseExecution;
    public StaffAccountSaveData Account => Owner == null ? null : Owner.StaffRuntime.Snapshot;
    public bool HasUnresolvedRequest => Request != null && !Request.IsCompleted &&
        Request.Status != StaffGachaPurchaseExecutionStatus.RejectedBeforeSend;
    public string LastError { get; private set; }

    public StaffGachaOfflineSession(IReadOnlyList<StaffData> catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Wallet = new StaffPurchaseDiamondWallet(() => Diamonds, value => Diamonds = value, () => WalletNotifications++);
        _transport = new MemoryTransport(this, catalog);
        try
        {
            Owner = BackendManager.CreateEditorOfflineOwner(_transport, new NoStageTransport(), Wallet,
                SelectFixedStaff, _ => RecordNotifications++);
            var auth = Owner.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
            _transport.LoggedIn = true;
            if (!Owner.NotifyFederationLoginSuccess(auth, Response("200", "")))
                throw new InvalidOperationException("검증용 메모리 인증 근거를 연결하지 못했습니다.");
            bool restored = false;
            Owner.GetAndRestoreGameDataAsync((query, result) => restored = result.Status == GameDataRestoreStatus.Ready);
            if (!restored || Owner.StaffRuntime.Mode != StaffAccountRuntimeMode.Common || Account == null)
                throw new InvalidOperationException("검증용 공용 데이터 복원 실패: " + Owner.GameDataRestoreResult.Reason);
        }
        catch { Dispose(); throw; }
    }

    public bool TryStart(StaffGachaOfflineCase scenario, out string error)
    {
        error = null;
        if (_disposed || Owner == null || Request != null)
        { error = "이 독립 시험은 이미 요청을 시작했거나 종료되었습니다."; return false; }
        if (!TryFixtureIds(scenario, out _fixedIds, out error)) return false;
        _drawIndex = 0;
        bool accepted = Owner.TryStartStaffPurchase(_fixedIds.Length == 1 ? StaffGachaPurchaseType.Single : StaffGachaPurchaseType.Multi,
            out _, out error);
        LastError = error;
        return accepted;
    }

    private bool TryFixtureIds(StaffGachaOfflineCase scenario, out string[] ids, out string error)
    {
        ids = null; error = null;
        switch (scenario)
        {
            case StaffGachaOfflineCase.SingleNew: ids = new[] { "STAFF23" }; break;
            case StaffGachaOfflineCase.Eleven: ids = new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)).ToArray(); break;
            case StaffGachaOfflineCase.SingleDuplicate: ids = new[] { "STAFF01" }; break;
            case StaffGachaOfflineCase.ElevenDuplicates: ids = Enumerable.Repeat("STAFF01", 11).ToArray(); break;
            case StaffGachaOfflineCase.Unique:
            case StaffGachaOfflineCase.Special:
                Rank rank = scenario == StaffGachaOfflineCase.Unique ? Rank.Unique : Rank.Special;
                StaffData staff = _catalog.Where(item => item != null && item.Rank == rank).OrderBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
                if (staff != null) ids = new[] { staff.Id };
                break;
        }
        if (ids == null || ids.Any(id => _catalog.Count(staff => staff != null && staff.Id == id) != 1))
        { error = "현재 등록 원본에서 시험에 필요한 직원·등급을 고유하게 확인할 수 없습니다."; return false; }
        return true;
    }

    private GachaStaffData SelectFixedStaff(IReadOnlyList<GachaData> candidates)
    {
        if (_fixedIds == null || _drawIndex >= _fixedIds.Length) throw new InvalidOperationException("고정 결과 범위 밖 추첨입니다.");
        string id = _fixedIds[_drawIndex++];
        var staff = candidates.OfType<GachaStaffData>().Single(item => item.Id == id);
        Rank rank = staff.StaffData.Rank;
        bool normal = rank == Rank.Normal1 || rank == Rank.Normal2;
        int roll = normal ? 0 : rank == Rank.Rare ? 60 : rank == Rank.Unique ? 80 : 95;
        var peers = candidates.OfType<GachaStaffData>().Where(item => normal
            ? item.Rank == Rank.Normal1 || item.Rank == Rank.Normal2 : item.Rank == rank).ToList();
        // The production selector performs both selections. This is its existing deterministic input seam.
        return StaffGachaRandomSelector.Select(candidates, roll, peers.FindIndex(item => item.Id == id));
    }

    public bool AddFiveDiamonds(out string error)
    {
        error = null;
        if (_disposed || Request == null || Request.IsCompleted)
        { error = "응답 대기 중인 독립 시험에서만 보상을 추가할 수 있습니다."; return false; }
        return Wallet.TryAddReward(5, out error);
    }

    public bool ReplySuccess()
    {
        if (_disposed || _transport.Writes.Count == 0) return false;
        _transport.Writes[0].Reply(Response("204", ""));
        return true; // Receipt delivery, not an assertion that this response was newly committed.
    }

    public bool ReplyIndeterminate()
    {
        if (_disposed || _transport.Writes.Count == 0) return false;
        _transport.Writes[0].Reply(null);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Owner != null)
        {
            Owner.DestroyEditorOfflineOwner();
        }
        Owner = null;
        // No reset of production ownership locks, no retry and no persistent recovery claim.
    }

    private sealed class PendingWrite
    {
        public string Json;
        public Action<BackendReturnObject> Reply;
    }

    private sealed class MemoryTransport : IGameDataBackendTransport
    {
        private readonly StaffGachaOfflineSession _session;
        private readonly IReadOnlyList<StaffData> _catalog;
        private readonly string _row = "offline-row:" + Guid.NewGuid().ToString("N");
        public readonly List<PendingWrite> Writes = new List<PendingWrite>();
        public int Reads;
        public bool LoggedIn { get; set; }
        public bool NativeLoggedIn => LoggedIn;
        public string AccountInDate { get; } = "offline-account:" + Guid.NewGuid().ToString("N");
        public bool GameplaySaveAllowed => true;
        public MemoryTransport(StaffGachaOfflineSession session, IReadOnlyList<StaffData> catalog) { _session = session; _catalog = catalog; }
        public IReadOnlyList<StaffData> ReadCatalog() => _catalog;
        public void Get(string account, Action<BackendReturnObject> callback)
        {
            if (account != AccountInDate) throw new InvalidOperationException("다른 검증 계정 조회입니다.");
            Reads++;
            var initial = new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4) }, 55);
            if (!StaffAccountSaveConverter.TrySerialize(initial, out string json, out string error)) throw new InvalidOperationException(error);
            var row = new JObject
            {
                ["owner_inDate"] = new JObject { ["S"] = account }, ["inDate"] = new JObject { ["S"] = _row },
                ["Dia"] = new JObject { ["N"] = "110" }, ["StaffAccount"] = new JObject { ["S"] = json }
            };
            callback(Response("200", new JObject { ["rows"] = new JArray(row), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None)));
        }
        public bool Restore(BackendReturnObject response) => response != null && response.IsSuccess();
        public Param InitialValues() => throw new InvalidOperationException("검증 세션에 최초 계정 생성은 없습니다.");
        public void Insert(Param values, Action<BackendReturnObject> callback) => throw new InvalidOperationException("검증 세션은 Insert를 허용하지 않습니다.");
        public Param LatestValues() { var values = new Param(); values.Add("Dia", _session.Diamonds); return values; }
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback)
        {
            if (target.AccountInDate != AccountInDate || target.RowInDate != _row) throw new InvalidOperationException("다른 검증 계정/행 전송입니다.");
            Writes.Add(new PendingWrite { Json = values.GetJson(), Reply = callback });
            if (Writes.Count > 1) callback(Response("204", "")); // Latest-value autosave is acknowledged by memory only.
        }
    }

    private sealed class NoStageTransport : IStageDataLoadTransport
    {
        public void Get(EStage stage, string account, Func<bool> current, Action<BackendReturnObject> reply) => throw new InvalidOperationException("공용 검증 계정은 Stage를 조회하지 않습니다.");
        public BackendReturnObject Get(EStage stage, string account, Func<bool> current) => throw new InvalidOperationException("Stage 조회 금지");
        public bool Apply(EStage stage, BackendReturnObject response, Func<bool> current, bool asynchronous) => throw new InvalidOperationException("Stage 적용 금지");
        public StaffStageRuntimeSnapshot ReadStaff(EStage stage) => throw new InvalidOperationException("Stage 읽기 금지");
    }

    internal static BackendReturnObject Response(string status, string raw)
    {
        var result = new BackendReturnObject();
        foreach (var pair in new Dictionary<string, string> { ["StatusCode"] = status, ["ReturnValue"] = raw, ["ErrorCode"] = "", ["Message"] = "" })
        {
            var property = typeof(BackendReturnObject).GetProperty(pair.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null) throw new InvalidOperationException("설치된 SDK의 모의 응답 경계를 확인할 수 없습니다.");
            property.SetValue(result, Convert.ChangeType(pair.Value, property.PropertyType, CultureInfo.InvariantCulture));
        }
        return result;
    }
}
#endif
