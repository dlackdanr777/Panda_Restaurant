using System;
using System.Collections.Generic;
using BackEnd;

namespace Muks.BackEnd
{
    /// <summary>
    /// 메인 스레드/한 세션/한 계정의 명시된 GameData 행에만 사용하는 저장 순서 관리 객체.
    /// 구매 계산/보상/RequestSession 완료 처리는 호출자 책임이다. 구매를 큐에 보관하지 않는다.
    /// 모든 실제 writer의 연결과 원본 복원 확인은 후속 단계이며, 현재 기존 저장 경로를 대체하지 않는다.
    /// </summary>
    public sealed class GameDataSaveCoordinator
    {
        // 미해결 저장만 강한 참조로 보관한다. 계정/조회 수명 변경으로 이 잠금을 잃지 않는다.
        // 메인 스레드 전용이며, 전체 writer의 순서를 관리하는 전역 조정기는 아니다.
        private static readonly HashSet<GameDataSaveCoordinator> BlockedCoordinators =
            new HashSet<GameDataSaveCoordinator>();

        private readonly GameDataSaveTarget _target;
        private readonly GameDataSingleUpdate _updater;
        private readonly Func<Param> _createLatestAutosave;
        private readonly Action<GameDataSaveReceipt> _onAutosaveConfirmed;
        private readonly HashSet<string> _usedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly string _autosavePrefix = "autosave:" + Guid.NewGuid().ToString("N") + ":";
        private long _autosaveNumber;
        private bool _pumpingAutosave;
        private Action<GameDataSaveReceipt> _onConfirmed;

        private GameDataSaveCoordinatorState _state = GameDataSaveCoordinatorState.Idle;
        public GameDataSaveCoordinatorState State
        {
            get => _state;
            private set
            {
                GameDataSaveCoordinatorState previous = _state;
                _state = value;
                if (value == GameDataSaveCoordinatorState.Indeterminate
                    || value == GameDataSaveCoordinatorState.LocalCompletionFailed)
                    BlockedCoordinators.Add(this);
                else if (value == GameDataSaveCoordinatorState.Idle
                    && previous == GameDataSaveCoordinatorState.ApplyingConfirmedState)
                    BlockedCoordinators.Remove(this);
                // 늦게 도착한 동일 요청의 확정 콜백을 적용하는 동안에도 잠금을 유지한다.
            }
        }
        public bool HasPendingAutosave { get; private set; }
        public bool CanStartPurchase => State == GameDataSaveCoordinatorState.Idle && !HasPendingAutosave && !_pumpingAutosave;
        public GameDataSaveIdentity CurrentIdentity { get; private set; }
        public GameDataSavePayload CurrentPayload { get; private set; }
        public GameDataSaveReceipt LastReceipt { get; private set; }
        public string LastError { get; private set; }

        /// <summary>
        /// 같은 계정/행에 미확정 또는 로컬 완료 실패가 남아 있으면 다른 저장 경로도 전송을 멈춘다.
        /// 새 조회/인증으로 해제되지 않으며, 이미 전송된 요청의 취소나 전역 순서 보장을 뜻하지 않는다.
        /// </summary>
        public static bool IsTargetBlocked(GameDataSaveTarget target)
        {
            if (target == null) return false;
            foreach (GameDataSaveCoordinator coordinator in BlockedCoordinators)
                if (coordinator._target.Matches(target)) return true;
            return false;
        }

