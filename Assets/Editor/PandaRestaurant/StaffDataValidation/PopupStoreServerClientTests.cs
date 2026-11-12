#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Real authority and client state machines with isolated in-memory store, journal and
/// transport. No Backend SDK invocation, login, live wallet, Scene or on-disk account state.</summary>
public sealed class PopupStoreServerClientTests
{
    private sealed class Store : IAtomicAccountStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, AccountState> _accounts = new Dictionary<string, AccountState>();
        public bool AtomicAccountTransactionsVerified => true;
        public bool FullWalletMigrationVerified => true;
        public int Writes;
        public Store()
        {
            foreach (var id in new[] { "gamer-A", "gamer-B" }) _accounts[id] = new AccountState { AccountId = id,
                WalletId = "wallet-" + id, WalletMigrationId = "isolated-migration", WalletMigrationComplete = true, Diamonds = 100 };
        }
        public AccountState Read(string account)
        { lock (_gate) return _accounts.TryGetValue(account, out var state) ? state.Copy() : null; }
        public Response Transact(string account, Func<AccountState, AtomicMutation> operation)
        {
            lock (_gate)
            {
                var mutation = operation(Read(account));
                if (mutation.Commit) { _accounts[account] = mutation.State.Copy(); Writes++; }
                return mutation.Response;
            }
        }
    }

    private sealed class Clock : IServerClock
    {
        public bool GameplayProtocolVerified => true;
        public string ProtocolVersion => "isolated-clock-v1";
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
        public double Elapsed;
        public bool TryGetElapsedGameplaySeconds(string account, string business, long started, out double elapsed)
        { elapsed = Elapsed; return true; }
    }
    private sealed class Lifecycle : IServerOrderLifecycle
    {
        public bool ProtocolVerified => true;
        public readonly Dictionary<string, double> Arrivals = new Dictionary<string, double>();
        public bool Released = true;
        public bool TryGetArrivalGameplaySeconds(string account, string business, string order, out double arrival)
        { return Arrivals.TryGetValue(order, out arrival); }
        public bool HasReleasedSlot(string account, string business, string order) => Released;
    }
    private sealed class Random : ISecureRandom
    {
        private int _ids;
        public bool IsCryptographicallySecure => true; // Explicit test-only deterministic dependency.
        public void Fill(byte[] bytes)
        {
            Array.Clear(bytes, 0, bytes.Length);
            if (bytes.Length != 4) { int id = Interlocked.Increment(ref _ids); bytes[0] = (byte)id; bytes[1] = (byte)(id >> 8); }
        }
    }
    private sealed class Journal : IPopupStoreServerRequestJournal
    {
        public readonly Dictionary<string, PopupStorePendingServerRequest> Pending = new Dictionary<string, PopupStorePendingServerRequest>();
        public int Prepared, Completed;
        public bool FailPrepare, FailRead, FailComplete;
        public Action BeforePrepare;
        public PopupStorePendingServerRequest Read(string account)
        { if (FailRead) throw new InvalidOperationException("Unreadable test journal."); return Pending.TryGetValue(account, out var request) ? request : null; }
        public bool Prepare(string account, PopupStorePendingServerRequest request)
        {
            BeforePrepare?.Invoke();
            if (FailPrepare) throw new InvalidOperationException("Test durable prepare failed.");
            var previous = Read(account); if (previous != null && !previous.Matches(request)) throw new InvalidOperationException("Conflict.");
            if (previous != null) return false;
            Pending[account] = request; Prepared++; return true;
        }
        public void Complete(string account, PopupStorePendingServerRequest expected)
        {
            if (FailComplete) throw new InvalidOperationException("Test durable completion failed.");
            var previous = Read(account); if (previous != null && !previous.Matches(expected)) throw new InvalidOperationException("Conflict.");
            Pending.Remove(account); Completed++;
        }
    }
    private sealed class AdmissionBoundary : IPopupStoreServerWalletBoundary
    {
        public PopupStoreWalletCapabilities Capabilities { get; } = new PopupStoreWalletCapabilities(true, true);
        public PopupStoreWalletSession CurrentSession { get; } = new PopupStoreWalletSession("gamer-A", 1);
        public PopupStoreWalletSnapshot ReadLocalWallet() => new PopupStoreWalletSnapshot("gamer-A", "wallet-gamer-A", 0, 100);
        public bool TryAcquireClaimLease(PopupStoreWalletSession session, PopupStoreWalletSnapshot expected,
            out IPopupStoreWalletLease lease, out string error)
        { lease = null; error = "Admission-only fixture."; return false; }
        public bool TryApplyLatestWallet(IPopupStoreWalletLease lease, PopupStoreWalletSnapshot expected,
            PopupStoreLatestWalletSnapshot latest, out string error)
        { error = "Admission-only fixture."; return false; }
    }
    private sealed class Transport : IPopupStoreFunctionTransport
    {
        internal sealed class Invocation
        {
            public string Json, Account;
            public Action<PopupStoreFunctionTransportResult> Callback;
            public PopupStoreFunctionTransportResult Result;
        }
        private readonly PopupStoreServerAuthority _authority;
        private readonly Func<string> _account;
        public readonly List<Invocation> Calls = new List<Invocation>();
        public bool HoldAfterCommit, HoldBeforeExecute;
        public PopupStoreFunctionTransportOutcome? NextFailure;
        public Action<Command> BeforeInvocation;
        public Action<Response> MutateResponse;
        public Func<string, string> MutateJson;
        public Transport(PopupStoreServerAuthority authority, Func<string> account) { _authority = authority; _account = account; }
        public void Send(string json, Action<PopupStoreFunctionTransportResult> completed)
        {
            var command = JsonConvert.DeserializeObject<Command>(json);
            BeforeInvocation?.Invoke(command);
            var invocation = new Invocation { Json = json, Account = _account(), Callback = completed }; Calls.Add(invocation);
            if (NextFailure.HasValue)
            {
                invocation.Result = new PopupStoreFunctionTransportResult(NextFailure.Value, errorCode: "isolated-no-send"); NextFailure = null;
                completed(invocation.Result); return;
            }
            if (HoldBeforeExecute) return;
            Execute(invocation);
            if (!HoldAfterCommit) completed(invocation.Result);
        }
        private void Execute(Invocation invocation)
        {
            var response = _authority.Execute(new AuthenticatedPrincipal(invocation.Account), JsonConvert.DeserializeObject<Command>(invocation.Json));
            MutateResponse?.Invoke(response);
            string json = JsonConvert.SerializeObject(response);
            invocation.Result = new PopupStoreFunctionTransportResult(PopupStoreFunctionTransportOutcome.ResponseReceived, MutateJson == null ? json : MutateJson(json));
        }
        public void Deliver(int index)
        {
            var invocation = Calls[index]; if (invocation.Result == null) Execute(invocation);
            invocation.Callback(invocation.Result);
        }
    }

    private sealed class Harness
    {
        public readonly Store Store = new Store();
        public readonly Clock Clock = new Clock();
        public readonly Lifecycle Lifecycle = new Lifecycle();
        public readonly Journal Journal = new Journal();
        public readonly PopupStoreServerAuthority Authority;
        public readonly Transport Transport;
        public PopupStoreRewardAccountSession Current = new PopupStoreRewardAccountSession("gamer-A");
        public PopupStoreServerClient Client;
        public PopupStoreServerClientResult Last;
        public string Business;
        private int _serial;
        public Harness()
        {
            Authority = new PopupStoreServerAuthority(Store, Clock, new Random(), Lifecycle);
            Transport = new Transport(Authority, () => Current.AccountId);
            Client = NewClient();
        }
        public PopupStoreServerClient NewClient() => new PopupStoreServerClient(Transport, Journal, () => Current);
        public void Ready()
        { Assert.That(Client.Recover(value => Last = value), Is.True); Assert.That(Client.State, Is.EqualTo(PopupStoreServerClientState.Ready), Last?.Error); }
        public Command Command(Operation operation)
        {
            return new Command { Operation = operation, RequestId = "client-" + Interlocked.Increment(ref _serial),
                BusinessSessionId = operation == Operation.Start ? null : Business,
                ExpectedPolicyVersion = operation == Operation.Start ? PopupStoreServerAuthority.PolicyVersion : null };
        }
        public Response Submit(Command command)
        {
            Last = null; Assert.That(Client.Submit(command, value => Last = value), Is.True);
            Assert.That(Last?.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed), Last?.Error);
            Assert.That(Last.Response.Status, Is.EqualTo(Status.Ok), Last.Response.Error); return Last.Response;
        }
        public Response Start()
        { Ready(); var response = Submit(Command(Operation.Start)); Business = response.Business.BusinessSessionId; return response; }
        public Response IssueAndPack()
        {
            var issue = Submit(Command(Operation.IssueOrder)); Lifecycle.Arrivals[issue.Order.OrderId] = Clock.Elapsed;
            var pack = Command(Operation.Pack); pack.OrderId = issue.Order.OrderId; pack.ReceiptId = "receipt-" + pack.RequestId; pack.Materials = issue.Order.Materials;
            return Submit(pack);
        }
        public void SettleC()
        {
            Start(); IssueAndPack(); Clock.Elapsed = 3.2; IssueAndPack();
            Clock.Elapsed = 22; Submit(Command(Operation.Settle)); Clock.Elapsed = 49; Submit(Command(Operation.Settle));
        }
    }

    [Test]
    public void StartupRequiresRecoveryAndDurableJournalBeforeAnyMutationDispatch()
    {
        var h = new Harness(); Assert.That(h.Client.Submit(h.Command(Operation.Start), value => h.Last = value), Is.False);
        Assert.That(h.Transport.Calls.Count, Is.Zero); Assert.That(h.Journal.Prepared, Is.Zero);
        h.Ready();
        h.Transport.BeforeInvocation = command => {
            if (command.Operation == Operation.Start)
            {
                var pending = h.Journal.Read(h.Current.AccountId); Assert.That(pending, Is.Not.Null);
                Assert.That(pending.RequestId, Is.EqualTo(command.RequestId));
                Assert.That(PopupStoreServerAuthority.CommandHash(JsonConvert.DeserializeObject<Command>(pending.CommandJson)), Is.EqualTo(PopupStoreServerAuthority.CommandHash(command)));
            }
        };
        h.Submit(h.Command(Operation.Start)); Assert.That(h.Journal.Completed, Is.EqualTo(1)); Assert.That(h.Journal.Read(h.Current.AccountId), Is.Null);
    }

    [Test]
    public void FullTypedOrderPackingSettlementAndClaimAcceptRealAuthoritativeWire()
    {
        var h = new Harness(); h.SettleC(); var response = h.Submit(h.Command(Operation.Claim));
        Assert.That(response.Decision.Grade, Is.EqualTo(1)); Assert.That(response.Grant.Diamonds, Is.EqualTo(1));
        Assert.That(response.Grant.BalanceAfter, Is.EqualTo(101)); Assert.That(response.Wallet, Is.Null);
        Assert.That(h.Store.Read("gamer-A").Diamonds, Is.EqualTo(101));
    }

    [Test]
    public void LostCommittedStartReplyRecoversWithoutAnotherQuotaUseAndIgnoresLateDuplicate()
    {
        var h = new Harness(); h.Ready(); h.Transport.HoldAfterCommit = true; int callbackCount = 0;
        var command = h.Command(Operation.Start); Assert.That(h.Client.Submit(command, value => callbackCount++), Is.True);
        int original = h.Transport.Calls.Count - 1; h.Client.MarkResponseMissing(); h.Transport.HoldAfterCommit = false;
        Assert.That(h.Client.Recover(value => h.Last = value), Is.True); Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
        Assert.That(h.Journal.Read("gamer-A"), Is.Null); h.Transport.Deliver(original); h.Transport.Deliver(original);
        Assert.That(callbackCount, Is.Zero); Assert.That(h.Store.Read("gamer-A").Usage[0].Used, Is.EqualTo(1)); Assert.That(h.Store.Read("gamer-A").Businesses.Count, Is.EqualTo(1));
    }

    [Test]
    public void LostCommittedClaimReplyRecoversOriginalGrantAndCurrentWallet()
    {
        var h = new Harness(); h.SettleC(); h.Transport.HoldAfterCommit = true;
        var claim = h.Command(Operation.Claim); h.Client.Submit(claim, value => h.Last = value); h.Client.MarkResponseMissing();
        h.Transport.HoldAfterCommit = false; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Last.Response.AppliedOperation, Is.EqualTo(Operation.Claim));
        Assert.That(h.Last.Response.Grant.GrantId, Is.Not.Empty); Assert.That(h.Last.Response.Wallet.Balance, Is.EqualTo(101));
        Assert.That(h.Store.Read("gamer-A").WalletVersion, Is.EqualTo(1)); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
    }

    [Test]
    public void MissingLookupRetainsPendingAndExplicitRetryReusesExactSameIdentity()
    {
        var h = new Harness(); h.Ready(); h.Transport.HoldBeforeExecute = true;
        var command = h.Command(Operation.Start); h.Client.Submit(command, value => h.Last = value); h.Client.MarkResponseMissing();
        string persisted = h.Journal.Read("gamer-A").CommandJson; h.Transport.HoldBeforeExecute = false;
        h.Client.Recover(value => h.Last = value); Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
        Assert.That(h.Journal.Read("gamer-A").CommandJson, Is.EqualTo(persisted));
        Assert.That(h.Client.RetrySamePending(value => h.Last = value), Is.True);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
        Assert.That(h.Transport.Calls[h.Transport.Calls.Count - 1].Json, Is.EqualTo(persisted)); Assert.That(h.Store.Read("gamer-A").Businesses.Count, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void LookupMustCorrelateExactAppliedRequestOperationAndHash(int field)
    {
        var h = new Harness(); h.Ready(); h.Transport.HoldAfterCommit = true;
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value); h.Client.MarkResponseMissing(); h.Transport.HoldAfterCommit = false;
        h.Transport.MutateResponse = response => {
            if (field == 0) response.AppliedRequestId = "another-request";
            if (field == 1) response.AppliedOperation = Operation.Claim;
            if (field == 2) response.AppliedCommandHash = "wrong-hash";
        };
        h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Transport.MutateResponse = null; h.Client.Recover(value => h.Last = value); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
    }

    [Test]
    public void SessionAToBToNewAIgnoresOldCallbackAndRequiresNewClientRecovery()
    {
        var h = new Harness(); h.Ready(); h.Transport.HoldAfterCommit = true; int callbacks = 0;
        h.Client.Submit(h.Command(Operation.Start), value => callbacks++); int call = h.Transport.Calls.Count - 1;
        h.Current = new PopupStoreRewardAccountSession("gamer-B"); h.Current = new PopupStoreRewardAccountSession("gamer-A");
        h.Transport.Deliver(call); h.Transport.Deliver(call);
        Assert.That(callbacks, Is.Zero); Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.Invalidated)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Client = h.NewClient(); h.Transport.HoldAfterCommit = false; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
    }

    [Test]
    public void DuplicateCurrentSuccessInvokesCompletionOnlyOnce()
    {
        var h = new Harness(); h.Ready(); int callbacks = 0;
        h.Client.Submit(h.Command(Operation.Start), value => callbacks++); int call = h.Transport.Calls.Count - 1;
        h.Transport.Deliver(call); h.Transport.Deliver(call);
        Assert.That(callbacks, Is.EqualTo(1)); Assert.That(h.Journal.Completed, Is.EqualTo(1));
    }

    [Test]
    public void CallerMutationCannotChangeDispatchedOrPersistedCommand()
    {
        var h = new Harness(); h.Ready(); h.Transport.HoldBeforeExecute = true;
        var command = h.Command(Operation.Start); string originalId = command.RequestId;
        h.Client.Submit(command, value => h.Last = value); int call = h.Transport.Calls.Count - 1;
        command.RequestId = "changed"; command.ExpectedPolicyVersion = "unapproved";
        Assert.That(h.Journal.Read("gamer-A").RequestId, Is.EqualTo(originalId)); h.Transport.Deliver(call);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Last.Response.Business.StartRequestId, Is.EqualTo(originalId));
    }

    [Test]
    public void EveryNoSendOutcomeRetainsSharedDurableRequestUntilExplicitRecovery()
    {
        var h = new Harness(); h.Ready(); h.Transport.NextFailure = PopupStoreFunctionTransportOutcome.Unavailable;
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        string pending = h.Journal.Read("gamer-A").CommandJson;
        h.Transport.NextFailure = PopupStoreFunctionTransportOutcome.RejectedBeforeSend;
        h.Client.RetrySamePending(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null); Assert.That(h.Store.Writes, Is.Zero);
        Assert.That(h.Journal.Read("gamer-A").CommandJson, Is.EqualTo(pending)); Assert.That(h.Journal.Completed, Is.Zero);
    }

    [Test]
    public void FailedOrCorruptJournalAndInvalidCommandNeverReachTransport()
    {
        var h = new Harness(); h.Ready(); int before = h.Transport.Calls.Count;
        h.Journal.FailPrepare = true; Assert.That(h.Client.Submit(h.Command(Operation.Start), value => h.Last = value), Is.False);
        Assert.That(h.Transport.Calls.Count, Is.EqualTo(before)); h.Journal.FailPrepare = false;
        var malformed = h.Command(Operation.Pack); malformed.BusinessSessionId = "business-test"; malformed.OrderId = "order-test";
        Assert.That(h.Client.Submit(malformed, value => h.Last = value), Is.False); Assert.That(h.Transport.Calls.Count, Is.EqualTo(before));
        Assert.That(h.Journal.Read("gamer-A"), Is.Null); h.Journal.FailRead = true;
        Assert.That(h.Client.Recover(value => h.Last = value), Is.False); Assert.That(h.Transport.Calls.Count, Is.EqualTo(before));
    }

    [Test]
    public void FailedLocalJournalCompletionRetainsCommittedRequestForRecovery()
    {
        var h = new Harness(); h.Ready(); h.Journal.FailComplete = true;
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Journal.FailComplete = false; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Store.Read("gamer-A").Usage[0].Used, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MalformedAuthenticatedStartPayloadDoesNotClearPending(int field)
    {
        var h = new Harness(); h.Ready(); h.Transport.MutateResponse = response => {
            if (field == 0) response.Access.AccountId = "foreign-account";
            if (field == 1) response.Business.TotalScore = -1;
            if (field == 2) response.Business.Phase = 99;
            if (field == 3) response.Business.Orders = null;
        };
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Transport.MutateResponse = null; h.Client.Recover(value => h.Last = value); Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ContradictoryClaimAndHistoricalWalletPayloadsDoNotClearPending(int field)
    {
        var h = new Harness(); h.SettleC(); h.Transport.MutateResponse = response => {
            if (field == 0) { response.Decision.Diamonds++; response.Business.Decision.Diamonds++; response.Grant.Diamonds++; response.Business.Grant.Diamonds++; }
            if (field == 1) { response.Grant.GrantKey = "foreign-grant"; response.Business.Grant.GrantKey = "foreign-grant"; }
            if (field == 2)
            {
                response.Decision = null; response.Grant = null; response.Business.Decision = null; response.Business.Grant = null;
                response.Business.IsSettled = false; response.Business.EndReason = BusinessEndReason.None; response.Business.Phase = 1;
                response.Business.HighestClearedGrade = 0; response.Business.TotalScore = 0; response.Business.SuccessfulOrders = 0; response.Business.Orders = new OrderSnapshot[0];
            }
            if (field == 3) response.Wallet = new WalletSnapshot { AccountId = "gamer-A", WalletId = "wallet-gamer-A", Balance = response.Grant.BalanceAfter, Version = response.Grant.WalletVersion };
        };
        h.Client.Submit(h.Command(Operation.Claim), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Transport.MutateResponse = null; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Store.Read("gamer-A").Diamonds, Is.EqualTo(101));
    }

    [Test]
    public void MalformedIssuedOrderIsRecoverableAndDoesNotTriggerAnotherRandomOrder()
    {
        var h = new Harness(); h.Start(); h.Transport.MutateResponse = response => response.Order.Slot = 8;
        h.Client.Submit(h.Command(Operation.IssueOrder), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); h.Transport.MutateResponse = null;
        h.Client.Recover(value => h.Last = value); Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
        Assert.That(h.Store.Read("gamer-A").Businesses[0].View.Orders.Length, Is.EqualTo(1));
    }

    [Test]
    public void ExistingAdmissionInterfacesUseRealAuthorityAccessStartAndRecordedQuery()
    {
        var h = new Harness(); h.Ready(); var adapter = new PopupStoreBackendPersistence(h.Client,
            new PopupStoreServerClaimCoordinator(h.Client, new AdmissionBoundary()));
        PopupStoreBusinessAccessSnapshot access = null;
        adapter.QueryAccess(h.Current, value => access = value);
        Assert.That(access, Is.Not.Null); Assert.That(access.Success, Is.True); Assert.That(access.Session, Is.SameAs(h.Current));
        Assert.That(access.DailyLimit, Is.EqualTo(1)); Assert.That(access.Remaining, Is.EqualTo(1)); Assert.That(h.Store.Writes, Is.Zero);
        var request = new PopupStoreBusinessStartRequest(h.Current.AccountId, "admission-request", PopupStoreBusinessMode.Rewarded, access.PolicyVersion);
        int localCompletions = 0;
        var transaction = new PopupStoreBusinessStartTransaction(request, adapter, () => h.Current,
            new PopupStoreBusinessStartContext(), record => localCompletions++);
        Assert.That(transaction.TrySubmit(), Is.True); Assert.That(transaction.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
        Assert.That(localCompletions, Is.EqualTo(1)); Assert.That(transaction.Record.Matches(request), Is.True);
        PopupStoreBusinessStartResponse queried = null; adapter.Query(h.Current, request, value => queried = value);
        Assert.That(queried.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.Started));
        Assert.That(queried.Record.BusinessSessionId, Is.EqualTo(transaction.Record.BusinessSessionId));
        adapter.QueryAccess(h.Current, value => access = value);
        Assert.That(access.Remaining, Is.Zero); Assert.That(h.Store.Read("gamer-A").Usage[0].Used, Is.EqualTo(1));
    }

    [Test]
    public void DefaultPersistenceRewardSubmissionIsBlockedByWalletGateBeforeTransport()
    {
        var h = new Harness(); h.SettleC(); var decision = h.Last.Response.Decision;
        var request = new PopupStoreRewardRequest(h.Current.AccountId, h.Business, decision.DecisionId,
            (PopupStoreGradeTier)decision.Grade, (PopupStoreRewardKind)decision.Kind, decision.Diamonds, decision.PolicyVersion);
        int sent = h.Transport.Calls.Count; var adapter = new PopupStoreBackendPersistence(h.Client); PopupStoreRewardResponse response = null;
        adapter.Submit(h.Current, request, value => response = value);
        Assert.That(response, Is.Not.Null); Assert.That(response.Kind, Is.EqualTo(PopupStoreRewardResponseKind.RejectedBeforeSend));
        Assert.That(h.Transport.Calls.Count, Is.EqualTo(sent)); Assert.That(h.Store.Read("gamer-A").Diamonds, Is.EqualTo(100));
        Assert.That(h.Store.Read("gamer-A").Businesses[0].View.Grant, Is.Null);
    }

    [Test]
    public void PersistenceRejectsClaimCoordinatorOwnedByAnotherClient()
    {
        var h = new Harness(); var other = h.NewClient(); var coordinator = new PopupStoreServerClaimCoordinator(other);
        Assert.Throws<ArgumentException>(() => new PopupStoreBackendPersistence(h.Client, coordinator));
        Assert.That(h.Transport.Calls.Count, Is.Zero); Assert.That(h.Store.Writes, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MissingOrDuplicateResponseEnvelopeFieldsNeverUseDtoDefaults(int field)
    {
        var h = new Harness(); h.Ready(); h.Transport.MutateJson = json => {
            if (field == 2) return "{\"Status\":0," + json.Substring(1);
            var obj = JObject.Parse(json); obj.Remove(field == 0 ? "Protocol" : "Status"); return obj.ToString(Formatting.None);
        };
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Transport.MutateJson = null; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed)); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
    }

    [Test]
    public void RecordedRequestReadWithoutLocalJournalStillChecksAppliedIdentity()
    {
        var h = new Harness(); var started = h.Start();
        h.Transport.MutateResponse = response => response.AppliedRequestId = "another-record";
        h.Client.QueryRecordedRequest(started.Business.StartRequestId, value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MissingOrCoercedNestedValueFieldsCannotClearPending(int field)
    {
        var h = new Harness(); h.Ready(); h.Transport.MutateJson = json => {
            var obj = JObject.Parse(json);
            if (field == 0) ((JObject)obj["Business"]).Remove("TotalScore");
            if (field == 1) obj["Access"]["Remaining"] = "0";
            if (field == 2) obj["Business"]["IsSettled"] = "false";
            return obj.ToString(Formatting.None);
        };
        h.Client.Submit(h.Command(Operation.Start), value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Indeterminate)); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        h.Transport.MutateJson = null; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
    }

    [Test]
    public void AdmissionQueryRejectsInvalidForeignAndPracticeRequestsBeforeTransport()
    {
        var h = new Harness(); h.Ready(); var adapter = new PopupStoreBackendPersistence(h.Client); int sends = h.Transport.Calls.Count;
        var cases = new PopupStoreBusinessStartRequest[] { null,
            new PopupStoreBusinessStartRequest(h.Current.AccountId, "", PopupStoreBusinessMode.Rewarded, PopupStoreServerAuthority.PolicyVersion),
            new PopupStoreBusinessStartRequest("foreign-account", "start", PopupStoreBusinessMode.Rewarded, PopupStoreServerAuthority.PolicyVersion),
            new PopupStoreBusinessStartRequest(h.Current.AccountId, "start", PopupStoreBusinessMode.Practice, PopupStoreServerAuthority.PolicyVersion) };
        foreach (var request in cases)
        {
            PopupStoreBusinessStartResponse response = null; adapter.Query(h.Current, request, value => response = value);
            Assert.That(response, Is.Not.Null); Assert.That(response.Kind, Is.Not.EqualTo(PopupStoreBusinessStartResponseKind.Started));
        }
        Assert.That(h.Transport.Calls.Count, Is.EqualTo(sends)); Assert.That(h.Store.Writes, Is.Zero);
    }

    [Test]
    public void AtomicPrepareLosingClientCannotDispatchOrEraseAnotherClientsIdenticalPendingRequest()
    {
        var h = new Harness(); h.Ready(); var other = h.NewClient(); other.Recover(_ => { });
        var command = h.Command(Operation.Start); h.Transport.HoldAfterCommit = true;
        int sends = h.Transport.Calls.Count;
        h.Journal.BeforePrepare = () => {
            h.Journal.BeforePrepare = null;
            Assert.That(other.Submit(command, _ => { }), Is.True);
        };
        Assert.That(h.Client.Submit(command, value => h.Last = value), Is.False);
        Assert.That(h.Transport.Calls.Count, Is.EqualTo(sends + 1)); Assert.That(h.Journal.Prepared, Is.EqualTo(1));
        Assert.That(h.Journal.Completed, Is.Zero); Assert.That(h.Journal.Read("gamer-A"), Is.Not.Null);
        Assert.That(h.Store.Read("gamer-A").Usage[0].Used, Is.EqualTo(1));
        h.Transport.Deliver(sends); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
    }

    [Test]
    public void ObservedInvalidationCannotReviveByRestoringTheOriginalSessionObject()
    {
        var h = new Harness(); h.Ready(); var original = h.Current; int sends = h.Transport.Calls.Count;
        h.Current = new PopupStoreRewardAccountSession("gamer-B");
        Assert.That(h.Client.QueryAccess(value => h.Last = value), Is.False);
        Assert.That(h.Client.State, Is.EqualTo(PopupStoreServerClientState.Invalidated));
        h.Current = original;
        Assert.That(h.Client.QueryAccess(value => h.Last = value), Is.False);
        Assert.That(h.Client.IsCurrentSession, Is.False); Assert.That(h.Transport.Calls.Count, Is.EqualTo(sends));
    }

    [Test]
    public void DefaultPersistenceBlocksAdmissionBeforeAccessReadOrQuotaConsumption()
    {
        var h = new Harness(); h.Ready(); int sends = h.Transport.Calls.Count;
        var adapter = new PopupStoreBackendPersistence(h.Client); PopupStoreBusinessAccessSnapshot access = null;
        adapter.QueryAccess(h.Current, value => access = value); Assert.That(access.Success, Is.False);
        var request = new PopupStoreBusinessStartRequest(h.Current.AccountId, "blocked-start", PopupStoreBusinessMode.Rewarded, PopupStoreServerAuthority.PolicyVersion);
        PopupStoreBusinessStartResponse response = null; adapter.Submit(h.Current, request, value => response = value);
        Assert.That(response.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend));
        Assert.That(h.Transport.Calls.Count, Is.EqualTo(sends)); Assert.That(h.Store.Writes, Is.Zero);
    }

    [Test]
    public void PendingAbandonReceiptRecoversWithoutBlockingFreshCleanupSettlement()
    {
        var h = new Harness(); h.Start(); h.Lifecycle.Released = false; h.IssueAndPack();
        h.Clock.Elapsed = 3.2; h.IssueAndPack(); h.Clock.Elapsed = 22;
        Assert.That(h.Submit(h.Command(Operation.Settle)).Business.AwaitingPhaseCleanup, Is.True);
        h.Transport.HoldAfterCommit = true; h.Client.Submit(h.Command(Operation.Abandon), value => h.Last = value);
        h.Client.MarkResponseMissing(); h.Transport.HoldAfterCommit = false; h.Client.Recover(value => h.Last = value);
        Assert.That(h.Last.Outcome, Is.EqualTo(PopupStoreServerClientOutcome.Confirmed));
        Assert.That(h.Last.Response.Business.AwaitingPhaseCleanup, Is.True); Assert.That(h.Journal.Read("gamer-A"), Is.Null);
        h.Lifecycle.Released = true; var settled = h.Submit(h.Command(Operation.Settle));
        Assert.That(settled.Business.EndReason, Is.EqualTo(BusinessEndReason.Abandoned)); Assert.That(settled.Decision.Grade, Is.Zero);
    }
}
#endif
