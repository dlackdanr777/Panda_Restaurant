using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public sealed class QuestStaffOffer
    {
        public string QuestId { get; }
        public string StaffId { get; }
        public StaffData StaffData { get; }
        internal GameDataRestoreQuery Query { get; }
        internal QuestStaffOffer(string questId, StaffData staff, GameDataRestoreQuery query)
        { QuestId = questId; StaffData = staff; StaffId = staff.Id; Query = query; }
    }

    // An owned employee's shop detail is not a free-grant offer or a retained result.
    public sealed class QuestOwnedStaffDetail
    {
        public string QuestId { get; }
        public StaffData StaffData { get; }
        internal GameDataRestoreQuery Query { get; }
        internal QuestOwnedStaffDetail(string questId, StaffData staff, GameDataRestoreQuery query)
        { QuestId = questId; StaffData = staff; Query = query; }
    }

    public partial class BackendManager
    {
        private IQuestStaffTutorialState _questStaffTutorialState;
        private bool _startingQuestStaffGrant;
        private List<QuestStaffGrantExecution> _questStaffGrants;
        public QuestStaffGrantExecution CurrentQuestStaffGrant { get; private set; }
        public QuestStaffGrantExecution LastCompletedQuestStaffGrant { get; private set; }
        public event Action<QuestStaffGrantExecution> QuestStaffGrantCompleted;
        public bool IsQuestStaffGrantProtected => GameDataSaveCoordinator.IsQuestStaffGrantTargetProtected(GameDataRestore.LegacyTarget);
        private IQuestStaffTutorialState QuestStaffState => _questStaffTutorialState ?? (IsOfflineOwner
            ? throw new InvalidOperationException("Offline quest state must be explicitly injected.")
            : (_questStaffTutorialState = new UserQuestStaffTutorialState()));

        // Both shortcut entry and the actual free input read the live progression, not a supplied quest ID.
        public bool TryGetCurrentQuestStaffOffer(out QuestStaffOffer offer, out string error)
        {
            offer = null; error = null;
            try
            {
                if (_startingQuestStaffGrant || !CanSaveLegacyGameData || StaffRuntime.Mode != StaffAccountRuntimeMode.Common
                    || !StaffRuntime.CanMutate)
                { error = "직원 획득을 준비하고 있습니다. 진행 중인 작업을 확인해 주세요."; return false; }
                var query = GameDataRestore.LegacyQuery;
                var target = GameDataRestore.LegacyTarget;
                var source = StaffRuntime.Snapshot;
                var before = QuestStaffState.Read();
                if (before == null || !before.IsEligible)
                { error = "현재 고용 퀘스트에서 받을 수 있는 직원이 없습니다."; return false; }
                if (source == null || source.Staff.Any(s => s.Id == before.RequiredStaffId))
                { error = "이미 보유한 직원입니다. 고용 퀘스트를 확인해 주세요."; return false; }
                var catalog = GameDataTransport.ReadCatalog();
                if (!QuestStaffGrantExecution.ValidateCatalog(catalog, source, before.RequiredStaffId, out var staff, out error)) return false;
                var coordinator = GetGameDataSaveCoordinator();
                if (coordinator == null || !coordinator.CanStartPurchase || !IsCurrentGameDataSaveSession(query, target)
                    || !ReferenceEquals(source, StaffRuntime.Snapshot) || !before.Matches(QuestStaffState.Read())
                    || !StaffRuntime.CanMutate)
                { error = "직원 획득 준비 중 상태가 변경됐거나 다른 저장이 진행 중입니다."; return false; }
                offer = new QuestStaffOffer(before.CurrentQuestId, staff, query);
                return true;
            }
            catch (Exception ex) { error = "고용 퀘스트 확인 실패: " + ex.GetType().Name; return false; }
        }

        // Navigation only: an accepted request remains viewable while sending and after
        // ownership is committed. This does not relax TryGetCurrentQuestStaffOffer or grant admission.
        public bool TryGetRetainedQuestStaffEntry(out QuestStaffOffer entry, out QuestStaffGrantExecution operation)
        {
            entry = null;
            operation = null;
            try
            {
                var retained = CurrentQuestStaffGrant;
                if (!IsCurrentQuestStaffGrant(retained)
                    || retained.Request.Status == GameDataSaveRequestStatus.InvalidatedAfterSend) return false;
                var current = QuestStaffState.Read();
                if (current == null || current.Stage != EStage.Stage1 || !current.FirstTutorialClear
                    || !current.DefinitionMatches || !current.PrerequisitesCleared || current.IsRewardClaimed
                    || current.CurrentQuestId != retained.QuestId
                    || current.RequiredStaffId != retained.Before.RequiredStaffId
                    || (current.GrantMask.HasValue && !QuestStaffTutorialPolicy.IsValidGrantMask(current.GrantMask.Value)))
                    return false;
                if (!QuestStaffGrantExecution.ValidateCatalog(ReadQuestStaffCatalog(), StaffRuntime.Snapshot,
                    current.RequiredStaffId, out var staff, out _)) return false;
                if (!IsCurrentQuestStaffGrant(retained) || !current.Matches(QuestStaffState.Read())) return false;
                entry = new QuestStaffOffer(retained.QuestId, staff, retained.Query);
                operation = retained;
                return true;
            }
            catch { return false; }
        }

        public bool TryGetCurrentQuestOwnedStaffDetail(out QuestOwnedStaffDetail detail)
        {
            detail = null;
            try
            {
                var query = GameDataRestore.LegacyQuery;
                var target = GameDataRestore.LegacyTarget;
                var source = StaffRuntime.Snapshot;
                var current = QuestStaffState.Read();
                if (StaffRuntime.Mode != StaffAccountRuntimeMode.Common || source == null
                    || !IsCurrentGameDataSaveSession(query, target) || current == null
                    || current.Stage != EStage.Stage1 || !current.FirstTutorialClear
                    || !current.DefinitionMatches || !current.PrerequisitesCleared || current.IsRewardClaimed
                    || !QuestStaffTutorialPolicy.TryGetMapping(current.CurrentQuestId, out var id, out _)
                    || current.RequiredStaffId != id || !source.Staff.Any(item => item.Id == id)
                    || (current.GrantMask.HasValue && !QuestStaffTutorialPolicy.IsValidGrantMask(current.GrantMask.Value)))
                    return false;
                if (!QuestStaffGrantExecution.ValidateCatalog(ReadQuestStaffCatalog(), source, id, out var staff, out _)
                    || !IsCurrentGameDataSaveSession(query, target) || !ReferenceEquals(source, StaffRuntime.Snapshot)
                    || !current.Matches(QuestStaffState.Read())) return false;
                detail = new QuestOwnedStaffDetail(current.CurrentQuestId, staff, query);
                return true;
            }
            catch { return false; }
        }

        public bool TryStartQuestStaffGrant(out QuestStaffGrantExecution execution, out string error)
        {
            execution = null;
            if (!TryGetCurrentQuestStaffOffer(out var offer, out error)) return false;
            _startingQuestStaffGrant = true;
            try
            {
                var coordinator = GetGameDataSaveCoordinator();
                var query = GameDataRestore.LegacyQuery;
                var state = QuestStaffState;
                var before = state.Read();
                if (coordinator == null || !coordinator.CanStartPurchase || before == null || !before.IsEligible
                    || before.CurrentQuestId != offer.QuestId || before.RequiredStaffId != offer.StaffId)
                { error = "현재 무료 직원 획득을 시작할 수 없습니다."; return false; }
                execution = new QuestStaffGrantExecution(this, coordinator, query, GameDataRestore.LegacyTarget, state, before, offer);
                CurrentQuestStaffGrant = execution;
                if (_questStaffGrants == null) _questStaffGrants = new List<QuestStaffGrantExecution>();
                _questStaffGrants.Add(execution);
                execution.AttachRequest(coordinator.StartQuestStaffGrant(execution));
                error = execution.Error;
                return execution.Request != null && execution.Request.Accepted
                    && execution.Request.Status != GameDataSaveRequestStatus.RejectedBeforeSend;
            }
            catch (Exception ex) { error = "직원 획득 준비 실패: " + ex.GetType().Name; return false; }
            finally { _startingQuestStaffGrant = false; }
        }

        internal bool IsCurrentQuestStaffGrant(QuestStaffGrantExecution operation) => operation != null
            && operation.Request != null && operation.Request.Accepted
            && operation.Request.Status != GameDataSaveRequestStatus.RejectedBeforeSend
            && ReferenceEquals(CurrentQuestStaffGrant, operation) && ReferenceEquals(_gameDataSaveQuery, operation.Query)
            && IsCurrentGameDataSaveSession(operation.Query, operation.Identity.Target) && StaffRuntime.Mode == StaffAccountRuntimeMode.Common
            && QuestStaffState.Read()?.CurrentQuestId == operation.QuestId;
        public bool CanPresentQuestStaffGrant(QuestStaffGrantExecution operation) => operation != null && operation.LocalCompleted
            && IsCurrentGameDataSaveSession(operation.Query, operation.Identity.Target) && StaffRuntime.Mode == StaffAccountRuntimeMode.Common;
        internal IReadOnlyList<StaffData> ReadQuestStaffCatalog() => GameDataTransport.ReadCatalog();
        internal void NotifyQuestStaffGrantCompleted(QuestStaffGrantExecution operation, IQuestStaffTutorialState state)
        {
            LastCompletedQuestStaffGrant = operation;
            void Notify(Action action)
            {
                if (!CanPresentQuestStaffGrant(operation)) return;
                try { action(); } catch (Exception ex) { operation.NotificationError = ex.GetType().Name; }
            }
            Notify(state.NotifyStaffAdded); // Refresh TYPE05 ownership only, never claim its quest reward.
            Notify(() => RequestGameDataAutosave()); // Latest quest flags/ordinary income, not the old grant payload.
            if (QuestStaffGrantCompleted != null)
                foreach (Action<QuestStaffGrantExecution> observer in QuestStaffGrantCompleted.GetInvocationList())
                    Notify(() => observer(operation));
            // No Dia, total-use count, item tutorial flag, PaymentInfo, or duplicate-token operation here.
        }

        private int? CurrentQuestStaffGrantMask => _questStaffTutorialState != null
            ? _questStaffTutorialState.Read()?.GrantMask : UserInfo.QuestStaffGrantMask;
        private bool TryAddQuestStaffGrantSaveField(Param values, out string error)
        {
            error = null;
            try
            {
                var current = CurrentQuestStaffGrantMask;
                var field = JObject.Parse(values.GetJson()).Property(QuestStaffTutorialPolicy.GrantFieldName);
                if (field != null)
                {
                    if (field.Value.Type == JTokenType.Integer && current.HasValue
                        && QuestStaffTutorialPolicy.IsValidGrantMask(current.Value) && field.Value.Value<long>() == current.Value) return true;
                    error = "직원 퀘스트 지급 근거가 최신 상태와 다릅니다."; return false;
                }
                if (current.HasValue)
                {
                    if (!QuestStaffTutorialPolicy.IsValidGrantMask(current.Value)) { error = "직원 퀘스트 지급 근거가 손상됐습니다."; return false; }
                    values.Add(QuestStaffTutorialPolicy.GrantFieldName, current.Value);
                }
                return true; // Missing old evidence remains missing until an eligible current grant succeeds.
            }
            catch { error = "직원 퀘스트 지급 근거를 읽을 수 없습니다."; return false; }
        }
        private bool ValidateQuestStaffGrantSave(GameDataSavePayload payload, out string error)
        {
            error = null;
            try
            {
                var field = JObject.Parse(payload.Json).Property(QuestStaffTutorialPolicy.GrantFieldName);
                if (field == null) return true; // A partial save cannot erase a field it does not contain.
                var mask = CurrentQuestStaffGrantMask;
                if (!mask.HasValue || !QuestStaffTutorialPolicy.IsValidGrantMask(mask.Value)
                    || field.Value.Type != JTokenType.Integer || field.Value.Value<long>() != mask.Value)
                { error = "명시된 직원 퀘스트 지급 근거가 현재 확정값과 다릅니다."; return false; }
                return true;
            }
            catch { error = "전송 전 직원 퀘스트 지급 근거 검증에 실패했습니다."; return false; }
        }
    }
}
