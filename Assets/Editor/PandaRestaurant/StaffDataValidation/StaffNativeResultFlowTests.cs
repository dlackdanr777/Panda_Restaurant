#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uses the existing isolated runtime owner and original Stage1 navigation fixture.
// These are EditMode samples of native Animator/tween transitions, not Player Loop or listening tests.
public sealed partial class StaffGachaOfflineSessionTests
{
    [TestCase(StaffGachaOfflineCase.SingleNew, -1, TestName = "NativeResult_Single_NativeMachineToCardClosesAndRestores")]
    [TestCase(StaffGachaOfflineCase.Eleven, -1, TestName = "NativeResult_Eleven_NormalCapsulesAndStoredTenCardGrid")]
    [TestCase(StaffGachaOfflineCase.Eleven, -2, TestName = "NativeResult_Eleven_SkipKeepsSameFixedBatch")]
    [TestCase(StaffGachaOfflineCase.Eleven, 9, TestName = "NativeResult_Eleven_SkipAtTenthCardRevealsFinalCapsule")]
    public void NativeResult_OriginalControlsPreserveConfirmedPurchase(StaffGachaOfflineCase scenario, int skipAt)
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        using (var ui = new OfflineNavigationView(session.Owner, true))
        {
            ui.ClickEntry();
            Animator machineAnimator = Reference<Animator>(ui.Staff, "_gachaMacineAnimator");
            machineAnimator.Update(0f);
            Assert.That(machineAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Idle")));
            // Exercise restoration from both the factory's false state (single) and the
            // source's enabled AnimationEvents state, suppressed before any animated step.
            bool originalFireEvents = scenario != StaffGachaOfflineCase.SingleNew;
            machineAnimator.fireEvents = originalFireEvents;
            bool originalComponents = Reference<GameObject>(ui.View, "_uiComponents").activeSelf;
            Transform slots = Reference<Transform>(ui.Staff, "_getStaffSlotFrame");
            Transform slotsParent = slots.parent;
            int slotsSibling = slots.GetSiblingIndex();
            RectTransform capsules = Reference<RectTransform>(ui.Staff, "_capsules");
            int capsulesSibling = capsules.GetSiblingIndex();
            Vector3 capsulesPosition = capsules.localPosition;
            AudioSource audio = Reference<AudioSource>(ui.Staff, "_gachaSound");
            AudioClip audioClip = audio.clip;
            var centralImageState = new NativeResultImageSnapshot(Reference<Image>(ui.Staff, "_getStaffImage"));
            var finalImageState = new NativeResultImageSnapshot(Reference<Image>(Reference<GachaCapsule>(ui.Staff, "_capsule"), "_image"));

            Assert.That(session.TryStart(scenario, out string error), Is.True, error);
            Assert.That(ui.Display.EditorIsResultVisible, Is.False, "Unconfirmed output must not show a result");
            session.Owner.EditorStaffPurchaseAdmission = _ => false;
            Assert.That(session.ReplySuccess(), Is.True);
            var request = session.Request;
            var acquisition = request.Plan.AccountResult.Acquisition;
            var account = session.Account;
            string frozenPlan = JsonConvert.SerializeObject(request.Plan);
            string frozenAccount = JsonConvert.SerializeObject(account);
            string frozenFollowup = session.FollowupPayload;
            int followupWrites = session.FollowupWrites;
            int count = scenario == StaffGachaOfflineCase.SingleNew ? 1 : 11;
            Assert.That(request.CompletionCount, Is.EqualTo(1));
            Assert.That(followupWrites, Is.EqualTo(1));
            Assert.That(session.HasUnresolvedRequest, Is.False);

            ui.Display.Tick();
            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Machine"), ui.Display.EditorAnimationError);
            Assert.That(machineAnimator.fireEvents, Is.False, "Product grant AnimationEvents remain isolated");
            Assert.That(ui.View.IsStartGacha, Is.True);
            Assert.That(Reference<GameObject>(ui.View, "_uiComponents").activeSelf, Is.False);
            Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));

