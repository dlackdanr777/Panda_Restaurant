#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Original prefab, real local transactions, independent old/new object graphs.
/// No Play transition, scene save, account writes, SDK request or reward/start authority.</summary>
public sealed class PopupStoreSessionLifetimeTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private string _account;
    private bool _sdk;
    private BackendManager _backend;
    private float _timeScale;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void CaptureExistingMemoryOnly()
    {
        _account = Account(); _sdk = BackEnd.Backend.IsInitialized; _backend = BackendManager.ExistingInstance;
        _timeScale = Time.timeScale; _random = UnityEngine.Random.state;
    }
    [TearDown]
    public void SessionReplacementNeverChangesRealCurrencyDailyUseSdkOrGlobalTime()
    {
        Assert.That(Account(), Is.EqualTo(_account)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void AuthoredPrefabHasOneLifetimeAndUsesItsExistingGradeSessionIdentity()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab.GetComponents<PopupStoreSessionLifetime>(), Has.Length.EqualTo(1));
        using (var h = new Host())
        {
            Assert.That(h.Current.Life.SessionId, Is.EqualTo(h.Current.Grade.SessionId));
            Assert.That(h.Current.Life.IsRetired, Is.False); Assert.That(h.Current.Clock.EndReason, Is.Null);
        }
    }

    [Test]
    public void AbandonButtonBecomesAvailableOnlyInsideTheRunningBusinessConfirmation()
    {
        using (var h = new Host())
        {
            var g = h.Current; Assert.That(g.Exit.CanConfirmAbandon, Is.False);
            g.StartThree(); Assert.That(g.Exit.CanConfirmAbandon, Is.False); g.Request();
            Assert.That(g.Exit.CanConfirmAbandon, Is.True); Assert.That(g.AbandonButton.IsInteractable(), Is.True);
            Assert.That(g.Exit.TryContinue(), Is.True); Assert.That(g.Exit.CanConfirmAbandon, Is.False);
        }
    }

    [Test]
    public void AbandonWithoutConfirmationCannotChangeARunningBusiness()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Fill(0); double time = g.Clock.RemainingSeconds;
            Assert.That(g.Exit.TryConfirmAbandon(), Is.False); Assert.That(g.Clock.AbandonCount, Is.Zero);
            Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running)); Assert.That(g.Counter.Count, Is.EqualTo(3));
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(time)); Assert.That(h.Navigation.FirstView, Is.SameAs(g.View));
        }
    }

    [Test]
    public void NotStartedClockRejectsAbandonAndNormalCloseDoesNotSpendOrFinalize()
    {
        using (var h = new Host())
        {
            var g = h.Current; Assert.That(AbandonClock(g.Clock), Is.False); Assert.That(g.View.TryClose(), Is.True);
            Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted)); Assert.That(g.Clock.AbandonCount, Is.Zero);
            Assert.That(g.Clock.EndReason, Is.Null); Assert.That(g.Grade.FinalizationCount, Is.Zero); Assert.That(g.Reward.RandomDrawCount, Is.Zero);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
        }
    }

    [TestCase("running")]
    [TestCase("confirmation")]
    [TestCase("focus")]
    public void ClockAbandonIsOneTerminalTransitionWithoutEndedEventOrTimeConsumption(string state)
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Clock.AdvanceTime(2);
            if (state == "confirmation") g.Request(); else if (state == "focus") g.Clock.SetApplicationFocused(false);
            double remaining = g.Clock.RemainingSeconds, elapsed = g.Clock.ElapsedSeconds; int ended = 0;
            g.Clock.Ended += () => ended++;
            Assert.That(AbandonClock(g.Clock), Is.True); Assert.That(AbandonClock(g.Clock), Is.False);
            Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned)); Assert.That(g.Clock.AbandonCount, Is.EqualTo(1));
            Assert.That(g.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.DirectExit));
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(g.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(g.Clock.EndedCount, Is.Zero); Assert.That(ended, Is.Zero); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void AuthoredAbandonButtonClosesToTheExistingMapExactlyOnce()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Request(); var click = g.AbandonButton.onClick; int before = h.Navigation.Count;
            for (int i = 0; i < 12; i++) click.Invoke();
            Assert.That(g.Clock.AbandonCount, Is.EqualTo(1)); Assert.That(g.Exit.AbandonCount, Is.EqualTo(1));
            Assert.That(h.Navigation.Count, Is.EqualTo(before - 1)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            Assert.That(g.Root.activeSelf, Is.False); Assert.That(g.Life.IsRetired, Is.True); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void RepeatedAbandonApiAndCloseRequestsCannotPopTheMapOrPrepareMoreSessions()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); h.Abandon(); int count = h.Navigation.Count;
            for (int i = 0; i < 8; i++)
            {
                Assert.That(g.Exit.TryConfirmAbandon(), Is.False); Assert.That(g.View.TryClose(), Is.False);
                Assert.That(g.View.TryRequestExit(), Is.False);
            }
            Assert.That(h.Navigation.Count, Is.EqualTo(count)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            Assert.That(g.Clock.AbandonCount, Is.EqualTo(1)); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void TerminalStateObserverCannotCommitAPackDuringAbandonNotification()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Fill(0); g.Request(); int attempts = 0, successes = 0;
            Action observer = () => { if (g.Clock.State == PopupStoreBusinessState.Abandoned) { attempts++; if (g.Packing.TryPack().Success) successes++; } };
            g.Clock.StateChanged += observer;
            try { Assert.That(g.Exit.TryConfirmAbandon(), Is.True); }
            finally { g.Clock.StateChanged -= observer; }
            Assert.That(attempts, Is.EqualTo(1)); Assert.That(successes, Is.Zero); Assert.That(g.Packing.CommittedReceipts, Is.Empty);
            Assert.That(g.Score.TotalScore, Is.Zero); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void AbandonKeepsAlreadyCommittedSaleHistoryWithoutFinalizingOrRollingReward()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); var receipt = g.Pack(0); g.CompleteDelivery();
            var records = g.Packing.CommittedReceipts.ToArray(); int score = g.Score.TotalScore; h.Abandon();
            Assert.That(score, Is.EqualTo(100)); Assert.That(g.Score.TotalScore, Is.EqualTo(score));
            CollectionAssert.AreEqual(records, g.Packing.CommittedReceipts.ToArray());
            Assert.That(g.Packing.IsCommittedReceipt(receipt), Is.True); Assert.That(g.Score.TryRecordReceipt(receipt), Is.False);
            Assert.That(g.Packing.SuccessfulPackCount, Is.EqualTo(1)); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void ContinuePreservesSessionMaterialsAndReceiptWithoutAnyAbandonment()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); var receipt = g.Pack(0); g.ToPhase("Move");
            string id = g.Life.SessionId; double remaining = g.Clock.RemainingSeconds; g.Fill(1); var materials = g.Counter.Materials.ToArray();
            g.Request(); g.Clock.AdvanceTime(999); g.Presentation.TickPresentation(999); Assert.That(g.Exit.TryContinue(), Is.True);
            Assert.That(g.Life.IsRetired, Is.False); Assert.That(g.Life.SessionId, Is.EqualTo(id)); Assert.That(g.Clock.AbandonCount, Is.Zero);
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(remaining)); CollectionAssert.AreEqual(materials, g.Counter.Materials.ToArray());
            Assert.That(g.Presentation.CurrentReceipt, Is.SameAs(receipt)); g.CompleteDelivery(); Assert.That(g.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void LateContinueCannotAcceptAnAbandonedSessionEvenBeforeItsOverlayIsDismissed()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Fill(0); g.Request();
            var materials = g.Counter.Materials.ToArray(); double remaining = g.Clock.RemainingSeconds;
            Assert.That(AbandonClock(g.Clock), Is.True); Assert.That(g.Exit.IsVisible, Is.True);
            Assert.That(g.Exit.TryContinue(), Is.False); Assert.That(g.Exit.ContinueCount, Is.Zero);
            Assert.That(g.Exit.IsVisible, Is.True); Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned));
            Assert.That(g.Clock.AbandonCount, Is.EqualTo(1)); Assert.That(g.Clock.CanAdvanceVisuals, Is.False);
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(remaining));
            CollectionAssert.AreEqual(materials, g.Counter.Materials.ToArray()); AssertNoAbandonReward(g);
        }
    }

    [TestCase("focus")]
    [TestCase("application")]
    [TestCase("view")]
    public void RestoringAnyContextCannotRestartAnAbandonedClock(string reason)
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Request(); Assert.That(AbandonClock(g.Clock), Is.True);
            double remaining = g.Clock.RemainingSeconds;
            Condition(g.Clock, reason, true); Condition(g.Clock, reason, false);
            g.Clock.SetManualPaused(false); Invoke(g.Clock, "SetExitConfirmationPaused", false);
            Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned)); Assert.That(g.Clock.CanAdvanceVisuals, Is.False);
            Assert.That(g.Clock.CanStartCustomerEntry, Is.False); Assert.That(g.Clock.CanCommitPacking, Is.False);
            Assert.That(g.Clock.AdvanceTime(999), Is.False); Assert.That(g.Clock.TryStartSession(), Is.False);
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(remaining)); AssertNoAbandonReward(g);
        }
    }

    [TestCase(.001d)]
    [TestCase(1d)]
    [TestCase(90d)]
    public void AbandonPreservesPositiveRemainingTimeInsteadOfManufacturingANormalDeadline(double remaining)
    {
        using (var h = new Host())
        {
            var g = h.Current; g.Start(); g.Clock.AdvanceTime(90 - remaining); h.Abandon();
            Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(remaining).Within(.000001));
            Assert.That(g.Clock.EndedCount, Is.Zero); Assert.That(g.Clock.AbandonCount, Is.EqualTo(1)); AssertNoAbandonReward(g);
        }
    }

    [Test]
    public void ClockDeadlineSettledBeforeAbandonWinsAndCannotBeRelabeledDirectExit()
    {
        using (var h = new Host())
        {
            var g = h.Current; double now = 0; Write(g.Clock, "_timeSource", new Func<double>(() => now)); g.Start(); now = 91;
            Assert.That(AbandonClock(g.Clock), Is.False); Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(g.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.Completed)); Assert.That(g.Clock.AbandonCount, Is.Zero);
            Assert.That(g.Clock.EndedCount, Is.EqualTo(1)); Assert.That(g.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(g.Grade.FinalResult, Is.Not.Null); Assert.That(g.Exit.TryConfirmAbandon(), Is.False);
        }
    }

    [Test]
    public void EndedBackRequestNeverTurnsNormalCompletionIntoAbandonment()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Pack(0); g.CompleteDelivery(); g.End(); var final = g.Grade.FinalResult;
            Assert.That(g.View.TryRequestExit(), Is.True); Assert.That(g.Clock.AbandonCount, Is.Zero);
            Assert.That(g.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.Completed)); Assert.That(g.Grade.FinalResult, Is.SameAs(final));
            Assert.That(g.Grade.FinalizationCount, Is.EqualTo(1)); Assert.That(g.Reward.RandomDrawCount, Is.EqualTo(1));
            Assert.That(g.Life.IsRetired, Is.True); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void AbandonDuringEachDeliveryPhaseArchivesOnlyTheOldReceiptAndNeverContinuesItsAnimation(string phase)
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); var receipt = old.Pack(0); old.ToPhase(phase); h.Abandon();
            old.Presentation.TickPresentation(999); old.Entrance.TickEntrance(999);
            Assert.That(old.Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
            Assert.That(old.Presentation.CurrentReceipt, Is.Null); Assert.That(old.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(old.Presentation.VisualOutcomes.Count(x => ReferenceEquals(x.Receipt, receipt)), Is.EqualTo(1));
            Assert.That(old.Presentation.VisualOutcomes.Single(x => ReferenceEquals(x.Receipt, receipt)).Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            AssertNoAbandonReward(old); var fresh = h.Reenter(); fresh.Start(); AssertFreshRunning(fresh);
            Assert.That(fresh.Score.TryRecordReceipt(receipt), Is.False); Assert.That(fresh.Presentation.ReceivedReceipts, Is.Empty);
        }
    }

    [Test]
    public void AbandonBeforeFirstVisualTickCancelsQueuedPresentationWithoutUndoingItsSale()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); var receipt = old.Pack(0);
            Assert.That(old.Queue, Has.Count.EqualTo(1)); h.Abandon();
            Assert.That(old.Queue, Is.Empty); Assert.That(old.Score.TotalScore, Is.EqualTo(100));
            Assert.That(old.Presentation.VisualOutcomes.Single().Receipt, Is.SameAs(receipt));
            Assert.That(old.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            AssertNoAbandonReward(old); var fresh = h.Reenter(); fresh.Start(); AssertFreshRunning(fresh);
        }
    }

    [TestCase("entering")]
    [TestCase("waiting")]
    [TestCase("complaint")]
    [TestCase("leaving")]
    public void NewBusinessNeverInheritsOldCustomerSlotMotionTimersOrSupplyHistory(string phase)
    {
        using (var h = new Host())
        {
            var old = h.Current;
            if (phase == "entering") { old.Start(); old.Entrance.TickEntrance(.1f); }
            else
            {
                old.StartThree();
                if (phase == "complaint") old.Clock.AdvanceTime(18);
                else if (phase == "leaving") { old.Pack(0); old.CompleteVisual(); }
            }
            string[] ids = old.Entrance.SuppliedOrders.Select(o => o.RuntimeId).ToArray(); h.Abandon();
            var fresh = h.Reenter(); fresh.Start(); AssertFreshRunning(fresh);
            fresh.Entrance.TickEntrance(0); fresh.Entrance.TickEntrance((float)Read(fresh.Entrance, "_entryDuration") + .01f);
            Assert.That(fresh.Entrance.SuppliedOrderCount, Is.EqualTo(1)); Assert.That(fresh.Entrance.IsWaiting(0), Is.True);
            Assert.That(ids, Does.Not.Contain(fresh.OrdersData[0].RuntimeId));
            Assert.That(fresh.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(18d)); Assert.That(fresh.Clock.ComplaintPenaltyCount, Is.Zero);
        }
    }

    [Test]
    public void ReentryAfterAbandonCreatesNewIdentityEmptyCounterAndIndependentWholeGraph()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); old.Fill(0); string oldId = old.Grade.SessionId;
            int[] instanceIds = Components(old).Select(c => c.GetInstanceID()).ToArray(); h.Abandon();
            var fresh = h.Reenter(); Assert.That(fresh.Grade.SessionId, Is.Not.EqualTo(oldId));
            Assert.That(fresh.Life.SessionId, Is.EqualTo(fresh.Grade.SessionId));
            CollectionAssert.AreNotEquivalent(instanceIds, Components(fresh).Select(c => c.GetInstanceID()).ToArray());
            Assert.That(Components(fresh).All(c => !instanceIds.Contains(c.GetInstanceID())), Is.True);
            fresh.Start(); AssertFreshRunning(fresh);
        }
    }

    [Test]
    public void ExistingMapEntryUpdatesItsSerializedReferenceToTheNewRegisteredView()
    {
        using (var h = new Host())
        {
            var old = h.Current; string id = old.Grade.SessionId; old.StartThree(); h.Abandon(); var fresh = h.Reenter();
            Assert.That(Read(h.Floor, "_popupStoreView"), Is.SameAs(fresh.View));
            Assert.That(h.Navigation.FirstView, Is.SameAs(fresh.View)); Assert.That(fresh.Grade.SessionId, Is.Not.EqualTo(id));
            int count = h.Navigation.Count; h.Entry.onClick.Invoke();
            Assert.That(h.Navigation.Count, Is.EqualTo(count)); Assert.That(h.Navigation.FirstView, Is.SameAs(fresh.View));
        }
    }

    [Test]
    public void FreshGraphIsPreparedWithoutStartingAnUnapprovedPlayerBusiness()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); h.Abandon();
            Assert.That(old.Life.TryPrepareForEntry(out var view), Is.True); h.Track(view); var fresh = new Graph(view);
            Assert.That(fresh.Root.activeSelf, Is.False); Assert.That(fresh.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(fresh.Grade.FinalizationCount, Is.Zero); Assert.That(fresh.Reward.RandomDrawCount, Is.Zero);
            Assert.That(fresh.Score.TotalScore, Is.Zero); Assert.That(fresh.Counter.Count, Is.Zero);
        }
    }

    [Test]
    public void ActiveUnretiredSessionCannotBeReplacedByPreparingEntryAgain()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Fill(0); string id = g.Life.SessionId; int count = h.Navigation.Count;
            bool accepted = g.Life.TryPrepareForEntry(out var same);
            if (accepted) Assert.That(same, Is.SameAs(g.View)); else Assert.That(same == null || same == g.View, Is.True);
            Assert.That(g.Life.SessionId, Is.EqualTo(id)); Assert.That(g.Life.IsRetired, Is.False);
            Assert.That(h.Navigation.Count, Is.EqualTo(count)); Assert.That(g.Counter.Count, Is.EqualTo(3));
        }
    }

    [Test]
    public void ForcedHideOfUnfinishedSessionKeepsItsIdentityAndDoesNotMeanAbandon()
    {
        using (var h = new Host())
        {
            var g = h.Current; g.StartThree(); g.Fill(0); string id = g.Life.SessionId; h.Navigation.Pop(); g.DisableServices();
            Assert.That(g.Life.IsRetired, Is.False); Assert.That(g.Clock.AbandonCount, Is.Zero);
            var same = h.Reenter(); Assert.That(same.View, Is.SameAs(g.View)); Assert.That(same.Life.SessionId, Is.EqualTo(id));
            Assert.That(same.Counter.Count, Is.EqualTo(3)); Assert.That(same.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.S)]
    public void ConfirmingNormalResultRetiresItAndNextBusinessHasNoResultOrOpenedReward(PopupStoreGradeTier tier)
    {
        using (var h = new Host())
        {
            var old = h.Current; old.Start(); old.SellUntil(tier); old.End(); old.ShowResult();
            var final = old.Grade.FinalResult; var decision = old.Reward.Result; string oldId = old.Life.SessionId;
            Assert.That(final.Grade, Is.EqualTo(tier)); Assert.That(old.Reveal.TryReveal(), Is.True); old.Reveal.TickPreview(1);
            Assert.That(old.Result.TryConfirm(), Is.True); old.DisableServices();
            Assert.That(old.Life.IsRetired, Is.True); Assert.That(old.Result.ConfirmationCount, Is.EqualTo(1));
            Assert.That(old.Grade.FinalResult, Is.SameAs(final)); Assert.That(old.Reward.Result, Is.SameAs(decision));
            Assert.That(old.Grade.FinalizationCount, Is.EqualTo(1)); Assert.That(old.Reward.RandomDrawCount, Is.EqualTo(1));
            var fresh = h.Reenter(); fresh.Start(); AssertFreshRunning(fresh);
            Assert.That(fresh.Life.SessionId, Is.Not.EqualTo(oldId)); Assert.That(fresh.Reveal.OpenedResult, Is.Null);
        }
    }

    [Test]
    public void OldMaterialAndAbandonButtonEventsCannotOperateOnFreshSession()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); var oldClick = old.AbandonButton.onClick;
            var oldClear = ((Button)Read(old.Counter, "_clearButton")).onClick; var oldPack = ((Button)Read(old.Packing, "_packButton")).onClick;
            h.Abandon(); var fresh = h.Reenter(); fresh.StartThree(); fresh.Fill(0); var materials = fresh.Counter.Materials.ToArray();
            oldClick.Invoke(); oldClear.Invoke(); oldPack.Invoke();
            CollectionAssert.AreEqual(materials, fresh.Counter.Materials.ToArray()); Assert.That(fresh.Clock.AbandonCount, Is.Zero);
            Assert.That(fresh.Packing.SuccessfulPackCount, Is.Zero); Assert.That(fresh.Grade.FinalizationCount, Is.Zero);
            Assert.That(h.Navigation.FirstView, Is.SameAs(fresh.View));
        }
    }

    [Test]
    public void ForeignOldReceiptAndVisualOutcomeCannotScoreOrReleaseANewCustomer()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); var receipt = old.Pack(0); old.CompleteDelivery();
            var outcome = old.Presentation.VisualOutcomes.Single(); h.Abandon(); var fresh = h.Reenter(); fresh.StartThree();
            string orderId = fresh.OrdersData[0].RuntimeId;
            Assert.That(fresh.Score.TryRecordReceipt(receipt), Is.False);
            Invoke(fresh.Entrance, "HandleVisualEnded", outcome);
            Assert.That(fresh.Entrance.IsWaiting(0), Is.True); Assert.That(fresh.Entrance.ReleasedCount, Is.Zero);
            Assert.That(fresh.Entrance.StartedDepartureCount, Is.Zero); Assert.That(fresh.OrdersData[0].RuntimeId, Is.EqualTo(orderId));
            Assert.That(fresh.Score.TotalScore, Is.Zero); Assert.That(fresh.Packing.IsCommittedReceipt(receipt), Is.False);
        }
    }

    [Test]
    public void ForeignExpiredTimerCannotPenalizeOrExpireTheFreshBusiness()
    {
        using (var h = new Host())
        {
            var old = h.Current; old.StartThree(); old.Clock.AdvanceTime(18); var timer = old.Patience.GetTimer(0);
            h.Abandon(); var fresh = h.Reenter(); fresh.StartThree(); double remaining = fresh.Clock.RemainingSeconds;
            Assert.That(fresh.Patience.TryProcessExpiredBatch(new[] { timer }), Is.Zero);
            Assert.That(fresh.Entrance.TryStartComplaint(timer), Is.False); Assert.That(fresh.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(fresh.Clock.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(fresh.Entrance.IsWaiting(0), Is.True);
        }
    }

    [Test]
    public void TwoAbandonedBusinessesProduceDistinctFreshIdentitiesWithoutAccumulatedData()
    {
        using (var h = new Host())
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var g = h.Current; Assert.That(ids.Add(g.Life.SessionId), Is.True); g.Start(); AssertFreshRunning(g);
                if (i == 2) break; h.Abandon(); h.Reenter();
            }
            Assert.That(ids, Has.Count.EqualTo(3)); Assert.That(h.Navigation.Count, Is.EqualTo(2));
        }
    }

    [TestCase("unknown-key")]
    [TestCase("wrong-expected")]
    [TestCase("active-old")]
    [TestCase("active-replacement")]
    public void InvalidNavigationReplacementIsAtomicAndKeepsTheOriginalRegistration(string failure)
    {
        using (var h = new Host())
        {
            var original = h.Current.View;
            if (failure != "active-old") { Assert.That(original.TryClose(), Is.True); h.Current.DisableServices(); }
            var candidate = h.CreateCandidate();
            if (failure == "active-replacement") candidate.gameObject.SetActive(true);
            int count = h.Navigation.Count; var dictionary = Registry(h.Navigation); var registered = dictionary[UIPopupStore.ViewName];
            string key = failure == "unknown-key" ? "missing-view" : UIPopupStore.ViewName;
            var expected = failure == "wrong-expected" ? candidate : original;
            Assert.That(h.Navigation.TryReplaceInactiveView(key, expected, candidate), Is.False);
            Assert.That(Registry(h.Navigation)[UIPopupStore.ViewName], Is.SameAs(registered)); Assert.That(h.Navigation.Count, Is.EqualTo(count));
            Assert.That(Read(h.Floor, "_popupStoreView"), Is.SameAs(original));
        }
    }

    [Test]
    public void ValidInactiveReplacementInitializesBeforePublishingAndPreservesOtherViewRegistrations()
    {
        using (var h = new Host())
        {
            var old = h.Current; Assert.That(old.View.TryClose(), Is.True); old.DisableServices(); var candidate = h.CreateCandidate();
            var before = Registry(h.Navigation); var floor = before["UIFloor3"]; var other = before["OtherView"];
            Assert.That(h.Navigation.TryReplaceInactiveView(UIPopupStore.ViewName, old.View, candidate), Is.True);
            Assert.That(Registry(h.Navigation)[UIPopupStore.ViewName], Is.SameAs(candidate)); Assert.That(Registry(h.Navigation)["UIFloor3"], Is.SameAs(floor));
            Assert.That(Registry(h.Navigation)["OtherView"], Is.SameAs(other)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            h.Navigation.Push(UIPopupStore.ViewName); Assert.That(h.Navigation.FirstView, Is.SameAs(candidate));
            Assert.That(candidate.VisibleState, Is.EqualTo(VisibleState.Appeared)); Assert.That(candidate.TryClose(), Is.True);
        }
    }

    [Test]
    public void ThrowingReplacementInitializationCannotPublishAHalfBoundView()
    {
        using (var h = new Host())
        {
            var old = h.Current; Assert.That(old.View.TryClose(), Is.True); old.DisableServices();
            var go = new GameObject("lifetime-throwing-view", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(h.Scope, false); go.SetActive(false);
            var candidate = go.AddComponent<PopupStoreLifetimeThrowingView>();
            Assert.That(h.Navigation.TryReplaceInactiveView(UIPopupStore.ViewName, old.View, candidate), Is.False);
            Assert.That(Registry(h.Navigation)[UIPopupStore.ViewName], Is.SameAs(old.View)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            Assert.That(candidate.gameObject.activeSelf, Is.False); Assert.That(candidate.VisibleState, Is.EqualTo(VisibleState.Disappeared));
        }
    }

    [Test]
    public void LifetimeReplacementDoesNotMutateTheTwoUseTestPolicyOrR0AuthorityContracts()
    {
        Assert.That(new PopupStoreBusinessAccessPolicy().RewardedDailyLimit, Is.EqualTo(2));
        string source = File.ReadAllText("Assets/Scripts/UI/Floor3UI/PopupStoreSessionLifetime.cs");
        foreach (string forbidden in new[] { "Backend.", "UserInfo.", "PlayerPrefs", "Time.timeScale", "PopupStoreBusinessStartTransaction",
            "PopupStoreRewardTransaction", "RewardedDailyLimit =", "TryStartSession(" }) Assert.That(source, Does.Not.Contain(forbidden), forbidden);
    }

    [Test]
    public void LifecycleValidationRestoresSceneUndoSelectionAndManagersWithoutSaving()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var h = new Host()) { h.Current.StartThree(); h.Abandon(); h.Reenter().Start(); }
        witness.AssertUnchanged("popup store session lifetime memory validation");
    }

    private static void AssertNoAbandonReward(Graph g)
    {
        Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned));
        Assert.That(g.Grade.FinalizationCount, Is.Zero); Assert.That(g.Grade.FinalResult, Is.Null);
        Assert.That(g.Reward.RandomDrawCount, Is.Zero); Assert.That(g.Reward.DecisionCount, Is.Zero); Assert.That(g.Reward.Result, Is.Null);
        Assert.That(g.Result.IsVisible, Is.False); Assert.That(g.Reveal.RevealCount, Is.Zero);
    }
    private static void AssertFreshRunning(Graph g)
    {
        Assert.That(g.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running)); Assert.That(g.Clock.RemainingSeconds, Is.EqualTo(90d));
        Assert.That(g.Clock.ElapsedSeconds, Is.Zero); Assert.That(g.Clock.EndReason, Is.Null); Assert.That(g.Clock.AbandonCount, Is.Zero);
        Assert.That(g.Clock.ComplaintPenaltyCount, Is.Zero); Assert.That(g.Score.TotalScore, Is.Zero); Assert.That(g.Score.SuccessfulOrderCount, Is.Zero);
        Assert.That(g.Counter.Count, Is.Zero); Assert.That(g.Packing.CommittedReceipts, Is.Empty); Assert.That(g.Queue, Is.Empty);
        Assert.That(g.Packing.SuccessfulPackCount, Is.Zero); Assert.That(g.Presentation.ReceivedReceipts, Is.Empty);
        Assert.That(g.Presentation.VisualOutcomes, Is.Empty); Assert.That(g.Presentation.CurrentReceipt, Is.Null);
        Assert.That(g.Presentation.StartedPresentationCount, Is.Zero); Assert.That(g.Presentation.CompletedDeliveryCount, Is.Zero);
        Assert.That(g.Entrance.SuppliedOrderCount, Is.Zero); Assert.That(g.Entrance.StartedEntranceCount, Is.Zero);
        Assert.That(g.Entrance.StartedDepartureCount, Is.Zero); Assert.That(g.Entrance.ReleasedCount, Is.Zero);
        Assert.That(g.Entrance.StartedComplaintCount, Is.Zero); Assert.That(g.Patience.ExpiredCount, Is.Zero);
        Assert.That(g.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.None)); Assert.That(g.Grade.FinalResult, Is.Null);
        Assert.That(g.Grade.FinalizationCount, Is.Zero); Assert.That(g.Reward.Result, Is.Null); Assert.That(g.Reward.RandomDrawCount, Is.Zero);
        Assert.That(g.Reward.DecisionCount, Is.Zero); Assert.That(g.Reveal.OpenedResult, Is.Null); Assert.That(g.Reveal.RevealCount, Is.Zero);
        Assert.That(g.Result.ConfirmationCount, Is.Zero); Assert.That(g.Result.PresentationCount, Is.Zero);
        Assert.That(g.Box.IsVisible, Is.False); Assert.That(g.Life.IsRetired, Is.False);
    }
    private static IEnumerable<Component> Components(Graph g) => new Component[] { g.View, g.Clock, g.Life, g.Entrance, g.Patience,
        g.Counter, g.Orders, g.Packing, g.Presentation, g.Score, g.Grade, g.Reward, g.Result, g.Box, g.Reveal };
    private static bool AbandonClock(PopupStoreBusinessClock clock) => (bool)Invoke(clock, "TryAbandonSession");
    private static Dictionary<string, MobileUIView> Registry(MobileUINavigation navigation)
        => (Dictionary<string, MobileUIView>)Read(navigation, "_viewDic");

    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        private readonly List<GameObject> _tracked = new List<GameObject>();
        internal Graph Current { get; private set; }
        internal MobileUINavigation Navigation => (MobileUINavigation)Read(_inner, "Navigation");
        internal UIFloor3 Floor => (UIFloor3)Read(_inner, "Floor");
        internal Button Entry => (Button)Read(_inner, "Entry");
        internal Transform Scope => Navigation.transform.parent;
        internal Host()
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            try { Current = new Graph((UIPopupStore)Read(_inner, "View")); Current.RefreshServices(); }
            catch { Dispose(); throw; }
        }
        internal void Track(UIPopupStore view)
        { if (view != null && !_tracked.Contains(view.gameObject)) _tracked.Add(view.gameObject); }
        internal void Abandon()
        {
            Current.Request();
            if (Current.Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            Assert.That(Current.Exit.TryConfirmAbandon(), Is.True); Current.DisableServices();
            Assert.That(Navigation.FirstView, Is.SameAs(Floor)); Assert.That(Current.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned));
        }
        internal Graph Reenter()
        {
            Entry.onClick.Invoke(); Assert.That(Navigation.FirstView, Is.InstanceOf<UIPopupStore>());
            var view = (UIPopupStore)Navigation.FirstView; Track(view); Current = new Graph(view); Current.RefreshServices(); return Current;
        }
        internal UIPopupStore CreateCandidate()
        {
            var holder = new GameObject("lifetime-inactive-candidate"); holder.transform.SetParent(Scope, false); holder.SetActive(false);
            var candidate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), holder.transform, false);
            candidate.SetActive(false); candidate.transform.SetParent(Scope, false);
            var view = candidate.GetComponent<UIPopupStore>(); Track(view); return view;
        }
        public void Dispose()
        {
            try
            {
                foreach (var go in _tracked)
                    if (go != null)
                    {
                        var presentation = go.GetComponent<PopupStorePackingPresentation>();
                        if (presentation != null && presentation.CurrentReceipt != null)
                            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                        go.GetComponent<PopupStoreCustomerEntrance>()?.PauseVisibleSession(); presentation?.EndVisibleSession();
                    }
            }
            finally { ((IDisposable)_inner).Dispose(); }
        }
    }

    private sealed class Graph
    {
        internal readonly UIPopupStore View;
        internal GameObject Root => View.gameObject;
        internal PopupStoreSessionLifetime Life => Root.GetComponent<PopupStoreSessionLifetime>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal PopupStoreExitConfirmation Exit => Root.GetComponent<PopupStoreExitConfirmation>();
        internal PopupStoreCustomerEntrance Entrance => Root.GetComponent<PopupStoreCustomerEntrance>();
        internal PopupStoreCustomerPatience Patience => Root.GetComponent<PopupStoreCustomerPatience>();
        internal PopupStoreSalesCounter Counter => Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
        internal PopupStoreOrderDisplay Orders => Root.GetComponent<PopupStoreOrderDisplay>();
        internal PopupStorePacking Packing => Root.GetComponent<PopupStorePacking>();
        internal PopupStorePackingPresentation Presentation => Root.GetComponent<PopupStorePackingPresentation>();
        internal PopupStoreScore Score => Root.GetComponent<PopupStoreScore>();
        internal PopupStoreGradeSession Grade => Root.GetComponent<PopupStoreGradeSession>();
        internal PopupStoreRewardSession Reward => Root.GetComponent<PopupStoreRewardSession>();
        internal PopupStoreResultPanel Result => Root.GetComponent<PopupStoreResultPanel>();
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal PopupStoreRewardRevealPreview Reveal => Root.GetComponent<PopupStoreRewardRevealPreview>();
        internal PopupStoreOrder[] OrdersData => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal Button AbandonButton => (Button)Read(Exit, "_abandonButton");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal Graph(UIPopupStore view) { View = view; }
        internal void RefreshServices()
        {
            Assert.That(Life, Is.Not.Null); Write(Clock, "_autoStartForEditorReview", false);
            Root.GetComponent<CanvasScaler>().enabled = false; Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True); Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True); Assert.That(Entrance.BeginVisibleSession(), Is.True); Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Patience.Refresh(), Is.True); Assert.That(Score.Refresh(), Is.True); Assert.That(Grade.Refresh(), Is.True);
            Assert.That(Reward.Refresh(), Is.True); Assert.That(Result.Refresh(), Is.True); Assert.That(Exit.Refresh(), Is.True);
        }
        internal void DisableServices()
        {
            // Synchronous EditMode dispatch explicitly models Unity's deactivate callbacks.
            Invoke(Clock, "OnDisable"); Entrance.PauseVisibleSession(); Presentation.EndVisibleSession();
            foreach (var c in new Component[] { Patience, Packing, Root.GetComponent<PopupStoreOrderProgress>(), Counter, Orders, Score, Grade, Result, Exit }) Invoke(c, "OnDisable");
        }
        internal void Start() { Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True); }
        internal void StartThree()
        {
            Start(); Entrance.TickEntrance(0); Entrance.TickEntrance(4); Entrance.TickEntrance(4);
            Entrance.TickEntrance((float)Read(Entrance, "_entryDuration") + .01f);
            for (int i = 0; i < 3; i++) Assert.That(Entrance.IsWaiting(i), Is.True);
        }
        internal void Request() { Assert.That(View.TryRequestExit(), Is.True); Assert.That(Exit.IsVisible, Is.True); }
        internal void Fill(int slot)
        {
            Assert.That(OrdersData[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            foreach (var material in materials) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal PopupStorePackingReceipt Pack(int slot)
        { Fill(slot); var packed = Packing.TryPack(); Assert.That(packed.Success, Is.True); return packed.Receipt; }
        internal void ToPhase(string phase)
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void CompleteVisual()
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
        }
        internal void CompleteDelivery() { CompleteVisual(); Entrance.TickEntrance((float)Read(Entrance, "_exitDuration") + .01f); }
        internal void SellUntil(PopupStoreGradeTier grade)
        {
            int safety = 0;
            while (Grade.Current.Grade != grade && safety++ < 2000)
            {
                Entrance.TickEntrance(.5f); int slot = Enumerable.Range(0, 3).Where(Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot >= 0) { Pack(slot); CompleteDelivery(); }
            }
            Assert.That(Grade.Current.Grade, Is.EqualTo(grade));
        }
        internal void End() { Clock.AdvanceTime(90); Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended)); }
        internal void ShowResult() { Result.TickResult(0); Result.TickResult(1); Result.TickResult(1); Assert.That(Result.IsVisible, Is.True); }
    }

    private static void Condition(PopupStoreBusinessClock clock, string reason, bool paused)
    {
        if (reason == "focus") clock.SetApplicationFocused(!paused);
        else if (reason == "application") clock.SetApplicationPaused(paused);
        else clock.SetViewVisible(!paused);
    }
    private static string Account() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
        UserInfo.CustomerEncounterContextGeneration, UserInfo.DailyAddMoney, UserInfo.DailyCookCount, UserInfo.DailyCumulativeCustomerCount,
        UserInfo.DailyAdvertisingViewCount, UserInfo.DailyCleanCount, UserInfo.DailyAdGoldRewardCount, UserInfo.DailyAdDiaRewardCount });
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object Invoke(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
}

public sealed class PopupStoreLifetimeThrowingView : MobileUIView
{
    public override void Init() { throw new InvalidOperationException("lifetime-test-init"); }
    public override void Show() { VisibleState = VisibleState.Appeared; }
    public override void Hide() { VisibleState = VisibleState.Disappeared; }
}
#endif
