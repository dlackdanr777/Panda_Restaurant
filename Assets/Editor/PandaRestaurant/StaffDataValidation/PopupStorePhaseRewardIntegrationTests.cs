#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Harness = PopupStorePhasePreviewNavigationTests.Harness;
using Object = UnityEngine.Object;

/// <summary>Original popup, actual packing receipts, isolated admission and real map navigation.</summary>
public sealed class PopupStorePhaseRewardIntegrationTests
{
    private GachaCollectionPreviewSceneWitness _workspace;
    private int _diamonds, _diamondNotifications;
    [SetUp] public void RecordWorkspace()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _diamonds = UserInfo.Dia; _diamondNotifications = 0;
        UserInfo.OnChangeDiaHandler += OnDiamondsChanged;
    }
    [TearDown] public void PreserveWorkspace()
    {
        UserInfo.OnChangeDiaHandler -= OnDiamondsChanged;
        Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds));
        Assert.That(_diamondNotifications, Is.Zero, "A local preview cannot notify the real wallet.");
        _workspace.AssertUnchanged("phase reward authored-controls real-navigation memory test");
    }
    private void OnDiamondsChanged() => _diamondNotifications++;

    [Test]
    public void BeforeApprovalAndDuringPlayReceiveCannotDrawOrDisplayAReward()
    {
        using (var t = new RewardHarness(false))
        {
            Assert.That(t.Game.Preview.TryReceiveResult(), Is.False);
            Assert.That(t.Game.Panel.TryConfirm(), Is.False);
            Assert.That(t.Game.Admission.LocalStartCount, Is.Zero);
            t.Start();
            Assert.That(t.Game.Preview.TryReceiveResult(), Is.False);
            t.AssertNoDraw();
            Assert.That(t.Game.Navigation.FirstView, Is.SameAs(t.Game.View));
        }
    }

    [Test]
    public void ActualSuccessfulReceiptBeforeTheDeadlineDoesNotPrematurelyCreateAReward()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; h.Ready(); h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Preview.Session.FinalResult, Is.Null);
            Assert.That(h.Preview.TryReceiveResult(), Is.False);
            t.AssertNoDraw();
        }
    }

    [Test]
    public void CommittedDeliveryAndDepartureMustSettleBeforeAnySummaryOrReceive()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; h.Ready(); h.Pack();
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            h.Panel.TickResult(100);
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Preview.TryReceiveResult(), Is.False);
            h.Deliver();
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.TryReceiveResult(), Is.False);
            h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            t.AssertNoDraw();
        }
    }

    [Test]
    public void RealReceiptSummaryReceiveOpenAndConfirmReturnToTheOriginalMapWithoutPayment()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; var receipt = t.SecuredCResult(); var final = h.Preview.Session.FinalResult;
            Assert.That(t.Text(h.Panel, "_titleText").text, Is.EqualTo("게임 종료"));
            Assert.That(t.Text(h.Panel, "_scoreText").text, Is.EqualTo("기록 점수 100"));
            Assert.That(t.Text(h.Panel, "_confirmText").text, Is.EqualTo("받기"));
            t.AssertNoDraw();
            t.Receive();
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            Assert.That(t.View.Session.ResultIdentity, Is.SameAs(final));
            Assert.That(t.View.DisplayedSprite, Is.SameAs(h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true)
                .SpriteFor(PopupStoreGradeTier.C)));
            Assert.That(t.View.DisplayedSprite.name, Is.EqualTo("PopupRewardBox_C_APPROVED"));
            t.Open();
            Assert.That(t.Text(t.View, "_amountText").text, Is.EqualTo("+1D"));
            Assert.That(t.Text(t.View, "_disclaimerText").text, Does.Contain("미지급"));
            Assert.That(t.View.TryConfirm(), Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            Assert.That(h.Root.activeSelf, Is.False);
            Assert.That(t.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Confirmed));
            Assert.That(h.Preview.ConfirmationCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            t.AssertOneReward(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void RepeatedReceiveInputsCannotDrawAgainOrAddAnotherOverlay()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult(); t.Receive();
            var decision = t.Game.Preview.RewardRoll.Decision;
            for (int i = 0; i < 5; i++)
            {
                Assert.That(t.Game.Panel.TryConfirm(), Is.False);
                Assert.That(t.Game.Preview.TryReceiveResult(), Is.False);
            }
            Assert.That(t.Game.Preview.RewardRoll.Decision, Is.SameAs(decision));
            Assert.That(t.Game.Root.GetComponentsInChildren<PopupStorePhaseRewardPresentation>(true), Has.Length.EqualTo(1));
            Assert.That(t.View.BeginCount, Is.EqualTo(1));
            Assert.That(t.Source.Calls, Is.EqualTo(1));
            Assert.That(t.Game.Preview.RewardRoll.DecisionCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void CloseAndTapCannotSkipTheSecuredReceiveRiseAndOpenSequence()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult();
            Assert.That(t.Game.Preview.TryConfirmResult(), Is.False);
            Assert.That(t.Game.View.TryRequestExit(), Is.False);
            Assert.That(t.Game.Exit.IsVisible, Is.False);
            t.Receive();
            Assert.That(t.View.TryOpen(), Is.False);
            Assert.That(t.View.TryConfirm(), Is.False);
            Assert.That(t.Game.View.TryRequestExit(), Is.False);
            t.View.Tick(t.View.RiseDuration, true);
            Assert.That(t.View.TryConfirm(), Is.False);
            Assert.That(t.View.TryOpen(), Is.True);
            Assert.That(t.View.TryOpen(), Is.False);
            Assert.That(t.View.TryConfirm(), Is.False);
            t.View.Tick(t.View.OpeningDuration, true);
            Assert.That(t.View.TryConfirm(), Is.True);
            Assert.That(t.View.TryConfirm(), Is.False);
            t.AssertOneReward();
        }
    }

    [Test]
    public void AwaitingTapNeverOpensAutomaticallyAndUsesNoAdditionalRandomValues()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult(); t.Receive(); t.View.Tick(100, true);
            t.View.Tick(1000, true);
            Assert.That(t.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.AwaitingTap));
            Assert.That(t.View.OpenCount, Is.Zero);
            Assert.That(t.Game.Preview.RewardRoll.RandomDrawCount, Is.EqualTo(1));
            Assert.That(t.Source.Calls, Is.EqualTo(1));
        }
    }

    [TestCase(PopupStorePhaseRewardVisualState.Rising)]
    [TestCase(PopupStorePhaseRewardVisualState.Opening)]
    [TestCase(PopupStorePhaseRewardVisualState.Revealed)]
    public void RealNavigationHideAndReentryResumeTheSamePresentationWithoutSummaryOrRestart(PopupStorePhaseRewardVisualState phase)
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; t.SecuredCResult(); t.Receive();
            if (phase != PopupStorePhaseRewardVisualState.Rising)
            { t.View.Tick(t.View.RiseDuration, true); Assert.That(t.View.TryOpen(), Is.True); }
            t.View.Tick(phase == PopupStorePhaseRewardVisualState.Revealed ? t.View.OpeningDuration : .1f, true);
            var elapsed = t.View.Session.Elapsed;
            var final = h.Preview.Session.FinalResult; var decision = h.Preview.RewardRoll.Decision;
            h.Navigation.Pop(UIPopupStore.ViewName);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            t.DispatchHiddenLifecycle();
            Assert.That(t.View.IsVisible, Is.False);
            t.Reenter(); h.Panel.TickResult(0);
            Assert.That(t.View.IsVisible, Is.True);
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(t.View.State, Is.EqualTo(phase));
            Assert.That(t.View.Session.Elapsed, Is.EqualTo(elapsed));
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(h.Preview.RewardRoll.Decision, Is.SameAs(decision));
            Assert.That(h.Preview.RewardRoll.RandomDrawCount, Is.EqualTo(1));
            Assert.That(t.View.BeginCount, Is.EqualTo(1));
            Assert.That(h.Preview.ConfirmationCount, Is.Zero);
        }
    }

    [Test]
    public void AlreadyConfirmedSamePopupReentryShowsSummaryThenClosesWithoutDrawOrReplay()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; t.SecuredCResult(); t.Receive(); t.Open();
            var final = h.Preview.Session.FinalResult; var decision = h.Preview.RewardRoll.Decision;
            Assert.That(t.View.TryConfirm(), Is.True);
            t.DispatchHiddenLifecycle();
            for (int i = 0; i < 2; i++)
            {
                t.Reenter(); h.ShowResult();
                Assert.That(t.View.IsVisible, Is.False);
                Assert.That(h.Panel.IsVisible, Is.True);
                Assert.That(h.Panel.AppearanceCount, Is.EqualTo(1));
                Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
                Assert.That(h.Preview.RewardRoll.Decision, Is.SameAs(decision));
                h.ConfirmToMap();
                Assert.That(h.Panel.TryConfirm(), Is.False);
                t.DispatchHiddenLifecycle();
            }
            Assert.That(h.Preview.ConfirmationCount, Is.EqualTo(3));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            t.AssertOneReward(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void NoClearedPhaseHasNoBoxOrDrawAndReceiveSimplyReturnsToMap()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game;
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds); Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.ConfirmToMap(); t.AssertNoDraw(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void AbandonAfterClearingCRetainsTheRecordButDoesNotDrawAReward()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; t.ClearC(); h.NextPhase();
            Assert.That(h.View.TryRequestExit(), Is.True); Assert.That(h.Exit.TryConfirmAbandon(), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Preview.Session.FinalResult.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            h.ConfirmToMap(); t.AssertNoDraw(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void FocusLossFreezesTheTerminalRewardAndRejectsItsInputUntilResume()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; t.SecuredCResult(); t.Receive();
            h.Clock.SetApplicationFocused(false); t.View.Tick(100, true);
            Assert.That(t.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Rising));
            Assert.That(t.View.Session.Elapsed, Is.Zero);
            Assert.That(t.View.TryOpen(), Is.False);
            h.Clock.SetApplicationFocused(true); t.Open();
            h.Clock.SetApplicationFocused(false);
            Assert.That(t.View.TryConfirm(), Is.False);
            h.Clock.SetApplicationFocused(true); Assert.That(t.View.TryConfirm(), Is.True);
            t.AssertOneReward();
        }
    }

    [Test]
    public void RejectedRealMapPopLeavesTheRewardRevealedAndAllowsOneLaterConfirmation()
    {
        using (var t = new RewardHarness())
        {
            var h = t.Game; t.SecuredCResult(); t.Receive(); t.Open();
            h.View.PopEnabled = false;
            Assert.That(t.View.TryConfirm(), Is.False);
            Assert.That(t.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
            Assert.That(t.View.ConfirmCount, Is.Zero);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            h.View.PopEnabled = true;
            Assert.That(t.View.TryConfirm(), Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            Assert.That(t.View.TryConfirm(), Is.False); t.AssertOneReward();
        }
    }

    [Test]
    public void AnAttachedRewardOwnerCannotBeReplacedBeforeOrAfterStart()
    {
        using (var t = new RewardHarness(false))
        {
            var roll = t.Game.Preview.RewardRoll;
            Assert.That(t.Game.Preview.AttachRewardView(t.View, RewardHarness.Table), Is.False);
            t.Start(); Assert.That(t.Game.Preview.AttachRewardView(t.View, RewardHarness.Table), Is.False);
            Assert.That(t.Game.Preview.RewardRoll, Is.SameAs(roll));
            Assert.That(t.Game.Preview.RewardPresentation, Is.SameAs(t.View));
            t.AssertNoDraw();
        }
    }

    [Test]
    public void InvalidRandomProviderFailsClosedWithoutChangingTheSummaryOrRetrying()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult(); t.Source.Value = -1;
            Assert.That(t.Game.Panel.TryConfirm(), Is.False);
            Assert.That(t.Game.Panel.TryConfirm(), Is.False);
            Assert.That(t.Game.Panel.IsVisible, Is.True);
            Assert.That(t.View.IsVisible, Is.False);
            Assert.That(t.View.BeginCount, Is.Zero);
            Assert.That(t.Game.Preview.RewardRoll.Decision, Is.Null);
            Assert.That(t.Game.Preview.RewardRoll.RandomDrawCount, Is.EqualTo(1));
            Assert.That(t.Source.Calls, Is.EqualTo(1));
            Assert.That(t.Game.Navigation.FirstView, Is.SameAs(t.Game.View));
        }
    }

    [Test]
    public void OriginalAuthoredButtonsInvokeReceiveOpenAndConfirmationOnce()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult();
            Field<Button>(t.Game.Panel, "_confirmButton").onClick.Invoke();
            Assert.That(t.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Rising));
            t.View.Tick(t.View.RiseDuration, true);
            Field<Button>(t.View, "_boxButton").onClick.Invoke();
            Field<Button>(t.View, "_boxButton").onClick.Invoke();
            t.View.Tick(t.View.OpeningDuration, true);
            Field<Button>(t.View, "_confirmButton").onClick.Invoke();
            Field<Button>(t.View, "_confirmButton").onClick.Invoke();
            Assert.That(t.Game.Navigation.FirstView, Is.SameAs(t.Game.Map));
            t.AssertOneReward();
        }
    }

    [Test]
    public void ReentrantRandomProviderCannotReceiveOrConfirmAnotherTransaction()
    {
        using (var t = new RewardHarness())
        {
            t.SecuredCResult(); bool nested = true;
            t.Source.Callback = () => nested = t.Game.Panel.TryConfirm();
            t.Receive(); Assert.That(nested, Is.False);
            Assert.That(t.Source.Calls, Is.EqualTo(1));
            Assert.That(t.Game.Preview.RewardRoll.DecisionCount, Is.EqualTo(1));
            Assert.That(t.View.BeginCount, Is.EqualTo(1));
            Assert.That(t.Game.Preview.ConfirmationCount, Is.Zero);
        }
    }

    private sealed class RewardHarness : IDisposable
    {
        internal static PopupStoreRewardTable Table => AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(
            "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset");
        internal readonly Harness Game;
        internal readonly PopupStorePhaseRewardPresentation View;
        internal readonly Source Source = new Source();
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal RewardHarness(bool start = true)
        {
            Game = new Harness(requestStart: false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null, "Build the authored Editor-only reward prefab before this suite.");
                var root = Object.Instantiate(prefab, Game.Root.transform, false);
                root.SetActive(false); _fonts.BindBeforeActivation(root); root.SetActive(true);
                View = root.GetComponent<PopupStorePhaseRewardPresentation>();
                Assert.That(Game.Preview.AttachRewardView(View, Table), Is.True);
                Assert.That(Game.Preview.RewardRoll.TrySetRandomSource(Source), Is.True);
                if (start) Start();
            }
            catch { Dispose(); throw; }
        }
        internal void Start()
        {
            Assert.That(Game.Controller.RequestStart(), Is.True, Game.Controller.LastError);
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(Game.Preview, Game.Clock);
        }
        internal PopupStorePackingReceipt ClearC()
        {
            Game.Ready(); var receipt = Game.Pack(); Game.Deliver(); Game.Entrance.TickEntrance(2);
            Game.Clock.AdvanceTime(Game.Clock.RemainingSeconds);
            Assert.That(Game.Preview.TickCleanup(), Is.True);
            Assert.That(Game.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            return receipt;
        }
        internal PopupStorePackingReceipt SecuredCResult()
        {
            var receipt = ClearC(); Game.NextPhase();
            Game.Clock.AdvanceTime(Game.Clock.RemainingSeconds);
            Assert.That(Game.Preview.TickCleanup(), Is.True); Game.ShowResult();
            Assert.That(Game.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(Game.Preview.Session.FinalizationCount, Is.EqualTo(1));
            return receipt;
        }
        internal void Receive()
        {
            Assert.That(Game.Panel.TryConfirm(), Is.True);
            Assert.That(View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Rising));
            Assert.That(View.IsVisible, Is.True);
        }
        internal void Open()
        {
            View.Tick(View.RiseDuration, true); Assert.That(View.TryOpen(), Is.True);
            View.Tick(View.OpeningDuration, true);
            Assert.That(View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
        }
        internal void DispatchHiddenLifecycle()
        {
            Assert.That(Game.Root.activeInHierarchy, Is.False);
            // EditMode does not consistently dispatch non-ExecuteAlways MonoBehaviour callbacks.
            // Exercise the actual production callbacks instead of resetting private presentation state.
            InvokeLifecycle(Game.Panel, "OnDisable");
            InvokeLifecycle(View, "OnDisable");
        }
        internal void Reenter()
        {
            Game.Reenter();
            Assert.That(Game.Root.activeInHierarchy, Is.True);
            InvokeLifecycle(Game.Panel, "OnEnable");
        }
        internal TMP_Text Text(Object target, string field) => Field<TMP_Text>(target, field);
        internal void AssertNoDraw()
        {
            Assert.That(Game.Preview.RewardRoll.RandomDrawCount, Is.Zero);
            Assert.That(Game.Preview.RewardRoll.DecisionCount, Is.Zero);
            Assert.That(Game.Preview.RewardRoll.Decision, Is.Null);
            Assert.That(View.BeginCount, Is.Zero);
            Assert.That(View.IsVisible, Is.False);
            Assert.That(Source.Calls, Is.Zero);
        }
        internal void AssertOneReward()
        {
            Assert.That(Game.Preview.RewardRoll.RandomDrawCount, Is.EqualTo(1));
            Assert.That(Game.Preview.RewardRoll.DecisionCount, Is.EqualTo(1));
            Assert.That(View.BeginCount, Is.EqualTo(1));
            Assert.That(View.OpenCount, Is.EqualTo(1));
            Assert.That(View.ConfirmCount, Is.EqualTo(1));
            Assert.That(Source.Calls, Is.EqualTo(1));
        }
        public void Dispose() { Game?.Dispose(); _fonts.Dispose(); }
    }
    private static void InvokeLifecycle(Object component, string method)
    {
        var callback = component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null, method);
        callback.Invoke(component, null);
    }
    private sealed class Source : IPopupStoreRewardRandomSource
    {
        internal int Calls, Value;
        internal Action Callback;
        public int NextInt(int upper)
        { Assert.That(upper, Is.EqualTo(1000)); Calls++; Callback?.Invoke(); return Value; }
    }
    private static T Field<T>(Object target, string name) where T : Object
        => new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
}
#endif
