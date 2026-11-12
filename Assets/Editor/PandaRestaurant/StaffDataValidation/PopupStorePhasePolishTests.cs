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

/// <summary>Instance-only modal styling and warning driven by the real isolated preview's accepted clock.</summary>
public sealed class PopupStorePhasePolishTests
{
    private sealed class PolishHost : IDisposable
    {
        internal readonly Host Game;
        internal readonly GameObject Root;
        internal readonly PopupStorePhasePreviewPolish Polish;
        internal readonly TMP_Text Time, Title, Body, ContinueText, AbandonText;
        internal readonly Button Continue, Abandon;
        internal readonly RectTransform Panel;
        internal readonly string OriginalTitle, OriginalBody, OriginalContinue, OriginalAbandon;
        internal readonly Color OriginalTimeColor, OriginalContinueColor, OriginalAbandonColor;
        internal readonly Vector2 OriginalBodyPosition, OriginalContinuePosition, OriginalAbandonPosition, OriginalPanelSize;
        internal PolishHost(bool bind = true, bool start = true, bool completeIntroduction = true)
        {
            Game = new Host(completeIntroduction: completeIntroduction, requestStart: start);
            try
            {
                var clock = new SerializedObject(Game.Clock);
                Time = (TMP_Text)clock.FindProperty("_timeText").objectReferenceValue;
                Title = Find<TMP_Text>("Exit Confirmation Title"); Body = Find<TMP_Text>("Exit Confirmation Explanation");
                Continue = Find<Button>("Continue Business Button"); Abandon = Find<Button>("Abandon Business Button - Unavailable");
                Panel = Find<RectTransform>("Exit Confirmation Panel");
                ContinueText = Continue.GetComponentInChildren<TMP_Text>(true); AbandonText = Abandon.GetComponentInChildren<TMP_Text>(true);
                OriginalTitle = Title.text; OriginalBody = Body.text; OriginalContinue = ContinueText.text; OriginalAbandon = AbandonText.text;
                OriginalTimeColor = Time.color; OriginalContinueColor = Continue.image.color; OriginalAbandonColor = Abandon.image.color;
                OriginalBodyPosition = Body.rectTransform.anchoredPosition;
                OriginalContinuePosition = ((RectTransform)Continue.transform).anchoredPosition;
                OriginalAbandonPosition = ((RectTransform)Abandon.transform).anchoredPosition; OriginalPanelSize = Panel.sizeDelta;
                Root = new GameObject("preview-polish-memory-test", typeof(RectTransform), typeof(PopupStorePhasePreviewPolish));
                Root.transform.SetParent(Game.Root.transform, false); Polish = Root.GetComponent<PopupStorePhasePreviewPolish>();
                if (bind)
                {
                    Assert.That(Bind(), Is.True);
                    Assert.That(Game.Preview.AttachPolish(Polish), Is.True);
                }
            }
            catch { Dispose(); throw; }
        }
        private T Find<T>(string name) where T : Component
            => Game.Root.GetComponentsInChildren<T>(true).Single(x => x.name == name);
        internal bool Bind() => Polish.Bind(Game.Preview, Time, Title, Body, Continue, Abandon, Panel);
        internal void Warning() { Assert.That(Game.Clock.AdvanceTime(Game.Clock.RemainingSeconds - 10), Is.True); }
        internal void AssertRestored()
        {
            Assert.That(Title.text, Is.EqualTo(OriginalTitle)); Assert.That(Body.text, Is.EqualTo(OriginalBody));
            Assert.That(ContinueText.text, Is.EqualTo(OriginalContinue)); Assert.That(AbandonText.text, Is.EqualTo(OriginalAbandon));
            Assert.That(Time.color, Is.EqualTo(OriginalTimeColor));
            Assert.That(Continue.image.color, Is.EqualTo(OriginalContinueColor)); Assert.That(Abandon.image.color, Is.EqualTo(OriginalAbandonColor));
            Assert.That(Body.rectTransform.anchoredPosition, Is.EqualTo(OriginalBodyPosition));
            Assert.That(((RectTransform)Continue.transform).anchoredPosition, Is.EqualTo(OriginalContinuePosition));
            Assert.That(((RectTransform)Abandon.transform).anchoredPosition, Is.EqualTo(OriginalAbandonPosition));
            Assert.That(Panel.sizeDelta, Is.EqualTo(OriginalPanelSize));
        }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); Game?.Dispose(); }
    }

    [Test]
    public void BindUsesTheExactRequestedExitLabelsAndSeparatesButtonTint()
    {
        using (var h = new PolishHost())
        {
            Assert.That(h.Title.text, Is.EqualTo("도전을 포기하시겠습니까?"));
            Assert.That(h.Body.text, Is.EqualTo("영업을 포기하면 사용한 영업권은 반환되지 않으며\n보상을 받을 수 없습니다."));
            Assert.That(h.ContinueText.text, Is.EqualTo("계속하기")); Assert.That(h.AbandonText.text, Is.EqualTo("포기하기"));
            Assert.That(h.Continue.image.color, Is.EqualTo(Color.white));
            Assert.That(h.Abandon.image.color, Is.EqualTo(new Color(1, .45f, 1, 1)));
        }
    }
    [Test]
    public void BodyAndButtonsMoveDownWithoutMovingTitleOrIncreasingPanelWidth()
    {
        using (var h = new PolishHost(bind: false))
        {
            var titlePosition = h.Title.rectTransform.anchoredPosition;
            Assert.That(h.Bind(), Is.True);
            Assert.That(h.Body.rectTransform.anchoredPosition, Is.EqualTo(h.OriginalBodyPosition + Vector2.down * 35));
            Assert.That(((RectTransform)h.Continue.transform).anchoredPosition, Is.EqualTo(h.OriginalContinuePosition + Vector2.down * 55));
            Assert.That(((RectTransform)h.Abandon.transform).anchoredPosition, Is.EqualTo(h.OriginalAbandonPosition + Vector2.down * 55));
            Assert.That(h.Panel.sizeDelta, Is.EqualTo(h.OriginalPanelSize + Vector2.up * 90));
            Assert.That(h.Title.rectTransform.anchoredPosition, Is.EqualTo(titlePosition));
        }
    }
    [Test]
    public void DuplicateBindCannotAccumulateLayoutOffsets()
    {
        using (var h = new PolishHost())
        {
            var position = h.Body.rectTransform.anchoredPosition; var size = h.Panel.sizeDelta;
            Assert.That(h.Bind(), Is.False); Assert.That(h.Body.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(h.Panel.sizeDelta, Is.EqualTo(size));
        }
    }
    [Test]
    public void DestroyRestoresEveryOriginalModalValueAndWarningColor()
    {
        using (var h = new PolishHost())
        {
            h.Warning();
            // Detached EditMode objects do not dispatch non-ExecuteAlways lifecycle callbacks.
            InvokeLifecycle(h.Polish, "OnDestroy"); Object.DestroyImmediate(h.Polish); h.AssertRestored();
        }
    }
    [Test]
    public void VisualBindingDoesNotConsumeUseOrChangeBusinessScoreOrdersAndClock()
    {
        using (var h = new PolishHost(bind: false))
        {
            double time = h.Game.Clock.RemainingSeconds; int starts = h.Game.Admission.LocalStartCount;
            int remaining = h.Game.Admission.Access.Remaining;
            Assert.That(h.Bind(), Is.True); h.Polish.TickWarning(10);
            Assert.That(h.Game.Clock.RemainingSeconds, Is.EqualTo(time)); Assert.That(h.Game.Admission.LocalStartCount, Is.EqualTo(starts));
            Assert.That(h.Game.Admission.Access.Remaining, Is.EqualTo(remaining)); Assert.That(h.Game.Score.TotalScore, Is.Zero);
            Assert.That(h.Game.Packing.CommittedReceipts, Is.Empty); h.Game.AssertNoR1();
        }
    }
    [Test]
    public void AboveTenSecondsKeepsAuthoredTimeColor()
    {
        using (var h = new PolishHost())
        {
            h.Game.Clock.AdvanceTime(h.Game.Clock.RemainingSeconds - 10.1);
            Assert.That(h.Polish.IsWarning, Is.False); Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
        }
    }
    [Test]
    public void AcceptedClockDeltaStartsAndPulsesWarningAtTenSeconds()
    {
        using (var h = new PolishHost())
        {
            h.Warning(); Assert.That(h.Polish.IsWarning, Is.True);
            Color red = h.Time.color; Assert.That(red, Is.EqualTo(new Color(.9f, .08f, .08f, 1)));
            h.Game.Clock.AdvanceTime(.5);
            Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
            h.Game.Clock.AdvanceTime(.5); Assert.That(h.Time.color, Is.EqualTo(red));
        }
    }
    [Test]
    public void WarningChangesColorOnlyAndDoesNotReplaceClockText()
    {
        using (var h = new PolishHost())
        {
            h.Warning(); string shown = h.Time.text; double remaining = h.Game.Clock.RemainingSeconds;
            h.Polish.TickWarning(.3);
            Assert.That(h.Time.text, Is.EqualTo(shown)); Assert.That(h.Game.Clock.RemainingSeconds, Is.EqualTo(remaining));
        }
    }
    [TestCase("focus")] [TestCase("background")] [TestCase("view")] [TestCase("exit")]
    public void EveryPauseReasonFreezesWarningPhaseAndAcceptedTime(string reason)
    {
        using (var h = new PolishHost())
        {
            h.Warning(); h.Game.Clock.AdvanceTime(.2); var color = h.Time.color; double seconds = h.Game.Clock.RemainingSeconds;
            SetPause(h.Game.Clock, reason, true); h.Polish.TickWarning(.4); h.Game.Clock.AdvanceTime(4);
            Assert.That(h.Time.color, Is.EqualTo(color)); Assert.That(h.Game.Clock.RemainingSeconds, Is.EqualTo(seconds));
            SetPause(h.Game.Clock, reason, false); h.Game.Clock.AdvanceTime(.2);
            Assert.That(h.Time.color, Is.Not.EqualTo(color));
        }
    }
    [Test]
    public void ClearingOnePauseDoesNotResumeWarningUnderAnotherPause()
    {
        using (var h = new PolishHost())
        {
            h.Warning(); h.Game.Clock.AdvanceTime(.2); var color = h.Time.color;
            h.Game.Clock.SetApplicationFocused(false); h.Game.Clock.SetApplicationPaused(true);
            h.Game.Clock.SetApplicationPaused(false); h.Polish.TickWarning(5); h.Game.Clock.AdvanceTime(.2);
            Assert.That(h.Time.color, Is.EqualTo(color));
            h.Game.Clock.SetApplicationFocused(true); h.Game.Clock.AdvanceTime(.2);
            Assert.That(h.Time.color, Is.Not.EqualTo(color));
        }
    }
    [Test]
    public void DeadlineResetsWarningAtZeroBeforeGameOverOrResult()
    {
        using (var h = new PolishHost())
        {
            h.Warning(); h.Game.Clock.AdvanceTime(10);
            Assert.That(h.Game.Clock.RemainingSeconds, Is.Zero); Assert.That(h.Polish.IsWarning, Is.False);
            Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
            h.Game.Preview.TickCleanup(); h.Polish.TickWarning(10);
            Assert.That(h.Polish.IsWarning, Is.False); Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
        }
    }
    [Test]
    public void TutorialReadyStateNeverWarnsEvenWithConfiguredLargeWarningThreshold()
    {
        using (var h = new PolishHost(start: false))
        {
            SetFloat(h.Polish, "_warningSeconds", 100);
            h.Polish.TickWarning(10); Assert.That(h.Polish.IsWarning, Is.False);
            Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
            Assert.That(h.Game.Admission.LocalStartCount, Is.Zero);
        }
    }
    [Test]
    public void InitialStageCountdownAndGameStartDoNotFlash()
    {
        using (var h = new PolishHost(completeIntroduction: false))
        {
            SetFloat(h.Polish, "_warningSeconds", 100);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(h.Game.Preview.PresentationCue, Is.Not.Empty);
                h.Polish.TickWarning(.5); Assert.That(h.Polish.IsWarning, Is.False);
                Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
                h.Game.Clock.AdvanceTime(100);
            }
        }
    }
    [Test]
    public void SuccessAndNextCountdownRestoreColorUntilNextActualPlayingPhase()
    {
        using (var h = new PolishHost())
        {
            h.Game.ClearPhase(); Assert.That(h.Game.Preview.PresentationCue, Is.EqualTo("Success"));
            SetFloat(h.Polish, "_warningSeconds", 100);
            for (int i = 0; i < 6; i++)
            {
                Assert.That(h.Game.Preview.PresentationCue, Is.Not.Empty);
                h.Polish.TickWarning(.5); Assert.That(h.Polish.IsWarning, Is.False);
                Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor)); h.Game.Clock.AdvanceTime(100);
            }
        }
    }
    [Test]
    public void DisablingPolishImmediatelyRestoresWarningColor()
    {
        using (var h = new PolishHost())
        {
            h.Warning(); h.Polish.enabled = false;
            InvokeLifecycle(h.Polish, "OnDisable");
            Assert.That(h.Polish.IsWarning, Is.False); Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
            h.Polish.TickWarning(.5); Assert.That(h.Time.color, Is.EqualTo(h.OriginalTimeColor));
            h.Polish.enabled = true; h.Polish.TickWarning(0); Assert.That(h.Polish.IsWarning, Is.True);
        }
    }
    [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1d)]
    public void InvalidWarningDeltaLeavesColorAndBusinessStateUnchanged(double delta)
    {
        using (var h = new PolishHost())
        {
            h.Warning(); Color color = h.Time.color; double seconds = h.Game.Clock.RemainingSeconds;
            h.Polish.TickWarning(delta); Assert.That(h.Time.color, Is.EqualTo(color));
            Assert.That(h.Game.Clock.RemainingSeconds, Is.EqualTo(seconds));
        }
    }
    [Test]
    public void InvalidOverlappingTextOrButtonsAreRejectedBeforeAnyOverride()
    {
        using (var h = new PolishHost(bind: false))
        {
            Assert.That(h.Polish.Bind(h.Game.Preview, h.Time, h.Body, h.Body, h.Continue, h.Abandon, h.Panel), Is.False);
            h.AssertRestored();
            Assert.That(h.Polish.Bind(h.Game.Preview, h.Time, h.Title, h.Body, h.Continue, h.Continue, h.Panel), Is.False);
            h.AssertRestored();
        }
    }
    [TestCase("_bodyVerticalOffset")] [TestCase("_buttonsVerticalOffset")] [TestCase("_panelExtraHeight")]
    public void NonFiniteLayoutValuesCannotMutateModal(string field)
    {
        using (var h = new PolishHost(bind: false))
        { SetFloat(h.Polish, field, float.NaN); Assert.That(h.Bind(), Is.False); h.AssertRestored(); }
    }
    [Test]
    public void PanelOutsideOwningPopupIsRejectedWithoutChangingItsLabels()
    {
        using (var h = new PolishHost(bind: false))
        {
            var parent = h.Panel.parent; var foreign = new GameObject("foreign-modal-parent", typeof(RectTransform));
            try
            {
                h.Panel.SetParent(foreign.transform, false);
                Assert.That(h.Bind(), Is.False); h.AssertRestored();
            }
            finally { h.Panel.SetParent(parent, false); Object.DestroyImmediate(foreign); }
        }
    }

    private static void SetFloat(Object target, string field, float value)
    { var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void InvokeLifecycle(Component target, string name)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void SetPause(PopupStoreBusinessClock clock, string reason, bool paused)
    {
        switch (reason)
        {
            case "focus": clock.SetApplicationFocused(!paused); break;
            case "background": clock.SetApplicationPaused(paused); break;
            case "view": clock.SetViewVisible(!paused); break;
            case "exit": typeof(PopupStoreBusinessClock).GetMethod("SetExitConfirmationPaused", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(clock, new object[] { paused }); break;
        }
    }
}
#endif
