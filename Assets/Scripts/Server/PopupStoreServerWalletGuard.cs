using System;
using System.Collections.Generic;

namespace Muks.BackEnd
{
    /// <summary>A new object is required for every authentication, including account A -> B -> A.</summary>
    public sealed class PopupStoreWalletSession
    {
        public string AccountId { get; }
        public long Generation { get; }
        public PopupStoreWalletSession(string accountId, long generation)
        { AccountId = accountId; Generation = generation; }
    }

    public sealed class PopupStoreWalletCapabilities
    {
        public bool MigratedServerWallet { get; }
        public bool AllLegacyDiaWritersFenced { get; }
        public bool CanClaim => MigratedServerWallet && AllLegacyDiaWritersFenced;
        public PopupStoreWalletCapabilities(bool migratedServerWallet, bool allLegacyDiaWritersFenced)
        { MigratedServerWallet = migratedServerWallet; AllLegacyDiaWritersFenced = allLegacyDiaWritersFenced; }
    }

    /// <summary>Version is the authoritative wallet revision, never a restore generation or claim counter.</summary>
    public sealed class PopupStoreWalletSnapshot
    {
        public string AccountId { get; }
        public string WalletId { get; }
        public long Version { get; }
        public int Balance { get; }
        public PopupStoreWalletSnapshot(string accountId, string walletId, long version, int balance)
        { AccountId = accountId; WalletId = walletId; Version = version; Balance = balance; }
        public bool IsValid => !string.IsNullOrWhiteSpace(AccountId) && !string.IsNullOrWhiteSpace(WalletId)
            && Version >= 0 && Balance >= 0;
        public bool SameWallet(PopupStoreWalletSnapshot other) => other != null
            && AccountId == other.AccountId && WalletId == other.WalletId;
        public bool SameState(PopupStoreWalletSnapshot other) => SameWallet(other)
            && Version == other.Version && Balance == other.Balance;
    }

    public interface IPopupStoreWalletLease
    {
        bool IsCurrent { get; }
        bool TryMarkRequestStarted();
        void MarkIndeterminate(string reason);
        bool TryCompleteKnownOutcome();
        bool TryCancelBeforeRequest();
    }

    /// <summary>
    /// Trusted synchronous memory boundary, not an SDK transport. A migrated implementation must fence
    /// every legacy Dia snapshot writer and journal/reconcile pending local changes. Apply is atomic,
    /// compares expectedLocal, publishes no events, and never adds the historical grant amount.
    /// A false Apply result guarantees no mutation. Leases outlive UI and unresolved requests.
    /// </summary>
    public interface IPopupStoreServerWalletBoundary
    {
        PopupStoreWalletCapabilities Capabilities { get; }
        PopupStoreWalletSession CurrentSession { get; }
        PopupStoreWalletSnapshot ReadLocalWallet();
        bool TryAcquireClaimLease(PopupStoreWalletSession session, PopupStoreWalletSnapshot expectedLocal,
            out IPopupStoreWalletLease lease, out string error);
        bool TryApplyLatestWallet(IPopupStoreWalletLease lease, PopupStoreWalletSnapshot expectedLocal,
            PopupStoreLatestWalletSnapshot latest, out string error);
    }

    /// <summary>Production default: existing client Dia snapshots have not migrated. No SDK or UserInfo access.</summary>
    public sealed class LegacyPopupStoreServerWalletBoundary : IPopupStoreServerWalletBoundary
    {
        public PopupStoreWalletCapabilities Capabilities { get; } = new PopupStoreWalletCapabilities(false, false);
        public PopupStoreWalletSession CurrentSession => null;
        public PopupStoreWalletSnapshot ReadLocalWallet() => null;
        public bool TryAcquireClaimLease(PopupStoreWalletSession session, PopupStoreWalletSnapshot expectedLocal,
            out IPopupStoreWalletLease lease, out string error)
        { lease = null; error = PopupStoreServerWalletGuard.UnavailableReason; return false; }
        public bool TryApplyLatestWallet(IPopupStoreWalletLease lease, PopupStoreWalletSnapshot expectedLocal,
            PopupStoreLatestWalletSnapshot latest, out string error)
        { error = PopupStoreServerWalletGuard.UnavailableReason; return false; }
    }

    public enum PopupStoreWalletClaimState { Reserved, Sending, Indeterminate, Reconciled, CancelledBeforeRequest }
    public enum PopupStoreWalletQueryOutcome { Unknown, Committed, RejectedNotApplied }

