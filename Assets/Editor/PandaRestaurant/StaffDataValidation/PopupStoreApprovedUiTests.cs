using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Approved source integrity and real prefab layout. Memory-only instances; no Scene, account, or GameView settings are saved.</summary>
public sealed class PopupStoreApprovedUiTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string SpriteFolder = "Assets/Sprites/PopupStore/UIApproved_v1_20261104/";
    private const string ApprovalPath = "Assets/Editor/PandaRestaurant/StaffDataValidation/PopupStoreUIApproved_v1_20261104/approval_manifest.json";
    private const string ApprovalSha = "c1dfdfe4e18b14a378657d46d71ef512694bdb79d96436cea4b06d4f65c7489c";

    [Serializable] private sealed class Approval { public bool art_approved; public int sprite_count; public ApprovedSprite[] assets; }
    [Serializable] private sealed class ApprovedSprite { public string file; public string sha256; public int[] pixels_wh; public string art_status; }

    [Test]
    public void AllThirtyApprovedSourcesRetainPinnedBytesRgbaSizeFourPixelPaddingAndSafeSpriteImport()
    {
        Assert.That(Hash(ApprovalPath), Is.EqualTo(ApprovalSha), "Approval record must remain the user-provided package record.");
        var approval = JsonUtility.FromJson<Approval>(File.ReadAllText(ApprovalPath));
        Assert.That(approval.art_approved, Is.True);
        Assert.That(approval.sprite_count, Is.EqualTo(30));
        Assert.That(approval.assets, Has.Length.EqualTo(30));
        CollectionAssert.AllItemsAreUnique(approval.assets.Select(a => a.file).ToArray());
        CollectionAssert.AreEquivalent(approval.assets.Select(a => Path.GetFileName(a.file)).ToArray(),
            Directory.GetFiles(SpriteFolder, "*.png").Select(Path.GetFileName).ToArray());
        foreach (var source in approval.assets)
        {
            string path = SpriteFolder + Path.GetFileName(source.file);
            Assert.That(source.art_status, Is.EqualTo("USER_APPROVED"), path);
            Assert.That(Hash(path), Is.EqualTo(source.sha256), path);
            byte[] bytes = File.ReadAllBytes(path);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray(), path);
            Assert.That(bytes[24], Is.EqualTo(8), path + " bit depth");
            Assert.That(bytes[25], Is.EqualTo(6), path + " RGBA colour type");
            Texture2D decoded = null;
            try
            {
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.That(ImageConversion.LoadImage(decoded, bytes, false), Is.True, path);
                Assert.That(decoded.width, Is.EqualTo(source.pixels_wh[0]), path);
                Assert.That(decoded.height, Is.EqualTo(source.pixels_wh[1]), path);
                var pixels = decoded.GetPixels32();
                Assert.That(pixels.Any(p => p.a == 255), Is.True, path + " visible artwork");
                for (int y = 0; y < decoded.height; y++)
                    for (int x = 0; x < decoded.width; x++)
                        if (x < 4 || y < 4 || x >= decoded.width - 4 || y >= decoded.height - 4)
                            Assert.That(pixels[y * decoded.width + x].a, Is.Zero, path + " transparent 4px margin");
            }
            finally { if (decoded != null) Object.DestroyImmediate(decoded); }
            Assert.That(File.Exists(path + ".meta"), Is.True, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid.Length, Is.EqualTo(32), path);
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(source.pixels_wh.Max()), path + " no source downsampling");
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count(), Is.EqualTo(1), path);
        }
    }

    [Test]
    public void ProtectedCloseRewardCatalogAndFontReferencesRemainOriginalAndNoPauseOrFakePagesAppear()
    {
        var prefab = Prefab();
        var close = Read<Button>(prefab.GetComponent<UIPopupStore>(), "_closeButton");
        Assert.That(GuidOf(((Image)close.targetGraphic).sprite), Is.EqualTo("8e684365c2478c0448d931e218907c21"));
        Assert.That(close.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(Read<Button>(prefab.GetComponent<PopupStorePauseController>(), "_pauseButton").gameObject.activeSelf, Is.False);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreRewardBox>(true), Has.Length.EqualTo(1));
        var resultBox = prefab.GetComponent<PopupStoreRewardBox>();
        string[] tiers = { "c", "b", "a", "s", "ss", "sss" };
        string[] guids = { "c158748c625d4d3fa935bc5b4b714602", "4e8ea94728034b3eaee381ade1dd0b83", "f7e2866e9f774145a2e5b33fba005825", "8cfcd6e3349a4b35973f5a6f922c8814", "6e2191b14bba47c2956a4086dadfb6f8", "20460be2050d4c6998c6c9206fff979b" };
        for (int i = 0; i < tiers.Length; i++) Assert.That(GuidOf(Read<Sprite>(resultBox, "_" + tiers[i] + "Sprite")), Is.EqualTo(guids[i]));
        foreach (var list in prefab.GetComponentsInChildren<PopupStoreMaterialList>(true))
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Read<PopupStoreMaterialCatalog>(list, "_catalog"))), Is.EqualTo("4ce68881c032419982169270f91601da"));
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(GuidOf(text.font), Is.EqualTo("0cbc05084ee1e444389450df4c0062e1"), text.name);
        foreach (var image in prefab.GetComponentsInChildren<Image>(true))
        {
            string path = AssetDatabase.GetAssetPath(image.sprite);
            Assert.That(path, Does.Not.Contain("UI_PageDot_"), "No page system exists; static dots must not misrepresent 32 materials.");
            Assert.That(path, Does.Not.Contain("UI_Button_Clear_Blank"));
            Assert.That(path, Does.Not.Contain("UI_Button_Pack_Blank"));
        }
    }

    [Test]
    public void ApprovedDecorationsDoNotStealMaterialScrollOrButtonRaycasts()
    {
        var prefab = Prefab();
        var decorations = prefab.GetComponentsInChildren<Image>(true).Where(i =>
            AssetDatabase.GetAssetPath(i.sprite).StartsWith(SpriteFolder, StringComparison.Ordinal)
            || AssetDatabase.GetAssetPath(i.sprite).StartsWith("Assets/Sprites/PopupStore/FinalGuideV3/", StringComparison.Ordinal)
            || AssetDatabase.GetAssetPath(i.sprite).StartsWith(PopupStoreCleanPartsTests.Art, StringComparison.Ordinal)).ToArray();
        Assert.That(decorations.Length, Is.GreaterThan(20), "The new skin must not turn this raycast check into an empty pass.");
        foreach (var image in decorations)
        {
            var button = image.GetComponent<Button>();
            Assert.That(image.raycastTarget, Is.EqualTo(button != null), image.name + " only an actual button may intercept input");
            if (button != null)
            {
                Assert.That(button.targetGraphic, Is.SameAs(image));
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero, "Runtime owner keeps exactly one callback binding.");
                Assert.That(image.preserveAspect, Is.True, image.name);
            }
        }
        foreach (var list in prefab.GetComponentsInChildren<PopupStoreMaterialList>(true))
        {
            var scroll = Read<ScrollRect>(list, "_scrollRect");
            Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>().enabled, Is.True);
            Assert.That(Read<Image>(list, "_itemTemplate").gameObject.activeSelf, Is.False);
            Assert.That(Read<Button>(list, "_previousButton").transform.IsChildOf(list.transform), Is.True);
            Assert.That(Read<Button>(list, "_nextButton").transform.IsChildOf(list.transform), Is.True);
        }
    }

    [Test]
    public void RealGradeAndPatienceFillMeshesAreEmptyAtZeroAndStayInsideTheirTracksAtHalfAndFull()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var fills = new List<Image> { Read<Image>(host.Root.GetComponent<PopupStoreGradeSession>(), "_progressImage") };
            fills.AddRange(Read<PopupStorePatienceSlot[]>(host.Root.GetComponent<PopupStoreCustomerPatience>(), "_slots").Select(slot => slot.Fill));
            Assert.That(fills, Has.Count.EqualTo(4));
            var populate = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            Assert.That(populate, Is.Not.Null);
            foreach (var fill in fills)
            {
                Assert.That(fill.sprite, Is.Not.Null, fill.name);
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled), fill.name);
                Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal), fill.name);
                Assert.That(fill.fillOrigin, Is.Zero, fill.name);
                var track = fill.transform.parent as RectTransform;
                Assert.That(track, Is.Not.Null);
                AssertInside(Bounds(fill.rectTransform, track), track.rect, fill.name + " stays within track");
                Rect full;
                using (var vertices = new VertexHelper())
                {
                    fill.fillAmount = 0; populate.Invoke(fill, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.Zero, fill.name + " must leave no coloured geometry at zero");
                    fill.fillAmount = 1; populate.Invoke(fill, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.GreaterThanOrEqualTo(4), fill.name);
                    full = MeshBounds(vertices);
                    AssertInside(full, fill.rectTransform.rect, fill.name + " full fill");
                    Assert.That(full.width, Is.GreaterThan(0));
                    fill.fillAmount = .5f; populate.Invoke(fill, new object[] { vertices });
                    var half = MeshBounds(vertices);
                    Assert.That(half.xMin, Is.EqualTo(full.xMin).Within(.1f), fill.name + " same fill origin");
                    Assert.That(half.width, Is.EqualTo(full.width * .5f).Within(.1f), fill.name + " half uses actual clipped geometry");
                    AssertInside(half, full, fill.name + " half fill cannot overrun full fill");
                }
            }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2340, 1080)]
    public void SupportedAspectGeometryKeepsListsIndependentWidePlushAndActionAreasSeparated(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            var ordered = host.Lists.OrderBy(list => Bounds((RectTransform)list.transform, host.Rect).center.x).ToArray();
            CollectionAssert.AreEqual(new[] { PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Plush, PopupStoreMaterialKind.Ribbon }, ordered.Select(Kind).ToArray());
            var panelRects = ordered.Select(list => Bounds((RectTransform)list.transform, host.Rect)).ToArray();
            var viewportRects = ordered.Select(list => Bounds(Scroll(list).viewport, host.Rect)).ToArray();
            Assert.That(viewportRects[1].width, Is.GreaterThan(viewportRects[0].width));
            Assert.That(viewportRects[1].width, Is.GreaterThan(viewportRects[2].width));
            for (int i = 0; i < ordered.Length; i++)
            {
                AssertInside(panelRects[i], host.Rect.rect, ordered[i].name);
                if (i > 0) Assert.That(panelRects[i - 1].xMax, Is.LessThan(panelRects[i].xMin), "Separate panel and touch areas");
                var icons = Icons(ordered[i]);
                Assert.That(icons.Length, Is.EqualTo(i == 1 ? 32 : 3));
                Assert.That(icons.All(image => image.raycastTarget), Is.True, "Runtime materials retain click targets.");
                int completelyVisible = icons.Count(image => Contains(Scroll(ordered[i]).viewport.rect, Bounds(image.rectTransform, Scroll(ordered[i]).viewport)));
                Assert.That(completelyVisible, Is.GreaterThanOrEqualTo(i == 1 ? 5 : 3), ordered[i].name + " complete visible materials");
                Assert.That(Read<Vector2>(ordered[i], "_iconSize").x, Is.GreaterThanOrEqualTo(90), "Keep material hit areas useful; do not shrink icons to force a pass.");
                var clear = Read<Button>(host.Counter, "_clearButton");
                var pack = Read<Button>(host.Root.GetComponent<PopupStorePacking>(), "_packButton");
                foreach (var action in new[] { clear, pack })
                    Assert.That(Bounds((RectTransform)action.transform, host.Rect).Overlaps(panelRects[i]), Is.False, action.name + " must not cover a material panel");
                Debug.Log("POPUP_STORE_UI_LAYOUT resolution=" + width + "x" + height + "; kind=" + Kind(ordered[i]) + "; viewport=" + viewportRects[i].width.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "; fullyVisible=" + completelyVisible + "; icon=" + Read<Vector2>(ordered[i], "_iconSize"));
            }
            Assert.That(Read<Button>(ordered[0], "_nextButton").interactable, Is.False);
            Assert.That(Read<Button>(ordered[2], "_nextButton").interactable, Is.False);
            Assert.That(Read<Button>(ordered[1], "_nextButton").interactable, Is.True);
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2340, 1080)]
    public void MaximumOrderIconsCounterSlotsAndOriginalTypographyRemainReadableWithinApprovedFrames(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            var display = host.Root.GetComponent<PopupStoreOrderDisplay>();
            Assert.That(display.Refresh(), Is.True);
            var patienceSlots = Read<PopupStorePatienceSlot[]>(host.Root.GetComponent<PopupStoreCustomerPatience>(), "_slots");
            for (int slot = 0; slot < display.Slots.Count; slot++)
            {
                // Original NPC sheets have substantial transparent padding. Protect their actual visible pixels,
                // not an empty part of the unchanged authored 320px image rectangle.
                var portrait = VisibleSpriteBounds(display.Slots[slot].CustomerImage, host.Rect);
                var bubble = VisibleSpriteBounds(display.Slots[slot].OrderFrame, host.Rect);
                var patience = VisibleGaugeBounds(patienceSlots[slot], host.Rect);
                // The newly approved v3 clock sits across the customer's foot line (guide y470..486).
                // Permit only that authored lower strip; it must never obscure the body or face.
                if (patience.Overlaps(portrait))
                {
                    float scale = Mathf.Min(host.Rect.rect.width / 1672f, host.Rect.rect.height / 941f);
                    float overlapHeight = Mathf.Min(patience.yMax, portrait.yMax) - Mathf.Max(patience.yMin, portrait.yMin);
                    Assert.That(overlapHeight, Is.LessThanOrEqualTo(18 * scale + .1f), "Only the approved foot-clock overlap is allowed; customer " + slot);
                    Assert.That(patience.yMax, Is.LessThanOrEqualTo(portrait.yMin + 18 * scale + .1f), "A gauge must not cover a customer's torso or face.");
                }
                Assert.That(patience.Overlaps(bubble), Is.False, "Patience gauge must remain clear of order " + slot
                    + "; gauge=" + patience + "; visibleBubble=" + bubble);
            }
            var third = display.Slots[2];
            var materialIcons = third.MaterialIcons.Where(i => i.sprite != null).ToArray();
            Assert.That(materialIcons, Has.Length.EqualTo(5));
            for (int i = 0; i < materialIcons.Length; i++)
            {
                var current = Bounds(materialIcons[i].rectTransform, third.OrderFrame.rectTransform);
                AssertInside(current, third.OrderFrame.rectTransform.rect, "Five-item order icon " + i);
                Assert.That(current.width, Is.GreaterThanOrEqualTo(48));
                Assert.That(materialIcons[i].preserveAspect, Is.True);
                if (i > 0) Assert.That(current.Overlaps(Bounds(materialIcons[i - 1].rectTransform, third.OrderFrame.rectTransform)), Is.False);
            }
            var catalog = Read<PopupStoreMaterialCatalog>(host.Counter, "_catalog");
            Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plush, out string error), Is.True, error);
            for (int i = 0; i < 5; i++) Assert.That(host.Counter.TryAdd(plush[i]), Is.True);
            Assert.That(host.Counter.TryAdd(plush[5]), Is.False);
            var slots = Read<Image[]>(host.Counter, "_slotIcons");
            Assert.That(slots, Has.Length.EqualTo(5));
            var tray = host.Root.GetComponentsInChildren<Image>(true).Single(i => AssetDatabase.GetAssetPath(i.sprite)
                == "Assets/Sprites/PopupStore/FinalGuideV3/FG_Tray_FiveEmptySlots.png");
            Assert.That(tray.preserveAspect, Is.True);
            Assert.That(tray.rectTransform.rect.width / tray.rectTransform.rect.height, Is.EqualTo(tray.sprite.rect.width / tray.sprite.rect.height).Within(.02f));
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i].sprite, Is.SameAs(plush[i].Sprite));
                AssertInside(Bounds(slots[i].rectTransform, tray.rectTransform), tray.rectTransform.rect, "Counter slot " + i);
                Assert.That(slots[i].preserveAspect, Is.True);
                if (i > 0) Assert.That(Bounds(slots[i - 1].rectTransform, host.Rect).xMax, Is.LessThan(Bounds(slots[i].rectTransform, host.Rect).xMin));
            }
            var score = Read<TMP_Text>(host.Root.GetComponent<PopupStoreScore>(), "_scoreText");
            var time = Read<TMP_Text>(host.Root.GetComponent<PopupStoreBusinessClock>(), "_timeText");
            foreach (var label in new[] { score, time })
            {
                label.ForceMeshUpdate(true, true);
                Assert.That(label.isTextOverflowing, Is.False, label.name);
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(26), label.name);
                AssertInside(Bounds(label.rectTransform, host.Rect), host.Rect.rect, label.name);
            }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2340, 1080)]
    public void StartAndExitModalsStillCoverNewSkinBlockInputAndRestoreItWithoutChangingMaterials(int width, int height)
    {
        using (var host = new Host(width, height, false))
        {
            // This retained legacy-modal test explicitly owns the optional 90-second review.
            // Ordinary hostless entry is covered by PopupStoreLatestEntryTests instead.
            PopupStoreApprovedTestSession.Attach(host.Root);
            var start = host.Root.GetComponent<PopupStoreStartPanel>();
            var overlay = Read<GameObject>(start, "_overlay");
            Assert.That(start.IsVisible, Is.True);
            AssertModal(host, overlay);
            var catalog = Read<PopupStoreMaterialCatalog>(host.Counter, "_catalog");
            catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plush, out _);
            Assert.That(host.Counter.TryAdd(plush[0]), Is.False, "Start screen blocks the actual selection boundary too.");
            host.Start();
            Assert.That(host.Counter.TryAdd(plush[0]), Is.True);
            var exit = host.Root.GetComponent<PopupStoreExitConfirmation>();
            Assert.That(exit.TryRequestExit(), Is.True);
            AssertModal(host, Read<GameObject>(exit, "_overlay"));
            Assert.That(host.Counter.TryAdd(plush[1]), Is.False);
            host.Counter.Clear();
            Assert.That(host.Counter.Count, Is.EqualTo(1), "A covered clear button cannot discard prepared materials.");
            Assert.That(exit.TryContinue(), Is.True);
            Assert.That(host.Counter.Count, Is.EqualTo(1));
            Assert.That(host.Counter.TryAdd(plush[1]), Is.True);
            Assert.That(host.Counter.Count, Is.EqualTo(2));
        }
    }

    private static void AssertModal(Host host, GameObject overlay)
    {
        Assert.That(overlay.activeInHierarchy, Is.True);
        Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(overlay.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
        Assert.That(host.Root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(host.Root.GetComponent<CanvasGroup>().interactable, Is.False);
        // Other hidden modal roots may remain later siblings after their OnEnable cleanup.
        // What matters is that no active visual or control renders over this full-screen blocker.
        for (int i = overlay.transform.GetSiblingIndex() + 1; i < host.Root.transform.childCount; i++)
        {
            var later = host.Root.transform.GetChild(i);
            if (!later.gameObject.activeInHierarchy) continue;
            Assert.That(later.GetComponentsInChildren<Graphic>(false).Any(graphic => graphic.isActiveAndEnabled && graphic.color.a > 0),
                Is.False, "Active visual above modal: " + later.name);
            Assert.That(later.GetComponentsInChildren<Selectable>(false).Any(control => control.isActiveAndEnabled),
                Is.False, "Active control above modal: " + later.name);
        }
        Assert.That(Contains(Bounds((RectTransform)overlay.transform, host.Rect), host.Rect.rect), Is.True, "Full screen modal blocker");
    }

    private sealed class Host : IDisposable
    {
        private readonly IDisposable _navigationHost;
        private readonly StaffGachaOfflineFonts _fonts;
        internal readonly GameObject Root;
        internal readonly RectTransform Rect;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreMaterialList[] Lists;
        internal Host(int width, int height, bool start)
        {
            // Reuse the existing actual-prefab navigation host so exit confirmation exercises its real stack contract.
            var hostType = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _navigationHost = (IDisposable)Activator.CreateInstance(hostType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { null }, System.Globalization.CultureInfo.InvariantCulture);
            Root = Read<GameObject>(_navigationHost, "Root");
            try
            {
                Root.name = "popup-approved-ui-memory-test";
                Root.SetActive(false);
                _fonts = new StaffGachaOfflineFonts();
                _fonts.BindBeforeActivation(Root);
                Root.SetActive(true);
                var scaler = Root.GetComponent<CanvasScaler>();
                // Match the authored ScaleWithScreenSize calculation without altering the user's GameView.
                float logWidth = Mathf.Log(width / scaler.referenceResolution.x, 2);
                float logHeight = Mathf.Log(height / scaler.referenceResolution.y, 2);
                float scale = Mathf.Pow(2, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
                scaler.enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Rect = Root.GetComponent<RectTransform>();
                Rect.sizeDelta = new Vector2(width / scale, height / scale);
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                if (start) Start();
                Layout();
            }
            catch { Dispose(); throw; }
        }
        internal void Start()
        {
            Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True);
            var entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
            entrance.TickEntrance(0); entrance.TickEntrance(4); entrance.TickEntrance(4);
            entrance.TickEntrance(Read<float>(entrance, "_entryDuration") + .01f);
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
            Layout();
        }
        private void Layout()
        {
            // V3 owns one popup-local contain transform; exercise it after the fixture changes
            // logical resolution, without changing the shared CanvasScaler or user GameView.
            var guide = Root.GetComponent("PopupStoreFinalGuideLayout");
            Assert.That(guide, Is.Not.Null);
            Assert.That(guide.GetType().GetMethod("ApplyLayout").Invoke(guide, null), Is.EqualTo(true));
            Canvas.ForceUpdateCanvases();
            foreach (var list in Lists) Scroll(list).Rebuild(CanvasUpdate.PostLayout);
        }
        public void Dispose() { try { _navigationHost?.Dispose(); } finally { _fonts?.Dispose(); } }
    }

    private static GameObject Prefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    private static PopupStoreMaterialKind Kind(PopupStoreMaterialList list) => Read<PopupStoreMaterialKind>(list, "_kind");
    private static ScrollRect Scroll(PopupStoreMaterialList list) => Read<ScrollRect>(list, "_scrollRect");
    private static Image[] Icons(PopupStoreMaterialList list) => Read<RectTransform>(list, "_content").GetComponentsInChildren<Image>(false);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static string GuidOf(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    private static Rect Bounds(RectTransform child, RectTransform root)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
    }
    private static Rect MeshBounds(VertexHelper mesh)
    {
        var vertices = new List<UIVertex>(); mesh.GetUIVertexStream(vertices);
        return Rect.MinMaxRect(vertices.Min(v => v.position.x), vertices.Min(v => v.position.y), vertices.Max(v => v.position.x), vertices.Max(v => v.position.y));
    }
    private static Rect VisibleGaugeBounds(PopupStorePatienceSlot slot, RectTransform root)
    {
        var visible = new List<Rect>();
        foreach (var image in slot.Root.GetComponentsInChildren<Image>(false))
        {
            if (!image.isActiveAndEnabled || image.color.a <= 0) continue;
            if (image.type == Image.Type.Filled)
            {
                using (var mesh = new VertexHelper())
                {
                    typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(image, new object[] { mesh });
                    foreach (var effect in image.GetComponents<BaseMeshEffect>())
                        if (effect.IsActive()) effect.ModifyMesh(mesh);
                    if (mesh.currentVertCount > 0) visible.Add(TransformBounds(MeshBounds(mesh), image.rectTransform, root));
                }
            }
            else visible.Add(VisibleSpriteBounds(image, root));
        }
        var time = slot.TimeText;
        if (time.isActiveAndEnabled && time.color.a > 0)
        {
            time.ForceMeshUpdate(true, true);
            var textBounds = time.textBounds;
            if (textBounds.size.x > 0 && textBounds.size.y > 0)
                visible.Add(TransformBounds(Rect.MinMaxRect(textBounds.min.x, textBounds.min.y,
                    textBounds.max.x, textBounds.max.y), time.rectTransform, root));
        }
        Assert.That(visible, Is.Not.Empty, "A waiting order must have a visible patience gauge.");
        return Rect.MinMaxRect(visible.Min(bounds => bounds.xMin), visible.Min(bounds => bounds.yMin),
            visible.Max(bounds => bounds.xMax), visible.Max(bounds => bounds.yMax));
    }
    private static Rect TransformBounds(Rect local, RectTransform source, RectTransform root)
    {
        var corners = new[] { new Vector3(local.xMin, local.yMin, 0), new Vector3(local.xMin, local.yMax, 0),
            new Vector3(local.xMax, local.yMax, 0), new Vector3(local.xMax, local.yMin, 0) }
            .Select(point => root.InverseTransformPoint(source.TransformPoint(point))).ToArray();
        return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
    }
    private static Rect VisibleSpriteBounds(Image image, RectTransform root)
    {
        Assert.That(image.sprite, Is.Not.Null, image.name);
        Assert.That(image.type == Image.Type.Simple || image.type == Image.Type.Sliced, Is.True,
            "Alpha visibility supports the authored Simple images and v4 fixed-border Sliced frames.");
        string path = AssetDatabase.GetAssetPath(image.sprite);
        Texture2D source = null;
        try
        {
            source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(source, File.ReadAllBytes(path), false), Is.True, path);
            // An existing Single sprite can be downsampled and packed into a larger SpriteAtlas.
            // Its source PNG still covers the full artwork; atlas texture dimensions are unrelated.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(image.sprite.rect.width / image.sprite.rect.height,
                Is.EqualTo((float)source.width / source.height).Within(.002f), "Source aspect must be preserved.");
            var pixels = source.GetPixels32();
            int left = source.width, bottom = source.height, right = -1, top = -1;
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    if (pixels[y * source.width + x].a != 0)
                    { left = Math.Min(left, x); bottom = Math.Min(bottom, y); right = Math.Max(right, x); top = Math.Max(top, y); }
            Assert.That(right, Is.GreaterThanOrEqualTo(left), path + " visible artwork pixels");
            Rect drawing = image.GetPixelAdjustedRect();
            if (image.type == Image.Type.Simple && image.preserveAspect)
            {
                float aspect = image.sprite.rect.width / image.sprite.rect.height;
                if (drawing.width / drawing.height > aspect)
                {
                    float newWidth = drawing.height * aspect;
                    drawing.x += (drawing.width - newWidth) * image.rectTransform.pivot.x;
                    drawing.width = newWidth;
                }
                else
                {
                    float newHeight = drawing.width / aspect;
                    drawing.y += (drawing.height - newHeight) * image.rectTransform.pivot.y;
                    drawing.height = newHeight;
                }
            }
            Rect alphaRect;
            if (image.type == Image.Type.Sliced && image.sprite.border != Vector4.zero)
            {
                // Transparent padding lies inside each fixed border, not in the stretched
                // centre. Map the source alpha extrema through the same three segments per
                // axis that Unity uses for Sliced Images, including narrow-rect border fitting.
                Vector4 border = image.sprite.border;
                float unitsPerPixel = 1 / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
                alphaRect = Rect.MinMaxRect(
                    SlicedAlphaCoordinate(left, source.width, image.sprite.rect.width, border.x, border.z, unitsPerPixel, drawing.xMin, drawing.width),
                    SlicedAlphaCoordinate(bottom, source.height, image.sprite.rect.height, border.y, border.w, unitsPerPixel, drawing.yMin, drawing.height),
                    SlicedAlphaCoordinate(right + 1, source.width, image.sprite.rect.width, border.x, border.z, unitsPerPixel, drawing.xMin, drawing.width),
                    SlicedAlphaCoordinate(top + 1, source.height, image.sprite.rect.height, border.y, border.w, unitsPerPixel, drawing.yMin, drawing.height));
            }
            else
                alphaRect = Rect.MinMaxRect(drawing.x + drawing.width * left / source.width,
                    drawing.y + drawing.height * bottom / source.height,
                    drawing.x + drawing.width * (right + 1) / source.width,
                    drawing.y + drawing.height * (top + 1) / source.height);
            var visible = TransformBounds(alphaRect, image.rectTransform, root);
            // V3 deliberately clips the lower customer body at the integrated counter. Compare what
            // actually survives each enabled clip, not hidden source pixels below that counter.
            foreach (var mask in image.GetComponentsInParent<RectMask2D>(true))
            {
                if (!mask.enabled) continue;
                var clip = Bounds(mask.rectTransform, root);
                visible = Rect.MinMaxRect(Mathf.Max(visible.xMin, clip.xMin), Mathf.Max(visible.yMin, clip.yMin),
                    Mathf.Min(visible.xMax, clip.xMax), Mathf.Min(visible.yMax, clip.yMax));
            }
            return visible;
        }
        finally { if (source != null) Object.DestroyImmediate(source); }
    }
    private static float SlicedAlphaCoordinate(float sourcePixel, float sourceSize, float spriteSize,
        float firstBorder, float lastBorder, float unitsPerPixel, float minimum, float length)
    {
        float pixel = sourcePixel * spriteSize / sourceSize;
        float first = firstBorder * unitsPerPixel, last = lastBorder * unitsPerPixel;
        float total = first + last;
        if (total > length && total > 0) { float fit = length / total; first *= fit; last *= fit; }
        if (pixel <= firstBorder && firstBorder > 0) return minimum + first * pixel / firstBorder;
        if (pixel >= spriteSize - lastBorder && lastBorder > 0)
            return minimum + length - last * (spriteSize - pixel) / lastBorder;
        float middle = spriteSize - firstBorder - lastBorder;
        Assert.That(middle, Is.GreaterThan(0), "Sliced source must retain an interior region.");
        return minimum + first + (length - first - last) * (pixel - firstBorder) / middle;
    }
    private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .1f && inner.xMax <= outer.xMax + .1f && inner.yMin >= outer.yMin - .1f && inner.yMax <= outer.yMax + .1f;
    private static void AssertInside(Rect inner, Rect outer, string message) => Assert.That(Contains(outer, inner), Is.True, message + " inner=" + inner + " outer=" + outer);
}
