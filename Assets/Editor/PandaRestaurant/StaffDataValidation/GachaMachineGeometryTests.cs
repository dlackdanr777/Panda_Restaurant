#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Uses the existing Stage1 controls and clips; never enters gameplay or purchases.</summary>
public sealed class GachaMachineGeometryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene _scene;
    private long _money;
    private int _dia, _count;
    private bool _backend;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void SetUp()
    {
        _money = UserInfo.Money; _dia = UserInfo.Dia; _count = UserInfo.TotalUseGachaMachineCount;
        _backend = BackEnd.Backend.IsInitialized; _random = UnityEngine.Random.state;
        _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(_count));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_backend));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        }
        finally { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OriginalMachine_IdleAndStartKeepAuthoredFrontFacingHalfOrder(bool staff)
    {
        Component machine = staff ? (Component)Find<UIStaffGacha>() : Find<UIItemGacha>();
        var animator = Read<Animator>(machine, "_gachaMacineAnimator");
        animator.fireEvents = false; animator.enabled = false;
        var clips = animator.runtimeAnimatorController.animationClips;
        var idle = clips.Single(clip => clip.name == "Idle_Gacha");
        Assert.That(AssetDatabase.GetAssetPath(idle), Is.EqualTo("Assets/Animation/GachaMachine/ItemGacha/Idle_Gacha.anim"),
            "Both product controllers use the shared idle clip");
        var start = clips.First(clip => clip.name == "Start_Gacha");
        AssertDoorCurve(idle, "Left Door", -30.9f);
        AssertDoorCurve(idle, "Right Door", 30f);
        // The staff Start clip authors both halves 3.3 units farther left.
        // Preserve that machine-specific offset; only the shared Idle had its
        // halves swapped. Both clips keep the same closed-door separation.
        float startLeft = staff ? -34.2f : -30.9f;
        float startRight = staff ? 26.7f : 30f;
        AssertDoorCurve(start, "Left Door", startLeft);
        AssertDoorCurve(start, "Right Door", startRight);

        var left = (RectTransform)animator.transform.Find("Gacha Machine/Left Door");
        var right = (RectTransform)animator.transform.Find("Gacha Machine/Right Door");
        Sprite leftSprite = left.GetComponent<Image>().sprite, rightSprite = right.GetComponent<Image>().sprite;
        Quaternion leftRotation = left.localRotation, rightRotation = right.localRotation;
        idle.SampleAnimation(animator.gameObject, 0f);
        Assert.That(left.anchoredPosition.x, Is.EqualTo(-30.9f).Within(.001f));
        Assert.That(right.anchoredPosition.x, Is.EqualTo(30f).Within(.001f));
        Assert.That(left.anchoredPosition.x, Is.LessThan(right.anchoredPosition.x));
        Assert.That(left.GetComponent<Image>().sprite, Is.SameAs(leftSprite));
        Assert.That(right.GetComponent<Image>().sprite, Is.SameAs(rightSprite));
        Assert.That(left.localRotation, Is.EqualTo(leftRotation));
        Assert.That(right.localRotation, Is.EqualTo(rightRotation));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(leftSprite)), Is.EqualTo("cdda939d507b2a04ab3fcaca259f13c5"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rightSprite)), Is.EqualTo("2fb5b64c65741e04193bd153b8ad6e20"));
        start.SampleAnimation(animator.gameObject, 0f);
        Assert.That(left.anchoredPosition.x, Is.EqualTo(startLeft).Within(.001f));
        Assert.That(right.anchoredPosition.x, Is.EqualTo(startRight).Within(.001f));
        Assert.That(right.anchoredPosition.x - left.anchoredPosition.x, Is.EqualTo(60.9f).Within(.001f));
        Assert.That(left.GetComponent<Image>().sprite, Is.SameAs(leftSprite));
        Assert.That(right.GetComponent<Image>().sprite, Is.SameAs(rightSprite));
        Assert.That(left.localRotation, Is.EqualTo(leftRotation));
        Assert.That(right.localRotation, Is.EqualTo(rightRotation));
    }

    [Test]
    public void FreeItemInput_MatchesNativeWorldRectangleAcrossDifferentParentsAndPivots()
    {
        UITutorial guide; RectTransform source, target;
        PrepareInput(out guide, out source, out target);
        Assert.That(target.localScale, Is.EqualTo(Vector3.one), "The authored press-effect neutral scale must remain one");
        guide.Gacha1ButtonSetActive(true, source);
        AssertSameCorners(source, target);
        Assert.That(source.gameObject.activeSelf, Is.True, "Layout does not change the product control's visibility");
        Assert.That(guide.Gacha1Button.Interactable, Is.True);
    }

    [Test]
    public void FreeItemInput_ResizeTracksNativeRectangleAndCirclePawWithoutResettingPressScale()
    {
        UITutorial guide; RectTransform source, target;
        PrepareInput(out guide, out source, out target);
        guide.Gacha1ButtonSetActive(true, source);
        guide.PunchHoleSetActive(true);
        guide.CustomHoleSetActiveImmediate(true, 350f, target.name, target);
        // The original normal button is hidden during the free tutorial input.
        source.gameObject.SetActive(false);
        ((RectTransform)source.parent).sizeDelta += new Vector2(640f, -220f);
        source.parent.localScale = new Vector3(.6f, 1.3f, 1f);
        source.anchoredPosition += new Vector2(90f, 130f);
        source.sizeDelta += new Vector2(55f, 35f);
        InvokeLateUpdate(guide);
        AssertSameCorners(source, target);
        Vector3 center = source.TransformPoint(source.rect.center);
        Assert.That(Vector3.Distance(Read<HoleClickHandler>(guide, "_customHole").HoleRect.position, center), Is.LessThan(.01f));
        Assert.That(Vector3.Distance(Read<RectTransform>(guide, "_customHoleCursorParent").position, center), Is.LessThan(.01f));
        target.localScale = new Vector3(.9f, .9f, 1f);
        InvokeLateUpdate(guide);
        Assert.That(target.localScale, Is.EqualTo(new Vector3(.9f, .9f, 1f)), "ButtonPressEffect owns click animation scale");
        Assert.That(source.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void FreeItemInput_HideAndReentryClearOldReferenceAndDoNotDrift()
    {
        UITutorial guide; RectTransform source, target;
        PrepareInput(out guide, out source, out target);
        guide.Gacha1ButtonSetActive(true, source);
        for (int i = 0; i < 5; i++) InvokeLateUpdate(guide);
        AssertSameCorners(source, target);
        guide.Hide();
        Assert.That(Read<RectTransform>(guide, "_gacha1ButtonReference"), Is.Null);
        Assert.That(target.gameObject.activeSelf, Is.False);
        Vector3 hiddenPosition = target.position;
        source.anchoredPosition += new Vector2(100f, 100f);
        InvokeLateUpdate(guide);
        Assert.That(target.position, Is.EqualTo(hiddenPosition));
        guide.Show();
        guide.Gacha1ButtonSetActive(true, source);
        AssertSameCorners(source, target);
        guide.runInEditMode = true;
        guide.gameObject.SetActive(false);
        Assert.That(Read<RectTransform>(guide, "_gacha1ButtonReference"), Is.Null);
        Assert.That(target.gameObject.activeSelf, Is.False);
    }

    private void PrepareInput(out UITutorial guide, out RectTransform source, out RectTransform target)
    {
        guide = Find<UITutorial>();
        source = (RectTransform)Find<UIItemGacha>().SingleButton.transform;
        target = (RectTransform)guide.Gacha1Button.transform;
        Assert.That(target.localScale, Is.EqualTo(Vector3.one));
        // Only display controls are detached. No scene managers or game Init run.
        guide.transform.SetParent(null, false);
        var overlay = (RectTransform)guide.transform;
        overlay.localScale = new Vector3(.8f, 1.2f, 1f);
        overlay.sizeDelta = new Vector2(1920f, 1080f);
        overlay.position = new Vector3(-280f, 50f, 0f);
        var nativeParent = new GameObject("isolated native button geometry", typeof(RectTransform)).GetComponent<RectTransform>();
        SceneManager.MoveGameObjectToScene(nativeParent.gameObject, _scene);
        nativeParent.sizeDelta = new Vector2(2300f, 1600f);
        nativeParent.localScale = new Vector3(1.1f, .7f, 1f);
        nativeParent.position = new Vector3(130f, -100f, 0f);
        source.SetParent(nativeParent, false);
        source.anchorMin = source.anchorMax = new Vector2(.3f, .15f);
        source.pivot = new Vector2(.2f, .8f);
        source.anchoredPosition = new Vector2(55f, 80f);
        source.gameObject.SetActive(true);
        guide.Show();
    }

    private static void AssertDoorCurve(AnimationClip clip, string name, float expected)
    {
        var binding = EditorCurveBinding.FloatCurve("Gacha Machine/" + name, typeof(RectTransform), "m_AnchoredPosition.x");
        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        Assert.That(curve, Is.Not.Null);
        Assert.That(curve.Evaluate(0f), Is.EqualTo(expected).Within(.001f));
    }

    private static void AssertSameCorners(RectTransform source, RectTransform target)
    {
        var expected = new Vector3[4]; var actual = new Vector3[4];
        source.GetWorldCorners(expected); target.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++)
            Assert.That(Vector3.Distance(actual[i], expected[i]), Is.LessThan(.02f), "World corner " + i);
    }

    private T Find<T>() where T : Component => _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static void InvokeLateUpdate(UITutorial guide) => typeof(UITutorial).GetMethod("LateUpdate", Private).Invoke(guide, null);
}
#endif
