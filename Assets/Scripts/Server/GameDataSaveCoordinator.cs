using System;
using System.Collections.Generic;
using BackEnd;

namespace Muks.BackEnd
{
    /// <summary>
    /// 메인 스레드/고정된 인증·복원 세션의 GameData 저장 순서 관리 객체.
    /// 일반 저장은 FIFO, 연속된 대기 자동 저장만 병합한다. 구매 계산/지급은 호출자 책임이다.
    /// </summary>
    public sealed class GameDataSaveCoordinator
    {
        // 준비부터 완료 처리까지 대상 소유권을 유지한다. 미해결 전송은 강한 참조로 남긴다.
        // 메인 스레드 전용. 재로그인/새 조정기로 이전 전송의 소유권을 우회할 수 없다.
        private static readonly HashSet<GameDataSaveCoordinator> BlockedCoordinators =
            new HashSet<GameDataSaveCoordinator>();

        private readonly GameDataSaveTarget _target;
        private readonly GameDataSingleUpdate _updater;
        private readonly Func<Param> _createLatestAutosave;
        private readonly Action<GameDataSaveReceipt> _onAutosaveConfirmed;
        private readonly Func<bool> _isSessionCurrent;
        private readonly HashSet<string> _usedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly string _requestPrefix = "save:" + Guid.NewGuid().ToString("N") + ":";
        private readonly LinkedList<SaveBatch> _pending = new LinkedList<SaveBatch>();
        private long _requestNumber;
        private bool _pumping;
        private bool _invalidated;
        private bool _autosaveNeeded;
        private bool _sendInvoked;
        private SaveBatch _active;
        private GameDataMailSaveLease _mailLease;
        internal StaffMigrationExecution ActiveStaffMigration => _active?.Migration;
        internal StaffGachaPurchaseExecution ActiveStaffPurchase => _active?.Purchase;
        internal FirstTutorialExecution ActiveFirstTutorial => _active?.Tutorial;
        internal QuestStaffGrantExecution ActiveQuestStaffGrant => _active?.QuestStaffGrant;

        private sealed class SaveBatch
        {
            public readonly GameDataSaveIdentity Identity;
            public readonly bool IsAutosave;
            public readonly Func<bool> AutosaveGuard;
            public readonly StaffMigrationExecution Migration;
            public readonly StaffGachaPurchaseExecution Purchase;
            public readonly FirstTutorialExecution Tutorial;
            public readonly QuestStaffGrantExecution QuestStaffGrant;
            public Func<Param> Factory;
            public readonly List<GameDataSaveRequest> Requests = new List<GameDataSaveRequest>();
            public SaveBatch(GameDataSaveIdentity identity, bool isAutosave, Func<Param> factory,
                Func<bool> autosaveGuard = null, StaffMigrationExecution migration = null, StaffGachaPurchaseExecution purchase = null, FirstTutorialExecution tutorial = null,
                QuestStaffGrantExecution questStaffGrant = null)
            { Identity = identity; IsAutosave = isAutosave; Factory = factory; AutosaveGuard = autosaveGuard; Migration = migration; Purchase = purchase; Tutorial = tutorial; QuestStaffGrant = questStaffGrant; }
        }

        private GameDataSaveCoordinatorState _state = GameDataSaveCoordinatorState.Idle;
        public GameDataSaveCoordinatorState State
        {
            get => _state;
            private set
            {
                _state = value;
                if (value == GameDataSaveCoordinatorState.Idle) BlockedCoordinators.Remove(this);
                else BlockedCoordinators.Add(this);
            }
        }
        public bool HasPendingAutosave
        {
            get
            {
                if (_autosaveNeeded) return true;
                foreach (SaveBatch batch in _pending) if (batch.IsAutosave) return true;
                return false;
            }
        }
        public bool CanStartPurchase => !_invalidated && State == GameDataSaveCoordinatorState.Idle
            && _pending.Count == 0 && !HasPendingAutosave && !_pumping && !IsTargetOwnedByOther(_target, this);
        public GameDataSaveIdentity CurrentIdentity { get; private set; }
        public GameDataSavePayload CurrentPayload { get; private set; }
        public GameDataSaveReceipt LastReceipt { get; private set; }
        public string LastError { get; private set; }

        /// <summary>
        /// 같은 계정/행을 준비·전송·완료 처리 중이거나 미해결 상태이면 다른 writer를 차단한다.
        /// 자기 전송 gate는 IsTargetOwnedByOther를 사용한다. 서버/앱 재시작 간 잠금은 아니다.
        /// </summary>
        public static bool IsTargetBlocked(GameDataSaveTarget target)
            => IsTargetOwnedByOther(target, null);

