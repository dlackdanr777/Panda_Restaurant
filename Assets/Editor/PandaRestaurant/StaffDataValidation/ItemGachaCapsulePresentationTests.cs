#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Muks.Tween;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Samples the original product capsule/controller in an unsaved EditMode preview.</summary>
public sealed class ItemGachaCapsulePresentationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene _preview;
    private UIItemGacha _item;
    private GachaCapsule _capsule;
    private Animator _animator;
    private Capsule _color;
    private Sprite _sprite;
    private string _accountBefore;

    [SetUp]
    public void SetUp()
    {
        _accountBefore = AccountState();
        _preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        _item = _preview.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<UIItemGacha>(true)).Single();
        _capsule = Read<GachaCapsule>(_item, "_capsule");
        _animator = Read<Animator>(_capsule, "_animator");
        _color = Read<Capsule[]>(_item, "_capsuleColors")[0];
        _sprite = _color.UpperCapsule;
        // Only the existing capsule is activated. The game's managers, purchases,
        // tutorials and machine AnimationEvents remain inactive in this preview.
        _capsule.gameObject.SetActive(false);
        _capsule.transform.SetParent(null, false);
        _animator.fireEvents = false;
        _animator.enabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        try { Assert.That(AccountState(), Is.EqualTo(_accountBefore), "Presentation changed rewards or currency"); }
        finally { if (_preview.IsValid()) EditorSceneManager.ClosePreviewScene(_preview); }
    }

    [Test]
    public void OriginalItemCapsule_DisabledAnimatorReplaysItsSmokeAndCompletesAtClipEnd()
    {
        var position = new Vector2(600, 0);
        _capsule.PreparePresentation(_sprite, _color, position);
        Image upper = Read<Image>(_capsule, "_upperCapsuleImage");
        Image image = Read<Image>(_capsule, "_image");
        GameObject smoke = _capsule.transform.Find("Smoke Effect Image").gameObject;
        Assert.That(_animator.enabled, Is.True);
        Assert.That(Read<RectTransform>(_capsule, "_rectTransform").anchoredPosition, Is.EqualTo(position));
        Assert.That(image.sprite, Is.SameAs(_sprite));
        Assert.That(upper.color.a, Is.EqualTo(1f).Within(.001f));
        Assert.That(smoke.activeSelf, Is.False);
        Assert.That(_capsule.IsOpenComplete, Is.False);

        _capsule.StartOpen();
        _animator.Update(.05f);
        Assert.That(smoke.activeSelf, Is.False);
        Assert.That(_capsule.IsOpenComplete, Is.False);
        _animator.Update(.15f);
        Assert.That(smoke.activeSelf, Is.True, "The product clip emits smoke after opening starts");
        _animator.Update(.51f);
        Assert.That(_capsule.IsOpenComplete, Is.True);
        Assert.That(smoke.activeSelf, Is.False);
        Assert.That(upper.color.a, Is.EqualTo(0f).Within(.001f));
        Assert.That(image.sprite, Is.SameAs(_sprite));
    }

    [Test]
    public void Capsule_CancelStopsMotionAndReprepareClearsPreviousOpenAndSmoke()
    {
        _capsule.PreparePresentation(_sprite, _color, new Vector2(600, -2000));
        _capsule.TweenAnchoredPosition(new Vector2(600, 0), 1f, Ease.Smoothstep);
        Assert.That(_capsule.IsMoving, Is.True);
        Assert.Throws<InvalidOperationException>(() => _capsule.StartOpen());
        _capsule.CancelPresentation();
        Assert.That(_capsule.IsMoving, Is.False);
        Assert.That(_capsule.gameObject.activeSelf, Is.False);
        Assert.That(_capsule.GetComponents<TweenData>().All(tween => !tween.enabled), Is.True);

        _capsule.PreparePresentation(_sprite, _color, Vector2.zero);
        _capsule.StartOpen();
        _animator.Update(.2f);
        Assert.That(_capsule.transform.Find("Smoke Effect Image").gameObject.activeSelf, Is.True);
        _capsule.PreparePresentation(_color.LowerCapsule, _color, new Vector2(600, -2000));
        Assert.That(_capsule.IsOpenComplete, Is.False);
        Assert.That(_capsule.IsMoving, Is.False);
        Assert.That(_capsule.transform.Find("Smoke Effect Image").gameObject.activeSelf, Is.False);
        Assert.That(Read<Image>(_capsule, "_image").sprite, Is.SameAs(_color.LowerCapsule));
        Assert.That(Read<Image>(_capsule, "_upperCapsuleImage").color.a, Is.EqualTo(1f).Within(.001f));
        _capsule.StartOpen();
        _animator.Update(.7f);
        Assert.That(_capsule.IsOpenComplete, Is.True);
    }

    [Test]
    public void ItemSummary_AnimationEventsAndRepeatedTouchesCannotRestartOrAdvanceFinalIndex()
    {
        Write(_item, "_isShowingSummary", true);
        Write(_item, "_summaryComplete", false);
        Write(_item, "_getItemIndex", 11);
        Write(_item, "_currentStep", 2);
        _item.SetStep(5);
        _item.SetStep(4);
        _item.SetStep(5);
        _item.OnScreenButtonClicked();
        Assert.That(Read<int>(_item, "_currentStep"), Is.EqualTo(5));
        Assert.That(Read<int>(_item, "_getItemIndex"), Is.EqualTo(11));
        Assert.That(Read<bool>(_item, "_isShowingSummary"), Is.True);
        Assert.That(Read<bool>(_item, "_summaryComplete"), Is.False);
    }

    [Test]
    public void ItemSkipLayout_UsesSharedBottomRightMarginsAfterMachineParentMoves()
    {
        UIGacha view = _item.GetComponentInParent<UIGacha>(true);
        typeof(GachaMachineParent).GetField("_uiGacha", Fields).SetValue(_item, view);
        var viewport = (RectTransform)view.transform;
        // Stage1 authors this hidden Gacha UI at scale zero. Use the shown
        // geometry on this inactive preview only; do not enter product Show/SDK.
        viewport.localScale = Vector3.one;
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 3840f);
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2160f);
        Button skip = Read<Button>(_item, "_skipButton");
        var rect = (RectTransform)skip.transform;
        skip.gameObject.SetActive(true);
        var corners = new Vector3[4];

        AssertItemSkipLayout(viewport, skip, corners);
        Vector3 position = rect.position;
        AssertItemSkipLayout(viewport, skip, corners);
        Assert.That(Vector3.Distance(rect.position, position), Is.LessThan(.01f), "Repeated layout must not drift");

        var parent = (RectTransform)rect.parent;
        parent.anchoredPosition += new Vector2(450f, -130f);
        parent.localScale = new Vector3(.8f, 1.1f, 1f);
        AssertItemSkipLayout(viewport, skip, corners);
    }

    [Test]
    public void ItemSkipLayout_DoesNotEnableHiddenControlsChangeInputOrAdvanceResult()
    {
        UIGacha view = _item.GetComponentInParent<UIGacha>(true);
        typeof(GachaMachineParent).GetField("_uiGacha", Fields).SetValue(_item, view);
        Button skip = Read<Button>(_item, "_skipButton");
        var rect = (RectTransform)skip.transform;
        var label = skip.GetComponentInChildren<TMPro.TMP_Text>(true);
        string originalLabel = label == null ? null : label.text;
        int originalIndex = Read<int>(_item, "_getItemIndex");
        int originalStep = Read<int>(_item, "_currentStep");
        UnityEngine.Random.State random = UnityEngine.Random.state;
        Button.ButtonClickedEvent originalClick = skip.onClick;
        int observedClicks = 0;
        // This unsaved preview-only listener cannot execute a product skip/purchase.
        skip.onClick = new Button.ButtonClickedEvent();
        skip.onClick.AddListener(() => observedClicks++);
        try
        {
            skip.gameObject.SetActive(false);
            skip.interactable = false;
            Vector3 hiddenPosition = rect.position;
            InvokeItemLateUpdate();
            Assert.That(skip.gameObject.activeSelf, Is.False);
            Assert.That(rect.position, Is.EqualTo(hiddenPosition));
            skip.gameObject.SetActive(true);
            InvokeItemLateUpdate();
            Assert.That(skip.interactable, Is.False, "Layout must not override execution/input policy");
            skip.onClick.Invoke();
            Assert.That(observedClicks, Is.EqualTo(1), "Layout preserves the existing button event");
            Assert.That(label == null ? null : label.text, Is.EqualTo(originalLabel));
            Assert.That(Read<int>(_item, "_getItemIndex"), Is.EqualTo(originalIndex));
            Assert.That(Read<int>(_item, "_currentStep"), Is.EqualTo(originalStep));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }
        finally { skip.onClick = originalClick; }
    }

    private void AssertItemSkipLayout(RectTransform viewport, Button skip, Vector3[] corners)
    {
        InvokeItemLateUpdate();
        var rect = (RectTransform)skip.transform;
        Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(rect.pivot, Is.EqualTo(Vector2.one * .5f));
        Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(320f, 100f)));
        rect.GetWorldCorners(corners);
        Vector3 lowerLeft = viewport.InverseTransformPoint(corners[0]);
        Vector3 upperRight = viewport.InverseTransformPoint(corners[2]);
        Assert.That(viewport.rect.xMax - upperRight.x, Is.EqualTo(40f).Within(.05f));
        Assert.That(lowerLeft.y - viewport.rect.yMin, Is.EqualTo(80f).Within(.05f));
        Assert.That(rect.GetSiblingIndex(), Is.EqualTo(rect.parent.childCount - 1), "Skip stays above the native screen click area");
    }

    private void InvokeItemLateUpdate()
        => typeof(UIItemGacha).GetMethod("LateUpdate", Fields).Invoke(_item, null);

    [Test]
    public void ItemHide_WhenAlreadyIdleStillCancelsFinalCapsuleAndHidesButtons()
    {
        UIGacha view = _item.GetComponentInParent<UIGacha>(true);
        typeof(GachaMachineParent).GetField("_uiGacha", Fields).SetValue(_item, view);
        Write(_item, "_currentStep", 1);
        Write(_item, "_isShowingSummary", true);
        _capsule.PreparePresentation(_sprite, _color, new Vector2(600, -2000));
        _capsule.TweenAnchoredPosition(new Vector2(600, 0), 1f, Ease.Smoothstep);
        _item.Hide();
        Assert.That(_capsule.gameObject.activeSelf, Is.False);
        Assert.That(_capsule.IsMoving, Is.False);
        Assert.That(Read<bool>(_item, "_isShowingSummary"), Is.False);
        Assert.That(Read<Animator>(_item, "_gachaMacineAnimator").enabled, Is.False);
        foreach (string field in new[] { "_singleButton", "_tenButton", "_skipButton", "_screenButton" })
            Assert.That(Read<Button>(_item, field).gameObject.activeSelf, Is.False, field);
    }

    private static T Read<T>(object target, string name)
        => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static void Write(object target, string name, object value)
        => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static string AccountState() => JsonConvert.SerializeObject(new
    {
        UserInfo.Money,
        UserInfo.Dia,
        UserInfo.TotalUseGachaMachineCount,
        PaymentInfo.GachaPaymentDatas,
        Items = UserInfo.GetGiveGachaItemDic(),
        ItemCounts = UserInfo.GetGiveGachaItemCountDic(),
        ItemLevels = UserInfo.GetGiveGachaItemLevelDic()
    });

    [Test]
    public void ItemOfflinePresentation_UsesOriginalEventsAndControlsButRejectsAllGrantEntrypoints()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        UnityEngine.Random.State random = UnityEngine.Random.state;
        string before = AccountState();
        bool sdkBefore = BackEnd.Backend.IsInitialized;
        bool loginBefore = BackEnd.Backend.IsLogin;
        try
        {
            using (var session = new StaffGachaOfflineSession(Resources.LoadAll<StaffData>("StaffData")))
            {
                GameObject root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(scene,
                    out UIGacha view, out UIStaffGacha staff, includeNavigation: true, includeItemPresentation: true);
                StaffGachaOfflineViewFactory.ConfigureNavigation(view, staff, session.Owner);
                UIItemGacha item = root.GetComponentInChildren<UIItemGacha>(true);
                item.ConfigureEditorOfflinePresentation(view);
                Assert.That(Read<List<UIGachaCardSlot>>(item, "_getItemSlotList").Count, Is.EqualTo(10));
                Assert.That(Read<UIBouncingBall>(item, "_bouncingBall"), Is.Not.Null);
                Assert.That(Read<Animator>(item, "_gachaMacineAnimator").fireEvents, Is.True);
                Assert.That(item.EditorNativeSoundSource.transform, Is.SameAs(item.transform));
                Assert.That(item.SingleButton.interactable, Is.False);
                Assert.That(Read<Button>(item, "_tenButton").interactable, Is.False);
                Assert.That(root.GetComponentsInChildren<MainScene>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Muks.BackEnd.BackendManager>(true), Is.Empty);
                item.OnSingleGachaButtonClicked();
                item.OnTenGachaButtonClicked();
                item.GetItem(null);
                Assert.That(item.StartAddItem(null), Is.False);
                Assert.Throws<InvalidOperationException>(() => item.BeginEditorOfflinePresentation(null));
                Assert.That(item.EditorFixedResultIds, Is.Empty);
                Assert.That(session.DrawCount, Is.Zero);
                Assert.That(session.PurchaseWrites, Is.Zero);
                Assert.That(session.FollowupWrites, Is.Zero);
                Assert.That(session.Request, Is.Null);
                Assert.That(AccountState(), Is.EqualTo(before));
                Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(sdkBefore));
                Assert.That(BackEnd.Backend.IsLogin, Is.EqualTo(loginBefore));
            }
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = random;
        }
    }

    [Test]
    public void ItemOfflinePresentation_MachineAnimationEventsCannotCallPurchaseOrGrant()
    {
        var allowed = new HashSet<string>
        {
            "SetStep", "PlayLeverSound", "PlayShakeCapsuleSound", "PlayFallCapsuleSound",
            "PlayOpenDoorSound", "PlayBoomSound", "PlayGetItemSound", "StartBallBounce",
            "StopBallBounce", "CapsuleSetSibilingIndex"
        };
        Animator machine = Read<Animator>(_item, "_gachaMacineAnimator");
        var events = machine.runtimeAnimatorController.animationClips.Distinct()
            .SelectMany(clip => UnityEditor.AnimationUtility.GetAnimationEvents(clip)).ToArray();
        Assert.That(events.Length, Is.GreaterThan(0));
        Assert.That(events.Any(item => item.functionName == "SetStep"), Is.True);
        foreach (AnimationEvent item in events)
            Assert.That(allowed.Contains(item.functionName), Is.True, item.functionName);
    }
}
#endif
