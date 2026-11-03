#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Actual popup prefab and existing transaction services, using local monotonic time only.
/// No Play, scene creation, account persistence, SDK requests or global time-scale mutation.</summary>
public sealed class PopupStoreExitConfirmationTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private string _account;
    private float _timeScale;
    private bool _sdk;
    private BackendManager _backend;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void RecordOnlyExistingAccountAndSdkState()
    {
        _account = AccountState(); _timeScale = Time.timeScale;
        _sdk = BackEnd.Backend.IsInitialized; _backend = BackendManager.ExistingInstance;
        _random = UnityEngine.Random.state;
    }

    [TearDown]
    public void ExitReviewCannotChangeCurrencyDailyDataSdkRandomOrGlobalTime()
    {
        Assert.That(AccountState(), Is.EqualTo(_account)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk)); Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void ActualPrefabHasOneAuthoredBlockingConfirmationAndOriginalFont()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var exit = prefab.GetComponent<PopupStoreExitConfirmation>();
        Assert.That(exit, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreExitConfirmation>(true), Has.Length.EqualTo(1));
        Assert.That(Read(exit, "_clock"), Is.SameAs(prefab.GetComponent<PopupStoreBusinessClock>()));
        var overlay = (GameObject)Read(exit, "_overlay");
        Assert.That(overlay.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(overlay.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.True);
        var labels = overlay.GetComponentsInChildren<TMP_Text>(true);
        string text = string.Join("\n", labels.Select(t => t.text));
        Assert.That(text, Does.Contain("영업을 포기하시겠습니까?"));
        Assert.That(text, Does.Contain("영업을 포기하면 사용한 영업권은 반환되지 않으며, 보상을 받을 수 없습니다."));
        Assert.That(text, Does.Contain("계속하기").And.Contain("포기하고 나가기"));
        var font = ((TMP_Text)Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_timeText")).font;
        foreach (var label in labels) { Assert.That(label.font, Is.SameAs(font)); Assert.That(label.raycastTarget, Is.False); }
    }

    [Test]
    public void LegacyPauseButtonRemainsHiddenAndInertAfterShowAndRefresh()
    {
        using (var h = new Host())
        {
            h.Start(); var pause = h.Root.GetComponent<PopupStorePauseController>();
            var button = (Button)Read(pause, "_pauseButton");
            for (int i = 0; i < 5; i++) { pause.Refresh(); button.onClick.Invoke(); Assert.That(pause.TryTogglePause(), Is.False); }
            Assert.That(button.gameObject.activeSelf, Is.False); Assert.That(button.interactable, Is.False);
            Assert.That(h.Clock.IsManualPaused, Is.False); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [Test]
    public void RunningCloseShowsConfirmationWithoutRemovingOrHidingItsView()
    {
        using (var h = new Host())
        {
            h.StartThree(); int count = h.Navigation.Count;
            Assert.That(h.View.TryClose(), Is.False);
            Assert.That(h.Exit.IsVisible, Is.True); Assert.That(h.Exit.ShowCount, Is.EqualTo(1));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(h.Clock.IsExitConfirmationPaused, Is.True); Assert.That(h.Clock.IsManualPaused, Is.False);
            Assert.That(h.Navigation.Count, Is.EqualTo(count)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.View.VisibleState, Is.EqualTo(VisibleState.Appeared)); Assert.That(h.Root.activeSelf, Is.True);
        }
    }

    [Test]
    public void AuthoredCloseButtonUsesTheSameConfirmationRequest()
    {
        using (var h = new Host())
        {
            h.Start(); ((Button)Read(h.View, "_closeButton")).onClick.Invoke();
            Assert.That(h.Exit.IsVisible, Is.True); Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.Exit.ShowCount, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CoordinatorBackRequestInterceptsPopupIncludingFallbackNavigation(bool emptyTop)
    {
        using (var h = new Host())
        {
            h.StartThree(); var coordinator = h.Coordinator(emptyTop); int count = h.Navigation.Count;
            Assert.That(coordinator.Pop(), Is.True);
            Assert.That(h.Exit.IsVisible, Is.True); Assert.That(h.Navigation.Count, Is.EqualTo(count));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View)); Assert.That(h.Exit.ShowCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedCloseBackAndMapEntryKeepOneConfirmationAndOnePopup()
    {
        using (var h = new Host())
        {
            h.StartThree(); var coordinator = h.Coordinator(); int count = h.Navigation.Count;
            for (int i = 0; i < 12; i++)
            {
                Assert.That(h.View.TryRequestExit(), Is.True); Assert.That(coordinator.Pop(), Is.True);
                Assert.That(h.View.TryClose(), Is.False); h.Entry.onClick.Invoke();
            }
            Assert.That(h.Exit.ShowCount, Is.EqualTo(1)); Assert.That(h.Navigation.Count, Is.EqualTo(count));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.Root.GetComponentsInChildren<PopupStoreExitConfirmation>(true), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void OpenConfirmationBlocksRawPopAndRestoresPreviousNavigationAndInputPermissions()
    {
        using (var h = new Host())
        {
            h.StartThree(); var input = (CanvasGroup)Read(h.Result, "_inputGroup");
            bool popEnabled = h.View.PopEnabled, interactable = input.interactable, raycasts = input.blocksRaycasts;
            int count = h.Navigation.Count; h.Request();
            Assert.That(h.View.PopEnabled, Is.False); Assert.That(input.interactable, Is.False);
            h.Navigation.Pop(); h.Navigation.Pop(UIPopupStore.ViewName);
            Assert.That(h.Navigation.Count, Is.EqualTo(count)); Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(h.Exit.IsVisible, Is.True); h.Continue();
            Assert.That(h.View.PopEnabled, Is.EqualTo(popEnabled)); Assert.That(input.interactable, Is.EqualTo(interactable));
            Assert.That(input.blocksRaycasts, Is.EqualTo(raycasts));
        }
    }

    [Test]
    public void ContinueButtonResumesExactlyTheSameBusinessAndMaterialOrder()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); h.Clock.AdvanceTime(2); var frozen = new Snapshot(h);
            string business = h.Grade.SessionId;
            h.Request(); h.TickAll(200); frozen.AssertSame(h);
            h.ContinueButton.onClick.Invoke();
            Assert.That(h.Exit.IsVisible, Is.False); Assert.That(h.Exit.ContinueCount, Is.EqualTo(1));
            Assert.That(h.Clock.IsExitConfirmationPaused, Is.False); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Grade.SessionId, Is.EqualTo(business)); frozen.AssertSame(h);
            Assert.That(h.Packing.TryPack().Success, Is.True); h.CompleteDelivery(); Assert.That(h.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ContinueAndAbandonRequestsWithoutAnOpenConfirmationAreNoOps()
    {
        using (var h = new Host())
        {
            h.StartThree(); var frozen = new Snapshot(h);
            Assert.That(h.Exit.TryContinue(), Is.False); Assert.That(h.Exit.TryConfirmAbandon(), Is.False);
            Assert.That(h.Exit.ContinueCount, Is.Zero); Assert.That(h.Exit.ShowCount, Is.Zero); frozen.AssertSame(h);
        }
    }

    [Test]
    public void ConfirmedAbandonClosesOnceWithoutCompletingExpiringOrSpendingAnything()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); h.Request();
            double remaining = h.Clock.RemainingSeconds;
            var materials = h.Counter.Materials.ToArray();
            Assert.That(h.Exit.CanConfirmAbandon, Is.True); Assert.That(h.AbandonButton.interactable, Is.True);
            h.AbandonButton.onClick.Invoke();
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Abandoned));
            Assert.That(h.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.DirectExit));
            for (int i = 0; i < 6; i++) { h.AbandonButton.onClick.Invoke(); Assert.That(h.Exit.TryConfirmAbandon(), Is.False); }
            Assert.That(h.Exit.TryContinue(), Is.False); h.TickAll(200);
            Assert.That(h.Clock.AbandonCount, Is.EqualTo(1)); Assert.That(h.Clock.EndedCount, Is.Zero);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(remaining, Is.GreaterThan(0));
            CollectionAssert.AreEqual(materials, h.Counter.Materials);
            Assert.That(h.Exit.IsVisible, Is.False); Assert.That(h.Grade.FinalResult, Is.Null);
            Assert.That(h.Grade.FinalizationCount, Is.Zero); Assert.That(h.Score.TotalScore, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().Result, Is.Null);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor)); Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Root.activeSelf, Is.False); Assert.That(h.Root.GetComponent<PopupStoreSessionLifetime>().IsRetired, Is.True);
            Assert.That(((GameObject)Read(h.Exit, "_reviewHint")).activeSelf, Is.False);
        }
    }

    [Test]
    public void OpeningSettlesCurrentTimeBeforePausingAndContinuingDiscardsElapsedWallTime()
    {
        using (var h = new Host())
        {
            double now = 100; Write(h.Clock, "_timeSource", new Func<double>(() => now)); h.StartThree();
            now = 102.25; h.Request();
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(87.75d));
            Assert.That(h.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(15.75d)); var frozen = new Snapshot(h);
            now = 9000; h.Clock.SynchronizeNow(); h.TickAll(200); frozen.AssertSame(h);
            h.Continue(); h.Clock.SynchronizeNow(); frozen.AssertSame(h);
            now += .25; h.Clock.SynchronizeNow(); Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(87.5d));
            Assert.That(h.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(15.5d));
        }
    }

    [Test]
    public void WaitingPatienceCannotExpireWhileConfirmationIsVisible()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Clock.AdvanceTime(17.75); h.Request(); var timer = h.Patience.GetTimer(0); var frozen = new Snapshot(h);
            h.TickAll(1000); frozen.AssertSame(h); Assert.That(timer.RemainingPatience, Is.EqualTo(.25d)); Assert.That(timer.Order.IsExpired, Is.False);
            h.Continue(); h.Clock.AdvanceTime(.25); Assert.That(timer.Order.IsExpired, Is.True); Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void EnteringCustomerCannotArriveOrSupplyAnotherOrderBehindConfirmation()
    {
        using (var h = new Host())
        {
            h.Start(); h.Entrance.TickEntrance(0); float duration = (float)Read(h.Entrance, "_entryDuration");
            h.Entrance.TickEntrance(duration * .25f); Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            h.Request(); var frozen = new Snapshot(h); h.TickAll(500); frozen.AssertSame(h);
            Assert.That(h.Patience.GetTimer(0), Is.Null); Assert.That(h.Orders.GetDisplayedMaterials(0), Is.Empty);
            h.Continue(); h.Entrance.TickEntrance(duration * .76f); Assert.That(h.Entrance.IsWaiting(0), Is.True);
            Assert.That(h.Entrance.SuppliedOrderCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void NormalDepartureCannotReleaseOrReplaceASlotUntilContinue()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); var receipt = h.Packing.TryPack().Receipt; h.Call("CompleteVisual", true);
            float duration = (float)Read(h.Entrance, "_exitDuration"); h.Entrance.TickEntrance(duration * .25f);
            Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving)); h.Request(); var frozen = new Snapshot(h);
            h.TickAll(300); frozen.AssertSame(h); Assert.That(h.Entrance.ReleasedCount, Is.Zero);
            h.Continue(); h.Entrance.TickEntrance(duration * .76f); Assert.That(h.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(h.Presentation.LastDeliveredReceipt, Is.SameAs(receipt)); Assert.That(h.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ComplaintIconAndItsSinglePenaltyRemainFrozenUntilContinue()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Clock.AdvanceTime(18); float duration = (float)Read(h.Entrance, "_complaintDuration");
            h.Entrance.TickEntrance(duration * .25f); Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            h.Request(); var frozen = new Snapshot(h); h.TickAll(300); frozen.AssertSame(h);
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(1)); Assert.That(h.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            h.Continue(); h.Entrance.TickEntrance(duration * .76f); Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            h.Call("FinishDepartures"); Assert.That(h.Entrance.ReleasedCount, Is.EqualTo(1)); Assert.That(h.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void ConfirmationFreezesEachCommittedDeliveryPhaseAndContinueFinishesTheSameReceiptOnce(string phase)
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); var receipt = h.Packing.TryPack().Receipt; h.Call("AdvanceVisualTo", phase);
            h.Request(); var frozen = new Snapshot(h); h.TickAll(300); frozen.AssertSame(h);
            Assert.That(h.Presentation.CurrentReceipt, Is.SameAs(receipt)); Assert.That(h.Presentation.VisualOutcomes, Is.Empty);
            h.Continue(); h.CompleteDelivery();
            Assert.That(h.Presentation.LastDeliveredReceipt, Is.SameAs(receipt)); Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(h.Presentation.VisualOutcomes, Has.Count.EqualTo(1));
            Assert.That(h.Presentation.VisualOutcomes[0].Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(h.Presentation.VisualOutcomes[0].Receipt, Is.SameAs(receipt)); Assert.That(h.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(1)); Assert.That(h.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ConfirmationBeforePresentationPreservesTheQueuedReceiptWithoutDequeueOrCancellation()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); var receipt = h.Packing.TryPack().Receipt;
            Assert.That(h.Queue, Has.Count.EqualTo(1)); h.Request(); var frozen = new Snapshot(h); h.TickAll(300); frozen.AssertSame(h);
            Assert.That(h.Queue.Peek(), Is.SameAs(receipt)); Assert.That(h.Presentation.CurrentReceipt, Is.Null);
            h.Continue(); h.CompleteDelivery(); Assert.That(h.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void ConfirmationBlocksMaterialClickAddClearAndPackingAtTheirTransactionBoundaries()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); h.Request(); var frozen = new Snapshot(h);
            var material = h.Counter.Materials[0]; Assert.That(h.Counter.TryAdd(material), Is.False); h.Counter.Clear();
            ((Button)Read(h.Counter, "_clearButton")).onClick.Invoke(); ((Button)Read(h.Packing, "_packButton")).onClick.Invoke();
            var item = h.Item("STAFF01"); var pointer = Pointer(); item.OnPointerDown(pointer); item.OnPointerClick(pointer);
            Assert.That(h.Packing.TryPack().Success, Is.False); frozen.AssertSame(h);
            Assert.That(h.Counter.CanAcceptInput, Is.False); Assert.That(((Button)Read(h.Packing, "_packButton")).IsInteractable(), Is.False);
            h.Continue(); Assert.That(h.Counter.CanAcceptInput, Is.True); Assert.That(h.Packing.TryPack().Success, Is.True);
            h.CompleteDelivery(); Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void MaterialPressBeforeConfirmationCannotCompleteAsASelectionAfterContinue()
    {
        using (var h = new Host())
        {
            h.StartThree(); var item = h.Item("STAFF01"); var pointer = Pointer(); item.OnPointerDown(pointer);
            h.Request(); h.Continue(); item.OnPointerClick(pointer); Assert.That(h.Counter.Count, Is.Zero);
            item.OnPointerDown(pointer); item.OnPointerClick(pointer); Assert.That(h.Counter.Count, Is.EqualTo(1));
        }
    }

    [TestCase("focus")]
    [TestCase("application")]
    public void ContinuingWhileExternalPauseRemainsCannotResumeBusinessOrCatchUpTime(string reason)
    {
        using (var h = new Host())
        {
            double now = 0; Write(h.Clock, "_timeSource", new Func<double>(() => now)); h.StartThree(); now = 1;
            h.Request(); External(h.Clock, reason, true); var frozen = new Snapshot(h); h.Continue();
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused)); Assert.That(h.Clock.IsExitConfirmationPaused, Is.False);
            now = 500; h.TickAll(300); h.Clock.SynchronizeNow(); frozen.AssertSame(h);
            External(h.Clock, reason, false); h.Clock.SynchronizeNow(); frozen.AssertSame(h);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            now += .25; h.Clock.SynchronizeNow(); Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(88.75d));
        }
    }

    [TestCase("focus")]
    [TestCase("application")]
    public void ReturningFocusOrForegroundCannotDismissAnOpenConfirmation(string reason)
    {
        using (var h = new Host())
        {
            h.StartThree(); External(h.Clock, reason, true); h.Request(); External(h.Clock, reason, false);
            Assert.That(h.Exit.IsVisible, Is.True); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            var frozen = new Snapshot(h); h.TickAll(300); frozen.AssertSame(h); h.Continue();
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [Test]
    public void AllExternalPauseReasonsMustClearAfterContinueBeforeAnyProgressResumes()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Request(); h.Clock.SetApplicationPaused(true); h.Clock.SetApplicationFocused(false); h.Continue();
            h.Clock.SetApplicationPaused(false); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            h.Clock.SetApplicationFocused(true); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Clock.IsManualPaused, Is.False); Assert.That(h.Clock.IsExitConfirmationPaused, Is.False);
        }
    }

    [Test]
    public void ContinueDiscardsFirstAutomaticVisualDeltaWithoutDiscardingLaterNormalFrames()
    {
        using (var h = new Host())
        {
            h.Start(); uint observed = h.Clock.VisualResumeVersion;
            h.Request(); h.Continue(); Assert.That(h.Clock.FilterAutomaticVisualDelta(900f, ref observed), Is.Zero);
            Assert.That(h.Clock.FilterAutomaticVisualDelta(.125f, ref observed), Is.EqualTo(.125f));
        }
    }

    [Test]
    public void LegacyManualPauseIsNotLeftLatchedAfterRequestAndContinue()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Clock.SetManualPaused(true); h.Request(); h.Continue();
            Assert.That(h.Clock.IsManualPaused, Is.False); Assert.That(h.Clock.IsExitConfirmationPaused, Is.False);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [Test]
    public void ReentrantClockObserverCannotCreateMultipleConfirmationInstances()
    {
        using (var h = new Host())
        {
            h.StartThree(); int callbacks = 0; Action observer = () => { callbacks++; h.View.TryRequestExit(); };
            h.Clock.StateChanged += observer;
            try { h.Request(); } finally { h.Clock.StateChanged -= observer; }
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(h.Exit.ShowCount, Is.EqualTo(1)); Assert.That(h.Exit.IsVisible, Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View)); h.Continue();
        }
    }

    [Test]
    public void TenExitCancelCyclesNeverFinalizeRerollResetOrdersOrAdvanceTime()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Fill(0); var frozen = new Snapshot(h);
            for (int i = 0; i < 10; i++) { h.Request(); h.TickAll(300); h.Continue(); }
            frozen.AssertSame(h); Assert.That(h.Exit.ShowCount, Is.EqualTo(10)); Assert.That(h.Exit.ContinueCount, Is.EqualTo(10));
            var rewards = h.Root.GetComponent<PopupStoreRewardSession>();
            Assert.That(rewards.RandomDrawCount, Is.Zero); Assert.That(rewards.DecisionCount, Is.Zero); Assert.That(h.Grade.FinalizationCount, Is.Zero);
        }
    }

    [Test]
    public void NotStartedClosesDirectlyWithoutConfirmationFinalizationOrReward()
    {
        using (var h = new Host())
        {
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted)); Assert.That(h.View.TryClose(), Is.True);
            Assert.That(h.Exit.IsVisible, Is.False); Assert.That(h.Exit.ShowCount, Is.Zero);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor)); Assert.That(h.Grade.FinalizationCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().DecisionCount, Is.Zero);
        }
    }

    [Test]
    public void DeadlineSettledByExitRequestCompletesOnceAndNextEntryUsesAFreshBusiness()
    {
        using (var h = new Host())
        {
            double now = 0; Write(h.Clock, "_timeSource", new Func<double>(() => now)); h.Start(); now = 100;
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended)); Assert.That(h.Clock.EndedCount, Is.EqualTo(1));
            Assert.That(h.Exit.IsVisible, Is.False); Assert.That(h.Exit.ShowCount, Is.Zero); Assert.That(h.Clock.IsExitConfirmationPaused, Is.False);
            Assert.That(h.Grade.FinalizationCount, Is.EqualTo(1)); var final = h.Grade.FinalResult;
            Assert.That(final, Is.Not.Null); Assert.That(h.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.Completed));
            Assert.That(h.Clock.AbandonCount, Is.Zero); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            AssertFreshEntryAfterTerminalClose(h, final.SessionId);
        }
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.S)]
    public void EndedResultConfirmationPreservesHistoryAndMapReturnThenPreparesAFreshBusiness(PopupStoreGradeTier tier)
    {
        using (var h = new Host())
        {
            var rewards = h.Root.GetComponent<PopupStoreRewardSession>(); var random = new FixedRewardSource();
            Assert.That(rewards.TrySetRandomSource(random), Is.True); Assert.That(rewards.Refresh(), Is.True);
            h.Start(); h.Request(); h.Continue(); h.Call("SellUntil", tier); h.Call("End"); h.ShowResult();
            var final = h.Grade.FinalResult; var decision = rewards.Result;
            var box = h.Root.GetComponent<PopupStoreRewardBox>(); var reveal = h.Root.GetComponent<PopupStoreRewardRevealPreview>();
            Assert.That(box.DisplayedSprite.name, Is.EqualTo("PopupRewardBox_" + tier + "_APPROVED"));
            Assert.That(reveal.TryReveal(), Is.True); reveal.TickPreview(1); h.ConfirmResult();
            Assert.That(h.Exit.IsVisible, Is.False); Assert.That(h.Grade.FinalResult, Is.SameAs(final));
            Assert.That(h.Grade.FinalizationCount, Is.EqualTo(1)); Assert.That(rewards.Result, Is.SameAs(decision));
            Assert.That(rewards.DecisionCount, Is.EqualTo(1)); Assert.That(rewards.RandomDrawCount, Is.EqualTo(1));
            Assert.That(random.Calls, Is.EqualTo(1));
            Assert.That(reveal.RevealCount, Is.EqualTo(1)); Assert.That(reveal.IsAnimating, Is.False);
            AssertFreshEntryAfterTerminalClose(h, final.SessionId);
        }
    }

    [Test]
    public void AnotherUiStillPopsNormallyThroughCoordinator()
    {
        using (var h = new Host())
        {
            // The shared result fixture's bare UIFloor3 intentionally has no map tween references.
            // Exercise the unmodified common-navigation path with an ordinary deterministic view instead.
            h.StartThree();
            var navObject = new GameObject("exit-confirmation-other-navigation"); navObject.transform.SetParent(h.Navigation.transform, false);
            var navigation = navObject.AddComponent<MobileUINavigation>();
            var viewObject = new GameObject("exit-confirmation-other-ui", typeof(RectTransform), typeof(Canvas));
            viewObject.transform.SetParent(navObject.transform, false); var view = viewObject.AddComponent<PopupStoreExitUnrelatedTestView>();
            Write(navigation, "_uiViews", new[] { new MobileViewDicStruct { Name = "ordinary", UIView = view } });
            Invoke(navigation, "Init"); navigation.PushNoAnime("ordinary"); var coordinator = h.Coordinator(false, navigation);
            Assert.That(navigation.FirstView, Is.SameAs(view)); Assert.That(coordinator.Pop(), Is.True);
            Assert.That(navigation.Count, Is.Zero); Assert.That(view.Hides, Is.EqualTo(1)); Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View)); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Exit.ShowCount, Is.Zero);
        }
    }

    [Test]
    public void ForcedNavigationHideRemainsSeparateFromUserAbandonAndReentryIsNotStranded()
    {
        using (var h = new Host())
        {
            h.StartThree(); h.Request(); double remaining = h.Clock.RemainingSeconds;
            h.Navigation.PopNoAnime(UIPopupStore.ViewName);
            // PopNoAnime bypasses Hide. The synchronous EditMode fixture must dispatch both
            // lifecycle callbacks that Unity invokes when this GameObject is disabled in Play.
            Invoke(h.Clock, "OnDisable"); h.Call("DisableObservers"); Invoke(h.Exit, "OnDisable");
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor)); Assert.That(h.Exit.IsVisible, Is.False);
            Assert.That(h.Clock.IsExitConfirmationPaused, Is.False); Assert.That(h.Grade.FinalResult, Is.Null);
            h.Reopen(); Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(h.Exit.IsVisible, Is.False);
        }
    }

    [Test]
    public void DisposeLeavesSceneUndoSelectionManagersAndWorkspaceWitnessUntouched()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var h = new Host()) { h.StartThree(); h.Request(); h.TickAll(200); h.Continue(); }
        witness.AssertUnchanged("popup store exit confirmation memory tests");
    }

    [Test]
    public void ExitUxContainsNoPaymentStartAuthorityGlobalTimeOrRewardTableWrites()
    {
        string source = File.ReadAllText("Assets/Scripts/UI/Floor3UI/PopupStoreExitConfirmation.cs");
        foreach (string forbidden in new[] { "Time.timeScale", "Backend.", "UserInfo.", "PlayerPrefs", "PopupStoreBusinessStartTransaction",
            "PopupStoreRewardTransaction", "TryStartSession(", "AdvanceTime(", "TryPack(" })
            Assert.That(source, Does.Not.Contain(forbidden), forbidden);
        Assert.That(source, Does.Contain("TryConfirmAbandon").And.Contain("CanConfirmAbandon"));
    }

    private sealed class FixedRewardSource : IPopupStoreRewardRandomSource
    { internal int Calls; public int NextPercent() { Calls++; return 85; } }

    private static void AssertFreshEntryAfterTerminalClose(Host h, string previousSession)
    {
        // DEV-05D-3C: acknowledgement retires a completed business. Forced Hide cases retain
        // their original graph elsewhere; the existing map prepares a distinct next business here.
        var navigation = h.Navigation; var floor = h.Floor;
        var lifetime = h.Root.GetComponent<PopupStoreSessionLifetime>();
        Assert.That(lifetime.IsRetired, Is.True);
        Assert.That(lifetime.TryPrepareForEntry(out var fresh), Is.True, lifetime.LastError);
        Assert.That(fresh, Is.Not.Null);
        var grade = fresh.GetComponent<PopupStoreGradeSession>();
        Assert.That(grade.SessionId, Is.Not.EqualTo(previousSession)); Assert.That(grade.FinalResult, Is.Null);
        Assert.That(grade.FinalizationCount, Is.Zero); Assert.That(fresh.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
        Assert.That(fresh.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        Assert.That(fresh.GetComponent<PopupStoreRewardSession>().Result, Is.Null);
        Assert.That(fresh.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        Assert.That(fresh.GetComponent<PopupStoreRewardRevealPreview>().RevealCount, Is.Zero);
        navigation.Push(UIPopupStore.ViewName);
        Assert.That(navigation.FirstView, Is.SameAs(fresh));
        Assert.That(fresh.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
        Assert.That(fresh.TryClose(), Is.True); Assert.That(navigation.FirstView, Is.SameAs(floor));
    }

    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal Host()
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            try { Assert.That(Exit, Is.Not.Null); Exit.Refresh(); }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => Get<GameObject>("Root");
        internal UIPopupStore View => Get<UIPopupStore>("View");
        internal UIFloor3 Floor => Get<UIFloor3>("Floor");
        internal Button Entry => Get<Button>("Entry");
        internal MobileUINavigation Navigation => Get<MobileUINavigation>("Navigation");
        internal PopupStoreExitConfirmation Exit => Root.GetComponent<PopupStoreExitConfirmation>();
        internal PopupStoreBusinessClock Clock => Get<PopupStoreBusinessClock>("Clock");
        internal PopupStoreCustomerEntrance Entrance => Get<PopupStoreCustomerEntrance>("Entrance");
        internal PopupStoreCustomerPatience Patience => Get<PopupStoreCustomerPatience>("Patience");
        internal PopupStoreOrderDisplay Orders => Get<PopupStoreOrderDisplay>("Orders");
        internal PopupStoreSalesCounter Counter => Get<PopupStoreSalesCounter>("Counter");
        internal PopupStorePacking Packing => Get<PopupStorePacking>("Packing");
        internal PopupStorePackingPresentation Presentation => Get<PopupStorePackingPresentation>("Presentation");
        internal PopupStoreScore Score => Get<PopupStoreScore>("Score");
        internal PopupStoreGradeSession Grade => Get<PopupStoreGradeSession>("Grade");
        internal PopupStoreResultPanel Result => Get<PopupStoreResultPanel>("Result");
        internal PopupStoreOrder[] OrdersData => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal Button ContinueButton => (Button)Read(Exit, "_continueButton");
        internal Button AbandonButton => (Button)Read(Exit, "_abandonButton");
        internal T Get<T>(string name) => (T)Read(_inner, name);
        internal object Call(string name, params object[] args) => Invoke(_inner, name, args);
        internal void Start() { Call("Start", 90d); Exit.Refresh(); }
        internal void StartThree() { Call("StartCustomers", 3, 90d); Exit.Refresh(); }
        internal void Request() { Assert.That(View.TryRequestExit(), Is.True); Assert.That(Exit.IsVisible, Is.True); }
        internal void Continue() { Assert.That(Exit.TryContinue(), Is.True); Assert.That(Exit.IsVisible, Is.False); }
        internal void Fill(int slot)
        {
            Assert.That(OrdersData[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            foreach (var material in materials) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal void TickAll(float delta)
        { Clock.AdvanceTime(delta); Entrance.TickEntrance(delta); Presentation.TickPresentation(delta); Patience.Refresh(); }
        internal void CompleteDelivery() { Call("CompleteVisual", true); Call("FinishDepartures"); }
        internal void ShowResult() { Call("ShowResult"); Result.TickResult(1); }
        internal void Reopen() { Call("Reopen"); Exit.Refresh(); }
        internal void ConfirmResult()
        {
            Assert.That(Result.TryConfirm(), Is.True); Call("DisableObservers");
            Assert.That(Navigation.FirstView, Is.SameAs(Floor)); Assert.That(Exit.IsVisible, Is.False);
        }
        internal PopupStoreMaterialItem Item(string id) => Root.GetComponentsInChildren<PopupStoreMaterialItem>(true).Single(i => i.Material != null && i.Material.Id == id);
        internal UINavigationCoordinator Coordinator(bool emptyTop = false, UINavigation selected = null)
        {
            var go = new GameObject("exit-confirmation-coordinator"); go.transform.SetParent(Navigation.transform, false);
            var coordinator = go.AddComponent<UINavigationCoordinator>(); var data = new NavigationData(); Write(data, "_uiNav", selected != null ? selected : Navigation);
            var entries = new List<NavigationData> { data };
            if (emptyTop)
            {
                var emptyGo = new GameObject("exit-confirmation-empty-navigation"); emptyGo.transform.SetParent(Navigation.transform, false);
                var empty = emptyGo.AddComponent<MobileUINavigation>(); Write(empty, "_uiViews", new MobileViewDicStruct[0]); Invoke(empty, "Init");
                var top = new NavigationData(); Write(top, "_uiNav", empty); entries.Add(top);
            }
            Write(coordinator, "_navDatas", entries.ToArray()); Invoke(coordinator, "Init"); return coordinator;
        }
        public void Dispose() => ((IDisposable)_inner).Dispose();
    }

    private sealed class Snapshot
    {
        private readonly double _remaining, _elapsed, _penalty;
        private readonly int[] _counts;
        private readonly object[] _slots;
        private readonly double?[] _patience;
        private readonly PopupStoreMaterialEntry[] _materials;
        private readonly PopupStorePackingReceipt[] _committed, _queued;
        private readonly PopupStoreVisualOutcome[] _outcomes;
        private readonly PopupStorePackingReceipt _receipt;
        private readonly RectTransform _target;
        private readonly PopupStorePackingPresentation.Phase _phase;
        private readonly float _phaseElapsed, _visibleElapsed;
        private readonly Vector3 _position, _scale;
        private readonly Color _color;
        private readonly bool _imageActive;
        internal Snapshot(Host h)
        {
            _remaining = h.Clock.RemainingSeconds; _elapsed = h.Clock.ElapsedSeconds; _penalty = h.Clock.ComplaintPenaltySeconds;
            _counts = Counts(h); _slots = Slots(h); _patience = Patience(h); _materials = h.Counter.Materials.ToArray();
            _committed = h.Packing.CommittedReceipts.ToArray(); _queued = h.Queue.ToArray(); _outcomes = h.Presentation.VisualOutcomes.ToArray();
            _receipt = h.Presentation.CurrentReceipt; _target = h.Presentation.CurrentTarget; _phase = h.Presentation.CurrentPhase;
            _phaseElapsed = (float)Read(h.Presentation, "_elapsed"); _visibleElapsed = (float)Read(h.Entrance, "_visibleElapsed");
            var image = (Image)Read(h.Presentation, "_resultImage"); _position = image.rectTransform.anchoredPosition3D;
            _scale = image.rectTransform.localScale; _color = image.color; _imageActive = image.gameObject.activeSelf;
        }
        internal void AssertSame(Host h)
        {
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(_remaining)); Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(_elapsed));
            Assert.That(h.Clock.ComplaintPenaltySeconds, Is.EqualTo(_penalty)); CollectionAssert.AreEqual(_counts, Counts(h));
            CollectionAssert.AreEqual(_slots, Slots(h)); CollectionAssert.AreEqual(_patience, Patience(h));
            CollectionAssert.AreEqual(_materials, h.Counter.Materials.ToArray()); CollectionAssert.AreEqual(_committed, h.Packing.CommittedReceipts.ToArray());
            CollectionAssert.AreEqual(_queued, h.Queue.ToArray()); CollectionAssert.AreEqual(_outcomes, h.Presentation.VisualOutcomes.ToArray());
            Assert.That(h.Presentation.CurrentReceipt, Is.SameAs(_receipt)); Assert.That(h.Presentation.CurrentTarget, Is.SameAs(_target));
            Assert.That(h.Presentation.CurrentPhase, Is.EqualTo(_phase)); Assert.That(Read(h.Presentation, "_elapsed"), Is.EqualTo(_phaseElapsed));
            Assert.That(Read(h.Entrance, "_visibleElapsed"), Is.EqualTo(_visibleElapsed));
            var image = (Image)Read(h.Presentation, "_resultImage"); Assert.That(image.rectTransform.anchoredPosition3D, Is.EqualTo(_position));
            Assert.That(image.rectTransform.localScale, Is.EqualTo(_scale)); Assert.That(image.color, Is.EqualTo(_color)); Assert.That(image.gameObject.activeSelf, Is.EqualTo(_imageActive));
        }
        private static int[] Counts(Host h) => new[] { h.Clock.EndedCount, h.Clock.ComplaintPenaltyCount, h.Patience.ExpiredCount,
            h.Entrance.SuppliedOrderCount, h.Entrance.StartedEntranceCount, h.Entrance.ArrivedCount, h.Entrance.StartedDepartureCount,
            h.Entrance.StartedComplaintCount, h.Entrance.ReleasedCount, h.Packing.SuccessfulPackCount, h.Score.TotalScore,
            h.Score.SuccessfulOrderCount, h.Presentation.StartedPresentationCount, h.Presentation.CompletedDeliveryCount, h.Grade.FinalizationCount };
        private static double?[] Patience(Host h) => Enumerable.Range(0, 3).Select(i => h.Patience.GetTimer(i)?.RemainingPatience).ToArray();
        private static object[] Slots(Host h)
        {
            var values = new List<object>();
            foreach (var slot in (Array)Read(h.Entrance, "_slots"))
            {
                if (slot == null) { values.Add(null); continue; }
                foreach (string name in new[] { "Order", "OrderId", "CustomerId", "State", "Progress", "StartTime", "Released", "ExitElapsed", "ExitProgress", "DepartureReceiptId", "DepartureReason", "ComplaintElapsed" }) values.Add(Read(slot, name));
                var image = (Image)Read(slot, "Image"); values.Add(image.rectTransform.anchoredPosition3D); values.Add(image.gameObject.activeSelf);
                var complaint = (Image)Read(slot, "ComplaintIcon");
                if (complaint != null) { values.Add(complaint.rectTransform.localScale); values.Add(complaint.gameObject.activeSelf); }
            }
            return values.ToArray();
        }
    }
    private static void External(PopupStoreBusinessClock clock, string reason, bool paused)
    { if (reason == "focus") clock.SetApplicationFocused(!paused); else clock.SetApplicationPaused(paused); }
    private static PointerEventData Pointer() => new PointerEventData(null) { button = PointerEventData.InputButton.Left,
        pointerId = 23, position = new Vector2(100, 100), eligibleForClick = true, dragging = false };
    private static string AccountState() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
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

/// <summary>Ordinary UI lifecycle probe; no popup policy or animated map dependencies.</summary>
public sealed class PopupStoreExitUnrelatedTestView : MobileUIView
{
    public int Hides { get; private set; }
    public override void Init() { base.OnValidate(); VisibleState = VisibleState.Disappeared; }
    public override void Show() { gameObject.SetActive(true); VisibleState = VisibleState.Appeared; }
    public override void Hide() { Hides++; VisibleState = VisibleState.Disappeared; gameObject.SetActive(false); }
}
#endif
