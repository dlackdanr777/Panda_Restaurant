#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Host = PopupStorePhasePreviewIntegrationTests.Host;

/// <summary>Real popup/admission/packing adapter with explicit shared-clock samples; no fake completed receipts or Play.</summary>
public sealed class PopupStorePhasePresentationTests
{
    [Test]
    public void InitialStageThreeTwoOneAndGameStartUseOneApprovalWithoutSpendingPlayTime()
    {
        using (var h = new Host(completeIntroduction: false))
        {
            Assert.That(h.Preview.Session.CompletionPolicy, Is.EqualTo(PopupStorePhaseCompletionPolicy.Deadline));
            Assert.That(h.Preview.Session.PhaseGeneration, Is.Zero);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(h.Preview, h.Clock);
            Assert.That(h.Preview.Session.PhaseGeneration, Is.EqualTo(1));
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Admission.BindingCount, Is.EqualTo(1));
            Assert.That(h.Admission.Access.Remaining, Is.Zero);
            h.AssertNoR1();
        }
    }

    [Test]
    public void SuccessNextStageCountdownAndGameStartPreserveBusinessButPrepareFreshOrders()
    {
        using (var h = new Host())
        {
            string session = h.Preview.Session.BusinessSessionId;
            h.ClearPhase(); var receipts = h.Packing.CommittedReceipts.ToArray();
            string phase = h.Preview.Session.PhaseRunId;
            h.NextPhase();
            Assert.That(h.Preview.Session.BusinessSessionId, Is.EqualTo(session));
            Assert.That(h.Preview.Session.PhaseRunId, Is.Not.EqualTo(phase));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(27));
            Assert.That(h.PlushCount, Is.EqualTo(8));
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.Zero);
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            CollectionAssert.AreEqual(receipts, h.Packing.CommittedReceipts);
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Admission.Access.Remaining, Is.Zero);
            h.AssertNoR1();
        }
    }

    [TestCase("Stage")] [TestCase("3")] [TestCase("2")] [TestCase("1")] [TestCase("GameStart")]
    public void EveryInitialCueBlocksPackingMaterialsSupplyAndPatience(string cue)
    {
        using (var h = new Host(completeIntroduction: false))
        {
            ReachCue(h, cue);
            AssertBlocked(h);
            h.Entrance.TickEntrance(1000);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Entrance.OccupiedSlotCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreCustomerPatience>().ExpiredCount, Is.Zero);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Ready));
            Assert.That(h.Preview.Session.PhaseGeneration, Is.Zero);
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            Assert.That(h.Preview.TryStart(), Is.False);
            Assert.That(h.Root.GetComponent<PopupStoreStartController>().RequestStart(), Is.False);
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            h.AssertNoR1();
        }
    }

    [TestCase("Success")] [TestCase("Stage")] [TestCase("3")]
    [TestCase("2")] [TestCase("1")] [TestCase("GameStart")]
    public void EveryBetweenStageCueBlocksNewWorkAndDoesNotChargePlayingElapsed(string cue)
    {
        using (var h = new Host())
        {
            h.ClearPhase(); double elapsed = h.Clock.ElapsedSeconds;
            ReachCue(h, cue);
            int entries = h.Entrance.StartedEntranceCount;
            int expired = h.Root.GetComponent<PopupStoreCustomerPatience>().ExpiredCount;
            AssertBlocked(h);
            h.Entrance.TickEntrance(1000);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(entries));
            Assert.That(h.Root.GetComponent<PopupStoreCustomerPatience>().ExpiredCount, Is.EqualTo(expired));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(1));
            Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
            h.AssertNoR1();
        }
    }

    [Test]
    public void InitialCountdownWaitsUntilAllFourContextPauseReasonsAreReleased()
    {
        using (var h = new Host(completeIntroduction: false))
        { ReachCue(h, "2"); AssertNestedContextPause(h); }
    }

    [Test]
    public void NextGameStartWaitsUntilAllFourContextPauseReasonsAreReleased()
    {
        using (var h = new Host())
        { h.ClearPhase(); ReachCue(h, "GameStart"); AssertNestedContextPause(h); }
    }

    [Test]
    public void GameOverStillHonorsBackgroundFocusAndExitPauseAfterTheCoreIsTerminal()
    {
        using (var h = new Host())
        {
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.IsTerminal, Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("GameOver"));
            AssertNestedContextPause(h);
            Assert.That(h.Preview.IsResultPresentationReady, Is.False);
        }
    }

    [Test]
    public void LargeSharedSamplesCannotSkipAnyInitialCueOrReduceTheFirstStage()
    {
        using (var h = new Host(completeIntroduction: false))
        {
            foreach (var cue in new[] { "Stage", "3", "2", "1", "GameStart" })
            {
                Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
                Assert.That(h.Clock.AdvanceTime(double.MaxValue), Is.True);
                Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
                Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            }
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            Assert.That(h.Preview.CanPlay, Is.True);
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.Zero);
        }
    }

    [Test]
    public void RepeatedQueriesAtTheSameClockSampleCannotAdvanceThePresentationTwice()
    {
        using (var h = new Host(completeIntroduction: false))
        {
            double now = 100;
            typeof(PopupStoreBusinessClock).GetField("_timeSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(h.Clock, new Func<double>(() => now));
            h.Clock.SynchronizeNow(); now += .2; h.Clock.SynchronizeNow();
            float progress = h.Preview.PresentationProgress;
            Assert.That(progress, Is.GreaterThan(0));
            for (int i = 0; i < 10; i++)
            {
                h.Clock.SynchronizeNow();
                Assert.That(h.Clock.CanCommitPacking, Is.False);
                Assert.That(h.Clock.CanStartCustomerEntry, Is.False);
            }
            Assert.That(h.Preview.PresentationProgress, Is.EqualTo(progress));
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Stage"));
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
        }
    }

    [Test]
    public void ASecondActualOrderBeyondCGoalStillPacksBeforeTheDeadline()
    {
        using (var h = new Host())
        {
            h.Ready(); var first = h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.CanPlay, Is.True);
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            var second = h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(second.ReceiptId, Is.Not.EqualTo(first.ReceiptId));
            Assert.That(h.Score.TotalScore, Is.EqualTo(200));
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.EqualTo(2));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Clock.AdvanceTime(h.Clock.RemainingSeconds), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Success"));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            h.AssertNoR1();
        }
    }

    [Test]
    public void SuccessCannotCoverACommittedDeliveryOrReleaseTheCustomerEarly()
    {
        using (var h = new Host())
        {
            h.Ready(); var receipt = h.Pack();
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            h.Deliver();
            Assert.That(h.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.False, "The customer still must leave and release its slot.");
            h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Success"));
            Assert.That(h.Presentation.VisualOutcomes.Single().Receipt, Is.SameAs(receipt));
            Assert.That(h.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
        }
    }

    [Test]
    public void FailedStageWaitsForItsCommittedDeliveryThenGameOverBeforeShowingTheResult()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); h.NextPhase(); h.Ready(); var receipt = h.Pack();
            Assert.That(h.Preview.Session.CurrentPhase.TargetSuccessfulOrders, Is.EqualTo(2));
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            Assert.That(h.Preview.Session.FinalResult, Is.Null);
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("GameOver"));
            Assert.That(h.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            Assert.That(h.Preview.IsResultPresentationReady, Is.False);
            h.Panel.TickResult(1000);
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Preview.TryConfirmResult(), Is.False);
            h.Clock.AdvanceTime(h.Preview.GameOverSeconds);
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            h.ShowResult();
            Assert.That(h.Panel.EditorShownPhaseResult, Is.SameAs(h.Preview.Session.FinalResult));
            Assert.That(h.Panel.EditorShownPhaseResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            h.AssertNoR1();
        }
    }

    [TestCase("Stage")] [TestCase("3")] [TestCase("2")] [TestCase("1")] [TestCase("GameStart")]
    public void AbandonDuringAnyInitialCueDoesNotStartCustomersOrInventACompletedStage(string cue)
    {
        using (var h = new Host(completeIntroduction: false))
        {
            ReachCue(h, cue);
            Assert.That(h.Preview.RequestAbandon(), Is.True);
            Assert.That(h.Preview.RequestAbandon(), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalResult.LastAttemptedPhaseNumber, Is.Zero);
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            Assert.That(h.Preview.IsResultPresentationReady, Is.True);
            h.Clock.AdvanceTime(1000); h.Entrance.TickEntrance(1000);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.AssertNoR1();
        }
    }

    [TestCase("Success")] [TestCase("Stage")] [TestCase("3")]
    [TestCase("2")] [TestCase("1")] [TestCase("GameStart")]
    public void AbandonBetweenStagesCancelsTheCueAndNeverReplaysThePreviousReceipt(string cue)
    {
        using (var h = new Host())
        {
            h.ClearPhase(); ReachCue(h, cue);
            var receipt = h.Packing.LastReceipt;
            int entries = h.Entrance.StartedEntranceCount;
            Assert.That(h.Preview.RequestAbandon(), Is.True);
            if (h.Preview.Session.IsAwaitingCleanup) Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            Assert.That(h.Preview.IsResultPresentationReady, Is.True);
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.Clock.AdvanceTime(1000); h.Entrance.TickEntrance(1000); h.Presentation.TickPresentation(1000);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(entries));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            h.AssertNoR1();
        }
    }

    [Test]
    public void FinalSssSuccessFinishesOnceWithoutStageSevenCountdownOrGameOver()
    {
        using (var h = new Host())
        {
            for (int stage = 1; stage <= 6; stage++)
            {
                h.ClearPhase();
                Assert.That(h.Preview.PresentationCue, Is.EqualTo("Success"));
                Assert.That(h.Preview.IsResultPresentationReady, Is.False);
                if (stage < 6) h.NextPhase();
            }
            Assert.That(h.Preview.Session.FinalResult, Is.Null);
            h.Clock.AdvanceTime(h.Preview.Session.CurrentPhase.StageClearNoticeSeconds);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Completed));
            Assert.That(h.Preview.PresentationCue, Is.Empty);
            Assert.That(h.Preview.IsResultPresentationReady, Is.True);
            Assert.That(h.Preview.Session.PhaseGeneration, Is.EqualTo(6));
            Assert.That(h.Preview.Session.CountdownValue, Is.Zero);
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(15));
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(15));
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(15));
            h.ShowResult();
            var result = h.Preview.Session.FinalResult;
            for (int i = 0; i < 5; i++) { h.Clock.AdvanceTime(100); h.Panel.TickResult(100); }
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(result));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Panel.PresentationCount, Is.EqualTo(1));
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            h.AssertNoR1();
        }
    }

    private static void AssertBlocked(Host h)
    {
        Assert.That(h.Preview.CanPlay || h.Preview.CanSupply || h.Clock.CanCommitPacking || h.Clock.CanStartCustomerEntry, Is.False);
        Assert.That(h.Counter.TryAdd(h.Material("STAFF01")), Is.False);
        Assert.That(h.Packing.TryPack().Success, Is.False);
        Assert.That(h.Counter.Count, Is.Zero);
        Assert.That(h.Preview.TryCreateOrder(0, out _), Is.False);
    }

    private static void ReachCue(Host h, string expected)
    {
        for (int i = 0; i < 7 && h.Preview.PresentationCue != expected; i++)
        {
            Assert.That(h.Preview.CanPlay, Is.False, "Cue was missed: " + expected);
            Assert.That(h.Clock.AdvanceTime(100), Is.True);
        }
        Assert.That(h.Preview.PresentationCue, Is.EqualTo(expected));
    }

    private static void AssertNestedContextPause(Host h)
    {
        string cue = h.Preview.PresentationCue; float progress = h.Preview.PresentationProgress;
        double elapsed = h.Clock.ElapsedSeconds;
        h.Clock.SetApplicationPaused(true); h.Clock.SetApplicationFocused(false);
        h.Clock.SetViewVisible(false); SetExitPause(h, true);
        Assert.That(h.Clock.AdvanceTime(100), Is.False);
        h.Clock.SetApplicationPaused(false); Assert.That(h.Clock.AdvanceTime(100), Is.False);
        h.Clock.SetViewVisible(true); Assert.That(h.Clock.AdvanceTime(100), Is.False);
        SetExitPause(h, false); Assert.That(h.Clock.AdvanceTime(100), Is.False);
        Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
        Assert.That(h.Preview.PresentationProgress, Is.EqualTo(progress));
        Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
        h.Clock.SetApplicationFocused(true);
        Assert.That(h.Clock.AdvanceTime(.1), Is.True);
        Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
        Assert.That(h.Preview.PresentationProgress, Is.GreaterThan(progress));
        Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
    }

    private static void SetExitPause(Host h, bool paused)
        => typeof(PopupStoreBusinessClock).GetMethod("SetExitConfirmationPaused", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(h.Clock, new object[] { paused });
}
#endif
