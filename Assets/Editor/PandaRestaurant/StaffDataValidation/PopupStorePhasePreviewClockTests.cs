#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real prefab/approval/clock/patience consumers; the stub supplies only the isolated phase authority.</summary>
public sealed class PopupStorePhasePreviewClockTests
{
    [Test]
    public void UnboundAuthoredClockKeepsTheOrdinaryNinetySecondPath()
    {
        using (var h = new Host(false))
        {
            h.Start();
            Assert.That(h.Clock.IsPhasePreview, Is.False);
            Assert.That(h.Clock.PhasePreviewAuthority, Is.Null);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90d));
            h.Clock.AdvanceTime(90);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(h.Clock.EndedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void NullAuthorityCannotEnableThePreview()
    {
        using (var h = new Host(false))
        {
            Assert.That(h.Clock.TryBindPhasePreview(null), Is.False);
            Assert.That(h.Clock.IsPhasePreview, Is.False);
        }
    }

    [Test]
    public void BoundAuthorityCannotBeReplacedEvenBeforeStart()
    {
        using (var h = new Host())
        {
            Assert.That(h.Clock.TryBindPhasePreview(h.Authority), Is.True);
            Assert.That(h.Clock.TryBindPhasePreview(new Authority(h.Root)), Is.False);
            Assert.That(h.Clock.PhasePreviewAuthority, Is.SameAs(h.Authority));
        }
    }

    [Test]
    public void StartedOrdinaryBusinessCannotBecomeAPreview()
    {
        using (var h = new Host(false))
        {
            h.Start();
            Assert.That(h.Clock.TryBindPhasePreview(new Authority(h.Root)), Is.False);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90d));
        }
    }

    [Test]
    public void PreviewStartsAtTwentyTwoWithoutChangingSerializedNinetyOrHudFont()
    {
        using (var h = new Host())
        {
            var label = (TMP_Text)Read(h.Clock, "_timeText");
            var font = label.font;
            var position = label.rectTransform.anchoredPosition;
            h.Start();
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22d));
            Assert.That(Read(h.Clock, "_initialSeconds"), Is.EqualTo(90d));
            Assert.That(Read(h.Clock, "_entryCutoffSeconds"), Is.EqualTo(10d));
            Assert.That(label.text, Is.EqualTo("00:22"));
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(h.Root.GetComponents<PopupStoreBusinessClock>().Length, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedStartAndStageChangeConsumeOneIsolatedApproval()
    {
        using (var h = new Host())
        {
            h.Start();
            Assert.That(h.Clock.TryStartSession(), Is.False);
            h.ClearCurrentPhase();
            for (int i = 0; i < 4; i++) h.Clock.AdvanceTime(1);
            Assert.That(h.Authority.Model.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
            var admission = h.Root.GetComponent<PopupStoreStartController>().Host;
            Assert.That(admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(admission.Access.Used, Is.EqualTo(1));
            Assert.That(h.Authority.StartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedSynchronizationAtTheSameMonotonicSampleCannotDoubleCharge()
    {
        using (var h = new Host())
        {
            double now = 100;
            Write(h.Clock, "_timeSource", (Func<double>)(() => now));
            h.Start();
            now = 101.25;
            h.Clock.SynchronizeNow();
            for (int i = 0; i < 8; i++)
            { h.Clock.SynchronizeNow(); _ = h.Clock.CanCommitPacking; _ = h.Clock.CanStartCustomerEntry; }
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(1.25));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(20.75));
            Assert.That(h.Authority.Model.RemainingSeconds, Is.EqualTo(20.75));
        }
    }

    [Test]
    public void ClockPublishesOnlyTheAcceptedAbsolutePlayingPosition()
    {
        using (var h = new Host())
        {
            double position = -1;
            int calls = 0;
            h.Clock.TimeAdvanced += value => { position = value; calls++; };
            h.Start(); h.Clock.AdvanceTime(1.5); h.Clock.AdvanceTime(2.5);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(position, Is.EqualTo(4));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(4));
            Assert.That(h.Authority.Model.RemainingSeconds, Is.EqualTo(18));
        }
    }

    [Test]
    public void LongFrameClampsAcceptedPlayingTimeAndNeverPublishesTheOrdinaryEnd()
    {
        using (var h = new Host())
        {
            int ended = 0;
            h.Clock.Ended += () => ended++;
            h.Start();
            var button = (Button)Read(h.Root.GetComponent<PopupStorePacking>(), "_packButton");
            Assert.That(button.interactable, Is.True);
            h.Clock.AdvanceTime(1000);
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(22));
            Assert.That(h.Clock.RemainingSeconds, Is.Zero);
            Assert.That(h.Authority.Model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Clock.EndedCount, Is.Zero);
            Assert.That(ended, Is.Zero);
            Assert.That(h.Clock.CanCommitPacking, Is.False);
            Assert.That(h.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(button.interactable, Is.False, "A phase timeout must notify the existing packing button immediately.");
        }
    }

    [Test]
    public void TerminalPhaseResultDoesNotFinalizeTheScoreGradeOrDrawR1()
    {
        using (var h = new Host())
        {
            h.Start(); h.Clock.AdvanceTime(22); h.CompleteCleanup();
            var grade = h.Root.GetComponent<PopupStoreGradeSession>();
            var reward = h.Root.GetComponent<PopupStoreRewardSession>();
            Assert.That(h.Authority.Model.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
            Assert.That(grade.Refresh(), Is.True);
            Assert.That(reward.TryDecide(), Is.False);
            Assert.That(grade.FinalResult, Is.Null);
            Assert.That(grade.FinalizationCount, Is.Zero);
            Assert.That(reward.PolicySnapshot, Is.Null);
            Assert.That(reward.Result, Is.Null);
            Assert.That(reward.DecisionCount, Is.Zero);
            Assert.That(reward.RandomDrawCount, Is.Zero);
            Assert.That(h.Clock.CanAdvanceVisuals, Is.True);
        }
    }

    [TestCase(10.001, true)]
    [TestCase(10d, false)]
    [TestCase(9d, false)]
    public void PreviewPreservesTheTenSecondEntryCutoff(double remaining, bool allowed)
    {
        using (var h = new Host())
        {
            h.Start(); h.Clock.AdvanceTime(22 - remaining);
            Assert.That(h.Clock.CanStartCustomerEntry, Is.EqualTo(allowed));
            Assert.That(h.Clock.CanCommitPacking, Is.True);
        }
    }

    [Test]
    public void GoalClearingBlocksNewWorkWhileLeavingCommittedVisualsAvailable()
    {
        using (var h = new Host())
        {
            h.Start(); h.Goal();
            Assert.That(h.Clock.CanCommitPacking, Is.False);
            Assert.That(h.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(h.Clock.CanAdvanceVisuals, Is.True);
            double remaining = h.Clock.RemainingSeconds;
            h.Clock.AdvanceTime(8);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
        }
    }

    [Test]
    public void NoticeAndThreeTwoOneUseTheSameSamplerWithoutChargingPatienceElapsed()
    {
        using (var h = new Host())
        {
            h.Start(); h.Clock.AdvanceTime(2); h.ClearCurrentPhase();
            h.Clock.AdvanceTime(1); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(3));
            h.Clock.AdvanceTime(1); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(2));
            h.Clock.AdvanceTime(1); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(1));
            h.Clock.AdvanceTime(1);
            Assert.That(h.Authority.Model.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(27));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(2));
            Assert.That(((Button)Read(h.Root.GetComponent<PopupStorePacking>(), "_packButton")).interactable, Is.True,
                "The next Playing phase must enable the existing packing button without a material click.");
        }
    }

    [Test]
    public void HugeTransitionDeltasAdvanceOneVisibleStepEachAndDoNotLeakIntoTheNextStage()
    {
        using (var h = new Host())
        {
            h.Start(); h.ClearCurrentPhase();
            h.Clock.AdvanceTime(1000); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(3));
            h.Clock.AdvanceTime(1000); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(2));
            h.Clock.AdvanceTime(1000); Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(1));
            h.Clock.AdvanceTime(1000);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(27));
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(h.Authority.Model.PhaseGeneration, Is.EqualTo(2));
        }
    }

    [TestCase("focus", PopupStorePhasePauseReason.FocusLost)]
    [TestCase("background", PopupStorePhasePauseReason.ApplicationBackground)]
    [TestCase("view", PopupStorePhasePauseReason.ViewHidden)]
    [TestCase("exit", PopupStorePhasePauseReason.ExitConfirmation)]
    public void EachExistingContextReasonPausesThePhaseAndClock(string reason, PopupStorePhasePauseReason expected)
    {
        using (var h = new Host())
        {
            h.Start(); h.ClearCurrentPhase(); SetPause(h.Clock, reason, true);
            CollectionAssert.Contains(h.Authority.Model.PauseReasons.ToArray(), expected);
            Assert.That(h.Clock.AdvanceTime(20), Is.False);
            Assert.That(h.Authority.Model.TransitionRemainingSeconds, Is.EqualTo(1));
            SetPause(h.Clock, reason, false);
            Assert.That(h.Authority.Model.IsPaused, Is.False);
            h.Clock.AdvanceTime(1);
            Assert.That(h.Authority.Model.CountdownValue, Is.EqualTo(3));
        }
    }

    [Test]
    public void AllFourPauseReasonsRemainIndependentUntilEveryReasonIsReleased()
    {
        using (var h = new Host())
        {
            h.Start();
            foreach (var reason in new[] { "focus", "background", "view", "exit" }) SetPause(h.Clock, reason, true);
            Assert.That(h.Authority.Model.PauseReasons.Count, Is.EqualTo(4));
            foreach (var reason in new[] { "focus", "background", "view" }) SetPause(h.Clock, reason, false);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(h.Authority.Model.IsPaused, Is.True);
            Assert.That(h.Clock.AdvanceTime(99), Is.False);
            SetPause(h.Clock, "exit", false);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Authority.Model.IsPaused, Is.False);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
        }
    }

    [Test]
    public void FocusReturnReanchorsTheSingleSourceWithoutBackgroundCatchup()
    {
        using (var h = new Host())
        {
            double now = 10;
            Write(h.Clock, "_timeSource", (Func<double>)(() => now));
            h.Start(); now = 12; h.Clock.SetApplicationFocused(false);
            now = 1012; h.Clock.SetApplicationFocused(true);
            h.Clock.SynchronizeNow();
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(20));
            now = 1013; h.Clock.SynchronizeNow();
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(19));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(3));
        }
    }

    [Test]
    public void ExistingVisualResumeFilterStillSkipsTheFirstAutomaticDelta()
    {
        using (var h = new Host())
        {
            h.Start();
            uint observed = h.Clock.VisualResumeVersion;
            h.Clock.SetApplicationFocused(false); h.Clock.SetApplicationFocused(true);
            Assert.That(h.Clock.FilterAutomaticVisualDelta(100, ref observed), Is.Zero);
            Assert.That(h.Clock.FilterAutomaticVisualDelta(.1f, ref observed), Is.EqualTo(.1f));
        }
    }

    [TestCase(1, 22d)]
    [TestCase(2, 26d)]
    [TestCase(3, 30d)]
    public void RealPatienceTimerUsesExplicitPreviewBasePlusFourPerAdditionalPlush(int count, double expected)
    {
        using (var h = new Host(true, orders: new[] { Order("CUSTOMER01", count) }))
        {
            h.Start();
            Assert.That(h.Patience.Refresh(), Is.True);
            Assert.That(h.Patience.GetTimer(0).InitialPatience, Is.EqualTo(expected));
            Assert.That(Read(h.Patience, "_onePlushSeconds"), Is.EqualTo(18d));
            Assert.That(Read(h.Patience, "_twoPlushSeconds"), Is.EqualTo(22d));
            Assert.That(Read(h.Patience, "_threePlushSeconds"), Is.EqualTo(26d));
        }
    }

    [Test]
    public void RealExpirationDeductsThreeOnceWithoutChargingTheOtherCustomersPatience()
    {
        using (var h = new Host(true, 60, 1, new[] { Order("CUSTOMER01", 1), Order("CUSTOMER04", 2) }))
        {
            h.Start(); h.Clock.AdvanceTime(1);
            Assert.That(h.Patience.GetTimer(0).Order.IsExpired, Is.True);
            Assert.That(h.Patience.GetTimer(1).RemainingPatience, Is.EqualTo(4));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(56));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(1));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(h.Clock.ComplaintPenaltySeconds, Is.EqualTo(3));
            Assert.That(h.Authority.PenaltyCalls, Is.EqualTo(1));
        }
    }

    [Test]
    public void DuplicateExpirationBatchCannotDeductAgain()
    {
        using (var h = new Host(true, 60, 1, new[] { Order("CUSTOMER01", 1) }))
        {
            h.Start(); h.Clock.AdvanceTime(1);
            var expired = h.Patience.GetTimer(0);
            for (int i = 0; i < 5; i++) Assert.That(h.Patience.TryProcessExpiredBatch(new[] { expired }), Is.Zero);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(56));
            Assert.That(h.Authority.PenaltyCalls, Is.EqualTo(1));
        }
    }

    [Test]
    public void OneSimultaneousBatchClampsAtZeroWithoutOrdinaryEndOrDoubleCharge()
    {
        using (var h = new Host(true, 3, 1, new[] { Order("CUSTOMER01", 1), Order("CUSTOMER04", 1) }))
        {
            h.Start(); h.Clock.AdvanceTime(1);
            Assert.That(h.Clock.RemainingSeconds, Is.Zero);
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(2));
            Assert.That(h.Clock.ComplaintPenaltySeconds, Is.EqualTo(6));
            Assert.That(h.Authority.PenaltyCalls, Is.EqualTo(1));
            Assert.That(h.Authority.Model.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Clock.EndedCount, Is.Zero);
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(1));
        }
    }

    [Test]
    public void NaturalDeadlineWinsOverSimultaneousPatienceExpiration()
    {
        using (var h = new Host(true, 1, 1, new[] { Order("CUSTOMER01", 1) }))
        {
            h.Start(); h.Clock.AdvanceTime(1);
            Assert.That(h.Authority.Model.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(h.Patience.GetTimer(0).Order.IsExpired, Is.False);
            Assert.That(h.Authority.PenaltyCalls, Is.Zero);
        }
    }

    [Test]
    public void ClearingFreezesTheRealPatienceTimersWithoutMarkingUnfinishedOrdersExpired()
    {
        using (var h = new Host(true, 60, 1, new[] { Order("CUSTOMER01", 1) }))
        {
            h.Start(); h.Goal(); h.Clock.AdvanceTime(100);
            h.Patience.Refresh();
            Assert.That(h.Patience.GetTimer(0).RemainingPatience, Is.EqualTo(1));
            Assert.That(h.Patience.GetTimer(0).IsRunning, Is.False);
            Assert.That(h.Patience.ExpiredCount, Is.Zero);
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
        }
    }

    [Test]
    public void PreviewHudUsesExistingTextAndNormalGradeRefreshCannotOverwriteIt()
    {
        using (var h = new Host())
        {
            h.Start();
            var grade = h.Root.GetComponent<PopupStoreGradeSession>();
            var text = (TMP_Text)Read(grade, "_gradeText");
            var font = text.font;
            Assert.That(Invoke(grade, "EditorRenderPhaseHud", "도전 C | 목표 0/1 | 확보 없음", .25f), Is.True);
            grade.Refresh(); grade.RefreshHud();
            Assert.That(text.text, Is.EqualTo("도전 C | 목표 0/1 | 확보 없음"));
            Assert.That(text.font, Is.SameAs(font));
            Assert.That(((Image)Read(grade, "_progressImage")).fillAmount, Is.EqualTo(.25f));
            Assert.That(grade.FinalizationCount, Is.Zero);
        }
    }

    [Test]
    public void PreviewHudCannotBeInjectedIntoTheOrdinaryGame()
    {
        using (var h = new Host(false))
        {
            var grade = h.Root.GetComponent<PopupStoreGradeSession>();
            Assert.That(Invoke(grade, "EditorRenderPhaseHud", "test", 1f), Is.False);
        }
    }

    [Test]
    public void PausedGoalStillNeedsUnpausedExactCleanupBeforeCountdown()
    {
        using (var h = new Host())
        {
            h.Start(); h.Goal(); SetPause(h.Clock, "exit", true);
            Assert.That(h.Authority.Model.TryCompleteCleanup(h.Authority.Cleanup()), Is.False);
            SetPause(h.Clock, "exit", false); h.CompleteCleanup();
            Assert.That(h.Authority.Model.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        }
    }

    [Test]
    public void RefreshingThePreviewMirrorsStateWithoutReadingTheTimeSource()
    {
        using (var h = new Host())
        {
            h.Start(); int samples = 0;
            Write(h.Clock, "_timeSource", (Func<double>)(() => { samples++; return 100; }));
            Assert.That(h.Clock.RefreshPhasePreview(), Is.True);
            Assert.That(samples, Is.Zero);
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
        }
    }

    [Test]
    public void PreviewDoesNotRestoreTheRetiredManualPauseButtonOrChangeGlobalTimeScale()
    {
        float scale = Time.timeScale;
        using (var h = new Host())
        {
            h.Start();
            var pause = h.Root.GetComponent<PopupStorePauseController>();
            Assert.That(pause.TryTogglePause(), Is.False);
            Assert.That(((Button)Read(pause, "_pauseButton")).gameObject.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }
        Assert.That(Time.timeScale, Is.EqualTo(scale));
    }

    private sealed class Authority : IPopupStorePhasePreviewClock
    {
        internal readonly PopupStorePhaseSession Model;
        internal int StartCount, PenaltyCalls;
        internal Authority(GameObject root, double duration = 22, double patience = 22)
        {
            var profile = PopupStorePhaseTestProfiles.CreateDefinitionProfile();
            var durations = new[] { duration, 27d, 30d, 34d, 38d, 42d };
            for (int i = 0; i < 6; i++)
            {
                profile.Phases[i].TimeLimitSeconds = durations[i];
                profile.Phases[i].CustomerPatienceSeconds = patience;
                profile.Phases[i].TargetSuccessfulOrders = i == 0 ? 1 : 2;
                profile.Phases[i].StageClearNoticeSeconds = 1;
            }
            Assert.That(profile.TryCreateSnapshot(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(PopupStorePhaseTestProfiles.CatalogPath),
                out var snapshot, out var error), Is.True, error);
            Assert.That(PopupStorePhaseSession.TryCreate(root.GetComponent<PopupStoreGradeSession>().SessionId,
                snapshot, out Model, out error), Is.True, error);
        }
        public bool TryStart() { bool started = Model.TryStart(); if (started) StartCount++; return started; }
        public void Advance(double elapsed) => Model.TryAdvanceTime(elapsed);
        public double RemainingSeconds => Model.RemainingSeconds;
        public bool CanPlay => Model.CanAcceptPacking;
        public bool CanEnter => Model.CanStartCustomerEntry;
        public double OnePlushPatienceSeconds => Model.CurrentPhase.CustomerPatienceSeconds;
        public void SetContext(bool viewVisible, bool applicationPaused, bool focused, bool exitPaused)
        {
            Model.TrySetPauseReason(PopupStorePhasePauseReason.ViewHidden, !viewVisible);
            Model.TrySetPauseReason(PopupStorePhasePauseReason.ApplicationBackground, applicationPaused);
            Model.TrySetPauseReason(PopupStorePhasePauseReason.FocusLost, !focused);
            Model.TrySetPauseReason(PopupStorePhasePauseReason.ExitConfirmation, exitPaused);
        }
        public bool TryPenalty(double seconds)
        { if (!CanPlay) return false; PenaltyCalls++; return Model.TryAdvanceTime(seconds); }
        internal PopupStorePhaseCleanupSignal Cleanup() => new PopupStorePhaseCleanupSignal(Model.BusinessSessionId,
            Model.PhaseRunId, PopupStorePhaseCleanupStatus.Completed);
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerPatience Patience;
        internal readonly Authority Authority;
        internal Host(bool preview = true, double duration = 22, double patience = 22, PopupStoreOrder[] orders = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/PopupStore/UIPopupStore.prefab"));
            try
            {
                Root.name = "phase-preview-clock-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Write(Clock, "_autoStartForEditorReview", false);
                var entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Write(entrance, "_automaticEntry", false);
                Write(entrance, "_fixedTestSupply", false);
                var display = Root.GetComponent<PopupStoreOrderDisplay>();
                Write(display, "_sampleOrders", orders ?? Array.Empty<PopupStoreOrder>());
                if (preview)
                {
                    Authority = new Authority(Root, duration, patience);
                    Assert.That(Clock.TryBindPhasePreview(Authority), Is.True);
                }
                var view = Root.GetComponent<UIPopupStore>();
                view.Init(); view.Show();
                Assert.That(display.Refresh(), Is.True);
                Assert.That(Root.GetComponentInChildren<PopupStoreSalesCounter>(true).Refresh(), Is.True);
                Assert.That(Root.GetComponent<PopupStorePacking>().Refresh(), Is.True);
                Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
                Assert.That(Patience.Refresh(), Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal void Start()
        {
            PopupStoreApprovedTestSession.Attach(Root, 1);
            Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True);
            Assert.That(Patience.Refresh(), Is.True);
        }
        internal void Goal()
        {
            Assert.That(Authority.Model.TryRecordPacking(new PopupStorePhasePackingEvidence(Authority.Model.BusinessSessionId,
                Authority.Model.PhaseRunId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))), Is.True);
            Clock.RefreshPhasePreview();
        }
        internal void CompleteCleanup()
        { Assert.That(Authority.Model.TryCompleteCleanup(Authority.Cleanup()), Is.True); Clock.RefreshPhasePreview(); }
        internal void ClearCurrentPhase() { Goal(); CompleteCleanup(); }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); }
    }

    private static PopupStoreOrder Order(string customer, int plushCount)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", Enumerable.Repeat("STAFF01", plushCount).ToArray());
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
    }
    private static void SetPause(PopupStoreBusinessClock clock, string reason, bool paused)
    {
        if (reason == "focus") clock.SetApplicationFocused(!paused);
        else if (reason == "background") clock.SetApplicationPaused(paused);
        else if (reason == "view") clock.SetViewVisible(!paused);
        else Invoke(clock, "SetExitConfirmationPaused", paused);
    }
    private static object Invoke(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, name);
        return field;
    }
}
#endif
