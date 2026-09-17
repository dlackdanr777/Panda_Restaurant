#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.Tween;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Actual detached Stage1 card/slot controls, with read-only fixed presentation data.
// These tests do not initialize the SDK or run a game's purchase/grant entry point.
public sealed partial class StaffGachaOfflineSessionTests
{
    private bool _resultRaycastOwnsPlaySettings;
    private bool _resultRaycastOriginalOptionsEnabled;
    private EnterPlayModeOptions _resultRaycastOriginalOptions;
    private SceneAsset _resultRaycastOriginalStartScene;
    private OfflineNavigationView _resultRaycastView;
    private StaffGachaOfflineSession _resultRaycastSession;
    private PopupItemResources _resultRaycastItems;

    [UnityTest]
    public IEnumerator ResultPopup_PlayerLoopRaycast_AllElevenStaffAndItemCardsKeepSummaryUntilBackdrop()
    {
        Assert.That(Application.isBatchMode && !Application.isPlaying, Is.True,
            "Run only in an isolated batch test runner, never in a user's playing editor");
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        Assert.That(typeof(Muks.BackEnd.BackendManager).GetField("_instance",
            BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
        Assert.That(Resources.FindObjectsOfTypeAll<MainScene>()
            .Any(scene => scene != null && scene.gameObject.scene.IsValid()), Is.False);

        _resultRaycastOriginalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _resultRaycastOriginalOptions = EditorSettings.enterPlayModeOptions;
        _resultRaycastOriginalStartScene = EditorSceneManager.playModeStartScene;
        _resultRaycastOwnsPlaySettings = true;
        string originalUser = PopupUserInfoState();
        _resultRaycastSession = new StaffGachaOfflineSession(Catalog());
        _resultRaycastView = new OfflineNavigationView(_resultRaycastSession.Owner, false,
            includeItemPresentation: true, playerLoop: true);
        var ui = _resultRaycastView;
        var session = _resultRaycastSession;
        // Keep the detached owner alive in the same unsaved test scene. Its explicit
        // Editor owner fence returns before BackendManager's SDK initialization.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(session.Owner.gameObject, ui.Root.scene);
        Assert.That(session.Owner.IsEditorOfflineOwner, Is.True);
        Assert.That(Resources.FindObjectsOfTypeAll<Muks.BackEnd.BackendManager>()
            .Where(owner => owner != null && owner.gameObject.scene.IsValid())
            .All(owner => owner == session.Owner), Is.True);
        Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount, Is.EqualTo(1));
        Assert.That(ui.Root.scene.GetRootGameObjects().All(root => root == ui.Root || root == session.Owner.gameObject), Is.True);
        Assert.That(ui.Root.activeInHierarchy, Is.False);
        EditorSceneManager.playModeStartScene = null;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        yield return new EnterPlayMode(false);

        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        ui.Root.SetActive(true);
        yield return null; // Actual Awake/Start registers the copied native UI and raycasters.
        StaffGachaOfflineViewFactory.BeginNavigation(ui.View);
        ui.ClickEntry();
        yield return null;
        Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
        Assert.That(session.ReplySuccess(), Is.True);
        Assert.That(ui.Display.TryShowCompleted(false, out error), Is.True, error);
        yield return null;
        var request = session.Request;
        string plan = JsonConvert.SerializeObject(request.Plan);
        string account = JsonConvert.SerializeObject(session.Account);
        int writes = session.PurchaseWrites + session.FollowupWrites;
        int draws = session.DrawCount;
        var staffCard = Reference<UIGachaCard>(ui.Staff, "_gachaCard");
        string bonus = PopupCardState(staffCard);
        var staffSlots = RuntimeReference<List<UIGachaCardSlot>>(ui.Staff, "_getStaffSlotList");
        Assert.That(Reference<Button>(ui.Staff, "_skipButton").gameObject.activeInHierarchy, Is.False);
        for (int index = 0; index < 13; index++)
        {
            int result = Math.Min(index, 10);
            Button selected = result < 10 ? staffSlots[result].GetComponent<Button>() : staffCard.GetComponent<Button>();
            Vector2 point = index == 11 ? new Vector2(.1f, .9f) : index == 12 ? new Vector2(.9f, .1f) : new Vector2(.5f, .5f);
            ResultPopupRaycastClick(selected, (RectTransform)selected.transform, point);
            yield return null; // Newly created popup must participate in a real canvas frame.
            var popup = RuntimeReference<GachaResultCardPopup>(ui.Display, "_popup");
            AssertPopupCenteredAndDismissible(popup, ui.View.transform, actualInput: true);
            NativeResultAssertText(popup.Card, request.DrawnStaff[result], request.Plan.AccountResult.Acquisition.Items[result]);
            Assert.That(PopupCardState(staffCard), Is.EqualTo(bonus));
            DismissPopup(popup, ui.View.transform, actualInput: true);
            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Summary"));
            Assert.That(ui.Display.EditorVisibleSlotCount, Is.EqualTo(10));
            yield return null;
        }
        NativeResultClock(ui.Display, "_nextInput");
        ResultPopupRaycastClick(Reference<Button>(ui.Staff, "_screenButton"),
            (RectTransform)Reference<Button>(ui.Staff, "_screenButton").transform, new Vector2(.5f, .08f));
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Closed"));
        yield return null;
        ui.ClickArrow("_leftButton");
        yield return null;
        Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));

