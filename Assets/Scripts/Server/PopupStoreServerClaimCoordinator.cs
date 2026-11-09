using System;
using PandaRestaurant.PopupStoreServer;

namespace Muks.BackEnd
{
    public enum PopupStoreServerClaimCoordinatorState { Ready, Claiming, QueryingLatestWallet, RecoveryRequired, Reconciled }

    /// <summary>
    /// Payout-facing adapter: CLAIM -> immutable grant -> separate QUERY of latest wallet -> local reconciliation.
    /// The generic client remains a protocol primitive, not payout authority. This adapter never calls AddDia.
    /// A future migrated boundary must bind its wallet session to the SAME authenticated epoch as client.Session;
    /// matching account strings alone is not authentication. No production boundary is supplied by default.
    /// </summary>
    public sealed class PopupStoreServerClaimCoordinator
    {
        private readonly IPopupStoreServerWalletBoundary _boundary;
        private readonly PopupStoreServerWalletGuard _guard;
        private readonly PopupStoreRewardAccountSession _session;
        private readonly PopupStoreWalletSession _walletSession;
        private PopupStoreRewardRequest _request;
        private PopupStoreRewardGrant _grant;
        private PopupStoreRewardResponse _confirmedResponse;
        private PopupStoreWalletClaim _claim;
        private Action<PopupStoreRewardResponse> _completed;
        private bool _busy;
        private long _attempt;
        public PopupStoreServerClient Client { get; }
        public PopupStoreWalletClaim ActiveClaim => _guard.ActiveClaim;
        public PopupStoreServerClaimCoordinatorState State { get; private set; }
        public string LastError { get; private set; }
        /// <summary>Read-only admission capability; does not require protocol recovery to have completed.
        /// Actual START/CLAIM still uses the client's recovery and pending-command gates.</summary>
        public bool IsWalletReadyForAdmission
        {
            get
            {
                try
                {
                    if (_busy || _guard.ActiveClaim != null
                        || (_claim != null && _claim.State != PopupStoreWalletClaimState.Reconciled)
                        || !Current(_session) || _boundary.Capabilities?.CanClaim != true) return false;
                    var wallet = _boundary.ReadLocalWallet();
                    return wallet?.IsValid == true && wallet.AccountId == _session.AccountId && Current(_session);
                }
                catch { return false; }
            }
        }
        public bool CanClaim
        {
            get
            {
                try { return IsWalletReadyForAdmission
                    && Client.State == PopupStoreServerClientState.Ready && !Client.HasPendingRequest; }
                catch { return false; }
            }
        }

        public PopupStoreServerClaimCoordinator(PopupStoreServerClient client, IPopupStoreServerWalletBoundary walletBoundary = null)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _boundary = walletBoundary ?? new LegacyPopupStoreServerWalletBoundary();
            _guard = new PopupStoreServerWalletGuard(_boundary);
            _session = client.Session;
            try { _walletSession = _boundary.CurrentSession; } catch { _walletSession = null; }
        }

        public bool Claim(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
            Action<PopupStoreRewardResponse> completed)
        {
            if (!Prepare(session, request, completed, false)) return false;
            long attempt = _attempt;
            State = PopupStoreServerClaimCoordinatorState.Claiming;
            bool delivered = false;
            bool accepted = Client.Submit(new Command { Operation = Operation.Claim,
                RequestId = _claim.RequestId, BusinessSessionId = request.BusinessSessionId }, result =>
            { delivered = true; ReceiveGrant(attempt, result); });
            if (!accepted && !delivered) Unknown(attempt, "기존 CLAIM 요청과 지갑을 먼저 복구해 주세요.");
            return accepted;
        }

        /// <summary>Reads existing business/request after timeout or restart. Never submits a new CLAIM or retries a write.</summary>
        public bool Recover(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
            Action<PopupStoreRewardResponse> completed)
        {
            // Explicit display recovery may observe the same immutable receipt again, without another
            // CLAIM, wallet apply, or grant. Duplicate transport callbacks still cannot reach this path.
            if (!_busy && Current(session) && _confirmedResponse != null && _request.Matches(request))
            { Notify(completed, _confirmedResponse); return true; }
            if (!Prepare(session, request, completed, true)) return false;
            long attempt = _attempt;
            bool delivered = false;
            Action<PopupStoreServerClientResult> reply = result => { delivered = true; ReceiveGrant(attempt, result); };
            bool accepted = Client.HasPendingRequest || Client.State == PopupStoreServerClientState.RecoverFirst
                ? Client.Recover(reply) : Client.QueryBusiness(request.BusinessSessionId, reply);
            if (!accepted && !delivered) Unknown(attempt, "기존 CLAIM의 서버 조회가 아직 진행 중입니다.");
            return accepted;
        }