        public GameDataSaveCoordinator(GameDataSaveTarget target, GameDataSingleUpdate updater,
            Func<Param> createLatestAutosave, Action<GameDataSaveReceipt> onAutosaveConfirmed = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));
            _createLatestAutosave = createLatestAutosave ?? throw new ArgumentNullException(nameof(createLatestAutosave));
            _onAutosaveConfirmed = onAutosaveConfirmed;
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
            if (!CanStartPurchase) { error = "선행 저장 또는 확정 처리가 끝나지 않았습니다."; return false; }
            if (createValues == null || onConfirmed == null)
            { error = "최신 구매 자료 생성과 성공 확정 처리 콜백이 필요합니다."; return false; }
            return PrepareAndSend(identity, createValues, onConfirmed, out error);
        }

        /// <summary>Param을 보관하지 않고 dirty 표시만 합친다. 실패/미확정의 재전송이나 잠금 해제 수단이 아니다.</summary>
        public bool MarkAutosaveNeeded()
        {
            HasPendingAutosave = true;
            if (State == GameDataSaveCoordinatorState.Indeterminate || State == GameDataSaveCoordinatorState.LocalCompletionFailed)
                return false;
            PumpAutosave();
            return true;
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

        private bool PrepareAndSend(GameDataSaveIdentity identity, Func<Param> createValues,
            Action<GameDataSaveReceipt> onConfirmed, out string error)
        {
            error = null;
            if (identity == null || !identity.IsValid || !_target.Matches(identity.Target) || _usedIds.Contains(identity.RequestId))
            { error = "저장 대상/요청 ID가 잘못되었거나 이 조정기에서 이미 사용되었습니다."; return false; }

            // gate/factory 재진입도 구매를 먼저 준비하거나 전송하지 못하게 잠근다.
            State = GameDataSaveCoordinatorState.Preparing;
            if (!_updater.CanSend(identity, out error))
            {
                State = GameDataSaveCoordinatorState.Idle;
                LastReceipt = GameDataSaveReceipt.Rejected(identity, null, error);
                LastError = error;
                return false;
            }
            GameDataSavePayload payload;
            try
            {
                if (!GameDataSavePayload.TryCapture(createValues(), out payload, out error))
                {
                    State = GameDataSaveCoordinatorState.Idle;
                    LastReceipt = GameDataSaveReceipt.Rejected(identity, null, error);
                    LastError = error;
                    return false;
                }
            }
            catch (Exception ex)
            {
                State = GameDataSaveCoordinatorState.Idle;
                error = "전송 전 자료 생성 실패: " + ex.Message;
                LastReceipt = GameDataSaveReceipt.Rejected(identity, null, error);
                LastError = error;
                return false;
            }

            CurrentIdentity = identity;
            CurrentPayload = payload;
            _onConfirmed = onConfirmed;
            _usedIds.Add(identity.RequestId);
            LastError = null;
            State = GameDataSaveCoordinatorState.Sending;
            _updater.Send(identity, payload, HandleReceipt);
            if (LastReceipt != null && identity.Matches(LastReceipt.Identity)
                && LastReceipt.Disposition == GameDataSaveDisposition.RejectedBeforeSend)
            { error = LastReceipt.Reason; return false; }
            return true;
        }

        private void HandleReceipt(GameDataSaveReceipt receipt)
        {
            if (receipt == null || !MatchesCurrent(receipt.Identity) ||
                (State != GameDataSaveCoordinatorState.Sending && State != GameDataSaveCoordinatorState.Indeterminate)) return;
            if (receipt.Disposition == GameDataSaveDisposition.Indeterminate)
            {
                // 응답 유실 알림 뒤 실제 오류 응답이 도착해도 raw 정보는 보존한다. 잠금은 그대로 유지한다.
                if (State == GameDataSaveCoordinatorState.Indeterminate &&
                    LastReceipt?.RawResponse != null && receipt.RawResponse == null) return;
                LastReceipt = receipt;
                LastError = receipt.Reason;
                State = GameDataSaveCoordinatorState.Indeterminate;
                return;
            }
            if (receipt.Disposition == GameDataSaveDisposition.RejectedBeforeSend)
            {
                // SDK에 들어가기 전에 거절된 경우만 idle로 되돌린다. 후속 자동 전송은 여기서 하지 않는다.
                if (State != GameDataSaveCoordinatorState.Sending || receipt.SendStarted) return;
                LastReceipt = receipt;
                LastError = receipt.Reason;
                _onConfirmed = null;
                State = GameDataSaveCoordinatorState.Idle;
                return;
            }
            if (receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed) return;

            LastReceipt = receipt;
            State = GameDataSaveCoordinatorState.ApplyingConfirmedState; // 완료 전달/재진입보다 먼저 확정
            Action<GameDataSaveReceipt> commit = _onConfirmed;
            _onConfirmed = null;
            try { commit?.Invoke(receipt); }
            catch (Exception ex)
            {
                // 서버 성공과 로컬 반영 실패를 구분한다. 대기 중인 낡은 값으로 덮어쓰지 않고 정지한다.
                LastError = "성공 응답 후 로컬 확정 처리 실패. 수동 대조가 필요합니다: " + ex.Message;
                State = GameDataSaveCoordinatorState.LocalCompletionFailed;
                return;
            }
            LastError = null;
            State = GameDataSaveCoordinatorState.Idle;
            PumpAutosave(); // 확정된 로컬 상태 반영이 끝난 다음에만 최신 자료 생성
        }

        private void PumpAutosave()
        {
            if (_pumpingAutosave || !HasPendingAutosave || State != GameDataSaveCoordinatorState.Idle) return;
            _pumpingAutosave = true;
            HasPendingAutosave = false;
            try
            {
                var identity = new GameDataSaveIdentity(_autosavePrefix + checked(++_autosaveNumber), _target);
                if (!PrepareAndSend(identity, _createLatestAutosave, _onAutosaveConfirmed, out _)) HasPendingAutosave = true;
            }
            catch (Exception ex)
            {
                // 내부 준비 예외는 후속 저장을 시작할 이유가 아니다.
                HasPendingAutosave = true;
                LastError = ex.Message;
                State = GameDataSaveCoordinatorState.LocalCompletionFailed;
            }
            finally { _pumpingAutosave = false; }
            // 동기 콜백에서 다시 dirty가 되어도 재귀 재전송하지 않는다. 다음 명시적 dirty 알림까지 보존한다.
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
        LocalCompletionFailed
    }
}
