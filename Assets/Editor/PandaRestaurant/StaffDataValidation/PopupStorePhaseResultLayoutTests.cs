#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Host = PopupStorePhasePreviewIntegrationTests.Host;
using Object = UnityEngine.Object;

/// <summary>Actual receipt/deadline results rendered through the original authored panel; no reward draw or wallet.</summary>
public sealed class PopupStorePhaseResultLayoutTests
{
    private GachaCollectionPreviewSceneWitness _workspace;
    private int _diamonds;
    [SetUp] public void Before()
    { _workspace = GachaCollectionPreviewSceneWitness.Capture(); _diamonds = UserInfo.Dia; }
    [TearDown] public void After()
    {
        Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds));
        _workspace.AssertUnchanged("phase summary box/score memory layout");
    }

    [TestCase(PopupStoreGradeTier.C)] [TestCase(PopupStoreGradeTier.B)] [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)] [TestCase(PopupStoreGradeTier.SS)] [TestCase(PopupStoreGradeTier.SSS)]
    public void HighestClearedGradeUsesOneApprovedLeftBoxAndImmutableRightScore(PopupStoreGradeTier grade)
    {
        using (var h = new Host())
        {
            FinishWithGrade(h, grade);
            var final = h.Preview.Session.FinalResult;
            var box = Box(h); var score = Text(h, "_scoreText");
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(grade));
            Assert.That(box.enabled, Is.True);
            Assert.That(box.sprite, Is.SameAs(h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true).SpriteFor(grade)));
            Assert.That(box.sprite.name, Is.EqualTo("PopupRewardBox_" + grade + "_APPROVED"));
            Assert.That(Field<RectTransform>(h.Panel, "_panel").GetComponentsInChildren<Image>(false)
                .Count(x => x.enabled && x.sprite != null && x.sprite.name.StartsWith("PopupRewardBox_", StringComparison.Ordinal)),
                Is.EqualTo(1), "Exactly one approved box belongs to the summary content.");
            Assert.That(score.text, Is.EqualTo("기록 점수 " + final.TotalScore));
            Assert.That(final.TotalScore, Is.EqualTo(h.Score.TotalScore));
            Assert.That(box.rectTransform.anchoredPosition.x, Is.LessThan(0));
            Assert.That(score.rectTransform.anchoredPosition.x, Is.GreaterThan(0));
            float rightOfBox = box.rectTransform.anchoredPosition.x + box.rectTransform.rect.width / 2;
            float leftOfScore = score.rectTransform.anchoredPosition.x - score.rectTransform.rect.width / 2;
            Assert.That(rightOfBox, Is.LessThan(leftOfScore), "The two content columns must not overlap.");
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().ShownResult, Is.Null);
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().AnimationCount, Is.Zero);
            Assert.That(h.Preview.RewardRoll, Is.Null); h.AssertNoR1();
        }
    }

    [Test]
    public void SummaryShowsOnlyTitleScoreAndReceiveTextInsideTheResultPanel()
    {
        using (var h = new Host())
        {
            FinishWithGrade(h, PopupStoreGradeTier.C);
            Assert.That(Text(h, "_titleText").text, Is.EqualTo("게임 종료"));
            Assert.That(Text(h, "_confirmText").text, Is.EqualTo("받기"));
            Assert.That(Text(h, "_gradeText").gameObject.activeSelf, Is.False);
            Assert.That(Text(h, "_countText").gameObject.activeSelf, Is.False);
            var texts = Field<RectTransform>(h.Panel, "_panel").GetComponentsInChildren<TMP_Text>(false)
                .Where(x => x.enabled && !string.IsNullOrWhiteSpace(x.text)).Select(x => x.text).ToArray();
            Assert.That(texts, Is.EquivalentTo(new[] { "게임 종료", "기록 점수 100", "받기" }));
            Assert.That(Box(h).raycastTarget, Is.False);
            Assert.That(Box(h).GetComponent<Button>().interactable, Is.False, "The summary box cannot open or draw a reward.");
        }
    }

    [Test]
    public void NoClearedGradeHidesBoxAndCentersTheZeroScore()
    {
        using (var h = new Host())
        {
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds); Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(Box(h).enabled, Is.False); Assert.That(Box(h).sprite, Is.Null);
            Assert.That(Text(h, "_scoreText").text, Is.EqualTo("기록 점수 0"));
            Assert.That(Text(h, "_scoreText").rectTransform.anchoredPosition.x, Is.Zero);
            h.AssertNoR1();
        }
    }

    [Test]
    public void AbandonAfterClearingCDoesNotShowItsPreviouslySecuredBox()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); h.NextPhase();
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Preview.RequestAbandon(), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(Box(h).enabled, Is.False); Assert.That(Box(h).sprite, Is.Null);
            Assert.That(Text(h, "_scoreText").text, Is.EqualTo("기록 점수 100")); h.AssertNoR1();
        }
    }

    [Test]
    public void CurrentPlayingScoreDoesNotPrematurelyShowASummaryBox()
    {
        using (var h = new Host())
        {
            h.Ready(); h.Pack(); h.Panel.TickResult(10);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100)); Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(Box(h).enabled, Is.False); Assert.That(h.Panel.EditorShownPhaseResult, Is.Null);
            Assert.That(h.Preview.Session.FinalResult, Is.Null); h.AssertNoR1();
        }
    }

    [Test]
    public void RepeatedSummaryTicksDoNotAnimateTheBoxFinalizeAgainOrDraw()
    {
        using (var h = new Host())
        {
            FinishWithGrade(h, PopupStoreGradeTier.C); var final = h.Preview.Session.FinalResult;
            var sprite = Box(h).sprite; var position = Box(h).rectTransform.anchoredPosition;
            for (int i = 0; i < 12; i++) h.Panel.TickResult(1);
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Panel.PresentationCount, Is.EqualTo(1)); Assert.That(h.Panel.AppearanceCount, Is.EqualTo(1));
            Assert.That(Box(h).sprite, Is.SameAs(sprite)); Assert.That(Box(h).rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().AnimationCount, Is.Zero); h.AssertNoR1();
        }
    }

    [Test]
    public void InspectorLayoutOverridesMoveOnlyTheBoxAndScore()
    {
        using (var h = new Host())
        {
            var titlePosition = Text(h, "_titleText").rectTransform.anchoredPosition;
            var confirmPosition = Field<Button>(h.Panel, "_confirmButton").transform.localPosition;
            SetVector(h.Panel, "_phaseSummaryBoxPosition", new Vector2(-230, -15));
            SetVector(h.Panel, "_phaseSummaryBoxSize", new Vector2(260, 260));
            SetVector(h.Panel, "_phaseSummaryScorePosition", new Vector2(165, -15));
            SetVector(h.Panel, "_phaseSummaryScoreSize", new Vector2(370, 100));
            SetFloat(h.Panel, "_phaseSummaryScoreFontSize", 41); SetFloat(h.Panel, "_phaseSummaryMinimumScoreFontSize", 27);
            FinishWithGrade(h, PopupStoreGradeTier.C);
            Assert.That(Box(h).rectTransform.anchoredPosition, Is.EqualTo(new Vector2(-230, -15)));
            Assert.That(Box(h).rectTransform.sizeDelta, Is.EqualTo(new Vector2(260, 260)));
            Assert.That(Text(h, "_scoreText").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(165, -15)));
            Assert.That(Text(h, "_scoreText").rectTransform.sizeDelta, Is.EqualTo(new Vector2(370, 100)));
            Assert.That(Text(h, "_scoreText").fontSizeMax, Is.EqualTo(41));
            Assert.That(Text(h, "_scoreText").fontSizeMin, Is.EqualTo(27));
            Assert.That(Text(h, "_titleText").rectTransform.anchoredPosition, Is.EqualTo(titlePosition));
            Assert.That(Field<Button>(h.Panel, "_confirmButton").transform.localPosition, Is.EqualTo(confirmPosition));
        }
    }

    [Test]
    public void HidingRestoresAuthoredRectanglesTypographyAndButtonState()
    {
        using (var h = new Host())
        {
            var box = Box(h); var score = Text(h, "_scoreText");
            var boxPosition = box.rectTransform.anchoredPosition; var boxSize = box.rectTransform.sizeDelta;
            var boxScale = box.rectTransform.localScale; bool aspect = box.preserveAspect, raycast = box.raycastTarget;
            var scorePosition = score.rectTransform.anchoredPosition; var scoreSize = score.rectTransform.sizeDelta;
            var alignment = score.alignment; float font = score.fontSize, min = score.fontSizeMin, max = score.fontSizeMax;
            bool auto = score.enableAutoSizing; var wrap = score.textWrappingMode; var overflow = score.overflowMode;
            bool gradeActive = Text(h, "_gradeText").gameObject.activeSelf, countActive = Text(h, "_countText").gameObject.activeSelf;
            bool button = box.GetComponent<Button>().interactable;
            FinishWithGrade(h, PopupStoreGradeTier.C);
            // The memory fixture explicitly dispatches the real non-ExecuteAlways lifecycle callback.
            typeof(PopupStoreResultPanel).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h.Panel, null);
            Assert.That(h.Panel.IsVisible, Is.False); Assert.That(box.enabled, Is.False); Assert.That(box.sprite, Is.Null);
            Assert.That(box.rectTransform.anchoredPosition, Is.EqualTo(boxPosition)); Assert.That(box.rectTransform.sizeDelta, Is.EqualTo(boxSize));
            Assert.That(box.rectTransform.localScale, Is.EqualTo(boxScale)); Assert.That(box.preserveAspect, Is.EqualTo(aspect));
            Assert.That(box.raycastTarget, Is.EqualTo(raycast)); Assert.That(box.GetComponent<Button>().interactable, Is.EqualTo(button));
            Assert.That(score.rectTransform.anchoredPosition, Is.EqualTo(scorePosition)); Assert.That(score.rectTransform.sizeDelta, Is.EqualTo(scoreSize));
            Assert.That(score.alignment, Is.EqualTo(alignment)); Assert.That(score.fontSize, Is.EqualTo(font));
            Assert.That(score.fontSizeMin, Is.EqualTo(min)); Assert.That(score.fontSizeMax, Is.EqualTo(max));
            Assert.That(score.enableAutoSizing, Is.EqualTo(auto)); Assert.That(score.textWrappingMode, Is.EqualTo(wrap));
            Assert.That(score.overflowMode, Is.EqualTo(overflow));
            Assert.That(Text(h, "_gradeText").gameObject.activeSelf, Is.EqualTo(gradeActive));
            Assert.That(Text(h, "_countText").gameObject.activeSelf, Is.EqualTo(countActive)); h.AssertNoR1();
        }
    }

    [TestCase("_phaseSummaryBoxPosition")] [TestCase("_phaseSummaryScorePosition")]
    [TestCase("_phaseSummaryNoBoxScorePosition")] [TestCase("_phaseSummaryBoxSize")] [TestCase("_phaseSummaryScoreSize")]
    public void NonFiniteLayoutIsRejectedBeforeAnyResultMutation(string field)
    {
        using (var h = new Host())
        {
            SetVector(h.Panel, field, new Vector2(float.NaN, 100));
            Assert.That(h.Panel.EditorValidatePhaseSummaryLayout(out var error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(h.Panel.EditorShownPhaseResult, Is.Null);
            Assert.That(h.Panel.PresentationCount, Is.Zero); Assert.That(Box(h).enabled, Is.False);
        }
    }

    [TestCase("_phaseSummaryBoxSize")] [TestCase("_phaseSummaryScoreSize")]
    public void ZeroSizedContentIsRejected(string field)
    {
        using (var h = new Host())
        { SetVector(h.Panel, field, new Vector2(0, 100)); Assert.That(h.Panel.EditorValidatePhaseSummaryLayout(out _), Is.False); }
    }

    [Test]
    public void ReversedOrNonFiniteFontRangeIsRejected()
    {
        using (var h = new Host())
        {
            SetFloat(h.Panel, "_phaseSummaryMinimumScoreFontSize", 50);
            Assert.That(h.Panel.EditorValidatePhaseSummaryLayout(out _), Is.False);
            SetFloat(h.Panel, "_phaseSummaryMinimumScoreFontSize", 30);
            SetFloat(h.Panel, "_phaseSummaryScoreFontSize", float.PositiveInfinity);
            Assert.That(h.Panel.EditorValidatePhaseSummaryLayout(out _), Is.False);
        }
    }

    private static void FinishWithGrade(Host h, PopupStoreGradeTier grade)
    {
        for (int phase = 1; phase <= (int)grade; phase++)
        {
            h.ClearPhase();
            if (phase < (int)grade) h.NextPhase();
        }
        if (grade == PopupStoreGradeTier.SSS) h.Clock.AdvanceTime(100); // Complete the final Success cue only.
        else
        {
            h.NextPhase(); h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
        }
        h.ShowResult();
    }
    private static Image Box(Host h) => h.Root.GetComponent<PopupStoreRewardBox>().BoxImage;
    private static TMP_Text Text(Host h, string name) => Field<TMP_Text>(h.Panel, name);
    private static T Field<T>(Object target, string name) where T : Object
        => new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
    private static void SetVector(Object target, string name, Vector2 value)
    { var so = new SerializedObject(target); so.FindProperty(name).vector2Value = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetFloat(Object target, string name, float value)
    { var so = new SerializedObject(target); so.FindProperty(name).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
