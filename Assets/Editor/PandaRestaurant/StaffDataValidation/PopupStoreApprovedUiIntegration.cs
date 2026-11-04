#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, one-prefab approved-art application. Never runs on import or in a Player.</summary>
public static class PopupStoreApprovedUiIntegration
{
    public const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    public const string SpriteFolder = "Assets/Sprites/PopupStore/UIApproved_v1_20261104/";
    public const string DocumentFolder = "Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStoreUIApproved_v1_20261104/";

    [Serializable] private sealed class ApprovalManifest { public bool art_approved; public int sprite_count; public ApprovedAsset[] assets; }
    [Serializable] private sealed class ApprovedAsset { public string file; public string sha256; public int[] pixels_wh; }
    [Serializable] private sealed class ImportManifest { public ImportAsset[] assets; }
    [Serializable] private sealed class ImportAsset { public string file; public ImportSettings unity; }
    [Serializable] private sealed class ImportSettings { public int[] nine_slice_border_lbrt_candidate_px; }
    [Serializable] private sealed class ApplyReport
    {
        public string prefab;
        public string[] approvedPngs;
        public string[] changedObjectPaths;
        public string[] unusedAlternatives;
        public string[] materialLayout;
        public bool codeAndGameRulesUnchanged;
        public bool pngBytesUnchanged;
        public bool existingXSpriteUnchanged;
        public int resultRewardBoxComponents;
    }

    /// <summary>Called deliberately by an Editor command; does not select, save, or discard user scenes.</summary>
    public static string Apply()
    {
        RequireSafeEditor();
        var approval = JsonUtility.FromJson<ApprovalManifest>(File.ReadAllText(DocumentFolder + "approval_manifest.json"));
        var imports = JsonUtility.FromJson<ImportManifest>(File.ReadAllText(DocumentFolder + "manifest.json"));
        ValidateSourceFiles(approval);
        var priorSelection = Selection.instanceIDs;
        string priorActiveScene = SceneManager.GetActiveScene().path;
        string[] sourceHashes = approval.assets.Select(a => Hash(SpriteFolder + Path.GetFileName(a.file))).ToArray();
        ConfigureApprovedImporters(approval, imports);
        GameObject root = null;
        var touched = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            // The actual authored view is inactive. Do not execute any business callbacks while authoring.
            if (root.activeSelf) throw new InvalidOperationException("The authored popup must remain inactive during UI integration.");
            if (root.GetComponentsInChildren<PopupStoreRewardBox>(true).Length != 1)
                throw new InvalidOperationException("The existing result reward-box component must remain singular.");
            var oldX = Need<Image>(root.transform, "HUD/Close Button").sprite;
            if (oldX == null) throw new InvalidOperationException("The approved existing X Sprite is missing.");
            // The popup owns this scaler. Height matching keeps the authored 1080-high layout
            // inside wider landscape views without changing shared Canvas or project settings.
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                throw new InvalidOperationException("The authored popup CanvasScaler contract is missing.");
            scaler.matchWidthOrHeight = 1f;
            touched.Add("Popup Store (local CanvasScaler height match)");
            ApplyHud(root, touched);
            ApplyCustomers(root, touched);
            ApplyCounter(root, touched);
            ApplyMaterials(root, "Box Panel", "UI_Panel_Materials_Box", -680, 480, false, touched);
            ApplyMaterials(root, "Plush Panel", "UI_Panel_Materials_Plush", 0, 840, true, touched);
            ApplyMaterials(root, "Ribbon Panel", "UI_Panel_Materials_Ribbon", 680, 480, false, touched);
            BindCurrentGradeHud(root);
            if (Need<Image>(root.transform, "HUD/Close Button").sprite != oldX)
                throw new InvalidOperationException("The original X Sprite changed unexpectedly.");
            if (root.GetComponentsInChildren<PopupStoreRewardBox>(true).Length != 1)
                throw new InvalidOperationException("The result component was duplicated.");
            for (int i = 0; i < approval.assets.Length; i++)
                if (sourceHashes[i] != Hash(SpriteFolder + Path.GetFileName(approval.assets[i].file)))
                    throw new InvalidOperationException("An approved PNG changed during integration.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("The approved popup prefab could not be saved.");
            var report = new ApplyReport
            {
                prefab = PrefabPath,
                approvedPngs = approval.assets.Select(a => SpriteFolder + Path.GetFileName(a.file)).ToArray(),
                changedObjectPaths = touched.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                unusedAlternatives = new[] { "UI_Button_Clear_Blank", "UI_Button_Pack_Blank", "UI_PageDot_Blue", "UI_PageDot_Inactive", "UI_PageDot_Orange", "UI_PageDot_Pink", "UI_Sign_HangingBlank", "UI_Panel_FloralBrown", "UI_Panel_FloralCream" },
                materialLayout = new[] { "Box: panel 480x260, viewport 384x160, icons 118x136, spacing 8, padding 6; 3 complete", "Plush: panel 840x260, viewport 740x160, icons 126x144, spacing 12, padding 10; 5 complete", "Ribbon: panel 480x260, viewport 384x160, icons 118x136, spacing 8, padding 6; 3 complete" },
                codeAndGameRulesUnchanged = true,
                pngBytesUnchanged = true,
                existingXSpriteUnchanged = true,
                resultRewardBoxComponents = 1
            };
            string json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory("Temp/PopupStoreVerification");
            File.WriteAllText("Temp/PopupStoreVerification/approved-ui-apply.json", json);
            return json;
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            if (!priorSelection.SequenceEqual(Selection.instanceIDs)
                || SceneManager.GetActiveScene().path != priorActiveScene)
                Debug.LogError("POPUP_STORE_UI_INTEGRATION: user selection or active scene changed; inspect without forcing restoration.");
        }
    }