        public void MarkResponseMissing()
        {
            if (!_busy) return;
            Client.MarkResponseMissing();
            Unknown(_attempt, "CLAIM 또는 최신 지갑 응답이 없습니다. 기존 영업을 조회해 주세요.");
        }

        private bool Prepare(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
            Action<PopupStoreRewardResponse> completed, bool recovery)
        {
            if (_busy) return false;
            if (request == null || !request.TryValidate(out _) || !Current(session) || request.AccountId != session.AccountId
                || !WalletReady())
            { Notify(completed, new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.RejectedBeforeSend,
                error: PopupStoreServerWalletGuard.UnavailableReason)); return false; }
            if (_claim?.State == PopupStoreWalletClaimState.Reconciled && _request != null
                && !_request.Matches(request) && _request.GrantKey != request.GrantKey)
            { _claim = null; _request = null; _grant = null; _confirmedResponse = null; }
            if (_claim != null && (!recovery || _request == null || !_request.Matches(request)
                || _claim.State == PopupStoreWalletClaimState.Reconciled))
            { Notify(completed, new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate,
                error: "동일한 미해결 요청만 조회할 수 있습니다.")); return false; }
            if (!recovery && (Client.State != PopupStoreServerClientState.Ready || Client.HasPendingRequest))
            { Notify(completed, new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate,
                error: "먼저 기존 서버 요청을 복구해 주세요.")); return false; }
            if (_claim == null)
            {
                if (!_guard.TryBeginClaim(PopupStoreBackendPersistence.ClaimRequestId(request.GrantKey), out var claim, out string error))
                { Notify(completed, new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.RejectedBeforeSend, error: error)); return false; }
                _claim = claim; _request = request;
                if (!ReferenceEquals(claim.Session, _walletSession) || claim.Session.AccountId != request.AccountId
                    || !_guard.TryMarkRequestStarted(claim, out error))
                {
                    if (_guard.TryCancelBeforeRequest(claim, out _)) { _claim = null; _request = null; }
                    Notify(completed, new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.RejectedBeforeSend,
                        error: error ?? "인증 세션과 지갑 보호가 일치하지 않습니다.")); return false;
                }
            }
            _completed = completed;
            _busy = true; ++_attempt;
            LastError = null;
            return true;
        }

        private void ReceiveGrant(long attempt, PopupStoreServerClientResult result)
        {
            if (!Active(attempt)) return;
            if (result?.Outcome != PopupStoreServerClientOutcome.Confirmed
                || !TryReadOutcome(result.Response, out var grant, out bool noReward))
            { Unknown(attempt, result?.Error ?? "서버의 확정 지급 기록을 확인하지 못했습니다."); return; }
            if (_grant != null && !SameGrant(_grant, grant))
            { Unknown(attempt, "기존 지급 영수증이 변경되었습니다."); return; }
            _grant = grant;
            if (!_guard.BeginReconciliation(_claim, out var query, out string error))
            { Unknown(attempt, error); return; }
            State = PopupStoreServerClaimCoordinatorState.QueryingLatestWallet;
            bool delivered = false;
            bool accepted = Client.QueryBusiness(_request.BusinessSessionId, latest =>
            {
                delivered = true;
                if (!Active(attempt)) return;
                if (latest?.Outcome != PopupStoreServerClientOutcome.Confirmed
                    || !TryReadOutcome(latest.Response, out var readGrant, out bool readNoReward)
                    || noReward != readNoReward || !SameGrant(grant, readGrant) || latest.Response.Wallet == null)
                { Unknown(attempt, "최신 QUERY의 영업·지급 기록 또는 지갑이 일치하지 않습니다."); return; }
                var wallet = latest.Response.Wallet;
                var evidence = new PopupStoreWalletClaimEvidence(query, _claim.RequestId, _request.AccountId, wallet.WalletId,
                    noReward ? PopupStoreWalletQueryOutcome.RejectedNotApplied : PopupStoreWalletQueryOutcome.Committed,
                    noReward ? null : new PopupStoreHistoricalWalletGrant(grant.GrantId, grant.BalanceVersion, grant.BalanceAfter));
                var snapshot = new PopupStoreLatestWalletSnapshot(query,
                    new PopupStoreWalletSnapshot(wallet.AccountId, wallet.WalletId, wallet.Version, wallet.Balance));
                if (!_guard.TryReconcile(_claim, evidence, snapshot, null, out string reconcileError))
                { Unknown(attempt, reconcileError); return; }
                _busy = false;
                State = PopupStoreServerClaimCoordinatorState.Reconciled;
                var callback = _completed; _completed = null;
                _confirmedResponse = new PopupStoreRewardResponse(_request,
                    noReward ? PopupStoreRewardResponseKind.NoReward : PopupStoreRewardResponseKind.Granted, grant);
                Notify(callback, _confirmedResponse);
            });
            if (!accepted && !delivered) Unknown(attempt, "최신 지갑 조회를 시작하지 못했습니다. 보호를 유지합니다.");
        }

        private bool TryReadOutcome(Response response, out PopupStoreRewardGrant grant, out bool noReward)
        {
            grant = null; noReward = false;
            var business = response?.Business;
            var decision = response?.Decision;
            if (response == null || response.Status != Status.Ok || business == null || !business.IsSettled
                || business.AccountId != _request.AccountId || business.BusinessSessionId != _request.BusinessSessionId
                || decision == null) return false;
            var expected = new PopupStoreRewardRequest(_request.AccountId, _request.BusinessSessionId, decision.DecisionId,
                (PopupStoreGradeTier)decision.Grade, (PopupStoreRewardKind)decision.Kind, decision.Diamonds, decision.PolicyVersion);
            if (!expected.Matches(_request)) return false;
            if (_request.Grade == PopupStoreGradeTier.None)
            { noReward = response.Grant == null && business.Grant == null; return noReward; }
            var source = response.Grant;
            if (source == null || source.GrantKey != expected.GrantKey || source.DecisionId != expected.DecisionId
                || source.Diamonds != expected.Diamonds) return false;
            grant = new PopupStoreRewardGrant(expected, source.GrantId, source.BalanceAfter, source.WalletVersion);
            return grant.Matches(_request);
        }

        private void Unknown(long attempt, string error)
        {
            if (!_busy || attempt != _attempt) return;
            _guard.MarkIndeterminate(_claim, error);
            State = PopupStoreServerClaimCoordinatorState.RecoveryRequired;
            LastError = error; _busy = false;
            var callback = _completed; _completed = null;
            if (Current(_session)) Notify(callback, new PopupStoreRewardResponse(_request, PopupStoreRewardResponseKind.Indeterminate, error: error));
        }
        private bool Active(long attempt)
        {
            if (!_busy || attempt != _attempt) return false;
            if (Current(_session)) return true;
            _guard.MarkIndeterminate(_claim, "인증 세션이 변경되었습니다.");
            State = PopupStoreServerClaimCoordinatorState.RecoveryRequired;
            _busy = false; _completed = null; return false;
        }
        private bool Current(PopupStoreRewardAccountSession session)
        {
            try { return session != null && ReferenceEquals(session, _session) && ReferenceEquals(session, Client.Session)
                && Client.IsCurrentSession
                && _walletSession != null && ReferenceEquals(_boundary.CurrentSession, _walletSession)
                && _walletSession.AccountId == session.AccountId && Client.State != PopupStoreServerClientState.Invalidated; }
            catch { return false; }
        }
        private bool WalletReady() { try { return _boundary.Capabilities?.CanClaim == true; } catch { return false; } }
        private static bool SameGrant(PopupStoreRewardGrant first, PopupStoreRewardGrant second) => first == null ? second == null
            : second != null && first.Decision.Matches(second.Decision) && first.GrantId == second.GrantId
                && first.BalanceVersion == second.BalanceVersion && first.BalanceAfter == second.BalanceAfter;
        private void Notify(Action<PopupStoreRewardResponse> callback, PopupStoreRewardResponse result)
        {
            if ((result.Kind == PopupStoreRewardResponseKind.Granted || result.Kind == PopupStoreRewardResponseKind.NoReward)
                && !Current(_session)) return;
            try { callback?.Invoke(result); }
            catch { LastError = "지급 확인 결과는 보존됐습니다. 화면 갱신을 다시 확인해 주세요."; }
        }
    }
}
