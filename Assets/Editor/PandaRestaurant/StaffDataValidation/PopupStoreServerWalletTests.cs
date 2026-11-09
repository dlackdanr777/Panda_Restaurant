#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Muks.BackEnd;
using NUnit.Framework;

/// <summary>Pure memory contract tests. No SDK, Unity singleton, scene, account, or legacy wallet mutation.</summary>
public sealed class PopupStoreServerWalletTests
{
    [Test]
    public void ProductionDefault_RejectsClaimWithoutCreatingLease()
    {
        var guard = new PopupStoreServerWalletGuard();
        Assert.That(guard.TryBeginClaim("request", out var claim, out string error), Is.False);
        Assert.That(claim, Is.Null);
        Assert.That(guard.ActiveClaim, Is.Null);
        Assert.That(error, Is.EqualTo(PopupStoreServerWalletGuard.UnavailableReason));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(false, false)]
    public void BothMigrationAndEveryLegacyWriterFence_AreRequired(bool migrated, bool fenced)
    {
        var boundary = new MemoryBoundary { Capabilities = new PopupStoreWalletCapabilities(migrated, fenced) };
        var guard = new PopupStoreServerWalletGuard(boundary);
        Assert.That(guard.TryBeginClaim("request", out _, out _), Is.False);
        Assert.That(boundary.Acquisitions, Is.Zero);
        Assert.That(boundary.ApplyCount, Is.Zero);
    }

