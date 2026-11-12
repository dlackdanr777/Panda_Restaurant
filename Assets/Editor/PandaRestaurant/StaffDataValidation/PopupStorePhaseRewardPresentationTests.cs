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
using Host = PopupStorePhasePreviewIntegrationTests.Host;

/// <summary>Explicit local reward visuals; no table draw, fabricated sale, account mutation or Play.</summary>
public sealed class PopupStorePhaseRewardPresentationTests
{
    private const string Notice = "검수용 · 미지급 · 실제 계정에 반영되지 않음";
    private static T Field<T>(Object target, string name) where T : Object
        => new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
    private static Sprite Approved(string grade = "C") => AssetDatabase.LoadAssetAtPath<Sprite>(
        "Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png");

    private sealed class ViewHost : IDisposable
    {
        internal readonly Host Game = new Host();
        internal readonly GameObject Root;
        internal readonly PopupStorePhaseRewardPresentation View;
        internal readonly object Identity = new object();
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal int CloseCalls;
        internal bool Accept = true;
        internal ViewHost(bool begin = true, string grade = "C", int amount = 30)
        {
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath);
                Assert.That(prefab, Is.Not.Null, "Explicitly author the Editor reward Prefab before executing the suite.");
                Root = Object.Instantiate(prefab, Game.Root.transform);
                Root.SetActive(false); _fonts.BindBeforeActivation(Root); Root.SetActive(true);
                View = Root.GetComponent<PopupStorePhaseRewardPresentation>();
                Assert.That(View.BindClock(Game.Clock), Is.True);
                if (begin) Assert.That(View.Begin(Identity, Approved(grade), amount, Notice, Close), Is.True, View.LastError);
            }
            catch { Dispose(); throw; }
        }
        internal bool Close() { CloseCalls++; return Accept; }
        internal void Arrive() => View.Tick(View.RiseDuration, true);
        internal void Reveal() { Arrive(); Assert.That(View.TryOpen(), Is.True); View.Tick(View.OpeningDuration, true); }
        public void Dispose() { Object.DestroyImmediate(Root); _fonts.Dispose(); Game.Dispose(); }
    }

    [Test]
    public void LocalStateReservesIdentityAndAmountWithoutReplayingBegin()
    {
        var state = new PopupStorePhaseRewardVisualSession(); var identity = new object();
        Assert.That(state.TryBegin(identity, 30), Is.True);
        Assert.That(state.Advance(.2, 1, 1), Is.True);
        Assert.That(state.TryBegin(identity, 30), Is.True);
        Assert.That(state.BeginCount, Is.EqualTo(1)); Assert.That(state.Elapsed, Is.EqualTo(.2));
        Assert.That(state.TryBegin(new object(), 30), Is.False);
        Assert.That(state.TryBegin(identity, 300), Is.False);
        Assert.That(state.Amount, Is.EqualTo(30));
    }
    [TestCase(-1)] [TestCase(int.MinValue)]
    public void NegativeAmountsCannotCreateVisualSession(int value)
    {
        var state = new PopupStorePhaseRewardVisualSession();
        Assert.That(state.TryBegin(new object(), value), Is.False);
        Assert.That(state.ResultIdentity, Is.Null); Assert.That(state.BeginCount, Is.Zero);
    }
    [Test]
    public void StateCannotOpenBeforeArrivalOrOpenTwice()
    {
        var state = new PopupStorePhaseRewardVisualSession(); state.TryBegin(new object(), 1);
        Assert.That(state.TryOpen(), Is.False); state.Advance(1, 1, 1);
        Assert.That(state.TryOpen(), Is.True); Assert.That(state.TryOpen(), Is.False);
        state.Advance(1, 1, 1); Assert.That(state.TryOpen(), Is.False); Assert.That(state.OpenCount, Is.EqualTo(1));
    }
    [Test]
    public void HugeDeltaCannotSkipUserTapOrConfirmAutomatically()
    {
        var state = new PopupStorePhaseRewardVisualSession(); state.TryBegin(new object(), 300);
        state.Advance(double.MaxValue, 1, 1);
        Assert.That(state.State, Is.EqualTo(PopupStorePhaseRewardVisualState.AwaitingTap));
        Assert.That(state.OpenCount, Is.Zero); state.TryOpen(); state.Advance(double.MaxValue, 1, 1);
        Assert.That(state.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
        Assert.That(state.ConfirmCount, Is.Zero);
    }
    [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)] [TestCase(-1d)]
    public void InvalidDeltasDoNotMutateLocalPresentation(double delta)
    {
        var state = new PopupStorePhaseRewardVisualSession(); state.TryBegin(new object(), 1);
        Assert.That(state.Advance(delta, 1, 1), Is.False);
        Assert.That(state.Elapsed, Is.Zero); Assert.That(state.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Rising));
    }
    [Test]
    public void ConfirmationReservesBeforeCallbackAndRejectedConfirmationCanRetry()
    {
        var state = new PopupStorePhaseRewardVisualSession(); var id = new object();
        state.TryBegin(id, 30); state.Advance(1, 1, 1); state.TryOpen(); state.Advance(1, 1, 1);
        Assert.That(state.TryReserveConfirmation(), Is.True); Assert.That(state.TryReserveConfirmation(), Is.False);
        state.CompleteConfirmation(false); Assert.That(state.ConfirmCount, Is.Zero);
        Assert.That(state.TryReserveConfirmation(), Is.True); state.CompleteConfirmation(true); state.CompleteConfirmation(true);
        Assert.That(state.ConfirmCount, Is.EqualTo(1)); Assert.That(state.TryBegin(id, 30), Is.False);
    }

    [Test]
    public void AuthoredEditorViewUsesOriginalBoomDiamondAndBoldYeongdeokFont()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath);
        Assert.That(prefab, Is.Not.Null); var view = prefab.GetComponent<PopupStorePhaseRewardPresentation>();
        Assert.That(view.ValidateConfiguration(out string error), Is.True, error);
        Assert.That(AssetDatabase.GetAssetPath(Field<AnimationClip>(view, "_boomClip")), Is.EqualTo(PopupStorePhaseRewardViewBuilder.BoomPath));
        Assert.That(AssetDatabase.GetAssetPath(Field<Image>(view, "_centralDiamond").sprite), Is.EqualTo(PopupStorePhaseRewardViewBuilder.DiamondPath));
        var amount = Field<TMP_Text>(view, "_amountText");
        Assert.That(AssetDatabase.GetAssetPath(amount.font), Is.EqualTo(PopupStorePhaseRewardViewBuilder.FontPath));
        Assert.That((amount.fontStyle & FontStyles.Bold) != 0, Is.True);
        Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty, "No independently advancing Animator.");
    }
    [TestCase("C")] [TestCase("B")] [TestCase("A")] [TestCase("S")] [TestCase("SS")] [TestCase("SSS")]
    public void ApprovedGradeSpriteIsDisplayedWithoutRecalculation(string grade)
    {
        using (var h = new ViewHost(grade: grade, amount: 300))
        { Assert.That(h.View.DisplayedSprite, Is.SameAs(Approved(grade))); Assert.That(h.View.Amount, Is.EqualTo(300)); h.Game.AssertNoR1(); }
    }
    [Test]
    public void BoxRisesFromBelowToAuthoredCentreBeforeAcceptingTap()
    {
        using (var h = new ViewHost())
        {
            var box = Field<Image>(h.View, "_boxImage"); float from = box.rectTransform.anchoredPosition.y;
            Assert.That(from, Is.LessThan(-500)); Assert.That(h.View.TryOpen(), Is.False);
            h.View.Tick(h.View.RiseDuration / 2, true);
            Assert.That(box.rectTransform.anchoredPosition.y, Is.GreaterThan(from));
            Assert.That(box.rectTransform.anchoredPosition.y, Is.LessThan(45)); h.Arrive();
            Assert.That(box.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 45)));
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.AwaitingTap));
        }
    }
    [Test]
    public void AwaitingBoxPulsesWithoutOpeningOrChangingPayload()
    {
        using (var h = new ViewHost())
        {
            h.Arrive(); var box = Field<Image>(h.View, "_boxImage"); h.View.Tick(.25f, true);
            Assert.That(box.rectTransform.localScale.x, Is.GreaterThan(1));
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.AwaitingTap));
            Assert.That(h.View.OpenCount, Is.Zero); Assert.That(h.View.Amount, Is.EqualTo(30));
            Assert.That(Field<TMP_Text>(h.View, "_tapText").gameObject.activeSelf, Is.True);
        }
    }
    [Test]
    public void OneTouchRevealsImmutableUnpaidValueAndOnlyThenEnablesConfirm()
    {
        using (var h = new ViewHost())
        {
            Assert.That(h.View.TryConfirm(), Is.False); h.Arrive(); Assert.That(h.View.TryOpen(), Is.True);
            Assert.That(h.View.TryOpen(), Is.False); Assert.That(h.View.TryConfirm(), Is.False);
            h.View.Tick(h.View.OpeningDuration, true);
            Assert.That(Field<TMP_Text>(h.View, "_amountText").text, Is.EqualTo("+30D"));
            Assert.That(Field<TMP_Text>(h.View, "_disclaimerText").text, Is.EqualTo(Notice));
            Assert.That(Field<Button>(h.View, "_confirmButton").interactable, Is.True);
            Assert.That(h.View.OpenCount, Is.EqualTo(1)); Assert.That(h.View.TryOpen(), Is.False);
        }
    }
    [TestCase("focus")] [TestCase("background")] [TestCase("view")] [TestCase("exit")]
    public void EachClockContextStopsAnimationAndInput(string reason)
    {
        using (var h = new ViewHost())
        {
            h.Arrive(); SetPause(h.Game.Clock, reason, true); double elapsed = h.View.Session.Elapsed;
            h.View.Tick(10, true); Assert.That(h.View.Session.Elapsed, Is.EqualTo(elapsed));
            Assert.That(h.View.TryOpen(), Is.False); SetPause(h.Game.Clock, reason, false);
            Assert.That(h.View.TryOpen(), Is.True);
        }
    }
    [Test]
    public void NestedClockPauseReasonsMustAllClearBeforeRewardContinues()
    {
        using (var h = new ViewHost())
        {
            h.Arrive(); Assert.That(h.View.TryOpen(), Is.True);
            h.Game.Clock.SetApplicationPaused(true); h.Game.Clock.SetApplicationFocused(false);
            h.Game.Clock.SetViewVisible(false); h.Game.Clock.SetManualPaused(true);
            h.Game.Clock.SetApplicationPaused(false); h.Game.Clock.SetApplicationFocused(true);
            h.Game.Clock.SetViewVisible(true); h.View.Tick(50, true);
            Assert.That(h.View.Session.Elapsed, Is.Zero); h.Game.Clock.SetManualPaused(false);
            h.View.Tick(h.View.OpeningDuration, true);
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
        }
    }
    [Test]
    public void HideAndDisablePreserveProgressAndDoNotReplayOpenOnResume()
    {
        using (var h = new ViewHost())
        {
            h.Arrive(); h.View.TryOpen(); h.View.Tick(.2f, true); double elapsed = h.View.Session.Elapsed;
            h.Root.SetActive(false);
            // Detached EditMode objects do not dispatch non-ExecuteAlways lifecycle callbacks.
            InvokeLifecycle(h.View, "OnDisable");
            h.View.Tick(100, true); h.Root.SetActive(true);
            Assert.That(h.View.IsVisible, Is.False); Assert.That(h.View.ResumePresentation(), Is.True);
            Assert.That(h.View.Session.Elapsed, Is.EqualTo(elapsed)); Assert.That(h.View.TryOpen(), Is.False);
            h.View.Tick(h.View.OpeningDuration, true); Assert.That(h.View.OpenCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void DuplicateBeginCannotChangeRevealedValueOrRestartAppearance()
    {
        using (var h = new ViewHost())
        {
            h.Reveal(); Assert.That(h.View.Begin(h.Identity, Approved(), 30, Notice, h.Close), Is.True);
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
            Assert.That(h.View.BeginCount, Is.EqualTo(1)); Assert.That(h.View.OpenCount, Is.EqualTo(1));
            Assert.That(h.View.Begin(h.Identity, Approved(), 300, Notice, h.Close), Is.False);
            Assert.That(h.View.Begin(new object(), Approved(), 30, Notice, h.Close), Is.False);
            Assert.That(h.View.Begin(h.Identity, Approved("B"), 30, Notice, h.Close), Is.False);
            Assert.That(h.View.Amount, Is.EqualTo(30));
        }
    }
    [Test]
    public void ReentrantConfirmationNavigatesAtMostOnce()
    {
        using (var h = new ViewHost(begin: false))
        {
            int calls = 0;
            Assert.That(h.View.Begin(h.Identity, Approved(), 30, Notice, () => { calls++; Assert.That(h.View.TryConfirm(), Is.False); return true; }), Is.True);
            h.Reveal(); Assert.That(h.View.TryConfirm(), Is.True); Assert.That(h.View.TryConfirm(), Is.False);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(h.View.ConfirmCount, Is.EqualTo(1));
            Assert.That(h.View.IsVisible, Is.False); Assert.That(h.View.ResumePresentation(), Is.False);
        }
    }
    [Test]
    public void FailedNavigationAllowsRetryWithoutReopeningBox()
    {
        using (var h = new ViewHost())
        {
            h.Reveal(); h.Accept = false; Assert.That(h.View.TryConfirm(), Is.False);
            Assert.That(h.View.IsVisible, Is.True); Assert.That(h.View.ConfirmCount, Is.Zero);
            h.Accept = true; Assert.That(h.View.TryConfirm(), Is.True);
            Assert.That(h.View.ConfirmCount, Is.EqualTo(1)); Assert.That(h.View.OpenCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void ThrowingNavigationDoesNotLeavePermanentConfirmationLatch()
    {
        using (var h = new ViewHost(begin: false))
        {
            h.View.Begin(h.Identity, Approved(), 30, Notice, () => throw new InvalidOperationException("test refusal"));
            h.Reveal(); Assert.That(h.View.TryConfirm(), Is.False);
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
            Assert.That(h.View.LastError, Does.Contain("test refusal")); Assert.That(h.View.ConfirmCount, Is.Zero);
        }
    }
    [Test]
    public void OriginalBoomSamplingMutatesOnlyItsDedicatedImage()
    {
        using (var h = new ViewHost())
        {
            var box = Field<Image>(h.View, "_boxImage"); var original = box.sprite;
            var boom = Field<Image>(h.View, "_boomImage"); var rootPosition = h.Game.Root.transform.localPosition;
            var clockTime = h.Game.Clock.RemainingSeconds; int score = h.Game.Score.TotalScore;
            h.Arrive(); h.View.TryOpen(); h.View.Tick(.12f, true);
            Assert.That(boom.gameObject.activeSelf, Is.True); Assert.That(boom.sprite, Is.Not.Null);
            var clip = Field<AnimationClip>(h.View, "_boomClip");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .Single(x => x.type == typeof(Image) && x.path == string.Empty && x.propertyName == "m_Sprite");
            Assert.That(boom.sprite, Is.SameAs(AnimationUtility.GetObjectReferenceCurve(clip, binding)[1].value));
            Assert.That(boom.mainTexture, Is.SameAs(boom.sprite.texture));
            Assert.That(box.sprite, Is.SameAs(original)); Assert.That(h.Game.Root.transform.localPosition, Is.EqualTo(rootPosition));
            Assert.That(h.Game.Clock.RemainingSeconds, Is.EqualTo(clockTime)); Assert.That(h.Game.Score.TotalScore, Is.EqualTo(score));
            Assert.That(h.Game.Packing.CommittedReceipts, Is.Empty); h.Game.AssertNoR1();
        }
    }
    private static void InvokeLifecycle(Component target, string name)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    [Test]
    public void EveryOriginalBoomKeyAndAlphaAreReplayedAtTheirAuthoredTimestamps()
    {
        using (var h = new ViewHost())
        {
            var clip = Field<AnimationClip>(h.View, "_boomClip");
            var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .Single(x => x.type == typeof(Image) && x.path == string.Empty && x.propertyName == "m_Sprite");
            var alphaBinding = AnimationUtility.GetCurveBindings(clip)
                .Single(x => x.type == typeof(Image) && x.path == string.Empty && x.propertyName == "m_Color.a");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            var alpha = AnimationUtility.GetEditorCurve(clip, alphaBinding);
            Assert.That(keys.Length, Is.EqualTo(7));
            var authored = new SerializedObject(h.View);
            Assert.That(authored.FindProperty("_boomFrames").arraySize, Is.EqualTo(keys.Length));
            Assert.That(authored.FindProperty("_boomFrameTimes").arraySize, Is.EqualTo(keys.Length));
            var boom = Field<Image>(h.View, "_boomImage");
            h.Arrive(); Assert.That(h.View.TryOpen(), Is.True);
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(authored.FindProperty("_boomFrames").GetArrayElementAtIndex(i).objectReferenceValue,
                    Is.SameAs(keys[i].value));
                Assert.That(authored.FindProperty("_boomFrameTimes").GetArrayElementAtIndex(i).floatValue,
                    Is.EqualTo(keys[i].time));
                float sample = keys[i].time + .001f;
                h.View.Tick(sample - (float)h.View.Session.Elapsed, true);
                Assert.That(boom.sprite, Is.SameAs(keys[i].value), "Original frame " + i);
                Assert.That(boom.color.a, Is.EqualTo(alpha.Evaluate(sample)).Within(.00001f));
                Assert.That(boom.mainTexture, Is.SameAs(((Sprite)keys[i].value).texture));
            }
            h.View.Tick(clip.length - (float)h.View.Session.Elapsed + .001f, true);
            Assert.That(boom.gameObject.activeSelf, Is.False, "The original puff ends at its authored duration.");
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Opening));
            Assert.That(h.View.OpenCount, Is.EqualTo(1));
        }
    }
    [Test]
    public void MissingOriginalBoomFrameRejectsBeforeBeginningOrDrawingAnything()
    {
        using (var h = new ViewHost(begin: false))
        {
            var so = new SerializedObject(h.View);
            so.FindProperty("_boomFrames").GetArrayElementAtIndex(1).objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.Begin(h.Identity, Approved(), 30, Notice, h.Close), Is.False);
            Assert.That(h.View.IsVisible, Is.False); Assert.That(h.View.BeginCount, Is.Zero); h.Game.AssertNoR1();
        }
    }
    [Test]
    public void NonIncreasingOriginalFrameTimesRejectBeforeBeginning()
    {
        using (var h = new ViewHost(begin: false))
        {
            var so = new SerializedObject(h.View);
            so.FindProperty("_boomFrameTimes").GetArrayElementAtIndex(1).floatValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(h.View.ValidateConfiguration(out _), Is.False);
            Assert.That(h.View.Begin(h.Identity, Approved(), 30, Notice, h.Close), Is.False);
            Assert.That(h.View.BeginCount, Is.Zero); Assert.That(h.View.OpenCount, Is.Zero);
        }
    }
    [Test]
    public void TerminalPhaseStillAllowsOnlyPresentationTimeAndNoSecondFinalization()
    {
        using (var h = new ViewHost())
        {
            h.Game.Clock.AdvanceTime(h.Game.Clock.RemainingSeconds);
            h.Game.Preview.TickCleanup();
            Assert.That(h.Game.Preview.Session.IsTerminal, Is.True);
            var result = h.Game.Preview.Session.FinalResult;
            int count = h.Game.Preview.Session.FinalizationCount;
            double elapsed = h.Game.Clock.ElapsedSeconds;
            Assert.That(h.Game.Clock.CanAdvanceVisuals, Is.True);
            h.Reveal();
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
            Assert.That(h.Game.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(h.Game.Preview.Session.FinalResult, Is.SameAs(result));
            Assert.That(h.Game.Preview.Session.FinalizationCount, Is.EqualTo(count)); h.Game.AssertNoR1();
        }
    }
    [Test]
    public void MissingUnpaidDisclaimerCannotBeginPresentation()
    {
        using (var h = new ViewHost(begin: false))
        { Assert.That(h.View.Begin(h.Identity, Approved(), 30, "획득", h.Close), Is.False); Assert.That(h.View.BeginCount, Is.Zero); }
    }
    [Test]
    public void UnknownBoxCannotBeginAndDoesNotAlterExistingImages()
    {
        using (var h = new ViewHost(begin: false))
        {
            var wrong = AssetDatabase.LoadAssetAtPath<Sprite>(PopupStorePhaseRewardViewBuilder.DiamondPath);
            Assert.That(h.View.Begin(h.Identity, wrong, 30, Notice, h.Close), Is.False);
            Assert.That(h.View.DisplayedSprite, Is.Null); Assert.That(h.View.BeginCount, Is.Zero);
        }
    }
    [Test]
    public void ForeignClockCannotBeBoundToOwnedRewardOverlay()
    {
        using (var h = new ViewHost()) using (var other = new Host())
        { Assert.That(h.View.BindClock(other.Clock), Is.False); h.Arrive(); Assert.That(h.View.TryOpen(), Is.True); }
    }
    [Test]
    public void BadExternalControlReferenceIsRejectedWithoutHidingIt()
    {
        using (var h = new ViewHost(begin: false))
        {
            var unrelated = new GameObject("Unrelated Canvas", typeof(CanvasGroup));
            try
            {
                var group = unrelated.GetComponent<CanvasGroup>(); group.alpha = .75f; group.interactable = true;
                var so = new SerializedObject(h.View); so.FindProperty("_overlay").objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo(); Assert.That(h.View.ValidateConfiguration(out _), Is.False);
                Assert.That(h.View.Begin(h.Identity, Approved(), 30, Notice, h.Close), Is.False); h.View.HidePresentation();
                Assert.That(group.alpha, Is.EqualTo(.75f)); Assert.That(group.interactable, Is.True);
            }
            finally { Object.DestroyImmediate(unrelated); }
        }
    }
    private static void SetPause(PopupStoreBusinessClock clock, string reason, bool paused)
    {
        switch (reason)
        {
            case "focus": clock.SetApplicationFocused(!paused); break;
            case "background": clock.SetApplicationPaused(paused); break;
            case "view": clock.SetViewVisible(!paused); break;
            case "exit": clock.SetManualPaused(paused); break;
        }
    }
}
#endif
