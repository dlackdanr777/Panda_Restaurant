#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Pure domain tests. The ONLY store here is an isolated serialized in-memory aggregate.
/// No Unity Scene, UserInfo, SDK, network, production account or persistent game data is touched.</summary>
public sealed class PopupStoreServerAuthorityTests
{
    private sealed class Store : IAtomicAccountStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, AccountState> _accounts = new Dictionary<string, AccountState>();
        public bool AtomicAccountTransactionsVerified { get; set; } = true;
        public bool FullWalletMigrationVerified { get; set; } = true;
        public int Writes;
        public bool ThrowAfterCommit;
        public void Seed(string id)
        { lock (_gate) _accounts[id] = new AccountState { AccountId = id, WalletId = "wallet-" + id, WalletMigrationComplete = true, WalletMigrationId = "isolated-test-migration", Diamonds = 100 }; }
        public AccountState Read(string id)
        { lock (_gate) return _accounts.TryGetValue(id, out var account) ? account.Copy() : null; }
        public Response Transact(string id, Func<AccountState, AtomicMutation> operation)
        {
            lock (_gate)
            {
                var mutation = operation(Read(id));
                if (mutation.Commit)
                {
                    _accounts[id] = mutation.State.Copy(); Writes++;
                    if (ThrowAfterCommit) { ThrowAfterCommit = false; throw new TimeoutException("Simulated committed write with missing response."); }
                }
                return mutation.Response;
            }
        }
        public void Alter(string id, Action<AccountState> action) { lock (_gate) action(_accounts[id]); }
    }

    private sealed class Clock : IServerClock
    {
        public bool GameplayProtocolVerified { get; set; } = true;
        public string ProtocolVersion { get; set; } = "isolated-test-active-clock-v1";
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
        public double Elapsed;
        public bool Available = true;
        public bool TryGetElapsedGameplaySeconds(string account, string business, long started, out double elapsed)
        { elapsed = Elapsed; return Available; }
    }

    private sealed class Lifecycle : IServerOrderLifecycle
    {
        public bool ProtocolVerified { get; set; } = true;
        public readonly Dictionary<string, double> Arrivals = new Dictionary<string, double>();
        public readonly HashSet<string> Released = new HashSet<string>();
        public bool TryGetArrivalGameplaySeconds(string account, string business, string order, out double arrival)
        { return Arrivals.TryGetValue(order, out arrival); }
        public bool HasReleasedSlot(string account, string business, string order) { return Released.Contains(order); }
    }

    private sealed class Random : ISecureRandom
    {
        public bool IsCryptographicallySecure { get; set; } = true; // Explicit test-only provider.
        public int Roll;
        public int Draws;
        private int _ids;
        public void Fill(byte[] bytes)
        {
            Array.Clear(bytes, 0, bytes.Length);
            int value;
            if (bytes.Length == 4) { value = Roll; Interlocked.Increment(ref Draws); }
            else value = Interlocked.Increment(ref _ids);
            bytes[0] = (byte)value; bytes[1] = (byte)(value >> 8); bytes[2] = (byte)(value >> 16); bytes[3] = (byte)(value >> 24);
        }
    }

    private sealed class Harness
    {
        public readonly Store Store = new Store();
        public readonly Clock Clock = new Clock();
        public readonly Lifecycle Lifecycle = new Lifecycle();
        public readonly Random Random = new Random();
        public readonly AuthenticatedPrincipal Principal = new AuthenticatedPrincipal("authenticated-gamer-A");
        public PopupStoreServerAuthority Authority;
        public string Business;
        private int _serial;
        public Harness()
        { Store.Seed(Principal.AccountId); Authority = new PopupStoreServerAuthority(Store, Clock, Random, Lifecycle); }
        public Command Command(Operation operation)
        { return new Command { Operation = operation, RequestId = "request-" + Interlocked.Increment(ref _serial), BusinessSessionId = operation == Operation.Start || operation == Operation.Access ? null : Business,
            ExpectedPolicyVersion = operation == Operation.Start ? PopupStoreServerAuthority.PolicyVersion : null }; }
        public Response Send(Command command) { return Authority.Execute(Principal, command); }
        public Response Send(Operation operation) { return Send(Command(operation)); }
        public Response Start()
        { var result = Send(Operation.Start); Assert.That(result.Status, Is.EqualTo(Status.Ok), result.Error); Business = result.Business.BusinessSessionId; return result; }
        public Response Issue(bool arrive = true)
        {
            var result = Send(Operation.IssueOrder); Assert.That(result.Status, Is.EqualTo(Status.Ok), result.Error);
            if (arrive) Lifecycle.Arrivals[result.Order.OrderId] = Clock.Elapsed;
            return result;
        }
        public Command PackCommand(OrderSnapshot order)
        { var command = Command(Operation.Pack); command.OrderId = order.OrderId; command.ReceiptId = "receipt-" + command.RequestId; command.Materials = (string[])order.Materials.Clone(); return command; }
        public Response Pack(OrderSnapshot order, bool release = true)
        {
            var response = Send(PackCommand(order)); Assert.That(response.Status, Is.EqualTo(Status.Ok), response.Error);
            if (release) Lifecycle.Released.Add(order.OrderId); return response;
        }
        public Response ClearCAndFailB()
        {
            Start(); Pack(Issue().Order); Clock.Elapsed = 3.2; Pack(Issue().Order);
            Clock.Elapsed = 22; var next = Send(Operation.Settle); Assert.That(next.Business.Phase, Is.EqualTo(2));
            Clock.Elapsed = 49; return Send(Operation.Settle);
        }
        public Response ClearAll()
        {
            var response = Start(); var policy = PopupStoreServerAuthority.ApprovedPolicy();
            while (!response.Business.IsSettled)
            {
                int phase = response.Business.Phase;
                while (response.Business.TotalScore < policy.Targets[phase - 1])
                {
                    response = Pack(Issue().Order);
                    if (response.Business.TotalScore < policy.Targets[phase - 1]) Clock.Elapsed += policy.EntryIntervals[phase - 1];
                }
                Assert.That(response.Business.HighestClearedGrade, Is.EqualTo(phase - 1), "Targets do not clear early.");
                Clock.Elapsed = response.Business.PhaseDeadlineSeconds; response = Send(Operation.Settle);
            }
            return response;
        }
    }

    [Test]
    public void AccessIsReadOnlyAndKstNoonUsesServerUtc()
    {
        var h = new Harness(); h.Clock.UtcNow = new DateTimeOffset(2026, 11, 5, 2, 59, 59, TimeSpan.Zero);
        Assert.That(h.Send(Operation.Access).Access.GameDay, Is.EqualTo("2026-11-04"));
        Assert.That(h.Send(Operation.Access).Access.NextResetUtcTicks, Is.EqualTo(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero).Ticks));
        h.Clock.UtcNow = h.Clock.UtcNow.AddSeconds(1);
        var response = h.Send(Operation.Access);
        Assert.That(response.Access.GameDay, Is.EqualTo("2026-11-05")); Assert.That(response.Access.DailyLimit, Is.EqualTo(1));
        Assert.That(response.Access.NextResetUtcTicks, Is.EqualTo(new DateTimeOffset(2026, 11, 6, 3, 0, 0, TimeSpan.Zero).Ticks));
        Assert.That(response.Access.ServerUtcTicks, Is.EqualTo(h.Clock.UtcNow.UtcDateTime.Ticks)); Assert.That(h.Store.Writes, Is.Zero);
    }

    [Test]
    public void EveryProductionDependencyAndAccountMigrationMustBeVerified()
    {
        for (int i = 0; i < 6; i++)
        {
            var h = new Harness();
            if (i == 0) h.Store.AtomicAccountTransactionsVerified = false;
            if (i == 1) h.Store.FullWalletMigrationVerified = false;
            if (i == 2) h.Clock.GameplayProtocolVerified = false;
            if (i == 3) h.Random.IsCryptographicallySecure = false;
            if (i == 4) h.Lifecycle.ProtocolVerified = false;
            if (i == 5) h.Store.Alter(h.Principal.AccountId, a => a.WalletMigrationComplete = false);
            Assert.That(h.Send(Operation.Start).Status, Is.EqualTo(Status.Disabled)); Assert.That(h.Store.Writes, Is.Zero);
        }
        var missing = new Harness(); missing.Authority = new PopupStoreServerAuthority(missing.Store, missing.Clock, missing.Random);
        Assert.That(missing.Send(Operation.Start).Status, Is.EqualTo(Status.Disabled));
    }

    [Test]
    public void StartReplayPrecedesNewDayClockAndPolicyChecksAndCannotConsumeTwice()
    {
        var h = new Harness(); var command = h.Command(Operation.Start); var first = h.Send(command);
        h.Clock.UtcNow = new DateTimeOffset(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);
        var replay = h.Send(command);
        Assert.That(replay.Business.BusinessSessionId, Is.EqualTo(first.Business.BusinessSessionId));
        Assert.That(replay.Access.GameDay, Is.EqualTo("2026-11-05")); Assert.That(h.Store.Writes, Is.EqualTo(1));
        command.ExpectedPolicyVersion = "tampered-policy";
        Assert.That(h.Send(command).Status, Is.EqualTo(Status.Conflict)); Assert.That(h.Store.Writes, Is.EqualTo(1));
    }

    [Test]
    public void ConcurrentStartRequestsOnlyCreateOneBusinessAndOneQuotaUse()
    {
        var h = new Harness(); var tasks = new Task<Response>[16];
        for (int i = 0; i < tasks.Length; i++) { var request = h.Command(Operation.Start); tasks[i] = Task.Run(() => h.Send(request)); }
        Task.WaitAll(tasks);
        int approved = 0; foreach (var task in tasks) if (task.Result.Status == Status.Ok) approved++;
        Assert.That(approved, Is.EqualTo(1)); var state = h.Store.Read(h.Principal.AccountId);
        Assert.That(state.Businesses.Count, Is.EqualTo(1)); Assert.That(state.Usage[0].Used, Is.EqualTo(1)); Assert.That(state.Diamonds, Is.EqualTo(100));
    }

    [Test]
    public void LostStartResponseRecoversOriginalRequestWithoutAnotherWrite()
    {
        var h = new Harness(); var command = h.Command(Operation.Start); h.Store.ThrowAfterCommit = true;
        Assert.Throws<TimeoutException>(() => h.Send(command));
        var query = h.Command(Operation.Query); query.LookupRequestId = command.RequestId;
        var recovered = h.Send(query);
        Assert.That(recovered.Status, Is.EqualTo(Status.Ok)); Assert.That(recovered.AppliedRequestId, Is.EqualTo(command.RequestId));
        Assert.That(recovered.AppliedOperation, Is.EqualTo(Operation.Start)); Assert.That(recovered.AppliedCommandHash, Is.EqualTo(PopupStoreServerAuthority.CommandHash(command)));
        Assert.That(h.Send(command).Business.BusinessSessionId, Is.EqualTo(recovered.Business.BusinessSessionId)); Assert.That(h.Store.Writes, Is.EqualTo(1));
    }

    [Test]
    public void LookupMissIsNeverAnAuthoritativeNoWriteFenceAndQueryHasNoWrites()
    {
        var h = new Harness(); var query = h.Command(Operation.Query); query.LookupRequestId = "possibly-in-flight";
        var result = h.Send(query); Assert.That(result.Status, Is.EqualTo(Status.NotFound)); Assert.That(result.AppliedRequestId, Is.Null);
        Assert.That(h.Store.Writes, Is.Zero); h.Start(); int writes = h.Store.Writes;
        var recovered = h.Send(Operation.Query); Assert.That(recovered.Business.BusinessSessionId, Is.EqualTo(h.Business)); Assert.That(h.Store.Writes, Is.EqualTo(writes));
    }

    [Test]
    public void SameRequestIdAcrossOperationsConflictsAndDoesNotWrite()
    {
        var h = new Harness(); var command = h.Command(Operation.Start); h.Send(command); int writes = h.Store.Writes;
        command.Operation = Operation.Abandon; command.ExpectedPolicyVersion = null; command.BusinessSessionId = h.Store.Read(h.Principal.AccountId).Businesses[0].View.BusinessSessionId;
        var response = h.Send(command); Assert.That(response.Status, Is.EqualTo(Status.Conflict)); Assert.That(response.AppliedRequestId, Is.Null); Assert.That(h.Store.Writes, Is.EqualTo(writes));
    }

    [Test]
    public void ForeignPrincipalCannotReadOrPackAnotherAccountsBusiness()
    {
        var h = new Harness(); h.Start(); var order = h.Issue().Order; h.Store.Seed("authenticated-gamer-B");
        var result = h.Authority.Execute(new AuthenticatedPrincipal("authenticated-gamer-B"), h.PackCommand(order));
        Assert.That(result.Status, Is.EqualTo(Status.NotFound)); Assert.That(result.Business, Is.Null);
        Assert.That(h.Store.Read(h.Principal.AccountId).Businesses[0].View.TotalScore, Is.Zero);
    }

    [Test]
    public void OrderIssueIsIdempotentUsesOnlyApprovedMaterialsAndRespectsThreeSlotsAndInterval()
    {
        var h = new Harness(); h.Start(); var issue = h.Command(Operation.IssueOrder); var first = h.Send(issue);
        Assert.That(first.Order.Materials, Is.EqualTo(new[] { "STAFF01", "BOX_PINK", "RIBBON_PINK" }));
        Assert.That(h.Send(issue).Order.OrderId, Is.EqualTo(first.Order.OrderId));
        Assert.That(h.Send(Operation.IssueOrder).Status, Is.EqualTo(Status.Rejected));
        h.Clock.Elapsed = 3.2; Assert.That(h.Issue(false).Order.Slot, Is.EqualTo(1));
        h.Clock.Elapsed = 6.4; Assert.That(h.Issue(false).Order.Slot, Is.EqualTo(2));
        h.Clock.Elapsed = 9.6; Assert.That(h.Send(Operation.IssueOrder).Status, Is.EqualTo(Status.Rejected));
        Assert.That(h.Store.Read(h.Principal.AccountId).Businesses[0].View.Orders.Length, Is.EqualTo(3));
    }

    [Test]
    public void ArrivalProofStartsPatienceAndPackedSlotRequiresTrustedRelease()
    {
        var h = new Harness(); h.Start(); var order = h.Issue(false).Order;
        Assert.That(h.Send(h.PackCommand(order)).Status, Is.EqualTo(Status.Rejected));
        h.Clock.Elapsed = 1; h.Lifecycle.Arrivals[order.OrderId] = 1;
        var packed = h.Pack(order, false); Assert.That(packed.Business.Orders[0].ExpiresAtGameplaySeconds, Is.EqualTo(23));
        h.Clock.Elapsed = 3.2; var second = h.Issue(false).Order; Assert.That(second.Slot, Is.EqualTo(1));
        h.Lifecycle.Released.Add(order.OrderId); h.Clock.Elapsed = 6.4;
        Assert.That(h.Issue(false).Order.Slot, Is.EqualTo(0));
    }

    [Test]
    public void ExactMultisetPackingUsesServerPointsAndRejectsUnknownQuantityAndWrongPriority()
    {
        var h = new Harness(); h.Start(); var first = h.Issue().Order; h.Clock.Elapsed = 3.2; var second = h.Issue().Order;
        Assert.That(h.Send(h.PackCommand(second)).Status, Is.EqualTo(Status.Conflict), "Earliest patience must win for identical orders.");
        var bad = h.PackCommand(first); bad.Materials[0] = "STAFF32";
        Assert.That(h.Send(bad).Status, Is.EqualTo(Status.Conflict));
        var reversed = h.PackCommand(first); Array.Reverse(reversed.Materials);
        var result = h.Send(reversed); Assert.That(result.Status, Is.EqualTo(Status.Ok)); Assert.That(result.Business.TotalScore, Is.EqualTo(100));
        var extra = h.PackCommand(second); extra.Materials = new[] { "STAFF01", "STAFF01", "BOX_PINK", "RIBBON_PINK" };
        Assert.That(h.Send(extra).Status, Is.EqualTo(Status.Conflict));
    }

    [Test]
    public void DuplicateReceiptWithNewRequestReplaysButChangedPayloadConflicts()
    {
        var h = new Harness(); h.Start(); var order = h.Issue().Order; var pack = h.PackCommand(order); var first = h.Send(pack);
        pack.RequestId = "retry-new-request"; Assert.That(h.Send(pack).Business.TotalScore, Is.EqualTo(100));
        pack.RequestId = "tamper-new-request"; pack.Materials[0] = "STAFF02";
        Assert.That(h.Send(pack).Status, Is.EqualTo(Status.Conflict));
        var state = h.Store.Read(h.Principal.AccountId); Assert.That(state.Businesses[0].Receipts.Count, Is.EqualTo(1)); Assert.That(state.Businesses[0].View.SuccessfulOrders, Is.EqualTo(1));
    }

    [Test]
    public void ExactDeadlineRejectsPackingAndTargetsDoNotClearEarly()
    {
        var h = new Harness(); h.Start(); h.Pack(h.Issue().Order); h.Clock.Elapsed = 3.2; var second = h.Issue().Order;
        h.Clock.Elapsed = 22; h.Lifecycle.Released.Add(second.OrderId); var late = h.Send(h.PackCommand(second));
        Assert.That(late.Status, Is.EqualTo(Status.Rejected)); Assert.That(late.Business.TotalScore, Is.EqualTo(100)); Assert.That(late.Decision.Grade, Is.Zero);
        var good = new Harness(); var result = good.ClearCAndFailB();
        Assert.That(result.Business.TotalScore, Is.EqualTo(200)); Assert.That(result.Decision.Grade, Is.EqualTo(1)); Assert.That(result.Business.EndReason, Is.EqualTo(BusinessEndReason.Natural));
    }

    [Test]
    public void EntryCutoffIsExactlyTenSecondsAndOldPhaseOrdersCannotPack()
    {
        var h = new Harness(); h.Start(); h.Clock.Elapsed = 12;
        Assert.That(h.Send(Operation.IssueOrder).Status, Is.EqualTo(Status.Rejected));
        var next = new Harness(); next.Start(); next.Pack(next.Issue().Order); next.Clock.Elapsed = 3.2; next.Pack(next.Issue().Order);
        next.Clock.Elapsed = 6.4; var old = next.Issue(false).Order; next.Clock.Elapsed = 22; next.Lifecycle.Released.Add(old.OrderId);
        Assert.That(next.Send(next.PackCommand(old)).Status, Is.EqualTo(Status.Rejected));
        Assert.That(next.Store.Read(next.Principal.AccountId).Businesses[0].View.Phase, Is.EqualTo(2));
    }

    [Test]
    public void TrustedTimeCannotMoveBackwardsAndMissingTimeDoesNotInventPause()
    {
        var h = new Harness(); h.Start(); h.Clock.Elapsed = 5; h.Send(Operation.Settle); h.Clock.Elapsed = 4;
        Assert.That(h.Send(Operation.Settle).Status, Is.EqualTo(Status.ClockUnavailable));
        h.Clock.Available = false; Assert.That(h.Send(Operation.Settle).Status, Is.EqualTo(Status.ClockUnavailable));
        Assert.That(h.Store.Read(h.Principal.AccountId).Businesses[0].View.ElapsedGameplaySeconds, Is.EqualTo(5));
    }

    [Test]
    public void AllSixStagesUseFrozenApprovedScoresTimesUnlocksAndOneR1Decision()
    {
        var h = new Harness(); var result = h.ClearAll();
        Assert.That(result.Decision.Grade, Is.EqualTo(6)); Assert.That(result.Business.TotalScore, Is.EqualTo(3900));
        Assert.That(result.Business.ElapsedGameplaySeconds, Is.EqualTo(193)); Assert.That(result.Decision.Diamonds, Is.EqualTo(30));
        string decision = result.Decision.DecisionId; int draws = h.Random.Draws;
        Assert.That(h.Send(Operation.Settle).Decision.DecisionId, Is.EqualTo(decision)); Assert.That(h.Random.Draws, Is.EqualTo(draws));
        var policy = h.Store.Read(h.Principal.AccountId).Businesses[0].Policy;
        Assert.That(policy.PlushCounts, Is.EqualTo(new[] { 5, 8, 12, 16, 24, 32 })); Assert.That(policy.Points, Is.EqualTo(new[] { 100, 150, 220 }));
    }

    [TestCase(899, 0, 1)]
    [TestCase(900, 1, 3)]
    [TestCase(996, 1, 3)]
    [TestCase(997, 2, 5)]
    [TestCase(999, 2, 5)]
    public void R1PermilleBoundariesAreServerSelected(int roll, int kind, int amount)
    {
        var h = new Harness(); h.Start(); h.Pack(h.Issue().Order); h.Clock.Elapsed = 3.2; h.Pack(h.Issue().Order);
        h.Clock.Elapsed = 22; h.Send(Operation.Settle); h.Random.Roll = roll; h.Clock.Elapsed = 49;
        var result = h.Send(Operation.Settle); Assert.That(result.Decision.Kind, Is.EqualTo(kind)); Assert.That(result.Decision.Diamonds, Is.EqualTo(amount));
    }

    [Test]
    public void AbandonNeverAwardsAndCannotReplaceAnAlreadySettledDecision()
    {
        var h = new Harness(); h.Start(); h.Pack(h.Issue().Order); h.Clock.Elapsed = 3.2; h.Pack(h.Issue().Order); h.Clock.Elapsed = 22; h.Send(Operation.Settle);
        var result = h.Send(Operation.Abandon); Assert.That(result.Business.HighestClearedGrade, Is.EqualTo(1)); Assert.That(result.Decision.Grade, Is.Zero);
        Assert.That(h.Send(Operation.Claim).Grant, Is.Null); Assert.That(h.Store.Read(h.Principal.AccountId).Diamonds, Is.EqualTo(100));
        var natural = new Harness(); var settled = natural.ClearCAndFailB();
        Assert.That(natural.Send(Operation.Abandon).Decision.DecisionId, Is.EqualTo(settled.Decision.DecisionId));
    }

    [Test]
    public void ConcurrentClaimsAtomicallyCommitExactlyOneGrantBalanceAndVersion()
    {
        var h = new Harness(); h.ClearCAndFailB(); var tasks = new Task<Response>[24];
        for (int i = 0; i < tasks.Length; i++) { var command = h.Command(Operation.Claim); tasks[i] = Task.Run(() => h.Send(command)); }
        Task.WaitAll(tasks); string grant = tasks[0].Result.Grant.GrantId;
        foreach (var task in tasks) { Assert.That(task.Result.Status, Is.EqualTo(Status.Ok)); Assert.That(task.Result.Grant.GrantId, Is.EqualTo(grant)); }
        var state = h.Store.Read(h.Principal.AccountId); Assert.That(state.Diamonds, Is.EqualTo(101)); Assert.That(state.WalletVersion, Is.EqualTo(1));
        Assert.That(state.Businesses[0].View.Grant.BalanceAfter, Is.EqualTo(101));
    }

    [Test]
    public void LostClaimResponseRestoresOriginalGrantAfterAnotherWalletWriteWithoutBalanceReplay()
    {
        var h = new Harness(); h.ClearCAndFailB(); var command = h.Command(Operation.Claim); h.Store.ThrowAfterCommit = true;
        Assert.Throws<TimeoutException>(() => h.Send(command));
        h.Store.Alter(h.Principal.AccountId, a => { a.Diamonds += 50; a.WalletVersion++; });
        var replay = h.Send(command); Assert.That(replay.Grant.BalanceAfter, Is.EqualTo(101)); Assert.That(replay.Grant.WalletVersion, Is.EqualTo(1));
        Assert.That(replay.Wallet, Is.Null);
        var query = h.Command(Operation.Query); query.LookupRequestId = command.RequestId; var latest = h.Send(query);
        Assert.That(latest.Grant.BalanceAfter, Is.EqualTo(101)); Assert.That(latest.Wallet.Balance, Is.EqualTo(151)); Assert.That(latest.Wallet.Version, Is.EqualTo(2));
        Assert.That(h.Store.Read(h.Principal.AccountId).Diamonds, Is.EqualTo(151)); Assert.That(h.Store.Read(h.Principal.AccountId).WalletVersion, Is.EqualTo(2));
    }

    [Test]
    public void MutableRepliesAndPolicyCopiesCannotMutateAuthorityOrReplay()
    {
        var h = new Harness(); var start = h.Start(); start.Business.Phase = 6; start.Access.Used = 0;
        var issue = h.Command(Operation.IssueOrder); var order = h.Send(issue); order.Order.Materials[0] = "STAFF32"; order.Business.TotalScore = int.MaxValue;
        var replay = h.Send(issue); Assert.That(replay.Order.Materials[0], Is.EqualTo("STAFF01")); Assert.That(replay.Business.TotalScore, Is.Zero);
        var policy = PopupStoreServerAuthority.ApprovedPolicy(); policy.Targets[0] = 1;
        Assert.That(h.Store.Read(h.Principal.AccountId).Businesses[0].Policy.Targets[0], Is.EqualTo(200));
    }

    [Test]
    public void FrozenPolicyTamperingAndWalletOverflowFailClosed()
    {
        var h = new Harness(); h.Start(); h.Store.Alter(h.Principal.AccountId, a => a.Businesses[0].Policy.Points[0] = int.MaxValue);
        Assert.That(h.Send(Operation.IssueOrder).Status, Is.EqualTo(Status.Disabled));
        var wallet = new Harness(); wallet.ClearCAndFailB(); wallet.Store.Alter(wallet.Principal.AccountId, a => a.Diamonds = int.MaxValue);
        Assert.That(wallet.Send(Operation.Claim).Status, Is.EqualTo(Status.Rejected)); Assert.That(wallet.Store.Read(wallet.Principal.AccountId).WalletVersion, Is.Zero);
        Assert.That(wallet.Store.Read(wallet.Principal.AccountId).Businesses[0].View.Grant, Is.Null);
    }

    [Test]
    public void NewDayStillRequiresRecoveringActiveOrUnclaimedBusiness()
    {
        var h = new Harness(); h.ClearCAndFailB(); h.Clock.UtcNow = h.Clock.UtcNow.AddDays(1);
        Assert.That(h.Send(Operation.Start).Status, Is.EqualTo(Status.Conflict)); h.Send(Operation.Claim); h.Clock.Elapsed = 0;
        Assert.That(h.Send(Operation.Start).Status, Is.EqualTo(Status.Ok));
        Assert.That(h.Store.Read(h.Principal.AccountId).Usage.Count, Is.EqualTo(2));
    }

    [Test]
    public void EqualPatienceUsesLeftmostSlotAndExpiryPenaltyNeverChargesOtherPatience()
    {
        var h = new Harness(); h.Start(); var first = h.Issue(false).Order; h.Clock.Elapsed = 3.2; var second = h.Issue(false).Order;
        h.Lifecycle.Arrivals[first.OrderId] = 3.2; h.Lifecycle.Arrivals[second.OrderId] = 3.2;
        Assert.That(h.Send(h.PackCommand(second)).Status, Is.EqualTo(Status.Conflict)); h.Pack(first); h.Pack(second);
        h.Clock.Elapsed = 22; h.Send(Operation.Settle); var a = h.Issue().Order;
        h.Clock.Elapsed = 25; var b = h.Issue().Order;
        h.Clock.Elapsed = 42; var expiry = h.Send(Operation.Settle);
        Assert.That(expiry.Business.PhaseDeadlineSeconds, Is.EqualTo(46));
        Assert.That(Array.Find(expiry.Business.Orders, x => x.OrderId == a.OrderId).Expired, Is.True);
        Assert.That(Array.Find(expiry.Business.Orders, x => x.OrderId == b.OrderId).ExpiresAtGameplaySeconds, Is.EqualTo(45));
        h.Clock.Elapsed = 45; h.Lifecycle.Released.Add(a.OrderId); h.Lifecycle.Released.Add(b.OrderId); var end = h.Send(Operation.Settle);
        Assert.That(end.Business.IsSettled, Is.True); Assert.That(end.Decision.Grade, Is.EqualTo(1));
        Assert.That(end.Business.ElapsedGameplaySeconds, Is.EqualTo(45));
    }

    [Test]
    public void AuthoritativePolicyMatchesCurrentAuthoredBalanceAndR1Assets()
    {
        var policy = PopupStoreServerAuthority.ApprovedPolicy();
        string balance = File.ReadAllText("Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStorePhasePreviewBalance.asset");
        string reward = File.ReadAllText("Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset");
        double[] Values(string text, string key) => Regex.Matches(text, @"(?m)^\s*(?:- )?" + Regex.Escape(key) + @":\s*([0-9.]+)\s*$")
            .Cast<Match>().Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        Assert.That(Values(balance, "seconds"), Is.EqualTo(policy.Seconds));
        Assert.That(Values(balance, "targetScore"), Is.EqualTo(policy.Targets));
        Assert.That(Values(balance, "plushCount"), Is.EqualTo(policy.PlushCounts));
        Assert.That(Values(balance, "onePlushPatienceSeconds"), Is.EqualTo(policy.Patience));
        Assert.That(Values(balance, "entryIntervalSeconds"), Is.EqualTo(policy.EntryIntervals));
        Assert.That(Values(balance, "threeMaterials"), Is.EqualTo(new[] { policy.Points[0] }));
        Assert.That(Values(balance, "fourMaterials"), Is.EqualTo(new[] { policy.Points[1] }));
        Assert.That(Values(balance, "fiveMaterials"), Is.EqualTo(new[] { policy.Points[2] }));
        for (int kind = 0; kind < 3; kind++)
        {
            string prefix = kind == 0 ? "_basic" : kind == 1 ? "_bonus" : "_jackpot";
            Assert.That(Values(reward, prefix + "Diamonds"), Is.EqualTo(Enumerable.Range(0, 6).Select(grade => policy.RewardAmounts[grade * 3 + kind]).ToArray()));
            Assert.That(Values(reward, prefix + "Weight"), Is.EqualTo(Enumerable.Repeat(policy.RewardWeights[kind], 6).ToArray()));
        }
    }

    [Test]
    public void PhaseBoundaryWaitsForEveryOldOrderReleaseAndNoElapsedTimeCarriesAcrossCleanup()
    {
        var h = new Harness(); h.Start(); var first = h.Issue().Order; h.Pack(first, false);
        h.Clock.Elapsed = 3.2; var second = h.Issue().Order; h.Pack(second, false);
        h.Clock.Elapsed = 22; var pending = h.Send(Operation.Settle);
        Assert.That(pending.Business.AwaitingPhaseCleanup, Is.True); Assert.That(pending.Business.Phase, Is.EqualTo(1));
        Assert.That(pending.Business.HighestClearedGrade, Is.Zero); Assert.That(pending.Business.IsSettled, Is.False);
        h.Lifecycle.Released.Add(first.OrderId);
        Assert.That(h.Send(Operation.IssueOrder).Status, Is.EqualTo(Status.Rejected), "An empty third slot cannot bypass old-phase cleanup.");
        h.Clock.Elapsed = 23; Assert.That(h.Send(Operation.Settle).Status, Is.EqualTo(Status.ClockUnavailable));
        h.Clock.Elapsed = 22; h.Lifecycle.Released.Add(second.OrderId); var next = h.Send(Operation.Settle);
        Assert.That(next.Business.AwaitingPhaseCleanup, Is.False); Assert.That(next.Business.Phase, Is.EqualTo(2));
        Assert.That(next.Business.ElapsedGameplaySeconds, Is.EqualTo(22)); Assert.That(next.Business.PhaseDeadlineSeconds, Is.EqualTo(49));
    }

    [Test]
    public void AbandonDuringSuccessfulPhaseCleanupForfeitsButFailedDeadlineStaysNatural()
    {
        var h = new Harness(); h.Start(); var first = h.Issue().Order; h.Pack(first, false);
        h.Clock.Elapsed = 3.2; var second = h.Issue().Order; h.Pack(second, false);
        h.Clock.Elapsed = 22; Assert.That(h.Send(Operation.Settle).Business.AwaitingPhaseCleanup, Is.True);
        var abandon = h.Send(Operation.Abandon); Assert.That(abandon.Status, Is.EqualTo(Status.Ok));
        Assert.That(abandon.Business.IsSettled, Is.False);
        h.Lifecycle.Released.Add(first.OrderId); h.Lifecycle.Released.Add(second.OrderId);
        var settled = h.Send(Operation.Settle);
        Assert.That(settled.Business.EndReason, Is.EqualTo(BusinessEndReason.Abandoned));
        Assert.That(settled.Decision.Grade, Is.Zero); Assert.That(settled.Business.Phase, Is.EqualTo(1));

        var failed = new Harness(); failed.Start(); var unfilled = failed.Issue().Order;
        failed.Clock.Elapsed = 22; Assert.That(failed.Send(Operation.Settle).Business.AwaitingPhaseCleanup, Is.True);
        Assert.That(failed.Send(Operation.Abandon).Status, Is.EqualTo(Status.Rejected));
        failed.Lifecycle.Released.Add(unfilled.OrderId);
        Assert.That(failed.Send(Operation.Settle).Business.EndReason, Is.EqualTo(BusinessEndReason.Natural));
    }
}
#endif
