using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;

public enum StaffAccountRuntimeMode { Unavailable, Legacy, Common }

/// <summary>Cost commit must be synchronous, non-notifying, and unchanged on false. Notify runs after level and cost commit.</summary>
public interface IStaffUpgradeWallet
{
    int Score { get; }
    long Gold { get; }
    int Diamonds { get; }
    bool TryApplyCost(UpgradeMoneyData cost, long expectedGold, int expectedDiamonds);
    void NotifyCost(UpgradeMoneyData cost);
}

/// <summary>
/// One Backend owner's current staff authority. Restored evidence is immutable; current data is replaced only by accepted
/// memory mutations or a different verified restore. Main-thread only; neither a purchase nor a persistence transaction.
/// </summary>
public sealed class StaffAccountRuntime
{
    private readonly Func<GameDataRestoreResult> _readRestore;
    private readonly Func<GameDataRestoreQuery> _readLegacyQuery;
    private readonly Func<bool> _canChange;
    private readonly Func<IReadOnlyList<StaffData>> _readCatalog;
    private GameDataRestoreEvidence _source;
    private GameDataRestoreQuery _query;
    private StaffAccountSaveData _current;
    private StaffAccountRuntimeMode _mode;
    private bool _changing;
    public string LastNotificationError { get; private set; }

    public StaffAccountRuntime(Func<GameDataRestoreResult> readRestore, Func<GameDataRestoreQuery> readLegacyQuery,
        Func<bool> canChange, Func<IReadOnlyList<StaffData>> readCatalog)
    {
        _readRestore = readRestore ?? throw new ArgumentNullException(nameof(readRestore));
        _readLegacyQuery = readLegacyQuery ?? throw new ArgumentNullException(nameof(readLegacyQuery));
        _canChange = canChange ?? throw new ArgumentNullException(nameof(canChange));
        _readCatalog = readCatalog ?? throw new ArgumentNullException(nameof(readCatalog));
    }

    public StaffAccountRuntimeMode Mode { get { Refresh(); return _mode; } }
    public StaffAccountSaveData Snapshot { get { Refresh(); return _mode == StaffAccountRuntimeMode.Common ? _current : null; } }
    public bool CanMutate => !_changing && Authorized();

    public void Refresh()
    {
        try
        {
            GameDataRestoreResult result = _readRestore();
            GameDataRestoreQuery query = _readLegacyQuery();
            // Read twice: query validation may invalidate the account while observing its identity.
            if (query == null || !ReferenceEquals(result, _readRestore())) { _mode = StaffAccountRuntimeMode.Unavailable; return; }
            if (result.Status == GameDataRestoreStatus.MigrationRequired)
            {
                _query = query;
                _mode = StaffAccountRuntimeMode.Legacy;
                return;
            }
            GameDataRestoreEvidence evidence = result.Evidence;
            if (result.Status != GameDataRestoreStatus.Ready || evidence == null || evidence.StaffAccount == null
                || !ReferenceEquals(evidence.Query, query)) { _mode = StaffAccountRuntimeMode.Unavailable; return; }
            if (!ReferenceEquals(_source, evidence))
            {
                _source = evidence;
                _current = evidence.StaffAccount;
            }
            _query = query;
            _mode = StaffAccountRuntimeMode.Common;
        }
        catch { _mode = StaffAccountRuntimeMode.Unavailable; }
    }

