#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>V3 layout and explicit v4 source replacement contracts. Memory fixtures are not GameView approval.</summary>
public sealed class PopupStoreFinalGuideTests
{
    private const string Art = "Assets/Sprites/PopupStore/FinalGuideV3/";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GachaCollectionPreviewSceneWitness _workspace;
    private UnityEngine.Random.State _random;
    private long _money, _encounter;
    private int _diamonds;
    private bool _backend;
    private float _timeScale;

    [Serializable] private sealed class Manifest
    {
        public string schema;
        public int active_ui_sprite_count, active_runtime_png_count_including_background;
        public Background background;
        public Part[] sprites;
    }
    [Serializable] private sealed class Background { public string file, sha256; public int[] size; public int bytes; }
    [Serializable] private sealed class Part
    {
        public string id, file, sha256;
        public int[] png_size, place_png_top_left_guide, guide_rect_xywh;
        public int padding_px;
        public bool active_v3;
    }

    [SetUp] public void CaptureWorkspace()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture(); _random = UnityEngine.Random.state;
        _money = UserInfo.Money; _diamonds = UserInfo.Dia; _encounter = UserInfo.CustomerEncounterContextGeneration;
        _backend = BackEnd.Backend.IsInitialized; _timeScale = Time.timeScale;
    }
    [TearDown] public void PreserveWorkspaceAndRandomState()
    {
        _workspace.AssertUnchanged("Final guide V3 memory tests"); Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_encounter));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_backend)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
    }

    [Test]
    public void OriginalManifestAndLayoutBytesIdentifyExactlyFortyThreeApprovedPngs()
    {
        Assert.That(Hash(Art + "active_manifest_v3.json"), Is.EqualTo("439d45fddc658133b6a4512c927b22bb22d689d5800d070ca12849c2a16ebf8d"));
        Assert.That(Hash(Art + "layout_final_guide_v3.json"), Is.EqualTo("a063d74fa1d5536b3f6288e658c606e2bce0baf7a04873534128e71630cfdccb"));
        var manifest = LoadManifest();
        Assert.That(manifest.schema, Is.EqualTo("popup-ui-final-apply-v3"));
        Assert.That(manifest.active_ui_sprite_count, Is.EqualTo(42));
        Assert.That(manifest.active_runtime_png_count_including_background, Is.EqualTo(43));
        Assert.That(manifest.sprites, Has.Length.EqualTo(42));
        Assert.That(manifest.sprites.All(part => part.active_v3), Is.True);
        CollectionAssert.AllItemsAreUnique(manifest.sprites.Select(part => part.id).ToArray());
        CollectionAssert.AllItemsAreUnique(manifest.sprites.Select(part => part.file).ToArray());
        var expected = manifest.sprites.Select(part => Path.GetFileName(part.file))
            .Concat(new[] { Path.GetFileName(manifest.background.file) }).ToArray();
        var map = PopupStoreCleanPartsTests.Map();
        CollectionAssert.AreEquivalent(expected, map.replacements.Select(row => row.old_id + ".png")
            .Concat(map.unchanged_v3.Select(part => part.id + ".png"))
            .Concat(new[] { "FG_Background_CafeCounter_APPROVED.png" }).ToArray());
        PopupStoreCleanPartsTests.AssertLegacyReplacementTrace();
    }

    [Test]
    public void BackgroundRetainsApprovedRgbBytesDimensionsAndOneFullResolutionSprite()
    {
        // V4 replaces only the old sign and ropes. Keep the original source identity in
        // its unchanged manifest and validate the approved derived background.
        Assert.That(LoadManifest().background.sha256, Is.EqualTo(PopupStoreCleanPartsTests.Map().background.original_sha256));
        PopupStoreCleanPartsTests.AssertCurrentBackground();
    }

    [Test]
    public void AllFortyTwoUiPartsKeepHashRgbaAlphaPaddingAndUnscaledSingleSpriteImports()
    {
        var replacements = PopupStoreCleanPartsTests.Map().replacements;
        foreach (var part in LoadManifest().sprites)
        {
            var replacement = replacements.SingleOrDefault(row => row.old_id == part.id);
            if (replacement != null)
            {
                Assert.That(replacement.old_sha256, Is.EqualTo(part.sha256), "Explicit approved source replacement trace");
                PopupStoreCleanPartsTests.AssertPart(PopupStoreCleanPartsTests.Parts().parts.Single(p => p.id == replacement.replacement_id));
                continue;
            }
            Assert.That(PopupStoreCleanPartsTests.Map().unchanged_v3.Any(keep => keep.id == part.id && keep.sha256 == part.sha256), Is.True);
            string path = Art + Path.GetFileName(part.file);
            Assert.That(Hash(path), Is.EqualTo(part.sha256), path);
            byte[] bytes = File.ReadAllBytes(path);
            AssertPngHeader(bytes, 6, path);
            var decoded = Decode(bytes);
            try
            {
                Assert.That(decoded.width, Is.EqualTo(part.png_size[0]), path);
                Assert.That(decoded.height, Is.EqualTo(part.png_size[1]), path);
                var pixels = decoded.GetPixels32();
                Assert.That(pixels.Any(pixel => pixel.a == 0) && pixels.Any(pixel => pixel.a == 255), Is.True, path + " real alpha range");
                bool marginClear = true;
                for (int y = 0; y < decoded.height && marginClear; y++)
                    for (int x = 0; x < decoded.width && marginClear; x++)
                        if (x < part.padding_px || y < part.padding_px || x >= decoded.width - part.padding_px || y >= decoded.height - part.padding_px)
                            marginClear = pixels[y * decoded.width + x].a == 0;
                Assert.That(marginClear, Is.True, path + " declared transparent padding");
                Assert.That(part.place_png_top_left_guide[0] + part.padding_px, Is.EqualTo(part.guide_rect_xywh[0]), part.id);
                Assert.That(part.place_png_top_left_guide[1] + part.padding_px, Is.EqualTo(part.guide_rect_xywh[1]), part.id);
                Assert.That(part.png_size[0] - part.padding_px * 2, Is.EqualTo(part.guide_rect_xywh[2]), part.id);
                Assert.That(part.png_size[1] - part.padding_px * 2, Is.EqualTo(part.guide_rect_xywh[3]), part.id);
            }
            finally { Object.DestroyImmediate(decoded); }
            AssertImport(path, part.png_size[0], part.png_size[1], true);
        }
    }

    [Test]
    public void EveryApprovedPartHasAnOwnedPrefabReferenceAndNoReferenceImageOrSecondCounterIsRendered()
    {
        var root = Prefab();
        PopupStoreCleanPartsTests.AssertCurrentDependencies(root);
        var background = root.transform.Find("Approved Front Background").GetComponent<Image>();
        Assert.That(background.color, Is.EqualTo(Color.white), "No unapproved colour filter.");
        Assert.That(background.preserveAspect, Is.True);
        foreach (var image in root.GetComponentsInChildren<Image>(true).Where(image => IsAuthoredVisible(image.transform, root.transform)))
        {
            string path = AssetDatabase.GetAssetPath(image.sprite);
            foreach (string excluded in new[] { "FG_Counter_Wood_CLEAN_RECONSTRUCTED", "FG_Sign_Blank", "FG_Background_VISIBLE_SOURCE_ONLY",
                "FG_Background_OCCLUSION_MASK", "APPROVED_FINAL_GUIDE_UNCHANGED", "PopupStore_BG_FrontView_Approved" })
                Assert.That(path, Does.Not.Contain(excluded), image.name + " forbidden duplicate or guide image");
        }
        foreach (string excluded in new[] { "Background Warm Veil", "Wood Counter Foreground", "Hanging Sign Caption" })
        {
            var old = root.transform.Find(excluded);
            Assert.That(old, Is.Null, "V4 removes obsolete popup-only decorations completely.");
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void MainPanelButtonTrayAndHudRectanglesMatchPaddedManifestCoordinates(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            string[] ids = { "FG_HUD_ScoreFrame", "FG_HUD_TimerFrame", "FG_Grade_Track", "FG_Icon_ClockPink",
                "FG_OrderBubble_01", "FG_OrderBubble_02", "FG_OrderBubble_03", "FG_OrderAlert_01", "FG_OrderAlert_02", "FG_OrderAlert_03",
                "FG_Button_Clear_Frame", "FG_Button_Pack_Frame", "FG_Icon_PackGift", "FG_Tray_FiveEmptySlots",
                "FG_Panel_Box", "FG_Tab_Box_Blank", "FG_Panel_Plush", "FG_Tab_Plush_Blank", "FG_Panel_Ribbon", "FG_Tab_Ribbon_Blank",
                "FG_Icon_Category_Box", "FG_Icon_Category_Plush", "FG_Icon_Category_Ribbon", "FG_Icon_Broom",
                "FG_Arrow_Box_Left", "FG_Arrow_Box_Right", "FG_Arrow_Plush_Left", "FG_Arrow_Plush_Right", "FG_Arrow_Ribbon_Left", "FG_Arrow_Ribbon_Right" };
            foreach (string id in ids)
            {
                var replacement = PopupStoreCleanPartsTests.Map().replacements.SingleOrDefault(row => row.old_id == id);
                if (replacement != null)
                {
                    PopupStoreCleanPartsTests.AssertMappedRect(PopupStoreCleanPartsTests.MappedImages(host.Root, id).Single(), host.Rect, replacement);
                    continue;
                }
                var part = LoadManifest().sprites.Single(item => item.id == id);
                var image = ImageFor(host.Root, id);
                Assert.That(image.preserveAspect, Is.True, id);
                Assert.That(image.type, Is.EqualTo(Image.Type.Simple), "Use source proportions instead of stretching curves or slots.");
                AssertGuideRect(image.rectTransform, host.Rect,
                    new Rect(part.place_png_top_left_guide[0], part.place_png_top_left_guide[1], part.png_size[0], part.png_size[1]), id);
            }
            AssertGuideRect(host.Root.transform.Find("Approved Front Background").GetComponent<RectTransform>(), host.Rect,
                new Rect(0, 0, 1672, 941), "background uses the same contain transform");
            AssertGuideRect(Read<Button>(host.View, "_closeButton").GetComponent<RectTransform>(), host.Rect,
                new Rect(1549, 19, 88, 84), "original X placement");
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void FiveActualSelectedMaterialsUseTheFiveApprovedTrayCentresWithoutDuplicateSlotFrames(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            var entries = host.Orders.GetDisplayedMaterials(2).ToArray();
            foreach (var entry in entries) Assert.That(host.Counter.TryAdd(entry), Is.True);
            var icons = Read<Image[]>(host.Counter, "_slotIcons");
            CollectionAssert.AreEqual(entries.Select(entry => entry.Sprite).ToArray(), icons.Select(image => image.sprite).ToArray());
            float[] x = { 512, 671, 835, 995, 1157 };
            for (int i = 0; i < icons.Length; i++)
            {
                var guide = ToGuide(Bounds(icons[i].rectTransform, host.Rect), host.Rect);
                Assert.That(guide.center.x, Is.EqualTo(x[i]).Within(.25f));
                Assert.That(guide.center.y, Is.EqualTo(607).Within(.25f));
                Assert.That(icons[i].preserveAspect, Is.True);
                if (i > 0) Assert.That(Bounds(icons[i - 1].rectTransform, host.Rect).Overlaps(Bounds(icons[i].rectTransform, host.Rect)), Is.False);
            }
            Assert.That(host.Counter.TryAdd(entries[0]), Is.False);
            CollectionAssert.AreEqual(entries.Select(entry => entry.Id).ToArray(), host.Counter.Materials.Select(entry => entry.Id).ToArray());
            foreach (var image in host.Counter.GetComponentsInChildren<Image>(true).Except(icons))
                if (image.name.StartsWith("Empty Slot", StringComparison.Ordinal))
                    Assert.That(image.enabled && image.color.a > 0 && image.sprite != null, Is.False, "Tray pixels already contain all five frames.");
            Read<Button>(host.Counter, "_clearButton").onClick.Invoke();
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(icons.All(image => !image.enabled || image.sprite == null || !image.gameObject.activeSelf), Is.True);
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void CustomerClipsLeaveEachBalloonAndGaugeOutsideTheirMaskAtTheApprovedCounterBoundary(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            var gauges = Read<PopupStorePatienceSlot[]>(host.Root.GetComponent<PopupStoreCustomerPatience>(), "_slots");
            for (int i = 0; i < 3; i++)
            {
                var slot = host.Orders.Slots[i];
                var clip = slot.CustomerImage.transform.parent.GetComponent<RectMask2D>();
                Assert.That(clip, Is.Not.Null); Assert.That(clip.enabled, Is.True);
                Assert.That(clip.transform.parent, Is.SameAs(slot.OrderFrame.transform.parent));
                Assert.That(slot.OrderFrame.transform.IsChildOf(clip.transform), Is.False);
                Assert.That(gauges[i].Root.transform.IsChildOf(clip.transform), Is.False);
                Assert.That(slot.CustomerImage.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                var clipGuide = ToGuide(Bounds(clip.rectTransform, host.Rect), host.Rect);
                Assert.That(clipGuide.yMax, Is.EqualTo(486).Within(.25f));
                Assert.That(clipGuide.xMin, Is.LessThanOrEqualTo(.25f));
                Assert.That(clipGuide.xMax, Is.GreaterThanOrEqualTo(1671.75f), "Entering/exiting portraits must not vanish at a narrow slot edge.");
                Assert.That(host.Entrance.IsWaiting(i), Is.True, "Visual clipping must not prevent arrival.");
                Assert.That(slot.OrderFrame.gameObject.activeInHierarchy, Is.True);
                Assert.That(gauges[i].Root.activeInHierarchy, Is.True);
            }
        }
    }

    [Test]
    public void EveryNewDecorationLeavesActualSelectionScrollAndButtonsAsTheOnlyInputTargets()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var graphics = host.Root.GetComponentsInChildren<Image>(true).Where(image =>
                AssetDatabase.GetAssetPath(image.sprite).StartsWith(Art, StringComparison.Ordinal)
                || AssetDatabase.GetAssetPath(image.sprite).StartsWith(PopupStoreCleanPartsTests.Art, StringComparison.Ordinal)).ToArray();
            Assert.That(graphics.Length, Is.GreaterThan(35));
            foreach (var image in graphics)
            {
                var button = image.GetComponent<Button>();
                Assert.That(image.raycastTarget, Is.EqualTo(button != null), image.name);
                if (button != null)
                { Assert.That(button.targetGraphic, Is.SameAs(image)); Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero); }
            }
            foreach (var list in host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true))
            {
                var scroll = Read<ScrollRect>(list, "_scrollRect");
                Assert.That(scroll.viewport.GetComponent<RectMask2D>().enabled, Is.True);
                Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True);
                var icons = Read<RectTransform>(list, "_content").GetComponentsInChildren<Image>(false);
                Assert.That(icons.Length, Is.EqualTo(Read<PopupStoreMaterialKind>(list, "_kind") == PopupStoreMaterialKind.Plush ? 32 : 3));
                Assert.That(icons.All(icon => icon.raycastTarget && icon.GetComponent<PopupStoreMaterialItem>() != null), Is.True);
            }
        }
    }

    [Test]
    public void MeasuredSourceBoundsRetainAllThirtyEightMaterialsAndOriginalCustomersWithoutSubstitutions()
    {
        var root = Prefab();
        var data = AssetDatabase.LoadAssetAtPath<PopupStoreSpriteVisualBounds>(Art + "OriginalSpriteVisualBounds.asset");
        Assert.That(data, Is.Not.Null);
        var entries = Read<PopupStoreSpriteVisualBounds.Entry[]>(data, "_entries");
        Assert.That(entries, Has.Length.EqualTo(41));
        CollectionAssert.AllItemsAreUnique(entries.Select(entry => entry.Sprite.GetInstanceID()).ToArray());
        var catalog = Read<PopupStoreMaterialCatalog>(root.GetComponent<PopupStoreOrderDisplay>(), "_catalog");
        var expected = new List<Sprite>();
        foreach (PopupStoreMaterialKind kind in Enum.GetValues(typeof(PopupStoreMaterialKind)))
        {
            Assert.That(catalog.TryGetMaterials(kind, out var materials, out string error), Is.True, error);
            expected.AddRange(materials.Select(material => material.Sprite));
        }
        expected.AddRange(Read<PopupStoreOrderCustomerVisual[]>(root.GetComponent<PopupStoreOrderDisplay>(), "_customerVisuals").Select(visual => visual.Sprite));
        CollectionAssert.AreEquivalent(expected, entries.Select(entry => entry.Sprite).ToArray());
        foreach (var entry in entries)
        {
            Assert.That(data.TryGet(entry.Sprite, out var measured), Is.True, entry.Sprite.name);
            var texture = Decode(File.ReadAllBytes(AssetDatabase.GetAssetPath(entry.Sprite)));
            try
            {
                var pixels = texture.GetPixels32();
                int x0 = texture.width, y0 = texture.height, x1 = -1, y1 = -1;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                        if (pixels[y * texture.width + x].a >= 16)
                        { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
                var original = new Rect((float)x0 / texture.width, (float)y0 / texture.height,
                    (float)(x1 - x0 + 1) / texture.width, (float)(y1 - y0 + 1) / texture.height);
                Assert.That(measured, Is.EqualTo(original), entry.Sprite.name + " measured approved source alpha, never an edited PNG");
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }

    [Test]
    public void MaterialMeshNormalizationFitsVisibleArtworkButPreservesSpriteUvsHitAreaAndProgressChildren()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var image = host.Orders.Slots[0].MaterialIcons[0];
            var effect = image.GetComponent<PopupStoreMaterialVisual>();
            Assert.That(effect, Is.Not.Null);
            var data = Read<PopupStoreSpriteVisualBounds>(effect, "_bounds");
            var catalog = Read<PopupStoreMaterialCatalog>(host.Counter, "_catalog");
            var originalSprite = image.sprite;
            var position = image.rectTransform.anchoredPosition;
            var size = image.rectTransform.sizeDelta;
            var children = Enumerable.Range(0, image.transform.childCount).Select(i => image.transform.GetChild(i).GetInstanceID()).ToArray();
            try
            {
                int checkedMaterials = 0;
                foreach (PopupStoreMaterialKind kind in Enum.GetValues(typeof(PopupStoreMaterialKind)))
                {
                    Assert.That(catalog.TryGetMaterials(kind, out var materials, out _), Is.True);
                    foreach (var material in materials)
                    {
                        image.sprite = material.Sprite;
                        Assert.That(data.TryGet(material.Sprite, out var opaque), Is.True);
                        using (var mesh = new VertexHelper())
                        {
                            Populate(image, mesh);
                            var before = Vertices(mesh);
                            effect.ModifyMesh(mesh);
                            var after = Vertices(mesh);
                            Assert.That(after.Count, Is.EqualTo(before.Count));
                            CollectionAssert.AreEqual(before.Select(v => v.uv0).ToArray(), after.Select(v => v.uv0).ToArray(), material.Id + " UV identity");
                            var rendered = MeshRect(after);
                            Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(material.Sprite);
                            Vector2 source = material.Sprite.rect.size;
                            Vector2 fraction = new Vector2(1 - (padding.x + padding.z) / source.x, 1 - (padding.y + padding.w) / source.y);
                            Vector2 full = new Vector2(rendered.width / fraction.x, rendered.height / fraction.y);
                            Vector2 origin = rendered.min - Vector2.Scale(full, new Vector2(padding.x / source.x, padding.y / source.y));
                            var visible = new Rect(origin + Vector2.Scale(full, opaque.min), Vector2.Scale(full, opaque.size));
                            AssertInside(visible, image.rectTransform.rect, material.Id + " actual visible bounds");
                            Assert.That(Mathf.Min(Mathf.Abs(visible.width - image.rectTransform.rect.width),
                                Mathf.Abs(visible.height - image.rectTransform.rect.height)), Is.LessThan(.05f), material.Id + " fits one axis without distortion");
                            Assert.That(visible.width / visible.height,
                                Is.EqualTo(opaque.width * source.x / (opaque.height * source.y)).Within(.001f), material.Id + " source aspect");
                        }
                        Assert.That(image.sprite, Is.SameAs(material.Sprite));
                        Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(position));
                        Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(size));
                        CollectionAssert.AreEqual(children, Enumerable.Range(0, image.transform.childCount).Select(i => image.transform.GetChild(i).GetInstanceID()).ToArray());
                        checkedMaterials++;
                    }
                }
                Assert.That(checkedMaterials, Is.EqualTo(38));
                Assert.That(host.Counter.Count, Is.Zero); Assert.That(host.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
                Assert.That(host.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            }
            finally { image.sprite = originalSprite; }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void ThreeFourFiveItemOrdersAndRepeatedPlushesRemainInsideTheirOwnApprovedContentAreas(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            Rect[] contents = { new Rect(261, 187, 316, 84), new Rect(684, 187, 327, 84), new Rect(1118, 187, 315, 84) };
            for (int slotIndex = 0; slotIndex < 3; slotIndex++)
            {
                var slot = host.Orders.Slots[slotIndex];
                var layout = slot.OrderFrame.GetComponent<PopupStoreOrderVisualLayout>();
                Assert.That(layout, Is.Not.Null);
                AssertGuideRect(layout.ContentArea, host.Rect, contents[slotIndex], "approved order content " + slotIndex);
                var materials = host.Orders.GetDisplayedMaterials(slotIndex);
                Assert.That(materials.Count, Is.EqualTo(slotIndex + 3));
                for (int i = 0; i < 5; i++)
                {
                    var icon = slot.MaterialIcons[i];
                    Assert.That(icon.gameObject.activeSelf, Is.EqualTo(i < materials.Count));
                    if (i >= materials.Count) continue;
                    Assert.That(icon.sprite, Is.SameAs(materials[i].Sprite));
                    AssertInside(Bounds(icon.rectTransform, layout.ContentArea), layout.ContentArea.rect, "material " + i);
                    Assert.That(icon.raycastTarget, Is.False);
                    if (i > 0) Assert.That(Bounds(icon.rectTransform, layout.ContentArea).Overlaps(Bounds(slot.MaterialIcons[i - 1].rectTransform, layout.ContentArea)), Is.False);
                }
            }
            Assert.That(host.Orders.Slots[1].MaterialIcons[0].sprite, Is.SameAs(host.Orders.Slots[1].MaterialIcons[1].sprite));
            Assert.That(host.Orders.Slots[1].MaterialIcons[0], Is.Not.SameAs(host.Orders.Slots[1].MaterialIcons[1]));
            Assert.That(host.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    [Test]
    public void RealScrollRangeControlsPageDotsWithoutInventingPagesForShortListsOrSelectingMaterials()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var lists = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
            foreach (var list in lists)
            {
                var kind = Read<PopupStoreMaterialKind>(list, "_kind");
                var indicator = list.GetComponentInChildren<PopupStoreScrollPageIndicator>(true);
                Assert.That(indicator, Is.Not.Null);
                var scroll = Read<ScrollRect>(list, "_scrollRect");
                indicator.Refresh();
                var initialIds = indicator.GetComponentsInChildren<Image>(true).Select(image => image.GetInstanceID()).ToArray();
                if (kind != PopupStoreMaterialKind.Plush)
                {
                    Assert.That(indicator.PageCount, Is.Zero);
                    Assert.That(indicator.GetComponentsInChildren<Image>(false), Is.Empty);
                    Assert.That(Read<Button>(list, "_nextButton").interactable, Is.False);
                }
                else
                {
                    // 32 existing icons and the authored 600px move distance produce seven real positions.
                    Assert.That(indicator.PageCount, Is.EqualTo(7));
                    Assert.That(indicator.CurrentPage, Is.Zero);
                    Read<Button>(list, "_nextButton").onClick.Invoke();
                    indicator.Refresh();
                    Assert.That(indicator.CurrentPage, Is.EqualTo(1));
                    scroll.horizontalNormalizedPosition = 1; indicator.Refresh();
                    Assert.That(indicator.CurrentPage, Is.EqualTo(6));
                    var dots = indicator.GetComponentsInChildren<Image>(false);
                    Assert.That(dots, Has.Length.EqualTo(7));
                    Assert.That(dots.All(dot => !dot.raycastTarget && dot.GetComponent<Selectable>() == null), Is.True);
                    Assert.That(dots.Count(dot => AssetDatabase.GetAssetPath(dot.sprite) == PopupStoreCleanPartsTests.Art + "V4_Dot_Pink_Active.png"), Is.EqualTo(1));
                    foreach (var dot in dots) AssertInside(Bounds(dot.rectTransform, indicator.GetComponent<RectTransform>()), indicator.GetComponent<RectTransform>().rect, "dot fits actual page indicator");
                }
                for (int i = 0; i < 10; i++) indicator.Refresh();
                CollectionAssert.AreEqual(initialIds, indicator.GetComponentsInChildren<Image>(true).Select(image => image.GetInstanceID()).ToArray());
            }
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    [TestCase("foreign-target")]
    [TestCase("duplicate-target")]
    [TestCase("invalid-dimensions")]
    public void InvalidGuidePlacementsRejectBeforeMovingAnyOwnedOrForeignObject(string defect)
    {
        using (var host = new Host(1920, 1080, true))
        using (var other = new Host(1920, 1080, true))
        {
            var layout = host.Root.GetComponent<PopupStoreFinalGuideLayout>();
            var entries = Read<PopupStoreFinalGuideLayout.Entry[]>(layout, "_placements");
            var before = entries.Select(entry => entry.Target.anchoredPosition).ToArray();
            var foreign = other.Root.transform.Find("HUD").GetComponent<RectTransform>();
            Vector2 foreignBefore = foreign.anchoredPosition;
            var invalid = entries.Select(entry => new PopupStoreFinalGuideLayout.Entry { Target = entry.Target, GuideRect = entry.GuideRect }).ToArray();
            if (defect == "foreign-target") invalid[invalid.Length - 1].Target = foreign;
            else if (defect == "duplicate-target") invalid[invalid.Length - 1].Target = invalid[0].Target;
            else invalid[invalid.Length - 1].GuideRect = new Rect(0, 0, float.NaN, 10);
            Write(layout, "_placements", invalid);
            Assert.That(layout.ApplyLayout(), Is.False);
            CollectionAssert.AreEqual(before, entries.Select(entry => entry.Target.anchoredPosition).ToArray());
            Assert.That(foreign.anchoredPosition, Is.EqualTo(foreignBefore));
            Write(layout, "_placements", entries);
            Assert.That(layout.ApplyLayout(), Is.True);
        }
    }

    [Test]
    public void CustomerVisualOwnershipRejectsForeignPortraitOrBodyWithoutMovingAnyCustomer()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var first = host.Orders.Slots[0]; var second = host.Orders.Slots[1];
            var clip = first.CustomerImage.transform.parent.GetComponent<PopupStoreCustomerVisualClip>();
            var otherClip = second.CustomerImage.transform.parent.GetComponent<PopupStoreCustomerVisualClip>();
            var body = clip.BodyArea;
            var positions = host.Orders.Slots.Select(slot => slot.CustomerImage.rectTransform.anchoredPosition).ToArray();
            Assert.That(clip.Owns(second.CustomerImage, first.OrderFrame.transform.parent), Is.False);
            Assert.That(clip.Owns(first.CustomerImage, second.OrderFrame.transform.parent), Is.False);
            Write(clip, "_bodyArea", otherClip.BodyArea);
            Assert.That(clip.Prepare(), Is.False);
            CollectionAssert.AreEqual(positions, host.Orders.Slots.Select(slot => slot.CustomerImage.rectTransform.anchoredPosition).ToArray());
            Assert.That(Enumerable.Range(0, 3).All(host.Entrance.IsWaiting), Is.True);
            Write(clip, "_bodyArea", body);
            Assert.That(clip.Owns(first.CustomerImage, first.OrderFrame.transform.parent), Is.True);
        }
    }

    [Test]
    public void ComplaintIconPreparationUsesTheOriginalVisibleHeadAndNeverChangesVisibilityOrForeignIcons()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var icons = Read<Image[]>(host.Entrance, "_complaintIcons");
            Assert.That(icons, Has.Length.EqualTo(3));
            var patience = host.Root.GetComponent<PopupStoreCustomerPatience>();
            var remaining = Enumerable.Range(0, 3).Select(i => patience.GetTimer(i).RemainingPatience).ToArray();
            double businessRemaining = host.Root.GetComponent<PopupStoreBusinessClock>().RemainingSeconds;
            int changes = 0;
            host.Orders.Refreshed += () => changes++;
            host.Orders.SlotChanged += _ => changes++;
            for (int i = 0; i < 3; i++)
            {
                var portrait = host.Orders.Slots[i].CustomerImage;
                var clip = portrait.transform.parent.GetComponent<PopupStoreCustomerVisualClip>();
                var icon = icons[i];
                Assert.That(Read<RectTransform>(clip, "_complaintIcon"), Is.SameAs(icon.rectTransform));
                Assert.That(icon.transform.parent, Is.SameAs(portrait.transform));
                var data = Read<PopupStoreSpriteVisualBounds>(clip, "_bounds");
                Assert.That(data.TryGet(portrait.sprite, out var opaque), Is.True);
                var sourceSprite = icon.sprite; var portraitSprite = portrait.sprite;
                bool active = icon.gameObject.activeSelf, enabled = icon.enabled;
                var color = icon.color; var scale = icon.rectTransform.localScale;
                var portraitPosition = portrait.rectTransform.anchoredPosition;
                for (int repeat = 0; repeat < 5; repeat++) Assert.That(clip.Prepare(), Is.True);
                Rect local = Bounds(icon.rectTransform, portrait.rectTransform);
                float visibleHead = portrait.rectTransform.rect.yMin + opaque.yMax * portrait.rectTransform.rect.height;
                Assert.That(local.yMin, Is.EqualTo(visibleHead + 4).Within(.05f), "Icon must sit above visible head pixels, not above empty PNG padding.");
                Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(56, 56)));
                Assert.That(icon.sprite, Is.SameAs(sourceSprite)); Assert.That(portrait.sprite, Is.SameAs(portraitSprite));
                Assert.That(icon.gameObject.activeSelf, Is.EqualTo(active)); Assert.That(icon.enabled, Is.EqualTo(enabled));
                Assert.That(icon.color, Is.EqualTo(color)); Assert.That(icon.rectTransform.localScale, Is.EqualTo(scale));
                Assert.That(Vector2.Distance(portrait.rectTransform.anchoredPosition, portraitPosition), Is.LessThan(.001f));
            }
            var firstClip = host.Orders.Slots[0].CustomerImage.transform.parent.GetComponent<PopupStoreCustomerVisualClip>();
            var own = Read<RectTransform>(firstClip, "_complaintIcon");
            var foreign = icons[1].rectTransform;
            Vector2 foreignPosition = foreign.anchoredPosition, foreignSize = foreign.sizeDelta;
            Write(firstClip, "_complaintIcon", foreign);
            Assert.That(firstClip.Prepare(), Is.True);
            Assert.That(foreign.anchoredPosition, Is.EqualTo(foreignPosition));
            Assert.That(foreign.sizeDelta, Is.EqualTo(foreignSize));
            Write(firstClip, "_complaintIcon", own);
            Assert.That(changes, Is.Zero);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
            Assert.That(Enumerable.Range(0, 3).All(host.Entrance.IsWaiting), Is.True);
            CollectionAssert.AreEqual(remaining, Enumerable.Range(0, 3).Select(i => patience.GetTimer(i).RemainingPatience).ToArray());
            Assert.That(host.Root.GetComponent<PopupStoreBusinessClock>().RemainingSeconds, Is.EqualTo(businessRemaining));
            Assert.That(host.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    [Test]
    public void OrderContentLayoutRejectsForeignContentOrIconsBeforeChangingEitherOrder()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var first = host.Orders.Slots[0]; var second = host.Orders.Slots[1];
            var layout = first.OrderFrame.GetComponent<PopupStoreOrderVisualLayout>();
            var positions = host.Orders.Slots.SelectMany(slot => slot.MaterialIcons).Select(image => image.rectTransform.anchoredPosition).ToArray();
            Assert.That(layout.Apply(second.MaterialIcons), Is.False);
            var content = layout.ContentArea;
            Write(layout, "_contentArea", second.OrderFrame.GetComponent<PopupStoreOrderVisualLayout>().ContentArea);
            Assert.That(layout.Apply(first.MaterialIcons), Is.False);
            CollectionAssert.AreEqual(positions, host.Orders.Slots.SelectMany(slot => slot.MaterialIcons).Select(image => image.rectTransform.anchoredPosition).ToArray());
            Write(layout, "_contentArea", content);
            Assert.That(layout.Apply(first.MaterialIcons), Is.True);
            Assert.That(host.Counter.Count, Is.Zero);
        }
    }

    [Test]
    public void HideReopenAndRepeatedLayoutReusePageDotsWithoutResettingScrollOrBusinessState()
    {
        using (var host = new Host(1920, 1080, true))
        {
            var plush = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true).Single(list => Read<PopupStoreMaterialKind>(list, "_kind") == PopupStoreMaterialKind.Plush);
            var scroll = Read<ScrollRect>(plush, "_scrollRect");
            var dots = plush.GetComponentInChildren<PopupStoreScrollPageIndicator>(true);
            scroll.horizontalNormalizedPosition = 1; dots.Refresh();
            var ids = dots.GetComponentsInChildren<Image>(true).Select(image => image.GetInstanceID()).ToArray();
            var clock = host.Root.GetComponent<PopupStoreBusinessClock>();
            double remaining = clock.RemainingSeconds;
            string[] orderIds = Enumerable.Range(0, 3).Select(host.Orders.GetDisplayedOrderId).ToArray();
            for (int i = 0; i < 5; i++)
            {
                host.Root.SetActive(false);
                Assert.That(host.Root.activeInHierarchy, Is.False);
                Assert.That(dots.GetComponentsInChildren<Image>(true).All(image => !image.gameObject.activeInHierarchy && !image.isActiveAndEnabled), Is.True,
                    "No dot can render while its owning popup is hidden, regardless of GetComponentsInChildren traversal semantics.");
                // Synchronous EditMode hosts invoke lifecycle methods explicitly (as the existing
                // ResultTests host does); ordinary MonoBehaviour messages are not a Play loop here.
                typeof(PopupStoreScrollPageIndicator).GetMethod("OnDisable", Fields).Invoke(dots, null);
                Assert.That(Read<ScrollRect>(dots, "_boundScroll"), Is.Null, "Disable must remove the existing scroll subscription.");
                Assert.That(dots.PageCount, Is.Zero);
                Assert.That(dots.GetComponentsInChildren<Image>(true).All(image => !image.gameObject.activeSelf), Is.True,
                    "The actual disable handler also clears each dot's own visible state.");
                host.Root.SetActive(true);
                typeof(PopupStoreScrollPageIndicator).GetMethod("OnEnable", Fields).Invoke(dots, null);
                Assert.That(host.Root.GetComponent<PopupStoreFinalGuideLayout>().ApplyLayout(), Is.True);
                dots.Refresh();
                Assert.That(dots.PageCount, Is.EqualTo(7)); Assert.That(dots.CurrentPage, Is.EqualTo(6));
                CollectionAssert.AreEqual(ids, dots.GetComponentsInChildren<Image>(true).Select(image => image.GetInstanceID()).ToArray());
            }
            Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(1).Within(.001f));
            Assert.That(clock.RemainingSeconds, Is.EqualTo(remaining));
            CollectionAssert.AreEqual(orderIds, Enumerable.Range(0, 3).Select(host.Orders.GetDisplayedOrderId).ToArray());
            Assert.That(host.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            Assert.That(host.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void SplitHudAndSingleLineButtonsRenderRealisticLongValuesWithoutTouchingNeighbouringControls(int width, int height)
    {
        using (var host = new Host(width, height, true))
        {
            var score = Read<TMP_Text>(host.Root.GetComponent<PopupStoreScore>(), "_scoreText");
            var time = Read<TMP_Text>(host.Root.GetComponent<PopupStoreBusinessClock>(), "_timeText");
            var title = host.Root.transform.Find("HUD/Score Label").GetComponent<TMP_Text>();
            var timeTitle = host.Root.transform.Find("HUD/Time Label").GetComponent<TMP_Text>();
            var status = host.Root.transform.Find("HUD/Next Grade").GetComponent<TMP_Text>();
            AssertGuideRect(title.rectTransform, host.Rect, new Rect(394, 23, 66, 31), "score title");
            AssertGuideRect(timeTitle.rectTransform, host.Rect, new Rect(1274, 22, 151, 32), "time title");
            Assert.That(title.text, Is.EqualTo("점수")); Assert.That(timeTitle.text, Is.EqualTo("남은 시간"));
            score.text = "2,640"; time.text = "01:30";
            foreach (string value in new[] { "보상 없음", "다음 등급까지 2,200점", "최고 등급 SSS" })
            {
                status.text = value;
                foreach (var label in new[] { score, time, title, timeTitle, status }) label.ForceMeshUpdate(true, true);
                Assert.That(GlyphBounds(score, host.Rect).Overlaps(GlyphBounds(status, host.Rect)), Is.False, value);
                Assert.That(GlyphBounds(score, host.Rect).Overlaps(GlyphBounds(title, host.Rect)), Is.False);
                Assert.That(GlyphBounds(time, host.Rect).Overlaps(GlyphBounds(timeTitle, host.Rect)), Is.False);
                AssertInside(GlyphBounds(score, host.Rect), host.Rect.rect, "long score glyphs");
                AssertInside(GlyphBounds(status, host.Rect), host.Rect.rect, "grade glyphs");
            }
            foreach (var button in new[] { Read<Button>(host.Counter, "_clearButton"), Read<Button>(host.Root.GetComponent<PopupStorePacking>(), "_packButton") })
            {
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                label.ForceMeshUpdate(true, true);
                Assert.That(label.textInfo.lineCount, Is.EqualTo(1), label.text);
                AssertInside(GlyphBounds(label, button.GetComponent<RectTransform>()), button.GetComponent<RectTransform>().rect, label.text);
                Assert.That(label.raycastTarget, Is.False);
                var icon = button.transform.Find("Approved Action Icon").GetComponent<Image>();
                Assert.That(GlyphBounds(label, button.GetComponent<RectTransform>()).Overlaps(Bounds(icon.rectTransform, button.GetComponent<RectTransform>())), Is.False);
            }
            Assert.That(host.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero, "Text stress is isolated from actual accounting.");
            Assert.That(host.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    private static void AssertImport(string path, int width, int height, bool alpha)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null, path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.isReadable, Is.False);
        Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        Assert.That(importer.alphaIsTransparency, Is.EqualTo(alpha));
        Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(Math.Max(width, height)));
        Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Assert.That(sprites, Has.Length.EqualTo(1));
        Assert.That(sprites[0].rect.size, Is.EqualTo(new Vector2(width, height)), path + " no imported downsample");
        string guid = AssetDatabase.AssetPathToGUID(path);
        Assert.That(guid.Length, Is.EqualTo(32)); Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));
    }
    private static void AssertPngHeader(byte[] bytes, int colourType, string path)
    {
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray(), path);
        Assert.That(bytes[24], Is.EqualTo(8), path + " bit depth"); Assert.That(bytes[25], Is.EqualTo(colourType), path + " colour type");
    }
    private static Texture2D Decode(byte[] bytes)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Assert.That(ImageConversion.LoadImage(texture, bytes, false), Is.True); return texture;
    }
    internal static void AssertGuideRect(RectTransform child, RectTransform root, Rect expected, string message)
    {
        Rect actual = ToGuide(Bounds(child, root), root);
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(.25f), message + " x");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(.25f), message + " y");
        Assert.That(actual.width, Is.EqualTo(expected.width).Within(.25f), message + " width including approved transparent padding");
        Assert.That(actual.height, Is.EqualTo(expected.height).Within(.25f), message + " height including approved transparent padding");
    }
    private static Rect ToGuide(Rect local, RectTransform root)
    {
        float scale = Mathf.Min(root.rect.width / 1672f, root.rect.height / 941f);
        float ox = (root.rect.width - 1672 * scale) * .5f, oy = (root.rect.height - 941 * scale) * .5f;
        return new Rect((local.xMin - root.rect.xMin - ox) / scale, (root.rect.yMax - local.yMax - oy) / scale, local.width / scale, local.height / scale);
    }
    private static Image ImageFor(GameObject root, string id) => root.GetComponentsInChildren<Image>(true)
        .Single(image => AssetDatabase.GetAssetPath(image.sprite) == Art + id + ".png");
    private static bool IsAuthoredVisible(Transform item, Transform root)
    { for (var current = item; current != null && current != root; current = current.parent) if (!current.gameObject.activeSelf) return false; return item.IsChildOf(root); }
    private static Manifest LoadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Art + "active_manifest_v3.json"));
    private static GameObject Prefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static void Populate(Image image, VertexHelper mesh) => typeof(Image).GetMethod("OnPopulateMesh", Fields,
        null, new[] { typeof(VertexHelper) }, null).Invoke(image, new object[] { mesh });
    private static List<UIVertex> Vertices(VertexHelper mesh)
    { var result = new List<UIVertex>(); mesh.GetUIVertexStream(result); return result; }
    private static Rect MeshRect(List<UIVertex> vertices) => Rect.MinMaxRect(vertices.Min(v => v.position.x), vertices.Min(v => v.position.y), vertices.Max(v => v.position.x), vertices.Max(v => v.position.y));
    private static Rect GlyphBounds(TMP_Text text, RectTransform root)
    {
        var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(character => character.isVisible).ToArray();
        Assert.That(glyphs.Length, Is.GreaterThan(0), text.name);
        var corners = glyphs.SelectMany(character => new[] { character.bottomLeft, character.topRight })
            .Select(point => root.InverseTransformPoint(text.rectTransform.TransformPoint(point))).ToArray();
        return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
    }
    private static void AssertInside(Rect inside, Rect outside, string message)
    {
        Assert.That(inside.xMin, Is.GreaterThanOrEqualTo(outside.xMin - .1f), message);
        Assert.That(inside.yMin, Is.GreaterThanOrEqualTo(outside.yMin - .1f), message);
        Assert.That(inside.xMax, Is.LessThanOrEqualTo(outside.xMax + .1f), message);
        Assert.That(inside.yMax, Is.LessThanOrEqualTo(outside.yMax + .1f), message);
    }
    private static Rect Bounds(RectTransform child, RectTransform root)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners); var p = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x), p.Max(v => v.y));
    }
    private sealed class Host : IDisposable
    {
        private readonly IDisposable _inner;
        internal readonly GameObject Root;
        internal readonly RectTransform Rect;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal Host(int width, int height, bool start)
        {
            var type = typeof(PopupStoreApprovedUiTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = (IDisposable)Activator.CreateInstance(type, Fields, null, new object[] { width, height, start }, CultureInfo.InvariantCulture);
            Root = Read<GameObject>(_inner, "Root"); Rect = Read<RectTransform>(_inner, "Rect");
            View = Root.GetComponent<UIPopupStore>(); Counter = Read<PopupStoreSalesCounter>(_inner, "Counter");
            Orders = Root.GetComponent<PopupStoreOrderDisplay>(); Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
        }
        public void Dispose() => _inner.Dispose();
    }
}
#endif
