#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit UI-02 authoring on the original popup prefab. No game/session changes or automatic import hooks.</summary>
public static class PopupStoreMainVisualIntegration
{
    private static readonly Color Ink = new Color(.30f, .16f, .08f, 1);

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || PrefabStageUtility.GetCurrentPrefabStage() != null
            || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("UI-02 requires clean idle EditMode. User scenes are never saved or discarded.");
        var selection = Selection.instanceIDs;
        var scene = SceneManager.GetActiveScene();
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(PopupStoreApprovedUiIntegration.PrefabPath);
            if (root.activeSelf) throw new InvalidOperationException("Author the existing inactive prefab only.");
            var oldX = Need<Image>(root.transform, "HUD/Close Button").sprite;
            var font = Need<TMP_Text>(root.transform, "HUD/Score");
            ApplyStage(root, font);
            ApplyHud(root);
            ApplyCustomers(root, font);
            ApplyCounter(root);
            foreach (string name in new[] { "Box Panel", "Plush Panel", "Ribbon Panel" })
            {
                var panel = Need<RectTransform>(root.transform, name);
                panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, -395);
                var title = Need<TMP_Text>(panel, "Panel Title");
                title.fontSize = 32;
                if (name == "Plush Panel")
                {
                    Rect(title.rectTransform, -264, 101, 155, 46);
                    title.fontSize = 28;
                }
            }
            if (Need<Image>(root.transform, "HUD/Close Button").sprite != oldX)
                throw new InvalidOperationException("The original X sprite must not change.");
            PrefabUtility.SaveAsPrefabAsset(root, PopupStoreApprovedUiIntegration.PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("UI-02 prefab save failed.");
            Debug.Log("POPUP_STORE_UI02_APPLIED original prefab; authored visual geometry only; PNG/importers/game rules unchanged");
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            if (!selection.SequenceEqual(Selection.instanceIDs) || SceneManager.GetActiveScene() != scene)
                Debug.LogError("UI-02 changed user selection or active scene; inspect without forcing restoration.");
        }
    }

    private static void ApplyStage(GameObject root, TMP_Text font)
    {
        var background = Need<Image>(root.transform, "Approved Front Background");
        // A separate translucent UI layer reduces background contrast without editing approved pixels.
        var veil = ImageChild(root.transform, "Background Warm Veil", null);
        Stretch(veil.rectTransform);
        veil.color = new Color(1f, .94f, .82f, .14f);
        veil.transform.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);

        // Reuse the original wood counter pixels. A mask exposes the counter region; the PNG is untouched.
        var foreground = root.transform.Find("Wood Counter Foreground") as RectTransform;
        if (foreground == null)
        {
            var go = new GameObject("Wood Counter Foreground", typeof(RectTransform), typeof(RectMask2D));
            go.layer = root.layer; go.transform.SetParent(root.transform, false); foreground = (RectTransform)go.transform;
        }
        const float scale = 1840f / 1332f;
        Rect(foreground, 0, -36 - 212 * scale * .5f, 1840, 212 * scale);
        var wood = ImageChild(foreground, "Original Background Counter", background.sprite);
        wood.preserveAspect = false;
        Rect(wood.rectTransform, (836 - (170 + 666)) * scale, ((502 + 106) - 470.5f) * scale, 1672 * scale, 941 * scale);
        // Stage is behind customers, while gauges and actions remain above it in their original hierarchy.
        foreground.SetSiblingIndex(veil.transform.GetSiblingIndex() + 1);
        var sign = TextChild(root.transform, "Hanging Sign Caption", font);
        Rect(sign.rectTransform, -800, 330, 185, 115);
        Style(sign, 28, Ink);
        sign.text = "스텝 봉제인형\n팝업스토어";
        sign.transform.SetSiblingIndex(foreground.GetSiblingIndex() + 1);
    }

    private static void ApplyHud(GameObject root)
    {
        var hud = Need<RectTransform>(root.transform, "HUD");
        Rect(hud, 0, 455, 1840, 124);
        Rect(Need<RectTransform>(hud, "Approved Score Frame"), -190, 0, 930, 124);
        Rect(Need<RectTransform>(hud, "Current Grade Reward"), -580, 0, 110, 110);
        var score = Need<TMP_Text>(hud, "Score");
        Rect(score.rectTransform, -405, 0, 220, 70); Style(score, 38, Color.white);
        Rect(Need<RectTransform>(hud, "Grade Progress Track"), 0, -25, 500, 46);
        var next = Need<TMP_Text>(hud, "Next Grade");
        Rect(next.rectTransform, 0, 24, 460, 40); Style(next, 28, Color.white);
        Rect(Need<RectTransform>(hud, "Approved Timer Frame"), 510, 0, 330, 124);
        Rect(Need<RectTransform>(hud, "Approved Pink Clock"), 410, 0, 108, 108);
        var time = Need<TMP_Text>(hud, "Time");
        Rect(time.rectTransform, 555, 0, 196, 94); Style(time, 32, Ink);
        Rect(Need<RectTransform>(hud, "Close Button"), 850, 5, 94, 94);
        var tickets = Need<TMP_Text>(hud, "Daily Business Tickets HUD");
        Rect(tickets.rectTransform, 760, -63, 250, 32); Style(tickets, 22, Ink);
    }

    private static void ApplyCustomers(GameObject root, TMP_Text font)
    {
        var orders = new SerializedObject(root.GetComponent<PopupStoreOrderDisplay>());
        orders.FindProperty("_iconSize").vector2Value = new Vector2(112, 112);
        orders.FindProperty("_spacing").floatValue = 20;
        orders.FindProperty("_padding").vector2Value = new Vector2(24, 28);
        orders.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 1; i <= 3; i++)
        {
            var place = Need<RectTransform>(root.transform, "Customer Place " + i);
            Rect(place, -430 + (i - 1) * 480, 134, 240, 260);
            var portrait = Need<Image>(place, "Customer Portrait");
            float size = i == 3 ? 340 : 352;
            Rect(portrait.rectTransform, 0, 0, size, size); portrait.preserveAspect = true;
            var balloon = Need<RectTransform>(place, "Order Balloon");
            Rect(balloon, 0, i == 3 ? 178 : 146, 480, 210);
            for (int j = 2; j <= 5; j++)
            {
                var icon = Need<RectTransform>(balloon, "Order Material " + j);
                var plus = TextChild(icon, "Order Separator", font);
                Rect(plus.rectTransform, -10, 0, 20, 32);
                plus.rectTransform.anchorMin = plus.rectTransform.anchorMax = new Vector2(0, .5f);
                Style(plus, 26, Ink); plus.text = "+";
                plus.enableAutoSizing = true; plus.fontSizeMin = 8; plus.fontSizeMax = 26;
                // Parent activation follows the authored order icon, including packed/expired/empty orders.
                plus.raycastTarget = false;
            }
            var gauge = Need<RectTransform>(place, "Patience Gauge");
            Rect(gauge, 0, -184, 300, 60);
            var track = Need<RectTransform>(gauge, "Gauge Track");
            Rect(track, 16, 0, 230, 52);
            Rect(Need<RectTransform>(track, "Gauge Fill"), 0, 0, 211.2f, 31.2f);
            Rect(Need<RectTransform>(gauge, "Approved Blue Clock"), -104, 0, 52, 60);
            var label = Need<TMP_Text>(gauge, "Patience Time");
            Rect(label.rectTransform, 30, 0, 154, 30); Style(label, 20, Color.white);
        }
    }

    private static void ApplyCounter(GameObject root)
    {
        const float width = 900, height = width * 195f / 924f;
        var counter = Need<RectTransform>(root.transform, "Sales Counter - Five Slots");
        Rect(counter, 0, -170, width, height);
        Rect(Need<RectTransform>(counter, "Approved Tray"), 0, 0, width, height);
        float[] centers = { 134, 296, 460, 623, 793.5f };
        for (int i = 0; i < 5; i++)
        {
            var slot = Need<RectTransform>(counter, "Empty Slot " + (i + 1));
            Rect(slot, (centers[i] / 924f - .5f) * width, (.5f - 96.5f / 195f) * height, 133.2f, 113.4f);
            Rect(Need<RectTransform>(slot, "Selected Material"), 0, 0, 110, 110);
        }
        // Existing packing visuals start on the resized tray; their animation/callback logic is unchanged.
        foreach (string path in new[] { "Packing Boom", "Finished Gift Box" })
        { var rect = Need<RectTransform>(root.transform, path); rect.anchoredPosition = new Vector2(0, -170); }
        var clear = Need<RectTransform>(root.transform, "Clear Counter Button");
        Rect(clear, -720, -169, 360, 360f * 141 / 285);
        var clearText = Need<TMP_Text>(clear, "Label");
        Rect(clearText.rectTransform, 64, 0, 214, 70); Style(clearText, 30, Color.white); clearText.text = "판매대 비우기";
        var pack = Need<RectTransform>(root.transform, "Pack Button");
        Rect(pack, 720, -169, 360, 360f * 143 / 298);
        var packText = Need<TMP_Text>(pack, "Label");
        Rect(packText.rectTransform, 45, 0, 225, 70); Style(packText, 38, Color.white);
    }

    private static T Need<T>(Transform root, string path) where T : Component
        => root.Find(path)?.GetComponent<T>() ?? throw new InvalidOperationException("Missing authored UI: " + path);
    private static void Rect(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.localScale = Vector3.one; r.localRotation = Quaternion.identity; r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); }
    private static void Stretch(RectTransform r)
    { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; r.localScale=Vector3.one; }
    private static Image ImageChild(Transform parent, string name, Sprite sprite)
    {
        var image=parent.Find(name)?.GetComponent<Image>();
        if(image==null) { var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); go.layer=parent.gameObject.layer; go.transform.SetParent(parent,false); image=go.GetComponent<Image>(); }
        image.sprite=sprite; image.color=Color.white; image.raycastTarget=false; image.type=Image.Type.Simple;
        return image;
    }
    private static TMP_Text TextChild(Transform parent,string name,TMP_Text template)
    {
        var text=parent.Find(name)?.GetComponent<TMP_Text>();
        if(text==null) { var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI)); go.layer=parent.gameObject.layer; go.transform.SetParent(parent,false); text=go.GetComponent<TMP_Text>(); }
        text.font=template.font; text.fontSharedMaterial=template.fontSharedMaterial; text.raycastTarget=false; return text;
    }
    private static void Style(TMP_Text text,float size,Color color)
    { text.fontSize=size; text.enableAutoSizing=false; text.color=color; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; }
}
#endif
