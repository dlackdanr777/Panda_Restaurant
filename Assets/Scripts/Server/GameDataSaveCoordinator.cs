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

        private sealed class SaveBatch
        {
            public readonly GameDataSaveIdentity Identity;
            public readonly bool IsAutosave;
            public readonly Func<bool> AutosaveGuard;
            public Func<Param> Factory;
            public readonly List<GameDataSaveRequest> Requests = new List<GameDataSaveRequest>();
            public SaveBatch(GameDataSaveIdentity identity, bool isAutosave, Func<Param> factory,
                Func<bool> autosaveGuard = null)
            { Identity = identity; IsAutosave = isAutosave; Factory = factory; AutosaveGuard = autosaveGuard; }
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

        /// <summary>일반 값 저장. 순서가 왔을 때만 factory를 실행하며 구매를 재계산하지 않는다.</summary>
        public GameDataSaveRequest EnqueueSave(Func<Param> factory,
            Action<GameDataSaveReceipt> onConfirmed = null, Action<GameDataSaveRequest> onStateChanged = null)
        {
            return Enqueue(factory, false, onConfirmed, onStateChanged, null);
        }

        /// <summary>맨 뒤의 미전송 자동 저장만 병합한다. 중간의 부분 갱신을 앞지르지 않는다.</summary>
        public GameDataSaveRequest RequestAutosave(Action<GameDataSaveReceipt> onConfirmed = null,
            Action<GameDataSaveRequest> onStateChanged = null, Func<bool> canCreateValues = null)
        {
            return Enqueue(_createLatestAutosave, true, onConfirmed, onStateChanged, canCreateValues);
        }

        private GameDataSaveRequest Enqueue(Func<Param> factory, bool autosave,
            Action<GameDataSaveReceipt> onConfirmed, Action<GameDataSaveRequest> onStateChanged,
            Func<bool> autosaveGuard)
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
                batch = new SaveBatch(NextIdentity(), autosave, factory, autosaveGuard);
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
        InvalidatedAfterSend
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