        public static bool IsTargetOwnedByOther(GameDataSaveTarget target, GameDataSaveCoordinator owner)
        {
            if (target == null) return false;
            foreach (GameDataSaveCoordinator coordinator in BlockedCoordinators)
                if (!ReferenceEquals(coordinator, owner) && coordinator._target.Matches(target)) return true;
            return false;
        }

        internal static bool IsStaffMigrationTargetProtected(GameDataSaveTarget target)
        {
            if (target == null) return false;
            foreach (GameDataSaveCoordinator coordinator in BlockedCoordinators)
                if (coordinator.ActiveStaffMigration != null && coordinator._target.Matches(target)) return true;
            return false;
        }

        internal static bool IsStaffPurchaseTargetProtected(GameDataSaveTarget target)
        {
            if (target == null) return false;
            foreach (GameDataSaveCoordinator coordinator in BlockedCoordinators)
                if (coordinator.ActiveStaffPurchase != null && coordinator._target.Matches(target)) return true;
            return false;
        }

        internal static bool IsFirstTutorialTargetProtected(GameDataSaveTarget target)
        {
            if (target == null) return false;
            foreach (var owner in BlockedCoordinators)
                if (owner.ActiveFirstTutorial != null && owner._target.Matches(target)) return true;
            return false;
        }

        internal GameDataSaveRequest StartFirstTutorial(FirstTutorialExecution tutorial)
        {
            if (!EnsureSession() || !CanStartPurchase) return GameDataSaveRequest.Rejected("저장 작업 완료를 기다려 주세요.");
            var batch = new SaveBatch(tutorial.Identity, false, tutorial.CreateValuesUnderProtection, tutorial: tutorial);
            var request = new GameDataSaveRequest(tutorial.Identity, tutorial.ConfirmGameData, null);
            tutorial.AttachRequest(request); batch.Requests.Add(request); _pending.AddLast(batch); Pump(); return request;
        }

        internal static bool IsQuestStaffGrantTargetProtected(GameDataSaveTarget target)
        {
            if (target == null) return false;
            foreach (var owner in BlockedCoordinators)
                if (owner.ActiveQuestStaffGrant != null && owner._target.Matches(target)) return true;
            return false;
        }
        internal GameDataSaveRequest StartQuestStaffGrant(QuestStaffGrantExecution operation)
        {
            if (!EnsureSession() || !CanStartPurchase) return GameDataSaveRequest.Rejected("다른 작업의 완료를 기다려 주세요.");
            var batch = new SaveBatch(operation.Identity, false, operation.CreateValuesUnderProtection, questStaffGrant: operation);
            var request = new GameDataSaveRequest(operation.Identity, operation.Confirm, null);
            operation.AttachRequest(request); batch.Requests.Add(request); _pending.AddLast(batch); Pump(); return request;
        }

        internal void FinishFirstTutorialStage(FirstTutorialExecution tutorial, bool success, string error)
        {
            if (!ReferenceEquals(ActiveFirstTutorial, tutorial) || State != GameDataSaveCoordinatorState.WaitingForTutorialStage || !EnsureSession()) return;
            if (!success) { LastError = error; State = GameDataSaveCoordinatorState.Indeterminate; return; }
            _active = null; _sendInvoked = false; LastError = null; State = GameDataSaveCoordinatorState.Idle; Pump();
        }

        // Unlike ordinary saves/migrations, purchases never wait for a FIFO slot. Protection precedes RNG.
        internal GameDataSaveRequest StartStaffPurchase(StaffGachaPurchaseExecution purchase)
        {
            if (!EnsureSession() || !CanStartPurchase)
                return GameDataSaveRequest.Rejected("다른 작업이 진행 중입니다. 잠시 후 다시 시도해 주세요.");
            var batch = new SaveBatch(purchase.Identity, false, purchase.CreateValuesUnderProtection, purchase: purchase);
            var request = new GameDataSaveRequest(purchase.Identity, purchase.Confirm, purchase.OnSaveStateChanged);
            purchase.AttachRequest(request);
            batch.Requests.Add(request);
            _pending.AddLast(batch);
            Pump();
            return request;
        }

