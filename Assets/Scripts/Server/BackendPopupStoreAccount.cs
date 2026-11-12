using System;
using System.Globalization;
using System.IO;
using BackEnd;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Muks.BackEnd
{
    public partial class BackendManager
    {
        private PopupStoreAccountBoundState _popupAccountState;
        private PopupStoreGameDataAccountBridge _popupAccountBridge;
        private IPopupStoreAccountClock _popupAccountClock;
        private IPopupStoreAccountMemory _popupAccountMemory;
        private IPopupStoreAccountJournalStore _popupAccountJournal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterPopupStoreAccountFactory()
        {
            PopupStoreAccountPhaseBootstrap.BridgeFactory = popup =>
            {
#if UNITY_EDITOR
                if (CustomerPartyManualPlayBootstrap.Active) return null;
#endif
                // Never initialize or authenticate an SDK merely because a popup was opened.
                if (_instance == null || _instance.IsOfflineOwner) return null;
                var configuration = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
                var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
                if (configuration == null || !configuration.EnableAccountMode
                    || !configuration.TryFreeze(catalog, out var profile, out _, out _)
                    || !configuration.RewardTable.TryCreateSnapshot(out var policy, out _)) return null;
                return _instance.CreatePopupStoreAccountBridge(profile, policy, configuration.DailyBusinessLimit);
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditorPopupStoreAccount(IPopupStoreAccountClock clock,
            IPopupStoreAccountJournalStore journal, IPopupStoreAccountMemory memory)
        {
            if (!IsOfflineOwner || clock == null || journal == null || memory == null || _popupAccountState != null)
                throw new InvalidOperationException("An unused explicitly injected offline owner is required.");
            _popupAccountClock = clock; _popupAccountJournal = journal; _popupAccountMemory = memory;
        }
#endif

        public PopupStoreGameDataAccountBridge CreatePopupStoreAccountBridge(PopupStorePhaseProfileSnapshot profile,
            PopupStoreRewardPolicySnapshot policy, int dailyLimit = 1)
        {
            if (dailyLimit < 1 || dailyLimit > PopupStoreGameDataRecord.MaximumDailyLimit) return null;
            if (profile == null || policy == null || policy.PolicyId != PopupStoreRewardTable.R1PolicyId
                || !GameDataTransport.GameplaySaveAllowed || !EnsurePopupStoreAccountState(out var state)) return null;
            if (_popupAccountBridge != null && _popupAccountBridge.IsReady) return _popupAccountBridge;
            _popupAccountBridge = new PopupStoreGameDataAccountBridge(state, profile, policy,
                _popupAccountClock ?? (_popupAccountClock = new BackendPopupClock(this)), GetGameDataSaveCoordinator, dailyLimit);
            return _popupAccountBridge;
        }

        private bool EnsurePopupStoreAccountState(out PopupStoreAccountBoundState state)
        {
            state = null;
            // Historical detached test owners also inject this boundary without the newer offline factory.
            // They must never acquire a production file journal as a side effect of unrelated tests.
            if (!(GameDataTransport is SdkGameDataBackendTransport)
                && (_popupAccountClock == null || _popupAccountJournal == null || _popupAccountMemory == null)) return false;
            var evidence = RestoredGameData;
            var query = GameDataRestore.LegacyQuery;
            var target = GameDataRestore.LegacyTarget;
            if (evidence == null || !ReferenceEquals(evidence.Query, query) || !IsCurrentGameDataSaveSession(query, target)) return false;
            if (_popupAccountState != null && ReferenceEquals(_popupAccountState.Query, query))
            { state = _popupAccountState; return true; }
            if (IsOfflineOwner && (_popupAccountClock == null || _popupAccountJournal == null || _popupAccountMemory == null)) return false;
            _popupAccountState?.Invalidate(); _popupAccountBridge = null;
            var memory = _popupAccountMemory ?? (_popupAccountMemory = new UserPopupStoreAccountMemory());
            var journal = _popupAccountJournal ?? (_popupAccountJournal = new PopupStoreAccountFileJournal(
                Path.Combine(Application.persistentDataPath, "PopupStoreAccountRecovery")));
            _popupAccountState = state = new PopupStoreAccountBoundState(query, target, evidence.RawJson,
                () => IsCurrentGameDataSaveSession(query, target), memory, journal);
            return true;
        }

        private void InvalidatePopupStoreAccount()
        {
            _popupAccountState?.Invalidate(); _popupAccountState = null; _popupAccountBridge = null;
            if (_gameDataTransport is SdkGameDataBackendTransport) UserInfo.SetPopupStoreAccountRecord(null);
        }

        // Runs before every existing owner-specific validation branch, including gacha and attendance.
        private string ValidatePopupStoreAccountSave(GameDataSaveIdentity identity, GameDataSavePayload payload, out bool owned)
        {
            owned = false;
            if (!EnsurePopupStoreAccountState(out var state)) return null; // Unconfigured offline tests preserve their old transport.
            return state.ValidateSave(identity, payload, out owned);
        }
        private void AddCurrentPopupStoreSaveField(Param values)
        {
            if (EnsurePopupStoreAccountState(out var state) && state.Record != null && state.Record.Revision > 0)
            {
                // LatestValues may contain a stale cached popup field. An exact duplicate is harmless;
                // updating the Param here is supported by the SDK's dictionary semantics.
                values[PopupStoreGameDataRecord.FieldName] = state.Record.ToJson();
            }
        }
        private string ValidatePopupStoreOrdinaryBalance(GameDataSavePayload payload)
            => EnsurePopupStoreAccountState(out var state) ? state.ValidateOrdinaryBalance(payload) : null;

        private sealed class BackendPopupClock : IPopupStoreAccountClock
        {
            private readonly BackendManager _owner;
            public BackendPopupClock(BackendManager owner) { _owner = owner; }
            public void Query(Action<DateTime> success, Action<string> failed)
                => _owner.GetServerTimeAsync(success, _ => failed("서버 시간을 확인할 수 없습니다. 영업권을 사용하지 않았습니다."));
        }
    }

    internal sealed class PopupStoreAccountBoundState : IPopupStoreGameDataState, IPopupStoreGameDataOperationJournal
    {
        private readonly Func<bool> _isCurrent;
        private readonly IPopupStoreAccountMemory _memory;
        private readonly IPopupStoreAccountJournalStore _journal;
        private readonly string _rawField;
        private bool _invalid, _rewardReserved, _hasClaimed, _journalBlocked;
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveTarget Target { get; }
        public PopupStoreGameDataRecord Record { get; private set; }
        public PopupStoreGameDataOperation Operation { get; set; }
        public PopupStoreAccountIntent Intent { get; private set; }
        public string Error { get; private set; }
        public string CleanupWarning { get; private set; }
        public bool IsCurrent => !_invalid && _isCurrent();
        public int Diamonds => _memory.Diamonds;
        public bool HasUnknownIntent => Intent != null && !Intent.DefinitelyNotSent && !Intent.Candidate.Matches(Record);

        public PopupStoreAccountBoundState(GameDataRestoreQuery query, GameDataSaveTarget target, string raw,
            Func<bool> current, IPopupStoreAccountMemory memory, IPopupStoreAccountJournalStore journal)
        {
            Query = query; Target = target; _isCurrent = current; _memory = memory; _journal = journal;
            // Read intent even when the account's popup field is malformed. Corruption is not proof of no payout.
            if (!_journal.TryRead(target, out var intent, out var journalError))
            { _journalBlocked = true; Error = journalError; return; }
            Intent = intent;
            try
            {
                var token = JObject.Parse(raw)["rows"]?[0]?[PopupStoreGameDataRecord.FieldName];
                bool present = token != null;
                if (present && (!(token is JObject wrapper) || wrapper.Count != 1 || wrapper["S"]?.Type != JTokenType.String))
                    throw new FormatException();
                _rawField = present ? (string)token["S"] : null;
                if (!PopupStoreGameDataRecord.TryRead(_rawField, present, out var record, out var error))
                { Error = error; return; }
                Record = record; _hasClaimed = record.State == PopupStoreGameDataState.Claimed;
                _memory.SetRecord(present ? record.ToJson() : null);
                if (intent != null && record.Matches(intent.Candidate))
                {
                    // A fresh original row restore already included Dia. Never apply a second delta.
                    if (!_journal.TryClear(target, intent.RequestId, out error)) { _journalBlocked = true; Error = error; return; }
                    Intent = null;
                }
                else if (intent != null && !record.Matches(intent.Source))
                    Error = "현재 서버 영업 기록과 미확정 요청이 다릅니다. 별도 확인이 필요합니다.";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is Newtonsoft.Json.JsonException)
            { Error = "팝업스토어 원본 저장 형식을 확인할 수 없습니다."; }
        }

        public void Invalidate()
        { _invalid = true; if (_rewardReserved) { _memory.ReleaseReward(this); _rewardReserved = false; } }
        public bool ReserveReward(out string error)
        {
            error = null;
            if (_rewardReserved) return true;
            if (!IsCurrent || !_memory.TryReserveReward(this, out error)) return false;
            _rewardReserved = true; return true;
        }
        public void ReleaseUnsentReward()
        { if (_rewardReserved) { _memory.ReleaseReward(this); _rewardReserved = false; } }
        public bool TryCommit(PopupStoreGameDataRecord expected, PopupStoreGameDataRecord next, int rewardDelta, out string error)
        {
            error = null;
            if (!IsCurrent || !expected.Matches(Record) || !expected.IsValidSuccessor(next))
            { error = "계정 또는 현재 영업 기록이 변경되었습니다."; return false; }
            if (rewardDelta > 0 && (!_rewardReserved || !_memory.TryCommitReward(this, rewardDelta, out error))) return false;
            Record = next; _hasClaimed |= rewardDelta > 0; _memory.SetRecord(next.ToJson());
            return true;
        }
        public void Notify()
        {
            if (!_rewardReserved) return;
            try { _memory.NotifyReward(); }
            finally { _memory.ReleaseReward(this); _rewardReserved = false; }
        }
        public bool TryPrepare(GameDataSaveIdentity identity, PopupStoreGameDataRecord source,
            PopupStoreGameDataRecord candidate, GameDataSavePayload payload, out string error)
        {
            error = null;
            if (!IsCurrent || Error != null || !source.Matches(Record)) { error = Error ?? "현재 영업 저장 상태가 아닙니다."; return false; }
            if (!_journal.TryRead(Target, out var existing, out error)) return false;
            if (existing != null && existing.Candidate.Matches(Record))
            {
                if (!_journal.TryClear(Target, existing.RequestId, out error)) return false;
                existing = null; Intent = null; CleanupWarning = null;
            }
            if (existing != null && (!existing.DefinitelyNotSent || !source.Matches(existing.Source) || !candidate.Matches(existing.Candidate)))
            { error = "앞선 저장 결과를 확인 중입니다. 새 요청을 보내지 않습니다."; return false; }
            var intent = new PopupStoreAccountIntent(Target, identity.RequestId, source, candidate, payload.Json);
            if (!_journal.TryWrite(intent, out error)) return false;
            Intent = intent; return true;
        }
        public void Complete(GameDataSaveIdentity identity, PopupStoreGameDataRecord candidate)
        {
            if (!IsCurrent || Intent == null || identity.RequestId != Intent.RequestId || !candidate.Matches(Record))
                throw new InvalidOperationException("확정 복구 기록의 소유권이 다릅니다.");
            if (!_journal.TryClear(Target, identity.RequestId, out var error)) { CleanupWarning = error; return; }
            Intent = null;
        }
        public void RejectBeforeSend(GameDataSaveIdentity identity, PopupStoreGameDataRecord candidate)
        {
            if (Intent == null || identity.RequestId != Intent.RequestId || !candidate.Matches(Intent.Candidate)) return;
            if (candidate.State == PopupStoreGameDataState.Started)
            {
                // No quota and no RNG was committed. The admission host may create a fresh start request.
                if (_journal.TryClear(Target, identity.RequestId, out var clearError)) Intent = null;
                else { _journalBlocked = true; Error = clearError; }
                ReleaseUnsentReward(); return;
            }
            var notSent = new PopupStoreAccountIntent(Target, identity.RequestId, Intent.Source, candidate, Intent.PayloadJson, true);
            if (_journal.TryWrite(notSent, out var error)) Intent = notSent;
            else Error = error;
            ReleaseUnsentReward();
        }
        public string ValidateSave(GameDataSaveIdentity identity, GameDataSavePayload payload, out bool owned)
        {
            owned = Operation != null && !Operation.Committed && !Operation.RejectedBeforeSend && Operation.Identity.Matches(identity);
            if (owned) return Operation.ValidateTransmission(identity, payload);
            if (_journalBlocked || HasUnknownIntent || (Error != null && Intent != null)) return "미확정 팝업스토어 저장을 확인하기 전에는 같은 계정 행을 덮어쓰지 않습니다.";
            if (Record == null)
            {
                var field = JObject.Parse(payload.Json)[PopupStoreGameDataRecord.FieldName];
                return field == null || (field.Type == JTokenType.String && (string)field == _rawField)
                    ? null : "손상된 팝업스토어 기록을 임의 초기화할 수 없습니다.";
            }
            string error = PopupStoreGameDataOperation.ValidateOrdinarySave(payload, Record);
            return error;
        }
        public string ValidateOrdinaryBalance(GameDataSavePayload payload)
        {
            // Called after every protected owner branch, independent of a caller-selected request name.
            if (_hasClaimed)
            {
                var dia = JObject.Parse(payload.Json)["Dia"];
                if (dia != null && (dia.Type != JTokenType.Integer || (long)dia != Diamonds))
                    return "과거 다이아 잔액으로 팝업스토어 지급 결과를 덮어쓸 수 없습니다.";
            }
            return null;
        }
    }

    /// <summary>Existing row + coordinator adapter. Local validation is not server-authoritative gameplay.</summary>
    public sealed class PopupStoreGameDataAccountBridge : IPopupStoreAccountPhaseBridge, IPopupStoreAccountRewardRecovery,
        IPopupStoreBusinessStartPersistence, IPopupStoreBusinessAccessInformation, IPopupStoreAccountProgress
    {
        public const string AccessVersion = "popup-account-v1";
        private readonly PopupStoreAccountBoundState _state;
        private readonly PopupStorePhaseProfileSnapshot _profile;
        private readonly PopupStoreRewardPolicySnapshot _policy;
        private readonly IPopupStoreAccountClock _clock;
        // Frozen per authenticated bridge. Inspector policy edits take effect on a new Play/login.
        private readonly int _dailyBusinessLimit;
        private readonly Func<GameDataSaveCoordinator> _coordinator;
        private readonly PopupStoreRewardAccountSession _account;
        private PopupStoreGameDataRecord _frozenFinal;
        private bool _starting;
        private GameDataSaveCoordinator _activeCoordinator;
        private double _pendingSeconds, _clockSeconds;
        private Action _clockTimeout;
        public PopupStoreAdmissionHost AdmissionHost { get; }
        public bool IsReady => _state.IsCurrent && _state.Record != null && _state.Error == null;
        public string Status => _state.Error ?? (_state.HasUnknownIntent ? "저장 결과 확인 중" : null);
        public PopupStoreGameDataRecord CurrentRecord => _state.Record;
        public PopupStoreGameDataOperation CurrentOperation => _state.Operation;
        public PopupStoreAccountRecoverySnapshot Recovery
        {
            get
            {
                if (!IsReady || AdmissionHost.BusinessSessionId == CurrentRecord.SessionId) return null;
                var record = CurrentRecord;
                if (record.State == PopupStoreGameDataState.Started && _state.Intent?.Candidate.IsRewardPending == true
                    && _state.Intent.Source.Matches(record)) record = _state.Intent.Candidate;
                return record.IsRewardPending || record.State == PopupStoreGameDataState.Started
                    ? new PopupStoreAccountRecoverySnapshot(record.SessionId, record.FinalScore, record.HighestGrade,
                        record.IsRewardPending, record.Diamonds, record.State == PopupStoreGameDataState.Started) : null;
            }
        }

        internal PopupStoreGameDataAccountBridge(PopupStoreAccountBoundState state, PopupStorePhaseProfileSnapshot profile,
            PopupStoreRewardPolicySnapshot policy, IPopupStoreAccountClock clock, Func<GameDataSaveCoordinator> coordinator,
            int dailyLimit = 1)
        {
            if (dailyLimit < 1 || dailyLimit > PopupStoreGameDataRecord.MaximumDailyLimit)
                throw new ArgumentOutOfRangeException(nameof(dailyLimit));
            _dailyBusinessLimit = dailyLimit;
            _state = state; _profile = profile; _policy = policy; _clock = clock; _coordinator = coordinator;
            _account = new PopupStoreRewardAccountSession(state.Target.AccountInDate);
            AdmissionHost = new PopupStoreAdmissionHost(this, this, () => IsReady ? _account : null,
                maximumDailyLimit: dailyLimit);
        }
        public void QueryAccess(PopupStoreRewardAccountSession session, Action<PopupStoreBusinessAccessSnapshot> completed)
        {
            if (!IsReady || !ReferenceEquals(session, _account) || _state.HasUnknownIntent)
            { completed(PopupStoreBusinessAccessSnapshot.Failure(session, Status ?? "계정 영업 기록을 확인할 수 없습니다.")); return; }
            ReadClock(utc => {
                string day = AttendanceProgress.GameDay(utc.AddHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                int dayOrder = string.CompareOrdinal(day, CurrentRecord.DayKey);
                if (_dailyBusinessLimit > 1 && CurrentRecord.UsedCount != 0 && dayOrder < 0)
                { completed(PopupStoreBusinessAccessSnapshot.Failure(session, "서버 영업일이 저장 기록보다 이전입니다. 기록을 초기화하지 않습니다.")); return; }
                int used = CurrentRecord.UsedCount != 0 && dayOrder <= 0 ? CurrentRecord.UsedCount : 0;
                if (CurrentRecord.IsRewardPending || CurrentRecord.State == PopupStoreGameDataState.Started || _state.Intent != null)
                {
                    // More tickets never authorize abandoning an unresolved business.
                    // Do not misreport a pending run as 200 consumed tickets.
                    if (_dailyBusinessLimit > 1)
                    { completed(PopupStoreBusinessAccessSnapshot.Failure(session, "진행 중 영업 또는 미확정 보상을 먼저 완료해 주세요.")); return; }
                    used = Math.Max(1, used);
                }
                completed(new PopupStoreBusinessAccessSnapshot(session, day, AccessVersion, _dailyBusinessLimit, used, new DateTimeOffset(utc)));
            }, error => completed(PopupStoreBusinessAccessSnapshot.Failure(session, error)));
        }
        public void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request, Action<PopupStoreBusinessStartResponse> completed)
        {
            if (!ReferenceEquals(session, _account)) { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Conflict)); return; }
            TryStartBusiness(request, AdmissionHost.LocalSessionId, completed);
        }
        public void TryStartBusiness(PopupStoreBusinessStartRequest request, string localSessionId, Action<PopupStoreBusinessStartResponse> completed)
        {
            if (!IsReady || request == null || !request.TryValidate(out _) || request.AccountId != _account.AccountId
                || request.PolicyVersion != AccessVersion || request.Mode != PopupStoreBusinessMode.Rewarded || string.IsNullOrWhiteSpace(localSessionId))
            { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: "현재 계정의 영업 시작 요청이 아닙니다.")); return; }
            if (CurrentRecord.StartRequestId == request.StartRequestId)
            { completed(StartResponse(request)); return; }
            if (_starting || _state.HasUnknownIntent) { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate, error: "앞선 시작 결과 확인 중")); return; }
            _starting = true;
            ReadClock(utc => {
                _starting = false;
                PopupStoreGameDataRecord next;
                string error = null;
                var prior = _state.Intent;
                if (prior != null && prior.DefinitelyNotSent && prior.Source.Matches(CurrentRecord)
                    && prior.Candidate.State == PopupStoreGameDataState.Started && prior.Candidate.StartRequestId == request.StartRequestId)
                    next = prior.Candidate;
                else if (!CurrentRecord.TryStart(utc, request.StartRequestId, localSessionId, _profile.ProfileId, _profile.Version,
                    _dailyBusinessLimit, out next, out error))
                { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: error)); return; }
                Execute(next, response => completed(response.State == PopupStoreAccountClaimState.Confirmed ? StartResponse(request)
                    : new PopupStoreBusinessStartResponse(request, response.State == PopupStoreAccountClaimState.Retryable
                        ? PopupStoreBusinessStartResponseKind.RejectedBeforeSend : PopupStoreBusinessStartResponseKind.Indeterminate, error: response.Message)));
            }, error => { _starting = false; completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: error)); });
        }
        public void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request, Action<PopupStoreBusinessStartResponse> completed)
        {
            if (!IsReady || !ReferenceEquals(session, _account)) { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Conflict)); return; }
            completed(StartResponse(request)); // No speculative re-send or "NotApplied" inference.
        }
        private PopupStoreBusinessStartResponse StartResponse(PopupStoreBusinessStartRequest request)
        {
            if (CurrentRecord.StartRequestId != request.StartRequestId || CurrentRecord.State != PopupStoreGameDataState.Started)
                return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate, error: "시작 저장 결과를 확인 중입니다.");
            var startUtc = new DateTimeOffset(CurrentRecord.StartedAtUtc);
            return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Started,
                new PopupStoreBusinessStartRecord(request, CurrentRecord.SessionId, CurrentRecord.DayKey, startUtc, CurrentRecord.UsedCount));
        }
        public void PrepareResult(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed)
        {
            if (!IsReady || session == null || !ReferenceEquals(session.FinalResult, result)
                || result.BusinessSessionId != CurrentRecord.SessionId || result.ProfileId != _profile.ProfileId
                || result.ProfileVersion != _profile.Version)
            { completed(Response(PopupStoreAccountClaimState.Rejected, "원래 영업의 확정 결과를 확인할 수 없습니다.")); return; }
            if (CurrentRecord.IsTerminal)
            {
                bool same = CurrentRecord.FinalScore == result.TotalScore && CurrentRecord.HighestGrade == result.HighestClearedGrade
                    && CurrentRecord.SuccessfulOrders == result.SuccessfulOrders && CurrentRecord.EndReason == result.EndReason;
                completed(Response(same ? PopupStoreAccountClaimState.Confirmed : PopupStoreAccountClaimState.Rejected,
                    same ? null : "저장된 결과와 현재 화면의 결과가 다릅니다.")); return;
            }
            if (_frozenFinal != null && _frozenFinal.SessionId != CurrentRecord.SessionId) _frozenFinal = null;
            if (_frozenFinal == null)
            {
                var prior = _state.Intent;
                if (prior != null && prior.Source.Matches(CurrentRecord) && prior.Candidate.IsTerminal)
                    _frozenFinal = prior.Candidate;
                else if (!CurrentRecord.TryFinalize(session, _policy,
                    (result.EndReason == PopupStorePhaseEndReason.Completed || result.EndReason == PopupStorePhaseEndReason.TimedOut)
                        && result.HighestClearedGrade >= PopupStoreGradeTier.C ? UnityEngine.Random.Range(0, 1000) : 0,
                    NewId("result"), out _frozenFinal, out string error))
                { completed(Response(PopupStoreAccountClaimState.Rejected, error)); return; }
            }
            Execute(_frozenFinal, completed);
        }
        public void FinalizeAndClaim(PopupStorePhaseSession session, PopupStorePhaseResult result, Action<PopupStoreAccountClaimResult> completed)
        {
            PrepareResult(session, result, prepared => {
                if (prepared.State != PopupStoreAccountClaimState.Confirmed) { completed(prepared); return; }
                ClaimFixed(completed);
            });
        }
        public void ClaimRecovery(Action<PopupStoreAccountClaimResult> completed)
        {
            if (!IsReady) { completed(Response(PopupStoreAccountClaimState.Rejected, Status)); return; }
            if (CurrentRecord.State == PopupStoreGameDataState.Started)
            {
                var prior = _state.Intent;
                if (prior != null && prior.Source.Matches(CurrentRecord) && prior.Candidate.IsTerminal)
                { Execute(prior.Candidate, outcome => { if (outcome.State == PopupStoreAccountClaimState.Confirmed) ClaimFixed(completed); else completed(outcome); }); return; }
                if (!CurrentRecord.TryAbandon(CurrentRecord.SessionId, NewId("interrupted"), out var abandoned, out string error))
                { completed(Response(PopupStoreAccountClaimState.Rejected, error)); return; }
                Execute(abandoned, completed); return;
            }
            ClaimFixed(completed);
        }
        private void ClaimFixed(Action<PopupStoreAccountClaimResult> completed)
        {
            if (!IsReady) { completed(Response(PopupStoreAccountClaimState.Rejected, Status)); return; }
            if (CurrentRecord.State == PopupStoreGameDataState.Claimed || CurrentRecord.State == PopupStoreGameDataState.NoReward
                || CurrentRecord.State == PopupStoreGameDataState.Abandoned) { completed(Response(PopupStoreAccountClaimState.Confirmed, null)); return; }
            var prior = _state.Intent;
            PopupStoreGameDataRecord next;
            if (prior != null && prior.Source.Matches(CurrentRecord) && prior.Candidate.State == PopupStoreGameDataState.Claimed) next = prior.Candidate;
            else if (!CurrentRecord.TryClaim(NewId("claim"), out next, out string error))
            { completed(Response(PopupStoreAccountClaimState.Rejected, error)); return; }
            Execute(next, completed);
        }
        private void Execute(PopupStoreGameDataRecord candidate, Action<PopupStoreAccountClaimResult> completed)
        {
            if (!IsReady) { completed(Response(PopupStoreAccountClaimState.Rejected, Status)); return; }
            if (CurrentRecord.Matches(candidate)) { completed(Response(PopupStoreAccountClaimState.Confirmed, null)); return; }
            if (_state.HasUnknownIntent) { completed(Response(PopupStoreAccountClaimState.Pending, "저장 결과 확인 중입니다. 재전송·재추첨하지 않습니다.")); return; }
            var coordinator = _coordinator();
            if (coordinator == null || !coordinator.CanStartPurchase)
            { completed(Response(PopupStoreAccountClaimState.Retryable, "다른 저장이 끝난 뒤 다시 확인해 주세요.")); return; }
            if (candidate.State == PopupStoreGameDataState.Claimed && !_state.ReserveReward(out string reserveError))
            { completed(Response(PopupStoreAccountClaimState.Retryable, reserveError)); return; }
            var identity = new GameDataSaveIdentity(candidate.LastRequestId + ":attempt:" + Guid.NewGuid().ToString("N"), _state.Target);
            var operation = new PopupStoreGameDataOperation(identity, CurrentRecord, candidate, coordinator, _state, _state);
            _activeCoordinator = coordinator; _pendingSeconds = 0;
            _state.Operation = operation;
            bool notified = false;
            Action changed = () => {
                if (!IsReady || notified) return;
                if (operation.Committed || operation.RejectedBeforeSend || operation.Status == GameDataSaveRequestStatus.Indeterminate
                    || operation.Status == GameDataSaveRequestStatus.InvalidatedAfterSend || operation.Status == GameDataSaveRequestStatus.LocalCompletionFailed)
                {
                    notified = true;
                    if (operation.RejectedBeforeSend) _state.ReleaseUnsentReward();
                    completed(Response(operation.Committed ? PopupStoreAccountClaimState.Confirmed
                        : operation.RejectedBeforeSend ? PopupStoreAccountClaimState.Retryable : PopupStoreAccountClaimState.Pending,
                        operation.Error));
                }
            };
            operation.Changed += changed;
            bool accepted = operation.TryStart(out string error);
            changed();
            if (!accepted && !notified) { _state.ReleaseUnsentReward(); completed(Response(PopupStoreAccountClaimState.Retryable, error)); }
        }
        private PopupStoreAccountClaimResult Response(PopupStoreAccountClaimState state, string message)
            => new PopupStoreAccountClaimResult(state, CurrentRecord?.SessionId, CurrentRecord?.Diamonds ?? 0, message);
        private static string NewId(string purpose) => "popup:" + purpose + ":" + Guid.NewGuid().ToString("N");
        public void ObserveElapsed(double elapsed)
        {
            if (!IsReady || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) return;
            if (_clockTimeout != null && (_clockSeconds += elapsed) >= 10) { var timeout = _clockTimeout; _clockTimeout = null; timeout(); }
            var operation = _state.Operation;
            if (operation != null && !operation.Committed && operation.Status == GameDataSaveRequestStatus.Sending
                && (_pendingSeconds += elapsed) >= 10)
                _activeCoordinator?.MarkResponseMissing(operation.Identity, "저장 응답 확인 중입니다. 같은 요청을 다시 보내지 않습니다.");
        }
        private void ReadClock(Action<DateTime> success, Action<string> failed)
        {
            if (_clockTimeout != null) { failed("앞선 서버 시각 조회가 끝난 뒤 다시 확인해 주세요."); return; }
            bool returned = false;
            _clockSeconds = 0;
            _clockTimeout = () => { if (!returned) { returned = true; failed("서버 시간 조회가 지연됐습니다. 영업권은 사용하지 않았습니다."); } };
            try { _clock.Query(utc => {
                if (returned) return; returned = true; _clockTimeout = null;
                if (!IsReady || utc.Kind != DateTimeKind.Utc || utc < DateTime.MinValue.AddHours(3)
                    || utc > DateTime.MaxValue.AddHours(-9)) { failed("현재 계정의 서버 UTC 시각을 확인할 수 없습니다."); return; }
                success(utc);
            }, error => { if (!returned) { returned = true; _clockTimeout = null; failed(error); } }); }
            catch (Exception ex) { if (!returned) { returned = true; _clockTimeout = null; failed("서버 시각 조회 실패: " + ex.GetType().Name); } }
        }
    }
}