    public sealed class PopupStoreWalletClaim
    {
        internal readonly PopupStoreServerWalletGuard Owner;
        internal readonly IPopupStoreWalletLease Lease;
        internal bool RequestStarted;
        internal PopupStoreWalletReconciliation CurrentQuery;
        public string RequestId { get; }
        public PopupStoreWalletSession Session { get; }
        public PopupStoreWalletSnapshot InitialWallet { get; }
        public PopupStoreWalletClaimState State { get; internal set; }
        public PopupStoreWalletSnapshot ReconciledWallet { get; internal set; }
        public string Error { get; internal set; }
        public string DisplayError { get; internal set; }
        internal PopupStoreWalletClaim(PopupStoreServerWalletGuard owner, IPopupStoreWalletLease lease,
            string requestId, PopupStoreWalletSession session, PopupStoreWalletSnapshot initialWallet)
        { Owner = owner; Lease = lease; RequestId = requestId; Session = session; InitialWallet = initialWallet; }
    }

    /// <summary>Fresh QUERY + latest-wallet-read correlation; a newer query invalidates earlier responses.</summary>
    public sealed class PopupStoreWalletReconciliation
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public PopupStoreWalletClaim Claim { get; }
        internal PopupStoreWalletReconciliation(PopupStoreWalletClaim claim) { Claim = claim; }
    }

    /// <summary>Historical receipt only. BalanceAfter must never be treated as the current wallet balance.</summary>
    public sealed class PopupStoreHistoricalWalletGrant
    {
        public string ClaimId { get; }
        public long WalletVersion { get; }
        public int BalanceAfter { get; }
        public PopupStoreHistoricalWalletGrant(string claimId, long walletVersion, int balanceAfter)
        { ClaimId = claimId; WalletVersion = walletVersion; BalanceAfter = balanceAfter; }
    }

    public sealed class PopupStoreWalletClaimEvidence
    {
        public PopupStoreWalletReconciliation Query { get; }
        public string RequestId { get; }
        public string AccountId { get; }
        public string WalletId { get; }
        public PopupStoreWalletQueryOutcome Outcome { get; }
        public PopupStoreHistoricalWalletGrant Grant { get; }
        public PopupStoreWalletClaimEvidence(PopupStoreWalletReconciliation query, string requestId,
            string accountId, string walletId, PopupStoreWalletQueryOutcome outcome, PopupStoreHistoricalWalletGrant grant = null)
        { Query = query; RequestId = requestId; AccountId = accountId; WalletId = walletId; Outcome = outcome; Grant = grant; }
    }

    /// <summary>
    /// Construct only from an authenticated latest-wallet QUERY response bound to Query, never by wrapping
    /// a claim receipt's BalanceAfter. The future transport must verify this provenance; this DTO is not proof by itself.
    /// </summary>
    public sealed class PopupStoreLatestWalletSnapshot
    {
        public PopupStoreWalletReconciliation Query { get; }
        public PopupStoreWalletSnapshot Wallet { get; }
        public PopupStoreLatestWalletSnapshot(PopupStoreWalletReconciliation query, PopupStoreWalletSnapshot wallet)
        { Query = query; Wallet = wallet; }
    }

    /// <summary>
    /// Process-local reconciliation guard. It does not grant diamonds, implement server idempotency, or
    /// make legacy saves safe. Live admission stays closed until the complete wallet migration is supplied.
    /// </summary>
    public sealed class PopupStoreServerWalletGuard
    {
        public const string UnavailableReason = "서버 지갑 이전과 모든 기존 Dia 저장 경로의 보호가 확인되지 않았습니다.";
        private readonly IPopupStoreServerWalletBoundary _boundary;
        private readonly HashSet<string> _knownRequests = new HashSet<string>(StringComparer.Ordinal);
        private bool _entering;
        private bool _reconciling;
        public PopupStoreWalletClaim ActiveClaim { get; private set; }

        public PopupStoreServerWalletGuard(IPopupStoreServerWalletBoundary boundary = null)
        { _boundary = boundary ?? new LegacyPopupStoreServerWalletBoundary(); }

        public bool TryBeginClaim(string requestId, out PopupStoreWalletClaim claim, out string error)
        {
            claim = null; error = null;
            if (_entering || _reconciling || ActiveClaim != null)
            { error = "이전 지갑 요청의 확인이 끝나지 않았습니다."; return false; }
            _entering = true;
            try
            {
                if (!ValidId(requestId) || _boundary.Capabilities?.CanClaim != true)
                { error = UnavailableReason; return false; }
                var session = _boundary.CurrentSession;
                var local = _boundary.ReadLocalWallet();
                if (session == null || !ValidId(session.AccountId) || local?.IsValid != true
                    || !ValidId(local.WalletId) || local.AccountId != session.AccountId)
                { error = "인증된 계정과 현재 서버 지갑 근거가 필요합니다."; return false; }
                string key = RequestKey(session.AccountId, local.WalletId, requestId);
                if (_knownRequests.Contains(key)) { error = "이미 접수한 지갑 요청입니다."; return false; }
                if (!_boundary.TryAcquireClaimLease(session, local, out var lease, out error) || lease == null) return false;
                claim = new PopupStoreWalletClaim(this, lease, requestId, session, local);
                ActiveClaim = claim;
                _knownRequests.Add(key);
                if (!Current(claim) || !local.SameState(_boundary.ReadLocalWallet()))
                {
                    error = "지갑 보호 확보 중 계정 또는 지갑이 변경되었습니다.";
                    TryCancelBeforeRequest(claim, out _);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "지갑 보호 확인 실패: " + ex.GetType().Name;
                if (claim != null) claim.Error = error;
                return false;
            }
            finally { _entering = false; }
        }

        public bool TryMarkRequestStarted(PopupStoreWalletClaim claim, out string error)
        {
            error = null;
            if (!Owned(claim) || claim.RequestStarted || !Current(claim))
            { error = "현재 계정의 미전송 요청이 아닙니다."; return false; }
            try
            {
                if (!claim.InitialWallet.SameState(_boundary.ReadLocalWallet()) || !claim.Lease.TryMarkRequestStarted())
                { error = "전송 전 지갑 상태 또는 보호권이 변경되었습니다."; return false; }
                claim.RequestStarted = true;
                claim.State = PopupStoreWalletClaimState.Sending;
                if (!Current(claim) || !claim.InitialWallet.SameState(_boundary.ReadLocalWallet()))
                    return Unknown(claim, "전송 준비 중 인증 세션 또는 지갑이 변경되었습니다.", out error);
                return true;
            }
            catch (Exception ex) { error = "전송 준비 실패: " + ex.GetType().Name; return false; }
        }

        public bool TryCancelBeforeRequest(PopupStoreWalletClaim claim, out string error)
        {
            error = null;
            if (!Owned(claim) || claim.RequestStarted) { error = "전송된 요청의 보호는 취소할 수 없습니다."; return false; }
            try
            {
                if (!claim.Lease.TryCancelBeforeRequest()) { error = "미전송 보호권을 해제하지 못했습니다."; return false; }
                claim.State = PopupStoreWalletClaimState.CancelledBeforeRequest;
                _knownRequests.Remove(RequestKey(claim.Session.AccountId, claim.InitialWallet.WalletId, claim.RequestId));
                ActiveClaim = null;
                return true;
            }
            catch (Exception ex) { error = "보호권 해제 실패: " + ex.GetType().Name; return false; }
        }

        public void MarkIndeterminate(PopupStoreWalletClaim claim, string reason)
        {
            if (!Owned(claim) || !claim.RequestStarted) return;
            claim.State = PopupStoreWalletClaimState.Indeterminate;
            claim.Error = reason ?? "QUERY와 최신 지갑을 확인할 때까지 보호를 유지합니다.";
            claim.CurrentQuery = null; // An uncertain result requires a new QUERY, never replayed reconciliation data.
            try { claim.Lease.MarkIndeterminate(claim.Error); }
            catch { /* The guard retains ownership even when the boundary cannot record its diagnostic. */ }
        }

        public bool BeginReconciliation(PopupStoreWalletClaim claim, out PopupStoreWalletReconciliation query, out string error)
        {
            query = null; error = null;
            if (_reconciling || !Owned(claim) || !claim.RequestStarted || !Current(claim))
            { error = "원래 인증 세션에서만 요청과 최신 지갑을 확인할 수 있습니다."; return false; }
            query = claim.CurrentQuery = new PopupStoreWalletReconciliation(claim);
            return true;
        }

        public bool TryReconcile(PopupStoreWalletClaim claim, PopupStoreWalletClaimEvidence evidence,
            PopupStoreLatestWalletSnapshot latest, Action onDisplay, out string error)
        {
            error = null;
            if (_reconciling || !Owned(claim) || !claim.RequestStarted)
            { error = "처리 중이거나 이미 완료한 지갑 확인입니다."; return false; }
            _reconciling = true;
            try
            {
                if (!Current(claim) || evidence == null || latest?.Wallet?.IsValid != true
                    || claim.CurrentQuery == null || !ReferenceEquals(evidence.Query, claim.CurrentQuery)
                    || !ReferenceEquals(latest.Query, claim.CurrentQuery) || evidence.RequestId != claim.RequestId
                    || evidence.AccountId != claim.Session.AccountId || evidence.WalletId != claim.InitialWallet.WalletId
                    || !latest.Wallet.SameWallet(claim.InitialWallet))
                    return Unknown(claim, "원래 요청·인증 세션에 연결된 QUERY와 최신 지갑이 필요합니다.", out error);
                var grant = evidence.Grant;
                if (evidence.Outcome == PopupStoreWalletQueryOutcome.Committed)
                {
                    if (grant == null || !ValidId(grant.ClaimId) || grant.WalletVersion < 0 || grant.BalanceAfter < 0
                        || latest.Wallet.Version < grant.WalletVersion
                        || (latest.Wallet.Version == grant.WalletVersion && latest.Wallet.Balance != grant.BalanceAfter))
                        return Unknown(claim, "과거 지급 기록과 최신 지갑 버전을 확인할 수 없습니다.", out error);
                }
                else if (evidence.Outcome != PopupStoreWalletQueryOutcome.RejectedNotApplied || grant != null)
                    return Unknown(claim, "서버 지급 여부가 확정되지 않았습니다.", out error);

                var current = _boundary.ReadLocalWallet();
                if (current?.IsValid != true || !current.SameWallet(claim.InitialWallet)
                    || latest.Wallet.Version < current.Version || latest.Wallet.Version < claim.InitialWallet.Version
                    || (latest.Wallet.Version == current.Version && latest.Wallet.Balance != current.Balance))
                    return Unknown(claim, "최신 지갑의 버전이 오래되었거나 같은 버전의 잔액이 다릅니다.", out error);
                if (!Current(claim)) return Unknown(claim, "지갑 적용 전 인증 세션이 변경되었습니다.", out error);
                if (!current.SameState(latest.Wallet)
                    && !_boundary.TryApplyLatestWallet(claim.Lease, current, latest, out error))
                    return Unknown(claim, error ?? "최신 지갑을 안전하게 반영하지 못했습니다.", out error);
                if (!Current(claim) || !latest.Wallet.SameState(_boundary.ReadLocalWallet()))
                    return Unknown(claim, "지갑 적용 결과 또는 인증 세션을 확인할 수 없습니다.", out error);
                if (!claim.Lease.TryCompleteKnownOutcome())
                    return Unknown(claim, "지갑 반영 후 보호권 종료를 확인하지 못했습니다.", out error);
                claim.ReconciledWallet = latest.Wallet;
                claim.State = PopupStoreWalletClaimState.Reconciled;
                claim.Error = null;
                ActiveClaim = null; // Complete before any display callback can reenter or throw.
                try { onDisplay?.Invoke(); }
                catch (Exception ex) { claim.DisplayError = ex.GetType().Name; }
                return true;
            }
            catch (Exception ex) { return Unknown(claim, "지갑 확인 결과 미확정: " + ex.GetType().Name, out error); }
            finally { _reconciling = false; }
        }

        private bool Unknown(PopupStoreWalletClaim claim, string reason, out string error)
        { error = reason; MarkIndeterminate(claim, reason); return false; }
        private bool Owned(PopupStoreWalletClaim claim) => claim != null && ReferenceEquals(claim.Owner, this)
            && ReferenceEquals(ActiveClaim, claim);
        private bool Current(PopupStoreWalletClaim claim)
        {
            try { return Owned(claim) && _boundary.Capabilities?.CanClaim == true
                && ReferenceEquals(_boundary.CurrentSession, claim.Session) && claim.Lease.IsCurrent; }
            catch { return false; }
        }
        private static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value)
            && value.IndexOf('\n') < 0 && value.IndexOf('\0') < 0;
        private static string RequestKey(string account, string wallet, string request) => account + "\n" + wallet + "\n" + request;
    }
}
