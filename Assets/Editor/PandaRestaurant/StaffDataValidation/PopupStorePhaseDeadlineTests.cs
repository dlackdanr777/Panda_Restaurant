#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

/// <summary>Pure TEST_ONLY phase commands. These assertions do not fabricate actual receipts or account approvals.</summary>
public sealed class PopupStorePhaseDeadlineTests
{
    [Test]
    public void ExistingCreationSignatureStillCompletesImmediatelyAtTheTarget()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-legacy", PopupStorePhaseTestProfiles.CreateSnapshot(),
            out var model, out var error), Is.True, error);
        Assert.That(model.CompletionPolicy, Is.EqualTo(PopupStorePhaseCompletionPolicy.ImmediateTarget));
        Assert.That(model.TryStart(), Is.True);
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [Test]
    public void ExplicitImmediatePolicyRetainsTheSameGoalBoundary()
    {
        var model = Started(PopupStorePhaseCompletionPolicy.ImmediateTarget);
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.CanAcceptPacking, Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.TryRecordPacking(Evidence(model, "too-late")), Is.False);
    }

    [TestCase(-1)] [TestCase(2)] [TestCase(999)]
    public void UnknownPolicyCannotCreateAnExecutableSession(int policy)
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-invalid", PopupStorePhaseTestProfiles.CreateSnapshot(),
            (PopupStorePhaseCompletionPolicy)policy, out var model, out var error), Is.False);
        Assert.That(model, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
    public void EveryStageKeepsPlayingBeyondItsTargetUntilItsOwnDeadline(int stage)
    {
        var model = AtStage(stage);
        string run = model.PhaseRunId;
        int transitions = model.TransitionCount;
        var previousGrade = model.HighestClearedGrade;
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders + 2);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.CanAcceptPacking && model.CanStartCustomerEntry, Is.True);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(previousGrade));
        Assert.That(model.ClearedPhases.Count, Is.EqualTo(stage - 1));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds - .125), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.PhaseRunId, Is.EqualTo(run));
        Assert.That(model.TransitionCount, Is.EqualTo(transitions));
        Assert.That(model.TryAdvanceTime(.125), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.PendingEndReason, Is.Null);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(model.CurrentPhase.Grade));
        Assert.That(model.ClearedPhases.Count, Is.EqualTo(stage));
        Assert.That(model.FinalResult, Is.Null);
    }

    [TestCase(0)] [TestCase(3)]
    public void ExactTargetAndOverTargetBothChooseSuccessOnlyAtZero(int surplus)
    {
        var model = Started();
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders + surplus);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.RemainingSeconds, Is.Zero);
        Assert.That(model.PendingEndReason, Is.Null);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(model.FinalResult, Is.Null);
    }

    [TestCase(0)] [TestCase(1)]
    public void FewerThanTheTargetChoosesTimeoutAndNeverRecordsAClear(int successful)
    {
        var model = Started(); AddOrders(model, successful);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Assert.That(model.ClearedPhases, Is.Empty);
        Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
        Assert.That(model.FinalResult.SuccessfulOrders, Is.EqualTo(successful));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [Test]
    public void ReceiptAcceptedBeforeTheLastTimeSampleRemainsCountedAtTheDeadline()
    {
        var model = Started();
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders - 1);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds - .001), Is.True);
        Assert.That(model.TryRecordPacking(Evidence(model, "last-before-zero")), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.TryAdvanceTime(.002), Is.True);
        Assert.That(model.PendingEndReason, Is.Null);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [Test]
    public void SettledZeroRejectsALateReceiptInsteadOfTurningFailureIntoSuccess()
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders - 1);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.TryRecordPacking(Evidence(model, "after-zero")), Is.False);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        var result = model.FinalResult;
        Assert.That(model.TryRecordPacking(Evidence(model, "after-result")), Is.False);
        Assert.That(model.FinalResult, Is.SameAs(result));
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
    }

    [Test]
    public void HugeDeadlineSampleCannotSkipCleanupOrSpendTheNextStage()
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(double.MaxValue), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.RemainingSeconds, Is.Zero);
        Assert.That(model.TryAdvanceTime(double.MaxValue), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        AdvanceNoticeAndCountdown(model);
        Assert.That(model.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
        Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
        Assert.That(model.PhaseSuccessfulOrders, Is.Zero);
    }

    [Test]
    public void ZeroElapsedAfterReachingTheTargetIsNotAnEarlyDeadline()
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(0), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [TestCase(-1d)] [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
    public void InvalidTimeCannotResolveATargetAlreadyReached(double elapsed)
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(elapsed), Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [TestCase("receipt")] [TestCase("order")]
    public void DuplicateIdentityRemainsRejectedAfterTheGoalWithoutBurningTheOtherIdentity(string duplicate)
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        var accepted = Evidence(model, "over-goal");
        Assert.That(model.TryRecordPacking(accepted), Is.True);
        var rejected = new PopupStorePhasePackingEvidence(model.BusinessSessionId, model.PhaseRunId,
            duplicate == "order" ? accepted.OrderId : "unused-order",
            duplicate == "receipt" ? accepted.ReceiptId : "unused-receipt");
        int before = model.PhaseSuccessfulOrders;
        Assert.That(model.TryRecordPacking(rejected), Is.False);
        Assert.That(model.PhaseSuccessfulOrders, Is.EqualTo(before));
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "unused-order", "unused-receipt")), Is.True);
    }

    [TestCase("business")] [TestCase("phase")] [TestCase("order")] [TestCase("receipt")]
    public void DeadlinePolicyDoesNotWeakenTheEvidenceIdentityRequirements(string defect)
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        int before = model.PhaseSuccessfulOrders;
        var invalid = new PopupStorePhasePackingEvidence(defect == "business" ? "other" : model.BusinessSessionId,
            defect == "phase" ? "other:1" : model.PhaseRunId, defect == "order" ? " " : "new-order",
            defect == "receipt" ? " " : "new-receipt");
        Assert.That(model.TryRecordPacking(invalid), Is.False);
        Assert.That(model.PhaseSuccessfulOrders, Is.EqualTo(before));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }

    [Test]
    public void OldPhaseReceiptAndCleanupCannotAffectTheNextStage()
    {
        var model = Started();
        var oldReceipt = Evidence(model, "last-old");
        Assert.That(model.TryRecordPacking(oldReceipt), Is.True);
        AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders - 1);
        var oldCleanup = Cleanup(model);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.TryCompleteCleanup(oldCleanup), Is.True); AdvanceNoticeAndCountdown(model);
        Assert.That(model.TryRecordPacking(oldReceipt), Is.False);
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "new-order", oldReceipt.ReceiptId)), Is.False);
        Assert.That(model.TryCompleteCleanup(oldCleanup), Is.False);
        Assert.That(model.PhaseSuccessfulOrders, Is.Zero);
    }

    [Test]
    public void RepeatedDeadlineCallsRecordOneClearAndOneCleanupTransition()
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        int transitions = model.TransitionCount;
        for (int i = 0; i < 5; i++) Assert.That(model.TryAdvanceTime(0), Is.False);
        CollectionAssert.AreEqual(new[] { 1 }, model.ClearedPhases);
        Assert.That(model.TransitionCount, Is.EqualTo(transitions));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.FinalizationCount, Is.Zero);
    }

    [TestCase(PopupStorePhasePauseReason.ApplicationBackground)]
    [TestCase(PopupStorePhasePauseReason.FocusLost)]
    [TestCase(PopupStorePhasePauseReason.ViewHidden)]
    [TestCase(PopupStorePhasePauseReason.ExitConfirmation)]
    public void ReachedGoalDoesNotBypassAnyExistingPauseReason(PopupStorePhasePauseReason reason)
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TrySetPauseReason(reason, true), Is.True);
        Assert.That(model.TryAdvanceTime(1000), Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model, "paused")), Is.False);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(model.TrySetPauseReason(reason, false), Is.True);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [Test]
    public void AbandonBeforeDeadlineKeepsSalesWithoutAwardingThePendingStage()
    {
        var model = AtStage(2); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders + 1);
        long total = model.TotalSuccessfulOrders;
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
        Assert.That(model.FinalResult.SuccessfulOrders, Is.EqualTo(total));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(model.FinalResult.PhaseSummaries.Last().Cleared, Is.False);
    }

    [Test]
    public void AbandonAfterSuccessfulDeadlinePreservesTheAcceptedClearFact()
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [TestCase(PopupStorePhaseCleanupStatus.Failed, PopupStorePhaseEndReason.CleanupFailed)]
    [TestCase(PopupStorePhaseCleanupStatus.Cancelled, PopupStorePhaseEndReason.CleanupCancelled)]
    public void DeadlineSuccessCannotFabricateSuccessfulVisualCleanup(PopupStorePhaseCleanupStatus status, PopupStorePhaseEndReason reason)
    {
        var model = Started(); AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId, status)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Faulted));
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(reason));
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
    }

    [Test]
    public void AllSixDeadlinesFinishOnceAndTheLastSuccessHasNoSeventhCountdown()
    {
        var model = Started(); string business = model.BusinessSessionId;
        for (int stage = 1; stage <= 6; stage++)
        {
            AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders + 1);
            Assert.That(model.FinalResult, Is.Null);
            Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
            Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
            if (stage < 6) AdvanceNoticeAndCountdown(model);
            else Assert.That(model.TryAdvanceTime(model.CurrentPhase.StageClearNoticeSeconds), Is.True);
        }
        var result = model.FinalResult;
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Completed));
        Assert.That(result.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Completed));
        Assert.That(result.BusinessSessionId, Is.EqualTo(business));
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
        Assert.That(model.PhaseGeneration, Is.EqualTo(6)); Assert.That(model.CountdownValue, Is.Zero);
        CollectionAssert.AreEqual(Enumerable.Range(1, 6), result.ClearedPhases);
        Assert.That(result.SuccessfulOrders, Is.EqualTo(model.Profile.Phases.Sum(phase => phase.TargetSuccessfulOrders + 1)));
        foreach (var summary in result.PhaseSummaries)
        {
            Assert.That(summary.Cleared, Is.True);
            Assert.That(summary.UsedSeconds, Is.EqualTo(model.Profile.Phases[summary.PhaseNumber - 1].TimeLimitSeconds));
        }
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
        Assert.That(model.TryAdvanceTime(1000), Is.False); Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.FinalResult, Is.SameAs(result));
    }

    private static PopupStorePhaseSession Started(PopupStorePhaseCompletionPolicy policy = PopupStorePhaseCompletionPolicy.Deadline)
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-deadline", PopupStorePhaseTestProfiles.CreateSnapshot(), policy,
            out var model, out var error), Is.True, error);
        Assert.That(model.TryStart(), Is.True); return model;
    }
    private static PopupStorePhasePackingEvidence Evidence(PopupStorePhaseSession model, string suffix)
        => new PopupStorePhasePackingEvidence(model.BusinessSessionId, model.PhaseRunId, "order-" + suffix, "receipt-" + suffix);
    private static PopupStorePhaseCleanupSignal Cleanup(PopupStorePhaseSession model)
        => new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId, PopupStorePhaseCleanupStatus.Completed);
    private static void AddOrders(PopupStorePhaseSession model, int count)
    {
        for (int i = 0; i < count; i++)
            Assert.That(model.TryRecordPacking(Evidence(model, model.PhaseRunId + ":" + model.TotalSuccessfulOrders)), Is.True);
    }
    private static PopupStorePhaseSession AtStage(int target)
    {
        var model = Started();
        while (model.CurrentPhase.PhaseNumber < target)
        {
            AddOrders(model, model.CurrentPhase.TargetSuccessfulOrders);
            Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
            Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
            AdvanceNoticeAndCountdown(model);
        }
        return model;
    }
    private static void AdvanceNoticeAndCountdown(PopupStorePhaseSession model)
    {
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(model.TryAdvanceTime(double.MaxValue), Is.True);
        foreach (int value in new[] { 3, 2, 1 })
        {
            Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Countdown));
            Assert.That(model.CountdownValue, Is.EqualTo(value));
            Assert.That(model.TryAdvanceTime(double.MaxValue), Is.True);
        }
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }
}
#endif
