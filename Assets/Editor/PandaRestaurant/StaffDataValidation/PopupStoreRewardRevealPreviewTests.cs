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
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual result-prefab preview tests. Existing finalized sales/decisions are read only;
/// no payment repository, start authority, SDK request or persistent account is exercised.</summary>
public sealed class PopupStoreRewardRevealPreviewTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string SourceFolder = "Assets/Scripts/UI/Floor3UI/";
    private const string Notice = "미지급 · 실제 계정에 반영되지 않음";
    private Dictionary<string, string> _protected;
    private string _account;
    private object _manager, _score;
    private BackendManager _backend;
    private bool _sdk;
    private UnityEngine.Random.State _random;

    [OneTimeSetUp]
    public void SnapshotProtectedDecisionPolicyTransactionAndApprovedArtFiles()
    {
        string[] protectedNames = { "PopupStoreRewardTable.cs", "PopupStoreRewardDecision.cs", "PopupStoreRewardSession.cs",
            "PopupStoreRewardPersistence.cs", "PopupStoreRewardTransaction.cs", "PopupStoreRewardRecovery.cs",
            "PopupStoreBusinessAccessPolicy.cs", "PopupStoreDailyUsage.cs", "PopupStoreBusinessStartTransaction.cs",
            "PopupStoreGradeSession.cs", "PopupStoreScore.cs", "PopupStorePacking.cs", "PopupStorePackingPresentation.cs" };
        var paths = protectedNames.Select(name => SourceFolder + name).ToList();
        paths.Add("Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset");
        paths.Add("Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStoreRewardFakeRepository.cs");
        paths.Add("Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStoreBusinessStartFakeRepository.cs");
        foreach (string grade in new[] { "C", "B", "A", "S", "SS", "SSS" }) paths.Add(SpritePath(grade));
        _protected = paths.ToDictionary(path => path, Hash);
    }

    [OneTimeTearDown]
    public void ProtectedSourcesAndOriginalPngsWereNotChangedByPreviewTests()
    { foreach (var pair in _protected) Assert.That(Hash(pair.Key), Is.EqualTo(pair.Value), pair.Key); }

    [SetUp]
    public void RecordAccountAndSdkWithoutStartingThem()
    {
        _account = AccountState(); _manager = Field(typeof(GameManager), "_instance").GetValue(null);
        _score = Field(typeof(UserInfo), "_score").GetValue(null); _backend = BackendManager.ExistingInstance;
        _sdk = BackEnd.Backend.IsInitialized; _random = UnityEngine.Random.state;
    }

    [TearDown]
    public void NoActualCurrencyDailyUsageManagerSdkOrUnityRandomStateWasChanged()
    {
        Assert.That(AccountState(), Is.EqualTo(_account)); Assert.That(Field(typeof(GameManager), "_instance").GetValue(null), Is.SameAs(_manager));
        Assert.That(Field(typeof(UserInfo), "_score").GetValue(null), Is.EqualTo(_score));
        Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void ActualPrefabAddsOnlyABoxSizedInputAndTwoDedicatedApprovedFontLabels()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var preview = prefab.GetComponent<PopupStoreRewardRevealPreview>();
        var panel = prefab.GetComponent<PopupStoreResultPanel>(); var box = prefab.GetComponent<PopupStoreRewardBox>();
        Assert.That(preview, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<PopupStoreRewardRevealPreview>(true), Has.Length.EqualTo(1));
        Assert.That(Read(preview, "_panel"), Is.SameAs(panel)); Assert.That(Read(preview, "_box"), Is.SameAs(box));
        Assert.That(Read(preview, "_reward"), Is.SameAs(prefab.GetComponent<PopupStoreRewardSession>()));
        Assert.That(Read(preview, "_grade"), Is.SameAs(prefab.GetComponent<PopupStoreGradeSession>()));
        var input = (Button)Read(preview, "_openButton");
        Assert.That(input.gameObject, Is.SameAs(box.BoxImage.gameObject)); Assert.That(input.targetGraphic, Is.SameAs(box.BoxImage));
        Assert.That(input.transition, Is.EqualTo(Selectable.Transition.None), "Disabled/pressed states must not tint approved art.");
        Assert.That(input.interactable, Is.False); Assert.That(box.BoxImage.raycastTarget, Is.False);
        var value = (TMP_Text)Read(preview, "_valueText"); var notice = (TMP_Text)Read(preview, "_noticeText");
        Assert.That(value, Is.Not.Null); Assert.That(notice, Is.Not.Null.And.Not.SameAs(value));
        var title = (TMP_Text)Read(panel, "_titleText");
        foreach (var label in new[] { value, notice })
        {
            Assert.That(label.font, Is.SameAs(title.font)); Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.text, Is.Empty, "No authored test amount/disclaimer leaks before Editor preview code enables it.");
            Assert.That(label.transform.IsChildOf((RectTransform)Read(panel, "_panel")), Is.True);
        }
        Assert.That((float)Read(preview, "_duration"), Is.InRange(.3f, .6f));
    }

    [TestCase(PopupStoreGradeTier.C, 2)]
    [TestCase(PopupStoreGradeTier.B, 3)]
    [TestCase(PopupStoreGradeTier.A, 4)]
    [TestCase(PopupStoreGradeTier.S, 6)]
    [TestCase(PopupStoreGradeTier.SS, 8)]
    [TestCase(PopupStoreGradeTier.SSS, 10)]
    public void EveryActualGradeRevealsOnlyItsAlreadyDecidedAmountAndUnchangedApprovedSprite(PopupStoreGradeTier grade, int diamonds)
    {
        using (var host = new Host(85))
        {
            host.Ready(grade); var snapshot = new Transactions(host); var decision = host.Session.Result;
            Assert.That(decision.Diamonds, Is.EqualTo(diamonds)); Assert.That(host.Preview.TryReveal(), Is.True);
            host.Preview.TickPreview(1);
            Assert.That(host.Preview.OpenedResult, Is.SameAs(host.Grade.FinalResult)); Assert.That(host.Preview.ShownDecision, Is.SameAs(decision));
            Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(grade))); Assert.That(host.Box.BoxImage.preserveAspect, Is.True);
            Assert.That(host.Value.text, Is.EqualTo("R0 테스트 보상 +" + diamonds + "D")); Assert.That(host.Notice.text, Is.EqualTo(Notice));
            Assert.That(host.Preview.IsPreviewVisible, Is.True); Assert.That(host.Preview.IsAnimating, Is.False);
            snapshot.AssertUnchanged(host); host.Confirm();
        }
    }

    [Test]
    public void NoSuccessfulOrdersHasNeitherBoxNorPreviewAndMakesNoRandomDraw()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.None, false);
            Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.RevealCount, Is.Zero);
            Assert.That(host.Preview.IsPreviewVisible, Is.False); Assert.That(host.Box.IsVisible, Is.False);
            Assert.That(host.Source.Calls, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.Zero); host.Confirm();
        }
    }

    [Test]
    public void InputWaitsForFinalResultPanelAppearanceAndBoxAppearanceToComplete()
    {
        using (var host = new Host())
        {
            Assert.That(host.Preview.TryReveal(), Is.False);
            host.Finish(PopupStoreGradeTier.C); Assert.That(host.Preview.TryReveal(), Is.False);
            host.Result.TickResult(0); Assert.That(host.Result.IsAppearing, Is.True); Assert.That(host.Preview.TryReveal(), Is.False);
            host.Result.TickResult(1); Assert.That(host.Box.IsAnimating, Is.True); Assert.That(host.Preview.TryReveal(), Is.False);
            host.Result.TickResult(1); Assert.That(host.Preview.Refresh(), Is.True);
            Assert.That(host.Preview.IsReady, Is.True); Assert.That(host.OpenButton.IsInteractable(), Is.True);
            Assert.That(host.Box.BoxImage.raycastTarget, Is.True); Assert.That(host.Preview.TryReveal(), Is.True);
        }
    }

    [Test]
    public void PendingDeliveryAndDepartureStillDelayResultAndPreview()
    {
        using (var host = new Host())
        {
            host.Call("StartCustomers", 3, 90d); var receipt = (PopupStorePackingReceipt)host.Call("Pack", 0);
            host.Call("End"); host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False); Assert.That(host.Preview.TryReveal(), Is.False);
            Assert.That(host.Queue.Single(), Is.SameAs(receipt)); host.Call("CompleteVisual", true); host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False); Assert.That(host.Preview.TryReveal(), Is.False);
            host.Call("FinishDepartures"); host.ShowReady(); Assert.That(host.Preview.TryReveal(), Is.True);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1)); Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ActualButtonSpamStartsOneShrinkFadeThenRestoresTheExactAuthoredVisual()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var snapshot = new Transactions(host);
            var scale = host.Box.BoxImage.rectTransform.localScale; var color = host.Box.BoxImage.color;
            host.OpenButton.onClick.Invoke(); Assert.That(host.Preview.IsAnimating, Is.True);
            for (int i = 0; i < 20; i++) { host.OpenButton.onClick.Invoke(); Assert.That(host.Preview.TryReveal(), Is.False); }
            host.Preview.TickPreview((float)Read(host.Preview, "_duration") * .4f);
            Assert.That(host.Box.BoxImage.rectTransform.localScale.x, Is.LessThan(scale.x));
            Assert.That(host.Box.BoxImage.color.a, Is.LessThan(color.a));
            Assert.That(host.Preview.IsPreviewVisible, Is.False);
            host.Preview.TickPreview(1);
            Assert.That(host.Box.BoxImage.rectTransform.localScale, Is.EqualTo(scale)); Assert.That(host.Box.BoxImage.color, Is.EqualTo(color));
            Assert.That(host.Preview.RevealCount, Is.EqualTo(1)); Assert.That(host.Preview.IsRevealed, Is.True);
            Assert.That(host.Preview.TryReveal(), Is.False); snapshot.AssertUnchanged(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForcedHideDuringOrAfterRevealRestoresTheSameUnconfirmedPreviewWithoutAnimation(bool finishAnimation)
    {
        using (var host = new Host(85))
        {
            host.Ready(PopupStoreGradeTier.S); var snapshot = new Transactions(host); Assert.That(host.Preview.TryReveal(), Is.True);
            host.Preview.TickPreview(finishAnimation ? 1f : .1f); host.ForceHide();
            Assert.That(host.Preview.IsAnimating, Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False);
            for (int i = 0; i < 5; i++) { Assert.That(host.Result.TryConfirm(), Is.False); Assert.That(host.Preview.TryReveal(), Is.False); }
            Assert.That(host.Result.ConfirmationCount, Is.Zero, "Forced cleanup does not acknowledge the result.");
            host.Reopen();
            Assert.That(host.Preview.IsPreviewVisible, Is.True); Assert.That(host.Preview.IsAnimating, Is.False);
            Assert.That(host.Preview.RevealCount, Is.EqualTo(1)); Assert.That(host.Value.text, Is.EqualTo("R0 테스트 보상 +6D"));
            Assert.That(host.Notice.text, Is.EqualTo(Notice)); Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.S)));
            Assert.That(host.Preview.TryReveal(), Is.False); snapshot.AssertUnchanged(host); host.Confirm();
        }
    }

    [Test]
    public void ForcedHideOfAnUnconfirmedResultDoesNotSpendItsFirstPreview()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); host.ForceHide(); host.Reopen();
            Assert.That(host.Preview.IsRevealed, Is.False); Assert.That(host.Preview.RevealCount, Is.Zero);
            Assert.That(host.Preview.TryReveal(), Is.True); Assert.That(host.Preview.RevealCount, Is.EqualTo(1));
            host.Confirm(); Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EitherDisableOrderAndForcedHideRestoreUnconfirmedBoxScaleAndColorIdempotently(bool boxFirst)
    {
        using (var host = new Host())
        {
            Write(host.Box, "_baseScale", .8f); host.Ready(PopupStoreGradeTier.C);
            var baseline = host.Box.BoxImage.rectTransform.localScale; var color = host.Box.BoxImage.color;
            Assert.That(host.Preview.TryReveal(), Is.True); host.Preview.TickPreview(.15f);
            if (boxFirst) { host.Box.HidePresentation(); host.Preview.HidePreview(); }
            else { host.Preview.HidePreview(); host.Box.HidePresentation(); }
            host.Preview.HidePreview(); host.Box.HidePresentation();
            host.ForceHide(); host.Reopen();
            Assert.That(host.Box.BoxImage.rectTransform.localScale, Is.EqualTo(baseline)); Assert.That(host.Box.BoxImage.color, Is.EqualTo(color));
            Assert.That(host.Preview.IsPreviewVisible, Is.True); Assert.That(host.Preview.IsAnimating, Is.False);
            Assert.That(host.Preview.RevealCount, Is.EqualTo(1));
            host.Confirm(); Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void FreshPopupInstanceDoesNotInheritAnotherInstancesOpenedStateOrDecision()
    {
        string decision;
        using (var first = new Host())
        { first.Ready(PopupStoreGradeTier.C); Assert.That(first.Preview.TryReveal(), Is.True); first.Preview.TickPreview(1); decision = first.Session.Result.DecisionId; }
        using (var second = new Host())
        {
            second.Ready(PopupStoreGradeTier.C); Assert.That(second.Session.Result.DecisionId, Is.Not.EqualTo(decision));
            Assert.That(second.Preview.IsRevealed, Is.False); Assert.That(second.Preview.RevealCount, Is.Zero); Assert.That(second.Preview.TryReveal(), Is.True);
        }
    }

    [Test]
    public void PreviewNeverAttemptsToResolveAnUndecidedRewardEvenIfItCouldBeResolved()
    {
        using (var host = new Host())
        {
            CallMethod(host.Session, "OnDestroy"); host.Finish(PopupStoreGradeTier.C); host.ShowReady(false);
            Assert.That(host.Session.IsResolved, Is.False); Assert.That(host.Session.Result, Is.Null);
            for (int i = 0; i < 8; i++) { host.Preview.Refresh(); Assert.That(host.Preview.TryReveal(), Is.False); host.Preview.TickPreview(1); }
            Assert.That(host.Source.Calls, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.Zero); Assert.That(host.Session.DecisionCount, Is.Zero);
            Assert.That(host.Preview.IsPreviewVisible, Is.False); host.Confirm();
        }
    }

    [Test]
    public void FailedR0ValidationCannotBeTurnedIntoARevealOrRetryDraw()
    {
        using (var host = new Host())
        {
            Write(host.Session, "_table", null); LogAssert.Expect(LogType.Error, new Regex("^PopupStoreRewardSession: local preview not decided\\."));
            host.Finish(PopupStoreGradeTier.C); host.ShowReady(false);
            Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False);
            Assert.That(host.Source.Calls, Is.Zero); Assert.That(host.Session.Result, Is.Null); host.Confirm();
        }
    }

    [TestCase("session")]
    [TestCase("grade")]
    [TestCase("replacement")]
    public void AReplacedOrForeignDecisionCannotBeDisplayedForThePinnedFinalResult(string defect)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var original = host.Session.Result;
            var replacement = NewDecision(defect == "session" ? "foreign-session" : original.SessionId,
                defect == "grade" ? PopupStoreGradeTier.S : original.Grade, original.Kind, original.Diamonds);
            Write(host.Session, "<Result>k__BackingField", replacement);
            try { Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False); Assert.That(host.Preview.LastError, Is.Not.Empty); }
            finally { Write(host.Session, "<Result>k__BackingField", original); }
            Assert.That(host.Preview.RevealCount, Is.Zero); host.Confirm();
        }
    }

    [Test]
    public void AnotherFinalResultObjectWithTheSameSessionValuesIsNotTheShownFinal()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var original = host.Grade.FinalResult;
            Assert.That(PopupStoreGradeCalculator.TryEvaluate(original.Score, original.SuccessfulOrders, out var evaluation, out _), Is.True);
            var copy = (PopupStoreGradeResult)Activator.CreateInstance(typeof(PopupStoreGradeResult), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { original.SessionId, evaluation }, CultureInfo.InvariantCulture);
            Write(host.Grade, "<FinalResult>k__BackingField", copy);
            try { Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False); }
            finally { Write(host.Grade, "<FinalResult>k__BackingField", original); }
            host.Confirm();
        }
    }

    [TestCase("_panel")]
    [TestCase("_box")]
    [TestCase("_reward")]
    [TestCase("_grade")]
    public void ForeignRootReferencesAreRejectedWithoutChangingTheOriginalResultOrConfirm(string field)
    {
        using (var host = new Host())
        using (var foreign = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var original = Read(host.Preview, field); var snapshot = new Transactions(host);
            Write(host.Preview, field, Read(foreign.Preview, field));
            try { Assert.That(host.Preview.TryReveal(), Is.False); }
            finally { Write(host.Preview, field, original); }
            Assert.That(host.Result.IsVisible, Is.True); snapshot.AssertUnchanged(host); host.Confirm();
        }
    }

    [TestCase("_valueText")]
    [TestCase("_noticeText")]
    [TestCase("_openButton")]
    public void MissingDedicatedPreviewReferenceCannotDisableTheNormalConfirmation(string field)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var original = Read(host.Preview, field); Write(host.Preview, field, null);
            try { Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Result.IsVisible, Is.True); }
            finally { Write(host.Preview, field, original); }
            host.Confirm();
        }
    }

    [TestCase("mapping")]
    [TestCase("missing-image")]
    [TestCase("wrong-grade")]
    public void MissingOrWrongApprovedSpriteBlocksPreviewWithoutChangingTheFinalResult(string defect)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var original = host.Box.BoxImage.sprite; var snapshot = new Transactions(host);
            if (defect == "mapping") Write(host.Box, "_cSprite", null);
            else host.Box.BoxImage.sprite = defect == "missing-image" ? null : Approved(PopupStoreGradeTier.S);
            try
            {
                Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.LastError, Is.Not.Empty);
                Assert.That(host.Preview.RevealCount, Is.Zero); Assert.That(host.Preview.IsPreviewVisible, Is.False);
                snapshot.AssertUnchanged(host);
            }
            finally { Write(host.Box, "_cSprite", original); host.Box.BoxImage.sprite = original; }
            host.Confirm();
        }
    }

    [TestCase("label")]
    [TestCase("button")]
    public void AccidentalExistingResultControlAssignmentsAreNotClearedOrDisabledByPreviewFailure(string defect)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var title = (TMP_Text)Read(host.Result, "_titleText");
            var confirm = (Button)Read(host.Result, "_confirmButton"); string text = title.text;
            string field = defect == "label" ? "_valueText" : "_openButton"; var original = Read(host.Preview, field);
            Write(host.Preview, field, defect == "label" ? (object)title : confirm);
            try
            {
                Assert.That(host.Preview.TryReveal(), Is.False);
                Assert.That(title.text, Is.EqualTo(text)); Assert.That(title.gameObject.activeSelf && title.enabled, Is.True);
                Assert.That(confirm.IsInteractable(), Is.True);
            }
            finally { Write(host.Preview, field, original); }
            host.Confirm();
        }
    }

    [Test]
    public void DestroyingTheOptionalPreviewLeavesTheExistingResultBoxAndFloor3ReturnWorking()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var host = new Host())
        {
            Object.DestroyImmediate(host.Preview); host.Finish(PopupStoreGradeTier.C); host.Call("ShowResult"); host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C)));
            Assert.That(host.Box.BoxImage.raycastTarget, Is.False); host.Confirm();
        }
        witness.AssertUnchanged("reward reveal missing optional component");
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidAnimationDeltaStopsOnlyThePreviewAndPreservesTheExit(float delta)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); var snapshot = new Transactions(host); Assert.That(host.Preview.TryReveal(), Is.True);
            host.Preview.TickPreview(delta);
            Assert.That(host.Preview.IsAnimating, Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False);
            Assert.That(host.Preview.RevealCount, Is.EqualTo(1)); snapshot.AssertUnchanged(host); host.Confirm();
        }
    }

    [TestCase("_duration", .299f)]
    [TestCase("_duration", .601f)]
    [TestCase("_duration", float.NaN)]
    [TestCase("_shrinkScale", 0f)]
    [TestCase("_shrinkScale", float.PositiveInfinity)]
    [TestCase("_minimumAlpha", -1f)]
    [TestCase("_minimumAlpha", 1.1f)]
    public void InvalidAuthoredTimingScaleOrAlphaRejectsOnlyThePreview(string field, float value)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); Write(host.Preview, field, value);
            Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.RevealCount, Is.Zero);
            Assert.That(host.Preview.IsAnimating, Is.False); Assert.That(host.Preview.LastError, Is.Not.Empty); host.Confirm();
        }
    }

    [TestCase("null")]
    [TestCase("empty")]
    [TestCase("value")]
    [TestCase("tangent")]
    public void InvalidCurveCannotLeaveTheBoxInAnUnboundedAnimation(string defect)
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C);
            AnimationCurve curve = defect == "null" ? null : defect == "empty" ? new AnimationCurve()
                : defect == "value" ? new AnimationCurve(new Keyframe(0, float.NaN), new Keyframe(1, 1))
                : new AnimationCurve(new Keyframe(0, 0, 0, float.PositiveInfinity), new Keyframe(1, 1));
            Write(host.Preview, "_curve", curve);
            Assert.That(host.Preview.TryReveal(), Is.False); Assert.That(host.Preview.IsAnimating, Is.False);
            Assert.That(host.Preview.LastError, Is.Not.Empty); host.Confirm();
        }
    }

    [Test]
    public void ADisabledPreviewCannotContinueTweenOrAcceptHiddenInput()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); Assert.That(host.Preview.TryReveal(), Is.True); host.Preview.TickPreview(.1f);
            host.Preview.enabled = false; CallMethod(host.Preview, "OnDisable"); var color = host.Box.BoxImage.color; var scale = host.Box.BoxImage.rectTransform.localScale;
            for (int i = 0; i < 10; i++) { host.Preview.TickPreview(1); Assert.That(host.Preview.TryReveal(), Is.False); }
            Assert.That(host.Box.BoxImage.color, Is.EqualTo(color)); Assert.That(host.Box.BoxImage.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(host.Preview.IsAnimating, Is.False); Assert.That(host.Preview.IsPreviewVisible, Is.False); host.Confirm();
        }
    }

    [Test]
    public void RevealUsesExplicitUnscaledDeltaEvenWhenGlobalTimeIsPaused()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); float previous = Time.timeScale;
            try { Time.timeScale = 0; Assert.That(host.Preview.TryReveal(), Is.True); host.Preview.TickPreview(1); Assert.That(host.Preview.IsPreviewVisible, Is.True); }
            finally { Time.timeScale = previous; }
        }
    }

    [Test]
    public void PlayerDisablePathClearsTestOnlyTextAndInputButPreservesTheResultAndConfirm()
    {
        Assert.That(PopupStoreRewardRevealPreview.IsSupported, Is.True, "This fixture is Editor-only; it does not claim a Player build was run.");
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.C); Assert.That(host.Preview.TryReveal(), Is.True); host.Preview.TickPreview(1);
            CallMethod(host.Preview, "DisablePreviewForPlayer");
            Assert.That(host.Value.text, Is.Empty); Assert.That(host.Notice.text, Is.Empty);
            Assert.That(host.Value.enabled && host.Value.gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Notice.enabled && host.Notice.gameObject.activeInHierarchy, Is.False);
            Assert.That(host.OpenButton.IsInteractable(), Is.False); Assert.That(host.Box.BoxImage.raycastTarget, Is.False);
            Assert.That(host.Box.IsVisible, Is.True); Assert.That(host.Result.IsVisible, Is.True); host.Confirm();
        }
        string source = File.ReadAllText(SourceFolder + "PopupStoreRewardRevealPreview.cs");
        string playerBranch = Regex.Match(source, @"#else\s+(public bool Refresh[\s\S]+?)#endif").Groups[1].Value;
        Assert.That(playerBranch, Does.Contain("Refresh() { DisablePreviewForPlayer(); return false; }"));
        Assert.That(playerBranch, Does.Contain("TryReveal() { DisablePreviewForPlayer(); return false; }"));
        Assert.That(playerBranch, Does.Contain("TickPreview(float unscaledDelta) => DisablePreviewForPlayer()"));
        Assert.That(playerBranch, Does.Not.Contain("R0 테스트 보상").And.Not.Contain("TryDecide").And.Not.Contain("NextInt"));
    }

    [Test]
    public void NewPreviewLabelsDoNotOverlapTheOriginalTextBoxOrConfirmationAtRestOrShrink()
    {
        using (var host = new Host())
        {
            host.Ready(PopupStoreGradeTier.SSS); var panel = (RectTransform)Read(host.Result, "_panel");
            var preserved = new[] { "_titleText", "_scoreText", "_gradeText", "_countText", "_confirmButton" }
                .Select(name => ((Component)Read(host.Result, name)).GetComponent<RectTransform>()).Concat(new[] { host.Box.BoxImage.rectTransform }).ToArray();
            Assert.That(host.Preview.TryReveal(), Is.True);
            foreach (float delta in new[] { .15f, 1f })
            {
                host.Preview.TickPreview(delta); Canvas.ForceUpdateCanvases();
                Rect value = BoundsWithin(host.Value.rectTransform, panel), notice = BoundsWithin(host.Notice.rectTransform, panel);
                AssertInside(value, panel.rect); AssertInside(notice, panel.rect); Assert.That(value.Overlaps(notice), Is.False);
                foreach (var control in preserved)
                {
                    Rect bounds = BoundsWithin(control, panel);
                    Assert.That(value.Overlaps(bounds), Is.False, host.Value.name + " overlaps " + control.name);
                    Assert.That(notice.Overlaps(bounds), Is.False, host.Notice.name + " overlaps " + control.name);
                }
            }
        }
    }

    [Test]
    public void AllSixOriginalApprovedPngHashesRemainPinnedAcrossThePreviewIntegration()
    {
        var expected = new Dictionary<string, string> {
            { "C", "cc9616ed1e83240d35f887318ac3929fc3c348375355a1a95fd5a529e3070e4f" },
            { "B", "c1eec03652e78d364ffd16bfb1f85bb44d88469d8242e47bdae286c70bd6f2c4" },
            { "A", "478fb0697068b66cd6200cabc9ae986acb1c39a0e5509955f3b66e7798bc075d" },
            { "S", "838682baef5be77959867c109e13c8dc2e3091b869456807616ff6b89ae13a35" },
            { "SS", "06504877e9d5187dcc6a2f6ae9b7ffe31489be6fe06642de993ec5229d1967db" },
            { "SSS", "d0f0617049c18507ad0abe4c8084635addf009209422de6f77eb7e8a798b30f5" } };
        foreach (var pair in expected) Assert.That(Hash(SpritePath(pair.Key)), Is.EqualTo(pair.Value), pair.Key);
    }

    private sealed class Source : IPopupStoreRewardRandomSource
    {
        private readonly int _value; internal int Calls { get; private set; }
        internal Source(int value) { _value = value; }
        public int NextInt(int exclusiveUpperBound) { Calls++; return _value; }
    }

    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal readonly Source Source;
        internal Host(int roll = 0)
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            Source = new Source(roll);
            try
            {
                // Historical R0 preview labels/amounts are intentionally covered independently of active R1.
                Write(Session, "_table", AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(
                    "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset"));
                Assert.That(Preview, Is.Not.Null); Assert.That(Session.TrySetRandomSource(Source), Is.True); Assert.That(Session.Refresh(), Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => Get<GameObject>("Root");
        internal PopupStoreRewardRevealPreview Preview => Root.GetComponent<PopupStoreRewardRevealPreview>();
        internal PopupStoreRewardSession Session => Root.GetComponent<PopupStoreRewardSession>();
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal PopupStoreResultPanel Result => Get<PopupStoreResultPanel>("Result");
        internal PopupStoreGradeSession Grade => Get<PopupStoreGradeSession>("Grade");
        internal PopupStoreScore Score => Get<PopupStoreScore>("Score");
        internal PopupStorePacking Packing => Get<PopupStorePacking>("Packing");
        internal PopupStorePackingPresentation Presentation => Get<PopupStorePackingPresentation>("Presentation");
        internal PopupStoreCustomerEntrance Entrance => Get<PopupStoreCustomerEntrance>("Entrance");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal TMP_Text Value => (TMP_Text)Read(Preview, "_valueText");
        internal TMP_Text Notice => (TMP_Text)Read(Preview, "_noticeText");
        internal Button OpenButton => (Button)Read(Preview, "_openButton");
        internal T Get<T>(string name) => (T)Read(_inner, name);
        internal object Call(string name, params object[] values) => CallMethod(_inner, name, values);
        internal void Finish(PopupStoreGradeTier tier)
        { Call("Start", 90d); if (tier != PopupStoreGradeTier.None) Call("SellUntil", tier); Call("End"); Assert.That(Grade.FinalResult.Grade, Is.EqualTo(tier)); }
        internal void Ready(PopupStoreGradeTier tier, bool expected = true) { Finish(tier); ShowReady(expected); }
        internal void ShowReady(bool expected = true)
        { Call("ShowResult"); Result.TickResult(1); Assert.That(Preview.Refresh(), Is.EqualTo(expected)); }
        internal void Confirm()
        {
            Assert.That(Result.TryConfirm(), Is.True); Assert.That(Root.activeSelf, Is.False); Call("DisableObservers");
            if (Preview != null) CallMethod(Preview, "OnDisable");
            Assert.That(Get<MobileUINavigation>("Navigation").FirstView, Is.SameAs(Get<UIFloor3>("Floor")));
            Assert.That(Get<MobileUINavigation>("Navigation").Count, Is.EqualTo(1));
        }
        internal void ForceHide()
        {
            // DEV-05D-3C: only unconfirmed forced cleanup retains this instance; normal confirmation retires it.
            Call("Close");
            if (Preview != null) CallMethod(Preview, "OnDisable");
        }
        internal void Reopen()
        {
            Call("Reopen"); CallMethod(Session, "OnEnable"); Result.TickResult(0);
            if (Preview != null) Preview.Refresh();
        }
        public void Dispose()
        {
            if (Session != null) CallMethod(Session, "OnDestroy");
            ((IDisposable)_inner).Dispose();
        }
    }

    private sealed class Transactions
    {
        private readonly PopupStoreGradeResult _final;
        private readonly PopupStoreRewardDecision _decision;
        private readonly int _score, _orders, _packs, _finalizations, _draws, _decisions, _sourceCalls;
        private readonly PopupStorePackingReceipt[] _committed, _queue;
        private readonly string[] _processed;
        internal Transactions(Host host)
        {
            _final = host.Grade.FinalResult; _decision = host.Session.Result; _score = host.Score.TotalScore; _orders = host.Score.SuccessfulOrderCount;
            _packs = host.Packing.SuccessfulPackCount; _finalizations = host.Grade.FinalizationCount; _draws = host.Session.RandomDrawCount;
            _decisions = host.Session.DecisionCount; _sourceCalls = host.Source.Calls;
            _committed = host.Packing.CommittedReceipts.ToArray(); _queue = host.Queue.ToArray(); _processed = host.Score.ProcessedReceiptIds.ToArray();
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(host.Grade.FinalResult, Is.SameAs(_final)); Assert.That(host.Session.Result, Is.SameAs(_decision));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(_finalizations)); Assert.That(host.Session.RandomDrawCount, Is.EqualTo(_draws));
            Assert.That(host.Session.DecisionCount, Is.EqualTo(_decisions)); Assert.That(host.Source.Calls, Is.EqualTo(_sourceCalls));
            Assert.That(host.Score.TotalScore, Is.EqualTo(_score)); Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(_orders));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_packs)); CollectionAssert.AreEqual(_committed, host.Packing.CommittedReceipts.ToArray());
            CollectionAssert.AreEqual(_queue, host.Queue.ToArray()); CollectionAssert.AreEquivalent(_processed, host.Score.ProcessedReceiptIds.ToArray());
        }
    }

    private static PopupStoreRewardDecision NewDecision(string session, PopupStoreGradeTier grade, PopupStoreRewardKind kind, int diamonds)
        => (PopupStoreRewardDecision)Activator.CreateInstance(typeof(PopupStoreRewardDecision), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { session, grade, kind, diamonds }, CultureInfo.InvariantCulture);
    private static string SpritePath(string grade) => "Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png";
    private static Sprite Approved(PopupStoreGradeTier grade) => AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(grade.ToString()));
    private static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
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
    private static object CallMethod(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    private static Rect BoundsWithin(RectTransform rectangle, RectTransform parent)
    {
        var corners = new Vector3[4]; rectangle.GetWorldCorners(corners); Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
        foreach (var corner in corners) { Vector2 local = parent.InverseTransformPoint(corner); min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
        return new Rect(min, max - min);
    }
    private static void AssertInside(Rect child, Rect parent)
    {
        Assert.That(child.xMin, Is.GreaterThanOrEqualTo(parent.xMin - .01f)); Assert.That(child.xMax, Is.LessThanOrEqualTo(parent.xMax + .01f));
        Assert.That(child.yMin, Is.GreaterThanOrEqualTo(parent.yMin - .01f)); Assert.That(child.yMax, Is.LessThanOrEqualTo(parent.yMax + .01f));
    }
}
#endif