        // Fixed, display-only item results use the same native summary fixtures.
        // No Purchase/GetItem/StartAddItem or account restore is called here.
        _resultRaycastItems = new PopupItemResources();
        List<GachaItemData> results = _resultRaycastItems.Data.Take(11).ToList();
        Animator itemAnimator = Reference<Animator>(ui.Item, "_gachaMacineAnimator");
        itemAnimator.fireEvents = false;
        itemAnimator.Play("Base Layer.ZoomIn Item", 0, 1f);
        itemAnimator.Update(0f);
        itemAnimator.fireEvents = true;
        PopupWrite(ui.Item, "_getItemList", results);
        PopupWrite(ui.Item, "_getItemIndex", 11);
        PopupWrite(ui.Item, "_currentStep", 5);
        PopupWrite(ui.Item, "_isShowingSummary", true);
        PopupWrite(ui.Item, "_summaryComplete", true);
        PopupWrite(ui.Item, "_screenTouchWaitTime", 0f);
        var itemCard = Reference<UIGachaCard>(ui.Item, "_skinGachaCard");
        itemCard.SetData(results[10]); itemCard.SetPosition(new Vector3(600f, 0f));
        itemCard.SetScale(1f); itemCard.gameObject.SetActive(true);
        var itemSlots = RuntimeReference<List<UIGachaCardSlot>>(ui.Item, "_getItemSlotList");
        for (int i = 0; i < 10; i++) { itemSlots[i].SetData(results[i]); itemSlots[i].gameObject.SetActive(true); }
        Reference<Transform>(ui.Item, "_getItemSlotFrame").gameObject.SetActive(true);
        Reference<Button>(ui.Item, "_skipButton").gameObject.SetActive(false);
        InvokePrivate(ui.Item, "PrepareSummaryInspection");
        yield return null;
        bonus = PopupCardState(itemCard);
        for (int index = 0; index < 13; index++)
        {
            int result = Math.Min(index, 10);
            Button selected = result < 10 ? itemSlots[result].GetComponent<Button>() : itemCard.GetComponent<Button>();
            Vector2 point = index == 11 ? new Vector2(.1f, .9f) : index == 12 ? new Vector2(.9f, .1f) : new Vector2(.5f, .5f);
            ResultPopupRaycastClick(selected, (RectTransform)selected.transform, point);
            yield return null;
            var popup = RuntimeReference<GachaResultCardPopup>(ui.Item, "_resultPopup");
            AssertPopupCenteredAndDismissible(popup, ui.View.transform, actualInput: true);
            Assert.That(Reference<TextMeshProUGUI>(popup.Card, "_nameText").text, Is.EqualTo(results[result].Name));
            Assert.That(PopupCardState(itemCard), Is.EqualTo(bonus));
            DismissPopup(popup, ui.View.transform, actualInput: true);
            Assert.That(ui.Item.EditorSummaryComplete, Is.True);
            Assert.That(ui.Item.EditorResultIndex, Is.EqualTo(11));
            yield return null;
        }
        PopupWrite(ui.Item, "_screenTouchWaitTime", 0f);
        ResultPopupRaycastClick(Reference<Button>(ui.Item, "_screenButton"),
            (RectTransform)Reference<Button>(ui.Item, "_screenButton").transform, new Vector2(.5f, .08f));
        for (int frame = 0; frame < 120 && ui.Item.EditorSummaryComplete; frame++) yield return null;
        Assert.That(ui.Item.EditorSummaryComplete, Is.False, "The real Stop -> Idle animation must close summary");
        Assert.That(ui.Item.EditorVisibleSlotCount, Is.Zero);
        Assert.That(PopupUserInfoState(), Is.EqualTo(originalUser));
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        Assert.That(session.Request, Is.SameAs(request));
        Assert.That(request.CompletionCount, Is.EqualTo(1));
        Assert.That(session.PurchaseWrites + session.FollowupWrites, Is.EqualTo(writes));
        Assert.That(session.DrawCount, Is.EqualTo(draws));
        Assert.That(session.RecordNotifications, Is.EqualTo(1));
        Assert.That(session.WalletNotifications, Is.EqualTo(1));
        Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(plan));
        Assert.That(JsonConvert.SerializeObject(session.Account), Is.EqualTo(account));
    }

    [UnityTearDown]
    public IEnumerator ResultPopup_PlayerLoopRestoresEditorAndOfflineScope()
    {
        if (!_resultRaycastOwnsPlaySettings) yield break;
        if (Application.isPlaying)
        {
            if (_resultRaycastView?.Root != null)
            {
                // Public machine close restores lifted summary parents while the
                // hierarchy is active, before Unity begins its OnDisable traversal.
                _resultRaycastView.Item.Hide();
                _resultRaycastView.Staff.Hide();
                _resultRaycastView.Root.SetActive(false);
            }
            yield return new ExitPlayMode();
        }
        try
        {
            _resultRaycastView?.Dispose();
            _resultRaycastItems?.Dispose();
            _resultRaycastSession?.Dispose();
        }
        finally
        {
            _resultRaycastView = null; _resultRaycastItems = null; _resultRaycastSession = null;
            EditorSettings.enterPlayModeOptions = _resultRaycastOriginalOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _resultRaycastOriginalOptionsEnabled;
            EditorSceneManager.playModeStartScene = _resultRaycastOriginalStartScene;
            _resultRaycastOwnsPlaySettings = false;
        }
    }

    [Test]
    public void ResultPopup_StaffAllElevenSelectionsPreserveBonusAndConfirmedReceipt()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        using (var ui = new OfflineNavigationView(session.Owner, true))
        {
            ui.ClickEntry();
            Reference<Animator>(ui.Staff, "_gachaMacineAnimator").Update(0f);
            Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
            Assert.That(session.ReplySuccess(), Is.True);
            Assert.That(ui.Display.TryShowCompleted(false, out error), Is.True, error);
            var request = session.Request;
            string plan = JsonConvert.SerializeObject(request.Plan);
            string account = JsonConvert.SerializeObject(session.Account);
            string followup = session.FollowupPayload;
            int followupWrites = session.FollowupWrites;
            var final = Reference<UIGachaCard>(ui.Staff, "_gachaCard");
            string finalState = PopupCardState(final);
            Vector3 finalPosition = final.transform.position;
            Assert.That(request.Plan.AccountResult.Acquisition.Items[0].IsNew, Is.True);
            Assert.That(request.Plan.AccountResult.Acquisition.Items[1].IsDuplicate, Is.True);
            Assert.That(request.DrawnStaff[0].Id, Is.EqualTo(request.DrawnStaff[1].Id),
                "Same staff ID must retain its distinct new/duplicate receipt per result");

            var slots = RuntimeReference<List<UIGachaCardSlot>>(ui.Staff, "_getStaffSlotList");
            for (int index = 0; index < 11; index++)
            {
                Button selected = index < 10 ? slots[index].GetComponent<Button>() : final.GetComponent<Button>();
                Assert.That(selected != null && selected.interactable, Is.True, "Actual result selection " + index);
                ResultPopupStateClick(selected);
                var popup = RuntimeReference<GachaResultCardPopup>(ui.Display, "_popup");
                AssertPopupCenteredAndDismissible(popup, ui.View.transform);
                NativeResultAssertText(popup.Card, request.DrawnStaff[index], request.Plan.AccountResult.Acquisition.Items[index]);
                Assert.That(PopupCardState(final), Is.EqualTo(finalState), "Selecting another result cannot replace the +1");
                Assert.That(final.transform.position, Is.EqualTo(finalPosition));
                DismissPopup(popup, ui.View.transform);
                Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Summary"));
                Assert.That(ui.Display.EditorVisibleSlotCount, Is.EqualTo(10));
                Assert.That(PopupCardState(final), Is.EqualTo(finalState));
            }
            AssertBonusCardCorners(final, ui.View.transform,
                () => RuntimeReference<GachaResultCardPopup>(ui.Display, "_popup"));
            Assert.That(ui.Display.SelectCard(-1), Is.False);
            Assert.That(ui.Display.SelectCard(11), Is.False);
            Assert.That(ui.Display.SelectCard(0), Is.True);
            var retainedPopup = RuntimeReference<GachaResultCardPopup>(ui.Display, "_popup");
            ui.ClickResultClose();
            Assert.That(retainedPopup.IsOpen, Is.False);
            ui.Display.Tick();
            ui.ClickArrow("_leftButton"); ui.ClickArrow("_rightButton");
            ui.Display.Tick(); // EditMode has no Update after the native machine's Show reset.
            Assert.That(retainedPopup.IsOpen, Is.False);
            Assert.That(ui.Display.EditorIsResultVisible, Is.False);
            Button retiredResultButton = Reference<Button>(ui.Staff, "_skipButton");
            Assert.That(retiredResultButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(retiredResultButton.interactable, Is.False);
            retiredResultButton.onClick.Invoke();
            Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False);
            Assert.That(ui.Display.SelectCard(10), Is.False);
            Assert.That(retainedPopup.IsOpen, Is.False,
                "The retired result control cannot reopen the dismissed inspection or summary");
            Assert.That(session.Request, Is.SameAs(request));
            Assert.That(request.CompletionCount, Is.EqualTo(1));
            Assert.That(session.PurchaseWrites, Is.EqualTo(1));
            Assert.That(session.FollowupWrites, Is.EqualTo(followupWrites));
            Assert.That(session.FollowupPayload, Is.EqualTo(followup));
            Assert.That(session.DrawCount, Is.EqualTo(11));
            Assert.That(session.RecordNotifications, Is.EqualTo(1));
            Assert.That(session.WalletNotifications, Is.EqualTo(1));
            Assert.That(session.Diamonds, Is.EqualTo(10));
            Assert.That(JsonConvert.SerializeObject(session.Account), Is.EqualTo(account));
            Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(plan));
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void ResultPopup_ItemAllElevenOriginalSlotsAndBonusAreReadOnlyAndCancelOnHide()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        using (var ui = new OfflineNavigationView(session.Owner, false, includeItemPresentation: true))
        using (var items = new PopupItemResources())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var list = RuntimeReference<List<UIGachaCardSlot>>(ui.Item, "_getItemSlotList");
            Assert.That(list.Count, Is.EqualTo(10), "Use the native offline presentation's actual slots/listeners");
            fonts.BindBeforeActivation(ui.Root);
            InvokePrivate(ui.Root.GetComponent<UIMainCanvas>(), "Awake");
            InvokePrivate(ui.Navigation, "Start");
            ui.Root.SetActive(true);
            StaffGachaOfflineViewFactory.BeginNavigation(ui.View);
            ui.ClickEntry(); ui.ClickArrow("_leftButton");
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));
            Animator itemAnimator = Reference<Animator>(ui.Item, "_gachaMacineAnimator");
            itemAnimator.fireEvents = false;
            itemAnimator.Play("Base Layer.ZoomIn Item", 0, 1f);
            itemAnimator.Update(0f);
            itemAnimator.fireEvents = true;
            List<GachaItemData> results = items.Data.Take(11).ToList();
            Assert.That(results.Count, Is.EqualTo(11));
            PopupWrite(ui.Item, "_getItemList", results);
            PopupWrite(ui.Item, "_getItemIndex", 11);
            PopupWrite(ui.Item, "_currentStep", 5);
            PopupWrite(ui.Item, "_isShowingSummary", true);
            PopupWrite(ui.Item, "_summaryComplete", true);
            var final = Reference<UIGachaCard>(ui.Item, "_skinGachaCard");
            final.SetData(results[10]); final.SetPosition(new Vector3(600f, 0f));
            final.SetScale(1f); final.gameObject.SetActive(true);
            for (int i = 0; i < 10; i++) { list[i].SetData(results[i]); list[i].gameObject.SetActive(true); }
            Reference<Transform>(ui.Item, "_getItemSlotFrame").gameObject.SetActive(true);
            Reference<Button>(ui.Item, "_skipButton").gameObject.SetActive(false);
            PopupWrite(ui.Item, "_screenTouchWaitTime", 0f);
            InvokePrivate(ui.Item, "PrepareSummaryInspection");
            string finalState = PopupCardState(final);
            string before = PopupUserInfoState();
            for (int index = 0; index < 11; index++)
            {
                Button selected = index < 10 ? list[index].GetComponent<Button>() : final.GetComponent<Button>();
                Assert.That(selected != null && selected.interactable, Is.True, "Native item selection " + index);
                ResultPopupStateClick(selected);
                var popup = RuntimeReference<GachaResultCardPopup>(ui.Item, "_resultPopup");
                AssertPopupCenteredAndDismissible(popup, ui.View.transform);
                Assert.That(Reference<TextMeshProUGUI>(popup.Card, "_nameText").text, Is.EqualTo(results[index].Name));
                Assert.That(Reference<Image>(popup.Card, "_skinImage").sprite, Is.SameAs(results[index].ThumbnailSprite));
                Assert.That(PopupCardState(final), Is.EqualTo(finalState));
                DismissPopup(popup, ui.View.transform);
                Assert.That(ui.Item.EditorSummaryComplete, Is.True);
                Assert.That(ui.Item.EditorResultIndex, Is.EqualTo(11));
                Assert.That(PopupUserInfoState(), Is.EqualTo(before));
            }
            AssertBonusCardCorners(final, ui.View.transform,
                () => RuntimeReference<GachaResultCardPopup>(ui.Item, "_resultPopup"));
            Assert.That(ui.Item.SelectResultCard(-1), Is.False);
            Assert.That(ui.Item.SelectResultCard(11), Is.False);
            Assert.That(ui.Item.SelectResultCard(0), Is.True);
            var retainedPopup = RuntimeReference<GachaResultCardPopup>(ui.Item, "_resultPopup");
            DismissPopup(retainedPopup, ui.View.transform);
            Assert.That(ui.Item.EditorSummaryComplete, Is.True, "First backdrop click closes only inspection");
            ui.Item.Hide(); // State/lifetime test; actual backdrop and Animator input is covered in the PlayerLoop test.
            Assert.That(retainedPopup.IsOpen, Is.False);
            Assert.That(ui.Item.EditorSummaryComplete, Is.False);
            Assert.That(ui.Item.EditorVisibleSlotCount, Is.Zero);
            Assert.That(final.gameObject.activeInHierarchy, Is.False);
            Assert.That(ui.Item.SelectResultCard(0), Is.False);
            ui.Item.Hide();
            ui.Item.Show();
            Assert.That(retainedPopup.IsOpen, Is.False);
            Assert.That(ui.Item.SelectResultCard(0), Is.False, "Idle reentry is not an available summary");
            Assert.That(PopupUserInfoState(), Is.EqualTo(before));
            Assert.That(session.Request, Is.Null);
            Assert.That(session.PurchaseWrites + session.FollowupWrites + session.DrawCount, Is.Zero);
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void ResultPopup_SharedCardRejectsMismatchedReceiptAndDisposesWithoutTouchingTemplate()
    {
        using (var witness = new GlobalWitness(Catalog()))
        using (var session = new StaffGachaOfflineSession(Catalog()))
        using (var ui = new OfflineNavigationView(session.Owner, true))
        {
            ui.ClickEntry();
            Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
            Assert.That(session.ReplySuccess(), Is.True);
            var template = Reference<UIGachaCard>(ui.Staff, "_gachaCard");
            string templateState = PopupCardState(template);
            var request = session.Request;
            template.TweenScale(Vector3.one * 1.3f, .2f, Ease.OutBack);
            TweenData[] runningTemplateTweens = template.GetComponentsInChildren<TweenData>(true)
                .Where(tween => tween.enabled).ToArray();
            Assert.That(runningTemplateTweens.Length, Is.GreaterThan(0));
            var popup = new GachaResultCardPopup(ui.View.transform, template);
            try
            {
                Assert.That(popup.ShowStaff(request.DrawnStaff[0], request.Plan.AccountResult.Acquisition.Items[0]), Is.True);
                Assert.That(popup.Card.GetComponentsInChildren<TweenData>(true).All(tween => !tween.enabled), Is.True,
                    "A live +1 pop tween must not run on the display-only clone with uninitialized runtime state");
                Assert.That(popup.Card.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(runningTemplateTweens.All(tween => tween.enabled), Is.True,
                    "Inspecting a result cannot stop the original bonus pop animation");
                AssertPopupCenteredAndDismissible(popup, ui.View.transform);
                Assert.That(popup.ShowStaff(request.DrawnStaff[0], request.Plan.AccountResult.Acquisition.Items[2]), Is.False);
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(popup.ShowStaff(request.DrawnStaff[1], request.Plan.AccountResult.Acquisition.Items[1]), Is.True);
                NativeResultAssertText(popup.Card, request.DrawnStaff[1], request.Plan.AccountResult.Acquisition.Items[1]);
                Assert.That(PopupCardState(template), Is.EqualTo(templateState));
            }
            finally { popup.Dispose(); template.TweenStop(); }
            Assert.That(popup.IsOpen, Is.False);
            Assert.That(RuntimeReference<GameObject>(popup, "_root") == null, Is.True);
            Assert.That(PopupCardState(template), Is.EqualTo(templateState));
            witness.AssertUnchanged();
        }
    }

    private static void AssertPopupCenteredAndDismissible(GachaResultCardPopup popup, Transform viewport, bool actualInput = false)
    {
        Assert.That(popup, Is.Not.Null);
        Assert.That(popup.IsOpen, Is.True);
        popup.BringToFront();
        Canvas.ForceUpdateCanvases();
        var root = RuntimeReference<GameObject>(popup, "_root").transform as RectTransform;
        var card = popup.Card.transform as RectTransform;
        Vector3 center = viewport.InverseTransformPoint(card.TransformPoint(card.rect.center));
        Assert.That(Vector2.Distance(center, ((RectTransform)viewport).rect.center), Is.LessThan(.1f));
        var corners = new Vector3[4]; card.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
            Assert.That(((RectTransform)viewport).rect.Contains(viewport.InverseTransformPoint(corner)), Is.True);
        Assert.That(root.GetSiblingIndex(), Is.EqualTo(root.parent.childCount - 1));
        Button background = RuntimeReference<Button>(popup, "_background");
        Assert.That(background.gameObject.activeInHierarchy && background.interactable, Is.True);
        Assert.That(background.GetComponent<Image>().color.a, Is.GreaterThan(0f).And.LessThan(1f));
        var body = popup.Card.transform.parent.GetComponent<Button>();
        Assert.That(body != null && body.interactable && body.targetGraphic.raycastTarget, Is.True);
        if (actualInput) ResultPopupRaycastClick(body);
        else ResultPopupStateClick(body);
        Assert.That(popup.IsOpen, Is.True, "Card-body clicks must not fall through and dismiss the popup");
    }

    private static void DismissPopup(GachaResultCardPopup popup, Transform viewport, bool actualInput = false)
    {
        Button background = RuntimeReference<Button>(popup, "_background");
        if (actualInput) ResultPopupRaycastClick(background, (RectTransform)viewport, new Vector2(.025f, .025f));
        else ResultPopupStateClick(background);
        Assert.That(popup.IsOpen, Is.False, "Only the background dismisses this inspection, not its summary");
    }

    private static void AssertBonusCardCorners(UIGachaCard card, Transform viewport,
        Func<GachaResultCardPopup> currentPopup)
    {
        foreach (Vector2 point in new[] { new Vector2(.1f, .9f), new Vector2(.9f, .1f) })
        {
            ResultPopupStateClick(card.GetComponent<Button>());
            Assert.That(currentPopup().IsOpen, Is.True, "The whole +1 card, not only its art center, is clickable");
            DismissPopup(currentPopup(), viewport);
        }
    }

    internal static void ResultPopupStateClick(Button button)
    {
        Assert.That(button != null && button.isActiveAndEnabled && button.IsInteractable(), Is.True);
        button.onClick.Invoke();
    }

    // This is used only after EnterPlayMode by the PlayerLoop regression. The
    // separate EditMode tests intentionally verify state, not physical hit testing.
    // Do not replace this with Button.onClick.Invoke: the regression was a valid
    // listener on a non-Graphic card, so the real top hit was its closing backdrop.
    internal static void ResultPopupRaycastClick(Button expected, RectTransform surface = null,
        Vector2? normalizedPoint = null)
    {
        Assert.That(expected != null && expected.isActiveAndEnabled && expected.IsInteractable(), Is.True);
        surface = surface != null ? surface : (RectTransform)expected.transform;
        EventSystem previous = EventSystem.current;
        var events = surface.transform.root.GetComponentInChildren<EventSystem>();
        Assert.That(events != null && events.isActiveAndEnabled, Is.True, "Use the existing sanitized view's native EventSystem");
        try
        {
            EventSystem.current = events;
            Canvas.ForceUpdateCanvases();
            var canvas = surface.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 normalized = normalizedPoint ?? new Vector2(.5f, .5f);
            Vector2 local = surface.rect.min + Vector2.Scale(surface.rect.size, normalized);
            var pointer = new PointerEventData(events)
            {
                pointerId = -1, button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, surface.TransformPoint(local)),
                eligibleForClick = true, clickCount = 1
            };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            var canvasGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            var candidateDiagnostics = new List<string>();
            for (int i = 0; i < canvasGraphics.Count; i++)
            {
                Graphic graphic = canvasGraphics[i]; // Unity's IndexedSet does not implement enumeration.
                if (!graphic.raycastTarget || !graphic.isActiveAndEnabled) continue;
                candidateDiagnostics.Add(graphic.name + "(depth=" + graphic.depth + ",cull=" +
                    graphic.canvasRenderer.cull + ",contains=" + RectTransformUtility.RectangleContainsScreenPoint(
                        graphic.rectTransform, pointer.position, camera) + ")");
            }
            Assert.That(hits.Count, Is.GreaterThan(0), "Actual GraphicRaycaster must hit the rendered result surface. " +
                "canvas=" + canvas.name + ", active=" + canvas.isActiveAndEnabled +
                ", rootActive=" + canvas.transform.root.gameObject.activeInHierarchy +
                ", preview=" + UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(canvas.gameObject.scene) +
                ", screen=" + Screen.width + "x" + Screen.height + ", point=" + pointer.position +
                ", pixelRect=" + canvas.pixelRect + ", renderMode=" + canvas.renderMode +
                ", surfaceRect=" + surface.rect +
                ", graphics=" + canvasGraphics.Count +
                ", candidates=" + string.Join(";", candidateDiagnostics) +
                ", raycasters=" + string.Join(";", RaycasterManager.GetRaycasters().Select(raycaster =>
                    raycaster.name + "/" + raycaster.GetType().Name + "/active=" + raycaster.isActiveAndEnabled)));
            RaycastResult top = hits[0];
            Assert.That(top.module, Is.InstanceOf<GraphicRaycaster>());
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);
            Assert.That(handler, Is.SameAs(expected.gameObject),
                "Top graphic '" + top.gameObject.name + "' must route to '" + expected.name +
                "', not the result backdrop. Hits: " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            pointer.pointerCurrentRaycast = top;
            pointer.pointerPressRaycast = top;
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(top.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            pointer.rawPointerPress = top.gameObject;
            if (pointer.pointerPress != null)
                ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }
        finally
        {
            EventSystem.current = previous;
        }
    }

    private static string PopupCardState(UIGachaCard card) => JsonConvert.SerializeObject(new
    {
        Name = Reference<TextMeshProUGUI>(card, "_nameText").text,
        Description = Reference<TextMeshProUGUI>(card, "_descriptionText").text,
        Effect = Reference<TextMeshProUGUI>(card, "_effectText").text,
        Sprite = Reference<Image>(card, "_skinImage").sprite?.GetInstanceID()
    });

    private static string PopupUserInfoState() => JsonConvert.SerializeObject(new
    {
        UserInfo.Money, UserInfo.Dia, UserInfo.TotalUseGachaMachineCount,
        PaymentInfo.GachaPaymentDatas, Items = UserInfo.GetGiveGachaItemDic(),
        Counts = UserInfo.GetGiveGachaItemCountDic(), Levels = UserInfo.GetGiveGachaItemLevelDic()
    });

    private static void PopupWrite(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    // EditMode's runner already owns an unsaved empty regular scene. Do not save,
    // replace or close it. Only explicitly supplied roots created by this fixture
    // may be parked while the display factory verifies its empty destination.
    internal sealed class ResultRaycastSceneScope : IDisposable
    {
        public readonly UnityEngine.SceneManagement.Scene Scene;
        private readonly UnityEngine.SceneManagement.Scene _parking;
        private readonly GameObject[] _parked;
        public ResultRaycastSceneScope(params GameObject[] ownedRoots)
        {
            Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Assert.That(Scene.IsValid() && Scene.isLoaded && string.IsNullOrEmpty(Scene.path) &&
                !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(Scene), Is.True,
                "Raycast tests require the runner's unsaved regular scene, never a user scene asset");
            GameObject[] roots = Scene.GetRootGameObjects();
            Assert.That(roots.All(root => ownedRoots.Contains(root) || IsBatchRunnerDefault(root)), Is.True,
                "Refuse to reuse a scene containing anything other than this fixture's explicitly owned roots: " +
                string.Join("; ", roots.Where(root => !ownedRoots.Contains(root)).Select(root =>
                    root.name + " [" + string.Join(",", root.GetComponents<Component>().Select(component => component == null ? "Missing" : component.GetType().FullName)) + "]")));
            _parked = roots;
            if (roots.Length != 0)
            {
                _parking = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                foreach (GameObject root in roots)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, _parking);
            }
            Assert.That(Scene.rootCount, Is.Zero);
        }

        private static bool IsBatchRunnerDefault(GameObject root)
        {
            // Unity's batch TestRunner creates an untitled DefaultGameObjects
            // scene. Temporarily park and restore its camera/light too; never
            // delete them, or accept roots containing gameplay/custom scripts.
            if (!Application.isBatchMode || root.transform.childCount != 0) return false;
            Component[] components = root.GetComponents<Component>();
            if (root.name == "Main Camera")
                return components.Length == 3 && components.All(component =>
                    component is Transform || component is Camera || component is AudioListener);
            return root.name == "Directional Light" && components.Length == 2 &&
                components.All(component => component is Transform || component is Light);
        }

        public void Dispose()
        {
            if (Scene.IsValid() && Scene.isLoaded)
            {
                foreach (GameObject root in _parked)
                    if (root != null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, Scene);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(Scene);
            }
            if (_parking.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(_parking);
        }
    }

    private sealed class PopupItemResources : IDisposable
    {
        private readonly Dictionary<FieldInfo, object> _saved = new Dictionary<FieldInfo, object>();
        private readonly GameObject _host;
        public readonly List<GachaItemData> Data;
        public PopupItemResources()
        {
            // Reuse the product resource parser on isolated registry collections, not another CSV schema.
            foreach (string name in new[] { "_gachaItemDataList", "_gachaItemDataDic", "_upgradeTypeGachaItemDataDic" })
            {
                FieldInfo field = typeof(ItemManager).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                _saved.Add(field, field.GetValue(null));
                field.SetValue(null, Activator.CreateInstance(field.FieldType));
            }
            _host = new GameObject("Detached item presentation resources");
            _host.SetActive(false);
            var manager = _host.AddComponent<ItemManager>();
            InvokePrivate(manager, "InitCsv");
            Data = manager.GetGachaItemDataList().OrderBy(item => item.Id).ToList();
        }
        public void Dispose()
        {
            foreach (var pair in _saved) pair.Key.SetValue(null, pair.Value);
            if (_host != null) Object.DestroyImmediate(_host);
        }
    }
}
#endif
