using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

/// <summary>Actual prefab transactions, terminal visuals and navigation in disposable memory instances.</summary>
public sealed class PopupStoreResultTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [Test]
    public void AuthoredResultIsOneEditableLocalPanelWithApprovedTypographyAndNoRewardBox()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var result = prefab.GetComponent<PopupStoreResultPanel>();
        Assert.That(result, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreResultPanel>(true), Has.Length.EqualTo(1));
        foreach (string field in new[] { "_grade", "_clock", "_packing", "_presentation", "_entrance", "_inputGroup" })
            Assert.That(((Component)Read(result, field)).gameObject, Is.SameAs(prefab), field);
        var overlay = (CanvasGroup)Read(result, "_overlay");
        var panel = (RectTransform)Read(result, "_panel");
        Assert.That(overlay.gameObject.activeSelf, Is.False);
        Assert.That(overlay.ignoreParentGroups, Is.True);
        Assert.That(panel.IsChildOf(overlay.transform), Is.True);
        var approved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/영덕대게체.asset");
        foreach (string field in new[] { "_titleText", "_scoreText", "_gradeText", "_countText", "_confirmText" })
        {
            var label = (TMP_Text)Read(result, field);
            Assert.That(label.transform.IsChildOf(panel), Is.True, field);
            Assert.That(label.font, Is.SameAs(approved), field);
            Assert.That(label.raycastTarget, Is.False, field);
        }
        var frame = (Image)Read(result, "_frame");
        var button = (Button)Read(result, "_confirmButton");
        Assert.That(frame.sprite, Is.Not.Null);
        Assert.That(button.transform.IsChildOf(panel), Is.True);
        Assert.That(button.targetGraphic as Image, Is.Not.Null);
        Assert.That(((Image)button.targetGraphic).sprite, Is.Not.Null);
        // Result artwork is restricted to the authored frame and confirmation control.
        // An unapproved grade-specific reward image cannot quietly become visible here.
        foreach (var image in panel.GetComponentsInChildren<Image>(true))
            Assert.That(image == frame || image.transform.IsChildOf(button.transform), Is.True, image.name);
    }

    [TestCase(PopupStoreGradeTier.None)]
    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void EveryFinalGradeDisplaysActualSalesSnapshotWithoutAnotherFinalization(PopupStoreGradeTier tier)
    {
        using (var host = new Host())
        {
            host.Start();
            if (tier != PopupStoreGradeTier.None) host.SellUntil(tier);
            host.End();
            var final = host.Grade.FinalResult;
            var transaction = new TransactionSnapshot(host);
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(final.SessionId, Is.EqualTo(host.Grade.SessionId));
            Assert.That(final.IsFinalized, Is.True);
            Assert.That(final.Grade, Is.EqualTo(tier));
            Assert.That(final.Score, Is.EqualTo(host.Score.TotalScore));
            Assert.That(final.SuccessfulOrders, Is.EqualTo(host.Score.SuccessfulOrderCount));
            Assert.That(host.Label("_titleText").text, Is.EqualTo("영업 종료"));
            Assert.That(host.Label("_scoreText").text, Is.EqualTo("최종 점수 " + final.Score));
            Assert.That(host.Label("_countText").text, Is.EqualTo("완료 주문 " + final.SuccessfulOrders + "건"));
            Assert.That(host.Label("_gradeText").text, Is.EqualTo(tier == PopupStoreGradeTier.None ? "최종 등급 없음" : "최종 등급 " + tier));
            Assert.That(host.Label("_confirmText").text, Is.EqualTo("확인"));
            Assert.That(host.Result.PresentationCount, Is.EqualTo(1));
            Assert.That(host.Result.AppearanceCount, Is.EqualTo(1));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            for (int i = 0; i < 4; i++) { Assert.That(host.Result.Refresh(), Is.True); host.Result.TickResult(1); }
            transaction.AssertUnchanged(host);
            Assert.That(host.Result.PresentationCount, Is.EqualTo(1));
            if (tier == PopupStoreGradeTier.None)
            {
                Assert.That(final.Score, Is.Zero);
                Assert.That(final.SuccessfulOrders, Is.Zero);
            }
            if (tier == PopupStoreGradeTier.C) Assert.That(final.Score, Is.EqualTo(100));
        }
    }

    [TestCase(PopupStoreBusinessState.NotStarted)]
    [TestCase(PopupStoreBusinessState.Running)]
    [TestCase(PopupStoreBusinessState.Paused)]
    public void NonEndedBusinessCannotBeFinalizedOrDisplayedByResultRefresh(PopupStoreBusinessState state)
    {
        using (var host = new Host())
        {
            if (state != PopupStoreBusinessState.NotStarted) host.Start();
            if (state == PopupStoreBusinessState.Paused) host.Clock.SetManualPaused(true);
            for (int i = 0; i < 3; i++) { Assert.That(host.Result.Refresh(), Is.True); host.Result.TickResult(10); }
            Assert.That(host.Clock.State, Is.EqualTo(state));
            Assert.That(host.Grade.FinalResult, Is.Null);
            Assert.That(host.Grade.FinalizationCount, Is.Zero);
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Result.TryConfirm(), Is.False);
            Assert.That(host.Navigation.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public void FinalizationDoesNotOpenUiUntilTheResultTickAndWaitingCustomersDoNotBlockIt()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.End();
            int expiredTimers = host.Patience.ExpiredCount;
            Assert.That(host.Grade.IsFinalized, Is.True);
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Result.Refresh(), Is.True);
            Assert.That(host.Result.IsVisible, Is.False, "Refresh must not advance business or show the panel.");
            host.ShowResult();
            for (int i = 0; i < 3; i++) Assert.That(host.Entrance.GetState(i), Is.EqualTo(PopupStoreCustomerState.Waiting));
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
            Assert.That(host.Patience.ExpiredCount, Is.EqualTo(expiredTimers), "The result does not expire or advance waiting customers.");
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [Test]
    public void QueuedAndCurrentReceiptsAreNotConsumedByResultAndAllExitsFinishBeforeDisplay()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var first = host.Pack(0);
            var second = host.Pack(1);
            host.End();
            var final = host.Grade.FinalResult;
            for (int i = 0; i < 4; i++) host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            CollectionAssert.AreEqual(new[] { first, second }, host.Queue.ToArray());
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            host.Presentation.TickPresentation(0);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(first));
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            CollectionAssert.AreEqual(new[] { second }, host.Queue.ToArray());
            host.CompleteVisual(false);
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            host.FinishDepartures();
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False, "The queued second receipt is still pending.");
            host.CompleteVisual();
            Assert.That(host.Entrance.GetState(1), Is.EqualTo(PopupStoreCustomerState.Leaving));
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False, "VisualEnded precedes the recipient's exit.");
            host.FinishDepartures();
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            CollectionAssert.AreEqual(new[] { first, second }, host.Presentation.ReceivedReceipts.ToArray());
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(2));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(2));
            Assert.That(host.Score.TotalScore, Is.EqualTo(240));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void DeadlineInEachSuccessfulVisualPhaseDoesNotHideCancelOrCoverTheDelivery(string phase)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var receipt = host.Pack(0);
            host.AdvanceVisualTo(phase);
            host.End();
            var final = host.Grade.FinalResult;
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
            Assert.That(host.Presentation.VisualOutcomes, Is.Empty);
            Assert.That(host.Presentation.gameObject.activeInHierarchy, Is.True);
            host.CompleteVisual(false);
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            host.FinishDepartures();
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(host.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ComplaintPenaltyDeadlineWaitsForComplaintExitAndPreservesEarlierSale(bool priorSale)
    {
        using (var host = new Host(new[] { 18d, 60d, 60d }))
        {
            host.StartCustomers(seconds: 20);
            if (priorSale) { host.Pack(1); host.CompleteVisual(); host.FinishDepartures(); }
            Assert.That(host.Clock.AdvanceTime(18), Is.True);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            var final = host.Grade.FinalResult;
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            host.Entrance.TickEntrance(host.ComplaintDuration);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            host.Result.TickResult(1);
            Assert.That(host.Result.IsVisible, Is.False);
            host.FinishDepartures();
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(final.Score, Is.EqualTo(priorSale ? 140 : 0));
            Assert.That(final.SuccessfulOrders, Is.EqualTo(priorSale ? 1 : 0));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(priorSale ? 2 : 1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidDeliveryTargetTerminatesTheVisualWithoutLosingScoreOrWaitingForever(bool disableComponent)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var receipt = host.Pack(0);
            host.AdvanceVisualTo("Move");
            host.End();
            var final = host.Grade.FinalResult;
            var target = host.Orders.Slots[0].CustomerImage;
            if (disableComponent) target.enabled = false; else target.gameObject.SetActive(false);
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            host.Presentation.TickPresentation(.01f);
            Assert.That(host.Presentation.CurrentReceipt, Is.Null);
            Assert.That(host.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(host.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(host.Score.TotalScore, Is.EqualTo(100));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
        }
    }

    [Test]
    public void ForeignSessionFinalResultIsRejectedWithoutRecalculationAndOriginalCanStillDisplay()
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            host.Start(0); other.Start(0);
            var original = host.Grade.FinalResult;
            Write(host.Grade, "<FinalResult>k__BackingField", other.Grade.FinalResult);
            try
            {
                ExpectResultError(); host.Result.TickResult(0);
                Assert.That(host.Result.IsVisible, Is.False);
                Assert.That(host.Result.TryConfirm(), Is.False);
                Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            }
            finally { Write(host.Grade, "<FinalResult>k__BackingField", original); }
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(original));
        }
    }

    [TestCase("_grade")]
    [TestCase("_clock")]
    [TestCase("_packing")]
    [TestCase("_presentation")]
    [TestCase("_entrance")]
    [TestCase("_inputGroup")]
    public void ForeignSourceCannotBindOrModifyAnotherPopup(string field)
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            object original = Read(host.Result, field);
            Write(host.Result, field, Read(other.Result, field));
            try
            {
                ExpectResultError();
                Assert.That(host.Result.Refresh(), Is.False);
                Assert.That(host.Result.IsVisible, Is.False);
                Assert.That(host.Result.TryConfirm(), Is.False);
                Assert.That(other.Input.interactable, Is.True);
                Assert.That(other.Input.blocksRaycasts, Is.True);
                Assert.That(other.Grade.FinalResult, Is.Null);
                Assert.That(other.Packing.CommittedReceipts, Is.Empty);
            }
            finally { Write(host.Result, field, original); }
            Assert.That(host.Result.Refresh(), Is.True);
        }
    }

    [Test]
    public void ConfirmDuringAppearanceIsIgnoredThenOneButtonClickReturnsToTheExistingFloor3()
    {
        using (var host = new Host())
        {
            host.Start(0);
            var final = host.Grade.FinalResult;
            int hidden = 0;
            host.Navigation.OnHideUIHandler += () => hidden++;
            host.Result.TickResult(0);
            Assert.That(host.Result.IsAppearing, Is.True);
            for (int i = 0; i < 5; i++) { Assert.That(host.Result.TryConfirm(), Is.False); host.Confirm.onClick.Invoke(); }
            Assert.That(host.Navigation.Count, Is.EqualTo(2));
            host.Result.TickResult(1);
            Assert.That(host.Confirm.IsInteractable(), Is.True);
            host.Confirm.onClick.Invoke();
            for (int i = 0; i < 5; i++) { host.Confirm.onClick.Invoke(); Assert.That(host.Result.TryConfirm(), Is.False); }
            Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1));
            Assert.That(hidden, Is.EqualTo(1));
            Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Floor.VisibleState, Is.EqualTo(VisibleState.Appeared));
            Assert.That(host.View.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(host.Root.activeSelf, Is.False);
            Assert.That(host.Result.IsVisible, Is.False);
            // Synchronous EditMode hosts must deliver the actual disabled lifecycle explicitly,
            // as Host.Close does. Verify navigation really hid the view before invoking it.
            host.DisableObservers();
            Assert.That(host.Input.interactable && host.Input.blocksRaycasts, Is.True);
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConfirmCannotPopAnUnrelatedViewOrAnUnsettledNavigation(bool transition)
    {
        using (var host = new Host())
        {
            host.Start(0); host.ShowResult();
            if (transition) host.Other.VisibleState = VisibleState.Disappearing;
            else host.Navigation.PushNoAnime("OtherView");
            int count = host.Navigation.Count;
            Assert.That(host.Result.TryConfirm(), Is.False);
            Assert.That(host.Navigation.Count, Is.EqualTo(count));
            Assert.That(host.Result.ConfirmationCount, Is.Zero);
            Assert.That(host.Navigation.CheckActiveView(UIPopupStore.ViewName), Is.True);
        }
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void ResultBlocksBackgroundGraphicsAndRestoresTheExactPriorInputFlags(bool interactable, bool raycasts)
    {
        using (var host = new Host())
        {
            host.Input.interactable = interactable; host.Input.blocksRaycasts = raycasts;
            host.Start(0); host.ShowResult();
            Assert.That(host.Input.interactable, Is.False);
            Assert.That(host.Input.blocksRaycasts, Is.False);
            var clear = (Button)Read(host.Counter, "_clearButton");
            Assert.That(clear.IsInteractable(), Is.False);
            Assert.That(clear.targetGraphic.Raycast(Vector2.zero, null), Is.False);
            Assert.That(host.Confirm.IsInteractable(), Is.True);
            Assert.That(host.Confirm.targetGraphic.Raycast(Vector2.zero, null), Is.True);
            Assert.That(host.Presentation.gameObject.activeInHierarchy, Is.True);
            Assert.That(host.Entrance.gameObject.activeInHierarchy, Is.True);
            Assert.That(host.Result.TryConfirm(), Is.True);
            Assert.That(host.View.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Assert.That(host.Root.activeSelf, Is.False);
            // The existing runner does not drive ordinary MonoBehaviour lifecycle in EditMode.
            host.DisableObservers();
            Assert.That(host.Input.interactable, Is.EqualTo(interactable));
            Assert.That(host.Input.blocksRaycasts, Is.EqualTo(raycasts));
            Assert.That(host.Overlay.gameObject.activeSelf, Is.False);
            Assert.That(host.Overlay.blocksRaycasts, Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClosingDuringOrAfterAppearanceReopensTheSameResultWithoutRepeatingTheAnimation(bool during)
    {
        using (var host = new Host())
        {
            host.Start(0); host.Result.TickResult(during ? 0 : 1);
            var final = host.Result.ShownResult;
            var transaction = new TransactionSnapshot(host);
            host.Close();
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Input.interactable && host.Input.blocksRaycasts, Is.True);
            for (int i = 0; i < 3; i++)
            {
                host.Reopen();
                Assert.That(host.Result.IsVisible, Is.False, "OnEnable/Refresh must wait for the later result tick.");
                host.Result.TickResult(0);
                Assert.That(host.Result.IsVisible, Is.True);
                Assert.That(host.Result.IsAppearing, Is.False);
                Assert.That(host.Result.ShownResult, Is.SameAs(final));
                Assert.That(host.Result.AppearanceCount, Is.EqualTo(1));
                host.Close();
            }
            Assert.That(host.Result.PresentationCount, Is.EqualTo(4));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Clock.TryStartSession(), Is.False);
            transaction.AssertUnchanged(host);
        }
    }

    [Test]
    public void DirectViewDisableAndReenableReleasesInputAndPreservesFinalIdentity()
    {
        using (var host = new Host())
        {
            host.Start(0); host.ShowResult();
            var final = host.Result.ShownResult;
            host.Root.SetActive(false);
            host.DisableObservers();
            Assert.That(host.Input.interactable && host.Input.blocksRaycasts, Is.True);
            Assert.That(host.Result.IsVisible, Is.False);
            host.View.Show(); host.RefreshServices();
            Assert.That(host.Result.IsVisible, Is.False);
            host.Result.TickResult(0);
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(host.Result.IsVisible, Is.True);
            Assert.That(host.Result.AppearanceCount, Is.EqualTo(1));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RefreshRebindsOnlyTheNewConfirmButtonAndDoesNotMultiplySubscriptions()
    {
        using (var host = new Host())
        {
            var original = host.Confirm;
            var go = new GameObject("replacement-confirm-memory", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(((RectTransform)Read(host.Result, "_panel")), false);
            var replacement = go.GetComponent<Button>();
            replacement.targetGraphic = go.GetComponent<Image>();
            Write(host.Result, "_confirmButton", replacement);
            for (int i = 0; i < 5; i++) Assert.That(host.Result.Refresh(), Is.True);
            host.Start(0); host.ShowResult();
            original.onClick.Invoke();
            Assert.That(host.Navigation.Count, Is.EqualTo(2));
            int hidden = 0; host.Navigation.OnHideUIHandler += () => hidden++;
            replacement.onClick.Invoke(); replacement.onClick.Invoke();
            Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1));
            Assert.That(hidden, Is.EqualTo(1));
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
        }
    }

    [Test]
    public void ClosingAnUnfinishedBusinessDoesNotCreateAnEndedResultOrRestartOnReentry()
    {
        using (var host = new Host())
        {
            host.StartCustomers(); host.Clock.AdvanceTime(2);
            double remaining = host.Clock.RemainingSeconds;
            host.Close();
            Assert.That(host.Grade.FinalResult, Is.Null);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            host.Reopen(); host.Result.TickResult(1);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(remaining).Within(.001));
            Assert.That(host.Grade.FinalizationCount, Is.Zero);
            Assert.That(host.Result.IsVisible, Is.False);
        }
    }

    [Test]
    public void CustomResultLabelsAndAnimationLeaveExistingScoreTimeGradeHudUntouched()
    {
        using (var host = new Host())
        {
            Write(host.Result, "_title", "CLOSED"); Write(host.Result, "_scoreFormat", "SCORE={0}");
            Write(host.Result, "_gradeFormat", "GRADE={0}"); Write(host.Result, "_countFormat", "ORDERS={0}");
            Write(host.Result, "_confirmLabel", "OK"); Write(host.Result, "_showDuration", .5f); Write(host.Result, "_startScale", .8f);
            Assert.That(host.Result.Refresh(), Is.True);
            host.StartCustomers(); host.Pack(0); host.CompleteVisual(); host.FinishDepartures(); host.End();
            var huds = new[] { (TMP_Text)Read(host.Score, "_scoreText"), (TMP_Text)Read(host.Clock, "_timeText"), (TMP_Text)Read(host.Grade, "_gradeText") };
            string[] texts = huds.Select(t => t.text).ToArray();
            var positions = huds.Select(t => t.rectTransform.anchoredPosition).ToArray();
            var sizes = huds.Select(t => t.rectTransform.sizeDelta).ToArray();
            var panel = (RectTransform)Read(host.Result, "_panel");
            Vector3 fullScale = panel.localScale;
            host.Result.TickResult(0);
            Assert.That(host.Result.IsAppearing, Is.True);
            Assert.That(host.Overlay.alpha, Is.Zero);
            Assert.That(panel.localScale, Is.EqualTo(fullScale * .8f));
            host.Result.TickResult(.25f);
            Assert.That(host.Result.IsAppearing, Is.True);
            Assert.That(host.Overlay.alpha, Is.GreaterThan(0).And.LessThan(1));
            host.Result.TickResult(.25f);
            Assert.That(host.Result.IsAppearing, Is.False);
            Assert.That(host.Overlay.alpha, Is.EqualTo(1));
            Assert.That(panel.localScale, Is.EqualTo(fullScale));
            Assert.That(host.Label("_titleText").text, Is.EqualTo("CLOSED"));
            Assert.That(host.Label("_scoreText").text, Is.EqualTo("SCORE=100"));
            Assert.That(host.Label("_gradeText").text, Is.EqualTo("GRADE=C"));
            Assert.That(host.Label("_countText").text, Is.EqualTo("ORDERS=1"));
            Assert.That(host.Label("_confirmText").text, Is.EqualTo("OK"));
            CollectionAssert.AreEqual(texts, huds.Select(t => t.text).ToArray());
            CollectionAssert.AreEqual(positions, huds.Select(t => t.rectTransform.anchoredPosition).ToArray());
            CollectionAssert.AreEqual(sizes, huds.Select(t => t.rectTransform.sizeDelta).ToArray());
        }
    }

    [Test]
    public void BrokenInspectorFormatDoesNotPartiallyLockTheViewOrChangeTheFinalResult()
    {
        using (var host = new Host())
        {
            host.Start(0);
            var final = host.Grade.FinalResult;
            Write(host.Result, "_scoreFormat", "{missing}");
            ExpectResultError(); host.Result.TickResult(0);
            Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Input.interactable && host.Input.blocksRaycasts, Is.True);
            Assert.That(host.Result.PresentationCount, Is.Zero);
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Write(host.Result, "_scoreFormat", "최종 점수 {0}");
            host.ShowResult();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
        }
    }

    [Test]
    public void DisposalRestoresSceneUndoSelectionManagersAndAccountWithoutSavingAnything()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var account = new AccountSnapshot();
        using (var host = new Host()) { host.Start(0); host.ShowResult(); Assert.That(host.Result.TryConfirm(), Is.True); }
        account.AssertUnchanged();
        witness.AssertUnchanged("popup-store result memory tests");
    }

    private sealed class Host : IDisposable
    {
        private readonly GameObject _scope;
        private readonly object _previousPopup, _previousPopupOpen;
        private readonly AccountSnapshot _account = new AccountSnapshot();
        internal readonly GameObject Root;
        internal readonly UIPopupStore View, Other;
        internal readonly UIFloor3 Floor;
        internal readonly MobileUINavigation Navigation;
        internal readonly Button Entry;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreCustomerPatience Patience;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly PopupStoreScore Score;
        internal readonly PopupStoreGradeSession Grade;
        internal readonly PopupStoreResultPanel Result;
        internal Button Confirm => (Button)Read(Result, "_confirmButton");
        internal CanvasGroup Input => (CanvasGroup)Read(Result, "_inputGroup");
        internal CanvasGroup Overlay => (CanvasGroup)Read(Result, "_overlay");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");
        internal float ComplaintDuration => (float)Read(Entrance, "_complaintDuration");
        internal TMP_Text Label(string name) => (TMP_Text)Read(Result, name);

        internal Host(double[] patience = null)
        {
            _previousPopup = ReadStatic(typeof(PopupManager), "_uiPopup");
            _previousPopupOpen = ReadStatic(typeof(PopupManager), "_isShowPopup");
            _scope = new GameObject("popup-store-result-memory-host");
            _scope.SetActive(false);
            try
            {
                WriteStatic(typeof(PopupManager), "_uiPopup", null); WriteStatic(typeof(PopupManager), "_isShowPopup", false);
                Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), _scope.transform, false);
                Root.name = "popup-store-result-memory-instance";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>(); Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>(); Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true); Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>(); Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>(); Score = Root.GetComponent<PopupStoreScore>();
                Grade = Root.GetComponent<PopupStoreGradeSession>(); Result = Root.GetComponent<PopupStoreResultPanel>();
                Assert.That(Result, Is.Not.Null, "The actual popup prefab must own its authored result component.");
                Write(Clock, "_autoStartForEditorReview", false);
                if (patience != null)
                {
                    Write(Patience, "_onePlushSeconds", patience[0]); Write(Patience, "_twoPlushSeconds", patience[1]); Write(Patience, "_threePlushSeconds", patience[2]);
                }
                Navigation = Create<MobileUINavigation>("result-test-navigation");
                Floor = Create<UIFloor3>("result-test-floor3"); Entry = Create<Button>("result-test-entry");
                Other = Create<UIPopupStore>("result-test-other-view"); Other.gameObject.AddComponent<Canvas>();
                Write(Other, "_closeButton", Create<Button>("other-close"));
                Write(Floor, "_buttons", new[] { Entry }); Write(Floor, "_popupStoreButton", Entry); Write(Floor, "_popupStoreView", View);
                Write(Navigation, "_uiViews", new[] {
                    new MobileViewDicStruct { Name = "UIFloor3", UIView = Floor },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = View },
                    new MobileViewDicStruct { Name = "OtherView", UIView = Other } });
                Invoke(Navigation, "Init");
                _scope.SetActive(true);
                Navigation.PushNoAnime("UIFloor3");
                Entry.onClick.Invoke();
                Assert.That(Navigation.FirstView, Is.SameAs(View));
                RefreshServices();
            }
            catch { Dispose(); throw; }
        }
        private T Create<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_scope.transform, false);
            return go.AddComponent<T>();
        }
        internal void RefreshServices()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True); Assert.That(Progress.Refresh(), Is.True); Assert.That(Packing.Refresh(), Is.True);
            Assert.That(Entrance.BeginVisibleSession(), Is.True); Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Patience.Refresh(), Is.True); Assert.That(Score.Refresh(), Is.True); Assert.That(Grade.Refresh(), Is.True);
            Assert.That(Result.Refresh(), Is.True);
        }
        internal void Start(double seconds = 90) { Write(Clock, "_initialSeconds", seconds); Assert.That(Clock.TryStartSession(), Is.True); }
        internal void StartCustomers(int count = 3, double seconds = 90)
        {
            Start(seconds); Entrance.TickEntrance(0);
            for (int i = 1; i < count; i++) Entrance.TickEntrance(4);
            Entrance.TickEntrance((float)Read(Entrance, "_entryDuration") + .01f);
            for (int i = 0; i < count; i++) Assert.That(Entrance.IsWaiting(i), Is.True, "arrival " + i);
        }
        internal PopupStorePackingReceipt Pack(int slot)
        {
            Assert.That(SourceOrders[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            foreach (var material in materials) Assert.That(Counter.TryAdd(material), Is.True);
            var packed = Packing.TryPack(); Assert.That(packed.Success, Is.True);
            return packed.Receipt;
        }
        internal void SellUntil(PopupStoreGradeTier tier)
        {
            int safety = 0;
            while (Grade.Current.Grade != tier && safety++ < 2000)
            {
                Entrance.TickEntrance(.5f);
                int slot = Enumerable.Range(0, 3).Where(Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                Pack(slot); CompleteVisual(); FinishDepartures();
            }
            Assert.That(safety, Is.LessThan(2000));
            Assert.That(Grade.Current.Grade, Is.EqualTo(tier));
            Assert.That(Entrance.SuppliedOrderCount, Is.LessThanOrEqualTo(20));
        }
        internal void AdvanceVisualTo(string phase)
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void CompleteVisual(bool begin = true)
        {
            if (begin) Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
        }
        internal void FinishDepartures() => Entrance.TickEntrance(ExitDuration + .01f);
        internal void End() { Clock.AdvanceTime(90); Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended)); Assert.That(Grade.IsFinalized, Is.True); }
        internal void ShowResult() { Result.TickResult(0); Result.TickResult(1); Assert.That(Result.IsVisible, Is.True); Assert.That(Result.IsAppearing, Is.False); }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            Assert.That(View.TryClose(), Is.True);
            DisableObservers();
        }
        internal void DisableObservers()
        {
            Entrance.PauseVisibleSession(); Presentation.EndVisibleSession();
            foreach (var component in new Component[] { Patience, Packing, Progress, Counter, Orders, Score, Grade, Result }) Invoke(component, "OnDisable");
        }
        internal void Reopen() { Entry.onClick.Invoke(); Assert.That(Navigation.FirstView, Is.SameAs(View)); RefreshServices(); }
        public void Dispose()
        {
            try
            {
                if (Result != null) Invoke(Result, "OnDisable");
                if (Entrance != null) Entrance.PauseVisibleSession();
                if (Presentation != null)
                {
                    if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                    Presentation.EndVisibleSession();
                }
                if (_scope != null) Object.DestroyImmediate(_scope);
            }
            finally
            {
                WriteStatic(typeof(PopupManager), "_uiPopup", _previousPopup); WriteStatic(typeof(PopupManager), "_isShowPopup", _previousPopupOpen);
                _account.AssertUnchanged();
            }
        }
    }

    private sealed class TransactionSnapshot
    {
        private readonly PopupStoreGradeResult _final;
        private readonly int _score, _orders, _packs, _finalizations;
        private readonly PopupStorePackingReceipt[] _committed, _queued;
        private readonly string[] _processed;
        internal TransactionSnapshot(Host host)
        {
            _final = host.Grade.FinalResult; _score = host.Score.TotalScore; _orders = host.Score.SuccessfulOrderCount;
            _packs = host.Packing.SuccessfulPackCount; _finalizations = host.Grade.FinalizationCount;
            _committed = host.Packing.CommittedReceipts.ToArray(); _queued = host.Queue.ToArray(); _processed = host.Score.ProcessedReceiptIds.ToArray();
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(host.Grade.FinalResult, Is.SameAs(_final)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(_finalizations));
            Assert.That(host.Score.TotalScore, Is.EqualTo(_score)); Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(_orders));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_packs));
            CollectionAssert.AreEqual(_committed, host.Packing.CommittedReceipts.ToArray()); CollectionAssert.AreEqual(_queued, host.Queue.ToArray());
            CollectionAssert.AreEquivalent(_processed, host.Score.ProcessedReceiptIds.ToArray());
        }
    }
    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = ReadStatic(typeof(UserInfo), "_score"), _manager = ReadStatic(typeof(GameManager), "_instance");
        private readonly BackendManager _backend = BackendManager.ExistingInstance;
        private readonly bool _sdk = BackEnd.Backend.IsInitialized;
        private readonly float _timeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits)); Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(ReadStatic(typeof(UserInfo), "_score"), Is.EqualTo(_score)); Assert.That(ReadStatic(typeof(GameManager), "_instance"), Is.SameAs(_manager));
            Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static void ExpectResultError() => LogAssert.Expect(LogType.Error, new Regex("^PopupStoreResultPanel:"));
    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name); method.Invoke(target, null);
    }
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static object ReadStatic(Type type, string name) => Field(type, name).GetValue(null);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static void WriteStatic(Type type, string name, object value) => Field(type, name).SetValue(null, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
}