    private static void RequireSafeEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("UI application requires an idle Edit Mode editor.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close the current Prefab Stage through its normal user flow before applying approved UI.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("A user scene is dirty; no forced save or discard is performed.");
    }

    private static void ValidateSourceFiles(ApprovalManifest approval)
    {
        if (approval == null || !approval.art_approved || approval.sprite_count != 30 || approval.assets == null || approval.assets.Length != 30)
            throw new InvalidOperationException("The complete user-approved 30-sprite manifest is required.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in approval.assets)
        {
            if (a == null || string.IsNullOrEmpty(a.file) || !a.file.StartsWith("sprites/", StringComparison.Ordinal)
                || a.file.IndexOf("..", StringComparison.Ordinal) >= 0 || !names.Add(Path.GetFileName(a.file))
                || a.pixels_wh == null || a.pixels_wh.Length != 2)
                throw new InvalidOperationException("Invalid or duplicated approved sprite entry.");
            string path = SpriteFolder + Path.GetFileName(a.file);
            if (!File.Exists(path) || !string.Equals(Hash(path), a.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Approved sprite bytes do not match: " + path);
        }
        if (Directory.GetFiles(SpriteFolder, "*.png").Length != 30)
            throw new InvalidOperationException("Only the approved 30 PNG sources belong in the new sprite folder.");
    }

    private static void ConfigureApprovedImporters(ApprovalManifest approval, ImportManifest imports)
    {
        foreach (var a in approval.assets)
        {
            string path = SpriteFolder + Path.GetFileName(a.file);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing new UI texture importer: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(a.pixels_wh[0], a.pixels_wh[1])));
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            var rule = imports?.assets?.FirstOrDefault(x => x.file == a.file);
            var border = rule?.unity?.nine_slice_border_lbrt_candidate_px;
            importer.spriteBorder = border != null && border.Length == 4
                ? new Vector4(border[0], border[1], border[2], border[3]) : Vector4.zero;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.width != a.pixels_wh[0] || sprite.rect.height != a.pixels_wh[1])
                throw new InvalidOperationException("The imported sprite dimensions do not match approval: " + path);
        }
    }

