using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Muks.BackEnd
{
    public partial class BackendManager
    {
        private BackendGachaEconomyStore _gachaEconomyStore;
        private GachaEconomyService _gachaEconomy;
        private GameDataRestoreQuery _gachaEconomyQuery;
        private readonly List<GachaStaffData> _economyWrappers = new List<GachaStaffData>();
        private readonly GachaExchangeAnchoredClock _gachaExchangeClock =
            new GachaExchangeAnchoredClock(() => Time.realtimeSinceStartupAsDouble);
        // Called only by the existing server-time SUCCESS handlers. No query or device-clock fallback here.
        private void RecordGachaEconomyServerUtc(DateTime utc) => _gachaExchangeClock.RecordServerUtc(utc);
        public bool IsGachaEconomyProtected => _gachaEconomyStore?.IsProtected == true;
        public GachaEconomyService GachaEconomy
        {
            get
            {
                var query = GameDataRestore.LegacyQuery;
                if (_gachaEconomy != null && ReferenceEquals(_gachaEconomyQuery, query)) return _gachaEconomy;
                if (query == null || StaffRuntime.Snapshot == null || IsOfflineOwner) return null;
                var items = ItemManager.Instance.GetGachaItemDataList();
                var staff = ReadStaffPurchaseCatalog();
                if (items == null || items.Count == 0 || staff == null || staff.Count == 0) return null;
                if (GetGameDataSaveCoordinator() == null) return null;
                var config = Resources.Load<GachaEconomySettings>("GachaEconomySettings");
                if (config == null) { config = ScriptableObject.CreateInstance<GachaEconomySettings>(); config.hideFlags = HideFlags.HideAndDontSave; }
                var catalog = new List<GachaData>(items);
                foreach (var data in staff)
                {
                    var wrapper = GachaStaffData.Create(data); wrapper.hideFlags = HideFlags.HideAndDontSave;
                    _economyWrappers.Add(wrapper); catalog.Add(wrapper);
                }
                _gachaEconomyQuery = query;
                _gachaEconomyStore = new BackendGachaEconomyStore(this, query, GameDataRestore.LegacyTarget);
                _gachaEconomy = new GachaEconomyService(_gachaEconomyStore, catalog, config, clock: _gachaExchangeClock);
                _gachaEconomy.Committed += OnEconomyCommitted;
                return _gachaEconomy;
            }
        }
        private void OnEconomyCommitted(GachaEconomyTransaction transaction)
        {
            if (_gachaEconomyStore == null || !_gachaEconomyStore.IsCurrent) return;
            foreach (var result in transaction.Results)
                if (result.IsNew && result.Kind == GachaAcquisitionKind.Item && result.Data is GachaItemData)
                    EnhancementFairyAcquisitionEvents.PublishConfirmed(transaction.Id, result.Id);
        }
        internal GameDataSaveCoordinator EconomyCoordinator() => GetGameDataSaveCoordinator();
        internal bool EconomySessionCurrent(GameDataRestoreQuery query, GameDataSaveTarget target)
            => IsCurrentGameDataSaveSession(query, target);
        internal void DisposeEconomy()
        {
            ClearEconomySession();
            if (_economyWrappers == null) return;
            foreach (var wrapper in _economyWrappers)
            {
                if (wrapper == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(wrapper);
                else UnityEngine.Object.DestroyImmediate(wrapper);
            }
            _economyWrappers.Clear();
        }
        internal void ClearEconomySession()
        {
            // Keep an unresolved store/reservation alive: a new session cannot turn unknown into unpaid.
            _gachaEconomyStore?.Invalidate();
            if (_gachaEconomy != null) _gachaEconomy.Committed -= OnEconomyCommitted;
            _gachaEconomy = null; _gachaEconomyQuery = null; _gachaEconomyStore = null;
            EnhancementFairyAcquisitionEvents.ResetSession("");
        }
    }

    internal sealed class BackendGachaEconomyStore : IGachaEconomyStore, IGachaEconomyDrawAdmission
    {
        private readonly BackendManager _owner;
        private readonly GameDataRestoreQuery _query;
        private readonly GameDataSaveTarget _target;
        private GameDataSaveCoordinator _coordinator;
        private GachaEconomyTransaction _transaction;
        private GameDataSaveIdentity _identity;
        private string _json;
        private string _lastTransactionId;
        private int _submissionAttempt;
        private bool _reservedDiamonds;
        private bool _confirming;
        private bool _invalidated;
        private Action<GachaStoreResult,string> _completed;
        public bool IsProtected => _transaction != null;
        public bool IsCurrent => !_invalidated && _owner.EconomySessionCurrent(_query, _target);
        public BackendGachaEconomyStore(BackendManager owner, GameDataRestoreQuery query, GameDataSaveTarget target)
        { _owner = owner; _query = query; _target = target; }
        public GachaEconomySnapshot Capture() => IsCurrent ? UserInfo.CaptureGachaEconomy(_target.AccountInDate, _owner.StaffRuntime.Snapshot) : null;
        public bool CanStart(out string error)
        {
            error = null;
            if (!IsCurrent || _owner.StaffRuntime.Snapshot == null || !_owner.StaffRuntime.CanMutate || IsProtected)
            { error = "계정 복원 또는 이전 거래 확인을 기다려 주세요."; return false; }
            _coordinator = _owner.EconomyCoordinator();
            if (_coordinator == null || !_coordinator.CanStartPurchase || UserInfo.EconomyInventoryReservation != null)
            { error = "진행 중인 저장을 기다려 주세요."; return false; }
            return true;
        }
        public bool CanDraw(GachaMachineKind machine, GachaPaymentKind payment, out string error)
        {
            error = null;
            if (machine == GachaMachineKind.Staff && _owner.TryGetCurrentQuestStaffOffer(out _, out _))
            { error = "현재 고용 퀘스트의 무료 직원을 먼저 받아 주세요."; return false; }
            return true;
        }
        public void Save(GachaEconomyTransaction transaction, Action<GachaStoreResult,string> completed)
        {
            if (!CanStart(out string error) || !transaction.Before.HasSameState(Capture()))
            { completed(GachaStoreResult.RejectedBeforeCommit, error ?? "거래 원본이 변경되었습니다."); return; }
            if (transaction.IsDraw && !CanDraw(transaction.Machine, transaction.Payment, out error))
            { completed(GachaStoreResult.RejectedBeforeCommit, error); return; }
            _transaction = transaction; _completed = completed;
            if (_lastTransactionId != transaction.Id) { _lastTransactionId = transaction.Id; _submissionAttempt = 0; }
            _identity = new GameDataSaveIdentity(transaction.Id + ":attempt:" + checked(++_submissionAttempt), _target);
            if (!UserInfo.TryReserveEconomyInventory(this)) { Reject("아이템 저장을 준비할 수 없습니다."); return; }
            int cost = transaction.Before.Diamonds - transaction.After.Diamonds;
            if (cost > 0)
            {
                if (!_owner.StaffPurchaseWallet.TryReserve(this, cost, transaction.Before.Diamonds, out error)) { Reject(error); return; }
                _reservedDiamonds = true;
            }
            bool accepted = _coordinator.TryStartPurchase(_identity, CreateValues, Confirm, out error, request =>
            {
                if (_transaction == null) return;
                if (request.Status == GameDataSaveRequestStatus.RejectedBeforeSend) Reject(request.Error);
                else if (request.Status == GameDataSaveRequestStatus.Indeterminate
                    || request.Status == GameDataSaveRequestStatus.InvalidatedAfterSend
                    || request.Status == GameDataSaveRequestStatus.LocalCompletionFailed)
                    _completed?.Invoke(GachaStoreResult.Indeterminate, request.Error);
            });
            if (!accepted && _transaction != null) { Reject(error); return; }
            if (_transaction != null && (_coordinator.State == GameDataSaveCoordinatorState.Indeterminate
                || _coordinator.State == GameDataSaveCoordinatorState.InvalidatedAfterSend
                || _coordinator.State == GameDataSaveCoordinatorState.LocalCompletionFailed))
                completed(GachaStoreResult.Indeterminate, _coordinator.LastError);
        }
        private Param CreateValues()
        {
            if (!ValidateSource(out string error)) throw new InvalidOperationException(error);
            var next = _transaction.After;
            if (!StaffAccountSaveConverter.TrySerialize(next.Account, out string account, out error)) throw new InvalidOperationException(error);
            var values = new Param();
            values.Add(GameDataRestoreContext.StaffAccountFieldName, account);
            if (_reservedDiamonds) values.Add("Dia", next.Diamonds);
            values.Add("GiveGachaItemCountList", next.ItemCounts.Select(x => new SaveCountData(x.Key, x.Value)).ToList());
            values.Add("GiveGachaItemLevelList", next.ItemLevels.Select(x => new SaveLevelData(x.Key, x.Value)).ToList());
            values.Add("GiveRecipeList", next.RecipeLevels.Select(x => new SaveLevelData(x.Key, x.Value)).ToList());
            values.Add("TotalUseGachaMachineCount", next.TotalDrawCount);
            _json = values.GetJson(); return values;
        }
        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
        {
            if (!IsProtected || !_identity.Matches(identity) || _coordinator.State != GameDataSaveCoordinatorState.Sending
                || payload == null || !JToken.DeepEquals(JObject.Parse(payload.Json), JObject.Parse(_json))) return "현재 경제 거래의 고정 저장 자료가 아닙니다.";
            return ValidateSource(out string error) ? null : error;
        }
        private bool ValidateSource(out string error)
        {
            error = null;
            if (!IsCurrent || _transaction == null || !ReferenceEquals(_owner.StaffRuntime.Snapshot, _transaction.Before.Account)
                || !ReferenceEquals(UserInfo.EconomyInventoryReservation, this) || !UserInfo.EconomyInventoryMatches(_transaction.Before))
            { error = "거래 준비 이후 계정 또는 보유 상태가 변경되었습니다."; return false; }
            return !_reservedDiamonds || _owner.StaffPurchaseWallet.ValidateReservation(this, out error);
        }
        private void Confirm(GameDataSaveReceipt receipt)
        {
            if (_transaction == null || _transaction.IsCompleted) return;
            _confirming = true;
            try
            {
                if (!ValidateCommit(_owner.StaffRuntime, receipt, out string error)) throw new InvalidOperationException(error);
                var tx = _transaction;
                var counts = tx.After.ItemCounts.ToDictionary(x => x.Key,x => x.Value);
                var levels = tx.After.ItemLevels.ToDictionary(x => x.Key,x => x.Value);
                var recipes = tx.After.RecipeLevels.ToDictionary(x => x.Key,x => x.Value);
                if (!_owner.StaffRuntime.TryCommitEconomy(this, receipt, out error)) throw new InvalidOperationException(error);
                UserInfo.CommitGachaEconomyInventory(tx, counts, levels, recipes);
                var callback = _completed;
                _transaction = null; _completed = null;
                UserInfo.ReleaseEconomyInventory(this);
                if (_reservedDiamonds)
                {
                    try { _owner.StaffPurchaseWallet.NotifyCommittedCost(); }
                    catch (Exception ex) { Debug.LogWarning("Economy wallet listener: " + ex.Message); }
                    finally { _owner.StaffPurchaseWallet.ReleaseBeforeSend(this); _reservedDiamonds = false; }
                }
                UserInfo.NotifyGachaEconomy(tx, () => IsCurrent);
                if (!IsCurrent) return;
                callback(GachaStoreResult.Confirmed, null);
                // Preserve any positive diamond reward accepted during the original request.
                _owner.RequestGameDataAutosave();
            }
            finally { _confirming = false; }
        }
        internal bool ValidateCommit(StaffAccountRuntime runtime, GameDataSaveReceipt receipt, out string error)
        {
            error = null;
            if (!_confirming || !ReferenceEquals(runtime, _owner.StaffRuntime) || receipt == null
                || !ReferenceEquals(receipt, _coordinator.LastReceipt) || !receipt.SendStarted
                || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || !_identity.Matches(receipt.Identity)
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState
                || !JToken.DeepEquals(JObject.Parse(receipt.Payload.Json), JObject.Parse(_json)))
            { error = "경제 거래의 실제 저장 확정 근거가 없습니다."; return false; }
            return ValidateSource(out error);
        }
        internal StaffAccountSaveData Candidate => _transaction?.After.Account;
        internal bool CommitReservedCost(out string error)
        { error = null; return !_reservedDiamonds || _owner.StaffPurchaseWallet.TryCommitReservedCost(this, out error); }
        private void Reject(string error)
        {
            var callback = _completed;
            if (_reservedDiamonds) { _owner.StaffPurchaseWallet.ReleaseBeforeSend(this); _reservedDiamonds = false; }
            UserInfo.ReleaseEconomyInventory(this); _transaction = null; _completed = null;
            callback?.Invoke(GachaStoreResult.RejectedBeforeCommit, error);
        }
        internal void Invalidate()
        {
            _invalidated = true;
            UserInfo.ReleaseEconomyInventory(this);
            if (_reservedDiamonds) { _owner.StaffPurchaseWallet.ReleaseBeforeSend(this); _reservedDiamonds = false; }
            if (_transaction != null) _completed?.Invoke(GachaStoreResult.Indeterminate, "거래 전송 이후 계정 세션이 변경되었습니다.");
        }
    }
}
