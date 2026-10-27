using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Pure grade boundaries and actual popup sale/deadline integration. Uses only disposable memory instances.</summary>
public sealed class PopupStoreGradeTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(0, 0, PopupStoreGradeTier.None)]
    [TestCase(100, 1, PopupStoreGradeTier.C)]
    [TestCase(699, 4, PopupStoreGradeTier.C)]
    [TestCase(700, 4, PopupStoreGradeTier.B)]
    [TestCase(1199, 7, PopupStoreGradeTier.B)]
    [TestCase(1200, 7, PopupStoreGradeTier.A)]
    [TestCase(1699, 10, PopupStoreGradeTier.A)]
    [TestCase(1700, 10, PopupStoreGradeTier.S)]
    [TestCase(2099, 12, PopupStoreGradeTier.S)]
    [TestCase(2100, 12, PopupStoreGradeTier.SS)]
    [TestCase(2399, 14, PopupStoreGradeTier.SS)]
    [TestCase(2400, 14, PopupStoreGradeTier.SSS)]
    [TestCase(2640, 20, PopupStoreGradeTier.SSS)]
    public void ExactRequiredBoundaryUsesScoreAndActualSuccessPresence(int score, int count, PopupStoreGradeTier grade)
    {
        Assert.That(PopupStoreGradeCalculator.TryEvaluate(score, count, out var result, out var error), Is.True, error);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Score, Is.EqualTo(score));
        Assert.That(result.SuccessfulOrders, Is.EqualTo(count));
        Assert.That(result.Grade, Is.EqualTo(grade));
        Assert.That(result.Progress, Is.InRange(0f, 1f));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(100, 0)]
    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(181, 1)]
    [TestCase(199, 2)]
    [TestCase(361, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void NegativeOrContradictorySnapshotsCannotBecomeAValidGrade(int score, int count)
    {
        Assert.That(PopupStoreGradeCalculator.TryEvaluate(score, count, out var result, out var error), Is.False);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Grade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(result.Progress, Is.InRange(0f, 1f));
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(100, 1, 0, 700, 100f / 700)]
    [TestCase(699, 4, 0, 700, 699f / 700)]
    [TestCase(700, 4, 700, 1200, 0f)]
    [TestCase(950, 6, 700, 1200, .5f)]
    [TestCase(1200, 7, 1200, 1700, 0f)]
    [TestCase(1450, 9, 1200, 1700, .5f)]
    [TestCase(1700, 10, 1700, 2100, 0f)]
    [TestCase(1900, 11, 1700, 2100, .5f)]
    [TestCase(2100, 12, 2100, 2400, 0f)]
    [TestCase(2250, 13, 2100, 2400, .5f)]
    [TestCase(2400, 14, 2400, 2400, 1f)]
    [TestCase(2640, 20, 2400, 2400, 1f)]
    public void ProgressUsesOnlyTheCurrentGradeIntervalAndResetsAtEachThreshold(int score, int count, int minimum, int target, float progress)
    {
        Assert.That(PopupStoreGradeCalculator.TryEvaluate(score, count, out var result, out var error), Is.True, error);
        Assert.That(result.MinimumScore, Is.EqualTo(minimum));
        Assert.That(result.NextTargetScore, Is.EqualTo(target));
        Assert.That(result.Progress, Is.EqualTo(progress).Within(.00001f));
    }

    [Test]
    public void GradeNeverFallsAsConsistentScoresRiseAndLargeValidInputsDoNotOverflow()
    {
        var previous = PopupStoreGradeTier.None;
        for (int score = 100; score <= 2640; score++)
        {
            int count = (score + 179) / 180;
            if (score < count * 100) continue;
            Assert.That(PopupStoreGradeCalculator.TryEvaluate(score, count, out var result, out var error), Is.True, error);
            Assert.That((int)result.Grade, Is.GreaterThanOrEqualTo((int)previous));
            Assert.That(float.IsNaN(result.Progress) || float.IsInfinity(result.Progress), Is.False);
            Assert.That(result.Progress, Is.InRange(0f, 1f));
            previous = result.Grade;
        }
        Assert.That(PopupStoreGradeCalculator.TryEvaluate(int.MaxValue, 20000000, out var large, out var largeError), Is.True, largeError);
        Assert.That(large.Grade, Is.EqualTo(PopupStoreGradeTier.SSS));
        Assert.That(large.Progress, Is.EqualTo(1f));
    }

    [Test]
    public void PrefabUsesOwnedGradeHudWithExistingScoreAndTimeTypographyUnchanged()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var authored = prefab.GetComponent<PopupStoreGradeSession>();
        Assert.That(authored, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreGradeSession>(true), Has.Length.EqualTo(1));
        Assert.That(Read(authored, "_score"), Is.SameAs(prefab.GetComponent<PopupStoreScore>()));
        Assert.That(Read(authored, "_clock"), Is.SameAs(prefab.GetComponent<PopupStoreBusinessClock>()));
        var scoreText = (TMP_Text)Read(prefab.GetComponent<PopupStoreScore>(), "_scoreText");
        var timeText = (TMP_Text)Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_timeText");
        using (var host = new Host())
        {
            Assert.That(host.GradeText.GetComponentInParent<UIPopupStore>(true), Is.SameAs(host.View));
            Assert.That(host.GradeText.font, Is.SameAs(scoreText.font));
            Assert.That(host.ProgressImage.GetComponentInParent<UIPopupStore>(true), Is.SameAs(host.View));
            Assert.That(host.ProgressImage.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(host.ProgressImage.raycastTarget, Is.False);
            Assert.That(host.GradeText.raycastTarget, Is.False);
            Assert.That(host.GradeText.text, Is.EqualTo("등급 —"));
            Assert.That(host.ProgressImage.fillAmount, Is.Zero);
            Assert.That(host.ScoreText.text, Is.EqualTo("점수 0"));
            Assert.That(host.ScoreText.font, Is.SameAs(scoreText.font));
            Assert.That(host.ScoreText.rectTransform.anchoredPosition, Is.EqualTo(scoreText.rectTransform.anchoredPosition));
            Assert.That(host.ScoreText.rectTransform.sizeDelta, Is.EqualTo(scoreText.rectTransform.sizeDelta));
            Assert.That(host.TimeText.font, Is.SameAs(timeText.font));
            Assert.That(host.TimeText.rectTransform.anchoredPosition, Is.EqualTo(timeText.rectTransform.anchoredPosition));
            Assert.That(host.TimeText.rectTransform.sizeDelta, Is.EqualTo(timeText.rectTransform.sizeDelta));
            Assert.That(host.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(host.Grade.FinalResult, Is.Null);
            Assert.That(host.Grade.IsFinalized, Is.False);
        }
    }

    [Test]
    public void FirstRealSaleUpdatesScoreGradeAndProgressTogetherWithoutConsumingItsReceipt()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            int feedback = host.FeedbackCount;
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(host.ScoreText.text, Is.EqualTo("점수 100"));
            Assert.That(host.GradeText.text, Is.EqualTo("등급 C"));
            Assert.That(host.Grade.Current.Score, Is.EqualTo(100));
            Assert.That(host.Grade.Current.SuccessfulOrders, Is.EqualTo(1));
            Assert.That(host.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(host.ProgressImage.fillAmount, Is.EqualTo(100f / 700).Within(.00001f));
            Assert.That(host.Grade.IsFinalized, Is.False);
            Assert.That(host.Grade.FinalizationCount, Is.Zero);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.FeedbackCount, Is.EqualTo(feedback));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(result.Receipt));
        }
    }

    [Test]
    public void NaturalDeadlineWithNoSalesFinalizesNoneExactlyOnce()
    {
        using (var host = new Host())
        {
            int notifications = 0;
            host.Grade.Finalized += _ => notifications++;
            host.StartCustomers();
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 0, 0, PopupStoreGradeTier.None);
            Assert.That(host.GradeText.text, Is.EqualTo("최종 등급 —"));
            Assert.That(host.ProgressImage.fillAmount, Is.Zero);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
        }
    }

    [Test]
    public void AZeroDurationSessionFinalizesNoneAtStartWithoutGeneratingAnOrderOrReward()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.Start(0);
            AssertFinal(host, 0, 0, PopupStoreGradeTier.None);
            Assert.That(host.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(host.Packing.CommittedReceipts, Is.Empty);
            Assert.That(host.Score.Entries, Is.Empty);
            Assert.That(host.Clock.TryStartSession(), Is.False);
            account.AssertUnchanged();
        }
        account.AssertUnchanged();
    }

    [Test]
    public void EarlierPackingObserverEndingBusinessStillIncludesTheJustCommittedReceipt()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            Invoke(host.Score, "OnDisable");
            Action<PopupStorePackingReceipt> endBeforeScoreCallback = _ => host.Clock.AdvanceTime(90);
            host.Packing.SucceededCommitted += endBeforeScoreCallback;
            Assert.That(host.Score.Refresh(), Is.True);
            host.Fill(0);
            var result = host.Packing.TryPack();
            host.Packing.SucceededCommitted -= endBeforeScoreCallback;
            Assert.That(result.Success, Is.True);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            Assert.That(host.Score.ProcessedReceiptIds.Single(), Is.EqualTo(result.Receipt.ReceiptId));
            host.CompleteVisual();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
        }
    }

    [Test]
    public void CompleteSnapshotReadDoesNotReplayAMissedReceiptButGradeRefreshSafelyReconcilesIt()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            Assert.That(host.Score.TotalScore, Is.Zero);
            Assert.That(host.Score.TryReadCompleteSnapshot(out _, out _, out var incomplete), Is.False);
            Assert.That(incomplete, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Score.TotalScore, Is.Zero, "Read-only snapshot query must not reconcile or award.");
            Assert.That(host.Score.Entries, Is.Empty);
            Assert.That(host.Grade.Refresh(), Is.True);
            Assert.That(host.Score.TryReadCompleteSnapshot(out int score, out int count, out var error), Is.True, error);
            Assert.That(score, Is.EqualTo(100));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(host.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(receipt));
        }
    }

    [TestCase("score")]
    [TestCase("grade")]
    [TestCase("both")]
    public void DisabledViewObserversStillFinalizeAuthenticPreDeadlineSalesOnTime(string disabled)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            if (disabled != "grade") { host.Score.enabled = false; Invoke(host.Score, "OnDisable"); }
            if (disabled != "score") { host.Grade.enabled = false; Invoke(host.Grade, "OnDisable"); }
            host.Fill(1);
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 140, 1, PopupStoreGradeTier.C);
            var final = host.Grade.FinalResult;
            host.Score.enabled = true;
            host.Grade.enabled = true;
            Invoke(host.Score, "OnEnable");
            Invoke(host.Grade, "OnEnable");
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(1));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(result.Receipt));
        }
    }

    [Test]
    public void GradeSubscribedLateAfterBusinessEndedRecoversTheSameReceiptWithoutAnotherTransaction()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            Invoke(host.Grade, "OnDestroy"); // Detach observers only; the test still owns the in-memory component.
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            host.Clock.AdvanceTime(90);
            Assert.That(host.Grade.FinalResult, Is.Null);
            Assert.That(host.Grade.Refresh(), Is.True);
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(receipt));
        }
    }

    [Test]
    public void BusinessEndingWhileAGiftMovesKeepsTheSameFinalResultThroughDeliveryAndSlotRelease()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(1);
            var receipt = host.Packing.TryPack().Receipt;
            host.AdvanceVisualToMove();
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 140, 1, PopupStoreGradeTier.C);
            var final = host.Grade.FinalResult;
            host.CompleteVisual(false);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.Entrance.GetState(1), Is.EqualTo(PopupStoreCustomerState.Empty));
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            AssertFinal(host, 140, 1, PopupStoreGradeTier.C);
            Assert.That(host.Score.TotalScore, Is.EqualTo(140));
        }
    }

    [Test]
    public void SeveralRealSalesImmediatelyBeforeEndAreAllIncludedAndQueuedReceiptsRemainOrdered()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var receipts = new PopupStorePackingReceipt[3];
            for (int slot = 0; slot < 3; slot++)
            {
                host.Fill(slot);
                receipts[slot] = host.Packing.TryPack().Receipt;
                Assert.That(receipts[slot], Is.Not.Null);
            }
            Assert.That(host.Score.TotalScore, Is.EqualTo(420));
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 420, 3, PopupStoreGradeTier.C);
            Assert.That(host.Grade.Current.SuccessfulOrders, Is.EqualTo(3));
            foreach (var receipt in receipts)
            {
                Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
                Assert.That(queued, Is.SameAs(receipt));
            }
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ComplaintPenaltyEndingBusinessKeepsNormalSalesAndNeverGivesAGradeForComplaintsAlone(bool priorSale)
    {
        using (var host = new Host(new[] { 18d, 60d, 60d }))
        {
            host.StartCustomers(seconds: 20);
            PopupStorePackingReceipt receipt = null;
            if (priorSale)
            {
                host.Fill(1);
                receipt = host.Packing.TryPack().Receipt;
                host.AdvanceVisualToMove();
            }
            int events = 0;
            host.Grade.Finalized += _ => events++;
            host.Clock.AdvanceTime(18);
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18d));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            AssertFinal(host, priorSale ? 140 : 0, priorSale ? 1 : 0, priorSale ? PopupStoreGradeTier.C : PopupStoreGradeTier.None);
            Assert.That(events, Is.EqualTo(1));
            if (priorSale)
            {
                host.CompleteVisual(false);
                Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            }
            host.Entrance.TickEntrance(.6f + host.ExitDuration + .01f);
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void EndedRequestsDuplicateReceiptsAndRepeatedFinalReadsCannotAlterTheSnapshot()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            host.Clock.AdvanceTime(90);
            var final = host.Grade.FinalResult;
            int events = 0;
            host.Grade.Finalized += _ => events++;
            host.Fill(1);
            var counter = host.Counter.Materials.ToArray();
            for (int i = 0; i < 5; i++)
            {
                Assert.That(host.Packing.TryPack().Success, Is.False);
                Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
                Assert.That(host.Grade.Refresh(), Is.True);
                ((Action)Read(host.Clock, "Ended"))?.Invoke();
                Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            }
            Assert.That(events, Is.Zero);
            CollectionAssert.AreEqual(counter, host.Counter.Materials);
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(receipt));
            foreach (var property in typeof(PopupStoreGradeResult).GetProperties()) Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
        }
    }

    [TestCase("queued")]
    [TestCase("moving")]
    [TestCase("delivered")]
    public void FinalResultAndInstanceIdentitySurviveCloseReentryWithoutResurrectingCompletedOrders(string phase)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            if (phase == "moving") host.AdvanceVisualToMove();
            if (phase == "delivered") host.CompleteVisual();
            host.Clock.AdvanceTime(90);
            var final = host.Grade.FinalResult;
            string session = host.Grade.SessionId;
            int started = host.Presentation.StartedPresentationCount;
            for (int i = 0; i < 3; i++) { host.Close(); host.Reopen(); }
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Grade.SessionId, Is.EqualTo(session));
            Assert.That(host.GradeText.text, Is.EqualTo("최종 등급 C"));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(started));
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
        }
    }

    [Test]
    public void DirectPopupDisableSettlesTheFinalClockSampleAndRetainsItsFinalSnapshot()
    {
        using (var host = new Host())
        {
            double now = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.StartCustomers();
            host.Fill(0);
            Assert.That(host.Packing.TryPack().Success, Is.True);
            now = 90;
            host.Root.SetActive(false);
            Invoke(host.Score, "OnDisable");
            Invoke(host.Grade, "OnDisable");
            Invoke(host.Clock, "OnDisable");
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            var final = host.Grade.FinalResult;
            host.Reopen();
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
        }
    }

    [Test]
    public void StateChangedClosingThePopupBeforeEndedPublicationStillCapturesEveryCommittedPoint()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            Assert.That(host.Score.TotalScore, Is.Zero);
            bool closedBeforeFinal = false;
            int finalEvents = 0;
            host.Clock.StateChanged += () =>
            {
                if (host.Clock.State != PopupStoreBusinessState.Ended) return;
                Assert.That(host.Grade.FinalResult, Is.Null);
                host.Close();
                closedBeforeFinal = !host.Root.activeSelf;
            };
            host.Grade.Finalized += _ => { Assert.That(closedBeforeFinal, Is.True); finalEvents++; };
            host.Clock.AdvanceTime(90);
            Assert.That(closedBeforeFinal, Is.True);
            Assert.That(finalEvents, Is.EqualTo(1));
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            Assert.That(host.Score.ProcessedReceiptIds.Single(), Is.EqualTo(receipt.ReceiptId));
            var final = host.Grade.FinalResult;
            host.Reopen();
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void FinalizedObserverExceptionsAndReentrantRefreshCannotUndoOrDuplicateFinalization()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            Assert.That(host.Packing.TryPack().Success, Is.True);
            int laterObservers = 0;
            host.Grade.Finalized += _ => throw new InvalidOperationException("grade-test-observer-failure");
            host.Grade.Finalized += result =>
            {
                laterObservers++;
                host.Grade.Refresh();
                host.Score.Refresh();
                Assert.That(host.Grade.FinalResult, Is.SameAs(result));
            };
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: grade-test-observer-failure"));
            host.Clock.AdvanceTime(90);
            Assert.That(laterObservers, Is.EqualTo(1));
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
        }
    }

    [Test]
    public void FreshPopupHasIndependentSessionIdentityAndNoPreviousFinalResultOrAccountChanges()
    {
        var account = new AccountSnapshot();
        using (var old = new Host())
        {
            old.StartCustomers();
            old.Fill(0);
            Assert.That(old.Packing.TryPack().Success, Is.True);
            old.Clock.AdvanceTime(90);
            using (var fresh = new Host())
            {
                Assert.That(fresh.Grade.SessionId, Is.Not.Null.And.Not.Empty);
                Assert.That(fresh.Grade.SessionId, Is.Not.EqualTo(old.Grade.SessionId));
                Assert.That(fresh.Grade.FinalResult, Is.Null);
                Assert.That(fresh.Grade.FinalizationCount, Is.Zero);
                Assert.That(fresh.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.None));
                Assert.That(fresh.Score.TotalScore, Is.Zero);
                AssertFinal(old, 100, 1, PopupStoreGradeTier.C);
                account.AssertUnchanged();
            }
        }
        account.AssertUnchanged();
    }

    [Test]
    public void RecoveringSeveralMissingScoresAtDeadlineCannotFinalizeTheFirstPartialReplay()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            for (int slot = 0; slot < 3; slot++)
            {
                host.Fill(slot);
                Assert.That(host.Packing.TryPack().Success, Is.True);
            }
            Assert.That(host.Score.TotalScore, Is.Zero);
            Assert.That(host.Packing.CommittedReceipts.Count, Is.EqualTo(3));
            int events = 0, publishedScore = -1, publishedCount = -1;
            host.Grade.Finalized += result => { events++; publishedScore = result.Score; publishedCount = result.SuccessfulOrders; };
            host.Clock.AdvanceTime(90);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(publishedScore, Is.EqualTo(420));
            Assert.That(publishedCount, Is.EqualTo(3));
            AssertFinal(host, 420, 3, PopupStoreGradeTier.C);
            Assert.That(host.Score.Entries.Count, Is.EqualTo(3));
        }
    }

    [TestCase("score")]
    [TestCase("clock")]
    public void ForeignDataSourcesCannotCrossFinalizeAnotherPopup(string source)
    {
        using (var owner = new Host())
        using (var other = new Host())
        {
            string field = source == "score" ? "_score" : "_clock";
            object original = Read(owner.Grade, field);
            Write(owner.Grade, field, source == "score" ? (object)other.Score : other.Clock);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreGradeSession: result not finalized\\. Score and business clock must belong to this popup\\."));
            Assert.That(owner.Grade.Refresh(), Is.False);
            Assert.That(owner.Grade.Current.IsValid, Is.False);
            Assert.That(owner.Grade.FinalResult, Is.Null);
            other.Start(0);
            AssertFinal(other, 0, 0, PopupStoreGradeTier.None);
            Assert.That(owner.Grade.FinalResult, Is.Null);
            Write(owner.Grade, field, original);
            Assert.That(owner.Grade.Refresh(), Is.True);
            Assert.That(owner.Grade.Current.IsValid, Is.True);
            Assert.That(owner.Grade.FinalResult, Is.Null);
        }
    }

    [TestCase("total")]
    [TestCase("receipt")]
    public void AContradictoryLiveLedgerCannotBeFrozenAsAValidGrade(string corruption)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            string box = receipt.BoxId;
            if (corruption == "total") Write(host.Score, "<TotalScore>k__BackingField", 140);
            else Write(receipt, "<BoxId>k__BackingField", "BOX_BLUE");
            Assert.That(host.Score.TryReadCompleteSnapshot(out _, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreGradeSession: result not finalized\\."));
            host.Clock.AdvanceTime(90);
            Assert.That(host.Grade.FinalResult, Is.Null);
            Assert.That(host.Grade.IsFinalized, Is.False);
            Assert.That(host.Grade.FinalizationCount, Is.Zero);
            Assert.That(host.Grade.Current.IsValid, Is.False);
            Assert.That(host.GradeText.text, Is.EqualTo("등급 확인 불가"));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            // Restore only the injected in-memory fault, then verify recovery does not replay the sale.
            Write(host.Score, "<TotalScore>k__BackingField", 100);
            Write(receipt, "<BoxId>k__BackingField", box);
            Assert.That(host.Grade.Refresh(), Is.True);
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(receipt));
        }
    }

    [TestCase("missing-text")]
    [TestCase("missing-progress")]
    [TestCase("bad-format")]
    public void BrokenGradeHudCannotPreventScoreFinalizationOrOriginalDelivery(string defect)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var text = host.GradeText;
            var fill = host.ProgressImage;
            if (defect == "missing-text") Write(host.Grade, "_gradeText", null);
            else if (defect == "missing-progress") Write(host.Grade, "_progressImage", null);
            else
            {
                Write(host.Grade, "_liveFormat", "등급 {");
                Write(host.Grade, "_finalFormat", "등급 {");
            }
            host.Fill(0);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreGradeSession: HUD update failed; result data is preserved\\."));
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            Assert.That(host.ScoreText.text, Is.EqualTo("점수 100"));
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 100, 1, PopupStoreGradeTier.C);
            host.CompleteVisual();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Write(host.Grade, "_gradeText", text);
            Write(host.Grade, "_progressImage", fill);
            Write(host.Grade, "_liveFormat", "등급 {0}");
            Write(host.Grade, "_finalFormat", "최종 등급 {0}");
            host.Grade.RefreshHud();
            Assert.That(text.text, Is.EqualTo("최종 등급 C"));
            Assert.That(fill.fillAmount, Is.EqualTo(100f / 700).Within(.00001f));
        }
    }

    [Test]
    public void ForeignProgressImageIsNotChangedAndTheLocalResultStillFinalizes()
    {
        using (var owner = new Host())
        using (var other = new Host())
        {
            owner.StartCustomers();
            var ownFill = owner.ProgressImage;
            var foreignFill = other.ProgressImage;
            float foreignValue = foreignFill.fillAmount;
            bool foreignEnabled = foreignFill.enabled;
            Write(owner.Grade, "_progressImage", foreignFill);
            owner.Fill(0);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreGradeSession: HUD update failed; result data is preserved\\."));
            Assert.That(owner.Packing.TryPack().Success, Is.True);
            owner.Clock.AdvanceTime(90);
            AssertFinal(owner, 100, 1, PopupStoreGradeTier.C);
            Assert.That(foreignFill.fillAmount, Is.EqualTo(foreignValue));
            Assert.That(foreignFill.enabled, Is.EqualTo(foreignEnabled));
            Assert.That(other.Grade.FinalResult, Is.Null);
            Assert.That(other.Score.TotalScore, Is.Zero);
            Write(owner.Grade, "_progressImage", ownFill);
            owner.Grade.RefreshHud();
            Assert.That(ownFill.fillAmount, Is.EqualTo(100f / 700).Within(.00001f));
        }
    }

    [Test]
    public void InspectorVisibilityAndTextFormattingDoNotControlTheResultOrExistingHudLayout()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var gradeFont = host.GradeText.font;
            var gradePosition = host.GradeText.rectTransform.anchoredPosition;
            var gradeSize = host.GradeText.rectTransform.sizeDelta;
            var scorePosition = host.ScoreText.rectTransform.anchoredPosition;
            var timePosition = host.TimeText.rectTransform.anchoredPosition;
            Write(host.Grade, "_showHud", false);
            host.Grade.RefreshHud();
            Assert.That(host.GradeText.enabled, Is.False);
            Assert.That(host.ProgressImage.enabled, Is.False);
            host.Fill(1);
            Assert.That(host.Packing.TryPack().Success, Is.True);
            host.Clock.AdvanceTime(90);
            var final = host.Grade.FinalResult;
            AssertFinal(host, 140, 1, PopupStoreGradeTier.C);
            Write(host.Grade, "_showHud", true);
            Write(host.Grade, "_showProgress", false);
            Write(host.Grade, "_finalFormat", "완료 등급 [{0}]");
            host.Grade.RefreshHud();
            Assert.That(host.GradeText.enabled, Is.True);
            Assert.That(host.ProgressImage.enabled, Is.False);
            Assert.That(host.GradeText.text, Is.EqualTo("완료 등급 [C]"));
            Write(host.Grade, "_showProgress", true);
            host.Grade.RefreshHud();
            Assert.That(host.ProgressImage.enabled, Is.True);
            Assert.That(host.ProgressImage.fillAmount, Is.EqualTo(.2f).Within(.00001f));
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.GradeText.font, Is.SameAs(gradeFont));
            Assert.That(host.GradeText.rectTransform.anchoredPosition, Is.EqualTo(gradePosition));
            Assert.That(host.GradeText.rectTransform.sizeDelta, Is.EqualTo(gradeSize));
            Assert.That(host.ScoreText.rectTransform.anchoredPosition, Is.EqualTo(scorePosition));
            Assert.That(host.TimeText.rectTransform.anchoredPosition, Is.EqualTo(timePosition));
        }
    }

    [Test]
    public void AllTwentyActualOrdersReach2640AndSSSBeforeOneImmutableFinalization()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            int finalized = 0;
            host.Grade.Finalized += _ => finalized++;
            host.Start();
            int safety = 0;
            var lastGrade = PopupStoreGradeTier.None;
            while (host.Entrance.ReleasedCount < 20 && safety++ < 2000)
            {
                host.Entrance.TickEntrance(.5f);
                int slot = Enumerable.Range(0, 3).Where(host.Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                host.Fill(slot);
                var result = host.Packing.TryPack();
                Assert.That(result.Success, Is.True);
                Assert.That(host.Grade.Current.Score, Is.EqualTo(host.Score.TotalScore));
                Assert.That(host.Grade.Current.SuccessfulOrders, Is.EqualTo(host.Score.SuccessfulOrderCount));
                Assert.That((int)host.Grade.Current.Grade, Is.GreaterThanOrEqualTo((int)lastGrade));
                Assert.That(host.ProgressImage.fillAmount, Is.InRange(0f, 1f));
                Assert.That(host.Grade.IsFinalized, Is.False);
                lastGrade = host.Grade.Current.Grade;
                host.CompleteVisual();
                Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
                host.Entrance.TickEntrance(host.ExitDuration + .01f);
            }
            Assert.That(safety, Is.LessThan(2000));
            Assert.That(host.Score.TotalScore, Is.EqualTo(2640));
            CollectionAssert.AreEqual(new[] { 8, 8, 4 }, Enumerable.Range(1, 3).Select(host.Score.GetSuccessfulCount));
            Assert.That(host.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.SSS));
            Assert.That(host.GradeText.text, Is.EqualTo("등급 SSS"));
            Assert.That(host.ProgressImage.fillAmount, Is.EqualTo(1));
            Assert.That(finalized, Is.Zero);
            host.Clock.AdvanceTime(90);
            AssertFinal(host, 2640, 20, PopupStoreGradeTier.SSS);
            Assert.That(host.GradeText.text, Is.EqualTo("최종 등급 SSS"));
            Assert.That(finalized, Is.EqualTo(1));
            Assert.That(host.Entrance.CompletedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.RemainingOrderCount, Is.Zero);
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(20));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            account.AssertUnchanged();
        }
        account.AssertUnchanged();
    }

    private static void AssertFinal(Host host, int score, int count, PopupStoreGradeTier grade)
    {
        Assert.That(host.Grade.IsFinalized, Is.True);
        Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        var result = host.Grade.FinalResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsFinalized, Is.True);
        Assert.That(result.SessionId, Is.EqualTo(host.Grade.SessionId));
        Assert.That(result.Score, Is.EqualTo(score));
        Assert.That(result.SuccessfulOrders, Is.EqualTo(count));
        Assert.That(result.Grade, Is.EqualTo(grade));
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress OrderProgress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly PopupStoreCustomerPatience Patience;
        internal readonly PopupStoreScore Score;
        internal readonly PopupStoreGradeSession Grade;
        internal int FeedbackCount;
        internal TMP_Text GradeText => (TMP_Text)Read(Grade, "_gradeText");
        internal Image ProgressImage => (Image)Read(Grade, "_progressImage");
        internal TMP_Text ScoreText => (TMP_Text)Read(Score, "_scoreText");
        internal TMP_Text TimeText => (TMP_Text)Read(Clock, "_timeText");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float EntryDuration => (float)Read(Entrance, "_entryDuration");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");

        internal Host(double[] durations = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-grade-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                OrderProgress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Score = Root.GetComponent<PopupStoreScore>();
                Grade = Root.GetComponent<PopupStoreGradeSession>();
                Assert.That(Grade, Is.Not.Null);
                Write(Clock, "_autoStartForEditorReview", false);
                if (durations != null)
                {
                    Write(Patience, "_onePlushSeconds", durations[0]);
                    Write(Patience, "_twoPlushSeconds", durations[1]);
                    Write(Patience, "_threePlushSeconds", durations[2]);
                }
                View.Init();
                View.Show();
                RefreshExisting();
                Assert.That(Entrance.BeginVisibleSession(), Is.True);
                Assert.That(Presentation.BeginVisibleSession(), Is.True);
                Assert.That(Patience.Refresh(), Is.True);
                Assert.That(Score.Refresh(), Is.True);
                Assert.That(Grade.Refresh(), Is.True);
                OrderProgress.FeedbackTriggered += CountFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Start(double seconds = 90)
        {
            Write(Clock, "_initialSeconds", seconds);
            Assert.That(Clock.TryStartSession(), Is.True);
        }
        internal void StartCustomers(int count = 3, double seconds = 90)
        {
            Start(seconds);
            Entrance.TickEntrance(0);
            for (int i = 1; i < count; i++) Entrance.TickEntrance(4);
            Entrance.TickEntrance(EntryDuration + .01f);
            for (int i = 0; i < count; i++) Assert.That(Entrance.IsWaiting(i), Is.True, "arrival " + i);
        }
        internal void Fill(int slot)
        {
            Assert.That(SourceOrders[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            foreach (var material in materials) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal void AdvanceVisualToMove()
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 1000 && Presentation.CurrentPhase.ToString() != "Move"; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo("Move"));
        }
        internal void CompleteVisual(bool begin = true)
        {
            if (begin) Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != "Idle"; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            View.Hide();
            Entrance.PauseVisibleSession();
            Presentation.EndVisibleSession();
            Invoke(Patience, "OnDisable");
            Invoke(Packing, "OnDisable");
            Invoke(OrderProgress, "OnDisable");
            Invoke(Counter, "OnDisable");
            Invoke(Orders, "OnDisable");
            Invoke(Score, "OnDisable");
            Invoke(Grade, "OnDisable");
        }
        internal void Reopen()
        {
            View.Show();
            RefreshExisting();
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Patience.Refresh(), Is.True);
            Assert.That(Score.Refresh(), Is.True);
            Assert.That(Grade.Refresh(), Is.True);
        }
        private void RefreshExisting()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(OrderProgress.Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
        }
        private void CountFeedback() => FeedbackCount++;
        public void Dispose()
        {
            if (OrderProgress != null) OrderProgress.FeedbackTriggered -= CountFeedback;
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null)
            {
                if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                Presentation.EndVisibleSession();
            }
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private readonly float _timeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(_score));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(target, null);
    }
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