    private static void ApplyHud(GameObject root, HashSet<string> touched)
    {
        var hud = Need<RectTransform>(root.transform, "HUD");
        Rect(hud, 0, 451, 1840, 124);
        var hudImage = hud.GetComponent<Image>();
        if (hudImage != null) { hudImage.enabled = false; hudImage.raycastTarget = false; }
        Need<Transform>(hud, "Title").gameObject.SetActive(false);
        Need<Transform>(hud, "Grade").gameObject.SetActive(false);
        Need<Transform>(hud, "Pause Button").gameObject.SetActive(false);
        var scoreFrame = Decorative(hud, "Approved Score Frame", "UI_HUD_ScoreFrame", true);
        Rect(scoreFrame.rectTransform, -260, 0, 1120, 124);
        scoreFrame.transform.SetAsFirstSibling();
        var timeFrame = Decorative(hud, "Approved Timer Frame", "UI_HUD_TimerFrame", true);
        Rect(timeFrame.rectTransform, 560, 0, 316, 118);
        timeFrame.transform.SetSiblingIndex(1);
        var clock = Decorative(hud, "Approved Pink Clock", "UI_Icon_ClockPink", false);
        Rect(clock.rectTransform, 458, 0, 82, 86);
        var score = Need<TMP_Text>(hud, "Score");
        Rect(score.rectTransform, -540, 0, 220, 70);
        TextStyle(score, 40, Color.white, TextAlignmentOptions.Center);
        var time = Need<TMP_Text>(hud, "Time");
        Rect(time.rectTransform, 605, 0, 190, 86);
        TextStyle(time, 30, new Color(.30f, .16f, .08f), TextAlignmentOptions.Center);
        var clockDisplay = new SerializedObject(root.GetComponent<PopupStoreBusinessClock>());
        clockDisplay.FindProperty("_timeFormat").stringValue = "남은 시간\n{0:00}:{1:00}";
        clockDisplay.ApplyModifiedPropertiesWithoutUndo();
        var tickets = Need<TMP_Text>(hud, "Daily Business Tickets HUD");
        Rect(tickets.rectTransform, 770, -50, 230, 32);
        TextStyle(tickets, 22, new Color(.30f, .16f, .08f), TextAlignmentOptions.Center);
        Rect(Need<RectTransform>(hud, "Close Button"), 850, 12, 76, 76);
        var track = Need<Image>(hud, "Grade Progress Track");
        Skin(track, "UI_Bar_Grade_Track", Image.Type.Sliced, false);
        Rect(track.rectTransform, -90, -24, 500, 46);
        var fill = Need<Image>(track.transform, "Grade Progress Fill");
        Skin(fill, "UI_Bar_Grade_Fill", Image.Type.Filled, false);
        Rect(fill.rectTransform, 0, 0, 480, 33);
        fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        var reward = Decorative(hud, "Current Grade Reward", null, false);
        Rect(reward.rectTransform, -740, 0, 110, 110);
        reward.enabled = false;
        var next = TextChild(hud, "Next Grade", Need<TMP_Text>(hud, "Grade"));
        Rect(next.rectTransform, -90, 24, 420, 40);
        TextStyle(next, 30, Color.white, TextAlignmentOptions.Center);
        next.text = "보상 없음";
        foreach (var path in new[] { "HUD", "HUD/Title", "HUD/Grade", "HUD/Pause Button", "HUD/Approved Score Frame", "HUD/Approved Timer Frame", "HUD/Approved Pink Clock", "HUD/Score", "HUD/Time", "HUD/Daily Business Tickets HUD", "HUD/Close Button", "HUD/Grade Progress Track", "HUD/Grade Progress Track/Grade Progress Fill", "HUD/Current Grade Reward", "HUD/Next Grade" }) touched.Add(path);
    }

    private static void ApplyCustomers(GameObject root, HashSet<string> touched)
    {
        var patience = root.GetComponent<PopupStoreCustomerPatience>();
        SetObject(patience, "_normalSprite", Sprite("UI_Bar_Patience_Fill_Green"));
        SetObject(patience, "_cautionSprite", Sprite("UI_Bar_Patience_Fill_Yellow"));
        SetObject(patience, "_urgentSprite", Sprite("UI_Bar_Patience_Fill_Red"));
        for (int i = 1; i <= 3; i++)
        {
            string parent = "Customer Place " + i;
            var balloon = Need<Image>(root.transform, parent + "/Order Balloon");
            Skin(balloon, "UI_OrderBubble_0" + i, Image.Type.Simple, true);
            Rect(balloon.rectTransform, 0, 210, 408, 179);
            var gauge = Need<RectTransform>(root.transform, parent + "/Patience Gauge");
            Rect(gauge, 0, 108, 330, 44);
            var track = Need<Image>(gauge, "Gauge Track");
            Skin(track, "UI_Bar_Patience_Track", Image.Type.Simple, true);
            Rect(track.rectTransform, -16, 0, 188, 44);
            var fill = Need<Image>(track.transform, "Gauge Fill");
            Skin(fill, "UI_Bar_Patience_Fill_Green", Image.Type.Filled, false);
            // Fill is the track's child, so local zero keeps both centered at gauge x=-16.
            Rect(fill.rectTransform, 0, 0, 172.8f, 26.4f);
            fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            var clock = Decorative(gauge, "Approved Blue Clock", "UI_Icon_ClockBlue", false);
            Rect(clock.rectTransform, -110, 0, 37.9f, 43.2f);
            var time = Need<TMP_Text>(gauge, "Patience Time");
            Rect(time.rectTransform, 118, 0, 60, 30);
            TextStyle(time, 22, new Color(.30f, .16f, .08f), TextAlignmentOptions.Center);
            foreach (var path in new[] { "/Order Balloon", "/Patience Gauge", "/Patience Gauge/Gauge Track", "/Patience Gauge/Gauge Track/Gauge Fill", "/Patience Gauge/Approved Blue Clock", "/Patience Gauge/Patience Time" }) touched.Add(parent + path);
        }
    }