    [Test]
    public void ClaimThenGachaSpend_AppliesLatestBalanceNeverHistoricalGrantBalance()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "request");
        var historical = boundary.ServerGrant("logical-claim", 20);
        boundary.ServerSpend(30); // Another server operation after the successful grant.
        guard.MarkIndeterminate(claim, "lost claim response");
        var query = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(query, historical), boundary.Latest(query), null, out string error), Is.True, error);
        Assert.That(historical.BalanceAfter, Is.EqualTo(120));
        Assert.That(boundary.Local.Balance, Is.EqualTo(90));
        Assert.That(boundary.Local.Version, Is.EqualTo(3));
        Assert.That(boundary.ApplyCount, Is.EqualTo(1));
        Assert.That(boundary.Lease.Completed, Is.True);
        Assert.That(guard.TryReconcile(claim, Committed(query, historical), boundary.Latest(query), null, out _), Is.False);
        Assert.That(guard.TryBeginClaim("request", out _, out _), Is.False);
        Assert.That(boundary.ApplyCount, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedServerClaimId_DoesNotCreditTwice_AndEqualLatestNeedsNoMutation()
    {
        var boundary = new MemoryBoundary();
        var firstGrant = boundary.ServerGrant("logical-claim", 20);
        boundary.Local = boundary.Server;
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "retry-after-restart");
        var repeatedGrant = boundary.ServerGrant("logical-claim", 20);
        var query = Query(guard, claim);
        Assert.That(repeatedGrant, Is.SameAs(firstGrant));
        Assert.That(guard.TryReconcile(claim, Committed(query, repeatedGrant), boundary.Latest(query), null, out _), Is.True);
        Assert.That(boundary.Server.Balance, Is.EqualTo(120));
        Assert.That(boundary.ApplyCount, Is.Zero);
    }

    [Test]
    public void ParallelClaimAndDuplicateSend_AreRejected_LeaseSurvivesUnknown()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        Assert.That(guard.TryBeginClaim("two", out _, out _), Is.False);
        Assert.That(guard.TryMarkRequestStarted(claim, out _), Is.False);
        guard.MarkIndeterminate(claim, "timeout");
        Assert.That(guard.TryCancelBeforeRequest(claim, out _), Is.False);
        Assert.That(guard.TryBeginClaim("two", out _, out _), Is.False);
        Assert.That(boundary.Lease.IsCurrent, Is.True);
        Assert.That(boundary.Lease.Completed, Is.False);
        Assert.That(boundary.Acquisitions, Is.EqualTo(1));
    }

    [Test]
    public void ASecondGuardCannotBypassTheBoundaryLeaseOfAnUnknownRequest()
    {
        var boundary = new MemoryBoundary();
        var first = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(first, "one");
        first.MarkIndeterminate(claim, "timeout");
        var second = new PopupStoreServerWalletGuard(boundary);
        Assert.That(second.TryBeginClaim("two", out _, out _), Is.False);
        Assert.That(first.ActiveClaim, Is.SameAs(claim));
        Assert.That(boundary.Lease.IsCurrent, Is.True);
    }

    [Test]
    public void KnownPreSendCancellation_CanReuseTheDurableRequestIdWithoutAnyGrant()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        Assert.That(guard.TryBeginClaim("one", out var claim, out _), Is.True);
        Assert.That(guard.TryCancelBeforeRequest(claim, out _), Is.True);
        Assert.That(claim.State, Is.EqualTo(PopupStoreWalletClaimState.CancelledBeforeRequest));
        Assert.That(guard.TryBeginClaim("one", out _, out _), Is.True);
        Assert.That(boundary.ApplyCount, Is.Zero);
    }

    [Test]
    public void WalletMutationBetweenAdmissionAndSend_RejectsBeforeMarkingRequestStarted()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        Assert.That(guard.TryBeginClaim("one", out var claim, out _), Is.True);
        boundary.Local = new PopupStoreWalletSnapshot("A", "wallet", 2, 90);
        Assert.That(guard.TryMarkRequestStarted(claim, out _), Is.False);
        Assert.That(boundary.Lease.Started, Is.False);
        Assert.That(guard.TryCancelBeforeRequest(claim, out _), Is.True);
    }

    [Test]
    public void LostMigrationCapabilityAfterSend_RetainsLeaseAndRejectsTheReply()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var grant = boundary.ServerGrant("logical-claim", 20);
        var query = Query(guard, claim);
        boundary.Capabilities = new PopupStoreWalletCapabilities(true, false);
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), null, out _), Is.False);
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.IsCurrent, Is.True);
        Assert.That(guard.ActiveClaim, Is.SameAs(claim));
    }

    [Test]
    public void AccountABA_RejectsOldReplyEvenWhenAccountGenerationAndBalanceMatch()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var query = Query(guard, claim);
        var grant = boundary.ServerGrant("logical-claim", 20);
        boundary.CurrentSession = new PopupStoreWalletSession("B", 2);
        boundary.CurrentSession = new PopupStoreWalletSession("A", 1); // Equal number is intentionally not authority.
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), null, out _), Is.False);
        Assert.That(boundary.Local.Balance, Is.EqualTo(100));
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.Completed, Is.False);
        Assert.That(guard.ActiveClaim, Is.SameAs(claim));
        Assert.That(guard.BeginReconciliation(claim, out _, out _), Is.False);
    }

    [Test]
    public void HistoricalReceiptAlone_CannotReleaseOrMutateWallet()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var query = Query(guard, claim);
        var grant = boundary.ServerGrant("logical-claim", 20);
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), null, null, out _), Is.False);
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.Completed, Is.False);
        Assert.That(claim.State, Is.EqualTo(PopupStoreWalletClaimState.Indeterminate));
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), null, out _), Is.False,
            "An uncertain reconciliation cannot replay its old query response.");
        var fresh = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(fresh, grant), boundary.Latest(fresh), null, out _), Is.True);
    }

    [Test]
    public void OutOfOrderQueryAndWrongRequestEvidence_CannotApply()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var old = Query(guard, claim);
        Query(guard, claim);
        var grant = boundary.ServerGrant("logical-claim", 20);
        Assert.That(guard.TryReconcile(claim, Committed(old, grant), boundary.Latest(old), null, out _), Is.False);
        var current = Query(guard, claim);
        var wrong = new PopupStoreWalletClaimEvidence(current, "another-request", "A", "wallet",
            PopupStoreWalletQueryOutcome.Committed, grant);
        Assert.That(guard.TryReconcile(claim, wrong, boundary.Latest(current), null, out _), Is.False);
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.Completed, Is.False);
    }

    [TestCase(2, 120)]
    [TestCase(3, 120)]
    public void StaleHistoricalOrSameVersionDifferentBalance_IsRejected(long version, int balance)
    {
        var boundary = new MemoryBoundary();
        var grant = boundary.ServerGrant("logical-claim", 20);
        boundary.ServerSpend(30);
        boundary.Local = boundary.Server;
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "retry");
        var query = Query(guard, claim);
        var stale = new PopupStoreLatestWalletSnapshot(query, new PopupStoreWalletSnapshot("A", "wallet", version, balance));
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), stale, null, out _), Is.False);
        Assert.That(boundary.Local.Balance, Is.EqualTo(90));
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.Completed, Is.False);
    }

    [Test]
    public void UnknownQuery_HoldsLease_UntilFreshDefinitiveQueryAndLatestWallet()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var unknown = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Evidence(unknown, PopupStoreWalletQueryOutcome.Unknown), boundary.Latest(unknown), null, out _), Is.False);
        Assert.That(boundary.Lease.Completed, Is.False);
        var fresh = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Evidence(fresh, PopupStoreWalletQueryOutcome.RejectedNotApplied), boundary.Latest(fresh), null, out _), Is.True);
        Assert.That(boundary.ApplyCount, Is.Zero);
        Assert.That(boundary.Lease.Completed, Is.True);
    }

    [Test]
    public void DisplayException_DoesNotReopenCompletedClaimOrApplyTwice()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var grant = boundary.ServerGrant("logical-claim", 20);
        var query = Query(guard, claim);
        int displays = 0;
        Action display = () => { displays++; throw new InvalidOperationException("display"); };
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), display, out _), Is.True);
        Assert.That(claim.State, Is.EqualTo(PopupStoreWalletClaimState.Reconciled));
        Assert.That(claim.DisplayError, Is.EqualTo(nameof(InvalidOperationException)));
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), display, out _), Is.False);
        Assert.That(displays, Is.EqualTo(1));
        Assert.That(boundary.ApplyCount, Is.EqualTo(1));
    }

    [Test]
    public void ApplyThenException_RequiresFreshQuery_AndDoesNotRepeatAppliedSnapshot()
    {
        var boundary = new MemoryBoundary { ThrowAfterApply = true };
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        var grant = boundary.ServerGrant("logical-claim", 20);
        var query = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), null, out _), Is.False);
        Assert.That(boundary.Local.Balance, Is.EqualTo(120));
        Assert.That(boundary.Lease.Completed, Is.False);
        boundary.ThrowAfterApply = false;
        var fresh = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(fresh, grant), boundary.Latest(fresh), null, out _), Is.True);
        Assert.That(boundary.ApplyCount, Is.EqualTo(1));
    }

    [Test]
    public void ReleaseFailure_RetainsOwnershipAndRequeriesWithoutRepeatingWalletApply()
    {
        var boundary = new MemoryBoundary();
        var guard = new PopupStoreServerWalletGuard(boundary);
        var claim = Start(guard, "one");
        boundary.Lease.AllowCompletion = false;
        var grant = boundary.ServerGrant("logical-claim", 20);
        var query = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(query, grant), boundary.Latest(query), null, out _), Is.False);
        Assert.That(guard.ActiveClaim, Is.SameAs(claim));
        Assert.That(boundary.Lease.IsCurrent, Is.True);
        boundary.Lease.AllowCompletion = true;
        var fresh = Query(guard, claim);
        Assert.That(guard.TryReconcile(claim, Committed(fresh, grant), boundary.Latest(fresh), null, out _), Is.True);
        Assert.That(boundary.ApplyCount, Is.EqualTo(1));
    }

    private static PopupStoreWalletClaim Start(PopupStoreServerWalletGuard guard, string request)
    {
        Assert.That(guard.TryBeginClaim(request, out var claim, out string error), Is.True, error);
        Assert.That(guard.TryMarkRequestStarted(claim, out error), Is.True, error);
        return claim;
    }
    private static PopupStoreWalletReconciliation Query(PopupStoreServerWalletGuard guard, PopupStoreWalletClaim claim)
    { Assert.That(guard.BeginReconciliation(claim, out var query, out string error), Is.True, error); return query; }
    private static PopupStoreWalletClaimEvidence Committed(PopupStoreWalletReconciliation query, PopupStoreHistoricalWalletGrant grant)
        => Evidence(query, PopupStoreWalletQueryOutcome.Committed, grant);
    private static PopupStoreWalletClaimEvidence Evidence(PopupStoreWalletReconciliation query, PopupStoreWalletQueryOutcome outcome,
        PopupStoreHistoricalWalletGrant grant = null) => new PopupStoreWalletClaimEvidence(query, query.Claim.RequestId,
            query.Claim.Session.AccountId, query.Claim.InitialWallet.WalletId, outcome, grant);

    private sealed class MemoryLease : IPopupStoreWalletLease
    {
        public bool IsCurrent => !Completed && !Cancelled;
        public bool Started, Completed, Cancelled, AllowCompletion = true;
        public bool TryMarkRequestStarted() { if (!IsCurrent || Started) return false; Started = true; return true; }
        public void MarkIndeterminate(string reason) { }
        public bool TryCompleteKnownOutcome()
        { if (!IsCurrent || !Started || !AllowCompletion) return false; Completed = true; return true; }
        public bool TryCancelBeforeRequest() { if (!IsCurrent || Started) return false; Cancelled = true; return true; }
    }

    /// <summary>Model only. This does not establish backend atomicity or implement the production migration.</summary>
    private sealed class MemoryBoundary : IPopupStoreServerWalletBoundary
    {
        private readonly Dictionary<string, PopupStoreHistoricalWalletGrant> _grants = new Dictionary<string, PopupStoreHistoricalWalletGrant>();
        public PopupStoreWalletCapabilities Capabilities { get; set; } = new PopupStoreWalletCapabilities(true, true);
        public PopupStoreWalletSession CurrentSession { get; set; } = new PopupStoreWalletSession("A", 1);
        public PopupStoreWalletSnapshot Local = new PopupStoreWalletSnapshot("A", "wallet", 1, 100);
        public PopupStoreWalletSnapshot Server = new PopupStoreWalletSnapshot("A", "wallet", 1, 100);
        public MemoryLease Lease;
        public int Acquisitions, ApplyCount;
        public bool ThrowAfterApply;
        public PopupStoreWalletSnapshot ReadLocalWallet() => Local;
        public bool TryAcquireClaimLease(PopupStoreWalletSession session, PopupStoreWalletSnapshot expectedLocal,
            out IPopupStoreWalletLease lease, out string error)
        {
            lease = null; error = null; Acquisitions++;
            if (Lease?.IsCurrent == true || !ReferenceEquals(session, CurrentSession) || !expectedLocal.SameState(Local)) return false;
            lease = Lease = new MemoryLease(); return true;
        }
        public bool TryApplyLatestWallet(IPopupStoreWalletLease lease, PopupStoreWalletSnapshot expectedLocal,
            PopupStoreLatestWalletSnapshot latest, out string error)
        {
            error = null;
            if (!ReferenceEquals(Lease, lease) || !lease.IsCurrent || !expectedLocal.SameState(Local)) return false;
            Local = latest.Wallet; ApplyCount++;
            if (ThrowAfterApply) throw new InvalidOperationException("after memory commit");
            return true;
        }
        public PopupStoreHistoricalWalletGrant ServerGrant(string logicalId, int amount)
        {
            if (_grants.TryGetValue(logicalId, out var existing)) return existing;
            Server = new PopupStoreWalletSnapshot("A", "wallet", checked(Server.Version + 1), checked(Server.Balance + amount));
            var grant = new PopupStoreHistoricalWalletGrant(logicalId, Server.Version, Server.Balance);
            _grants.Add(logicalId, grant); return grant;
        }
        public void ServerSpend(int amount)
        { Server = new PopupStoreWalletSnapshot("A", "wallet", checked(Server.Version + 1), checked(Server.Balance - amount)); }
        public PopupStoreLatestWalletSnapshot Latest(PopupStoreWalletReconciliation query) => new PopupStoreLatestWalletSnapshot(query, Server);
    }
}
#endif
