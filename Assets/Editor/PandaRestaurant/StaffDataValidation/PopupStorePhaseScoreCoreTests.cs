#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>Pure score-goal contract. Evidence is a TEST_ONLY adapter assertion, not a fabricated game receipt.</summary>
public sealed class PopupStorePhaseScoreCoreTests
{
    [Test]
    public void GoalMetricAndEveryTargetAreDeepFrozenBeforeExecution()
    {
        var mutable = Profile(); var first = mutable.Phases[0]; var snapshot = Snapshot(mutable);
        mutable.GoalMetric = PopupStorePhaseGoalMetric.SuccessfulOrders;
        first.TargetScore = 1; mutable.Phases[1].TargetScore = 2;
        mutable.Phases = null;
        Assert.That(snapshot.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
        Assert.That(snapshot.Phases.Select(p => p.TargetScore), Is.EqualTo(new[] { 300, 600, 900, 1200, 1500, 1800 }));
        Assert.That(typeof(PopupStorePhaseSnapshot).GetProperty("TargetScore").GetSetMethod(true), Is.Null);
        Assert.That(typeof(PopupStorePhaseProfileSnapshot).GetProperty("GoalMetric").GetSetMethod(true), Is.Null);
    }

    [TestCase(-1)] [TestCase(2)] [TestCase(999)]
    public void UndefinedGoalMetricCannotBecomeAnExecutableSnapshot(int value)
    { var profile = Profile(); profile.GoalMetric = (PopupStorePhaseGoalMetric)value; Reject(profile, "GoalMetric"); }

    [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
    public void NonPositiveScoreTargetsAreRejected(int value)
    { var profile = Profile(); profile.Phases[0].TargetScore = value; Reject(profile, "TargetScore"); }

    [TestCase(300)] [TestCase(299)]
    public void LaterCumulativeTargetCannotEqualOrDecreaseFromItsPredecessor(int target)
    { var profile = Profile(); profile.Phases[1].TargetScore = target; Reject(profile, "TargetScore"); }

    [Test]
    public void NegativeUnusedLegacyFieldsAreRejectedRatherThanSilentlyCorrected()
    {
        var profile = Profile(); profile.Phases[0].TargetSuccessfulOrders = -1;
        Reject(profile, "TargetSuccessfulOrders");
        var legacy = PopupStorePhaseTestProfiles.CreateDefinitionProfile(); legacy.Phases[0].TargetScore = -1;
        Reject(legacy, "TargetScore");
    }

    [Test]
    public void ScoreProfileDoesNotRequireAnySuccessfulOrderTarget()
    {
        var profile = Profile();
        Assert.That(profile.Phases.All(p => p.TargetSuccessfulOrders == 0), Is.True);
        Assert.That(Snapshot(profile).Phases.All(p => p.TargetSuccessfulOrders == 0), Is.True);
    }

    [Test]
    public void OmittedCreationPolicyChoosesDeadlineForScoreGoalsOnly()
    {
        var score = Create();
        Assert.That(score.CompletionPolicy, Is.EqualTo(PopupStorePhaseCompletionPolicy.Deadline));
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-legacy", PopupStorePhaseTestProfiles.CreateSnapshot(),
            out var legacy, out var error), Is.True, error);
        Assert.That(legacy.Profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.SuccessfulOrders));
        Assert.That(legacy.CompletionPolicy, Is.EqualTo(PopupStorePhaseCompletionPolicy.ImmediateTarget));
    }

    [Test]
    public void ExplicitEarlyCompletionPolicyCannotBypassScoreDeadline()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-score", Snapshot(Profile()),
            PopupStorePhaseCompletionPolicy.ImmediateTarget, out var model, out var error), Is.False);
        Assert.That(model, Is.Null); Assert.That(error, Does.Contain("deadline"));
    }

    [Test]
    public void LegacyFourIdentityEvidenceStillCompletesItsOriginalCountCriterion()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-legacy", PopupStorePhaseTestProfiles.CreateSnapshot(),
            out var model, out var error), Is.True, error);
        Assert.That(model.TryStart(), Is.True);
        for (int i = 0; i < model.CurrentPhase.TargetSuccessfulOrders; i++)
            Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId, model.PhaseRunId,
                "order-" + i, "receipt-" + i)), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.TotalScore, Is.Zero); Assert.That(model.PhaseScore, Is.Zero);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
    public void InvalidAwardDoesNotReserveEitherIdentityOrIncrementAnyCounter(int points)
    {
        var model = Started(); var before = Stamp(model);
        Assert.That(model.TryRecordPacking(Evidence(model, "same", points)), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Assert.That(model.TryRecordPacking(Evidence(model, "same", 100)), Is.True);
        Assert.That(model.TotalScore, Is.EqualTo(100));
        Assert.That(model.PhaseSuccessfulOrders, Is.EqualTo(1));
    }

    [Test]
    public void LegacyEvidenceWithoutPointsCannotScoreInTheNewCriterion()
    {
        var model = Started();
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "order-one", "receipt-one")), Is.False);
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "order-one", "receipt-one", 140)), Is.True);
        Assert.That(model.TotalScore, Is.EqualTo(140));
    }

    [Test]
    public void SameNumberOfSuccessfulOrdersCanFailOrPassDependingOnTheirAwardTotal()
    {
        var low = Started(); var high = Started();
        Record(low, "one", 100); Record(low, "two", 100);
        Record(high, "one", 100); Record(high, "two", 200);
        Assert.That(low.TotalSuccessfulOrders, Is.EqualTo(high.TotalSuccessfulOrders));
        Deadline(low); Deadline(high);
        Assert.That(low.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Assert.That(high.PendingEndReason, Is.Null);
        Assert.That(low.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(high.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
    }

    [Test]
    public void ACountTargetNeitherPassesNorBlocksTheCumulativeScoreCriterion()
    {
        var profile = Profile(); profile.Phases[0].TargetSuccessfulOrders = 999;
        var model = Started(profile); Record(model, "one", 300); Deadline(model);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        profile = Profile(); profile.Phases[0].TargetSuccessfulOrders = 1;
        model = Started(profile);
        for (int i = 0; i < 5; i++) Record(model, i.ToString(), 10);
        Deadline(model);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
    }

    [TestCase(299, false)] [TestCase(300, true)] [TestCase(301, true)]
    public void ExactDeadlineBoundaryUsesGreaterThanOrEqualCumulativeScore(int points, bool passed)
    {
        var model = Started(); Record(model, "one", points);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Deadline(model);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(passed ? PopupStoreGradeTier.C : PopupStoreGradeTier.None));
        Assert.That(model.PendingEndReason == null, Is.EqualTo(passed));
    }

    [Test]
    public void ReachingGoalEarlyKeepsPackingAndDoesNotFinalizeBeforeZero()
    {
        var model = Started(); Record(model, "goal", 300);
        Assert.That(model.TryAdvanceTime(model.RemainingSeconds - .125), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Record(model, "extra", 180);
        Assert.That(model.TotalScore, Is.EqualTo(480));
        Assert.That(model.FinalResult, Is.Null); Assert.That(model.ClearedPhases, Is.Empty);
        Assert.That(model.TryAdvanceTime(.125), Is.True);
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        var before = Stamp(model);
        Assert.That(model.TryRecordPacking(Evidence(model, "late", 180)), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
    }

    [Test]
    public void DeadlineBeforePackingCannotBeReversedByLateHighPoints()
    {
        var model = Started(); Record(model, "one", 100); Deadline(model);
        Assert.That(model.TryRecordPacking(Evidence(model, "late", 1000)), Is.False);
        Assert.That(model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Cleanup(model);
        Assert.That(model.FinalResult.TotalScore, Is.EqualTo(100));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [Test]
    public void PhaseScoreResetsWhileCumulativeScoreCountsTowardTheNextTarget()
    {
        var model = Started(); Record(model, "c", 400); Deadline(model); Next(model);
        Assert.That(model.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
        Assert.That(model.PhaseScore, Is.Zero); Assert.That(model.TotalScore, Is.EqualTo(400));
        Assert.That(model.PhaseSuccessfulOrders, Is.Zero); Assert.That(model.TotalSuccessfulOrders, Is.EqualTo(1));
        Record(model, "b", 200); Deadline(model);
        Assert.That(model.PhaseScore, Is.EqualTo(200));
        Assert.That(model.TotalScore, Is.EqualTo(600));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.B));
    }

    [Test]
    public void SurplusFromEarlierPhaseCanSatisfyTheNextCumulativeGoalButStillWaitsForItsDeadline()
    {
        var model = Started(); Record(model, "surplus", 800); Deadline(model); Next(model);
        Assert.That(model.PhaseScore, Is.Zero); Assert.That(model.TotalScore, Is.EqualTo(800));
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Deadline(model);
        Assert.That(model.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.B));
        Assert.That(model.PhaseSuccessfulOrders, Is.Zero);
    }

    [Test]
    public void DuplicateReceiptOrOrderCannotIncreaseTheCumulativeAward()
    {
        var model = Started(); Record(model, "one", 100); var before = Stamp(model);
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "other-order", "receipt-one", 300)), Is.False);
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "order-one", "other-receipt", 300)), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Assert.That(model.TryRecordPacking(new PopupStorePhasePackingEvidence(model.BusinessSessionId,
            model.PhaseRunId, "other-order", "other-receipt", 200)), Is.True);
        Assert.That(model.TotalScore, Is.EqualTo(300));
    }

    [TestCase("session")] [TestCase("phase")] [TestCase("order")] [TestCase("receipt")]
    public void InvalidIdentityLeavesPointsAndReceiptReservationsUntouched(string defect)
    {
        var model = Started(); var before = Stamp(model);
        var evidence = new PopupStorePhasePackingEvidence(defect == "session" ? "foreign" : model.BusinessSessionId,
            defect == "phase" ? "foreign" : model.PhaseRunId, defect == "order" ? " " : "order-one",
            defect == "receipt" ? null : "receipt-one", 300);
        Assert.That(model.TryRecordPacking(evidence), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Record(model, "one", 300);
    }

    [Test]
    public void PreviousPhaseLateEvidenceCannotLeakItsScoreIntoANewRun()
    {
        var model = Started(); var late = Evidence(model, "late", 999);
        Record(model, "c", 300); Deadline(model); Next(model);
        var before = Stamp(model);
        Assert.That(model.TryRecordPacking(late), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Record(model, "late", 140);
        Assert.That(model.TotalScore, Is.EqualTo(440));
        Assert.That(model.PhaseScore, Is.EqualTo(140));
    }

    [TestCase(PopupStorePhasePauseReason.ApplicationBackground)]
    [TestCase(PopupStorePhasePauseReason.FocusLost)]
    [TestCase(PopupStorePhasePauseReason.ViewHidden)]
    [TestCase(PopupStorePhasePauseReason.ExitConfirmation)]
    public void EveryPauseReasonRejectsScoresUntilResume(PopupStorePhasePauseReason reason)
    {
        var model = Started(); Assert.That(model.TrySetPauseReason(reason, true), Is.True);
        var before = Stamp(model);
        Assert.That(model.TryRecordPacking(Evidence(model, "paused", 300)), Is.False);
        Assert.That(model.TryAdvanceTime(100), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Assert.That(model.TrySetPauseReason(reason, false), Is.True);
        Record(model, "paused", 300);
    }

    [Test]
    public void OverflowRejectsBeforeIdentityReservationAndLeavesTheLedgerAtomic()
    {
        var model = Started(); Record(model, "large", int.MaxValue - 1); var before = Stamp(model);
        Assert.That(model.TryRecordPacking(Evidence(model, "edge", 2)), Is.False);
        Assert.That(Stamp(model), Is.EqualTo(before));
        Record(model, "edge", 1);
        Assert.That(model.TotalScore, Is.EqualTo(int.MaxValue));
        Assert.That(model.PhaseScore, Is.EqualTo(int.MaxValue));
        Assert.That(model.TotalSuccessfulOrders, Is.EqualTo(2));
        Assert.That(model.TryRecordPacking(Evidence(model, "overflow", 1)), Is.False);
        Assert.That(model.TotalSuccessfulOrders, Is.EqualTo(2));
    }

    [Test]
    public void SuccessfulAllSixStagesFreezeEachPhaseAndCumulativeScoreOnce()
    {
        var model = Started();
        for (int i = 1; i <= 6; i++)
        {
            Assert.That(model.CurrentPhase.PhaseNumber, Is.EqualTo(i));
            Record(model, "stage-" + i, 300); Deadline(model);
            if (i < 6) Next(model);
            else { Cleanup(model); Assert.That(model.TryAdvanceTime(model.TransitionRemainingSeconds), Is.True); }
        }
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Completed));
        var result = model.FinalResult;
        Assert.That(result.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
        Assert.That(result.TotalScore, Is.EqualTo(1800));
        Assert.That(result.SuccessfulOrders, Is.EqualTo(6));
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
        Assert.That(result.PhaseSummaries.Select(p => p.PhaseScore), Is.EqualTo(Enumerable.Repeat(300, 6)));
        Assert.That(result.PhaseSummaries.Select(p => p.TotalScore), Is.EqualTo(new[] { 300, 600, 900, 1200, 1500, 1800 }));
        Assert.That(model.TryAdvanceTime(100), Is.False);
        Assert.That(model.TryRecordPacking(Evidence(model, "after", 100)), Is.False);
        Assert.That(model.TryAbandon(), Is.False);
        Assert.That(model.FinalResult, Is.SameAs(result));
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
    }

    [Test]
    public void PartialPhaseTimeoutPreservesBothEarlierAndUnclearedPhasePoints()
    {
        var model = Started(); Record(model, "c", 300); Deadline(model); Next(model);
        Record(model, "b-insufficient", 140); Deadline(model); Cleanup(model);
        var result = model.FinalResult;
        Assert.That(result.TotalScore, Is.EqualTo(440));
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(result.PhaseSummaries[0].PhaseScore, Is.EqualTo(300));
        Assert.That(result.PhaseSummaries[1].PhaseScore, Is.EqualTo(140));
        Assert.That(result.PhaseSummaries[1].TotalScore, Is.EqualTo(440));
        Assert.That(result.PhaseSummaries[1].Cleared, Is.False);
    }

    [TestCase(PopupStorePhaseCleanupStatus.Completed, PopupStorePhaseEndReason.Abandoned)]
    [TestCase(PopupStorePhaseCleanupStatus.Failed, PopupStorePhaseEndReason.CleanupFailed)]
    [TestCase(PopupStorePhaseCleanupStatus.Cancelled, PopupStorePhaseEndReason.CleanupCancelled)]
    public void AbandonOrUnsuccessfulCleanupDoesNotEraseCommittedPoints(PopupStorePhaseCleanupStatus cleanup, PopupStorePhaseEndReason expected)
    {
        var model = Started(); Record(model, "sale", 180);
        Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId, cleanup)), Is.True);
        Assert.That(model.FinalResult.EndReason, Is.EqualTo(expected));
        Assert.That(model.FinalResult.TotalScore, Is.EqualTo(180));
        Assert.That(model.FinalResult.PhaseSummaries.Single().PhaseScore, Is.EqualTo(180));
        Assert.That(model.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
    }

    [Test]
    public void CancellingBeforeAnyStartHasAnImmutableZeroScoreResult()
    {
        var model = Create(); Assert.That(model.TryAbandon(), Is.True);
        Assert.That(model.FinalResult.TotalScore, Is.Zero);
        Assert.That(model.FinalResult.PhaseSummaries, Is.Empty);
        Assert.That(model.FinalResult.SuccessfulOrders, Is.Zero);
        Assert.That(model.FinalizationCount, Is.EqualTo(1));
    }

    [Test]
    public void PointsAndFinalScoreFactsExposeNoMutationMethods()
    {
        Assert.That(typeof(PopupStorePhasePackingEvidence).GetProperty("Points").GetSetMethod(true), Is.Null);
        Assert.That(typeof(PopupStorePhaseResult).GetProperty("TotalScore").GetSetMethod(true), Is.Null);
        Assert.That(typeof(PopupStorePhaseProgressSummary).GetProperty("PhaseScore").GetSetMethod(true), Is.Null);
        Assert.That(typeof(PopupStorePhaseProgressSummary).GetProperty("TotalScore").GetSetMethod(true), Is.Null);
    }

    private static PopupStorePhaseProfile Profile()
    {
        var profile = PopupStorePhaseTestProfiles.CreateDefinitionProfile();
        profile.ProfileId = "TEST_ONLY_cumulative_score";
        profile.GoalMetric = PopupStorePhaseGoalMetric.CumulativeScore;
        for (int i = 0; i < 6; i++) { profile.Phases[i].TargetScore = (i + 1) * 300; profile.Phases[i].TargetSuccessfulOrders = 0; }
        return profile;
    }
    private static PopupStorePhaseProfileSnapshot Snapshot(PopupStorePhaseProfile profile)
    {
        Assert.That(profile.TryCreateSnapshot(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(PopupStorePhaseTestProfiles.CatalogPath),
            out var snapshot, out var error), Is.True, error);
        return snapshot;
    }
    private static void Reject(PopupStorePhaseProfile profile, string reason)
    {
        Assert.That(profile.TryCreateSnapshot(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(PopupStorePhaseTestProfiles.CatalogPath),
            out var snapshot, out var error), Is.False);
        Assert.That(snapshot, Is.Null); Assert.That(error, Does.Contain(reason));
    }
    private static PopupStorePhaseSession Create(PopupStorePhaseProfile profile = null)
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-cumulative", Snapshot(profile ?? Profile()),
            out var model, out var error), Is.True, error);
        return model;
    }
    private static PopupStorePhaseSession Started(PopupStorePhaseProfile profile = null)
    { var model = Create(profile); Assert.That(model.TryStart(), Is.True); return model; }
    private static PopupStorePhasePackingEvidence Evidence(PopupStorePhaseSession model, string id, int points)
        => new PopupStorePhasePackingEvidence(model.BusinessSessionId, model.PhaseRunId, "order-" + id, "receipt-" + id, points);
    private static void Record(PopupStorePhaseSession model, string id, int points)
        => Assert.That(model.TryRecordPacking(Evidence(model, id, points)), Is.True);
    private static void Deadline(PopupStorePhaseSession model)
    { Assert.That(model.TryAdvanceTime(model.RemainingSeconds), Is.True); Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Clearing)); }
    private static void Cleanup(PopupStorePhaseSession model)
        => Assert.That(model.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(model.BusinessSessionId, model.PhaseRunId,
            PopupStorePhaseCleanupStatus.Completed)), Is.True);
    private static void Next(PopupStorePhaseSession model)
    {
        Cleanup(model); Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(model.TryAdvanceTime(model.TransitionRemainingSeconds), Is.True);
        for (int i = 3; i >= 1; i--)
        { Assert.That(model.CountdownValue, Is.EqualTo(i)); Assert.That(model.TryAdvanceTime(model.TransitionRemainingSeconds), Is.True); }
        Assert.That(model.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }
    private static string Stamp(PopupStorePhaseSession model)
        => string.Join("|", model.State, model.TotalScore, model.PhaseScore, model.TotalSuccessfulOrders,
            model.PhaseSuccessfulOrders, model.RemainingSeconds, model.PhaseRunId, model.HighestClearedGrade,
            model.FinalizationCount, model.TransitionCount);
}
#endif
