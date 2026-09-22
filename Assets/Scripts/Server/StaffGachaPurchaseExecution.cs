using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Muks.BackEnd
{
    public enum StaffGachaPurchaseExecutionStatus
    {
        Preparing, Sending, Completed, RejectedBeforeSend, Indeterminate,
        LocalCompletionFailed, InvalidatedAfterSend
    }

    /// <summary>One fixed local purchase and its real save receipt. No UI lifetime, retry or durable idempotency claim.</summary>
    public sealed class StaffGachaPurchaseExecution
    {
        private readonly BackendManager _owner;
        private readonly GameDataSaveCoordinator _coordinator;
        private readonly IStaffPurchaseWallet _wallet;
        private readonly StaffGachaPurchaseType _type;
        private readonly StaffGachaRequestSession _session = new StaffGachaRequestSession();
        private StaffData[] _catalog;
        private string[] _catalogJson;
        private readonly HashSet<GachaStaffData> _ownedDisplayData = new HashSet<GachaStaffData>();
        private bool _confirming;
        private bool _reserved;
        private int _cost;
        private int _initialDiamonds;
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveIdentity Identity { get; }
        public GameDataSaveRequest Request { get; private set; }
        public StaffAccountSaveData Source { get; private set; }
        public StaffGachaPurchasePlan Plan => _session.CurrentRequest?.Plan;
        public StaffGachaRequestState SessionState => _session.State;
        public IReadOnlyList<GachaStaffData> DrawnStaff { get; private set; }
        public string StaffAccountJson { get; private set; }
        public bool IsCompleted { get; private set; }
        public int CompletionCount { get; private set; }
        public string NotificationError { get; internal set; }
        public string Error => Request?.Error;
        public StaffGachaPurchaseExecutionStatus Status
        {
            get
            {
                if (IsCompleted) return StaffGachaPurchaseExecutionStatus.Completed;
                switch (Request?.Status)
                {
                    case GameDataSaveRequestStatus.Sending: return StaffGachaPurchaseExecutionStatus.Sending;
                    case GameDataSaveRequestStatus.RejectedBeforeSend: return StaffGachaPurchaseExecutionStatus.RejectedBeforeSend;
                    case GameDataSaveRequestStatus.Indeterminate: return StaffGachaPurchaseExecutionStatus.Indeterminate;
                    case GameDataSaveRequestStatus.LocalCompletionFailed: return StaffGachaPurchaseExecutionStatus.LocalCompletionFailed;
                    case GameDataSaveRequestStatus.InvalidatedAfterSend: return StaffGachaPurchaseExecutionStatus.InvalidatedAfterSend;
                    default: return StaffGachaPurchaseExecutionStatus.Preparing;
                }
            }
        }

        internal StaffGachaPurchaseExecution(BackendManager owner, GameDataSaveCoordinator coordinator,
            GameDataRestoreQuery query, GameDataSaveTarget target, IStaffPurchaseWallet wallet, StaffGachaPurchaseType type)
        {
            _owner = owner; _coordinator = coordinator; Query = query; _wallet = wallet; _type = type;
            Identity = new GameDataSaveIdentity("staff-purchase:" + Guid.NewGuid().ToString("N"), target);
        }

        internal void AttachRequest(GameDataSaveRequest request) => Request = request;
        internal void Start() => Request = _coordinator.StartStaffPurchase(this);

        // The coordinator owns the target before this factory runs. Readiness/policy failures never consume RNG.
        internal Param CreateValuesUnderProtection()
        {
            if (!HasProtection() || !_owner.IsCurrentStaffPurchase(this))
                throw new InvalidOperationException("현재 직원 구매 보호권이 없습니다.");
            Source = _owner.StaffRuntime.Snapshot;
            if (!StaffGachaPurchasePlanCalculator.TryGetPolicy(_type, out int cost, out int count))
                throw new InvalidOperationException("구매 종류가 잘못되었습니다.");
            int before = _wallet.Diamonds;
            if (!TryReadCandidates(_owner.ReadStaffPurchaseCatalog(), Source, out var registered, out string error))
                throw new InvalidOperationException(error);
            _catalog = registered.ToArray();
            _catalogJson = _catalog.Select(JsonUtility.ToJson).ToArray();
            _cost = cost;
            _initialDiamonds = before;
            if (!_owner.IsCurrentStaffPurchase(this) || !ReferenceEquals(Source, _owner.StaffRuntime.Snapshot)
                || !_wallet.TryReserve(this, cost, before, out error))
                throw new InvalidOperationException(error ?? "구매 준비 중 상태가 변경되었습니다.");
            _reserved = true;
            if (!Revalidate(out error)) throw new InvalidOperationException(error);

            // Native wrappers exist only for this one draw. Unselected/rejected wrappers are never orphaned;
            // selected wrappers belong to this result until its Backend owner is destroyed, not its window.
            var candidates = new List<GachaData>(_catalog.Length);
            try
            {
                foreach (StaffData staff in _catalog)
                {
                    GachaStaffData wrapper = GachaStaffData.Create(staff);
                    wrapper.hideFlags = HideFlags.HideAndDontSave;
                    candidates.Add(wrapper);
                }
                var draw = _owner.StaffPurchaseDraw;
                var results = new List<GachaStaffData>(count);
                for (int i = 0; i < count; i++)
                {
                    GachaStaffData selected = draw != null ? draw(candidates) : StaffGachaRandomSelector.Select(candidates);
                    if (selected == null || !candidates.Any(candidate => ReferenceEquals(candidate, selected)))
                        throw new InvalidOperationException("등록 자료 밖의 직원 추첨 결과입니다.");
                    results.Add(selected);
                }
                if (!Revalidate(out error)) throw new InvalidOperationException(error);
                // Exactly once: the session reuses PurchasePlan -> Apply -> Acquisition calculators.
                if (!_session.TryStart(Identity.RequestId, Source, before, _type, results, out error))
                    throw new InvalidOperationException(error);
                if (!StaffAccountSaveConverter.TrySerialize(Plan.AccountResult.UpdatedAccount, out string json, out error))
                    throw new InvalidOperationException(error);
                StaffAccountJson = json;
                var values = new Param();
                values.Add("Dia", Plan.DiamondsAfter);
                values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
                DrawnStaff = Array.AsReadOnly(results.ToArray());
                foreach (GachaStaffData selected in results) _ownedDisplayData.Add(selected);
                return values;
            }
            finally
            {
                foreach (GachaStaffData candidate in candidates)
                    if (!_ownedDisplayData.Contains(candidate)) DestroyWrapper(candidate);
            }
        }

        internal static bool TryReadCandidates(IReadOnlyList<StaffData> catalog, StaffAccountSaveData source,
            out IReadOnlyList<StaffData> candidates, out string error)
        {
            candidates = null;
            if (!StaffAccountSaveConverter.Validate(source, out error)) return false;
            if (GameDataRestoreContext.TryBuildStaffMaximums(() => catalog, out var maximums, out error)
                != GameDataRestoreStatus.Ready
                || GameDataRestoreContext.ValidateStaffLevels(source.Staff, maximums, out error) != GameDataRestoreStatus.Ready)
                return false;
            // Never use the display manager's skip/deduplicate list to authorize a paid draw.
            if (!StaffGachaRandomSelector.TryValidatePurchaseCatalog(catalog, out error)) return false;
            candidates = catalog;
            return true;
        }

        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
        {
            if (IsCompleted || !HasProtection() || _coordinator.State != GameDataSaveCoordinatorState.Sending
                || !Identity.Matches(identity) || !MatchesPayload(payload)) return "현재 구매의 고정 전송 자료가 아닙니다.";
            return Revalidate(out string error) && HasProtection() ? null : error ?? "구매 전송 근거가 변경되었습니다.";
        }

        internal void Confirm(GameDataSaveReceipt receipt)
        {
            if (IsCompleted) return;
            _confirming = true;
            try
            {
                if (!ValidateConfirmation(_owner.StaffRuntime, receipt, out string error))
                    throw new InvalidOperationException(error);
                // Server acknowledgement and local application are deliberately distinct states.
                if (!_session.TryHandleResponse(Identity.RequestId, StaffGachaResponseKind.SuccessConfirmed, out var completed)
                    || completed == null || !ReferenceEquals(completed.Plan, Plan))
                    throw new InvalidOperationException("구매 성공 결과를 한 번만 확정할 수 있습니다.");
                if (!_owner.StaffRuntime.TryCommitPurchase(this, receipt, _wallet, out error))
                    throw new InvalidOperationException(error);
                IsCompleted = true;
                CompletionCount = 1;
                // Staff and current-balance-minus-cost are both committed before any listener observes them.
                _owner.NotifyStaffPurchaseCompleted(this, _wallet);
                _wallet.ReleaseBeforeSend(this); // The reservation is already committed; release its mutation fence.
                _reserved = false;
            }
            finally { _confirming = false; }
        }

        internal bool ValidateConfirmation(StaffAccountRuntime runtime, GameDataSaveReceipt receipt, out string error)
        {
            error = null;
            if (!_confirming || IsCompleted || !ReferenceEquals(runtime, _owner.StaffRuntime) || !HasProtection()
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState
                || receipt == null || !ReferenceEquals(receipt, _coordinator.LastReceipt) || !receipt.SendStarted
                || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || !Identity.Matches(receipt.Identity)
                || !MatchesPayload(receipt.Payload))
            { error = "동일 구매 요청의 실제 성공·보호·전송 근거가 없습니다."; return false; }
            return Revalidate(out error);
        }

        private bool Revalidate(out string error)
        {
            error = null;
            if (!HasProtection() || !_owner.IsCurrentStaffPurchase(this)
                || !ReferenceEquals(Source, _owner.StaffRuntime.Snapshot))
            { error = "구매 계정·세대·공용 직원 상태가 변경되었습니다."; return false; }
            if (!_wallet.ValidateReservation(this, _cost, _initialDiamonds, out error)) return false;
            IReadOnlyList<StaffData> current = _owner.ReadStaffPurchaseCatalog();
            if (!TryReadCandidates(current, Source, out _, out error)) return false;
            if (current.Count != _catalog.Length)
            { error = "직원 등록 자료가 변경되었습니다."; return false; }
            for (int i = 0; i < current.Count; i++)
                if (!ReferenceEquals(current[i], _catalog[i]) || JsonUtility.ToJson(current[i]) != _catalogJson[i])
                { error = "직원 등록·등급·레벨 자료가 변경되었습니다."; return false; }
            // Catalog/provider callbacks may reenter or invalidate the current session.
            if (!_owner.IsCurrentStaffPurchase(this) || !ReferenceEquals(Source, _owner.StaffRuntime.Snapshot)
                || !HasProtection())
            { error = "구매 검증 중 상태가 변경되었습니다."; return false; }
            return _wallet.ValidateReservation(this, _cost, _initialDiamonds, out error);
        }

        private bool HasProtection() => ReferenceEquals(_coordinator.ActiveStaffPurchase, this);
        private bool MatchesPayload(GameDataSavePayload payload)
        {
            if (Plan == null || payload == null || StaffAccountJson == null) return false;
            try
            {
                JObject fields = JObject.Parse(payload.Json);
                return fields.Properties().Count() == 2 && fields["Dia"]?.Type == JTokenType.Integer
                    && fields["Dia"].Value<long>() == Plan.DiamondsAfter
                    && fields[GameDataRestoreContext.StaffAccountFieldName]?.Type == JTokenType.String
                    && (string)fields[GameDataRestoreContext.StaffAccountFieldName] == StaffAccountJson;
            }
            catch { return false; }
        }

        internal void ReleaseUnsent()
        {
            if (_reserved) { _wallet.ReleaseBeforeSend(this); _reserved = false; }
            ReleaseOwnedDisplayData();
            _session.TryHandleResponse(Identity.RequestId, StaffGachaResponseKind.UnappliedFailureConfirmed, out _);
        }

        // Backend-owner lifetime cleanup only. A scene-bound display must never call this for a live result.
        internal void ReleaseOwnedDisplayData()
        {
            foreach (GachaStaffData item in _ownedDisplayData) DestroyWrapper(item);
            _ownedDisplayData.Clear();
            DrawnStaff = null;
        }

        private static void DestroyWrapper(GachaStaffData item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
        internal void OnSaveStateChanged(GameDataSaveRequest request)
        {
            if (request.Status == GameDataSaveRequestStatus.Indeterminate || request.Status == GameDataSaveRequestStatus.InvalidatedAfterSend)
                _session.TryHandleResponse(Identity.RequestId, StaffGachaResponseKind.Indeterminate, out _);
        }
    }
}