        public GameDataSaveCoordinator(GameDataSaveTarget target, GameDataSingleUpdate updater,
            Func<Param> createLatestAutosave, Action<GameDataSaveReceipt> onAutosaveConfirmed = null,
            Func<bool> isSessionCurrent = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));
            _createLatestAutosave = createLatestAutosave ?? throw new ArgumentNullException(nameof(createLatestAutosave));
            _onAutosaveConfirmed = onAutosaveConfirmed;
            _isSessionCurrent = isSessionCurrent;
            _updater.BindOwner(this);
        }

        /// <summary>
        /// 우편 SDK 호출보다 먼저 같은 복원 세션/행의 저장 순서를 예약한다.
        /// 예약 중 접수된 저장은 기존 FIFO에 남고, 확정 종료 후에만 자료를 생성/전송한다.
        /// </summary>
        internal bool TryReserveForMail(GameDataRestoreQuery query, Func<bool> isQueryCurrent,
            out GameDataMailSaveLease lease, out string error)
        {
            lease = null;
            error = null;
            if (query == null || isQueryCurrent == null || !_target.IsValid
                || !string.Equals(query.AccountInDate, _target.AccountInDate, StringComparison.Ordinal))
            { error = "우편 수령에 필요한 현재 계정·복원 행 근거가 없습니다."; return false; }
            if (!EnsureSession() || !ReadCurrent(isQueryCurrent) || !EnsureSession())
            { error = "우편 수령 전 인증·복원 세션이 변경되었습니다."; return false; }
            if (!CanStartPurchase || _mailLease != null)
            { error = "진행 중이거나 미해결인 저장이 있어 우편을 수령할 수 없습니다."; return false; }
            _mailLease = new GameDataMailSaveLease(this, query, _target, isQueryCurrent);
            State = GameDataSaveCoordinatorState.ReservedForMail;
            LastError = null;
            lease = _mailLease;
            return true;
        }

        internal bool IsMailLeaseCurrent(GameDataMailSaveLease lease)
        {
            if (lease == null || !ReferenceEquals(_mailLease, lease)
                || State != GameDataSaveCoordinatorState.ReservedForMail || !EnsureSession()) return false;
            if (!ReadCurrent(lease.IsQueryCurrent))
            {
                InvalidateSession();
                return false;
            }
            return EnsureSession() && ReferenceEquals(_mailLease, lease)
                && State == GameDataSaveCoordinatorState.ReservedForMail;
        }

        internal bool TryMarkMailRequestStarted(GameDataMailSaveLease lease)
        {
            if (!IsMailLeaseCurrent(lease) || lease.IsIndeterminate) return false;
            // 동일 고정 ID batch의 다음 우편은 호출자가 순차 응답을 확인한 뒤 시작한다.
            lease.HasRequestStarted = true;
            return true;
        }

        internal void MarkMailIndeterminate(GameDataMailSaveLease lease, string reason)
        {
            if (!IsMailLeaseCurrent(lease) || !lease.HasRequestStarted) return;
            lease.IsIndeterminate = true;
            lease.Error = reason ?? "우편 수령 결과를 확인하지 못했습니다. 저장 예약을 유지합니다.";
            LastError = lease.Error;
            // 미확정은 미반영 실패가 아니다. FIFO도 소유권도 해제하지 않는다.
        }

        internal bool TryReleaseMail(GameDataMailSaveLease lease, bool knownOutcome)
        {
            if (!IsMailLeaseCurrent(lease) || knownOutcome != lease.HasRequestStarted) return false;
            _mailLease = null; // 후속 factory/콜백 재진입보다 먼저 이전 lease를 종료한다.
            LastError = null;
            State = GameDataSaveCoordinatorState.Idle;
            Pump();
            return true;
        }

        private static bool ReadCurrent(Func<bool> predicate)
        {
            try { return predicate(); }
            catch { return false; }
        }

        /// <summary>일반 값 저장. 순서가 왔을 때만 factory를 실행하며 구매를 재계산하지 않는다.</summary>
        public GameDataSaveRequest EnqueueSave(Func<Param> factory,
            Action<GameDataSaveReceipt> onConfirmed = null, Action<GameDataSaveRequest> onStateChanged = null)
        {
            return Enqueue(factory, false, onConfirmed, onStateChanged, null);
        }

        // Only a validated Backend migration intent can obtain this protected FIFO slot.
        internal GameDataSaveRequest EnqueueStaffMigration(StaffMigrationExecution migration) =>
            Enqueue(migration.CreateValuesUnderProtection, false, migration.Confirm, null, null, migration);

        /// <summary>맨 뒤의 미전송 자동 저장만 병합한다. 중간의 부분 갱신을 앞지르지 않는다.</summary>
        public GameDataSaveRequest RequestAutosave(Action<GameDataSaveReceipt> onConfirmed = null,
            Action<GameDataSaveRequest> onStateChanged = null, Func<bool> canCreateValues = null)
        {
            return Enqueue(_createLatestAutosave, true, onConfirmed, onStateChanged, canCreateValues);
        }

        private GameDataSaveRequest Enqueue(Func<Param> factory, bool autosave,
            Action<GameDataSaveReceipt> onConfirmed, Action<GameDataSaveRequest> onStateChanged,
            Func<bool> autosaveGuard, StaffMigrationExecution migration = null)
        {
            if (!EnsureSession()) return GameDataSaveRequest.Rejected("인증·복원 세션이 무효화되었습니다.");
            string error = factory == null ? "저장 자료 생성 함수가 필요합니다."
                : !_target.IsValid ? "저장 대상이 유효하지 않습니다."
                : IsStopped ? "선행 저장 결과가 미해결 상태입니다."
                : IsTargetOwnedByOther(_target, this) ? "같은 GameData 행을 다른 저장 작업이 소유하고 있습니다." : null;
            if (error != null)
            {
                var rejected = GameDataSaveRequest.Rejected(error);
                rejected.OnStateChanged = onStateChanged;
                Notify(rejected);
                rejected.ClearCallbacks();
                return rejected;
            }

            SaveBatch batch = autosave && _pending.Last != null && _pending.Last.Value.IsAutosave
                && Equals(_pending.Last.Value.AutosaveGuard, autosaveGuard)
                ? _pending.Last.Value : null;
            if (batch == null)
            {
                batch = new SaveBatch(migration?.Identity ?? NextIdentity(), autosave, factory, autosaveGuard, migration);
                _pending.AddLast(batch);
            }
            var request = new GameDataSaveRequest(batch.Identity, onConfirmed, onStateChanged);
            batch.Requests.Add(request);
            Pump();
            return request;
        }

        /// <summary>
        /// false면 시작을 수락하지 않았다. true도 전송/저장 성공을 뜻하지 않는다(LastReceipt 확인).
        /// factory는 수락 가능한 상태에서만 호출하므로 오래 대기한 구매 계산안이 전송되지 않는다.
        /// 실제 연결 시 factory에서 현재 스냅샷으로 기존 RequestSession/PurchasePlan을 사용해야 한다.
        /// </summary>
        public bool TryStartPurchase(GameDataSaveIdentity identity, Func<Param> createValues,
            Action<GameDataSaveReceipt> onConfirmed, out string error)
        {
            error = null;
            if (!EnsureSession()) { error = "인증·복원 세션이 무효화되었습니다."; return false; }
            if (!CanStartPurchase) { error = "선행 저장 또는 확정 처리가 끝나지 않았습니다."; return false; }
            if (createValues == null || onConfirmed == null)
            { error = "최신 구매 자료 생성과 성공 확정 처리 콜백이 필요합니다."; return false; }
            if (identity == null || !identity.IsValid || !_target.Matches(identity.Target) || _usedIds.Contains(identity.RequestId))
            { error = "저장 대상/요청 ID가 잘못되었거나 이 조정기에서 이미 사용되었습니다."; return false; }
            var batch = new SaveBatch(identity, false, createValues);
            var request = new GameDataSaveRequest(identity, onConfirmed, null);
            batch.Requests.Add(request);
            _pending.AddLast(batch);
            Pump();
            error = request.Error;
            return request.Status != GameDataSaveRequestStatus.RejectedBeforeSend;
        }

        /// <summary>Param을 보관하지 않고 dirty 표시만 합친다. 실패/미확정의 재전송이나 잠금 해제 수단이 아니다.</summary>
        public bool MarkAutosaveNeeded()
        {
            if (!EnsureSession()) return false;
            if (IsStopped) { _autosaveNeeded = true; return false; }
            GameDataSaveRequest request = RequestAutosave();
            return request.Accepted && request.Status != GameDataSaveRequestStatus.RejectedBeforeSend;
        }

        /// <summary>전송 전 대기는 폐기하지만 이미 전송된 원본/소유권은 지우지 않는다.</summary>
        public void InvalidateSession()
        {
            if (_invalidated) return;
            _invalidated = true;
            _autosaveNeeded = false;
            foreach (SaveBatch batch in _pending) AbandonBeforeSend(batch, "이전 인증·복원 세션의 대기 작업입니다.");
            _pending.Clear();
            if (_mailLease != null)
            {
                if (_mailLease.HasRequestStarted)
                {
                    _mailLease.IsIndeterminate = true;
                    _mailLease.Error = "우편 요청 후 인증·복원 세션이 변경되었습니다. 이전 수령의 저장 소유권을 유지합니다.";
                    LastError = _mailLease.Error;
                    State = GameDataSaveCoordinatorState.InvalidatedAfterSend;
                }
                else
                {
                    // SDK 호출이 없었던 예약만 안전하게 폐기한다. 전송된 예약은 이 경로로 해제하지 않는다.
                    _mailLease = null;
                    State = GameDataSaveCoordinatorState.Idle;
                }
                return;
            }
            if (_active == null) return;
            if (!_sendInvoked)
            {
                AbandonBeforeSend(_active, "전송 전 인증·복원 세션이 변경되었습니다.");
                _active = null;
                State = GameDataSaveCoordinatorState.Idle;
                return;
            }
            State = GameDataSaveCoordinatorState.InvalidatedAfterSend;
            LastError = "전송 이후 인증·복원 세션이 변경되었습니다. 현재 상태에 적용하거나 후속 저장하지 않습니다.";
            _active.Factory = null;
            foreach (GameDataSaveRequest request in _active.Requests)
            {
                if (request.Status != GameDataSaveRequestStatus.SuccessConfirmed)
                    request.SetState(GameDataSaveRequestStatus.InvalidatedAfterSend, LastReceipt, LastError);
                request.ClearCallbacks();
            }
        }

        /// <summary>
        /// 외부에서 응답 유실을 알릴 수 있다. 타이머/조회/재시도는 만들지 않으며, 전송 안 됨으로 바꾸지 않는다.
        /// 향후 수동 대조/해소 연결점: 단순 재조회 값은 종료 증거가 아니다. 임의 Reset/실패 확정 API는 없다.
        /// 원래 요청의 실제 성공 콜백이 뒤늦게 도착하면 그 동일 ID/행/계정 응답만 수용한다.
        /// </summary>
        public bool MarkResponseMissing(GameDataSaveIdentity identity, string reason)
        {
            if (State != GameDataSaveCoordinatorState.Sending || !MatchesCurrent(identity)) return false;
            HandleReceipt(GameDataSaveReceipt.Unknown(CurrentIdentity, CurrentPayload, reason ?? "응답이 확인되지 않았습니다."));
            return true;
        }

        private bool IsStopped => State == GameDataSaveCoordinatorState.Indeterminate
            || State == GameDataSaveCoordinatorState.LocalCompletionFailed
            || State == GameDataSaveCoordinatorState.InvalidatedAfterSend;

        private bool EnsureSession()
        {
            if (_invalidated) return false;
            bool current;
            // An external coordinator must inherit its production updater's frozen session too.
            // A caller-provided predicate cannot revive an updater from an older authentication/query.
            try { current = _updater.IsSessionCurrent() && (_isSessionCurrent == null || _isSessionCurrent()); }
            catch { current = false; }
            if (!current) InvalidateSession();
            return current;
        }

        private GameDataSaveIdentity NextIdentity()
            => new GameDataSaveIdentity(_requestPrefix + checked(++_requestNumber), _target);

        private bool PrepareAndSend(SaveBatch batch)
        {
            if (!EnsureSession()) { AbandonBeforeSend(batch, "이전 세션의 저장입니다."); return false; }
            if (IsTargetOwnedByOther(_target, this))
            { RejectBeforeSend(batch, "다른 저장 작업이 같은 GameData 행을 소유하고 있습니다."); return false; }
            if (batch.Migration != null)
            {
                try
                {
                    if (!batch.Migration.PrepareBeforeProtection(out string preparationError))
                    { RejectBeforeSend(batch, preparationError); return false; }
                }
                catch (Exception ex)
                { RejectBeforeSend(batch, "이전 준비 확인 실패: " + ex.GetType().Name); return false; }
            }
            if (!EnsureSession()) { AbandonBeforeSend(batch, "이전 준비 중 세션이 무효화되었습니다."); return false; }
            _active = batch;
            _sendInvoked = false;
            CurrentIdentity = batch.Identity;
            CurrentPayload = null;
            LastReceipt = null;
            State = GameDataSaveCoordinatorState.Preparing;
            if (!_updater.CanSend(batch.Identity, out string error))
            { RejectBeforeSend(batch, error); return false; }
            if (!EnsureSession()) return false;
            if (!CheckAutosaveGuard(batch, out error)) { RejectBeforeSend(batch, error); return false; }
            if (!EnsureSession()) return false;
            GameDataSavePayload payload;
            try
            {
                if (!GameDataSavePayload.TryCapture(batch.Factory(), out payload, out error))
                { RejectBeforeSend(batch, error); return false; }
            }
            catch (Exception ex)
            {
                RejectBeforeSend(batch, "전송 전 자료 생성 실패: " + ex.Message);
                return false;
            }
            if (!EnsureSession()) return false;
            if (!CheckAutosaveGuard(batch, out error)) { RejectBeforeSend(batch, error); return false; }
            if (!EnsureSession()) return false;
            CurrentPayload = payload;
            batch.Factory = null; // 고정 자료만 남긴다. 같은 작업의 자료를 재생성/재전송하지 않는다.
            _usedIds.Add(batch.Identity.RequestId);
            LastError = null;
            State = GameDataSaveCoordinatorState.Sending;
            foreach (GameDataSaveRequest request in batch.Requests)
                request.SetState(GameDataSaveRequestStatus.Sending, null, null);
            if (!CheckAutosaveGuard(batch, out error)) { RejectBeforeSend(batch, error); return false; }
            if (!EnsureSession()) return false;
            _sendInvoked = true; // 이후 전송 전 거절 receipt가 오면 미전송임을 다시 확정할 수 있다.
            _updater.Send(batch.Identity, payload, HandleReceipt);
            if (LastReceipt != null && batch.Identity.Matches(LastReceipt.Identity)
                && LastReceipt.Disposition == GameDataSaveDisposition.RejectedBeforeSend)
                return false;
            return true;
        }

        private void HandleReceipt(GameDataSaveReceipt receipt)
        {
            // A later Stage failure does not reopen the already committed GameData phase.
            if (_active?.Tutorial?.CoreCommitted == true) return;
            if (receipt == null || _active == null || !MatchesCurrent(receipt.Identity) ||
                (State != GameDataSaveCoordinatorState.Sending && State != GameDataSaveCoordinatorState.Indeterminate
                    && State != GameDataSaveCoordinatorState.InvalidatedAfterSend)) return;
            if (!EnsureSession())
            {
                // Send 내부 마지막 gate의 거절이면 실제 미전송이 입증되므로 그때만 소유권 해제.
                if (!receipt.SendStarted && receipt.Disposition == GameDataSaveDisposition.RejectedBeforeSend)
                { RejectBeforeSend(_active, receipt.Reason); return; }
                if (LastReceipt?.Disposition == GameDataSaveDisposition.SuccessConfirmed) return;
                if (LastReceipt?.RawResponse == null || receipt.RawResponse != null) LastReceipt = receipt;
                foreach (GameDataSaveRequest request in _active.Requests)
                    if (request.Status != GameDataSaveRequestStatus.SuccessConfirmed)
                        request.SetState(GameDataSaveRequestStatus.InvalidatedAfterSend, LastReceipt, LastError);
                return;
            }
            if (receipt.Disposition == GameDataSaveDisposition.Indeterminate)
            {
                // 응답 유실 알림 뒤 실제 오류 응답이 도착해도 raw 정보는 보존한다. 잠금은 그대로 유지한다.
                if (State == GameDataSaveCoordinatorState.Indeterminate &&
                    LastReceipt?.RawResponse != null && receipt.RawResponse == null) return;
                LastReceipt = receipt;
                LastError = receipt.Reason;
                State = GameDataSaveCoordinatorState.Indeterminate;
                SetActiveState(GameDataSaveRequestStatus.Indeterminate, receipt, LastError);
                return;
            }
            if (receipt.Disposition == GameDataSaveDisposition.RejectedBeforeSend)
            {
                // SDK에 들어가기 전에 거절된 경우만 idle로 되돌린다. 후속 자동 전송은 여기서 하지 않는다.
                if (State != GameDataSaveCoordinatorState.Sending || receipt.SendStarted) return;
                RejectBeforeSend(_active, receipt.Reason);
                return;
            }
            if (receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed) return;

            LastReceipt = receipt;
            State = GameDataSaveCoordinatorState.ApplyingConfirmedState; // 완료 전달/재진입보다 먼저 확정
            SaveBatch completed = _active;
            try
            {
                if (completed.IsAutosave) _onAutosaveConfirmed?.Invoke(receipt);
                if (!EnsureSession()) return;
                // 완료 목록은 이미 활성 배치로 분리되었다. 재진입 요청은 반드시 다음 배치에 들어간다.
                foreach (GameDataSaveRequest request in completed.Requests)
                {
                    if (!EnsureSession()) return;
                    Action<GameDataSaveReceipt> callback = request.OnConfirmed;
                    request.OnConfirmed = null;
                    callback?.Invoke(receipt);
                    if (!EnsureSession()) return;
                    request.SetState(GameDataSaveRequestStatus.SuccessConfirmed, receipt, null);
                    request.ClearCallbacks();
                }
            }
            catch (Exception ex) { FailLocalCompletion(receipt, ex); return; }
            if (completed.Tutorial != null)
            {
                State = GameDataSaveCoordinatorState.WaitingForTutorialStage;
                completed.Tutorial.AfterGameDataConfirmed();
                return;
            }
            _active = null;
            _sendInvoked = false;
            LastError = null;
            State = GameDataSaveCoordinatorState.Idle;
            Pump();
        }

        private void Pump()
        {
            if (_pumping || State != GameDataSaveCoordinatorState.Idle || !EnsureSession()) return;
            _pumping = true;
            try
            {
                if (_autosaveNeeded)
                {
                    _autosaveNeeded = false;
                    SaveBatch batch = _pending.Last != null && _pending.Last.Value.IsAutosave
                        && _pending.Last.Value.AutosaveGuard == null
                        ? _pending.Last.Value : new SaveBatch(NextIdentity(), true, _createLatestAutosave);
                    if (_pending.Last == null || !ReferenceEquals(batch, _pending.Last.Value)) _pending.AddLast(batch);
                    batch.Requests.Add(new GameDataSaveRequest(batch.Identity, null, null));
                }
                // 동기 가짜 응답도 같은 loop로 이어지고 재귀적으로 SDK를 호출하지 않는다.
                while (State == GameDataSaveCoordinatorState.Idle && _pending.Count > 0 && EnsureSession())
                {
                    SaveBatch batch = _pending.First.Value;
                    _pending.RemoveFirst();
                    // A rejected batch is already removed; it is never regenerated/retried.
                    // Independently accepted work behind it must not wait for an unrelated new request.
                    if (!PrepareAndSend(batch) &&
                        (State != GameDataSaveCoordinatorState.Idle || !EnsureSession())) break;
                }
            }
            catch (Exception ex)
            {
                if (_active != null && _sendInvoked) FailLocalCompletion(LastReceipt, ex);
                else
                {
                    LastError = ex.Message;
                    if (_active != null) RejectBeforeSend(_active, LastError);
                }
            }
            finally { _pumping = false; }
        }

        private void RejectBeforeSend(SaveBatch batch, string error)
        {
            batch.Purchase?.ReleaseUnsent();
            batch.QuestStaffGrant?.ReleaseDisplayData();
            LastError = error;
            LastReceipt = GameDataSaveReceipt.Rejected(batch.Identity,
                batch.Identity.Matches(CurrentIdentity) ? CurrentPayload : null, error);
            batch.Factory = null;
            foreach (GameDataSaveRequest request in batch.Requests)
            {
                request.SetState(GameDataSaveRequestStatus.RejectedBeforeSend, LastReceipt, error);
                Notify(request);
                request.ClearCallbacks();
            }
            if (ReferenceEquals(_active, batch)) { _active = null; _sendInvoked = false; }
            State = GameDataSaveCoordinatorState.Idle;
        }

        private static bool CheckAutosaveGuard(SaveBatch batch, out string error)
        {
            error = null;
            try
            {
                if (batch.AutosaveGuard == null || batch.AutosaveGuard()) return true;
                error = "자동 저장의 현재 게임 진행 조건을 충족하지 않습니다.";
            }
            catch (Exception ex) { error = "자동 저장 조건 확인 실패: " + ex.Message; }
            return false;
        }

        private static void AbandonBeforeSend(SaveBatch batch, string error)
        {
            batch.Purchase?.ReleaseUnsent();
            batch.QuestStaffGrant?.ReleaseDisplayData();
            batch.Factory = null;
            foreach (GameDataSaveRequest request in batch.Requests)
            {
                request.SetState(GameDataSaveRequestStatus.RejectedBeforeSend,
                    GameDataSaveReceipt.Rejected(batch.Identity, null, error), error);
                request.ClearCallbacks();
            }
        }

        private void SetActiveState(GameDataSaveRequestStatus status, GameDataSaveReceipt receipt, string error)
        {
            SaveBatch batch = _active;
            var changed = new List<GameDataSaveRequest>();
            foreach (GameDataSaveRequest request in batch.Requests)
            {
                if (request.Status != status) changed.Add(request);
                request.SetState(status, receipt, error);
            }
            foreach (GameDataSaveRequest request in changed)
            {
                Notify(request);
                // 알림 재진입에서 원래 성공 응답이 확정되어도 완료 ticket을 다시 미확정으로 만들지 않는다.
                if (!EnsureSession() || !ReferenceEquals(_active, batch) || request.Status != status) return;
            }
        }

        private void FailLocalCompletion(GameDataSaveReceipt receipt, Exception exception)
        {
            if (!EnsureSession()) return;
            LastError = "서버 응답 후 로컬 완료 처리 실패. 후속 저장을 중단합니다: " + exception.Message;
            State = GameDataSaveCoordinatorState.LocalCompletionFailed;
            foreach (GameDataSaveRequest request in _active.Requests)
            {
                if (request.Status == GameDataSaveRequestStatus.SuccessConfirmed) continue;
                request.SetState(GameDataSaveRequestStatus.LocalCompletionFailed, receipt, LastError);
                request.OnConfirmed = null;
                Notify(request);
                request.ClearCallbacks();
                if (!EnsureSession()) return;
            }
        }

        private void Notify(GameDataSaveRequest request)
        {
            if (!EnsureSession()) return;
            try { request.OnStateChanged?.Invoke(request); }
            catch (Exception ex)
            {
                // 진단 알림 예외는 SDK 오류나 저장 성공으로 바꾸지 않으며 전송을 재시도하지 않는다.
                LastError = "저장 상태 알림 실패: " + ex.Message;
                request.SetState(request.Status, request.Receipt, LastError);
            }
        }

        private bool MatchesCurrent(GameDataSaveIdentity identity) => CurrentIdentity != null
            && CurrentIdentity.Matches(identity) && _target.Matches(identity.Target);
    }

    public enum GameDataSaveCoordinatorState
    {
        Idle,
        Preparing,
        Sending,
        ApplyingConfirmedState,
        Indeterminate,
        LocalCompletionFailed,
        InvalidatedAfterSend,
        ReservedForMail,
        WaitingForTutorialStage
    }

    /// <summary>
    /// 메모리상의 우편 수령/저장 보호권. 요청의 ID·응답 신뢰 판단은 우편 처리자가 담당한다.
    /// 전송 후 미확정 또는 이전 세션의 보호권은 화면 닫기/Reset으로 해제할 수 없다.
    /// </summary>
    public sealed class GameDataMailSaveLease : IMailReceiveProtection
    {
        private readonly GameDataSaveCoordinator _owner;
        internal readonly Func<bool> IsQueryCurrent;
        internal bool HasRequestStarted;
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveTarget Target { get; }
        public bool IsCurrent => _owner.IsMailLeaseCurrent(this);
        public bool IsIndeterminate { get; internal set; }
        public string Error { get; internal set; }

        internal GameDataMailSaveLease(GameDataSaveCoordinator owner, GameDataRestoreQuery query,
            GameDataSaveTarget target, Func<bool> isQueryCurrent)
        { _owner = owner; Query = query; Target = target; IsQueryCurrent = isQueryCurrent; }

        public bool TryMarkRequestStarted() => _owner.TryMarkMailRequestStarted(this);
        public void MarkIndeterminate(string reason) => _owner.MarkMailIndeterminate(this, reason);
        public bool TryCompleteKnownOutcome() => _owner.TryReleaseMail(this, true);
        public bool TryCancelBeforeRequest() => _owner.TryReleaseMail(this, false);
    }

    public enum GameDataSaveRequestStatus
    {
        RejectedBeforeSend,
        Accepted,
        Sending,
        SuccessConfirmed,
        Indeterminate,
        LocalCompletionFailed,
        InvalidatedAfterSend
    }

    /// <summary>Accepted는 큐 접수일 뿐이다. Receipt와 Status를 통해 전송/서버/로컬 완료를 구별한다.</summary>
    public sealed class GameDataSaveRequest
    {
        public bool Accepted { get; }
        public GameDataSaveRequestStatus Status { get; private set; }
        public GameDataSaveReceipt Receipt { get; private set; }
        public string Error { get; private set; }
        public GameDataSaveIdentity Identity { get; }
        internal Action<GameDataSaveReceipt> OnConfirmed;
        internal Action<GameDataSaveRequest> OnStateChanged;

        internal GameDataSaveRequest(GameDataSaveIdentity identity, Action<GameDataSaveReceipt> onConfirmed,
            Action<GameDataSaveRequest> onStateChanged)
        {
            Accepted = true;
            Identity = identity;
            Status = GameDataSaveRequestStatus.Accepted;
            OnConfirmed = onConfirmed;
            OnStateChanged = onStateChanged;
        }

        private GameDataSaveRequest(string error)
        { Accepted = false; Status = GameDataSaveRequestStatus.RejectedBeforeSend; Error = error; }

        internal static GameDataSaveRequest Rejected(string error) => new GameDataSaveRequest(error);

        internal void SetState(GameDataSaveRequestStatus status, GameDataSaveReceipt receipt, string error)
        { Status = status; Receipt = receipt; Error = error; }

        internal void ClearCallbacks() { OnConfirmed = null; OnStateChanged = null; }
    }
}
