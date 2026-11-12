#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using PandaRestaurant.PopupStoreServer;

/// <summary>Actual authority + production partition codec/store, isolated atomic Database port.
/// These prove local contracts, not BACKND SDK execution, deployed permissions or actual CAS.</summary>
public sealed class PopupStoreServerPartitionStoreTests
{
    private sealed class Port : IPartitionDatabasePort
    {
        internal readonly object Gate = new object();
        internal Dictionary<string, PartitionRecord> Rows = new Dictionary<string, PartitionRecord>();
        internal int Commits, LastWrites, FailAt = -1;
        internal bool LoseAcknowledgement, NullResult, WrongCount, WrongRows, ForceConflict, Truncate;
        internal Action BeforeCommit;
        internal void Seed(AccountState account)
        { foreach (var row in PopupStorePartitionCodec.Encode(account, 1)) Rows.Add(row.Key, Copy(row)); }
        public PartitionRecord ReadRoot(string owner)
        { lock (Gate) return Rows.TryGetValue(PopupStorePartitionCodec.RootKey(owner), out var row) ? Copy(row) : null; }
        public IReadOnlyList<PartitionRecord> ReadRecords(string owner, int maximum)
        { lock (Gate) return Rows.Values.Where(x => x.OwnerId == owner).OrderBy(x => x.Key, StringComparer.Ordinal).Take(Truncate ? 1 : maximum + 1).Select(Copy).ToArray(); }
        public PartitionCommitResult Commit(string owner, IReadOnlyList<PartitionWrite> writes)
        {
            lock (Gate)
            {
                var action = BeforeCommit; BeforeCommit = null; action?.Invoke();
                LastWrites = writes.Count;
                var next = Rows.ToDictionary(x => x.Key, x => Copy(x.Value));
                var seen = new HashSet<string>();
                for (int i = 0; i < writes.Count; i++)
                {
                    var write = writes[i]; var row = write.Value;
                    if (i == FailAt || ForceConflict || row.OwnerId != owner || !seen.Add(row.Key)) return Failed();
                    if (write.Insert)
                    { if (next.ContainsKey(row.Key)) return Failed(); }
                    else if (!next.TryGetValue(row.Key, out var prior) || prior.Revision != write.ExpectedRevision) return Failed();
                    next[row.Key] = Copy(row);
                }
                Rows = next; Commits++;
                if (LoseAcknowledgement) { LoseAcknowledgement = false; throw new TimeoutException(); }
                if (NullResult) return null;
                return new PartitionCommitResult { Success = true, OperationCount = writes.Count + (WrongCount ? 1 : 0), AffectedRows = writes.Count - (WrongRows ? 1 : 0) };
            }
        }
        private static PartitionCommitResult Failed() => new PartitionCommitResult { Success = false };
    }
    private sealed class Clock : IServerClock
    {
        public bool GameplayProtocolVerified => true;
        public string ProtocolVersion => "partition-isolated-clock";
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);
        public double Elapsed;
        public bool TryGetElapsedGameplaySeconds(string a, string b, long t, out double value) { value = Elapsed; return true; }
    }
    private sealed class Lifecycle : IServerOrderLifecycle
    {
        public bool ProtocolVerified => true;
        public readonly Dictionary<string, double> Arrivals = new Dictionary<string, double>();
        public readonly HashSet<string> Released = new HashSet<string>();
        public bool TryGetArrivalGameplaySeconds(string a, string b, string o, out double t) => Arrivals.TryGetValue(o, out t);
        public bool HasReleasedSlot(string a, string b, string o) => Released.Contains(o);
    }
    private sealed class Random : ISecureRandom
    {
        public bool IsCryptographicallySecure => true;
        public int Draws, Ids, Roll;
        public void Fill(byte[] bytes)
        {
            Array.Clear(bytes, 0, bytes.Length);
            int v = bytes.Length == 4 ? Roll : Interlocked.Increment(ref Ids);
            if (bytes.Length == 4) Interlocked.Increment(ref Draws);
            for (int i = 0; i < 4; i++) bytes[i] = (byte)(v >> (i * 8));
        }
    }
    private sealed class Harness
    {
        internal readonly Port Port = new Port();
        internal readonly Clock Clock = new Clock();
        internal readonly Lifecycle Life = new Lifecycle();
        internal readonly Random Random = new Random();
        internal readonly AuthenticatedPrincipal Principal = new AuthenticatedPrincipal("isolated-A");
        internal readonly PopupStorePartitionAccountStore Store;
        internal readonly PopupStoreServerAuthority Authority;
        internal string Business;
        private int serial;
        internal Harness()
        {
            Port.Seed(Seed()); Store = new PopupStorePartitionAccountStore(Port, Principal, Verified());
            Authority = new PopupStoreServerAuthority(Store, Clock, Random, Life);
        }
        internal Command Command(Operation op) => new Command { Operation = op, RequestId = "request-" + Interlocked.Increment(ref serial),
            BusinessSessionId = op == Operation.Start || op == Operation.Access ? null : Business,
            ExpectedPolicyVersion = op == Operation.Start ? PopupStoreServerAuthority.PolicyVersion : null };
        internal Response Send(Command cmd) => Authority.Execute(Principal, cmd);
        internal Response Send(Operation op) => Send(Command(op));
        internal Response Start() { var r = Send(Operation.Start); Assert.That(r.Status, Is.EqualTo(Status.Ok), r.Error); Business = r.Business.BusinessSessionId; return r; }
        internal Response Issue()
        { var r = Send(Operation.IssueOrder); Assert.That(r.Status, Is.EqualTo(Status.Ok), r.Error); Life.Arrivals[r.Order.OrderId] = Clock.Elapsed; return r; }
        internal Response Pack(OrderSnapshot order)
        {
            var cmd = Command(Operation.Pack); cmd.OrderId = order.OrderId; cmd.ReceiptId = "receipt-" + cmd.RequestId; cmd.Materials = order.Materials.Reverse().ToArray();
            var r = Send(cmd); Assert.That(r.Status, Is.EqualTo(Status.Ok), r.Error); Life.Released.Add(order.OrderId); return r;
        }
        internal Response SettleC()
        { Start(); Pack(Issue().Order); Clock.Elapsed = 3.2; Pack(Issue().Order); Clock.Elapsed = 22; Send(Operation.Settle); Clock.Elapsed = 49; return Send(Operation.Settle); }
    }
    private static PartitionRecord Copy(PartitionRecord row) => PopupStorePartitionCodec.Clone(row);
    private static AccountState Seed() => new AccountState { AccountId = "isolated-A", WalletId = "wallet-A", WalletMigrationId = "migration-A", WalletMigrationComplete = true, Diamonds = 100 };
    private static PartitionStoreVerification Verified() => new PartitionStoreVerification { DevelopmentEnvironmentVerified = true,
        SchemaAndPermissionsVerified = true, AtomicPredicatesVerified = true, PayloadRoundTripVerified = true, AllWalletWritersMigrated = true, MigrationId = "migration-A" };
    private static AtomicMutation Append(AccountState account, string id)
    {
        var response = new Response { RequestId = id, Status = Status.Ok };
        account.Requests.Add(new RequestState { RequestId = id, Fingerprint = "fingerprint-" + id, Response = response });
        return new AtomicMutation(account, response, true);
    }

    [TestCase("a:b", "c", "a", "b:c")]
    [TestCase("a.b", "c", "a", "b.c")]
    [TestCase("a", "b/c", "a/b", "c")]
    [TestCase("a", "\uD55C\uAE00", "a", "\uD55C\uAE00 ")]
    public void CompositePrimaryKeyEncodingHasNoDelimiterAmbiguity(string a, string b, string c, string d)
    { Assert.That(PopupStorePartitionCodec.RecordKey(a, "request", "", b), Is.Not.EqualTo(PopupStorePartitionCodec.RecordKey(c, "request", "", d))); }

    [Test]
    public void AccountDayRequestBusinessAndGrantKindsCannotCollide()
    {
        var keys = new[] { "account", "day", "business", "request", "receipt", "grant" }.Select(x => PopupStorePartitionCodec.RecordKey("a", x, "", "same"));
        Assert.That(keys.Distinct().Count(), Is.EqualTo(6));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void DeploymentAndMigrationEvidenceRemainRequired(int gate)
    {
        var p = new Port(); p.Seed(Seed()); var v = Verified();
        if (gate == 0) v.DevelopmentEnvironmentVerified = false;
        if (gate == 1) v.SchemaAndPermissionsVerified = false;
        if (gate == 2) v.AtomicPredicatesVerified = false;
        if (gate == 3) v.PayloadRoundTripVerified = false;
        if (gate == 4) v.AllWalletWritersMigrated = false;
        if (gate == 5) v.MigrationId = null;
        var s = new PopupStorePartitionAccountStore(p, new AuthenticatedPrincipal("isolated-A"), v);
        Assert.Throws<InvalidOperationException>(() => s.Read("isolated-A")); Assert.That(p.Commits, Is.Zero);
    }

    [Test]
    public void DefaultVerificationIsUnavailableAndDoesNotReadOrWrite()
    { var p = new Port(); var s = new PopupStorePartitionAccountStore(p, new AuthenticatedPrincipal("isolated-A")); Assert.That(s.AtomicAccountTransactionsVerified, Is.False); Assert.Throws<InvalidOperationException>(() => s.Read("isolated-A")); }

    [Test]
    public void AuthenticatedPrincipalCannotAccessAnotherAccount()
    { var h = new Harness(); Assert.Throws<InvalidOperationException>(() => h.Store.Read("isolated-B")); Assert.Throws<InvalidOperationException>(() => h.Store.Transact("isolated-B", a => Append(a, "x"))); }

    [Test]
    public void ClaimCannotCreateMissingAccountOrCompleteMigration()
    {
        var p = new Port(); var s = new PopupStorePartitionAccountStore(p, new AuthenticatedPrincipal("isolated-A"), Verified());
        Assert.That(s.Read("isolated-A"), Is.Null); Assert.Throws<InvalidOperationException>(() => s.Transact("isolated-A", _ => Append(Seed(), "x"))); Assert.That(p.Commits, Is.Zero);
    }

    [Test]
    public void NoCommitMutationDoesNotWrite()
    { var h = new Harness(); h.Store.Transact("isolated-A", a => new AtomicMutation(a, new Response(), false)); Assert.That(h.Port.Commits, Is.Zero); }

    [Test]
    public void SeparateRequestsExceedOldAccountDocumentLimitWithoutPruning()
    {
        var h = new Harness();
        for (int i = 0; i < 50; i++) h.Store.Transact("isolated-A", a => Append(a, "history-" + i));
        var state = h.Store.Read("isolated-A");
        Assert.That(JsonConvert.SerializeObject(state).Length, Is.GreaterThan(11000));
        Assert.That(state.Requests.Count, Is.EqualTo(50)); Assert.That(h.Port.LastWrites, Is.EqualTo(2));
        Assert.That(h.Port.Rows.Values.All(r => r.Payload.Data.Length <= PopupStorePartitionCodec.MaximumPayloadCharacters), Is.True);
    }

    [Test]
    public void ExactReceiptAndHistoricalResponseRoundTripPreservesHistoricalOrderState()
    {
        var h = new Harness(); h.Start(); var issued = h.Issue(); var packed = h.Pack(issued.Order);
        var stored = h.Store.Read("isolated-A");
        Assert.That(stored.Businesses[0].Receipts[0].Response.Business.TotalScore, Is.EqualTo(100));
        Assert.That(stored.Businesses[0].Receipts[0].OrderId, Is.EqualTo(issued.Order.OrderId));
        var rows = PopupStorePartitionCodec.Encode(stored, 12);
        var decoded = PopupStorePartitionCodec.Decode("isolated-A", rows.Single(x => x.Kind == "account"), rows);
        Assert.That(JsonConvert.SerializeObject(decoded), Is.EqualTo(JsonConvert.SerializeObject(stored)));
        Assert.That(decoded.Businesses[0].Receipts.Count, Is.EqualTo(1));
        Assert.That(decoded.Requests[1].Response.Business.Orders[0].Packed, Is.False);
        Assert.That(decoded.Businesses[0].View.Orders[0].Packed, Is.True);
    }

    [TestCase("remove")] [TestCase("duplicate")] [TestCase("owner")] [TestCase("key")] [TestCase("ordinal")]
    [TestCase("checksum")] [TestCase("size")] [TestCase("codec")] [TestCase("decoded-size")] [TestCase("payload")]
    public void CorruptedOrIncompleteSnapshotIsRejectedBeforeMutation(string corruption)
    {
        var h = new Harness(); h.Start(); var rows = h.Port.Rows.Values.Select(Copy).ToList();
        var root = rows.Single(x => x.Kind == "account"); var row = rows.First(x => x.Kind != "account");
        if (corruption == "remove") rows.Remove(row);
        if (corruption == "duplicate") rows.Add(Copy(row));
        if (corruption == "owner") row.OwnerId = "isolated-B";
        if (corruption == "key") row.Key += "x";
        if (corruption == "ordinal") row.Ordinal++;
        if (corruption == "checksum") row.Payload.Sha256 = new string('0', 64);
        if (corruption == "size") row.Payload.Data = new string('A', 10001);
        if (corruption == "codec") row.Payload.Codec = "unknown";
        if (corruption == "decoded-size") row.Payload.DecodedBytes = 1;
        if (corruption == "payload") row.Payload.Data = "!invalid!";
        Assert.That(() => PopupStorePartitionCodec.Decode("isolated-A", root, rows), Throws.Exception);
    }

    [Test]
    public void TruncatedSdkScanCannotResetDailyUsageOrDropRequestHistory()
    { var h = new Harness(); h.Start(); h.Port.Truncate = true; Assert.Throws<InvalidOperationException>(() => h.Store.Read("isolated-A")); Assert.That(h.Port.Commits, Is.EqualTo(1)); }

    [Test]
    public void TotalDecodedSnapshotCapacityIsCheckedBeforeDecompression()
    {
        var account = Seed();
        for (int i = 0; i < 17; i++) Append(account, "entry" + i);
        var rows = PopupStorePartitionCodec.Encode(account, 1).Select(Copy).ToArray();
        foreach (var row in rows) row.Payload.DecodedBytes = PopupStorePartitionCodec.MaximumDecodedBytes;
        var error = Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Decode(account.AccountId, rows.Single(x => x.Kind == "account"), rows));
        Assert.That(error.Message, Is.EqualTo("partition_snapshot_capacity"));
    }

    [Test]
    public void CrossingDecodedSnapshotCapacityIsRejectedBeforeDurableCommit()
    {
        var account = Seed();
        string repeated = new string('x', 600000);
        for (int i = 0; i < 27; i++) Append(account, "large-history-" + i).Response.Error = repeated;
        var port = new Port(); port.Seed(account);
        var store = new PopupStorePartitionAccountStore(port, new AuthenticatedPrincipal(account.AccountId), Verified());
        Assert.That(store.Read(account.AccountId).Requests.Count, Is.EqualTo(27));
        var beforeRevision = port.ReadRoot(account.AccountId).Revision;
        var error = Assert.Throws<InvalidOperationException>(() => store.Transact(account.AccountId, state =>
        {
            var mutation = Append(state, "cross-capacity"); mutation.Response.Error = repeated; return mutation;
        }));
        Assert.That(error.Message, Is.EqualTo("partition_snapshot_capacity"));
        Assert.That(port.Commits, Is.Zero); Assert.That(port.ReadRoot(account.AccountId).Revision, Is.EqualTo(beforeRevision));
        Assert.That(store.Read(account.AccountId).Requests.Count, Is.EqualTo(27));
    }

    [TestCase("request")] [TestCase("receipt")] [TestCase("grant")]
    public void ExistingIdempotencyRecordsAreImmutable(string kind)
    {
        var h = new Harness(); h.SettleC(); h.Send(Operation.Claim); var old = h.Port.ReadRecords("isolated-A", 10000);
        var state = h.Store.Read("isolated-A"); Append(state, "next");
        if (kind == "request") state.Requests[0].Response.Error = "rewrite";
        if (kind == "receipt") state.Businesses[0].Receipts[0].Fingerprint = "rewrite";
        if (kind == "grant") state.Businesses[0].View.Grant.GrantId = "rewrite";
        Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Plan("isolated-A", old, state));
    }

    [Test]
    public void HistoryRemovalAndReorderingAreRejected()
    {
        var h = new Harness(); h.Start(); h.Issue(); var old = h.Port.ReadRecords("isolated-A", 10000); var state = h.Store.Read("isolated-A");
        state.Requests.RemoveAt(0); Append(state, "replace"); Append(state, "next");
        Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Plan("isolated-A", old, state));
        state = h.Store.Read("isolated-A"); state.Requests.Reverse(); Append(state, "next");
        Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Plan("isolated-A", old, state));
    }

    [TestCase("balance")] [TestCase("version")] [TestCase("identity")] [TestCase("migration")]
    public void WalletCannotChangeWithoutMatchingAtomicGrant(string field)
    {
        var h = new Harness(); var state = h.Store.Read("isolated-A"); Append(state, "x");
        if (field == "balance") state.Diamonds++;
        if (field == "version") state.WalletVersion++;
        if (field == "identity") state.WalletId += "x";
        if (field == "migration") state.WalletMigrationId += "x";
        Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Plan("isolated-A", h.Port.ReadRecords("isolated-A", 10000), state));
    }

    [Test]
    public void TenWriteLimitIsCheckedBeforeSubmission()
    {
        var h = new Harness(); var state = h.Store.Read("isolated-A"); Append(state, "many");
        for (int i = 0; i < 9; i++) state.Usage.Add(new DailyUsageState { GameDay = "day" + i, Used = 1 });
        Assert.Throws<InvalidOperationException>(() => PopupStorePartitionCodec.Plan("isolated-A", h.Port.ReadRecords("isolated-A", 10000), state));
        Assert.That(h.Port.Commits, Is.Zero);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void EveryPartialStartWriteFailureRollsBackEntireTransaction(int failedWrite)
    {
        var h = new Harness(); h.Port.FailAt = failedWrite; var before = JsonConvert.SerializeObject(h.Store.Read("isolated-A"));
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(Operation.Start));
        Assert.That(JsonConvert.SerializeObject(h.Store.Read("isolated-A")), Is.EqualTo(before)); Assert.That(h.Port.Commits, Is.Zero);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void EveryPartialClaimWriteFailureLeavesWalletAndGrantUnchanged(int failedWrite)
    {
        var h = new Harness(); h.SettleC(); h.Port.FailAt = failedWrite; var before = JsonConvert.SerializeObject(h.Store.Read("isolated-A"));
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(Operation.Claim));
        Assert.That(JsonConvert.SerializeObject(h.Store.Read("isolated-A")), Is.EqualTo(before));
    }

    [Test]
    public void DuplicateInsertRollsBackAccountRevisionAndWallet()
    {
        var h = new Harness(); var duplicate = h.Command(Operation.Start);
        h.Port.BeforeCommit = () =>
        {
            var rows = h.Port.Rows.Values.Select(Copy).ToArray(); var state = h.Store.Read("isolated-A"); Append(state, duplicate.RequestId);
            var request = PopupStorePartitionCodec.Plan("isolated-A", rows, state).Single(x => x.Value.Kind == "request").Value;
            h.Port.Rows.Add(request.Key, Copy(request));
        };
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(duplicate));
        Assert.That(h.Port.ReadRoot("isolated-A").Revision, Is.EqualTo(1)); Assert.That(h.Port.Commits, Is.Zero);
    }

    [Test]
    public void StaleAccountCasCannotConsumeSecondTicket()
    {
        var h = new Harness(); var outer = h.Command(Operation.Start); var winner = h.Command(Operation.Start);
        h.Port.BeforeCommit = () => Assert.That(h.Send(winner).Status, Is.EqualTo(Status.Ok));
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(outer));
        var state = h.Store.Read("isolated-A"); Assert.That(state.Usage.Single().Used, Is.EqualTo(1)); Assert.That(state.Businesses.Count, Is.EqualTo(1)); Assert.That(h.Port.Commits, Is.EqualTo(1));
    }

    [TestCase("null")] [TestCase("operations")] [TestCase("rows")] [TestCase("conflict")]
    public void UnexpectedCommitReplyIsUnknownAndNeverAutomaticallyResubmitted(string reply)
    {
        var h = new Harness(); h.Port.NullResult = reply == "null"; h.Port.WrongCount = reply == "operations"; h.Port.WrongRows = reply == "rows"; h.Port.ForceConflict = reply == "conflict";
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(Operation.Start)); Assert.That(h.Port.Commits, Is.EqualTo(reply == "conflict" ? 0 : 1));
    }

    [Test]
    public void LostStartResponseRecoversOriginalRequestAndSession()
    {
        var h = new Harness(); var command = h.Command(Operation.Start); h.Port.LoseAcknowledgement = true;
        Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(command));
        var expected = h.Store.Read("isolated-A").Requests.Single().Response;
        var replay = h.Send(command); Assert.That(JsonConvert.SerializeObject(replay), Is.EqualTo(JsonConvert.SerializeObject(expected)));
        Assert.That(h.Port.Commits, Is.EqualTo(1));
    }

    [Test]
    public void LostClaimResponseAndNewRequestCannotPayTwiceOrReroll()
    {
        var h = new Harness(); var settled = h.SettleC(); var draws = h.Random.Draws; var command = h.Command(Operation.Claim);
        h.Port.LoseAcknowledgement = true; Assert.Throws<PartitionOutcomeUnknownException>(() => h.Send(command));
        var replay = h.Send(command); var again = h.Send(Operation.Claim); var account = h.Store.Read("isolated-A");
        Assert.That(replay.Grant.GrantId, Is.EqualTo(again.Grant.GrantId)); Assert.That(account.Diamonds, Is.EqualTo(100 + settled.Decision.Diamonds));
        Assert.That(account.WalletVersion, Is.EqualTo(1)); Assert.That(h.Port.Rows.Values.Count(x => x.Kind == "grant"), Is.EqualTo(1)); Assert.That(h.Random.Draws, Is.EqualTo(draws));
    }

    [Test]
    public void ActualAuthorityStartPackSettleAndClaimUseAtMostSixWritesEach()
    {
        var h = new Harness(); h.Start(); Assert.That(h.Port.LastWrites, Is.EqualTo(4));
        var order = h.Issue().Order; Assert.That(h.Port.LastWrites, Is.EqualTo(3));
        h.Pack(order); Assert.That(h.Port.LastWrites, Is.EqualTo(4));
        h.Clock.Elapsed = 3.2; h.Pack(h.Issue().Order); h.Clock.Elapsed = 22; h.Send(Operation.Settle);
        Assert.That(h.Port.LastWrites, Is.LessThanOrEqualTo(6)); h.Clock.Elapsed = 49; h.Send(Operation.Settle);
        h.Send(Operation.Claim); Assert.That(h.Port.LastWrites, Is.EqualTo(4));
        Assert.That(h.Store.Read("isolated-A").Diamonds, Is.EqualTo(101));
    }

    [Test]
    public void KstNoonReadDoesNotRewriteYesterdayOrConsumeAnotherTicket()
    {
        var h = new Harness(); h.Clock.UtcNow = new DateTimeOffset(2026, 11, 5, 2, 59, 59, TimeSpan.Zero); h.Start();
        h.Clock.UtcNow = h.Clock.UtcNow.AddSeconds(1); var access = h.Send(Operation.Access);
        Assert.That(access.Access.GameDay, Is.EqualTo("2026-11-05")); Assert.That(access.Access.Remaining, Is.EqualTo(1));
        Assert.That(h.Store.Read("isolated-A").Usage.Single().GameDay, Is.EqualTo("2026-11-04")); Assert.That(h.Port.Commits, Is.EqualTo(1));
    }

    [Test]
    public void ReturnedSnapshotMutationCannotAlterDurableHistory()
    {
        var h = new Harness(); h.Start(); var state = h.Store.Read("isolated-A"); state.Diamonds = 999; state.Requests.Clear();
        Assert.That(h.Store.Read("isolated-A").Diamonds, Is.EqualTo(100)); Assert.That(h.Store.Read("isolated-A").Requests.Count, Is.EqualTo(1));
    }

    [Test]
    public void ActualSixPhaseGameAndOldRequestReplayExceedOldSingleDocumentLimit()
    {
        var h = new Harness(); var firstCommand = h.Command(Operation.Start); var first = h.Send(firstCommand); h.Business = first.Business.BusinessSessionId;
        var response = first; var policy = PopupStoreServerAuthority.ApprovedPolicy(); bool duplicateMaterials = false;
        while (!response.Business.IsSettled)
        {
            int phase = response.Business.Phase;
            while (response.Business.TotalScore < policy.Targets[phase - 1])
            {
                var order = h.Issue().Order;
                duplicateMaterials |= order.Materials.Distinct().Count() < order.Materials.Length;
                response = h.Pack(order);
                Assert.That(h.Port.LastWrites, Is.LessThanOrEqualTo(6));
                if (response.Business.TotalScore < policy.Targets[phase - 1]) h.Clock.Elapsed += policy.EntryIntervals[phase - 1];
            }
            h.Clock.Elapsed = response.Business.PhaseDeadlineSeconds; response = h.Send(Operation.Settle);
        }
        Assert.That(response.Decision.Grade, Is.EqualTo(6)); Assert.That(duplicateMaterials, Is.True);
        var claim = h.Send(Operation.Claim); var account = h.Store.Read("isolated-A");
        Assert.That(account.Diamonds, Is.EqualTo(130)); Assert.That(account.WalletVersion, Is.EqualTo(1));
        Assert.That(JsonConvert.SerializeObject(account).Length, Is.GreaterThan(11000));
        Assert.That(JsonConvert.SerializeObject(h.Send(firstCommand)), Is.EqualTo(JsonConvert.SerializeObject(first)));
        Assert.That(h.Store.Read("isolated-A").Businesses[0].View.TotalScore, Is.GreaterThanOrEqualTo(3900));
        Assert.That(account.Businesses[0].Receipts.Count, Is.EqualTo(claim.Business.SuccessfulOrders));
        Assert.That(h.Port.Rows.Values.All(x => x.Payload.Data.Length <= PopupStorePartitionCodec.MaximumPayloadCharacters), Is.True);
    }
}
#endif
