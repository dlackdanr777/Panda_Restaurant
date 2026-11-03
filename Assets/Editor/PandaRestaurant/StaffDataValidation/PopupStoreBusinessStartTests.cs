#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEngine;

/// <summary>Fake start authority and policy tests only. No live quota, SDK request, payment,
/// account persistence, player-facing mode or Play Mode is introduced.</summary>
public sealed class PopupStoreBusinessStartTests
{
    private string _accountSnapshot;
    private bool _sdk;
    private BackendManager _backend;
    private UnityEngine.Random.State _random;
    private Dictionary<string, string> _protected;
    private const string Folder = "Assets/Scripts/UI/Floor3UI/";
    private const string Day = "2026-11-02";

    [OneTimeSetUp]
    public void SnapshotProtectedFiles()
    {
        var paths = new List<string> { "Assets/Resources/UI/PopupStore/UIPopupStore.prefab", "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset",
            Folder + "PopupStoreRewardPersistence.cs", Folder + "PopupStoreRewardTransaction.cs", Folder + "PopupStoreRewardRecovery.cs",
            Folder + "PopupStoreRewardDecision.cs", Folder + "PopupStoreRewardSession.cs", Folder + "PopupStoreGradeSession.cs",
            "Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStoreRewardFakeRepository.cs" };
        foreach (string grade in new[] { "C", "B", "A", "S", "SS", "SSS" })
            paths.Add("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png");
        _protected = paths.ToDictionary(p => p, Hash);
    }
    [OneTimeTearDown]
    public void AssertProtectedFiles()
    { foreach (var pair in _protected) Assert.That(Hash(pair.Key), Is.EqualTo(pair.Value), pair.Key + " changed."); }
    [SetUp]
    public void Before()
    {
        _accountSnapshot = AccountState(); _sdk = BackEnd.Backend.IsInitialized; _backend = BackendManager.ExistingInstance;
        _random = UnityEngine.Random.state;
    }
    [TearDown]
    public void After()
    {
        Assert.That(AccountState(), Is.EqualTo(_accountSnapshot)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void DefaultPolicyIsAnImmutableTwoStartTestProposalWithSafePracticeClockFailure()
    {
        var policy = new PopupStoreBusinessAccessPolicy(); Assert.That(policy.IsTestPolicy, Is.True);
        Assert.That(policy.Version, Is.EqualTo("v1.3-test")); Assert.That(policy.RewardedDailyLimit, Is.EqualTo(2));
        Assert.That(policy.PracticeClockFailure, Is.EqualTo(PopupStorePracticeClockFailure.Reject));
        Assert.That(typeof(PopupStoreBusinessAccessPolicy).GetProperties().All(p => !p.CanWrite), Is.True);
    }

    [TestCase(0)]
    [TestCase(3)]
    public void ConfiguredRewardedLimitIsIndependentOfR0AndOnlyAcceptedStartsConsumeIt(int limit)
    {
        var h = new Harness(new PopupStoreBusinessAccessPolicy("custom", limit));
        for (int i = 0; i < limit; i++) Assert.That(h.Start("r" + i).State, Is.EqualTo(PopupStoreBusinessStartState.Started));
        var denied = h.Start("denied"); Assert.That(denied.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(denied.Record, Is.Null); AssertSnapshot(h, Day, limit, limit); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(limit));
    }

    [Test]
    public void InvalidPolicyConfigurationCannotSilentlyEnableAReleaseOrOfflineMode()
    {
        Assert.Throws<ArgumentException>(() => new PopupStoreBusinessAccessPolicy(" "));
        Assert.Throws<ArgumentException>(() => new PopupStoreBusinessAccessPolicy(rewardedDailyLimit: -1));
        Assert.Throws<ArgumentException>(() => new PopupStoreBusinessAccessPolicy(practiceClockFailure: (PopupStorePracticeClockFailure)99));
    }

    [Test]
    public void OpeningOrInspectingAStartScreenAndPreparingACheckpointConsumesNothing()
    {
        var h = new Harness(); var tx = h.Transaction("prepared");
        for (int i = 0; i < 8; i++) { h.Store.Read("A", Day); tx.Capture(); }
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Prepared));
        Assert.That(h.Context.GetPendingRequest("A"), Is.Null); Assert.That(h.Repository.SubmitCalls, Is.Zero); AssertSnapshot(h, Day, 0, 0);
    }