    private bool Authorized()
    {
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable) return false;
        GameDataRestoreQuery query = _query;
        try { if (!_canChange()) return false; }
        catch { return false; }
        Refresh();
        return _mode != StaffAccountRuntimeMode.Unavailable && ReferenceEquals(query, _query);
    }

    public bool IsOwned(string id) => GetLevel(id).HasValue;
    public int? GetLevel(string id)
    {
        StaffAccountSaveData current = Snapshot;
        if (current == null || string.IsNullOrEmpty(id)) return null;
        foreach (StaffAccountStaffRecord staff in current.Staff) if (staff.Id == id) return staff.Level;
        return null;
    }
    public List<StaffData> GetOwnedStaff()
    {
        StaffAccountSaveData current = Snapshot;
        if (current == null || !TryCatalog(out IReadOnlyList<StaffData> catalog)) return new List<StaffData>();
        var byId = catalog.ToDictionary(staff => staff.Id, StringComparer.Ordinal);
        var result = new List<StaffData>();
        foreach (StaffAccountStaffRecord staff in current.Staff)
        {
            if (!byId.TryGetValue(staff.Id, out StaffData data)) return new List<StaffData>();
            result.Add(data);
        }
        return result;
    }
    public bool CanUpgrade(StaffData staff)
    {
        int? level = staff == null ? null : GetLevel(staff.Id);
        return CanMutate && level.HasValue && IsRegistered(staff) && staff.CanUpgradeFromSavedLevel(level.Value);
    }

    public bool TryGive(StaffData staff, Action notify, out bool added)
    {
        added = false;
        if (!CanMutate || Mode != StaffAccountRuntimeMode.Common) return false;
        _changing = true;
        try
        {
            GameDataRestoreQuery query = _query;
            StaffAccountSaveData before = _current;
            if (!IsRegistered(staff) || !StillSame(query, before, StaffAccountRuntimeMode.Common)) return false;
            if (before.Staff.Any(record => record.Id == staff.Id)) return true;
            var records = new List<StaffAccountStaffRecord>(before.Staff) { new StaffAccountStaffRecord(staff.Id, 1) };
            _current = new StaffAccountSaveData(before.Version, records, before.PandaTokens);
            added = true;
            Notify(notify);
            return true;
        }
        finally { _changing = false; }
    }

    /// <summary>Both common and managed-legacy growth use the original StaffData policy and the same no-event cost boundary.</summary>
    public bool TryUpgrade(StageInfo stage, StaffData staff, IStaffUpgradeWallet wallet, Action notify, out string error)
    {
        error = null;
        if (stage == null || wallet == null || !CanMutate) { error = "현재 계정의 직원 변경을 허용할 수 없습니다."; return false; }
        _changing = true;
        try
        {
            GameDataRestoreQuery query = _query;
            StaffAccountRuntimeMode mode = _mode;
            StaffAccountSaveData before = _current;
            StaffStageRuntimeSnapshot legacy = mode == StaffAccountRuntimeMode.Legacy ? stage.CaptureStaffRuntimeSnapshot() : null;
            int level;
            if (!IsRegistered(staff) || (mode == StaffAccountRuntimeMode.Common
                ? !TryLevel(before, staff.Id, out level) : !stage.TryGetLegacyStaffLevel(staff.Id, out level)))
            { error = "유효한 보유 직원이 아닙니다."; return false; }
            if (!staff.CanUpgradeFromSavedLevel(level)) { error = "현재 레벨에서는 성장할 수 없습니다."; return false; }
            UpgradeMoneyData cost = staff.GetUpgradeMoneyData(level);
            int minimumScore = staff.GetUpgradeMinScore(level);
            long gold = wallet.Gold;
            int diamonds = wallet.Diamonds;
            if (wallet.Score < minimumScore) { error = "성장에 필요한 평점이 부족합니다."; return false; }
            if (cost == null || cost.Price < 0 || (cost.MoneyType != MoneyType.Gold && cost.MoneyType != MoneyType.Dia))
            { error = "성장 비용 자료가 올바르지 않습니다."; return false; }
            if ((cost.MoneyType == MoneyType.Gold && gold < cost.Price) || (cost.MoneyType == MoneyType.Dia && diamonds < cost.Price))
            { error = "성장에 필요한 재화가 부족합니다."; return false; }
            StaffAccountSaveData next = mode == StaffAccountRuntimeMode.Common
                ? new StaffAccountSaveData(before.Version, before.Staff.Select(record => record.Id == staff.Id
                    ? new StaffAccountStaffRecord(record.Id, level + 1) : record).ToArray(), before.PandaTokens) : null;

            // External reads are complete. The two commits below have no events, SDK calls or asynchronous work.
            int currentScore = wallet.Score;
            long currentGold = wallet.Gold;
            int currentDiamonds = wallet.Diamonds;
            bool sameLegacy = legacy == null || (!legacy.IsApplying && legacy.HasSameState(stage.CaptureStaffRuntimeSnapshot()));
            if (currentScore < minimumScore || currentGold != gold || currentDiamonds != diamonds
                || !sameLegacy || !StillSame(query, before, mode))
            { error = "성장 확인 중 계정·보유·레벨·재화 상태가 변경되었습니다."; return false; }
            if (!wallet.TryApplyCost(cost, gold, diamonds)) { error = "성장 재화 차감을 확정하지 못했습니다."; return false; }
            if (mode == StaffAccountRuntimeMode.Common) _current = next;
            else stage.CommitLegacyStaffUpgrade(staff.Id);
            // Keep reentry locked while both currency and staff subscribers see the committed state.
            Notify(() => wallet.NotifyCost(cost));
            Notify(notify);
            return true;
        }
        finally { _changing = false; }
    }

    private bool StillSame(GameDataRestoreQuery query, StaffAccountSaveData before, StaffAccountRuntimeMode mode)
        => Authorized() && _mode == mode && ReferenceEquals(_query, query)
            && (mode != StaffAccountRuntimeMode.Common || ReferenceEquals(_current, before));

    private bool IsRegistered(StaffData staff)
    {
        if (staff == null || !TryCatalog(out IReadOnlyList<StaffData> catalog)) return false;
        StaffData registered = catalog.FirstOrDefault(item => item.Id == staff.Id);
        return ReferenceEquals(registered, staff);
    }
    private bool TryCatalog(out IReadOnlyList<StaffData> catalog)
    {
        catalog = null;
        try
        {
            IReadOnlyList<StaffData> read = _readCatalog();
            if (GameDataRestoreContext.TryBuildStaffMaximums(() => read, out _, out _) != GameDataRestoreStatus.Ready) return false;
            catalog = read;
            return true;
        }
        catch { return false; }
    }
    private static bool TryLevel(StaffAccountSaveData data, string id, out int level)
    {
        level = 0;
        foreach (StaffAccountStaffRecord record in data.Staff)
            if (record.Id == id) { level = record.Level; return true; }
        return false;
    }
    private void Notify(Action action)
    {
        try { action?.Invoke(); }
        catch (Exception ex) { LastNotificationError = ex.GetType().Name; } // A listener failure cannot uncommit an accepted memory mutation.
    }

    public bool TryAddSaveField(Param values, out string error)
    {
        error = null;
        if (values == null) { error = "저장 자료가 없습니다."; return false; }
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable) { error = "현재 공용/기존 직원 복원 근거가 없습니다."; return false; }
        try
        {
            JObject fields = JObject.Parse(values.GetJson());
            if (_mode == StaffAccountRuntimeMode.Legacy)
            {
                if (fields.Property(GameDataRestoreContext.StaffAccountFieldName) == null) return true;
                error = "이전 전 계정에 StaffAccount를 임의로 저장할 수 없습니다.";
                return false;
            }
            if (!StaffAccountSaveConverter.TrySerialize(_current, out string json, out error)) return false;
            JToken old = fields[GameDataRestoreContext.StaffAccountFieldName];
            if (old != null)
            {
                if (old.Type == JTokenType.String && string.Equals((string)old, json, StringComparison.Ordinal)) return true;
                error = "기존 Param의 StaffAccount가 현재 공용 상태와 다릅니다.";
                return false;
            }
            values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
            return true;
        }
        catch { error = "공용 직원 저장 자료를 생성할 수 없습니다."; return false; }
    }

    public bool ValidateSaveField(GameDataSavePayload payload, out string error)
    {
        error = null;
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable || payload == null)
        { error = "현재 직원 복원 근거/저장 자료가 없습니다."; return false; }
        try
        {
            JObject fields = JObject.Parse(payload.CreateParamCopy().GetJson());
            JProperty field = fields.Property(GameDataRestoreContext.StaffAccountFieldName);
            if (field == null) return true; // A genuine partial update does not overwrite common data.
            if (_mode != StaffAccountRuntimeMode.Common || field.Value.Type != JTokenType.String
                || !StaffAccountSaveConverter.TrySerialize(_current, out string current, out error)
                || !string.Equals((string)field.Value, current, StringComparison.Ordinal))
            { error = "명시된 StaffAccount는 현재 검증된 공용 상태와 일치해야 합니다."; return false; }
            return true;
        }
        catch { error = "전송 전 StaffAccount 검증에 실패했습니다."; return false; }
    }
}
