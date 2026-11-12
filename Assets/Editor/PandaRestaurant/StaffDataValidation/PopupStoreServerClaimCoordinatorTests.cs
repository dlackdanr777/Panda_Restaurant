#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Muks.BackEnd;
using Newtonsoft.Json;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Real authority + protocol client + guard; only transport, atomic store, clocks and wallet are in memory.</summary>
public sealed class PopupStoreServerClaimCoordinatorTests
{
    [Test]
    public void DefaultBoundary_RejectsBeforeAnyClaimTransportOrGrant()
    {
        var h = new Harness();
        var coordinator = new PopupStoreServerClaimCoordinator(h.Client);
        PopupStoreRewardResponse result = null;
        Assert.That(coordinator.CanClaim, Is.False);
        Assert.That(coordinator.Claim(h.Session, h.Request, x => result = x), Is.False);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.RejectedBeforeSend));
        Assert.That(h.Transport.Commands, Is.Empty);
        Assert.That(h.Store.Account.Businesses[0].View.Grant, Is.Null);
    }

    [Test]
    public void DefaultPersistenceCannotAdvertisePaidAccessOrSubmitNewStartWithoutWalletMigration()
    {
        var h = new Harness();
        var adapter = new PopupStoreBackendPersistence(h.Client);
        PopupStoreBusinessAccessSnapshot access = null;
        adapter.QueryAccess(h.Session, value => access = value);
        Assert.That(access.Success, Is.False);
        PopupStoreBusinessStartResponse started = null;
        var request = new PopupStoreBusinessStartRequest(h.Session.AccountId, "unavailable-start",
            PopupStoreBusinessMode.Rewarded, PopupStoreServerAuthority.PolicyVersion);
        adapter.Submit(h.Session, request, value => started = value);
        Assert.That(started.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend));
        Assert.That(h.Transport.Commands, Is.Empty);
        Assert.That(h.Store.Account.Usage, Is.Empty);
    }

    [Test]
    public void WalletAdmissionCapabilityCanBeInspectedBeforeClientRecovery_WithoutAllowingClaim()
    {
        var h = new Harness();
        var freshClient = new PopupStoreServerClient(h.Transport, h.Journal, () => h.CurrentSession);
        var coordinator = new PopupStoreServerClaimCoordinator(freshClient, h.Boundary);
        Assert.That(coordinator.IsWalletReadyForAdmission, Is.True);
        Assert.That(coordinator.CanClaim, Is.False);
        Assert.That(h.Transport.Commands, Is.Empty);
        h.Boundary.Local = new PopupStoreWalletSnapshot("foreign-account", "wallet-a", 0, 100);
        Assert.That(coordinator.IsWalletReadyForAdmission, Is.False);
    }

    [Test]
    public void ExistingStartCanStillBeQueriedWhenDefaultWalletAdmissionIsUnavailable()
    {
        var h = new Harness(true);
        var request = new PopupStoreBusinessStartRequest(h.Session.AccountId, "existing-start",
            PopupStoreBusinessMode.Rewarded, PopupStoreServerAuthority.PolicyVersion);
        PopupStoreServerClientResult original = null;
        Assert.That(h.Client.Submit(new Command { Operation = Operation.Start, RequestId = request.StartRequestId,
            ExpectedPolicyVersion = request.PolicyVersion }, reply => original = reply), Is.True);
        Assert.That(original.Response.Status, Is.EqualTo(Status.Ok));
        var adapter = new PopupStoreBackendPersistence(h.Client);
        PopupStoreBusinessStartResponse recovered = null;
        adapter.Query(h.Session, request, value => recovered = value);
        Assert.That(recovered.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.Started));
        Assert.That(recovered.Record.BusinessSessionId, Is.EqualTo(original.Response.Business.BusinessSessionId));
        Assert.That(h.Transport.Commands.Count(x => x.Operation == Operation.Start), Is.EqualTo(1));
    }

    [Test]
    public void ClaimRequiresSeparateLatestQueryBeforeGrantedCompletion()
    {
        var h = new Harness();
        h.Transport.HoldQueries = true;
        var results = new List<PopupStoreRewardResponse>();
        Assert.That(h.Coordinator.Claim(h.Session, h.Request, results.Add), Is.True);
        Assert.That(h.Transport.Commands.Select(x => x.Operation), Is.EqualTo(new[] { Operation.Claim, Operation.Query }));
        Assert.That(results, Is.Empty);
        Assert.That(h.Boundary.Local.Balance, Is.EqualTo(100));
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
        Assert.That(h.Coordinator.ActiveClaim, Is.Not.Null);
        h.Transport.DeliverHeld();
        Assert.That(results.Single().Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Boundary.Local.Balance, Is.EqualTo(100 + h.Request.Diamonds));
        Assert.That(h.Boundary.Applies, Is.EqualTo(1));
        Assert.That(h.Coordinator.ActiveClaim, Is.Null);
        h.Transport.DeliverLastAgain();
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(h.Boundary.Applies, Is.EqualTo(1));
    }

    [Test]
    public void GachaSpendAfterGrant_ReconcilesLatestVersionInsteadOfHistoricalBalanceAfter()
    {
        var h = new Harness();
        h.Transport.AfterClaim = () => { h.Store.Account.Diamonds -= 10; h.Store.Account.WalletVersion++; };
        PopupStoreRewardResponse result = null;
        Assert.That(h.Coordinator.Claim(h.Session, h.Request, x => result = x), Is.True);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(result.Grant.BalanceAfter, Is.EqualTo(100 + h.Request.Diamonds));
        Assert.That(h.Boundary.Local.Balance, Is.EqualTo(90 + h.Request.Diamonds));
        Assert.That(h.Boundary.Local.Version, Is.EqualTo(result.Grant.BalanceVersion + 1));
        Assert.That(h.Boundary.Applies, Is.EqualTo(1));
    }

    [Test]
    public void LostClaimResponse_RecoversSameRequestWithoutAnotherClaim()
    {
        var h = new Harness();
        h.Transport.LoseNextClaimResponse = true;
        var results = new List<PopupStoreRewardResponse>();
        h.Coordinator.Claim(h.Session, h.Request, results.Add);
        var sent = h.Transport.Commands.Single();
        Assert.That(results.Single().Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        Assert.That(h.Journal.Read(h.Session.AccountId).RequestId, Is.EqualTo(sent.RequestId));
        Assert.That(h.Coordinator.ActiveClaim.State, Is.EqualTo(PopupStoreWalletClaimState.Indeterminate));
        Assert.That(h.Coordinator.Claim(h.Session, h.Request, results.Add), Is.False);
        Assert.That(h.Coordinator.Recover(h.Session, h.Request, results.Add), Is.True);
        Assert.That(results.Last().Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Transport.Commands.Count(x => x.Operation == Operation.Claim), Is.EqualTo(1));
        Assert.That(h.Transport.Commands.Any(x => x.Operation == Operation.Query && x.LookupRequestId == sent.RequestId), Is.True);
        Assert.That(h.Store.Account.Diamonds, Is.EqualTo(100 + h.Request.Diamonds));
        Assert.That(h.Boundary.Applies, Is.EqualTo(1));
    }

    [Test]
    public void RestartRecovery_UsesPendingCommandAndExistingBusinessQueriesWithoutNewClaim()
    {
        var h = new Harness();
        h.Transport.LoseNextClaimResponse = true;
        h.Coordinator.Claim(h.Session, h.Request, _ => { });
        var restartedSession = new PopupStoreRewardAccountSession(h.Session.AccountId);
        var boundary = new Boundary(restartedSession.AccountId);
        var client = new PopupStoreServerClient(h.Transport, h.Journal, () => restartedSession);
        var restarted = new PopupStoreServerClaimCoordinator(client, boundary);
        PopupStoreRewardResponse result = null;
        Assert.That(restarted.Recover(restartedSession, h.Request, x => result = x), Is.True);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Transport.Commands.Count(x => x.Operation == Operation.Claim), Is.EqualTo(1));
        Assert.That(boundary.Applies, Is.EqualTo(1));
        Assert.That(h.Journal.Read(restartedSession.AccountId), Is.Null);
    }

    [Test]
    public void LostLatestQuery_RetainsLeaseAfterClaimJournalCleared_ThenRecoversByBusiness()
    {
        var h = new Harness();
        h.Transport.LoseNextQueryResponse = true;
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        Assert.That(h.Journal.Read(h.Session.AccountId), Is.Null);
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Coordinator.Recover(h.Session, h.Request, x => result = x), Is.True);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Transport.Commands.Count(x => x.Operation == Operation.Claim), Is.EqualTo(1));
    }

    [Test]
    public void QueryWithoutLatestWallet_DoesNotApplyHistoricalGrant()
    {
        var h = new Harness();
        h.Transport.AlterQuery = reply => reply.Wallet = null;
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Local.Balance, Is.EqualTo(100));
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
    }

    [Test]
    public void LatestWalletForAnotherRow_CannotReleaseOriginalWalletLease()
    {
        var h = new Harness();
        h.Transport.AlterQuery = reply => reply.Wallet.WalletId = "another-wallet";
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
    }

    [Test]
    public void SettledNoReward_StillChecksLatestWallet_WithoutCreatingGrantOrCredit()
    {
        var h = new Harness(true);
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.NoReward));
        Assert.That(h.Transport.Commands.Select(x => x.Operation), Is.EqualTo(new[] { Operation.Claim, Operation.Query }));
        Assert.That(h.Store.Account.Businesses[0].View.Grant, Is.Null);
        Assert.That(h.Boundary.Local.Balance, Is.EqualTo(100));
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Lease.Completed, Is.True);
    }

    [Test]
    public void QueryWithDifferentImmutableGrant_CannotCompletePayout()
    {
        var h = new Harness();
        h.Transport.AlterQuery = reply =>
        {
            reply.Grant.GrantId = "changed-grant";
            reply.Business.Grant.GrantId = "changed-grant";
        };
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
    }

    [Test]
    public void AccountABAWhileLatestQueryPending_NeverAppliesOldReply()
    {
        var h = new Harness();
        h.Transport.HoldQueries = true;
        int completed = 0;
        h.Coordinator.Claim(h.Session, h.Request, _ => completed++);
        h.CurrentSession = new PopupStoreRewardAccountSession("account-b");
        h.Boundary.CurrentSession = new PopupStoreWalletSession("account-b", 2);
        h.CurrentSession = new PopupStoreRewardAccountSession(h.Session.AccountId);
        h.Boundary.CurrentSession = new PopupStoreWalletSession(h.Session.AccountId, 1);
        h.Transport.DeliverHeld();
        h.Coordinator.MarkResponseMissing();
        Assert.That(completed, Is.Zero);
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
    }

    [Test]
    public void ThrowingDisplay_CanExplicitlyRecoverSameReceiptWithoutRpcOrApply()
    {
        var h = new Harness();
        h.Coordinator.Claim(h.Session, h.Request, _ => throw new InvalidOperationException("display"));
        int calls = h.Transport.Commands.Count;
        PopupStoreRewardResponse recovered = null;
        Assert.That(h.Coordinator.Recover(h.Session, h.Request, x => recovered = x), Is.True);
        Assert.That(recovered.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Transport.Commands.Count, Is.EqualTo(calls));
        Assert.That(h.Boundary.Applies, Is.EqualTo(1));
        Assert.That(h.Coordinator.Claim(h.Session, h.Request, _ => { }), Is.False);
        Assert.That(h.Transport.Commands.Count, Is.EqualTo(calls));
    }

    [Test]
    public void CachedReceiptRecovery_RechecksLiveClientEpochEvenIfWalletBoundaryStillHasOldSession()
    {
        var h = new Harness();
        h.Coordinator.Claim(h.Session, h.Request, _ => { });
        int applies = h.Boundary.Applies;
        h.CurrentSession = new PopupStoreRewardAccountSession(h.Session.AccountId);
        PopupStoreRewardResponse result = null;
        Assert.That(h.Coordinator.Recover(h.Session, h.Request, x => result = x), Is.False);
        Assert.That(result?.Kind, Is.Not.EqualTo(PopupStoreRewardResponseKind.Granted));
        Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.Invalidated));
        Assert.That(h.Boundary.Applies, Is.EqualTo(applies));
    }

    [Test]
    public void FirstTransportUnavailable_IsConservativelyProtectedUntilAuthoritativeRecovery()
    {
        var h = new Harness();
        h.Transport.UnavailableNextClaim = true;
        PopupStoreRewardResponse result = null;
        h.Coordinator.Claim(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate));
        var pending = h.Journal.Read(h.Session.AccountId);
        Assert.That(pending, Is.Not.Null);
        Assert.That(pending.RequestId, Is.EqualTo(PopupStoreBackendPersistence.ClaimRequestId(h.Request.GrantKey)));
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
        Assert.That(h.Store.Account.Businesses[0].View.Grant, Is.Null);
        h.Coordinator.Recover(h.Session, h.Request, x => result = x);
        Assert.That(result.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Indeterminate), "No grant in one QUERY is not an in-flight request fence.");
        Assert.That(h.Journal.Read(h.Session.AccountId).Matches(pending), Is.True);
        Assert.That(h.Transport.Commands.Count(command => command.Operation == Operation.Claim), Is.EqualTo(1));
        Assert.That(h.Transport.Commands.Last().Operation, Is.EqualTo(Operation.Query));
        Assert.That(h.Transport.Commands.Last().LookupRequestId, Is.EqualTo(pending.RequestId));
        Assert.That(h.Boundary.Applies, Is.Zero);
        Assert.That(h.Boundary.Lease.IsCurrent, Is.True);
    }

    private sealed class Harness
    {
        public readonly PopupStoreRewardAccountSession Session = new PopupStoreRewardAccountSession("account-a");
        public PopupStoreRewardAccountSession CurrentSession;
        public readonly Store Store = new Store();
        public readonly Journal Journal = new Journal();
        public readonly Boundary Boundary;
        public readonly Transport Transport;
        public readonly PopupStoreServerClient Client;
        public readonly PopupStoreServerClaimCoordinator Coordinator;
        public readonly PopupStoreRewardRequest Request;
        public Harness(bool noReward = false)
        {
            CurrentSession = Session;
            var policy = PopupStoreServerAuthority.ApprovedPolicy();
            long ticks = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc).Ticks;
            var decision = new DecisionSnapshot { DecisionId = "decision-a", PolicyVersion = PopupStoreServerAuthority.RewardPolicyVersion,
                Grade = noReward ? 0 : 1, Kind = 0, Diamonds = noReward ? 0 : policy.RewardAmounts[0] };
            Store.Account = new AccountState { AccountId = Session.AccountId, WalletId = "wallet-a", WalletMigrationId = "memory-migration",
                WalletMigrationComplete = true, Diamonds = 100, WalletVersion = 0 };
            Store.Account.Businesses.Add(new BusinessState { Policy = policy, View = new BusinessSnapshot {
                AccountId = Session.AccountId, BusinessSessionId = "business-a", StartRequestId = "start-a",
                PolicyVersion = policy.Version, ClockProtocolVersion = "memory-clock", StartGameDay = PopupStoreServerAuthority.GameDay(new DateTimeOffset(ticks, TimeSpan.Zero)),
                StartedAtUtcTicks = ticks, Phase = noReward ? 1 : 2, HighestClearedGrade = noReward ? 0 : 1,
                TotalScore = noReward ? 0 : policy.Targets[0], SuccessfulOrders = noReward ? 0 : 2,
                EndReason = BusinessEndReason.Natural, IsSettled = true,
                ElapsedGameplaySeconds = noReward ? 22 : 49, PhaseDeadlineSeconds = noReward ? 22 : 49,
                Orders = Array.Empty<OrderSnapshot>(), Decision = decision } });
            Request = new PopupStoreRewardRequest(Session.AccountId, "business-a", decision.DecisionId,
                noReward ? PopupStoreGradeTier.None : PopupStoreGradeTier.C,
                PopupStoreRewardKind.Basic, decision.Diamonds, decision.PolicyVersion);
            Boundary = new Boundary(Session.AccountId);
            Transport = new Transport(new PopupStoreServerAuthority(Store, new Clock(), new Random(), new Lifecycle()), Session.AccountId);
            Client = new PopupStoreServerClient(Transport, Journal, () => CurrentSession);
            Assert.That(Client.Recover(_ => { }), Is.True);
            Assert.That(Client.State, Is.EqualTo(PopupStoreServerClientState.Ready));
            Transport.Commands.Clear();
            Coordinator = new PopupStoreServerClaimCoordinator(Client, Boundary);
        }
    }

    private sealed class Store : IAtomicAccountStore
    {
        public AccountState Account;
        public bool AtomicAccountTransactionsVerified => true;
        public bool FullWalletMigrationVerified => true;
        public AccountState Read(string account) => Account?.AccountId == account ? Account.Copy() : null;
        public Response Transact(string account, Func<AccountState, AtomicMutation> action)
        { var result = action(Read(account)); if (result.Commit) Account = result.State.Copy(); return result.Response; }
    }
    private sealed class Clock : IServerClock
    {
        public bool GameplayProtocolVerified => true;
        public string ProtocolVersion => "memory-clock";
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
        public bool TryGetElapsedGameplaySeconds(string account, string business, long started, out double elapsed)
        { elapsed = 49; return true; }
    }
    private sealed class Lifecycle : IServerOrderLifecycle
    {
        public bool ProtocolVerified => true;
        public bool TryGetArrivalGameplaySeconds(string account, string business, string order, out double arrival) { arrival = 0; return false; }
        public bool HasReleasedSlot(string account, string business, string order) => false;
    }
    private sealed class Random : ISecureRandom
    {
        private byte _next;
        public bool IsCryptographicallySecure => true; // Explicit memory model, never a production provider.
        public void Fill(byte[] bytes) { Array.Clear(bytes, 0, bytes.Length); bytes[0] = ++_next; }
    }
    private sealed class Journal : IPopupStoreServerRequestJournal
    {
        private PopupStorePendingServerRequest _pending;
        public PopupStorePendingServerRequest Read(string account) => _pending;
        public bool Prepare(string account, PopupStorePendingServerRequest request)
        {
            if (_pending != null)
            { if (!_pending.Matches(request)) throw new InvalidOperationException(); return false; }
            _pending = request; return true;
        }
        public void Complete(string account, PopupStorePendingServerRequest expected)
        { if (_pending != null && !_pending.Matches(expected)) throw new InvalidOperationException(); _pending = null; }
    }
    private sealed class Lease : IPopupStoreWalletLease
    {
        public bool Started, Completed;
        public bool IsCurrent => !Completed;
        public bool TryMarkRequestStarted() { if (Started || Completed) return false; Started = true; return true; }
        public void MarkIndeterminate(string reason) { }
        public bool TryCompleteKnownOutcome() { if (!Started || Completed) return false; Completed = true; return true; }
        public bool TryCancelBeforeRequest() { if (Started || Completed) return false; Completed = true; return true; }
    }
    private sealed class Boundary : IPopupStoreServerWalletBoundary
    {
        public PopupStoreWalletCapabilities Capabilities => new PopupStoreWalletCapabilities(true, true);
        public PopupStoreWalletSession CurrentSession { get; set; }
        public PopupStoreWalletSnapshot Local;
        public Lease Lease;
        public int Applies;
        public Boundary(string account) { CurrentSession = new PopupStoreWalletSession(account, 1); Local = new PopupStoreWalletSnapshot(account, "wallet-a", 0, 100); }
        public PopupStoreWalletSnapshot ReadLocalWallet() => Local;
        public bool TryAcquireClaimLease(PopupStoreWalletSession session, PopupStoreWalletSnapshot expectedLocal,
            out IPopupStoreWalletLease lease, out string error)
        { error = null; lease = null; if (Lease?.IsCurrent == true || !ReferenceEquals(session, CurrentSession) || !expectedLocal.SameState(Local)) return false;
            lease = Lease = new Lease(); return true; }
        public bool TryApplyLatestWallet(IPopupStoreWalletLease lease, PopupStoreWalletSnapshot expectedLocal,
            PopupStoreLatestWalletSnapshot latest, out string error)
        { error = null; if (!ReferenceEquals(lease, Lease) || !lease.IsCurrent || !expectedLocal.SameState(Local)) return false;
            Local = latest.Wallet; Applies++; return true; }
    }
    private sealed class Transport : IPopupStoreFunctionTransport
    {
        private readonly PopupStoreServerAuthority _authority;
        private readonly AuthenticatedPrincipal _principal;
        private Action _held, _last;
        public readonly List<Command> Commands = new List<Command>();
        public bool HoldQueries, LoseNextClaimResponse, LoseNextQueryResponse, UnavailableNextClaim;
        public Action AfterClaim;
        public Action<Response> AlterQuery;
        public Transport(PopupStoreServerAuthority authority, string account)
        { _authority = authority; _principal = new AuthenticatedPrincipal(account); }
        public void Send(string json, Action<PopupStoreFunctionTransportResult> completed)
        {
            var command = JsonConvert.DeserializeObject<Command>(json); Commands.Add(command);
            if (command.Operation == Operation.Claim && UnavailableNextClaim)
            { UnavailableNextClaim = false; completed(new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.Unavailable)); return; }
            var response = _authority.Execute(_principal, command);
            if (command.Operation == Operation.Claim)
            {
                AfterClaim?.Invoke();
                if (LoseNextClaimResponse) { LoseNextClaimResponse = false; completed(new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.Indeterminate)); return; }
            }
            if (command.Operation == Operation.Query)
            {
                if (LoseNextQueryResponse) { LoseNextQueryResponse = false; completed(new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.Indeterminate)); return; }
                AlterQuery?.Invoke(response);
            }
            string captured = JsonConvert.SerializeObject(response);
            Action reply = () => completed(new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.ResponseReceived, captured));
            _last = reply;
            if (command.Operation == Operation.Query && HoldQueries) { _held = reply; return; }
            reply();
        }
        public void DeliverHeld() { var reply = _held; _held = null; reply(); }
        public void DeliverLastAgain() => _last();
    }
}
#endif
