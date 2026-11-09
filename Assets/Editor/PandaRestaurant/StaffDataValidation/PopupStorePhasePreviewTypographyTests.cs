#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Host = PopupStorePhasePreviewIntegrationTests.Host;

/// <summary>Approved-font geometry checks at both reference pixel scales; not a GameView screenshot assertion.</summary>
public sealed class PopupStorePhasePreviewTypographyTests
{
    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void EveryStageAndTransitionFitsTheExistingHudAtBothReferencePixelScales(int width, int height)
    {
        using (var h = new Host())
        {
            var hud = h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            var authoredRect = hud.NextGradeText.rectTransform;
            var before = new RectState(authoredRect);
            var font = hud.NextGradeText.font;
            foreach (var phase in h.Profile.Phases)
            {
                Assert.That(hud.RefreshDisplay(), Is.True);
                Assert.That(hud.EditorPhaseProgressText.text, Does.Contain("도전 " + phase.Grade));
                Assert.That(hud.EditorPhaseProgressText.text, Does.Contain("목표 0/" + phase.TargetSuccessfulOrders));
                Assert.That(hud.NextGradeText.text, Does.StartWith("확보 "));
                Fits(hud.NextGradeText, width, height);
                Fits(hud.EditorPhaseProgressText, width, height);
                before.AssertUnchanged(authoredRect);
                Assert.That(hud.EditorPhaseProgressText.font, Is.SameAs(font));
                h.ClearPhase(); hud.RefreshDisplay(); Fits(hud.EditorPhaseProgressText, width, height);
                if (phase.Grade == PopupStoreGradeTier.SSS) break;
                foreach (var cue in new[] { "Stage", "3", "2", "1", "GameStart", "" })
                {
                    Assert.That(h.Preview.CanPlay, Is.False);
                    h.Clock.AdvanceTime(100); hud.RefreshDisplay();
                    Assert.That(h.Preview.PresentationCue, Is.EqualTo(cue));
                    if (cue == "3" || cue == "2" || cue == "1")
                        Assert.That(hud.EditorPhaseProgressText.text, Is.EqualTo("다음 단계 · " + cue));
                    Fits(hud.EditorPhaseProgressText, width, height);
                }
                Assert.That(h.Preview.CanPlay, Is.True);
            }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void LongestLocalResultFitsOriginalGradeAndCountRectanglesWithoutChangingTheirFonts(int width, int height)
    {
        using (var h = new Host())
        {
            var grade = Text(h, "Result Grade"); var count = Text(h, "Result Completed Orders");
            var gradeRect = new RectState(grade.rectTransform); var countRect = new RectState(count.rectTransform);
            var gradeFont = grade.font; var countFont = count.font;
            float gradeSize = grade.fontSize, countSize = count.fontSize;
            foreach (var phase in h.Profile.Phases)
            { h.ClearPhase(); if (phase.Grade != PopupStoreGradeTier.SSS) h.NextPhase(); }
            h.Clock.AdvanceTime(1); h.ShowResult();
            // DEV-GAME-02B-3: progress facts stay in FinalResult; the summary only shows actual score.
            Assert.That(grade.gameObject.activeSelf, Is.False);
            Assert.That(count.gameObject.activeSelf, Is.False);
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
            Assert.That(h.Preview.Session.FinalResult.SuccessfulOrders, Is.EqualTo(15));
            Assert.That(Text(h, "Result Title").text, Is.EqualTo("게임 종료"));
            Assert.That(Text(h, "Result Score").alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Fits(Text(h, "Result Score"), width, height);
            gradeRect.AssertUnchanged(grade.rectTransform); countRect.AssertUnchanged(count.rectTransform);
            Assert.That(grade.font, Is.SameAs(gradeFont)); Assert.That(count.font, Is.SameAs(countFont));
            h.Panel.enabled = false;
            DispatchLifecycle(h.Panel, "OnDisable");
            Assert.That(grade.fontSize, Is.EqualTo(gradeSize)); Assert.That(count.fontSize, Is.EqualTo(countSize));
            Assert.That(grade.enableAutoSizing, Is.False); Assert.That(count.enableAutoSizing, Is.False);
        }
    }

    [Test]
    public void RepeatedRefreshUsesOneMemoryLabelAndDisablingRestoresTheAuthoredHudStyle()
    {
        using (var h = new Host())
        {
            var hud = h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Host.Prefab).GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            for (int i = 0; i < 20; i++) Assert.That(hud.RefreshDisplay(), Is.True);
            var label = hud.EditorPhaseProgressText;
            Assert.That(h.Root.GetComponentsInChildren<TMP_Text>(true).Count(x => x.name == "Editor Phase Progress Label"), Is.EqualTo(1));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            hud.enabled = false;
            DispatchLifecycle(hud, "OnDisable");
            Assert.That(label.enabled, Is.False);
            Assert.That(hud.NextGradeText.fontSize, Is.EqualTo(prefab.NextGradeText.fontSize));
            Assert.That(hud.NextGradeText.enableAutoSizing, Is.EqualTo(prefab.NextGradeText.enableAutoSizing));
            hud.enabled = true; DispatchLifecycle(hud, "OnEnable");
            Assert.That(hud.EditorPhaseProgressText, Is.SameAs(label));
            Assert.That(label.enabled, Is.True);
        }
    }

    [Test]
    public void NoClearedGradeResultExplicitlyShowsNoBoxAndFitsWithoutARewardDecision()
    {
        using (var h = new Host())
        {
            h.Clock.AdvanceTime(22); Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            var grade = Text(h, "Result Grade");
            Assert.That(grade.gameObject.activeSelf, Is.False);
            Assert.That(Text(h, "Result Completed Orders").gameObject.activeSelf, Is.False);
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Fits(Text(h, "Result Score"), 1280, 720);
            h.AssertNoR1();
        }
    }

    private static TMP_Text Text(Host h, string name) => h.Root.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == name);

    private static void DispatchLifecycle(MonoBehaviour component, string method)
    {
        // These production MonoBehaviours intentionally do not ExecuteAlways. The EditMode
        // fixture dispatches their real Unity lifecycle method rather than assuming a Play callback.
        var callback = component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null, method);
        callback.Invoke(component, null);
    }

    private static void Fits(TMP_Text text, int width, int height)
    {
        Assert.That((float)width / height, Is.EqualTo(16f / 9f).Within(.001));
        text.ForceMeshUpdate(true, true);
        var preferred = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity);
        var rect = text.rectTransform.rect;
        float pixelScale = width / 1920f;
        Assert.That(preferred.x * pixelScale, Is.LessThanOrEqualTo(rect.width * pixelScale + 1f), text.name + ": " + text.text);
        Assert.That(preferred.y * pixelScale, Is.LessThanOrEqualTo(rect.height * pixelScale + 1f), text.name + ": " + text.text);
        Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
    }

    private readonly struct RectState
    {
        private readonly Vector2 _anchorMin, _anchorMax, _position, _size, _pivot;
        internal RectState(RectTransform rect)
        { _anchorMin = rect.anchorMin; _anchorMax = rect.anchorMax; _position = rect.anchoredPosition; _size = rect.sizeDelta; _pivot = rect.pivot; }
        internal void AssertUnchanged(RectTransform rect)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(_anchorMin)); Assert.That(rect.anchorMax, Is.EqualTo(_anchorMax));
            Assert.That(rect.anchoredPosition, Is.EqualTo(_position)); Assert.That(rect.sizeDelta, Is.EqualTo(_size)); Assert.That(rect.pivot, Is.EqualTo(_pivot));
        }
    }
}
#endif