            if (skipAt == -2)
            {
                NativeResultClick(ui.Staff, "_skipButton", "건너뛰기");
                NativeResultFinishCascade(ui);
                NativeResultFinishCapsule(ui, 10);
            }
            else
            {
                NativeResultFinishCentralReveal(ui, 0, count == 1);
                NativeResultAssertCard(ui, request, 0);
                if (count == 11)
                {
                    for (int index = 0; index < 10; index++)
                    {
                        Assert.That(ui.Display.EditorVisibleSlotCount, Is.Zero,
                            "The first ten reveals use the centred native machine, not a background grid");
                        if (index == skipAt)
                        {
                            NativeResultClick(ui.Staff, "_skipButton", "건너뛰기");
                        }
                        else
                        {
                            NativeResultAdvanceAcknowledgedCard(ui, index);
                        }
                        if (index < 9)
                        {
                            NativeResultFinishCentralReveal(ui, index + 1, false);
                            NativeResultAssertCard(ui, request, index + 1);
                            continue;
                        }
                        NativeResultFinishCascade(ui);
                        NativeResultFinishCapsule(ui, 10);
                        NativeResultAssertCard(ui, request, 10);
                    }
                }
            }

            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Summary"));
            Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(count - 1));
            Assert.That(ui.Display.EditorResultCount, Is.EqualTo(count));
            Assert.That(ui.Display.EditorVisibleSlotCount, Is.EqualTo(count == 11 ? 10 : 0));
            NativeResultAssertCard(ui, request, count - 1);
            if (count == 11)
            {
                List<UIGachaCardSlot> cards = RuntimeReference<List<UIGachaCardSlot>>(ui.Staff, "_getStaffSlotList");
                for (int index = 0; index < 10; index++)
                {
                    Assert.That(cards[index].gameObject.activeInHierarchy, Is.True);
                    NativeResultAssertText(cards[index], request.DrawnStaff[index], acquisition.Items[index]);
                    Button button = cards[index].GetComponent<Button>();
                    Assert.That(button != null && button.interactable, Is.True, "Native retained slot input " + index);
                    ResultPopupStateClick(button);
                    var popup = RuntimeReference<GachaResultCardPopup>(ui.Display, "_popup");
                    Assert.That(popup.IsOpen, Is.True);
                    Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index));
                    Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(acquisition.Items[index]));
                    NativeResultAssertText(popup.Card, request.DrawnStaff[index], acquisition.Items[index]);
                    NativeResultAssertText(Reference<UIGachaCard>(ui.Staff, "_gachaCard"),
                        request.DrawnStaff[10], acquisition.Items[10]);
                    DismissPopup(popup, ui.View.transform);
                    Assert.That(popup.IsOpen, Is.False);
                }
            }

            ui.ClickResultClose();
            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Closed"));
            Assert.That(ui.View.IsStartGacha, Is.False);
            Assert.That(Reference<GameObject>(ui.View, "_uiComponents").activeSelf, Is.EqualTo(originalComponents));
            Assert.That(machineAnimator.fireEvents, Is.EqualTo(originalFireEvents));
            Assert.That(machineAnimator.enabled, Is.True);
            Assert.That(audio.isPlaying, Is.False, "EditMode does not play/listen to sound; closing leaves the source stopped");
            Assert.That(audio.clip, Is.SameAs(audioClip));
            Assert.That(slots.parent, Is.SameAs(slotsParent));
            Assert.That(slots.GetSiblingIndex(), Is.EqualTo(slotsSibling));
            Assert.That(capsules.GetSiblingIndex(), Is.EqualTo(capsulesSibling));
            Assert.That(capsules.localPosition, Is.EqualTo(capsulesPosition));
            Assert.That(Reference<GachaCapsule>(ui.Staff, "_capsule").gameObject.activeSelf, Is.False);
            centralImageState.AssertRestored();
            finalImageState.AssertRestored();

            ui.Display.Tick();
            NativeResultAssertClosedControlLayout(ui);
            Button retiredResultButton = Reference<Button>(ui.Staff, "_skipButton");
            retiredResultButton.onClick.Invoke();
            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Closed"));
            Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False);
            Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));
            ui.ClickArrow("_leftButton");
            ui.Display.Tick();
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));
            Assert.That(Reference<Button>(ui.Staff, "_skipButton").gameObject.activeInHierarchy, Is.False,
                "A staff result control must not leak onto the item machine");
            ui.ClickArrow("_rightButton");
            ui.Display.Tick();
            Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False,
                "Native machine reentry must not reopen an acknowledged result");
            NativeResultAssertClosedControlLayout(ui);

            Assert.That(session.Request, Is.SameAs(request));
            Assert.That(session.Owner.LastCompletedStaffPurchaseExecution, Is.SameAs(request));
            Assert.That(request.Plan.AccountResult.Acquisition, Is.SameAs(acquisition));
            Assert.That(session.Account, Is.SameAs(account));
            Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(frozenPlan));
            Assert.That(JsonConvert.SerializeObject(session.Account), Is.EqualTo(frozenAccount));
            Assert.That(request.CompletionCount, Is.EqualTo(1));
            Assert.That(session.Diamonds, Is.EqualTo(count == 1 ? 100 : 10));
            Assert.That(session.Account.PandaTokens, Is.EqualTo(count == 1 ? 55 : 110));
            Assert.That(session.DrawCount, Is.EqualTo(count));
            Assert.That(session.PurchaseWrites, Is.EqualTo(1));
            Assert.That(session.FollowupWrites, Is.EqualTo(followupWrites));
            Assert.That(session.FollowupPayload, Is.EqualTo(frozenFollowup));
            Assert.That(session.RecordNotifications, Is.EqualTo(1));
            Assert.That(session.WalletNotifications, Is.EqualTo(1));
            witness.AssertUnchanged();
        }
    }

    private static void NativeResultAssertClosedControlLayout(OfflineNavigationView ui)
    {
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Closed"));
        Button replay = Reference<Button>(ui.Staff, "_skipButton");
        GameObject components = Reference<GameObject>(ui.View, "_uiComponents");
        Button exit = components.transform.Find("Exit Button").GetComponent<Button>();
        Assert.That(components.activeInHierarchy, Is.True, "The normal machine controls must be restored");
        Assert.That(replay.gameObject.activeSelf, Is.False);
        Assert.That(replay.gameObject.activeInHierarchy, Is.False);
        Assert.That(replay.interactable, Is.False);
        Assert.That(exit.gameObject.activeInHierarchy && exit.interactable, Is.True,
            "The normal machine exit remains available without a retained-result control");
    }

    private static void NativeResultFinishCapsule(OfflineNavigationView ui, int expectedIndex)
    {
        Assert.That(expectedIndex, Is.EqualTo(10), "The separate right capsule is exclusively the final +1");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("CapsuleMoving"));
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(expectedIndex));
        GachaCapsule capsule = Reference<GachaCapsule>(ui.Staff, "_capsule");
        Animator animator = Reference<Animator>(capsule, "_animator");
        GameObject smoke = capsule.transform.Find("Smoke Effect Image").gameObject;
        Assert.That(capsule.IsMoving, Is.True);
        Assert.That(capsule.gameObject.activeInHierarchy, Is.True);
        NativeResultAssertCapsuleContents(Reference<Image>(capsule, "_image"),
            Reference<Image>(capsule, "_upperCapsuleImage"), Reference<Image>(capsule, "_lowerCapsuleImage"));
        Assert.That(Reference<RectTransform>(capsule, "_rectTransform").anchoredPosition.x, Is.EqualTo(600f).Within(0.01f));
        Assert.That(ui.Display.EditorIsResultVisible, Is.False);
        // Reuse the fixture's actual TweenData callbacks; only elapsed test time is advanced.
        typeof(OfflineNavigationView).GetMethod("CompleteNavigationTweens", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(ui, null);
        Assert.That(capsule.IsMoving, Is.False);
        Assert.That(Reference<RectTransform>(capsule, "_rectTransform").anchoredPosition, Is.EqualTo(new Vector2(600f, 0f)));
        ui.Display.Tick();
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("CapsuleOpening"));
        Assert.That(smoke.activeSelf, Is.False);
        animator.Update(0.2f);
        Assert.That(smoke.activeSelf, Is.True, "The original capsule clip must emit its smoke");
        Assert.That(capsule.IsOpenComplete, Is.False);
        ui.Display.Tick();
        Assert.That(ui.Display.EditorIsResultVisible, Is.False, "Do not display the card before its capsule opens");
        animator.Update(0.51f);
        Assert.That(capsule.IsOpenComplete, Is.True);
        Assert.That(smoke.activeSelf, Is.False);
        ui.Display.Tick();
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Summary"));
        Assert.That(capsule.gameObject.activeSelf, Is.False);
        Assert.That(ui.Display.EditorIsResultVisible, Is.True);
        Assert.That(ui.Display.EditorAnimationError, Is.Null);
    }

    private static void NativeResultFinishCentralReveal(OfflineNavigationView ui, int expectedIndex, bool single)
    {
        Animator animator = Reference<Animator>(ui.Staff, "_gachaMacineAnimator");
        int waitState = Animator.StringToHash("Base Layer.Wait_Gacha");
        int openState = Animator.StringToHash("Base Layer.Open_Gacha");
        int resultState = Animator.StringToHash("Base Layer.ZoomIn Item");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Machine"));
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(expectedIndex));
        // Use the source controller's real Start/Step2Skip -> Wait transition. No phase,
        // sequence position or Animator state is written by this helper.
        for (int frame = 0; frame < 1200; frame++)
        {
            animator.Update(0.05f);
            ui.Display.Tick();
            if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == waitState && !animator.IsInTransition(0)) break;
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(waitState));
        Assert.That(animator.IsInTransition(0), Is.False);
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Machine"));
        Assert.That(ui.Display.EditorIsResultVisible, Is.False);
        Assert.That(ui.Display.EditorVisibleSlotCount, Is.Zero);
        Assert.That(Reference<GachaCapsule>(ui.Staff, "_capsule").gameObject.activeSelf, Is.False);
        NativeResultAssertCapsuleContents(Reference<Image>(ui.Staff, "_getStaffImage"),
            Reference<Image>(ui.Staff, "_upperCapsule"), Reference<Image>(ui.Staff, "_lowerCapsule"));
        animator.Update(0.5f);
        ui.Display.Tick();
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(waitState),
            "Production input mode must stay at the native wait capsule until the user touches it");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Machine"));

        NativeResultClock(ui.Display, "_nextInput");
        NativeResultClick(ui.Staff, "_screenButton");
        NativeResultClick(ui.Staff, "_screenButton"); // Same-frame repeat must not skip Open or the next result.
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(expectedIndex));
        Assert.That(ui.Display.TryShowCompleted(true, out _), Is.False, "A click cannot restart an active machine");
        bool sawOpen = false;
        for (int frame = 0; frame < 1200 && ui.Display.EditorPresentationPhase == "Machine"; frame++)
        {
            animator.Update(0.05f);
            sawOpen |= animator.GetCurrentAnimatorStateInfo(0).fullPathHash == openState;
            ui.Display.Tick();
        }
        Assert.That(sawOpen, Is.True, "Every central result must pass through the authored Open_Gacha clip");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(resultState));
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo(single ? "Summary" : "Card"),
            ui.Display.EditorAnimationError);
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(expectedIndex));
        Assert.That(ui.Display.EditorVisibleSlotCount, Is.Zero);
        Assert.That(Reference<RectTransform>(Reference<UIGachaCard>(ui.Staff, "_gachaCard"), "_rectTransform").anchoredPosition,
            Is.EqualTo(Vector2.zero));
        Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));
        Assert.That(animator.fireEvents, Is.False, "Native visual transitions never re-enable grant events");
    }

    private static void NativeResultAdvanceAcknowledgedCard(OfflineNavigationView ui, int index)
    {
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Card"));
        NativeResultClock(ui.Display, "_nextInput");
        NativeResultClick(ui.Staff, "_screenButton");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Card"), "First touch acknowledges card text only");
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index));
        NativeResultClick(ui.Staff, "_screenButton");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Card"), "Rapid duplicate touch must respect the text delay");
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index));
        NativeResultClock(ui.Display, "_nextInput");
        NativeResultClick(ui.Staff, "_screenButton");
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo(index == 9 ? "Accumulating" : "Machine"));
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index == 9 ? 9 : index + 1));
        // A queued old screen event cannot advance the new native reveal or start another cascade.
        Reference<Button>(ui.Staff, "_screenButton").onClick.Invoke();
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo(index == 9 ? "Accumulating" : "Machine"));
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index == 9 ? 9 : index + 1));
    }

    private static void NativeResultFinishCascade(OfflineNavigationView ui)
    {
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Accumulating"));
        Assert.That(ui.Display.EditorVisibleSlotCount, Is.Zero);
        Reference<Button>(ui.Staff, "_skipButton").onClick.Invoke(); // A queued duplicate Skip is a no-op.
        Assert.That(ui.Display.EditorVisibleSlotCount, Is.Zero);
        for (int index = 0; index < 10; index++)
        {
            NativeResultClock(ui.Display, "_nextSlot");
            ui.Display.Tick();
            Assert.That(ui.Display.EditorVisibleSlotCount, Is.EqualTo(index + 1));
            Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Accumulating"));
            Assert.That(Reference<GachaCapsule>(ui.Staff, "_capsule").gameObject.activeSelf, Is.False);
        }
        ui.Display.Tick();
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("Accumulating"),
            "Ten slots must finish their final pause before the +1 capsule starts");
        NativeResultClock(ui.Display, "_nextSlot");
        ui.Display.Tick();
        Assert.That(ui.Display.EditorPresentationPhase, Is.EqualTo("CapsuleMoving"));
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(10));
    }

    private static void NativeResultAssertCard(OfflineNavigationView ui,
        Muks.BackEnd.StaffGachaPurchaseExecution request, int index)
    {
        Assert.That(ui.Display.EditorIsResultVisible, Is.True);
        Assert.That(ui.Display.EditorResultIndex, Is.EqualTo(index));
        Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(request.Plan.AccountResult.Acquisition.Items[index]));
        float expectedX = ui.Display.EditorPresentationPhase == "Summary" && ui.Display.EditorResultCount == 11 ? 600f : 0f;
        Assert.That(Reference<RectTransform>(Reference<UIGachaCard>(ui.Staff, "_gachaCard"), "_rectTransform").anchoredPosition,
            Is.EqualTo(new Vector2(expectedX, 0f)), "Individual cards are central; only the final summary uses the right side");
        NativeResultAssertText(Reference<UIGachaCard>(ui.Staff, "_gachaCard"),
            request.DrawnStaff[index], request.Plan.AccountResult.Acquisition.Items[index]);
    }

    private static void NativeResultAssertText(UnityEngine.Object card, GachaStaffData staff,
        StaffGachaAcquisitionItem item)
    {
        Assert.That(Reference<TextMeshProUGUI>(card, "_nameText").text,
            Is.EqualTo(string.IsNullOrWhiteSpace(staff.Name) ? staff.Id : staff.Name));
        Assert.That(Reference<TextMeshProUGUI>(card, "_descriptionText").text,
            Is.EqualTo(item.IsNew ? "신규 획득" : "중복 획득\n판다토큰 +" + item.PandaTokenReward));
        Assert.That(Reference<Image>(card, "_skinImage").sprite,
            Is.SameAs(staff.ThumbnailSprite == null ? staff.Sprite : staff.ThumbnailSprite));
    }

    private static void NativeResultAssertCapsuleContents(Image content, Image upper, Image lower)
    {
        // Inspect the real UI mesh, independently of the product fitting helper. This
        // includes Image preserve-aspect/padding rather than just the RectTransform box.
        Transform frame = content.transform.parent;
        Rect top = NativeResultMeshBounds(upper, frame), bottom = NativeResultMeshBounds(lower, frame);
        Rect capsule = Rect.MinMaxRect(Mathf.Min(top.xMin, bottom.xMin), Mathf.Min(top.yMin, bottom.yMin),
            Mathf.Max(top.xMax, bottom.xMax), Mathf.Max(top.yMax, bottom.yMax));
        Rect visible = NativeResultMeshBounds(content, frame);
        Assert.That(Vector2.Distance(visible.center, capsule.center), Is.LessThan(0.1f),
            "The staff must be centred inside the closed capsule, not lifted by its gameplay foot pivot");
        Assert.That(visible.xMin, Is.GreaterThan(capsule.xMin));
        Assert.That(visible.xMax, Is.LessThan(capsule.xMax));
        Assert.That(visible.yMin, Is.GreaterThan(capsule.yMin));
        Assert.That(visible.yMax, Is.LessThan(capsule.yMax));
        Assert.That(Mathf.Max(visible.width / capsule.width, visible.height / capsule.height),
            Is.EqualTo(0.88f).Within(0.01f), "The staff is readable but smaller than its enclosing capsule");
    }

    private static Rect NativeResultMeshBounds(Image image, Transform relativeTo)
    {
        Canvas.ForceUpdateCanvases();
        using (var mesh = new VertexHelper())
        {
            typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null).Invoke(image, new object[] { mesh });
            Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
            UIVertex vertex = new UIVertex();
            mesh.PopulateUIVertex(ref vertex, 0);
            Vector2 min = relativeTo.InverseTransformPoint(image.transform.TransformPoint(vertex.position));
            Vector2 max = min;
            for (int index = 1; index < mesh.currentVertCount; index++)
            {
                mesh.PopulateUIVertex(ref vertex, index);
                Vector2 point = relativeTo.InverseTransformPoint(image.transform.TransformPoint(vertex.position));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }

    private sealed class NativeResultImageSnapshot
    {
        private readonly Image _image;
        private readonly Sprite _sprite;
        private readonly Vector2 _anchorMin, _anchorMax, _pivot, _size, _position;
        private readonly Vector3 _scale;
        private readonly bool _aspect;
        public NativeResultImageSnapshot(Image image)
        {
            _image = image; _sprite = image.sprite; _aspect = image.preserveAspect;
            RectTransform rect = image.rectTransform;
            _anchorMin = rect.anchorMin; _anchorMax = rect.anchorMax; _pivot = rect.pivot;
            _size = rect.sizeDelta; _position = rect.anchoredPosition; _scale = rect.localScale;
        }
        public void AssertRestored()
        {
            RectTransform rect = _image.rectTransform;
            Assert.That(_image.sprite, Is.SameAs(_sprite));
            Assert.That(_image.preserveAspect, Is.EqualTo(_aspect));
            Assert.That(rect.anchorMin, Is.EqualTo(_anchorMin));
            Assert.That(rect.anchorMax, Is.EqualTo(_anchorMax));
            Assert.That(rect.pivot, Is.EqualTo(_pivot));
            Assert.That(rect.sizeDelta, Is.EqualTo(_size));
            Assert.That(rect.anchoredPosition, Is.EqualTo(_position));
            Assert.That(rect.localScale, Is.EqualTo(_scale));
        }
    }

    private static void NativeResultClick(UIStaffGacha staff, string field, string expectedLabel = null)
    {
        Button button = Reference<Button>(staff, field);
        Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, "Native input unavailable: " + field);
        if (expectedLabel != null)
            Assert.That(button.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo(expectedLabel));
        button.onClick.Invoke();
    }

    private static void NativeResultClock(StaffGachaPurchaseDisplay display, string field)
    {
        Assert.That(field == "_nextInput" || field == "_nextSlot", Is.True);
        typeof(StaffGachaPurchaseDisplay).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(display, 0f);
    }
}
#endif
