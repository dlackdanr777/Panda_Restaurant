#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit asset authoring only. Never runs on import or edits the original Scene/popup prefab.</summary>
internal static class PopupStorePhasePreviewViewBuilder
{
    internal const string Folder = "Assets/Editor/PandaRestaurant/StaffDataValidation/UI";
    internal const string PrefabPath = Folder + "/PopupStorePhasePreviewView.prefab";
    internal const string GameplayPath = Folder + "/PopupStoreTutorialGameplayV4.png";
    internal const string PhasePath = Folder + "/PopupStoreTutorialPhase.png";
    internal const string TicketPath = "Assets/Sprites/PopupStore/Tickets/PopupStore_MiniGameTicket.png";
    private const string SpriteRoot = "Assets/Sprites/";

    public static GameObject Build(string gameplayAssetPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("The preview view can be authored only while the original Editor is idle.");
        if (!string.Equals(gameplayAssetPath?.Replace('\\', '/'), GameplayPath, StringComparison.Ordinal))
            throw new ArgumentException("Use the current original-prefab gameplay capture at " + GameplayPath, nameof(gameplayAssetPath));
        ImportScreenshot(gameplayAssetPath);
        var gameplay = Require<Sprite>(gameplayAssetPath);
        var frame = Require<Sprite>(SpriteRoot + "UI/UIMiniGame/UI_튜토리얼_미니게임_프레임.png");
        var arrow = Require<Sprite>(SpriteRoot + "UI/UI공통 이미지/UI화살표.png");
        var playSprite = Require<Sprite>(SpriteRoot + "Tutorial/MiniGameTutorial/UI_튜토리얼_미니게임_플레이 버튼.png");
        var closeSprite = Require<Sprite>(SpriteRoot + "UI/UIMiniGame/UI_버튼_나가기.png");
        var ticket = Require<Sprite>(TicketPath);
        var bodyFont = Require<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("0cbc05084ee1e444389450df4c0062e1"));
        var titleFont = Require<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("a5ffeaee8dff19d4a8b8376eacd85a4b"));
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null && existing.GetComponent<PopupStorePhasePreviewView>() == null)
            throw new InvalidOperationException("Refusing to overwrite an unrelated asset at " + PrefabPath);

        GameObject root = null;
        try
        {
            root = new GameObject("PopupStore Phase Preview View", typeof(RectTransform), typeof(CanvasGroup), typeof(PopupStorePhasePreviewView));
            root.layer = 5;
            root.AddComponent<PopupStorePhasePreviewPolish>();
            Stretch((RectTransform)root.transform);
            var input = root.GetComponent<CanvasGroup>();
            input.ignoreParentGroups = true; input.alpha = 1; input.interactable = false; input.blocksRaycasts = false;
            var view = root.GetComponent<PopupStorePhasePreviewView>();
            var serialized = new SerializedObject(view);
            Set(serialized, "_inputGroup", input);

            var tutorial = Group("Tutorial", root.transform);
            Shade(tutorial, .65f);
            var paper = Picture("Recipe Tutorial Frame", tutorial, frame, Vector2.zero, new Vector2(1600, 878.35f));
            paper.raycastTarget = false;
            var title = Text("Page Title", tutorial, titleFont, new Vector2(0, 347), new Vector2(1270, 95), 59, Color.white);
            var pageNumber = Text("Page Number", tutorial, bodyFont, new Vector2(0, 287), new Vector2(200, 42), 29, new Color(1, .93f, .79f));
            var body = Text("Page Description", tutorial, bodyFont, new Vector2(0, -225), new Vector2(1250, 125), 40, new Color(.26f, .13f, .04f));
            var screenshotRegion = Region("Gameplay Crop", tutorial, new Vector2(0, 35), new Vector2(1140, 380));
            var screenshot = Screenshot("Current Gameplay Capture", screenshotRegion, out var aspect);
            var secondRegion = Region("Phase Material Crop", tutorial, new Vector2(0, -20), new Vector2(1140, 265));
            var second = Screenshot("Actual Phase GameView", secondRegion, out var secondAspect);
            var previous = Button("Previous Page", tutorial, arrow, new Vector2(-722, -25), new Vector2(90, 93));
            previous.transform.localScale = new Vector3(-1, 1, 1);
            var next = Button("Next Page", tutorial, arrow, new Vector2(722, -25), new Vector2(90, 93));
            var close = Button("Close Tutorial", tutorial, closeSprite, new Vector2(711, 351), new Vector2(85, 85));
            var play = Button("Play", tutorial, playSprite, new Vector2(265, -300), new Vector2(300, 103));
            Text("Play Label", play.transform, bodyFont, Vector2.zero, new Vector2(280, 90), 51, new Color(.28f, .13f, .015f)).text = "플레이";
            var ticketImage = Picture("Popup Store Minigame Ticket", tutorial, ticket, new Vector2(-310, -300), new Vector2(170, 100));
            var ticketStatus = Text("Ticket Status", tutorial, bodyFont, new Vector2(-80, -300), new Vector2(230, 60), 31, new Color(.36f, .18f, .05f));
            ticketStatus.text = "이용권 —/—";

            Set(serialized, "_tutorialGroup", tutorial.gameObject);
            Set(serialized, "_titleText", title); Set(serialized, "_bodyText", body); Set(serialized, "_pageText", pageNumber);
            Set(serialized, "_screenshot", screenshot); Set(serialized, "_secondScreenshot", second);
            Set(serialized, "_screenshotAspect", aspect); Set(serialized, "_secondScreenshotAspect", secondAspect);
            Set(serialized, "_screenshotRegion", screenshotRegion); Set(serialized, "_secondScreenshotRegion", secondRegion);
            Set(serialized, "_previousButton", previous); Set(serialized, "_nextButton", next);
            Set(serialized, "_playButton", play); Set(serialized, "_closeButton", close);
            Set(serialized, "_ticketImage", ticketImage); Set(serialized, "_ticketStatusText", ticketStatus);
            serialized.FindProperty("_ticketPosition").vector2Value = ticketImage.rectTransform.anchoredPosition;
            serialized.FindProperty("_ticketSize").vector2Value = ticketImage.rectTransform.sizeDelta;
            serialized.FindProperty("_ticketStatusPosition").vector2Value = ticketStatus.rectTransform.anchoredPosition;
            serialized.FindProperty("_ticketStatusSize").vector2Value = ticketStatus.rectTransform.sizeDelta;
            serialized.FindProperty("_playPosition").vector2Value = play.GetComponent<RectTransform>().anchoredPosition;
            serialized.FindProperty("_playSize").vector2Value = play.GetComponent<RectTransform>().sizeDelta;

            var pages = serialized.FindProperty("_pages"); pages.arraySize = 5;
            Page(pages.GetArrayElementAtIndex(0), gameplay, "주문을 보고 포장해요",
                "손님이 원하는 조합을 확인해요",
                new Rect(.125f, .44f, .27f, .40f));
            Page(pages.GetArrayElementAtIndex(1), gameplay, "순서는 상관없어요",
                "순서와 상관없이 재료를 담으면 성공!", new Rect(0, 0, 1, 1), false);
            Page(pages.GetArrayElementAtIndex(2), gameplay, "손님에게 전달해요",
                "포장하기로 손님에게 물건을 전달하세요!\n잘못된 포장은 판매대 비우기로 다시 시작해 보아요",
                new Rect(.23f, .245f, .55f, .445f));
            Page(pages.GetArrayElementAtIndex(3), gameplay, "스테이지를 통과해요",
                "스테이지를 통과해서 다양한 보상을 노려봅시다!", new Rect(0, 0, 1, 1), false);
            Page(pages.GetArrayElementAtIndex(4), gameplay, "더 높은 단계로 갈수록 커져요",
                "높은 스테이지를 통과할수록\n더 좋은 상자와 더 많은 다이아를 얻을 수 있어요!", new Rect(0, 0, 1, 1), false);
            pages.GetArrayElementAtIndex(2).FindPropertyRelative("BodyFontSize").floatValue = 36;
            pages.GetArrayElementAtIndex(4).FindPropertyRelative("BodyFontSize").floatValue = 36;
            float[] visualOffsets = { -75, -90, -65, -70, -40 };
            float[] bodyPositions = { -280, -280, -295, -310, -142 };
            for (int i = 0; i < pages.arraySize; i++)
            {
                pages.GetArrayElementAtIndex(i).FindPropertyRelative("VisualOffset").vector2Value = new Vector2(0, visualOffsets[i]);
                pages.GetArrayElementAtIndex(i).FindPropertyRelative("BodyPosition").vector2Value = new Vector2(0, bodyPositions[i]);
            }
            var trayExample = BuildTrayExample(tutorial);
            var deliveryExample = BuildDeliveryExample(tutorial);
            var prizeExample = BuildPrizeExample(tutorial, bodyFont);
            var growthExample = BuildGrowthExample(tutorial, bodyFont);
            pages.GetArrayElementAtIndex(1).FindPropertyRelative("Illustration").objectReferenceValue = trayExample.gameObject;
            pages.GetArrayElementAtIndex(2).FindPropertyRelative("Illustration").objectReferenceValue = deliveryExample.gameObject;
            pages.GetArrayElementAtIndex(3).FindPropertyRelative("Illustration").objectReferenceValue = prizeExample.gameObject;
            pages.GetArrayElementAtIndex(4).FindPropertyRelative("Illustration").objectReferenceValue = growthExample.gameObject;
            foreach (var illustration in new[] { trayExample, deliveryExample, prizeExample, growthExample }) illustration.gameObject.SetActive(false);

            var presentation = Group("Stage Presentation", root.transform);
            Shade(presentation, .42f);
            var cue = Picture("Recipe Cue", presentation, null, Vector2.zero, new Vector2(1250, 310));
            var stage = Text("Stage Number", presentation, titleFont, Vector2.zero, new Vector2(1400, 250), 140, new Color(1, .90f, .73f));
            stage.text = "STAGE 1";
            Set(serialized, "_presentationGroup", presentation.gameObject);
            Set(serialized, "_cueImage", cue); Set(serialized, "_stageText", stage);
            Set(serialized, "_three", Require<Sprite>(SpriteRoot + "MiniGame/MiniGame1/텔레그라피_3.png"));
            Set(serialized, "_two", Require<Sprite>(SpriteRoot + "MiniGame/MiniGame1/텔레그라피_2.png"));
            Set(serialized, "_one", Require<Sprite>(SpriteRoot + "MiniGame/MiniGame1/텔레그라피_1.png"));
            Set(serialized, "_gameStart", Require<Sprite>(SpriteRoot + "MiniGame/텔레그라피_game start.png"));
            Set(serialized, "_success", Require<Sprite>(SpriteRoot + "MiniGame/텔레그라피_success.png"));
            Set(serialized, "_gameOver", Require<Sprite>(SpriteRoot + "MiniGame/텔레그라피_game over.png"));
            serialized.FindProperty("_stageNoticeSeconds").floatValue = 1.5f;
            serialized.FindProperty("_gameStartSeconds").floatValue = .7f;
            serialized.FindProperty("_gameOverSeconds").floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.HideAll();
            if (!view.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
            EnsureFolder(Folder);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || saved == null) throw new InvalidOperationException("Could not save " + PrefabPath);
            return saved;
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    // The new Editor-only source capture is the only import this authoring tool may change.
    private static void ImportScreenshot(string path)
    {
        if (path != GameplayPath)
            throw new ArgumentException("Only the new isolated tutorial capture may be imported.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("Current popup gameplay capture is missing.", path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Capture is not a texture: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static T Require<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new FileNotFoundException("Required original preview asset is unavailable: " + path, path);
        return asset;
    }
    private static void Page(SerializedProperty page, Sprite sprite, string title, string body, Rect crop,
        bool show = true, bool second = false, Rect secondCrop = default)
    {
        page.FindPropertyRelative("Screenshot").objectReferenceValue = sprite;
        page.FindPropertyRelative("Title").stringValue = title;
        page.FindPropertyRelative("Body").stringValue = body;
        page.FindPropertyRelative("ShowScreenshot").boolValue = show;
        page.FindPropertyRelative("Crop").rectValue = crop;
        page.FindPropertyRelative("ShowSecondCrop").boolValue = second;
        page.FindPropertyRelative("SecondCrop").rectValue = second ? secondCrop : new Rect(0, 0, 1, 1);
        page.FindPropertyRelative("ScreenshotPosition").vector2Value = new Vector2(0, 35);
        page.FindPropertyRelative("ScreenshotSize").vector2Value = new Vector2(1140, 380);
        page.FindPropertyRelative("BodyPosition").vector2Value = new Vector2(0, -225);
        page.FindPropertyRelative("BodyFontSize").floatValue = 40;
        page.FindPropertyRelative("Illustration").objectReferenceValue = null;
        page.FindPropertyRelative("VisualOffset").vector2Value = Vector2.zero;
    }
    private static RectTransform BuildTrayExample(Transform tutorial)
    {
        var root = Group("Unordered Tray Example", tutorial);
        Picture("Original Five Slot Tray", root, Require<Sprite>(SpriteRoot + "PopupStore/FinalGuideV3/FG_Tray_FiveEmptySlots.png"),
            new Vector2(0, 55), new Vector2(1180, 224));
        var catalog = Require<PopupStoreMaterialCatalog>("Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset");
        string[] ids = { "BOX_PINK", "RIBBON_YELLOW", "STAFF01" };
        var kinds = new[] { PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon, PopupStoreMaterialKind.Plush };
        for (int i = 0; i < ids.Length; i++)
        {
            if (!catalog.TryGetMaterials(kinds[i], out var entries, out var error)) throw new InvalidOperationException(error);
            Sprite sprite = null;
            foreach (var entry in entries) if (entry.Id == ids[i]) sprite = entry.Sprite;
            if (sprite == null) throw new InvalidOperationException("Missing unchanged catalog material " + ids[i]);
            Picture("Selected " + ids[i], root, sprite, new Vector2(-417 + i * 209, 62), new Vector2(174, 174));
        }
        return root;
    }
    private static RectTransform BuildDeliveryExample(Transform tutorial)
    {
        var root = Group("Delivery Satisfaction Example", tutorial);
        Picture("Original Customer Joy", root, Require<Sprite>(SpriteRoot + "Customer/감정표현/신남.png"),
            new Vector2(112, 152), new Vector2(136, 136));
        return root;
    }
    private static RectTransform BuildPrizeExample(Transform tutorial, TMP_FontAsset font)
    {
        var root = Group("Tutorial SSS Reward Example", tutorial);
        var diamond = Require<Sprite>(SpriteRoot + "UI/UI공통 이미지/다이아.png");
        Vector2[] points = { new Vector2(-330, 140), new Vector2(-445, 65), new Vector2(-285, -75),
            new Vector2(-415, -105), new Vector2(325, 140), new Vector2(450, 50), new Vector2(290, -78),
            new Vector2(425, -104), new Vector2(-185, 160), new Vector2(180, 160) };
        for (int i = 0; i < points.Length; i++)
        {
            float size = i % 3 == 0 ? 105 : 78;
            var image = Picture("Example Diamond " + (i + 1), root, diamond, points[i], new Vector2(size, size));
            image.rectTransform.localEulerAngles = new Vector3(0, 0, (i % 2 == 0 ? -1 : 1) * (12 + i * 2));
        }
        Picture("Approved SSS Box Example", root, RewardBox("SSS"), new Vector2(0, 50), new Vector2(400, 400));
        Text("Tutorial Example Disclaimer", root, font, new Vector2(0, -173), new Vector2(600, 40), 27,
            new Color(.47f, .29f, .15f)).text = "튜토리얼 예시";
        return root;
    }
    private static RectTransform BuildGrowthExample(Transform tutorial, TMP_FontAsset font)
    {
        var root = Group("Six Approved Reward Grades", tutorial);
        string[] grades = { "C", "B", "A", "S", "SS", "SSS" };
        float[] centers = { -550, -360, -155, 65, 300, 550 };
        var arrow = Require<Sprite>(SpriteRoot + "UI/UI공통 이미지/UI화살표.png");
        for (int i = 0; i < grades.Length; i++)
        {
            float size = 120 + i * 20;
            Picture("Approved " + grades[i] + " Growth", root, RewardBox(grades[i]), new Vector2(centers[i], 115), new Vector2(size, size));
            Text("Grade " + grades[i], root, font, new Vector2(centers[i], -17), new Vector2(120, 42), 31,
                new Color(.37f, .18f, .05f)).text = grades[i];
            if (i < grades.Length - 1) Picture("Growth Arrow " + (i + 1), root, arrow,
                new Vector2((centers[i] + centers[i + 1]) * .5f, 115), new Vector2(31, 33));
        }
        return root;
    }
    private static Sprite RewardBox(string grade)
        => Require<Sprite>(SpriteRoot + "PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png");
    private static void Set(SerializedObject target, string property, Object value)
        => target.FindProperty(property).objectReferenceValue = value;
    private static RectTransform Group(string name, Transform parent)
    {
        var rect = Region(name, parent, Vector2.zero, Vector2.zero); Stretch(rect); return rect;
    }
    private static RectTransform Region(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Shade(RectTransform parent, float alpha)
    {
        var image = Group("Input Blocker", parent).gameObject.AddComponent<Image>();
        image.color = new Color(.09f, .045f, .02f, alpha); image.raycastTarget = true;
    }
    private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Region(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
    }
    private static Button Button(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Picture(name, parent, sprite, position, size); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(.9f, .9f, .9f);
        colors.disabledColor = new Color(.7f, .7f, .7f, .7f); button.colors = colors;
        return button;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, Vector2 position,
        Vector2 size, float fontSize, Color color)
    {
        var text = Region(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false; return text;
    }
    private static RawImage Screenshot(string name, RectTransform parent, out AspectRatioFitter aspect)
    {
        var image = Region(name, parent, Vector2.zero, parent.sizeDelta).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false; aspect = image.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 16f / 9;
        return image;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
