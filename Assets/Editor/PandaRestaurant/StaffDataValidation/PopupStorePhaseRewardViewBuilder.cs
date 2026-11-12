#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit authoring of a new Editor-only overlay. Never replaces a Scene or original popup asset.</summary>
internal static class PopupStorePhaseRewardViewBuilder
{
    internal const string PrefabPath = "Assets/Editor/PandaRestaurant/StaffDataValidation/UI/PopupStorePhaseRewardView.prefab";
    internal const string DiamondPath = "Assets/Sprites/UI/UIGacha/가챠UI/가챠UI_다이아.png";
    internal const string BoomPath = "Assets/Animation/Boom/Boom_Play.anim";
    internal const string FontPath = "Assets/TextMesh Pro/Fonts/영덕대게체.asset";

    public static GameObject Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Author reward presentation only while the original Editor is idle.");
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null && existing.GetComponent<PopupStorePhaseRewardPresentation>() == null)
            throw new InvalidOperationException("The target path belongs to another asset.");
        var diamond = Require<Sprite>(DiamondPath);
        var boom = Require<AnimationClip>(BoomPath);
        var font = Require<TMP_FontAsset>(FontPath);
        var buttonSprite = Require<Sprite>("Assets/Sprites/Tutorial/MiniGameTutorial/UI_튜토리얼_미니게임_플레이 버튼.png");
        GameObject root = null;
        try
        {
            root = new GameObject("PopupStore Phase Reward View", typeof(RectTransform), typeof(CanvasGroup), typeof(PopupStorePhaseRewardPresentation));
            root.layer = 5;
            Stretch((RectTransform)root.transform);
            var group = root.GetComponent<CanvasGroup>();
            group.ignoreParentGroups = true; group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            var dim = Picture("Reward Background", root.transform, null, Vector2.zero, Vector2.zero);
            Stretch(dim.rectTransform); dim.color = new Color(.05f, .025f, .015f, .78f); dim.raycastTarget = true;
            var sparkles = Rect("Diamond Burst", root.transform, Vector2.zero, new Vector2(900, 700));
            var diamonds = new Image[12];
            for (int i = 0; i < diamonds.Length; i++)
                diamonds[i] = Picture("Original Diamond " + (i + 1), sparkles, diamond, Vector2.zero, new Vector2(105, 105));
            var central = Picture("Central Diamond", root.transform, diamond, new Vector2(0, 72), new Vector2(240, 240));
            var box = Picture("Approved Reward Box", root.transform, null, new Vector2(0, -780), new Vector2(430, 430));
            var tap = box.gameObject.AddComponent<Button>(); tap.targetGraphic = box; tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };
            var boomImage = Picture("Original Boom", root.transform, null, new Vector2(0, 45), new Vector2(530, 530));
            var touchText = Text("Touch Prompt", root.transform, font, new Vector2(0, -285), new Vector2(1000, 80), 46, "상자를 터치해 주세요");
            var amount = Text("Unpaid Amount", root.transform, font, new Vector2(0, -215), new Vector2(1050, 150), 110, string.Empty);
            amount.fontStyle = FontStyles.Bold; amount.color = new Color(1, .94f, .54f);
            var disclaimer = Text("Preview Disclaimer", root.transform, font, new Vector2(0, -325), new Vector2(1400, 100), 30, string.Empty);
            disclaimer.color = new Color(1, .93f, .82f);
            var confirmImage = Picture("Confirm", root.transform, buttonSprite, new Vector2(0, -440), new Vector2(340, 110));
            confirmImage.raycastTarget = true;
            var confirm = confirmImage.gameObject.AddComponent<Button>(); confirm.targetGraphic = confirmImage;
            confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            Text("Confirm Label", confirm.transform, font, Vector2.zero, new Vector2(300, 100), 54, "확인").color = new Color(.27f, .13f, .03f);
            var view = root.GetComponent<PopupStorePhaseRewardPresentation>();
            var so = new SerializedObject(view);
            Set(so, "_overlay", group); Set(so, "_boxImage", box); Set(so, "_boxButton", tap);
            Set(so, "_boomImage", boomImage); Set(so, "_boomClip", boom); Set(so, "_centralDiamond", central);
            SetOriginalBoomKeys(so, boom);
            Set(so, "_tapText", touchText); Set(so, "_amountText", amount); Set(so, "_disclaimerText", disclaimer);
            Set(so, "_confirmButton", confirm);
            var entries = so.FindProperty("_diamondImages"); entries.arraySize = diamonds.Length;
            for (int i = 0; i < diamonds.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = diamonds[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            view.HidePresentation();
            if (!view.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    private static T Require<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing original asset: " + path);
    private static void SetOriginalBoomKeys(SerializedObject so, AnimationClip clip)
    {
        var spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Where(x => x.type == typeof(Image) && x.path == string.Empty && x.propertyName == "m_Sprite").ToArray();
        var alphaBindings = AnimationUtility.GetCurveBindings(clip)
            .Where(x => x.type == typeof(Image) && x.path == string.Empty && x.propertyName == "m_Color.a").ToArray();
        if (spriteBindings.Length != 1 || alphaBindings.Length != 1)
            throw new InvalidOperationException("Original boom must have one local UI Image sprite/alpha track.");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, spriteBindings[0]);
        var frames = so.FindProperty("_boomFrames"); var times = so.FindProperty("_boomFrameTimes");
        frames.arraySize = times.arraySize = keys.Length;
        for (int i = 0; i < keys.Length; i++)
        {
            if (!(keys[i].value is Sprite)) throw new InvalidOperationException("Original boom contains a missing Sprite frame.");
            frames.GetArrayElementAtIndex(i).objectReferenceValue = keys[i].value;
            times.GetArrayElementAtIndex(i).floatValue = keys[i].time;
        }
        so.FindProperty("_boomAlphaCurve").animationCurveValue = AnimationUtility.GetEditorCurve(clip, alphaBindings[0]);
    }
    private static void Set(SerializedObject so, string property, Object value)
        => so.FindProperty(property).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, string value)
    {
        var rect = Rect(name, parent, position, size); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.text = value;
        text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
        text.enableAutoSizing = false; text.enableWordWrapping = true; text.raycastTarget = false;
        return text;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
#endif