    [Test]
    public void RewardedStartsMoveZeroToOneToTwoAndThirdDenialIssuesNoBusinessId()
    {
        var h = new Harness(); AssertSnapshot(h, Day, 0, 0);
        var one = h.Start("one"); Assert.That(one.Record.RewardedUsesAfterStart, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
        var two = h.Start("two"); Assert.That(two.Record.RewardedUsesAfterStart, Is.EqualTo(2)); AssertSnapshot(h, Day, 2, 2);
        var three = h.Start("three"); Assert.That(three.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(three.Record, Is.Null); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(2)); AssertSnapshot(h, Day, 2, 2);
        var probe = new PaymentProbe(true); Assert.That(probe.TryRun(h, three.Record, 1, PopupStoreBusinessEndReason.Completed), Is.False); probe.AssertUntouched();
    }

    [Test]
    public void PracticeRemainsUnlimitedAfterRewardedQuotaIsUsedWithoutAnyRandomOrPayoutCall()
    {
        var h = new Harness(); h.Start("one"); h.Start("two"); var probe = new PaymentProbe(true);
        for (int i = 0; i < 25; i++)
        {
            var tx = h.Start("practice-" + i, PopupStoreBusinessMode.Practice);
            Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
            Assert.That(tx.Record.Mode, Is.EqualTo(PopupStoreBusinessMode.Practice));
            Assert.That(probe.TryRun(h, tx.Record, 20, PopupStoreBusinessEndReason.Completed), Is.False);
        }
        AssertSnapshot(h, Day, 2, 27); probe.AssertUntouched();
    }

    [TestCase(0, PopupStoreBusinessEndReason.Completed)]
    [TestCase(3, PopupStoreBusinessEndReason.DirectExit)]
    public void ZeroSalesOrDirectExitRetainsConsumedQuotaButNeverDrawsOrCreatesAPayment(int successful, PopupStoreBusinessEndReason reason)
    {
        var h = new Harness(); var tx = h.Start("business"); var probe = new PaymentProbe(true);
        Assert.That(probe.TryRun(h, tx.Record, successful, reason), Is.False); probe.AssertUntouched(); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void EligibleFakeOutcomeCanUseC1ContractWithoutChargingTheStartQuotaAgain()
    {
        var h = new Harness(); var tx = h.Start("business"); var probe = new PaymentProbe(false);
        Assert.That(probe.TryRun(h, tx.Record, 1, PopupStoreBusinessEndReason.Completed), Is.True);
        Assert.That(probe.TryRun(h, tx.Record, 1, PopupStoreBusinessEndReason.Completed), Is.True);
        Assert.That(probe.Store.Read("A").Balance, Is.EqualTo(110)); Assert.That(probe.Store.Read("A").Grants, Is.EqualTo(1));
        AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void ClientConstructedLookalikeRecordCannotAuthorizeTheFakeRewardFlow()
    {
        var h = new Harness(); var stored = h.Start("real").Record;
        var forged = new PopupStoreBusinessStartRecord(stored.Request, stored.BusinessSessionId, stored.StartDayKey, stored.StartedAtUtc, stored.RewardedUsesAfterStart);
        Assert.That(forged.Matches(stored.Request), Is.True, "Shape validity is intentionally weaker than authority.");
        var probe = new PaymentProbe(true); Assert.That(probe.TryRun(h, forged, 1, PopupStoreBusinessEndReason.Completed), Is.False); probe.AssertUntouched();
    }

    [Test]
    public void MalformedStartRecordWithoutARequestFailsTheRewardGateSafely()
    {
        var h = new Harness(); var malformed = new PopupStoreBusinessStartRecord(null, "fake", Day, h.Now, 1);
        var probe = new PaymentProbe(true); Assert.That(probe.TryRun(h, malformed, 1, PopupStoreBusinessEndReason.Completed), Is.False); probe.AssertUntouched();
        AssertSnapshot(h, Day, 0, 0);
    }

    [TestCase("2026-11-02T02:59:59.9999999+00:00", "2026-11-01")]
    [TestCase("2026-11-02T03:00:00+00:00", "2026-11-02")]
    [TestCase("2026-11-02T03:00:00.0000001+00:00", "2026-11-02")]
    [TestCase("2027-01-01T02:59:59+00:00", "2026-12-31")]
    [TestCase("2028-03-01T02:59:59+00:00", "2028-02-29")]
    public void GameDayChangesAtExactlyKstNoonIncludingCalendarBoundaries(string input, string expected)
    {
        Assert.That(PopupStoreDailyUsage.TryGetDayKey(Instant(input), out string key), Is.True); Assert.That(key, Is.EqualTo(expected));
    }

    [Test]
    public void EquivalentInstantsProduceTheSameDayRegardlessOfOffsetOrCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            foreach (string value in new[] { "2026-11-02T03:00:00+00:00", "2026-11-02T12:00:00+09:00", "2026-11-01T22:00:00-05:00" })
            { Assert.That(PopupStoreDailyUsage.TryGetDayKey(Instant(value), out string key), Is.True); Assert.That(key, Is.EqualTo(Day)); }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void UnrepresentablePreviousGameDateFailsClosedWithoutWrapping()
    {
        Assert.That(PopupStoreDailyUsage.TryGetDayKey(DateTimeOffset.MinValue, out string key), Is.False); Assert.That(key, Is.Null);
        Assert.That(PopupStoreDailyUsage.TryGetDayKey(DateTimeOffset.MaxValue, out key), Is.True); Assert.That(key, Is.EqualTo("9999-12-31"));
    }

    [Test]
    public void NewGameDayHasItsOwnCounterAndOldBusinessKeepsItsOriginalDayForever()
    {
        var h = new Harness(); h.Now = Instant("2026-11-02T02:59:59Z");
        var old = h.Start("before"); h.Start("before-two"); AssertSnapshot(h, "2026-11-01", 2, 2);
        h.Now = Instant("2026-11-02T03:00:00Z"); var next = h.Start("after");
        Assert.That(next.Record.StartDayKey, Is.EqualTo(Day)); AssertSnapshot(h, Day, 1, 3);
        var replay = h.Transaction("before"); replay.TrySubmit();
        Assert.That(replay.Record, Is.SameAs(old.Record)); Assert.That(replay.Record.StartDayKey, Is.EqualTo("2026-11-01"));
        AssertSnapshot(h, "2026-11-01", 2, 3); AssertSnapshot(h, Day, 1, 3);
    }

    [Test]
    public void ResponseLostJustBeforeNoonRecoversOldDayWithoutConsumingTheNewDay()
    {
        var h = new Harness(); h.Now = Instant("2026-11-02T02:59:59Z");
        h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate;
        var tx = h.Start("edge"); h.Now = Instant("2026-11-02T03:00:00Z"); tx.TryReconcile();
        Assert.That(tx.Record.StartDayKey, Is.EqualTo("2026-11-01")); AssertSnapshot(h, Day, 0, 1); AssertSnapshot(h, "2026-11-01", 1, 1);
    }

    [TestCase("missing")]
    [TestCase("throw")]
    [TestCase("underflow")]
    public void NewRewardedStartRequiresAUsableAuthorityClock(string failure)
    {
        var h = new Harness();
        h.Store.Clock = () => failure == "throw" ? throw new InvalidOperationException("clock unavailable")
            : failure == "underflow" ? DateTimeOffset.MinValue : (DateTimeOffset?)null;
        var tx = h.Start("clock-failed"); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        AssertSnapshot(h, Day, 0, 0); Assert.That(h.Store.IssuedBusinessCount, Is.Zero);
        h.Store.Clock = () => Instant("2026-11-02T03:00:00Z"); tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PracticeClockFailureRequiresTheExplicitTestOptionAndStillCannotReward(bool allow)
    {
        var policy = new PopupStoreBusinessAccessPolicy(practiceClockFailure: allow ? PopupStorePracticeClockFailure.AllowWithoutDayKey : PopupStorePracticeClockFailure.Reject);
        var h = new Harness(policy); h.Now = null; var tx = h.Start("practice", PopupStoreBusinessMode.Practice);
        Assert.That(tx.State, Is.EqualTo(allow ? PopupStoreBusinessStartState.Started : PopupStoreBusinessStartState.RejectedBeforeSend));
        if (allow) { Assert.That(tx.Record.StartDayKey, Is.Null); Assert.That(tx.Record.StartedAtUtc, Is.Null); }
        var probe = new PaymentProbe(true); Assert.That(probe.TryRun(h, tx.Record, 1, PopupStoreBusinessEndReason.Completed), Is.False); probe.AssertUntouched();
        AssertSnapshot(h, Day, 0, allow ? 1 : 0);
    }

    [Test]
    public void AcceptedReplayPrecedesClockFailureAndPolicyChanges()
    {
        var h = new Harness(); var first = h.Start("accepted");
        h.Store.Clock = () => throw new InvalidOperationException("should not need clock for replay");
        h.Store.UsePolicy(new PopupStoreBusinessAccessPolicy("changed", 0));
        var replay = h.Transaction("accepted"); replay.TrySubmit(); Assert.That(replay.Record, Is.SameAs(first.Record));
        Assert.That(replay.Record.PolicyVersion, Is.EqualTo("v1.3-test")); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void IdenticalStartAndDuplicateCallbacksReturnOneBusinessAndOneConsumption()
    {
        var h = new Harness(); int calls = 0; var tx = h.Transaction("same", complete: r => calls++); tx.TrySubmit();
        for (int i = 0; i < 5; i++) { tx.TrySubmit(); h.Repository.ReplayLast(); }
        var second = h.Transaction("same"); second.TrySubmit(); Assert.That(second.Record, Is.SameAs(tx.Record));
        Assert.That(calls, Is.EqualTo(1)); Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [TestCase("mode")]
    [TestCase("policy")]
    public void OneStartRequestCannotChangeItsAcceptedModeOrPolicy(string field)
    {
        var h = new Harness(); var first = h.Start("same");
        var request = new PopupStoreBusinessStartRequest("A", "same", field == "mode" ? PopupStoreBusinessMode.Practice : PopupStoreBusinessMode.Rewarded,
            field == "policy" ? "different" : "v1.3-test");
        var tx = h.From(request); tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Conflict));
        Assert.That(h.Store.Find("A", "same"), Is.SameAs(first.Record)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void FailedClockAttemptStillPinsTheRequestConditionsAgainstModeChanges()
    {
        var h = new Harness(); h.Now = null; h.Start("same"); h.Now = Instant("2026-11-02T03:00:00Z");
        var changed = h.Start("same", PopupStoreBusinessMode.Practice); Assert.That(changed.State, Is.EqualTo(PopupStoreBusinessStartState.Conflict)); AssertSnapshot(h, Day, 0, 0);
    }

    [Test]
    public void LimitRefusalReplaysAcrossNoonAndRequiresANewStartIdentityForTheNewDay()
    {
        var h = new Harness(); h.Start("one"); h.Start("two"); var denied = h.Start("third");
        h.Now = Instant("2026-11-03T03:00:00Z"); denied.TrySubmit();
        Assert.That(denied.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); AssertSnapshot(h, "2026-11-03", 0, 2);
        var next = h.Start("new-day"); Assert.That(next.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, "2026-11-03", 1, 3);
    }

    [Test]
    public void ExpectedBusinessIdCanVerifyAnExistingReceiptButCannotChooseANewBusiness()
    {
        var h = new Harness(); var invented = h.From(Request("invented", expected: "chosen-by-client")); invented.TrySubmit();
        Assert.That(invented.State, Is.EqualTo(PopupStoreBusinessStartState.Conflict)); AssertSnapshot(h, Day, 0, 0);
        var actual = h.Start("real", context: new PopupStoreBusinessStartContext());
        var correct = h.From(Request("real", expected: actual.Record.BusinessSessionId), new PopupStoreBusinessStartContext()); correct.TryReconcile();
        Assert.That(correct.Record, Is.SameAs(actual.Record));
        var wrong = h.From(Request("real", expected: "wrong"), new PopupStoreBusinessStartContext()); wrong.TryReconcile();
        Assert.That(wrong.State, Is.EqualTo(PopupStoreBusinessStartState.Conflict)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void UnknownStartBlocksBothRetryAndAnotherRequestOnTheSameDeviceUntilReconciled()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate; var first = h.Start("first");
        var second = h.Transaction("second"); Assert.That(first.TrySubmit(), Is.False); Assert.That(second.TrySubmit(), Is.False);
        Assert.That(h.Context.GetPendingRequest("A"), Is.SameAs(first.Request)); Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
        first.TryReconcile(); Assert.That(h.Context.GetPendingRequest("A"), Is.Null);
        Assert.That(second.TrySubmit(), Is.True); AssertSnapshot(h, Day, 2, 2);
    }

    [TestCase(PopupStoreFakeStartQueryFault.NotFound)]
    [TestCase(PopupStoreFakeStartQueryFault.Indeterminate)]
    [TestCase(PopupStoreFakeStartQueryFault.Throw)]
    public void QueryProblemsDoNotReleaseThePendingStartOrProveNoConsumption(PopupStoreFakeStartQueryFault failure)
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate; var tx = h.Start("pending");
        h.Repository.NextQueryFault = failure; tx.TryReconcile();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.ReconciliationRequired)); Assert.That(h.Transaction("replacement").TrySubmit(), Is.False);
        AssertSnapshot(h, Day, 1, 1); tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void AuthoritativeNoEffectProofAllowsOnlyAStableRequestRetryAfterUncertainty()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.UnknownWithoutCommit; var tx = h.Start("unknown");
        tx.TryReconcile(); Assert.That(tx.TrySubmit(), Is.False); AssertSnapshot(h, Day, 0, 0);
        h.Repository.NextQueryFault = PopupStoreFakeStartQueryFault.ConfirmNotApplied; tx.TryReconcile();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.NotAppliedConfirmed)); Assert.That(tx.TrySubmit(), Is.True); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void ExplicitPreSendRejectionCreatesNothingAndReleasesTheDeviceLease()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.RejectBeforeSend; var rejected = h.Start("rejected");
        Assert.That(rejected.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); Assert.That(h.Context.GetPendingRequest("A"), Is.Null);
        AssertSnapshot(h, Day, 0, 0); Assert.That(h.Start("different").State, Is.EqualTo(PopupStoreBusinessStartState.Started));
    }

    [Test]
    public void MissingCallbackCanSettleItsLateSuccessExactlyOnce()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDrop; var tx = h.Start("late");
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Submitting)); tx.MarkResponseMissing();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Indeterminate)); h.Repository.ReplayLast(); h.Repository.ReplayLast();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void OldSuccessCannotClearANewerDifferentRequestLease()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer; var old = h.Start("old"); old.MarkResponseMissing();
        var restored = PopupStoreBusinessStartTransaction.Restore(old.Capture(), h.Repository, () => h.Session, h.Context); restored.TryReconcile();
        h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate; var next = h.Start("next");
        h.Repository.DeliverNext(); Assert.That(h.Context.GetPendingRequest("A"), Is.SameAs(next.Request));
        Assert.That(h.Transaction("third").TrySubmit(), Is.False); next.TryReconcile(); AssertSnapshot(h, Day, 2, 2);
    }

    [Test]
    public void ANewerQueryIgnoresTheEarlierSubmissionCallback()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer; var tx = h.Start("pending"); tx.MarkResponseMissing();
        h.Repository.NextQueryFault = PopupStoreFakeStartQueryFault.Defer; tx.TryReconcile();
        h.Repository.DeliverNext(); Assert.That(tx.Record, Is.Null); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void StartCommittedThenLocalHandlerFailureRetriesOnlyIdempotentLocalCompletion()
    {
        var h = new Harness(); int calls = 0; var seen = new HashSet<string>();
        var tx = h.Transaction("handler", complete: record => { seen.Add(record.BusinessSessionId); if (++calls == 1) throw new Exception("view setup failed"); }); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); Assert.That(tx.LocalCompletionPending, Is.True);
        h.Repository.ReplayLast(); Assert.That(calls, Is.EqualTo(1)); Assert.That(tx.RetryLocalCompletion(), Is.True);
        Assert.That(seen.Count, Is.EqualTo(1)); Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void AdapterExceptionAfterCommitIsUnknownAndCannotCauseASecondStart()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.ThrowAfterCommit; var tx = h.Start("exception");
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Indeterminate)); Assert.That(tx.TrySubmit(), Is.False);
        tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, Day, 1, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdapterExceptionAfterTerminalCallbackDoesNotUndoStartedOrRejected(bool rejected)
    {
        var h = new Harness(); if (rejected) h.Repository.NextSubmitFault = PopupStoreFakeStartFault.RejectBeforeSend;
        h.Repository.ThrowAfterNextDelivery = true; var tx = h.Start("terminal");
        Assert.That(tx.State, Is.EqualTo(rejected ? PopupStoreBusinessStartState.RejectedBeforeSend : PopupStoreBusinessStartState.Started));
        AssertSnapshot(h, Day, rejected ? 0 : 1, rejected ? 0 : 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecreatedExecutorQueriesAuthorityRegardlessOfCheckpointObservedState(bool observedSuccess)
    {
        var h = new Harness(); if (!observedSuccess) h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDrop;
        var old = h.Start("restart"); var saved = old.Capture(); old.Invalidate();
        var adapter = new PopupStoreBusinessStartFakeRepository(h.Store); var freshContext = new PopupStoreBusinessStartContext();
        var restored = PopupStoreBusinessStartTransaction.Restore(saved, adapter, () => h.Session, freshContext);
        Assert.That(restored.State, Is.EqualTo(PopupStoreBusinessStartState.ReconciliationRequired)); Assert.That(restored.TrySubmit(), Is.False);
        restored.TryReconcile(); Assert.That(restored.Record, Is.SameAs(h.Store.Find("A", "restart")));
        Assert.That(adapter.SubmitCalls, Is.Zero); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void RecoveryRejectsAShapeValidReplacementBusinessIdAndKeepsTheOriginalLease()
    {
        var h = new Harness(); var first = h.Start("known-business");
        var restored = PopupStoreBusinessStartTransaction.Restore(first.Capture(), h.Repository, () => h.Session, h.Context);
        h.Repository.TransformNextReply = response => new PopupStoreBusinessStartResponse(response.Request, PopupStoreBusinessStartResponseKind.Started,
            new PopupStoreBusinessStartRecord(response.Record.Request, "substituted-business", response.Record.StartDayKey,
                response.Record.StartedAtUtc, response.Record.RewardedUsesAfterStart));
        restored.TryReconcile(); Assert.That(restored.State, Is.EqualTo(PopupStoreBusinessStartState.ReconciliationRequired));
        Assert.That(restored.Record, Is.Null); Assert.That(h.Transaction("replacement").TrySubmit(), Is.False);
        restored.TryReconcile(); Assert.That(restored.Record, Is.SameAs(first.Record));
        Assert.That(restored.ConfirmationCount, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void QueryRejectionOrContradictoryRecordCannotProveTheOriginalStartHadNoEffect(bool contradictoryRecord)
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate;
        var tx = h.Start("query-safety");
        h.Repository.TransformNextReply = response => new PopupStoreBusinessStartResponse(response.Request,
            contradictoryRecord ? PopupStoreBusinessStartResponseKind.NotFound : PopupStoreBusinessStartResponseKind.RejectedBeforeSend,
            contradictoryRecord ? response.Record : null, "Injected unsafe query outcome.");
        tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.ReconciliationRequired));
        Assert.That(tx.Record, Is.Null); Assert.That(h.Transaction("replacement").TrySubmit(), Is.False);
        AssertSnapshot(h, Day, 1, 1); tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void SameRawRequestIdIsSeparatedByAuthenticatedAccount()
    {
        var h = new Harness(); var a = h.Start("same"); h.Store.RegisterAccount("B"); h.Session = h.Store.OpenSession("B");
        var b = h.From(new PopupStoreBusinessStartRequest("B", "same", PopupStoreBusinessMode.Rewarded, "v1.3-test")); b.TrySubmit();
        Assert.That(b.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); Assert.That(a.Record.BusinessSessionId, Is.Not.EqualTo(b.Record.BusinessSessionId));
        Assert.That(h.Store.Read("B", Day).Usage, Is.EqualTo(1)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void AccountSwitchRejectsOldCallbackAndReturningAccountRecoversOnlyItsOriginalStart()
    {
        var h = new Harness(); h.Store.RegisterAccount("B"); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
        int callbacks = 0; var tx = h.Transaction("A-start", complete: record => callbacks++); tx.TrySubmit(); var saved = tx.Capture();
        h.Session = h.Store.OpenSession("B"); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Invalidated)); Assert.That(callbacks, Is.Zero); Assert.That(h.Store.Read("B", Day).Starts, Is.Zero);
        h.Session = h.Store.OpenSession("A"); var restored = PopupStoreBusinessStartTransaction.Restore(saved, h.Repository, () => h.Session, h.Context); restored.TryReconcile();
        Assert.That(restored.Record.AccountId, Is.EqualTo("A")); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void SameAccountReloginCannotApplyTheOldSessionCallback()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer; var tx = h.Start("session");
        h.Session = h.Store.OpenSession("A"); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Invalidated)); Assert.That(tx.Record, Is.Null); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void WrongAccountCannotQueryOrRestoreAnotherAccountsStart()
    {
        var h = new Harness(); var tx = h.Start("A-only"); h.Store.RegisterAccount("B"); h.Session = h.Store.OpenSession("B");
        var restored = PopupStoreBusinessStartTransaction.Restore(tx.Capture(), h.Repository, () => h.Session, new PopupStoreBusinessStartContext());
        Assert.That(restored.State, Is.EqualTo(PopupStoreBusinessStartState.Invalidated)); Assert.That(restored.TryReconcile(), Is.False);
        var reply = h.Store.Query(h.Session, tx.Request); Assert.That(reply.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend));
        Assert.That(h.Store.Read("B", Day).Starts, Is.Zero);
    }

    [Test]
    public void ForgedOrRevokedTokensCannotConsumeQuota()
    {
        var h = new Harness(); h.Session = new PopupStoreRewardAccountSession("A"); var forged = h.Start("forged");
        Assert.That(forged.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        h.Session = h.Store.OpenSession("A"); h.Store.Revoke(h.Session); var revoked = h.Start("revoked");
        Assert.That(revoked.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); AssertSnapshot(h, Day, 0, 0);
    }

    [Test]
    public void LastRemainingRewardedStartCanBeConsumedByOnlyOneOfTwoDevices()
    {
        var h = new Harness(); h.Start("already-one"); var states = new PopupStoreBusinessStartState[2];
        RunTogether(i =>
        {
            var repo = new PopupStoreBusinessStartFakeRepository(h.Store);
            var tx = new PopupStoreBusinessStartTransaction(Request("device-" + i), repo, () => h.Session, new PopupStoreBusinessStartContext());
            tx.TrySubmit(); states[i] = tx.State;
        }, 2);
        Assert.That(states.Count(s => s == PopupStoreBusinessStartState.Started), Is.EqualTo(1));
        Assert.That(states.Count(s => s == PopupStoreBusinessStartState.RejectedBeforeSend), Is.EqualTo(1)); AssertSnapshot(h, Day, 2, 2);
    }

    [Test]
    public void ConcurrentDevicesReplayingOneRequestReceiveOneBusinessRecordAndOneUsage()
    {
        var h = new Harness(); var records = new PopupStoreBusinessStartRecord[10];
        RunTogether(i =>
        {
            var tx = new PopupStoreBusinessStartTransaction(Request("same"), new PopupStoreBusinessStartFakeRepository(h.Store), () => h.Session, new PopupStoreBusinessStartContext());
            tx.TrySubmit(); records[i] = tx.Record;
        }, records.Length);
        Assert.That(records[0], Is.Not.Null); Assert.That(records.All(r => ReferenceEquals(r, records[0])), Is.True); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void ConcurrentReadersNeverObserveARewardedBusinessWithoutItsQuotaConsumption()
    {
        var h = new Harness(new PopupStoreBusinessAccessPolicy(rewardedDailyLimit: 20));
        RunTogether(i =>
        {
            for (int j = i; j < 20; j += 4)
            {
                h.Store.Submit(h.Session, Request("atomic-" + j));
                var snapshot = h.Store.Read("A", Day);
                if (snapshot.Usage != snapshot.RewardedStartsForDay || snapshot.Usage != snapshot.Starts) throw new Exception("Partial start/quota commit observed.");
            }
        }, 4);
        AssertSnapshot(h, Day, 20, 20);
    }

    [Test]
    public void NoEffectReconciliationFencesAnOlderSubmissionWaitingToCommit()
    {
        var h = new Harness(); var request = Request("fenced"); PopupStoreBusinessStartResponse late = null; Exception error = null;
        using (var entered = new ManualResetEventSlim(false))
        using (var release = new ManualResetEventSlim(false))
        {
            h.Store.BeforeSubmitLock = ticket => { entered.Set(); release.Wait(); };
            var thread = new Thread(() => { try { late = h.Store.Submit(h.Session, request); } catch (Exception e) { error = e; } }); thread.IsBackground = true; thread.Start();
            bool ready = false, joined = false; PopupStoreBusinessStartResponse proof = null;
            try { ready = entered.Wait(TimeSpan.FromSeconds(10)); proof = h.Store.Query(h.Session, request, true); }
            finally { release.Set(); joined = thread.Join(10000); h.Store.BeforeSubmitLock = null; }
            Assert.That(ready && joined, Is.True); Assert.That(error, Is.Null); Assert.That(proof.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.NotAppliedConfirmed));
            Assert.That(late.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend)); AssertSnapshot(h, Day, 0, 0);
            Assert.That(h.Start("fenced").State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, Day, 1, 1);
        }
    }

    [TestCase("day")]
    [TestCase("mode")]
    [TestCase("missing")]
    [TestCase("echo")]
    public void MalformedStartResponseCannotConfirmOrReleaseAnotherStart(string problem)
    {
        var h = new Harness(); h.Repository.TransformNextReply = r =>
        {
            var changed = Request("bad", mode: problem == "mode" ? PopupStoreBusinessMode.Practice : PopupStoreBusinessMode.Rewarded);
            var record = problem == "missing" ? null : new PopupStoreBusinessStartRecord(changed, r.Record.BusinessSessionId,
                problem == "day" ? "1999-01-01" : r.Record.StartDayKey, r.Record.StartedAtUtc, r.Record.RewardedUsesAfterStart);
            return new PopupStoreBusinessStartResponse(problem == "echo" ? Request("other") : r.Request, PopupStoreBusinessStartResponseKind.Started, record);
        };
        var tx = h.Start("bad"); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Indeterminate)); Assert.That(tx.Record, Is.Null);
        Assert.That(h.Transaction("replacement").TrySubmit(), Is.False); tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void SessionGetterInvalidationCannotReviveAnExecutorThroughReconcile()
    {
        var h = new Harness(); bool invalidate = false; PopupStoreBusinessStartTransaction tx = null;
        tx = new PopupStoreBusinessStartTransaction(Request("getter"), h.Repository, () => { if (invalidate && tx != null) tx.Invalidate(); return h.Session; }, h.Context);
        h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer; tx.TrySubmit(); invalidate = true;
        Assert.That(tx.TryReconcile(), Is.False); h.Repository.DeliverNext(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Invalidated)); AssertSnapshot(h, Day, 1, 1);
    }

    [Test]
    public void ClockDelegateRevokingAuthenticationCannotCommitAfterItsReturn()
    {
        var h = new Harness(); h.Store.Clock = () => { h.Store.Revoke(h.Session); return Instant("2026-11-02T03:00:00Z"); };
        var tx = h.Start("revoked-during-clock"); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); AssertSnapshot(h, Day, 0, 0);
    }

    [Test]
    public void ClockDelegateChangingPolicyCannotCommitUsingTheEarlierPolicy()
    {
        var h = new Harness(); h.Store.Clock = () => { h.Store.UsePolicy(new PopupStoreBusinessAccessPolicy("new-version", 0)); return Instant("2026-11-02T03:00:00Z"); };
        var tx = h.Start("policy-race"); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); AssertSnapshot(h, Day, 0, 0);
    }

    [Test]
    public void StartPolicyExecutionLeavesExistingLocalPreviewFinalResultAndApprovedBoxUntouched()
    {
        var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
        var host = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
        try
        {
            var root = (GameObject)Read(host, "Root"); var reward = root.GetComponent<PopupStoreRewardSession>();
            Assert.That(reward.TrySetRandomSource(new FixedRandom()), Is.True); Assert.That(reward.Refresh(), Is.True);
            Call(host, "Start", 90d); Call(host, "SellUntil", PopupStoreGradeTier.C); Call(host, "End"); Call(host, "ShowResult");
            var grade = root.GetComponent<PopupStoreGradeSession>(); var box = root.GetComponent<PopupStoreRewardBox>();
            var originalDecision = reward.Result; var originalFinal = grade.FinalResult; var sprite = box.DisplayedSprite;
            int draws = reward.RandomDrawCount, finalized = grade.FinalizationCount, animations = box.AnimationCount;
            var h = new Harness(); var practice = h.Start("practice", PopupStoreBusinessMode.Practice); var probe = new PaymentProbe(true);
            Assert.That(probe.TryRun(h, practice.Record, 1, PopupStoreBusinessEndReason.Completed), Is.False); probe.AssertUntouched();
            Assert.That(reward.Result, Is.SameAs(originalDecision)); Assert.That(reward.RandomDrawCount, Is.EqualTo(draws));
            Assert.That(grade.FinalResult, Is.SameAs(originalFinal)); Assert.That(grade.FinalizationCount, Is.EqualTo(finalized));
            Assert.That(box.DisplayedSprite, Is.SameAs(sprite)); Assert.That(box.AnimationCount, Is.EqualTo(animations));
            Debug.Log("POPUP_STORE_START_POLICY_TEST_EVIDENCE mode=Practice starts=1 rewardedUses=0 randomCalls=0 paymentFactories=0 fakeLedger=0 fakeBalance=100 realAccountChanges=0 localPreviewUnchanged=True");
        }
        finally { ((IDisposable)host).Dispose(); }
    }

    [Test]
    public void RuntimePolicyContractsHaveNoSdkPlayerPrefsDeviceClockOrRewardExecutionCalls()
    {
        foreach (string name in new[] { "PopupStoreBusinessAccessPolicy", "PopupStoreDailyUsage", "PopupStoreBusinessStartTransaction",
            "PopupStoreAdmissionHost", "PopupStoreStartController" })
        {
            string source = File.ReadAllText(Folder + name + ".cs");
            foreach (string forbidden in new[] { "UserInfo.", "Backend.", "PlayerPrefs.", "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow",
                "UnityEngine.Random.", "new PopupStoreRewardTransaction", "new PopupStoreRewardRandomSource", "File.Write", "Directory.Create" })
                Assert.That(source, Does.Not.Contain(forbidden), name + " has forbidden execution dependency.");
        }
        // DEV-05E-1 adds one private callback-observation proxy, not a live authority or default adapter.
        var proxy = typeof(PopupStoreAdmissionHost).GetNestedType("ObservedPersistence", BindingFlags.NonPublic);
        Assert.That(proxy, Is.Not.Null);
        Assert.That(proxy.IsNestedPrivate && proxy.IsSealed, Is.True);
        var adapters = typeof(IPopupStoreBusinessStartPersistence).Assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface
            && typeof(IPopupStoreBusinessStartPersistence).IsAssignableFrom(t)).ToArray();
        CollectionAssert.AreEquivalent(new[] { proxy }, adapters, "Only the injected authority forwarding proxy is permitted.");
        var fields = proxy.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.That(fields, Has.Length.EqualTo(1));
        Assert.That(fields[0].FieldType, Is.EqualTo(typeof(PopupStoreAdmissionHost)));
        Assert.That(fields[0].IsInitOnly, Is.True);
        Assert.That(typeof(PopupStoreAdmissionHost).GetConstructor(Type.EmptyTypes), Is.Null,
            "The runtime host must receive its authority; it cannot construct a default adapter.");
        string hostSource = File.ReadAllText(Folder + "PopupStoreAdmissionHost.cs");
        Assert.That(hostSource, Does.Contain("_owner._persistence.Submit("));
        Assert.That(hostSource, Does.Contain("_owner._persistence.Query("));
        foreach (string forbidden in new[] { "System.IO", "System.Net", "Resources.Load", "PopupStoreBusinessStartFake", "new Backend" })
            Assert.That(hostSource, Does.Not.Contain(forbidden), "The observer proxy cannot supply storage, network or fake authority.");
    }

    private sealed class Harness
    {
        internal readonly PopupStoreBusinessAccessPolicy Policy;
        internal readonly PopupStoreBusinessStartFakeStore Store;
        internal readonly PopupStoreBusinessStartFakeRepository Repository;
        internal readonly PopupStoreBusinessStartContext Context = new PopupStoreBusinessStartContext();
        internal PopupStoreRewardAccountSession Session;
        internal DateTimeOffset? Now = Instant("2026-11-02T03:00:00Z");
        internal Harness(PopupStoreBusinessAccessPolicy policy = null)
        {
            Policy = policy ?? new PopupStoreBusinessAccessPolicy(); Store = new PopupStoreBusinessStartFakeStore(() => Now, Policy);
            Store.RegisterAccount("A"); Session = Store.OpenSession("A"); Repository = new PopupStoreBusinessStartFakeRepository(Store);
        }
        internal PopupStoreBusinessStartTransaction From(PopupStoreBusinessStartRequest request, PopupStoreBusinessStartContext context = null,
            Action<PopupStoreBusinessStartRecord> complete = null)
            => new PopupStoreBusinessStartTransaction(request, Repository, () => Session, context ?? Context, complete);
        internal PopupStoreBusinessStartTransaction Transaction(string id, PopupStoreBusinessMode mode = PopupStoreBusinessMode.Rewarded,
            PopupStoreBusinessStartContext context = null, Action<PopupStoreBusinessStartRecord> complete = null)
            => From(new PopupStoreBusinessStartRequest("A", id, mode, Policy.Version), context, complete);
        internal PopupStoreBusinessStartTransaction Start(string id, PopupStoreBusinessMode mode = PopupStoreBusinessMode.Rewarded,
            PopupStoreBusinessStartContext context = null)
        { var tx = Transaction(id, mode, context); tx.TrySubmit(); return tx; }
    }
    private sealed class PaymentProbe
    {
        internal readonly PopupStoreRewardFakeStore Store = new PopupStoreRewardFakeStore();
        private readonly PopupStoreRewardAccountSession _session;
        private readonly bool _forbid;
        private readonly Dictionary<string, PopupStoreRewardRequest> _decisions = new Dictionary<string, PopupStoreRewardRequest>();
        private int _draws, _factories;
        internal PaymentProbe(bool forbid) { _forbid = forbid; Store.RegisterAccount("A", 100); _session = Store.OpenSession("A"); }
        internal bool TryRun(Harness h, PopupStoreBusinessStartRecord record, int success, PopupStoreBusinessEndReason reason)
        {
            if (!h.Store.CanConsiderReward(h.Session, record, success, reason)) return false;
            if (!_decisions.TryGetValue(record.BusinessSessionId, out var approved))
            {
                _draws++; if (_forbid) throw new Exception("An ineligible mode/outcome reached the random supplier.");
                approved = new PopupStoreRewardRequest("A", record.BusinessSessionId, "authority-decision-" + record.BusinessSessionId,
                    PopupStoreGradeTier.C, PopupStoreRewardKind.Jackpot, 10, "R0");
                Store.RegisterDecision(approved); _decisions.Add(record.BusinessSessionId, approved);
            }
            _factories++; if (_forbid) throw new Exception("An ineligible mode/outcome constructed a payout transaction.");
            var tx = new PopupStoreRewardTransaction(approved, new PopupStoreRewardFakeRepository(Store), () => _session); tx.TrySubmit();
            return tx.State == PopupStoreRewardTransactionState.Granted;
        }
        internal void AssertUntouched()
        {
            Assert.That(_draws, Is.Zero); Assert.That(_factories, Is.Zero); var snapshot = Store.Read("A");
            Assert.That(snapshot.Balance, Is.EqualTo(100)); Assert.That(snapshot.Grants, Is.Zero); Assert.That(snapshot.Version, Is.Zero);
        }
    }
    private sealed class FixedRandom : IPopupStoreRewardRandomSource { public int NextInt(int exclusiveUpperBound) => 99; }
    private static PopupStoreBusinessStartRequest Request(string id, PopupStoreBusinessMode mode = PopupStoreBusinessMode.Rewarded, string expected = null)
        => new PopupStoreBusinessStartRequest("A", id, mode, "v1.3-test", expected);
    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
    private static void AssertSnapshot(Harness h, string day, int uses, int starts)
    { var snapshot = h.Store.Read("A", day); Assert.That(snapshot.Usage, Is.EqualTo(uses)); Assert.That(snapshot.Starts, Is.EqualTo(starts)); Assert.That(snapshot.RewardedStartsForDay, Is.EqualTo(uses)); }
    private static string AccountState() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
        UserInfo.CustomerEncounterContextGeneration, UserInfo.DailyAddMoney, UserInfo.DailyCookCount, UserInfo.DailyCumulativeCustomerCount,
        UserInfo.DailyAdvertisingViewCount, UserInfo.DailyCleanCount, UserInfo.DailyAdGoldRewardCount, UserInfo.DailyAdDiaRewardCount });
    private static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    private static object Read(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    private static void RunTogether(Action<int> action, int count)
    {
        using (var ready = new CountdownEvent(count))
        using (var go = new ManualResetEventSlim(false))
        {
            var errors = new Exception[count]; var threads = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                threads[i] = new Thread(() => { ready.Signal(); go.Wait(); try { action(index); } catch (Exception e) { errors[index] = e; } });
                threads[i].IsBackground = true; threads[i].Start();
            }
            bool started = ready.Wait(TimeSpan.FromSeconds(10)); go.Set(); bool joined = true;
            foreach (var thread in threads) joined &= thread.Join(10000);
            Assert.That(started && joined, Is.True, "Fake concurrent workers did not settle."); Assert.That(errors.Where(e => e != null).ToArray(), Is.Empty);
        }
    }
}
#endif
