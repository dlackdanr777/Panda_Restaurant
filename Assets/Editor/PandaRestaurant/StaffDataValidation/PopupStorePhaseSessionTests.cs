#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

/// <summary>Synthetic TEST_ONLY model commands; these are not actual packing, UI or account evidence.</summary>
public sealed class PopupStorePhaseSessionTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void MissingBusinessIdentityRejectsCreation(string id)
    {
        Assert.That(PopupStorePhaseSession.TryCreate(id, PopupStorePhaseTestProfiles.CreateSnapshot(), out var model, out var error), Is.False);
        Assert.That(model, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void MissingProfileCannotBeReplacedWithOperationalDefaults()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-business", null, out var model, out var error), Is.False);
        Assert.That(model, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void ReadyDoesNotStartFromQueriesTimeOrPackingAndExplicitStartIsOnce()
    {
        var model = Create();
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Ready));
        Assert.That(model.PhaseGeneration, Is.Zero);
        Assert.That(model.PhaseRunId, Is.Empty);
        Assert.That(model.TryAdvanceTime(100), Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model)), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.FinalResult, Is.Null);
        Assert.That(model.TransitionCount, Is.Zero);
        Assert.That(model.TryStart(), Is.True);
        string id = model.PhaseRunId;
        Assert.That(model.TryStart(), Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
        Assert.That(model.PhaseRunId, Is.EqualTo(id));
        Assert.That(model.PhaseGeneration, Is.EqualTo(1));
    }

    [Test]
    public void OneCommittedOrderAssertionCountsOneRegardlessOfPlushQuantity()
    {
        var model = Started();
        // Evidence represents one externally authorized Receipt; material quantity is deliberately
        // not a multiplier or an independent source of transaction authorization in this core.
        Assert.That(model.TryRecordPacking(Evidence(model, "TEST_ONLY-two-plush-order", "receipt-1")), Is.True);
        Assert.That(model.PhaseSuccessfulOrders, Is.EqualTo(1));
        Assert.That(model.TotalSuccessfulOrders, Is.EqualTo(1));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [TestCase("receipt")]
    [TestCase("order")]
    public void SameReceiptOrSameOrderCannotBeCountedTwice(string duplicate)
    {
        var model = Started();
        Assert.That(model.TryRecordPacking(Evidence(model, "order-1", "receipt-1")), Is.True);
        var again = Evidence(model, duplicate == "order" ? "order-1" : "order-2",
            duplicate == "receipt" ? "receipt-1" : "receipt-2");
        Assert.That(model.TryRecordPacking(again), Is.False);
        Assert.That(model.TotalSuccessfulOrders, Is.EqualTo(1));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.TryRecordPacking(Evidence(model, "order-2", "receipt-2")), Is.True,
            "Rejected input must not reserve its otherwise unused ID.");
    }

    [TestCase("null")]
    [TestCase("business")]
    [TestCase("business-case")]
    [TestCase("phase")]
    [TestCase("empty-phase")]
    [TestCase("empty-order")]
    [TestCase("blank-order")]
    [TestCase("empty-receipt")]
    [TestCase("blank-receipt")]
    public void InvalidOrForeignEvidenceLeavesProgressUntouched(string defect)
    {
        var model = Started();
        var input = defect == "null" ? null : new PopupStorePhasePackingEvidence(
            defect == "business" ? "OTHER" : defect == "business-case" ? model.BusinessSessionId.ToUpperInvariant() : model.BusinessSessionId,
            defect == "phase" ? "future:99" : defect == "empty-phase" ? "" : model.PhaseRunId,
            defect == "empty-order" ? "" : defect == "blank-order" ? " " : "order-1",
            defect == "empty-receipt" ? "" : defect == "blank-receipt" ? " " : "receipt-1");
        Assert.That(model.TryRecordPacking(input), Is.False);
        Assert.That(model.TotalSuccessfulOrders, Is.Zero);
        Assert.That(model.PhaseSuccessfulOrders, Is.Zero);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(model.TryRecordPacking(Evidence(model, "order-1", "receipt-1")), Is.True);
    }

    [Test]
    public void DifferentCoreInstancesNeverReusePhaseTokensEvenForTheSameBusiness()
    {
        var first = Started(); var second = Started();
        Assert.That(first.BusinessSessionId, Is.EqualTo(second.BusinessSessionId));
        Assert.That(first.PhaseRunId, Is.Not.EqualTo(second.PhaseRunId));
        Assert.That(second.TryRecordPacking(Evidence(first)), Is.False);
    }

    [Test]
    public void TargetCommitsClearedFactOnceAndBlocksMorePackingBeforeCleanup()
    {
        var model = Started();
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        ReachGoal(model);
        int transitionCount = model.TransitionCount;
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.IsAwaitingCleanup, Is.True);
        Assert.That(model.PendingEndReason, Is.Null);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        CollectionAssert.AreEqual(new[] { 1 }, model.ClearedPhases);
        Assert.That(model.CanAcceptPacking, Is.False);
        Assert.That(model.CanStartCustomerEntry, Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model, "extra", "extra")), Is.False);
        Assert.That(model.TryAdvanceTime(1000), Is.False);
        Assert.That(model.TransitionCount, Is.EqualTo(transitionCount));
        Assert.That(model.FinalResult, Is.Null);
        Assert.That(model.FinalizationCount, Is.Zero);
    }

    [TestCase("business")]
    [TestCase("phase")]
    [TestCase("empty")]
    [TestCase("status")]
    [TestCase("null")]
    public void InvalidCleanupCannotReleaseTheCurrentPhase(string defect)
    {
        var model = Started(); ReachGoal(model);
        var input = defect == "null" ? null : new PopupStorePhaseCleanupSignal(
            defect == "business" ? "OTHER" : model.BusinessSessionId,
            defect == "phase" ? "old:1" : defect == "empty" ? "" : model.PhaseRunId,
            defect == "status" ? (PopupStorePhaseCleanupStatus)99 : PopupStorePhaseCleanupStatus.Completed);
        Assert.That(model.TryCompleteCleanup(input), Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
    }

    [Test]
    public void CleanupAcknowledgmentIsOnceAndStaleSignalsCannotReleaseTheNextPhase()
    {
        var model = Started(); ReachGoal(model);
        var old = Cleanup(model);
        Assert.That(model.TryCompleteCleanup(old), Is.True);
        Assert.That(model.TryCompleteCleanup(old), Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        MoveToNextPhase(model);
        ReachGoal(model);
        Assert.That(model.TryCompleteCleanup(old), Is.False);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
    }

    [TestCase(PopupStorePhaseCleanupStatus.Failed, PopupStorePhaseEndReason.CleanupFailed)]
    [TestCase(PopupStorePhaseCleanupStatus.Cancelled, PopupStorePhaseEndReason.CleanupCancelled)]
    public void FailedOrCancelledCleanupIsFaultedAndNeverCompleted(PopupStorePhaseCleanupStatus status, PopupStorePhaseEndReason reason)
    {
        var model = Started(); ReachGoal(model);
        Assert.That(model.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId, status)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Faulted));
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(reason));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.TryAdvanceTime(1), Is.False);
    }

    [Test]
    public void AllSixPhasesFinishOnceWithNoSeventhPhaseOrLastCountdown()
    {
        var model = Started();
        string business = model.BusinessSessionId;
        long orders = 0;
        for (int phase = 1; phase <= 6; phase++)
        {
            Assert.That(model.CurrentPhase.PhaseNumber, Is.EqualTo(phase));
            Assert.That(model.PhaseSuccessfulOrders, Is.Zero);
            Assert.That(model.RemainingSeconds, Is.EqualTo(model.CurrentPhase.TimeLimitSeconds));
            Assert.That(model.TryAdvanceTime(.25), Is.True);
            orders += model.CurrentPhase.TargetSuccessfulOrders;
            ReachGoal(model);
            Assert.That(model.FinalResult, Is.Null);
            Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
            Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
            if (phase < 6) MoveToNextPhase(model);
            else
            {
                Assert.That(model.TryAdvanceTime(model.CurrentPhase.StageClearNoticeSeconds), Is.True);
                Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Completed));
                Assert.That(model.CountdownValue, Is.Zero);
            }
            Assert.That(model.BusinessSessionId, Is.EqualTo(business));
        }
        var result = model.FinalResult;
        Assert.That(result.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Completed));
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
        Assert.That(result.LastAttemptedPhaseNumber, Is.EqualTo(6));
        Assert.That(result.SuccessfulOrders, Is.EqualTo(orders));
        Assert.That(result.ProfileId, Is.EqualTo(model.Profile.ProfileId));
        Assert.That(result.ProfileVersion, Is.EqualTo(model.Profile.Version));
        Assert.That(result.PhaseSummaries.Count, Is.EqualTo(6));
        CollectionAssert.AreEqual(Enumerable.Range(1, 6), result.ClearedPhases);
        Assert.That(result.PhaseSummaries.All(item => item.Cleared && item.UsedSeconds == .25), Is.True);
        Assert.That(result.PhaseSummaries.Select(item => item.PhaseRunId).Distinct().Count(), Is.EqualTo(6));
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
        Assert.That(model.TryStart(), Is.False);
        Assert.That(model.TryAdvanceTime(10000), Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model)), Is.False);
        Assert.That(model.FinalResult, Is.SameAs(result));
    }

    [Test]
    public void ReadyAbandonEndsWithoutInventingAStartedPhase()
    {
        var model = Create();
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
        Assert.That(model.FinalResult.LastAttemptedPhaseNumber, Is.Zero);
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(model.FinalResult.PhaseSummaries, Is.Empty);
        Assert.That(model.TryStart(), Is.False);
        Assert.That(model.TryAbandon(), Is.False);
    }

    [Test]
    public void PlayingAbandonKeepsCommittedProgressAndRequiresCleanupBeforeFinalResult()
    {
        var model = Started();
        model.TryAdvanceTime(1.25);
        Assert.That(model.TryRecordPacking(Evidence(model)), Is.True);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.FinalResult, Is.Null);
        Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model, "other", "other")), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
        Assert.That(model.FinalResult.SuccessfulOrders, Is.EqualTo(1));
        Assert.That(model.FinalResult.PhaseSummaries.Single().UsedSeconds, Is.EqualTo(1.25));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [Test]
    public void AbandonDuringGoalCleanupKeepsTheClearedFactButStopsFurtherPhases()
    {
        var model = Started(); ReachGoal(model);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(model.TryAdvanceTime(100), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AbandonAfterCleanupNeedsNoSecondCleanupAndNeverBecomesCompleted(bool countdown)
    {
        var model = Started(); ReachGoal(model); model.TryCompleteCleanup(Cleanup(model));
        if (countdown) model.TryAdvanceTime(model.CurrentPhase.StageClearNoticeSeconds);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
        Assert.That(model.IsAwaitingCleanup, Is.False);
        Assert.That(model.FinalResult.PhaseSummaries.Count, Is.EqualTo(1));
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
    }

    [Test]
    public void AcceptedTimeoutCannotBeRelabelledAsAbandonWhileWaitingForCleanup()
    {
        var model = Started();
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [Test]
    public void ExplicitAbandonIsAllowedDuringPauseButCleanupWaitsForResume()
    {
        var model = Started();
        model.TrySetPauseReason(PopupStorePhasePauseReason.ExitConfirmation, true);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.False);
        Assert.That(model.FinalResult, Is.Null);
        model.TrySetPauseReason(PopupStorePhasePauseReason.ExitConfirmation, false);
        Assert.That(model.TryCompleteCleanup(Cleanup(model)), Is.True);
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
    }

    private static PopupStorePhaseSession Create()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-business", PopupStorePhaseTestProfiles.CreateSnapshot(), out var model, out var error), Is.True, error);
        return model;
    }
    private static PopupStorePhaseSession Started()
    { var model = Create(); Assert.That(model.TryStart(), Is.True); return model; }
    private static PopupStorePhasePackingEvidence Evidence(PopupStorePhaseSession model, string order = "order-1", string receipt = "receipt-1")
        => new PopupStorePhasePackingEvidence(model.BusinessSessionId, model.PhaseRunId, order, receipt);
    private static PopupStorePhaseCleanupSignal Cleanup(PopupStorePhaseSession model)
        => new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId, PopupStorePhaseCleanupStatus.Completed);
    private static void ReachGoal(PopupStorePhaseSession model)
    {
        while (model.State == PopupStorePhaseState.Playing)
        {
            string suffix = model.PhaseRunId + ":" + model.PhaseSuccessfulOrders;
            Assert.That(model.TryRecordPacking(Evidence(model, "order-" + suffix, "receipt-" + suffix)), Is.True);
        }
    }
    private static void MoveToNextPhase(PopupStorePhaseSession model)
    {
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(model.TryAdvanceTime(model.CurrentPhase.StageClearNoticeSeconds), Is.True);
        for (int count = 3; count >= 1; count--)
        {
            Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Countdown));
            Assert.That(model.CountdownValue, Is.EqualTo(count));
            Assert.That(model.TryAdvanceTime(model.CurrentPhase.CountdownStepSeconds), Is.True);
        }
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }
}
#endif