    private static void ApplyCounter(GameObject root, HashSet<string> touched)
    {
        const string counterPath = "Sales Counter - Five Slots";
        var counter = Need<RectTransform>(root.transform, counterPath);
        const float width = 1000, height = width * 195f / 924f;
        Rect(counter, 0, -130, width, height);
        var tray = Decorative(counter, "Approved Tray", "UI_Tray_FiveEmptySlots", false);
        Rect(tray.rectTransform, 0, 0, width, height);
        tray.transform.SetAsFirstSibling();
        float[] centers = { 134, 296, 460, 623, 793.5f };
        for (int i = 0; i < 5; i++)
        {
            var slot = Need<Image>(counter, "Empty Slot " + (i + 1));
            Rect(slot.rectTransform, (centers[i] / 924f - .5f) * width, (.5f - 96.5f / 195f) * height, 148, 126);
            slot.enabled = false; slot.raycastTarget = false; slot.color = Color.clear;
            var selected = Need<Image>(slot.transform, "Selected Material");
            Rect(selected.rectTransform, 0, 0, 118, 118);
            selected.preserveAspect = true; selected.raycastTarget = false;
            touched.Add(counterPath + "/Empty Slot " + (i + 1));
            touched.Add(counterPath + "/Empty Slot " + (i + 1) + "/Selected Material");
        }
        var clear = Need<Image>(root.transform, "Clear Counter Button");
        Skin(clear, "UI_Button_Clear_WithIcon", Image.Type.Simple, true, true);
        Rect(clear.rectTransform, -720, -115, 280, 280f * 141 / 285);
        var clearLabel = Need<TMP_Text>(clear.transform, "Label");
        Rect(clearLabel.rectTransform, 52, 0, 138, 68);
        TextStyle(clearLabel, 28, Color.white, TextAlignmentOptions.Center);
        clearLabel.text = "판매대\n비우기";
        var pack = Need<Image>(root.transform, "Pack Button");
        Skin(pack, "UI_Button_Pack_WithIcon", Image.Type.Simple, true, true);
        Rect(pack.rectTransform, 720, -115, 290, 290f * 143 / 298);
        var packLabel = Need<TMP_Text>(pack.transform, "Label");
        Rect(packLabel.rectTransform, 40, 0, 170, 68);
        TextStyle(packLabel, 36, Color.white, TextAlignmentOptions.Center);
        foreach (var path in new[] { counterPath, counterPath + "/Approved Tray", "Clear Counter Button", "Clear Counter Button/Label", "Pack Button", "Pack Button/Label" }) touched.Add(path);
    }

