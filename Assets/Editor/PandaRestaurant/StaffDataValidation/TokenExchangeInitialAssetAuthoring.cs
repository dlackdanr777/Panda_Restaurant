using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Explicit, one-time authoring of the approved legacy appearance. Runtime never calls this builder.
/// Existing assets are returned unchanged; subsequent design edits belong to their Inspectors.
/// </summary>
public static class TokenExchangeInitialAssetAuthoring
{
    public const string PrefabPath = "Assets/Resources/UI/GachaCollection/TokenExchangeView.prefab";
    public const string EntryPrefabPath = "Assets/Resources/UI/GachaCollection/TokenExchangeEntryButton.prefab";
    private const string ScenePath = "Assets/Scenes/Stage1.unity";

    public static GameObject CreateViewPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;
        GachaCollectionUiTheme theme = RequireTheme();
        EnsureFolder("Assets/Resources/UI/GachaCollection");
        GameObject root = new GameObject("Token Exchange", typeof(RectTransform));
        root.SetActive(false);
        int layer = LayerMask.NameToLayer("UI");
        if (layer >= 0) root.layer = layer;
        try
        {
            var rect = (RectTransform)root.transform;
            E.Stretch(rect);
            var author = new AppearanceAuthor(root.transform, theme);
            author.BuildAndConnect();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Token Exchange prefab could not be saved.");
            return prefab;
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static GameObject CreateEntryPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
        if (existing != null) return existing;
        GachaCollectionUiTheme theme = RequireTheme();
        EnsureFolder("Assets/Resources/UI/GachaCollection");
        GameObject host = new GameObject("Entry Authoring Host", typeof(RectTransform));
        host.SetActive(false);
        int layer = LayerMask.NameToLayer("UI");
        if (layer >= 0) host.layer = layer;
        try
        {
            // Equivalent to the previous HUD-relative position: x = viewport left + 32, y = -105 -119.
            Image art = E.ExchangeTitle("Open Token Exchange", host.transform, theme, theme.Font,
                32, 224, 344, 118.4f);
            art.raycastTarget = true;
            art.gameObject.AddComponent<GachaButtonInputSound>();
            Button button = art.gameObject.AddComponent<Button>();
            button.targetGraphic = art;
            button.transition = Selectable.Transition.None;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(art.gameObject, EntryPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Token Exchange entry prefab could not be saved.");
            return prefab;
        }
        finally { Object.DestroyImmediate(host); }
    }

    // Deliberate batch entry point for initial delivery, never an InitializeOnLoad/import callback.
    public static void CreateAndConnectStage1()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stage1 initial wiring requires an explicit EditMode batch invocation.");
        string original = File.ReadAllText(ScenePath);
        Scene scene = default;
        string scratch = "Assets/__TokenExchangeAuthoring_" + Guid.NewGuid().ToString("N");
        try
        {
            GameObject viewAsset = CreateViewPrefab();
            GameObject entryAsset = CreateEntryPrefab();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var group = scene.GetRootGameObjects().Single(item => item.name == "Canvas Group");
            UIGacha gacha = group.transform.Find("Main Canvas/Gacha UI").GetComponent<UIGacha>();
            bool originalActive = gacha.gameObject.activeSelf;
            var serialized = new SerializedObject(gacha);
            SerializedProperty viewField = RequireProperty(serialized, "_collectionExchange");
            SerializedProperty buttonField = RequireProperty(serialized, "_collectionExchangeButton");
            if (viewField.objectReferenceValue != null || buttonField.objectReferenceValue != null)
            {
                if (viewField.objectReferenceValue == null || buttonField.objectReferenceValue == null)
                    throw new InvalidOperationException("Existing Stage1 exchange wiring is partial; preserve it for review.");
                Debug.Log("TOKEN_EXCHANGE_ASSET_AUTHORING: Existing Stage1 references preserved; no scene write.");
                return;
            }

            long gachaId = unchecked((long)GlobalObjectId.GetGlobalObjectIdSlow(gacha).targetObjectId);
            long transformId = unchecked((long)GlobalObjectId.GetGlobalObjectIdSlow(gacha.transform).targetObjectId);
            if (gachaId == 0 || transformId == 0)
                throw new InvalidOperationException("Could not identify original Stage1 object IDs.");

            GameObject buttonInstance = (GameObject)PrefabUtility.InstantiatePrefab(entryAsset, gacha.transform);
            GameObject viewInstance = (GameObject)PrefabUtility.InstantiatePrefab(viewAsset, gacha.transform);
            viewInstance.SetActive(false);
            viewField.objectReferenceValue = viewInstance.GetComponent<TokenExchangeView>();
            buttonField.objectReferenceValue = buttonInstance.GetComponent<Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (gacha.gameObject.activeSelf != originalActive)
                throw new InvalidOperationException("The source Gacha UI activeSelf changed unexpectedly.");

            EnsureFolder(scratch);
            string copyPath = scratch + "/Stage1.unity";
            if (!EditorSceneManager.SaveScene(scene, copyPath, true))
                throw new InvalidOperationException("Could not serialize the temporary Stage1 authoring copy.");
            string serializedCopy = File.ReadAllText(copyPath);
            string updated = MergeOnlyExchangeWiring(original, serializedCopy, gachaId, transformId);
            EditorSceneManager.CloseScene(scene, true);
            if (File.ReadAllText(ScenePath) != original)
                throw new InvalidOperationException("Stage1 changed externally during authoring; no scene write performed.");
            File.WriteAllText(ScenePath, updated, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            Debug.Log("TOKEN_EXCHANGE_ASSET_AUTHORING: Saved both prefabs and two Stage1 references; original objects preserved.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (AssetDatabase.IsValidFolder(scratch)) AssetDatabase.DeleteAsset(scratch);
        }
    }

    // Unity may normalize unrelated scene components when serializing. Preserve every original block
    // except the two explicit references and the parent's child list; append only newly created blocks.
    private static string MergeOnlyExchangeWiring(string original, string copy, long gachaId, long transformId)
    {
        var before = SceneBlocks(original);
        var after = SceneBlocks(copy);
        if (!before.ContainsKey(gachaId) || !before.ContainsKey(transformId) ||
            !after.ContainsKey(gachaId) || !after.ContainsKey(transformId))
            throw new InvalidOperationException("Stage1 serialization did not preserve existing object IDs.");
        string gacha = before[gachaId];
        foreach (string field in new[] { "_collectionExchange", "_collectionExchangeButton" })
        {
            Match newField = Regex.Match(after[gachaId], @"(?m)^  " + field + @": \{fileID: -?\d+\}\r?$" );
            if (!newField.Success || newField.Value.Contains("fileID: 0"))
                throw new InvalidOperationException("New Stage1 reference was not serialized: " + field);
            string pattern = @"(?m)^  " + field + @":.*(?:\r?\n|$)";
            if (Regex.IsMatch(gacha, pattern)) gacha = Regex.Replace(gacha, pattern, newField.Value.TrimEnd('\r') + "\n");
            else gacha = gacha.TrimEnd('\r', '\n') + "\n" + newField.Value.TrimEnd('\r') + "\n";
        }
        const string childrenPattern = @"(?m)^  m_Children:(?: \[\])?\r?\n(?:  - \{fileID: -?\d+\}\r?\n)*";
        Match oldChildren = Regex.Match(before[transformId], childrenPattern);
        Match newChildren = Regex.Match(after[transformId], childrenPattern);
        if (!oldChildren.Success || !newChildren.Success)
            throw new InvalidOperationException("Stage1 Gacha UI child lists could not be verified.");
        string[] oldIds = Regex.Matches(oldChildren.Value, @"fileID: (-?\d+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        string[] newIds = Regex.Matches(newChildren.Value, @"fileID: (-?\d+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        if (newIds.Length != oldIds.Length + 2 || !newIds.Take(oldIds.Length).SequenceEqual(oldIds))
            throw new InvalidOperationException("Stage1 original child order changed; refusing to replace it.");
        string transform = before[transformId].Replace(oldChildren.Value, newChildren.Value);
        string result = original.Replace(before[gachaId], gacha).Replace(before[transformId], transform);
        var added = after.Where(pair => !before.ContainsKey(pair.Key)).ToArray();
        var instances = added.Where(pair => pair.Value.StartsWith("--- !u!1001 ", StringComparison.Ordinal)).ToArray();
        string viewGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
        string entryGuid = AssetDatabase.AssetPathToGUID(EntryPrefabPath);
        if (instances.Length != 2 || !instances.Any(pair => pair.Value.Contains("guid: " + viewGuid)) ||
            !instances.Any(pair => pair.Value.Contains("guid: " + entryGuid)))
            throw new InvalidOperationException("Expected exactly the two new exchange prefab instances.");
        foreach (var block in added)
        {
            bool isExpectedInstance = instances.Any(instance => instance.Key == block.Key);
            bool isExpectedReference = block.Value.Split('\n')[0].Contains(" stripped") &&
                instances.Any(instance => block.Value.Contains("m_PrefabInstance: {fileID: " + instance.Key + "}"));
            if (!isExpectedInstance && !isExpectedReference)
                throw new InvalidOperationException("Unrelated new Stage1 serialization block; no write: " + block.Key);
            result = result.TrimEnd('\r', '\n') + "\n" + block.Value;
        }
        return result;
    }

    private static Dictionary<long, string> SceneBlocks(string value)
    {
        MatchCollection headers = Regex.Matches(value, @"(?m)^--- !u!\d+ &(-?\d+)(?: stripped)?\r?$" );
        var result = new Dictionary<long, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            int end = i + 1 < headers.Count ? headers[i + 1].Index : value.Length;
            result.Add(long.Parse(headers[i].Groups[1].Value), value.Substring(headers[i].Index, end - headers[i].Index));
        }
        return result;
    }

    private static GachaCollectionUiTheme RequireTheme()
    {
        var theme = GachaCollectionUiTheme.Load();
        if (theme == null || theme.Font == null || theme.SoldOutFont == null || theme.PandaToken == null)
            throw new InvalidOperationException("The original GachaCollectionUiTheme font/token references are required.");
        return theme;
    }

    private static SerializedProperty RequireProperty(SerializedObject value, string name)
        => value.FindProperty(name) ?? throw new InvalidOperationException("Missing serialized authoring field: " + name);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        if (separator > 0) EnsureFolder(path.Substring(0, separator));
        AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
    }

    private sealed class AppearanceAuthor
    {
        private readonly Transform transform;
        private readonly GachaCollectionUiTheme _theme;
        private readonly TMP_FontAsset _font;
        private RectTransform _board, _content, _balanceFrame, _priceRow, _priceTokenRow;
        private TextMeshProUGUI _balance, _name, _description, _quantity, _price, _buyLabel, _refreshLabel;
        private Image _priceToken, _art;
        private Button _closeButton, _buy, _refresh;
        private readonly List<ProductCard> _cards = new List<ProductCard>();

        private sealed class ProductCard
        {
            public Button Button;
            public RectTransform PriceGroup;
            public Image Frame, Artwork, Token;
            public Image[] Stars;
            public TextMeshProUGUI Name, Price, Availability;
            public RectTransform Stamp;
            public CanvasGroup StampGroup;
        }

        internal AppearanceAuthor(Transform root, GachaCollectionUiTheme theme)
        { transform = root; _theme = theme; _font = theme.Font; }

        internal void BuildAndConnect()
        {
            Build();
            var responsive = E.Rect("Responsive Layout", transform, 0, 0, 1650, 925);
            responsive.anchorMin = responsive.anchorMax = responsive.pivot = new Vector2(.5f, .5f);
            responsive.anchoredPosition = Vector2.zero;
            _board.SetParent(responsive, false);
            _board.anchoredPosition = new Vector2(-135, 0);
            _balanceFrame.SetParent(responsive, false);
            _balanceFrame.anchoredPosition = new Vector2(575, 262.5f);

            GridLayoutGroup grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(188, 188);
            grid.spacing = new Vector2(14, 77);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.SetLayoutHorizontal();
            grid.SetLayoutVertical();

            var fitter = transform.gameObject.AddComponent<TokenExchangeViewportFitter>();
            var fitterFields = new SerializedObject(fitter);
            RequireProperty(fitterFields, "_viewport").objectReferenceValue = transform;
            RequireProperty(fitterFields, "_content").objectReferenceValue = responsive;
            RequireProperty(fitterFields, "_viewportFraction").floatValue = .94f;
            fitterFields.ApplyModifiedPropertiesWithoutUndo();

            var view = transform.gameObject.AddComponent<TokenExchangeView>();
            var fields = new SerializedObject(view);
            Set(fields, "_theme", _theme);
            Set(fields, "_board", _board); Set(fields, "_content", _content); Set(fields, "_balanceFrame", _balanceFrame);
            Set(fields, "_priceRow", _priceRow); Set(fields, "_priceTokenRow", _priceTokenRow);
            Set(fields, "_balance", _balance); Set(fields, "_name", _name); Set(fields, "_description", _description);
            Set(fields, "_quantity", _quantity); Set(fields, "_price", _price); Set(fields, "_buyLabel", _buyLabel);
            Set(fields, "_refreshLabel", _refreshLabel); Set(fields, "_priceToken", _priceToken); Set(fields, "_art", _art);
            Set(fields, "_closeButton", _closeButton); Set(fields, "_buy", _buy); Set(fields, "_refresh", _refresh);
            var cards = RequireProperty(fields, "_cards");
            cards.arraySize = _cards.Count;
            for (int i = 0; i < _cards.Count; i++)
            {
                var source = _cards[i];
                var card = cards.GetArrayElementAtIndex(i);
                Set(card, "Button", source.Button); Set(card, "PriceGroup", source.PriceGroup);
                Set(card, "Frame", source.Frame); Set(card, "Artwork", source.Artwork); Set(card, "Token", source.Token);
                Set(card, "Name", source.Name); Set(card, "Price", source.Price); Set(card, "Availability", source.Availability);
                Set(card, "Stamp", source.Stamp); Set(card, "StampGroup", source.StampGroup);
                var stars = card.FindPropertyRelative("Stars");
                stars.arraySize = source.Stars.Length;
                for (int star = 0; star < source.Stars.Length; star++) stars.GetArrayElementAtIndex(star).objectReferenceValue = source.Stars[star];
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject value, string name, Object reference)
            => RequireProperty(value, name).objectReferenceValue = reference;
        private static void Set(SerializedProperty value, string name, Object reference)
            => value.FindPropertyRelative(name).objectReferenceValue = reference;

        // The remaining construction code is the pre-refactor approved appearance, copied once
        // into this Editor-only authoring path. It is never a runtime layout authority.
        private void Build()
        {
            var shade = E.Panel("Input Blocking Backdrop", transform, 0, 0, 0, 0, new Color(0, 0, 0, .78f), 0);
            E.Stretch(shade.rectTransform); shade.raycastTarget = true;
            _board = E.Rect("Wooden Exchange Board", transform, 0, 0, 1380, 925);
            _board.anchorMin = _board.anchorMax = _board.pivot = new Vector2(.5f, .5f);
            _board.anchoredPosition = Vector2.zero;
            // The chain's upper length extends offscreen. Every link retains the original art's aspect ratio.
            E.Icon("Left Hanging Chain", _board, _theme.LeftChain, 250, -390, 54, 536);
            E.Icon("Right Hanging Chain", _board, _theme.RightChain, 1110, -390, 54, 536);
            E.Icon("Original Exchange Wood Frame", _board, _theme.WoodFrame, 0, 125, 1380, 799.1f);
            E.ExchangeTitle("Original Wooden Title", _board, _theme, _font, 455, 73, 470, 162);
            _balanceFrame = E.Icon("External Panda Token Balance", transform, _theme.CurrencyPill, 0, 0, 250, 68.5558f).rectTransform;
            _balanceFrame.anchorMin = _balanceFrame.anchorMax = new Vector2(.5f, .5f);
            E.Icon("Balance Token", _balanceFrame, _theme.PandaToken, -5.8f, -7.35f, 80, 80);
            _balance = E.Label("Available Panda Tokens", _balanceFrame, _font, "0", 88.45f, 10.24f, 142.22f, 49.84f,
                38, new Color32(110, 56, 16, 255));
            _balance.fontStyle = FontStyles.Bold;
            _closeButton = E.ArtButton("Close Exchange", _board, _font, "", _theme.ExchangeClose, 1290, 151, 62, 62,
                _theme.Ink, null, SoundEffectType.ButtonExitSound);
    
            SlicePanel("Native Selected Product Frame", 108, 248, 470, 609);
            _art = E.Icon("Selected Product Image", _board, null, 215, 278, 258, 224);
            _name = E.Label("Selected Product Name", _board, _font, "", 138, 517, 410, 57, 33, _theme.Ink);
            SlicePanel("Native Description Frame", 141, 580, 404, 155);
            _description = E.Label("Selected Product Description", _board, _font, "", 156, 594, 374, 125, 24,
                _theme.Ink, TextAlignmentOptions.Center);
            _description.textWrappingMode = TextWrappingModes.Normal;
            _description.enableAutoSizing = false;
            _description.overflowMode = TextOverflowModes.Ellipsis;
            _quantity = E.Label("Quantity", _board, _font, "", 153, 742, 380, 29, 21, _theme.Ink);
            _priceRow = E.Rect("Selected Price Row", _board, 153, 776, 380, 38);
            var priceLayout = _priceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            priceLayout.spacing = 0;
            priceLayout.childAlignment = TextAnchor.MiddleCenter;
            priceLayout.childControlWidth = false;
            priceLayout.childControlHeight = false;
            priceLayout.childForceExpandWidth = false;
            priceLayout.childForceExpandHeight = false;
    
            _priceTokenRow = E.Rect("Token Price Row", _priceRow, 0, 0, 0, 40);
            _priceTokenRow.anchorMin = _priceTokenRow.anchorMax = _priceTokenRow.pivot = new Vector2(.5f, .5f);
            var tokenPriceLayout = _priceTokenRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tokenPriceLayout.spacing = 5;
            tokenPriceLayout.childAlignment = TextAnchor.MiddleCenter;
            tokenPriceLayout.childControlWidth = true;
            tokenPriceLayout.childControlHeight = false;
            tokenPriceLayout.childForceExpandWidth = false;
            tokenPriceLayout.childForceExpandHeight = false;
            var tokenPriceSize = _priceTokenRow.gameObject.AddComponent<ContentSizeFitter>();
            tokenPriceSize.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tokenPriceSize.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    
            _priceToken = E.Icon("Price Panda Token", _priceTokenRow, _theme.PandaToken, 0, 0, 40, 40);
            var tokenLayout = _priceToken.gameObject.AddComponent<LayoutElement>();
            tokenLayout.preferredWidth = tokenLayout.preferredHeight = 40;
            tokenLayout.flexibleWidth = tokenLayout.flexibleHeight = 0;
            _price = E.Label("Token Price", _priceTokenRow, _font, "", 0, 0, 173, 38, 29, _theme.Ink, TextAlignmentOptions.MidlineLeft);
            _price.enableAutoSizing = false;
            _price.textWrappingMode = TextWrappingModes.NoWrap;
            _buy = E.ArtButton("Exchange Selected Product", _board, _font, "교환하기", _theme.ExchangeBuy,
                219, 817, 248, 57, _theme.Ink, null);
            _buyLabel = _buy.GetComponentInChildren<TextMeshProUGUI>();
    
            _content = E.Rect("Product Grid", _board, 693, 275, 596, 524);
            for (int i = 0; i < 6; i++) _cards.Add(CreateCard(i));
            _refreshLabel = E.Label("Remaining Refreshes", _board, _font, "", 885, 848, 310, 40, 25,
                _theme.Cream, TextAlignmentOptions.Right);
            _refresh = E.ArtButton("Refresh Display", _board, _font, "", _theme.ExchangeRefresh,
                1213, 832, 72, 72, _theme.Ink, null);
        }
    
        private Image SlicePanel(string name, float x, float y, float width, float height, float pixelScale = 8)
        {
            // The theme references an unchanged native PNG copied with a local slice border. Creating
            // a sprite from another sprite's texture + rect would sample the wrong region when packed.
            Image image = E.Icon(name, _board, _theme.DetailFrame, x, y, width, height);
            image.type = Image.Type.Sliced; image.preserveAspect = false; image.pixelsPerUnitMultiplier = pixelScale;
            return image;
        }
        private ProductCard CreateCard(int index)
        {
            var frame = E.Icon("Product Slot " + index, _content, _theme.NormalProductFrame,
                (index % 3) * 202, (index / 3) * 265, 188, 188);
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<GachaButtonInputSound>();
            var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            GachaButtonInputSound.Bind(button);
            var priceGroup = E.Rect("Price Group", frame.transform, 0, 0, 0, 40);
            priceGroup.anchorMin = priceGroup.anchorMax = priceGroup.pivot = new Vector2(.5f, .5f);
            priceGroup.anchoredPosition = new Vector2(0, -114.55f);
            var priceLayout = priceGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
            priceLayout.spacing = 5;
            priceLayout.childAlignment = TextAnchor.MiddleCenter;
            priceLayout.childControlWidth = true;
            priceLayout.childControlHeight = false;
            priceLayout.childForceExpandWidth = false;
            priceLayout.childForceExpandHeight = false;
            var priceSize = priceGroup.gameObject.AddComponent<ContentSizeFitter>();
            priceSize.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            priceSize.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    
            var token = E.Icon("Panda Token Price", priceGroup, _theme.PandaToken, 0, 0, 30, 30);
            var tokenLayout = token.gameObject.AddComponent<LayoutElement>();
            tokenLayout.preferredWidth = tokenLayout.preferredHeight = 30;
            tokenLayout.flexibleWidth = tokenLayout.flexibleHeight = 0;
            var card = new ProductCard
            {
                Button = button, Frame = frame, PriceGroup = priceGroup,
                Artwork = E.Icon("Artwork", frame.transform, null, 40, 35, 105, 90),
                Name = E.Label("Product Name", frame.transform, _font, "", 34, 144, 120, 24, 20, _theme.Ink),
                Price = E.Label("Price", priceGroup, _font, "", 0, 0, 0, 31, 20, _theme.Cream, TextAlignmentOptions.Left),
                Token = token,
                Availability = E.Label("Availability", priceGroup, _font, "", 0, 0, 0, 21, 14,
                    _theme.Cream, TextAlignmentOptions.Right),
                Stars = new Image[5]
            };
            RectTransform artworkRect = card.Artwork.rectTransform;
            artworkRect.anchorMin = artworkRect.anchorMax = artworkRect.pivot = new Vector2(.5f, .5f);
            artworkRect.anchoredPosition = new Vector2(-1.5f, 11.7f);
            artworkRect.sizeDelta = new Vector2(120, 100);
    
            RectTransform nameRect = card.Name.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(.5f, .5f);
            nameRect.pivot = new Vector2(.5f, .5f);
            nameRect.anchoredPosition = new Vector2(0, -58.2f);
            nameRect.sizeDelta = new Vector2(120, 24);
            for (int i = 0; i < card.Stars.Length; i++)
            {
                card.Stars[i] = E.Icon("Rank Star " + (i + 1), frame.transform, _theme.Star, 44 + i * 20, 122, 20, 20);
                RectTransform starRect = card.Stars[i].rectTransform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(.5f, .5f);
                starRect.pivot = new Vector2(.5f, 1f);
                starRect.anchoredPosition = new Vector2(-40 + i * 20, -28);
                starRect.sizeDelta = new Vector2(20, 20);
            }
            card.Name.textWrappingMode = TextWrappingModes.NoWrap;
            card.Name.overflowMode = TextOverflowModes.Ellipsis;
            card.Price.fontSize = 20;
            card.Price.fontSizeMin = 14;
            card.Price.fontSizeMax = 20;
            card.Price.enableAutoSizing = true;
            card.Price.textWrappingMode = TextWrappingModes.NoWrap;
            card.Price.overflowMode = TextOverflowModes.Ellipsis;
            card.Stamp = E.Rect("SOLD OUT Stamp", frame.transform, 94, 92, 170, 56);
            card.Stamp.localRotation = Quaternion.Euler(0, 0, 12);
            card.StampGroup = card.Stamp.gameObject.AddComponent<CanvasGroup>();
            card.StampGroup.blocksRaycasts = false; card.StampGroup.interactable = false;
            var stampInk = new Color32(186, 42, 32, 255);
            E.Panel("Stamp Paper", card.Stamp, 0, 0, 170, 56, new Color(1,1,1,.9f), 3);
            E.Panel("Top Ink", card.Stamp, 0, 0, 170, 4, stampInk, 0);
            E.Panel("Bottom Ink", card.Stamp, 0, 52, 170, 4, stampInk, 0);
            E.Panel("Left Ink", card.Stamp, 0, 0, 4, 56, stampInk, 0);
            E.Panel("Right Ink", card.Stamp, 166, 0, 4, 56, stampInk, 0);
            E.Label("SOLD OUT", card.Stamp, _theme.SoldOutFont, "SOLD OUT", 6, 5, 158, 46, 32, stampInk);
            card.Stamp.gameObject.SetActive(false);
            return card;
        }
    }
    private static class E
    {
        internal static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        internal static GachaRoundedPanel Panel(string name, Transform parent, float x, float y, float width,
            float height, Color color, float radius = 18f)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var panel = rect.gameObject.AddComponent<GachaRoundedPanel>();
            panel.Radius = radius; panel.color = color; panel.raycastTarget = false;
            return panel;
        }
        internal static Image Icon(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }
        internal static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string value,
            float x, float y, float width, float height, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(14, size * .62f); text.enableAutoSizing = true;
            text.color = color; text.alignment = align; text.raycastTarget = false;
            return text;
        }
        internal static Button Button(string name, Transform parent, TMP_FontAsset font, string label,
            float x, float y, float width, float height, Color fill, Color ink, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(name, parent, x, y, width, height, fill);
            panel.raycastTarget = true;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = ink; outline.effectDistance = new Vector2(2, -3);
            panel.gameObject.AddComponent<GachaButtonInputSound>(); // Handle submit before Button changes/closes the view.
            var button = panel.gameObject.AddComponent<Button>();
            GachaButtonInputSound.Bind(button);
            button.targetGraphic = panel;
            var colors = button.colors; colors.disabledColor = new Color(.64f,.64f,.64f,1); button.colors = colors;
            Label("Label", panel.transform, font, label, 8, 3, width - 16, height - 6, 28, ink);
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
        internal static TMP_FontAsset FontFrom(Transform parent, GachaCollectionUiTheme theme)
        {
            TMP_Text text = parent.GetComponentInChildren<TMP_Text>(true);
            return text != null && text.font != null ? text.font : theme.Font;
        }
        internal static Button ArtButton(string name, Transform parent, TMP_FontAsset font, string label, Sprite sprite,
            float x, float y, float width, float height, Color ink, UnityEngine.Events.UnityAction action,
            SoundEffectType sound = SoundEffectType.ButtonClickSound)
        {
            var graphic = Icon(name, parent, sprite, x, y, width, height);
            // Image.preserveAspect aligns inside its rect using the pivot. Keep the same outer
            // bounds but center the artwork, so a wide hit area and its caption share a center.
            graphic.rectTransform.pivot = new Vector2(.5f, .5f);
            graphic.rectTransform.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
            graphic.raycastTarget = true;
            graphic.gameObject.AddComponent<GachaButtonInputSound>();
            var button = graphic.gameObject.AddComponent<Button>();
            GachaButtonInputSound.Bind(button, sound);
            button.targetGraphic = graphic;
            Label("Label", graphic.transform, font, label, 5, 3, width - 10, height - 6, 25, ink);
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
    
        internal static Image ExchangeTitle(string name, Transform parent, GachaCollectionUiTheme theme,
            TMP_FontAsset font, float x, float y, float width, float height)
        {
            var image = Icon(name, parent, theme.ExchangeTitle, x, y, width, height);
            image.preserveAspect = false;
            // Replace the baked ticket lettering on the original flat wood interior only.
            var cover = Panel("Title Lettering Ground", image.transform, 0, 0, 0, 0,
                new Color32(201, 117, 56, 255), 0).rectTransform;
            cover.anchorMin = new Vector2(80f / 470, 1 - 124f / 162);
            cover.anchorMax = new Vector2(390f / 470, 1 - 42f / 162);
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            var label = Label("Title", image.transform, font, "토큰교환소", 0, 0, 0, 0,
                height * .31f, Color.white);
            label.rectTransform.anchorMin = new Vector2(.17f, .23f);
            label.rectTransform.anchorMax = new Vector2(.83f, .76f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return image;
        }
    }
    
}
