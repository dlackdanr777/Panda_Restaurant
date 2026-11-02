#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Original popup prefab, real sales and monotonic fake time only. No scene creation,
/// account persistence, start-authority calls, SDK requests or global time-scale changes.</summary>
public sealed class PopupStorePauseTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private Dictionary<string, string> _protected;
    private string _account;
    private float _timeScale;
    private bool _sdk;
    private BackendManager _backend;
    private UnityEngine.Random.State _random;

    [OneTimeSetUp]
    public void RecordProtectedRewardPolicyScoreAndApprovedArt()
    {
        string[] names = { "PopupStoreRewardTable.cs", "PopupStoreRewardDecision.cs", "PopupStoreRewardSession.cs",
            "PopupStoreRewardPersistence.cs", "PopupStoreRewardTransaction.cs", "PopupStoreRewardRecovery.cs",
            "PopupStoreBusinessAccessPolicy.cs", "PopupStoreDailyUsage.cs", "PopupStoreBusinessStartTransaction.cs",
            "PopupStoreGradeSession.cs", "PopupStoreScore.cs", "PopupStoreResultPanel.cs", "PopupStoreRewardBox.cs", "PopupStoreRewardRevealPreview.cs" };
        var paths = names.Select(name => "Assets/Scripts/UI/Floor3UI/" + name).ToList();
        paths.Add("Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset");
        foreach (string grade in new[] { "C", "B", "A", "S", "SS", "SSS" })
            paths.Add("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png");
        _protected = paths.ToDictionary(path => path, Hash);
    }
    [OneTimeTearDown]
    public void ProtectedFilesAreUnchangedByPauseValidation()
    { foreach (var pair in _protected) Assert.That(Hash(pair.Key), Is.EqualTo(pair.Value), pair.Key); }
    [SetUp]
    public void RecordLocalMemoryWithoutInitializingAnySdk()
    { _account = AccountState(); _timeScale = Time.timeScale; _sdk = BackEnd.Backend.IsInitialized; _backend = BackendManager.ExistingInstance; _random = UnityEngine.Random.state; }
    [TearDown]
    public void PauseNeverChangesActualCurrencyDailyDataSdkOrGlobalTime()
    {
        Assert.That(AccountState(), Is.EqualTo(_account)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk)); Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void ActualPrefabUsesItsExistingPauseButtonAndApprovedFontWithoutEnablingDisplayOnlyControls()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); var pause = prefab.GetComponent<PopupStorePauseController>();
        Assert.That(pause, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<PopupStorePauseController>(true), Has.Length.EqualTo(1));
        Assert.That(Read(pause, "_clock"), Is.SameAs(prefab.GetComponent<PopupStoreBusinessClock>()));
        var button = (Button)Read(pause, "_pauseButton"); var label = (TMP_Text)Read(pause, "_label");
        Assert.That(button.name, Is.EqualTo("Pause Button")); Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null);
        Assert.That(label.font, Is.SameAs(((TMP_Text)Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_timeText")).font));
        Assert.That(label.raycastTarget, Is.False); Assert.That(label.transform.IsChildOf(prefab.transform), Is.True);
        using (var host = new Host())
        {
            host.Start(); Assert.That(host.Button.IsInteractable(), Is.True);
            foreach (var other in (Button[])Read(host.View, "_displayOnlyButtons"))
                if (other != null && other != host.Button) Assert.That(other.interactable, Is.False, other.name);
        }
    }

    [Test]
    public void RunningButtonPausesAndTheSameButtonResumesWithoutResettingBusiness()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Clock.AdvanceTime(2); var before = new Frozen(host);
            host.Click(); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused)); Assert.That(host.Clock.IsManualPaused, Is.True);
            Assert.That(host.Label.text, Does.Contain("계속하기")); Assert.That(host.Button.IsInteractable(), Is.True);
            host.Click(); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running)); Assert.That(host.Clock.IsManualPaused, Is.False);
            Assert.That(host.Label.text, Does.Contain("일시정지")); Assert.That(host.Pause.ToggleCount, Is.EqualTo(2)); before.AssertUnchanged(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NotStartedAndEndedRejectManualPauseWithoutBlockingLaterPermittedWork(bool ended)
    {
        using (var host = new Host())
        {
            if (ended) { host.Start(); host.Clock.AdvanceTime(90); }
            host.Pause.Refresh(); Assert.That(host.Button.IsInteractable(), Is.False); Assert.That(host.Pause.TryTogglePause(), Is.False);
            host.Clock.SetManualPaused(true); Assert.That(host.Clock.IsManualPaused, Is.False); Assert.That(host.Pause.ToggleCount, Is.Zero);
            if (ended) { Assert.That(host.Clock.CanAdvanceVisuals, Is.True); host.ShowResult(); host.Confirm(); }
            else { host.Start(); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running)); }
        }
    }

    [Test]
    public void RepeatedRefreshKeepsOneClockSubscriptionAndSameFrameButtonSpamTogglesOnce()
    {
        using (var host = new Host())
        {
            host.Start(); for (int i = 0; i < 20; i++) Assert.That(host.Pause.Refresh(), Is.True);
            var listeners = (Action)Read(host.Clock, "StateChanged");
            Assert.That(listeners.GetInvocationList().Count(d => ReferenceEquals(d.Target, host.Pause)), Is.EqualTo(1));
            host.NextFrame(); for (int i = 0; i < 20; i++) host.Button.onClick.Invoke();
            Assert.That(host.Clock.IsManualPaused, Is.True); Assert.That(host.Pause.ToggleCount, Is.EqualTo(1));
            host.Click(); Assert.That(host.Clock.IsManualPaused, Is.False); Assert.That(host.Pause.ToggleCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void ReentrantStateObserverCannotToggleTheClockBackDuringTheFirstClick()
    {
        using (var host = new Host())
        {
            host.Start(); int calls = 0; bool nested = true;
            Action observer = () => { calls++; host.NextFrame(); nested = host.Pause.TryTogglePause(); };
            host.Clock.StateChanged += observer;
            try { host.Toggle(); } finally { host.Clock.StateChanged -= observer; }
            Assert.That(calls, Is.EqualTo(1)); Assert.That(nested, Is.False); Assert.That(host.Clock.IsManualPaused, Is.True);
            Assert.That(host.Pause.ToggleCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void PauseFirstSettlesForegroundTimeAndResumeNeverChargesTheRestInterval()
    {
        using (var host = new Host())
        {
            double now = 100; Write(host.Clock, "_timeSource", new Func<double>(() => now)); host.StartThree();
            now = 102.25; host.Toggle();
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(87.75d)); Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(2.25d));
            Assert.That(host.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(15.75d)); var frozen = new Frozen(host);
            now = 10000; host.Clock.SynchronizeNow(); host.TickAll(100); frozen.AssertUnchanged(host);
            host.Toggle(); host.Clock.SynchronizeNow(); frozen.AssertUnchanged(host);
            now += .25; host.Clock.SynchronizeNow(); Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(87.5d));
            Assert.That(host.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(15.5d));
        }
    }

    [TestCase("focus")]
    [TestCase("application")]
    [TestCase("view")]
    public void ExternalPauseReasonsFreezeClockAndAllProgressWithoutResumeCatchup(string reason)
    {
        using (var host = new Host())
        {
            double now = 0; Write(host.Clock, "_timeSource", new Func<double>(() => now)); host.StartThree();
            host.Fill(0); Assert.That(host.Packing.TryPack().Success, Is.True); host.ToPhase("Move"); now = .25;
            Condition(host.Clock, reason, true); var frozen = new Frozen(host); now = 999;
            host.TickAll(100); host.Clock.SynchronizeNow(); frozen.AssertUnchanged(host);
            Condition(host.Clock, reason, false); host.Clock.SynchronizeNow(); frozen.AssertUnchanged(host);
            now += .5; host.Clock.SynchronizeNow(); Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(.75d));
            host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
        }
    }

    [TestCase("focus")]
    [TestCase("application")]
    [TestCase("view")]
    public void ClearingOnePauseReasonCannotResumeWhileManualOrExternalPauseStillExists(string reason)
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Toggle(); Condition(host.Clock, reason, true);
            Condition(host.Clock, reason, false); Assert.That(host.Clock.IsManualPaused, Is.True);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Condition(host.Clock, reason, true); host.Toggle(); Assert.That(host.Clock.IsManualPaused, Is.False);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            var frozen = new Frozen(host); host.TickAll(100); frozen.AssertUnchanged(host);
            Condition(host.Clock, reason, false); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [Test]
    public void AHiddenPopupCannotResumeOnlyBecauseApplicationAndFocusReturn()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Clock.SetApplicationPaused(true); host.Clock.SetApplicationFocused(false); host.Close();
            host.Clock.SetApplicationPaused(false); host.Clock.SetApplicationFocused(true);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused)); Assert.That(host.Clock.CanAdvanceVisuals, Is.False);
            var frozen = new Frozen(host); host.TickAll(100); frozen.AssertUnchanged(host);
            host.Reopen(); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
        }
    }

    [Test]
    public void ManualPauseAndWaitingOrdersSurviveNormalCloseAndSameInstanceReentry()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); host.Clock.AdvanceTime(2); host.Toggle(); var frozen = new Frozen(host);
            var orders = host.SourceOrders.ToArray(); host.Close(); host.Clock.SetApplicationFocused(false); host.Clock.SetApplicationFocused(true); host.Reopen();
            Assert.That(host.Clock.IsManualPaused, Is.True); Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            CollectionAssert.AreEqual(orders, host.SourceOrders); frozen.AssertUnchanged(host);
            Assert.That(host.Button.IsInteractable(), Is.True); host.Toggle(); Assert.That(host.Packing.TryPack().Success, Is.True);
            host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
        }
    }

    [TestCase("manual")]
    [TestCase("focus")]
    [TestCase("application")]
    public void FirstAutomaticVisualDeltaAfterResumeIsDiscardedButFollowingDeltaIsAccepted(string reason)
    {
        using (var host = new Host())
        {
            host.Start(); uint observed = host.Clock.VisualResumeVersion;
            Assert.That(host.Clock.FilterAutomaticVisualDelta(.1f, ref observed), Is.EqualTo(.1f));
            Condition(host.Clock, reason, true); Condition(host.Clock, reason, false);
            Assert.That(host.Clock.FilterAutomaticVisualDelta(300f, ref observed), Is.Zero);
            Assert.That(observed, Is.EqualTo(host.Clock.VisualResumeVersion));
            Assert.That(host.Clock.FilterAutomaticVisualDelta(.125f, ref observed), Is.EqualTo(.125f));
        }
    }

    [Test]
    public void DeadlineWonDuringPauseClickCannotLatchManualPauseOrBlockCommittedDelivery()
    {
        using (var host = new Host())
        {
            double now = 0; Write(host.Clock, "_timeSource", new Func<double>(() => now)); host.StartThree();
            host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.ToPhase("Move");
            now = 100; host.NextFrame(); Assert.That(host.Pause.TryTogglePause(), Is.False);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended)); Assert.That(host.Clock.IsManualPaused, Is.False);
            Assert.That(host.Clock.CanAdvanceVisuals, Is.True); Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
            host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            host.ShowResult(); Assert.That(host.Result.ShownResult.SuccessfulOrders, Is.EqualTo(1)); host.Confirm();
        }
    }

    [Test]
    public void PausedEntryIntervalCannotSupplyMoreOrdersAndResumesFromItsExactVisibleTime()
    {
        using (var host = new Host())
        {
            host.Start(); host.Entrance.TickEntrance(0); host.Entrance.TickEntrance(1); host.Toggle(); var frozen = new Frozen(host);
            for (int i = 0; i < 10; i++) host.TickAll(90); frozen.AssertUnchanged(host);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1)); host.Toggle();
            host.Entrance.TickEntrance(2.9f); Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            host.Entrance.TickEntrance(.11f); Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void EnteringCustomerCannotArriveOrAcquireAnOrderWhilePaused()
    {
        using (var host = new Host())
        {
            host.Start(); host.Entrance.TickEntrance(0); float duration = (float)Read(host.Entrance, "_entryDuration");
            host.Entrance.TickEntrance(duration * .25f); Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            host.Toggle(); var frozen = new Frozen(host); host.TickAll(100); frozen.AssertUnchanged(host);
            Assert.That(host.Patience.GetTimer(0), Is.Null); Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            host.Toggle(); host.Entrance.TickEntrance(duration * .5f); Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            host.Entrance.TickEntrance(duration * .26f); Assert.That(host.Entrance.IsWaiting(0), Is.True);
            Assert.That(host.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(18d));
        }
    }

    [Test]
    public void WaitingPatienceAndItsGaugeCannotExpireDuringPauseButResumeFromTheRemainingValue()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Clock.AdvanceTime(17.75); host.Toggle(); var timer = host.Patience.GetTimer(0); var frozen = new Frozen(host);
            Assert.That(timer.RemainingPatience, Is.EqualTo(.25d)); host.TickAll(1000); host.Patience.Refresh(); frozen.AssertUnchanged(host);
            Assert.That(timer.Order.IsExpired, Is.False); Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            host.Toggle(); host.Clock.AdvanceTime(.25); Assert.That(timer.Order.IsExpired, Is.True);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1)); Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
        }
    }

    [Test]
    public void NormalLeavingCustomerCannotReleaseOrRefillItsSlotDuringPause()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.Call("CompleteVisual", true);
            float duration = (float)Read(host.Entrance, "_exitDuration"); host.Entrance.TickEntrance(duration * .25f);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving)); host.Toggle(); var frozen = new Frozen(host);
            host.TickAll(1000); frozen.AssertUnchanged(host); Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Toggle(); host.Entrance.TickEntrance(duration * .5f); Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Entrance.TickEntrance(duration * .26f); Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt)); Assert.That(host.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ExpiredComplaintFreezesItsIconMotionAndPenaltyIdentityUntilResume()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Clock.AdvanceTime(18); float duration = (float)Read(host.Entrance, "_complaintDuration");
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired)); host.Entrance.TickEntrance(duration * .25f);
            host.Toggle(); var frozen = new Frozen(host); host.TickAll(100); host.Patience.Refresh(); frozen.AssertUnchanged(host);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1)); Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero); host.Toggle(); host.Entrance.TickEntrance(duration * .5f);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired)); host.Entrance.TickEntrance(duration * .26f);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving)); host.Call("FinishDepartures");
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1)); Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void EveryCommittedPackingPhaseFreezesReceiptTargetTransformAndElapsedThenCompletesOnce(string phase)
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.ToPhase(phase); host.Toggle();
            var frozen = new Frozen(host); for (int i = 0; i < 4; i++) host.TickAll(100); frozen.AssertUnchanged(host);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt)); Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Packed));
            host.Toggle(); host.CompleteDeliveryAndDeparture();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1)); Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes[0].Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.Presentation.VisualOutcomes[0].Receipt, Is.SameAs(receipt)); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1)); Assert.That(host.Score.TotalScore, Is.EqualTo(100)); Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void PausingBeforePresentationDoesNotDequeueOrCancelAnAlreadyCommittedReceipt()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.Toggle();
            Assert.That(host.Queue.Single(), Is.SameAs(receipt)); host.TickAll(100);
            Assert.That(host.Queue.Single(), Is.SameAs(receipt)); Assert.That(host.Presentation.CurrentReceipt, Is.Null);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero); Assert.That(host.Presentation.VisualOutcomes, Is.Empty);
            host.Toggle(); host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void NativeBoomAnimatorSpeedFreezesImmediatelyAndRestoresItsOriginalNonDefaultValue()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); Assert.That(host.Packing.TryPack().Success, Is.True); host.ToPhase("Boom");
            var animator = (Animator)Read(host.Presentation, "_boomAnimator"); animator.speed = .73f;
            host.Toggle(); Assert.That(animator.speed, Is.Zero); host.Presentation.TickPresentation(100); Assert.That(animator.speed, Is.Zero);
            host.Clock.SetApplicationFocused(false); host.Toggle(); Assert.That(animator.speed, Is.Zero);
            host.Clock.SetApplicationFocused(true); Assert.That(animator.speed, Is.EqualTo(.73f)); host.CompleteDeliveryAndDeparture();
        }
    }

    [Test]
    public void NativeAnimatorWaitsThroughTheResumeFrameWhileTheAutomaticLargeDeltaIsDiscarded()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.ToPhase("Boom");
            int frame = 100; Write(host.Presentation, "_nativeFrameSource", new Func<int>(() => frame));
            var animator = (Animator)Read(host.Presentation, "_boomAnimator"); animator.speed = .73f;
            foreach (string reason in new[] { "manual", "focus" })
            {
                uint observed = host.Clock.VisualResumeVersion;
                float elapsed = (float)Read(host.Presentation, "_elapsed");
                Condition(host.Clock, reason, true); Assert.That(animator.speed, Is.Zero);
                Condition(host.Clock, reason, false); Assert.That(animator.speed, Is.Zero, "The native Animator must not consume the resume frame's stale unscaled delta.");
                float filtered = host.Clock.FilterAutomaticVisualDelta(1000f, ref observed);
                Assert.That(filtered, Is.Zero); host.Presentation.TickPresentation(filtered);
                Assert.That(animator.speed, Is.Zero); Assert.That(Read(host.Presentation, "_elapsed"), Is.EqualTo(elapsed));
                Assert.That(host.Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Boom));
                Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
                frame++;
                host.Presentation.TickPresentation(host.Clock.FilterAutomaticVisualDelta(.01f, ref observed));
                Assert.That(animator.speed, Is.EqualTo(.73f)); Assert.That(Read(host.Presentation, "_elapsed"), Is.EqualTo(elapsed + .01f).Within(.00001f));
            }
            host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1)); Assert.That(host.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ASecondContextPauseCancelsPendingNativeResumeAndCloseRestoresSpeedWithoutDuplicateReceiptWork()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.ToPhase("Boom");
            int frame = 10; Write(host.Presentation, "_nativeFrameSource", new Func<int>(() => frame));
            var animator = (Animator)Read(host.Presentation, "_boomAnimator"); animator.speed = .57f;
            host.Toggle(); host.Clock.SetApplicationFocused(false); host.Toggle();
            frame = 1000; host.Presentation.TickPresentation(0); Assert.That(animator.speed, Is.Zero);
            host.Clock.SetApplicationFocused(true); Assert.That(animator.speed, Is.Zero);
            host.Clock.SetApplicationPaused(true); frame = 2000; host.Presentation.TickPresentation(0); Assert.That(animator.speed, Is.Zero);
            host.Clock.SetApplicationPaused(false); host.Presentation.TickPresentation(0); Assert.That(animator.speed, Is.Zero);
            // Existing direct-close cancellation is preserved; this test does not introduce a new abandonment policy.
            host.Close(); Assert.That(animator.speed, Is.EqualTo(.57f)); Assert.That(host.Presentation.CurrentReceipt, Is.Null);
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes[0].Receipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.VisualOutcomes[0].Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1)); Assert.That(host.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void CoreMaterialClearAndPackingApisRejectPausedMutationsButReuseThePreparedOrderAfterResume()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var materials = host.Counter.Materials.ToArray(); host.Toggle();
            Assert.That(host.Counter.CanAcceptInput, Is.False); Assert.That(host.Counter.TryAdd(materials[0]), Is.False);
            host.Counter.Clear(); host.ClearButton.onClick.Invoke(); host.PackButton.onClick.Invoke();
            Assert.That(host.Packing.TryPack().Success, Is.False); CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero); Assert.That(host.Score.TotalScore, Is.Zero);
            Assert.That(host.ClearButton.IsInteractable(), Is.False); Assert.That(host.PackButton.IsInteractable(), Is.False);
            host.Toggle(); Assert.That(host.Counter.CanAcceptInput, Is.True); Assert.That(host.ClearButton.IsInteractable(), Is.True);
            var packed = host.Packing.TryPack(); Assert.That(packed.Success, Is.True); Assert.That(host.Counter.Count, Is.Zero);
            host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(packed.Receipt));
        }
    }

    [TestCase("manual")]
    [TestCase("focus")]
    [TestCase("application")]
    public void MaterialClickStartedBeforeSuspensionCannotCompleteAfterTheContextReturns(string reason)
    {
        using (var host = new Host())
        {
            host.StartThree(); var item = host.Item("STAFF01"); var data = Pointer(); item.OnPointerDown(data);
            Condition(host.Clock, reason, true); Condition(host.Clock, reason, false); item.OnPointerClick(data);
            Assert.That(host.Counter.Count, Is.Zero); Assert.That(host.Score.TotalScore, Is.Zero);
            item.OnPointerDown(data); item.OnPointerClick(data); Assert.That(host.Counter.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void PausedNativeClicksAndNonTransactionalScrollCannotChangeMaterialsOrMatchFeedback()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Toggle(); int feedback = 0; host.Progress.FeedbackTriggered += () => feedback++;
            var lists = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true); var otherPositions = lists.Select(l => ((ScrollRect)Read(l, "_scrollRect")).horizontalNormalizedPosition).ToArray();
            var item = host.Item("STAFF01"); var data = Pointer(); item.OnPointerDown(data); item.OnPointerClick(data);
            var list = item.Owner; var scroll = (ScrollRect)Read(list, "_scrollRect");
            list.OnBeginDrag(data); list.OnDrag(data); data.scrollDelta = Vector2.down; list.OnScroll(data);
            ((Button)Read(list, "_nextButton")).onClick.Invoke();
            Assert.That(host.Counter.Count, Is.Zero); Assert.That(feedback, Is.Zero); Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            for (int i = 0; i < lists.Length; i++) if (lists[i] != list)
                Assert.That(((ScrollRect)Read(lists[i], "_scrollRect")).horizontalNormalizedPosition, Is.EqualTo(otherPositions[i]));
            Assert.That(scroll.horizontalNormalizedPosition, Is.InRange(0f, 1f));
        }
    }

    [Test]
    public void TenPauseResumeCyclesCannotAccumulateTimeChangeReceiptsOrResetOrders()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var frozen = new Frozen(host);
            for (int i = 0; i < 10; i++) { host.Toggle(); host.TickAll(500); host.Toggle(); }
            frozen.AssertUnchanged(host); Assert.That(host.Pause.ToggleCount, Is.EqualTo(20)); Assert.That(host.Clock.EndedCount, Is.Zero);
        }
    }

    [Test]
    public void EndedFocusLossStillFreezesVisualsAndFocusReturnFinishesTheOriginalReceipt()
    {
        using (var host = new Host())
        {
            host.StartThree(); host.Fill(0); var receipt = host.Packing.TryPack().Receipt; host.ToPhase("Move"); host.Clock.AdvanceTime(90);
            host.Clock.SetApplicationFocused(false); var frozen = new Frozen(host); host.TickAll(100); frozen.AssertUnchanged(host);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended)); Assert.That(host.Result.IsVisible, Is.False);
            host.Clock.SetApplicationFocused(true); host.CompleteDeliveryAndDeparture(); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            host.ShowResult(); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1)); host.Confirm();
        }
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.S)]
    public void PauseDoesNotChangeFinalGradeApprovedBoxReadOnlyRewardRevealOrExistingFloor3Return(PopupStoreGradeTier grade)
    {
        using (var host = new Host())
        {
            var reward = host.Root.GetComponent<PopupStoreRewardSession>(); var random = new Source();
            Assert.That(reward.TrySetRandomSource(random), Is.True); Assert.That(reward.Refresh(), Is.True);
            host.Start(); host.Toggle(); host.TickAll(100); host.Toggle(); host.Call("SellUntil", grade); host.Call("End"); host.ShowResult();
            var final = host.Grade.FinalResult; var decision = reward.Result; var reveal = host.Root.GetComponent<PopupStoreRewardRevealPreview>();
            Assert.That(host.Button.IsInteractable(), Is.False); Assert.That(host.Pause.TryTogglePause(), Is.False);
            Assert.That(reveal.TryReveal(), Is.True); reveal.TickPreview(1); Assert.That(reveal.ShownDecision, Is.SameAs(decision));
            var box = host.Root.GetComponent<PopupStoreRewardBox>(); Assert.That(box.DisplayedSprite.name, Is.EqualTo("PopupRewardBox_" + grade + "_APPROVED"));
            host.Confirm(); host.Reopen(); host.Result.TickResult(0); reveal.Refresh();
            Assert.That(reveal.IsPreviewVisible, Is.True); Assert.That(reveal.IsAnimating, Is.False); Assert.That(reveal.RevealCount, Is.EqualTo(1));
            Assert.That(host.Grade.FinalResult, Is.SameAs(final)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(reward.Result, Is.SameAs(decision)); Assert.That(reward.DecisionCount, Is.EqualTo(1));
            Assert.That(reward.RandomDrawCount, Is.EqualTo(1)); Assert.That(random.Calls, Is.EqualTo(1)); host.Confirm();
        }
    }

    [TestCase("_clock")]
    [TestCase("_pauseButton")]
    [TestCase("_label")]
    public void MissingPauseReferencesCannotMutateTheClockOrBlockTheNormalClosePath(string field)
    {
        using (var host = new Host())
        {
            host.StartThree(); var before = new Frozen(host); object original = Read(host.Pause, field); Write(host.Pause, field, null);
            try { Assert.That(host.Pause.Refresh(), Is.False); Assert.That(host.Pause.TryTogglePause(), Is.False); before.AssertUnchanged(host); }
            finally { Write(host.Pause, field, original); host.Pause.Refresh(); }
            host.Close(); Assert.That(host.Get<MobileUINavigation>("Navigation").FirstView, Is.SameAs(host.Get<UIFloor3>("Floor")));
        }
    }

    [Test]
    public void PauseInputIsAPlayerCapablePathAndAutomaticVisualUpdatesUseTheResumeDeltaGate()
    {
        string controller = File.ReadAllText("Assets/Scripts/UI/Floor3UI/PopupStorePauseController.cs");
        string playerSource = Regex.Replace(controller, @"#if UNITY_EDITOR[\s\S]*?#endif", string.Empty);
        Assert.That(playerSource, Does.Contain("public bool TryTogglePause()"));
        Assert.That(playerSource, Does.Contain("_clock.SetManualPaused(!before)"), "Only diagnostics may be Editor-only; the pause input remains in Player source.");
        foreach (string name in new[] { "PopupStoreCustomerEntrance.cs", "PopupStorePackingPresentation.cs" })
            Assert.That(File.ReadAllText("Assets/Scripts/UI/Floor3UI/" + name), Does.Contain("FilterAutomaticVisualDelta"), name);
        Assert.That(controller, Does.Not.Contain("Time.timeScale").And.Not.Contain("UserInfo.").And.Not.Contain("Backend."));
    }

    private sealed class Source : IPopupStoreRewardRandomSource
    { internal int Calls; public int NextPercent() { Calls++; return 85; } }
    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        private int _frame = 100;
        internal Host()
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            try { Assert.That(Pause, Is.Not.Null); Write(Pause, "_frameSource", new Func<int>(() => _frame)); Assert.That(Pause.Refresh(), Is.True); }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => Get<GameObject>("Root");
        internal UIPopupStore View => Get<UIPopupStore>("View");
        internal PopupStoreBusinessClock Clock => Get<PopupStoreBusinessClock>("Clock");
        internal PopupStorePauseController Pause => Root.GetComponent<PopupStorePauseController>();
        internal PopupStoreCustomerEntrance Entrance => Get<PopupStoreCustomerEntrance>("Entrance");
        internal PopupStoreCustomerPatience Patience => Get<PopupStoreCustomerPatience>("Patience");
        internal PopupStoreOrderDisplay Orders => Get<PopupStoreOrderDisplay>("Orders");
        internal PopupStoreOrderProgress Progress => Get<PopupStoreOrderProgress>("Progress");
        internal PopupStoreSalesCounter Counter => Get<PopupStoreSalesCounter>("Counter");
        internal PopupStorePacking Packing => Get<PopupStorePacking>("Packing");
        internal PopupStorePackingPresentation Presentation => Get<PopupStorePackingPresentation>("Presentation");
        internal PopupStoreScore Score => Get<PopupStoreScore>("Score");
        internal PopupStoreGradeSession Grade => Get<PopupStoreGradeSession>("Grade");
        internal PopupStoreResultPanel Result => Get<PopupStoreResultPanel>("Result");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal Button Button => (Button)Read(Pause, "_pauseButton");
        internal TMP_Text Label => (TMP_Text)Read(Pause, "_label");
        internal Button ClearButton => (Button)Read(Counter, "_clearButton");
        internal Button PackButton => (Button)Read(Packing, "_packButton");
        internal T Get<T>(string name) => (T)Read(_inner, name);
        internal object Call(string name, params object[] args) => CallMethod(_inner, name, args);
        internal void Start() { Call("Start", 90d); Pause.Refresh(); }
        internal void StartThree() { Call("StartCustomers", 3, 90d); Pause.Refresh(); }
        internal void NextFrame() { _frame++; }
        internal void Click() { NextFrame(); Button.onClick.Invoke(); }
        internal void Toggle() { NextFrame(); Assert.That(Pause.TryTogglePause(), Is.True); }
        internal void Fill(int slot)
        {
            Assert.That(SourceOrders[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            foreach (var material in materials) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal PopupStoreMaterialItem Item(string id) => Root.GetComponentsInChildren<PopupStoreMaterialItem>(true).Single(i => i.Material != null && i.Material.Id == id);
        internal void TickAll(float delta)
        { Clock.AdvanceTime(delta); Entrance.TickEntrance(delta); Presentation.TickPresentation(delta); Patience.Refresh(); }
        internal void ToPhase(string phase) => Call("AdvanceVisualTo", phase);
        internal void CompleteDeliveryAndDeparture() { Call("CompleteVisual", true); Call("FinishDepartures"); }
        internal void ShowResult() { Call("ShowResult"); Result.TickResult(1); }
        internal void Close() { Call("Close"); CallMethod(Pause, "OnDisable"); }
        internal void Reopen() { Call("Reopen"); Assert.That(Pause.Refresh(), Is.True); }
        internal void Confirm()
        {
            Assert.That(Result.TryConfirm(), Is.True); Call("DisableObservers"); CallMethod(Pause, "OnDisable");
            Assert.That(Get<MobileUINavigation>("Navigation").FirstView, Is.SameAs(Get<UIFloor3>("Floor")));
        }
        public void Dispose() => ((IDisposable)_inner).Dispose();
    }

    /// <summary>Observable state and animation positions, not a second implementation of time/packing.</summary>
    private sealed class Frozen
    {
        private readonly double _remaining, _elapsed, _penalty;
        private readonly int[] _counts;
        private readonly object[] _slots;
        private readonly double?[] _patience;
        private readonly float _visibleElapsed, _phaseElapsed;
        private readonly PopupStorePackingPresentation.Phase _phase;
        private readonly PopupStorePackingReceipt _receipt, _delivered;
        private readonly RectTransform _target;
        private readonly PopupStorePackingReceipt[] _committed, _queued;
        private readonly PopupStoreVisualOutcome[] _outcomes;
        private readonly PopupStoreMaterialEntry[] _materials;
        private readonly Vector3 _position, _scale;
        private readonly Color _color;
        private readonly bool _resultActive;
        internal Frozen(Host host)
        {
            _remaining = host.Clock.RemainingSeconds; _elapsed = host.Clock.ElapsedSeconds; _penalty = host.Clock.ComplaintPenaltySeconds;
            _counts = Counts(host); _slots = Slots(host); _patience = Patience(host);
            _visibleElapsed = (float)Read(host.Entrance, "_visibleElapsed"); _phaseElapsed = (float)Read(host.Presentation, "_elapsed");
            _phase = host.Presentation.CurrentPhase; _receipt = host.Presentation.CurrentReceipt; _target = host.Presentation.CurrentTarget;
            _delivered = host.Presentation.LastDeliveredReceipt; _committed = host.Packing.CommittedReceipts.ToArray(); _queued = host.Queue.ToArray();
            _outcomes = host.Presentation.VisualOutcomes.ToArray(); _materials = host.Counter.Materials.ToArray();
            var image = (Image)Read(host.Presentation, "_resultImage"); _position = image.rectTransform.anchoredPosition3D;
            _scale = image.rectTransform.localScale; _color = image.color; _resultActive = image.gameObject.activeSelf;
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(_remaining)); Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(_elapsed));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(_penalty)); CollectionAssert.AreEqual(_counts, Counts(host));
            CollectionAssert.AreEqual(_slots, Slots(host)); CollectionAssert.AreEqual(_patience, Patience(host));
            Assert.That(Read(host.Entrance, "_visibleElapsed"), Is.EqualTo(_visibleElapsed)); Assert.That(Read(host.Presentation, "_elapsed"), Is.EqualTo(_phaseElapsed));
            Assert.That(host.Presentation.CurrentPhase, Is.EqualTo(_phase)); Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(_receipt));
            Assert.That(host.Presentation.CurrentTarget, Is.SameAs(_target)); Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(_delivered));
            CollectionAssert.AreEqual(_committed, host.Packing.CommittedReceipts.ToArray()); CollectionAssert.AreEqual(_queued, host.Queue.ToArray());
            CollectionAssert.AreEqual(_outcomes, host.Presentation.VisualOutcomes.ToArray()); CollectionAssert.AreEqual(_materials, host.Counter.Materials.ToArray());
            var image = (Image)Read(host.Presentation, "_resultImage"); Assert.That(image.rectTransform.anchoredPosition3D, Is.EqualTo(_position));
            Assert.That(image.rectTransform.localScale, Is.EqualTo(_scale)); Assert.That(image.color, Is.EqualTo(_color)); Assert.That(image.gameObject.activeSelf, Is.EqualTo(_resultActive));
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
                foreach (string field in new[] { "Order", "OrderId", "CustomerId", "State", "Progress", "StartTime", "Released", "ExitElapsed", "ExitProgress", "DepartureReceiptId", "DepartureReason", "ComplaintElapsed" }) values.Add(Read(slot, field));
                var image = (Image)Read(slot, "Image"); values.Add(image.rectTransform.anchoredPosition3D); values.Add(image.gameObject.activeSelf);
                var complaint = (Image)Read(slot, "ComplaintIcon"); if (complaint != null) { values.Add(complaint.rectTransform.localScale); values.Add(complaint.gameObject.activeSelf); }
            }
            return values.ToArray();
        }
    }

    private static void Condition(PopupStoreBusinessClock clock, string reason, bool paused)
    {
        switch (reason)
        {
            case "manual": clock.SetManualPaused(paused); break;
            case "focus": clock.SetApplicationFocused(!paused); break;
            case "application": clock.SetApplicationPaused(paused); break;
            case "view": clock.SetViewVisible(!paused); break;
            default: throw new ArgumentOutOfRangeException(nameof(reason));
        }
    }
    private static PointerEventData Pointer() => new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerId = 11,
        position = new Vector2(100, 100), eligibleForClick = true, dragging = false, scrollDelta = Vector2.zero };
    private static string AccountState() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
        UserInfo.CustomerEncounterContextGeneration, UserInfo.DailyAddMoney, UserInfo.DailyCookCount, UserInfo.DailyCumulativeCustomerCount,
        UserInfo.DailyAdvertisingViewCount, UserInfo.DailyCleanCount, UserInfo.DailyAdGoldRewardCount, UserInfo.DailyAdDiaRewardCount });
    private static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object CallMethod(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
}
#endif