    private static void ApplyMaterials(GameObject root, string panelPath, string sprite, float x, float width, bool plush, HashSet<string> touched)
    {
        var panel = Need<Image>(root.transform, panelPath);
        Skin(panel, sprite, Image.Type.Sliced, false);
        Rect(panel.rectTransform, x, -370, width, 260);
        var title = Need<TMP_Text>(panel.transform, "Panel Title");
        Rect(title.rectTransform, -width * .5f + (plush ? 150 : 141), 101, plush ? 110 : 132, 46);
        TextStyle(title, plush ? 26 : 28, Color.white, TextAlignmentOptions.Center);
        var viewport = Need<RectTransform>(panel.transform, "Material Viewport");
        Rect(viewport, 0, -20, plush ? 740 : 384, 160);
        // This already-authored viewport is the input surface; the decorative panel above is not.
        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null) viewportImage.raycastTarget = true;
        var previous = Need<Image>(panel.transform, "Previous Page");
        Skin(previous, "UI_Arrow_Left", Image.Type.Simple, true, true);
        Rect(previous.rectTransform, -width * .5f + 24, -20, 32, 32f * 63 / 51);
        var next = Need<Image>(panel.transform, "Next Page");
        Skin(next, "UI_Arrow_Right", Image.Type.Simple, true, true);
        Rect(next.rectTransform, width * .5f - 24, -20, 32, 32f * 63 / 51);
        var list = panel.GetComponent<PopupStoreMaterialList>();
        var serialized = new SerializedObject(list);
        serialized.FindProperty("_iconSize").vector2Value = plush ? new Vector2(126, 144) : new Vector2(118, 136);
        serialized.FindProperty("_spacing").floatValue = plush ? 12 : 8;
        serialized.FindProperty("_padding").floatValue = plush ? 10 : 6;
        serialized.FindProperty("_pageDistance").floatValue = plush ? 690 : 384;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var path in new[] { panelPath, panelPath + "/Panel Title", panelPath + "/Material Viewport", panelPath + "/Previous Page", panelPath + "/Next Page" }) touched.Add(path);
    }

    private static void BindCurrentGradeHud(GameObject root)
    {
        var presenter = root.GetComponent<PopupStoreCurrentGradeHud>();
        if (presenter == null) presenter = root.AddComponent<PopupStoreCurrentGradeHud>();
        SetObject(presenter, "_gradeSession", root.GetComponent<PopupStoreGradeSession>());
        SetObject(presenter, "_score", root.GetComponent<PopupStoreScore>());
        SetObject(presenter, "_clock", root.GetComponent<PopupStoreBusinessClock>());
        SetObject(presenter, "_rewardImage", Need<Image>(root.transform, "HUD/Current Grade Reward"));
        SetObject(presenter, "_nextGradeText", Need<TMP_Text>(root.transform, "HUD/Next Grade"));
        var resultMapping = new SerializedObject(root.GetComponent<PopupStoreRewardBox>());
        foreach (string field in new[] { "_cSprite", "_bSprite", "_aSprite", "_sSprite", "_ssSprite", "_sssSprite" })
            SetObject(presenter, field, resultMapping.FindProperty(field).objectReferenceValue);
    }

    private static Image Decorative(Transform parent, string name, string sprite, bool sliced)
    {
        var found = parent.Find(name);
        Image image;
        if (found != null) image = found.GetComponent<Image>();
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
        }
        if (image == null) throw new InvalidOperationException("Existing decoration name has no Image: " + name);
        image.sprite = sprite == null ? null : Sprite(sprite);
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced;
        image.color = Color.white; image.raycastTarget = false;
        return image;
    }

    private static TMP_Text TextChild(Transform parent, string name, TMP_Text template)
    {
        var found = parent.Find(name);
        if (found != null) return found.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("Existing text name has no TMP component.");
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = template.font;
        text.fontSharedMaterial = template.fontSharedMaterial;
        text.raycastTarget = false;
        return text;
    }

    private static void Skin(Image image, string name, Image.Type type, bool aspect, bool input = false)
    {
        image.sprite = Sprite(name); image.type = type; image.preserveAspect = aspect;
        image.color = Color.white; image.raycastTarget = input;
    }

    private static void Rect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }

    private static void TextStyle(TMP_Text text, float size, Color color, TextAlignmentOptions alignment)
    {
        text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = alignment; text.raycastTarget = false;
    }

    private static T Need<T>(Transform root, string path) where T : Component
    {
        var target = root.Find(path);
        var component = target != null ? target.GetComponent<T>() : null;
        return component != null ? component : throw new InvalidOperationException("Required authored UI is missing: " + path + " (" + typeof(T).Name + ")");
    }

    private static Sprite Sprite(string name)
        => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png")
            ?? throw new InvalidOperationException("Approved Sprite was not imported: " + name);

    private static void SetObject(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        if (owner == null || value == null) throw new InvalidOperationException("A required approved UI binding is missing: " + field);
        var serialized = new SerializedObject(owner);
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Required serialized display field is missing: " + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
}
#endif
