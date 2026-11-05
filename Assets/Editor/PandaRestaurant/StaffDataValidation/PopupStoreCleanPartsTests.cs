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

/// <summary>Approved v4 replacement contracts. Memory rendering is not actual GameView visual approval.</summary>
public sealed class PopupStoreCleanPartsTests
{
    internal const string Art = "Assets/Sprites/PopupStore/CleanPartsV4/";
    internal const string PreviousArt = "Assets/Sprites/PopupStore/FinalGuideV3/";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GachaCollectionPreviewSceneWitness _workspace;
    private UnityEngine.Random.State _random;
    private long _money, _encounter;
    private int _diamonds;
    private bool _backend;
    private float _timeScale;

    [Serializable] internal sealed class ReplacementMap
    {
        public string schema;
        public int[] guide_canvas;
        public int new_sprite_count, old_sprite_bindings_replaced;
        public Replacement[] replacements;
        public Retained[] unchanged_v3;
        public Background background;
        public Overlay new_overlay;
    }
    [Serializable] internal sealed class Replacement
    {
        public string old_id, old_sha256, replacement_id, replacement_file, replacement_sha256;
        public int[] target_guide_rect_xywh;
        public int new_padding_px;
    }
    [Serializable] internal sealed class Retained { public string id, file, sha256; }
    [Serializable] internal sealed class Background { public string original_sha256, replacement_file, replacement_sha256; public int[] size; public int modified_pixels, changes_outside_mask; }
    [Serializable] internal sealed class Overlay { public string new_id; public int[] target_guide_rect_xywh; }
    [Serializable] internal sealed class PartManifest { public string schema; public Part[] parts; }
    [Serializable] internal sealed class Part
    {
        public string id, file, sha256;
        public int[] native_content_size, png_size, nine_slice_lbrt_native;
        public int padding_px;
    }

    [SetUp] public void CaptureOriginalWorkspace()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture(); _random = UnityEngine.Random.state;
        _money = UserInfo.Money; _diamonds = UserInfo.Dia; _encounter = UserInfo.CustomerEncounterContextGeneration;
        _backend = BackEnd.Backend.IsInitialized; _timeScale = Time.timeScale;
    }
    [TearDown] public void PreserveWorkspaceRandomAndAccount()
    {
        _workspace.AssertUnchanged("Clean parts V4 memory tests");
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_encounter));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_backend)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
    }

    [Test]
    public void ApprovedMapContainsThirtyTwoReplacementsTenRetainedPartsAndOneSeparateSign()
    {
        Assert.That(Hash(Art + "replacement_map_v4.json"), Is.EqualTo("0bfe2007a60f684c9ae04a16c710deca60a2a256a735157d2f48452b04019c7b"));
        Assert.That(Hash(Art + "new_parts_manifest.json"), Is.EqualTo("ccb8b58dad013a025648c048f54535faa6b4151581f11d43685195f3b08cbe34"));
        var map = Map(); var manifest = Parts();
        Assert.That(manifest.schema, Is.EqualTo("popup-cleanparts-v4"));
        Assert.That(map.new_sprite_count, Is.EqualTo(24)); Assert.That(map.old_sprite_bindings_replaced, Is.EqualTo(32));
        Assert.That(map.replacements, Has.Length.EqualTo(32)); Assert.That(map.unchanged_v3, Has.Length.EqualTo(10));
        Assert.That(manifest.parts, Has.Length.EqualTo(24));
        CollectionAssert.AreEqual(new[] { 1672, 941 }, map.guide_canvas);
        CollectionAssert.AllItemsAreUnique(map.replacements.Select(p => p.old_id).ToArray());
        CollectionAssert.AllItemsAreUnique(manifest.parts.Select(p => p.id).ToArray());
        Assert.That(map.replacements.Select(p => p.replacement_id).Distinct().Count(), Is.EqualTo(23));
        CollectionAssert.AreEquivalent(manifest.parts.Select(p => p.id).ToArray(),
            map.replacements.Select(p => p.replacement_id).Distinct().Concat(new[] { map.new_overlay.new_id }).ToArray());
        Assert.That(map.new_overlay.new_id, Is.EqualTo("V4_Sign_RopesLeaves_EMPTY"));
        CollectionAssert.AreEqual(new[] { 1, 0, 237, 241 }, map.new_overlay.target_guide_rect_xywh);
    }

    [Test]
    public void AllTwentyFourApprovedPngsKeepSourceHashRealAlphaSixPixelPaddingAndFullResolutionImports()
    {
        foreach (var part in Parts().parts) AssertPart(part);
        CollectionAssert.AreEquivalent(Parts().parts.Select(p => p.id + ".png").Concat(new[] { "V4_Background_NoOldSign.png" }).ToArray(),
            Directory.GetFiles(Art, "*.png").Select(Path.GetFileName).ToArray());
    }

    [Test]
    public void SignRemovedBackgroundUsesOnlyTheApprovedDerivedBytesAtOriginalResolution() => AssertCurrentBackground();

    [Test]
    public void AllTenRetainedV3AssetsKeepBytesReferencesAndUniqueOriginalFiles()
    {
        var dependencies = EditorUtility.CollectDependencies(new Object[] { Prefab() });
        foreach (var part in Map().unchanged_v3)
        {
            string path = PreviousArt + part.id + ".png";
            Assert.That(Hash(path), Is.EqualTo(part.sha256), part.id);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null); Assert.That(dependencies, Does.Contain(sprite));
            Assert.That(File.Exists(Art + part.id + ".png"), Is.False, "Do not reimport the package's reference copies.");
        }
    }

    [Test]
    public void EveryOneOfTheThirtyTwoOldRolesUsesTheApprovedReplacementIncludingSharedSprites()
    {
        var root = Prefab();
        foreach (var row in Map().replacements)
        {
            var sprites = BoundSprites(root, row.old_id);
            Assert.That(sprites, Is.Not.Empty, row.old_id);
            foreach (var sprite in sprites)
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(Art + row.replacement_id + ".png"), row.old_id);
            Assert.That(Hash(Art + row.replacement_id + ".png"), Is.EqualTo(row.replacement_sha256));
        }
        Assert.That(BoundSprites(root, "FG_OrderAlert_01").Single(), Is.SameAs(BoundSprites(root, "FG_OrderAlert_03").Single()));
        Assert.That(BoundSprites(root, "FG_Arrow_Box_Left").Single(), Is.SameAs(BoundSprites(root, "FG_Arrow_Ribbon_Left").Single()));
    }

    [Test]
    public void SupersededV3PartsAndTheirMetasAreRemovedOnlyAlongTheCompleteApprovedReplacementTrace()
    {
        AssertLegacyReplacementTrace();
        var dependencies = AssetDatabase.GetDependencies(PrefabPath, true);
        foreach (var row in Map().replacements)
            Assert.That(dependencies, Does.Not.Contain(PreviousArt + row.old_id + ".png"));
        Assert.That(dependencies, Does.Not.Contain(PreviousArt + "FG_Background_CafeCounter_APPROVED.png"));
    }

    [Test]
    public void ExactlyOneDerivedBackgroundAndOneNewSignReplaceAllOldOverlayResidue()
    {
        var root = Prefab(); AssertCurrentDependencies(root);
        var background = root.transform.Find("Approved Front Background").GetComponent<Image>();
        var sign = root.transform.Find("Approved Clean Sign").GetComponent<Image>();
        Assert.That(background.transform.parent, Is.SameAs(root.transform));
        Assert.That(sign.transform.parent, Is.SameAs(root.transform));
        Assert.That(sign.transform.GetSiblingIndex(), Is.GreaterThan(background.transform.GetSiblingIndex()));
        Assert.That(sign.transform.GetSiblingIndex(), Is.LessThan(root.transform.Find("HUD").GetSiblingIndex()));
        foreach (string old in new[] { "Background Warm Veil", "Wood Counter Foreground", "Hanging Sign Caption" })
            Assert.That(root.transform.Find(old), Is.Null, "Remove obsolete popup-only overlays rather than leave disabled residue.");
        Assert.That(sign.GetComponent<Selectable>(), Is.Null);
        // DEV-GAME-01 adds approved independent lettering without restoring any obsolete overlay.
        var lettering = sign.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(lettering, Has.Length.EqualTo(1));
        Assert.That(lettering[0].gameObject.name, Is.EqualTo("Popup Store Sign Text"));
        Assert.That(lettering[0].text, Is.EqualTo("POPUP\nSTORE"));
        Assert.That(lettering[0].raycastTarget, Is.False);
        Assert.That(AssetDatabase.GetAssetPath(lettering[0].font), Is.EqualTo("Assets/TextMesh Pro/Fonts/영덕대게체.asset"));
    }

    [Test]
    public void NewColoredPartsUseWhiteTintAndKeepOnlyOriginalInteractiveTargets()
    {
        var root = Prefab();
        var images = root.GetComponentsInChildren<Image>(true).Where(i => AssetDatabase.GetAssetPath(i.sprite).StartsWith(Art, StringComparison.Ordinal)).ToArray();
        Assert.That(images.Length, Is.GreaterThanOrEqualTo(25));
        foreach (var image in images)
        {
            Assert.That(image.color, Is.EqualTo(Color.white), image.name);
            if (image.name == "Previous Page" || image.name == "Next Page")
                Assert.That(image.GetComponent<Button>(), Is.Not.Null);
            else { Assert.That(image.raycastTarget, Is.False, image.name); Assert.That(image.GetComponent<Selectable>(), Is.Null, image.name); }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void CleanPartsFitApprovedContentCoordinatesWithoutMovingExistingMaterialsCustomersOrButtons(int width, int height)
    {
        using (var h = new Host(width, height, true))
        {
            AssertLayout(h.Root, h.Rect);
            var body = new[] { new Rect(315, 306, 215, 183), new Rect(722, 325, 223, 165), new Rect(1151, 295, 225, 192) };
            for (int i = 0; i < 3; i++)
                PopupStoreFinalGuideTests.AssertGuideRect(h.Root.transform.Find("Customer Place " + (i + 1) + "/Approved Body Area").GetComponent<RectTransform>(), h.Rect, body[i], "original customer body area");
            PopupStoreFinalGuideTests.AssertGuideRect(h.Root.transform.Find("Sales Counter - Five Slots/Approved Tray").GetComponent<RectTransform>(), h.Rect,
                new Rect(382, 526, 910, 175), "original tray");
            PopupStoreFinalGuideTests.AssertGuideRect(h.Root.transform.Find("HUD/Close Button").GetComponent<RectTransform>(), h.Rect,
                new Rect(1549, 19, 88, 84), "original X");
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    public void ThreeFourFiveOrdersKeepEveryOriginalMaterialAndDuplicatePlushInsideEmptyFrames(int width, int height)
    {
        using (var h = new Host(width, height, true))
        {
            for (int s = 0; s < 3; s++)
            {
                var slot = h.Orders.Slots[s]; var materials = h.Orders.GetDisplayedMaterials(s);
                Assert.That(materials.Count, Is.EqualTo(3 + s));
                var content = slot.OrderFrame.GetComponent<PopupStoreOrderVisualLayout>().ContentArea;
                for (int i = 0; i < materials.Count; i++)
                {
                    Assert.That(slot.MaterialIcons[i].sprite, Is.SameAs(materials[i].Sprite));
                    AssertInside(Bounds(slot.MaterialIcons[i].rectTransform, content), content.rect, "real icon inside original content");
                    if (i > 0) Assert.That(Bounds(slot.MaterialIcons[i - 1].rectTransform, content).xMax, Is.LessThan(Bounds(slot.MaterialIcons[i].rectTransform, content).xMin));
                }
                Assert.That(slot.MaterialIcons.Count(i => i.gameObject.activeSelf), Is.EqualTo(materials.Count));
            }
            Assert.That(h.Orders.Slots[1].MaterialIcons[0].sprite, Is.SameAs(h.Orders.Slots[1].MaterialIcons[1].sprite));
            Assert.That(h.Orders.Slots[1].MaterialIcons[0], Is.Not.SameAs(h.Orders.Slots[1].MaterialIcons[1]));
        }
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(5)]
    public void ExistingCounterZeroThreeAndFiveStatesUseOriginalTrayAndOriginalSelectionData(int count)
    {
        using (var h = new Host(1920, 1080, true))
        {
            var catalog = Read<PopupStoreMaterialCatalog>(h.Counter, "_catalog");
            string[] ids = { "STAFF01", "STAFF01", "BOX_PINK", "RIBBON_YELLOW", "STAFF02" };
            var materials = new List<PopupStoreMaterialEntry>();
            foreach (PopupStoreMaterialKind kind in Enum.GetValues(typeof(PopupStoreMaterialKind)))
            { Assert.That(catalog.TryGetMaterials(kind, out var entries, out string error), Is.True, error); materials.AddRange(entries); }
            foreach (string id in ids.Take(count)) Assert.That(h.Counter.TryAdd(materials.Single(m => m.Id == id)), Is.True);
            Assert.That(h.Counter.Count, Is.EqualTo(count));
            CollectionAssert.AreEqual(ids.Take(count).ToArray(), h.Counter.Materials.Select(m => m.Id).ToArray());
            var icons = Read<Image[]>(h.Counter, "_slotIcons");
            for (int i = 0; i < 5; i++) Assert.That(icons[i].gameObject.activeSelf, Is.EqualTo(i < count));
            Read<Button>(h.Counter, "_clearButton").onClick.Invoke(); Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    [TestCase(0f)]
    [TestCase(.01f)]
    [TestCase(.5f)]
    [TestCase(1f)]
    public void PatienceColorsAndGradeFillStayInsideTheirInnerChannelsAtZeroOneFiftyAndHundredPercent(float amount)
    {
        using (var h = new Host(1920, 1080, true))
        {
            var patience = h.Root.GetComponent<PopupStoreCustomerPatience>();
            foreach (var slot in Read<PopupStorePatienceSlot[]>(patience, "_slots"))
            {
                var fill = slot.Fill; var track = fill.transform.parent.GetComponent<Image>();
                var before = fill.rectTransform.rect;
                foreach (string field in new[] { "_normalSprite", "_cautionSprite", "_urgentSprite" })
                {
                    fill.sprite = Read<Sprite>(patience, field); fill.fillAmount = amount;
                    using (var mesh = new VertexHelper())
                    {
                        Populate(fill, mesh);
                        var effect = fill.GetComponent<PopupStorePaddedFillVisual>(); Assert.That(effect, Is.Not.Null);
                        effect.ModifyMesh(mesh); var vertices = Vertices(mesh);
                        if (amount == 0) Assert.That(vertices, Is.Empty);
                        else
                        {
                            var local = MeshRect(vertices);
                            var world = TransformRect(local, fill.rectTransform, h.Rect);
                            var guide = ToGuide(world, h.Rect);
                            Assert.That(guide.width, Is.EqualTo(165 * amount).Within(.1f)); Assert.That(guide.height, Is.EqualTo(21).Within(.1f));
                            AssertInside(TransformRect(local, fill.rectTransform, track.rectTransform), track.rectTransform.rect, "fill never crosses frame");
                            Assert.That(guide.width, Is.GreaterThan(0)); Assert.That(guide.height, Is.GreaterThan(0));
                        }
                    }
                    Assert.That(fill.rectTransform.rect, Is.EqualTo(before));
                }
            }
            var gradeFill = h.Root.transform.Find("HUD/Grade Progress Track/Grade Progress Fill").GetComponent<Image>();
            gradeFill.fillAmount = amount;
            using (var mesh = new VertexHelper())
            {
                Populate(gradeFill, mesh);
                var effect = gradeFill.GetComponent<PopupStorePaddedFillVisual>(); Assert.That(effect, Is.Not.Null);
                effect.ModifyMesh(mesh); var vertices = Vertices(mesh);
                if (amount == 0) Assert.That(vertices, Is.Empty);
                else
                {
                    Rect rendered = ToGuide(TransformRect(MeshRect(vertices), gradeFill.rectTransform, h.Rect), h.Rect);
                    Assert.That(rendered.width, Is.EqualTo(434 * amount).Within(.1f)); Assert.That(rendered.height, Is.EqualTo(21).Within(.1f));
                    Assert.That(rendered.x, Is.EqualTo(569).Within(.1f)); Assert.That(rendered.y, Is.EqualTo(72).Within(.1f));
                }
            }
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    [Test]
    public void ThreeIndependentBlueClocksRemainOutsideTrackChildrenWithNoDuplicateClockImage()
    {
        var root = Prefab();
        var blue = root.GetComponentsInChildren<Image>(true).Where(i => AssetDatabase.GetAssetPath(i.sprite) == Art + "V4_Clock_Blue.png").ToArray();
        var pink = root.GetComponentsInChildren<Image>(true).Where(i => AssetDatabase.GetAssetPath(i.sprite) == Art + "V4_Clock_Pink.png").ToArray();
        Assert.That(blue, Has.Length.EqualTo(3)); Assert.That(pink, Has.Length.EqualTo(1));
        foreach (var clock in blue)
        {
            Assert.That(clock.transform.parent.name, Is.EqualTo("Patience Gauge")); Assert.That(clock.preserveAspect, Is.True);
            Assert.That(clock.transform.GetSiblingIndex(), Is.GreaterThan(clock.transform.parent.Find("Gauge Track").GetSiblingIndex()));
        }
        Assert.That(pink[0].preserveAspect, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SpeechFrameKeepsBothCornersAndCentralTailAtOneUniformScaleWithContinuousUvs(bool wide)
    {
        using (var h = new Host(1920, 1080, true))
        {
            var image = h.Orders.Slots[wide ? 1 : 0].OrderFrame;
            var effect = image.GetComponent<PopupStoreSpeechFrameVisual>(); Assert.That(effect, Is.Not.Null);
            Assert.That(image.type, Is.EqualTo(Image.Type.Simple)); Assert.That(image.preserveAspect, Is.False);
            var sprite = image.sprite; var imageRect = image.GetPixelAdjustedRect();
            using (var mesh = new VertexHelper())
            {
                Populate(image, mesh); effect.ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.EqualTo(20));
                var strips = new List<UIVertex>();
                for (int i = 0; i < mesh.currentVertCount; i++) { UIVertex v = default(UIVertex); mesh.PopulateUIVertex(ref v, i); strips.Add(v); }
                float scale = imageRect.height / sprite.rect.height;
                float Width(int strip) => strips.Skip(strip * 4).Take(4).Max(v => v.position.x) - strips.Skip(strip * 4).Take(4).Min(v => v.position.x);
                Assert.That(Width(0), Is.EqualTo(64 * scale).Within(.01f));
                Assert.That(Width(2), Is.EqualTo(82 * scale).Within(.01f));
                Assert.That(Width(4), Is.EqualTo(64 * scale).Within(.01f));
                for (int strip = 1; strip < 5; strip++)
                {
                    var left = strips.Skip((strip - 1) * 4).Take(4); var right = strips.Skip(strip * 4).Take(4);
                    Assert.That(left.Max(v => v.position.x), Is.EqualTo(right.Min(v => v.position.x)).Within(.001f));
                    Assert.That(left.Max(v => v.uv0.x), Is.EqualTo(right.Min(v => v.uv0.x)).Within(.00001f));
                }
                Assert.That(image.sprite, Is.SameAs(sprite)); AssertInside(MeshRect(Vertices(mesh)), imageRect, "all strips fit frame");
            }
        }
    }

    [Test]
    public void NarrowSpeechFrameUsesNonOverlappingContainInsteadOfNegativeStretchBands()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var image = h.Orders.Slots[0].OrderFrame; image.rectTransform.sizeDelta = new Vector2(50, 140);
            using (var mesh = new VertexHelper())
            {
                Populate(image, mesh); image.GetComponent<PopupStoreSpeechFrameVisual>().ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.EqualTo(4));
                var bounds = MeshRect(Vertices(mesh)); AssertInside(bounds, image.rectTransform.rect, "narrow safe fallback");
                Assert.That(bounds.width / bounds.height, Is.EqualTo(image.sprite.rect.width / image.sprite.rect.height).Within(.001f));
            }
        }
    }

    [Test]
    public void SpeechFrameEffectNeverRewritesMismatchedSpriteOrSlicedInput()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var image = h.Orders.Slots[0].OrderFrame; var effect = image.GetComponent<PopupStoreSpeechFrameVisual>();
            foreach (bool mismatched in new[] { true, false })
            {
                image.sprite = mismatched ? h.Orders.Slots[1].OrderFrame.sprite : Read<Sprite>(effect, "_sourceSprite");
                image.type = mismatched ? Image.Type.Simple : Image.Type.Sliced;
                using (var mesh = new VertexHelper())
                {
                    Populate(image, mesh); var before = Vertices(mesh); effect.ModifyMesh(mesh); var after = Vertices(mesh);
                    Assert.That(after.Count, Is.EqualTo(before.Count));
                    for (int i = 0; i < before.Count; i++) { Assert.That(after[i].position, Is.EqualTo(before[i].position)); Assert.That(after[i].uv0, Is.EqualTo(before[i].uv0)); }
                }
            }
        }
    }

    [Test]
    public void FramesTabsAndTracksHaveBordersWhileAspectOnlyPartsHaveNoSlicedBorder()
    {
        foreach (var part in Parts().parts)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + part.id + ".png");
            var expected = part.nine_slice_lbrt_native;
            if (expected == null || expected.Length == 0) Assert.That(sprite.border, Is.EqualTo(Vector4.zero), part.id);
            else Assert.That(sprite.border, Is.EqualTo(new Vector4(expected[0], expected[1], expected[2], expected[3])), part.id);
        }
    }

    [Test]
    public void ExistingActualScrollPositionsStillDriveDotsAndShortListsDoNotGainExamplePages()
    {
        using (var h = new Host(1920, 1080, true))
        {
            foreach (var list in h.Root.GetComponentsInChildren<PopupStoreMaterialList>(true))
            {
                var indicator = list.GetComponentInChildren<PopupStoreScrollPageIndicator>(true); indicator.Refresh();
                var kind = Read<PopupStoreMaterialKind>(list, "_kind");
                if (kind == PopupStoreMaterialKind.Plush)
                {
                    Assert.That(indicator.PageCount, Is.EqualTo(7));
                    Read<Button>(list, "_nextButton").onClick.Invoke(); indicator.Refresh(); Assert.That(indicator.CurrentPage, Is.EqualTo(1));
                    Assert.That(indicator.GetComponentsInChildren<Image>(false).Count(i => AssetDatabase.GetAssetPath(i.sprite) == Art + "V4_Dot_Pink_Active.png"), Is.EqualTo(1));
                }
                else { Assert.That(indicator.PageCount, Is.Zero); Assert.That(Read<Button>(list, "_nextButton").interactable, Is.False); }
            }
            Assert.That(h.Counter.Count, Is.Zero); Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    [Test]
    public void ZeroSalesAndDisplayRefreshKeepNoneNoRewardDecisionAndNoImageBasedExampleValues()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var grade = h.Root.GetComponent<PopupStoreGradeSession>(); var rewards = h.Root.GetComponent<PopupStoreRewardSession>();
            for (int i = 0; i < 5; i++) { h.Root.GetComponent<PopupStoreFinalGuideLayout>().ApplyLayout(); h.Orders.Refresh(); }
            Assert.That(grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.None)); Assert.That(grade.FinalResult, Is.Null);
            Assert.That(grade.FinalizationCount, Is.Zero); Assert.That(rewards.RandomDrawCount, Is.Zero);
            Assert.That(h.Root.transform.Find("HUD/Current Grade Reward").GetComponent<Image>().enabled, Is.False);
            Assert.That(h.Root.transform.Find("HUD/Next Grade").GetComponent<TMP_Text>().text, Is.EqualTo("보상 없음"));
            Assert.That(h.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreBusinessClock>().RemainingSeconds, Is.EqualTo(90));
        }
    }

    [Test]
    public void EmptySlotAndHudDecorationComponentsAreRemovedWithoutRemovingTheirOwnedTransformsOrIcons()
    {
        var root = Prefab(); var hud = root.transform.Find("HUD");
        Assert.That(hud.GetComponent<Image>(), Is.Null); Assert.That(hud.GetComponent<CanvasRenderer>(), Is.Null);
        var icons = Read<Image[]>(root.GetComponentInChildren<PopupStoreSalesCounter>(true), "_slotIcons");
        Assert.That(icons, Has.Length.EqualTo(5));
        for (int i = 0; i < 5; i++)
        {
            var slot = root.transform.Find("Sales Counter - Five Slots/Empty Slot " + (i + 1));
            Assert.That(slot, Is.Not.Null); Assert.That(slot.GetComponent<RectTransform>(), Is.Not.Null);
            Assert.That(slot.GetComponent<Image>(), Is.Null); Assert.That(slot.GetComponent<CanvasRenderer>(), Is.Null);
            Assert.That(icons[i].transform.parent, Is.SameAs(slot)); Assert.That(icons[i].GetComponent<PopupStoreMaterialVisual>(), Is.Not.Null);
        }
    }

    [Test]
    public void AspectOnlyClocksArrowsAndSignRenderUniformMeshesInsideTheirApprovedInputRectangles()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var images = h.Root.GetComponentsInChildren<Image>(true).Where(image =>
            {
                string path = AssetDatabase.GetAssetPath(image.sprite);
                return path.StartsWith(Art + "V4_Clock_", StringComparison.Ordinal)
                    || path.StartsWith(Art + "V4_Arrow_", StringComparison.Ordinal) || path == Art + "V4_Sign_RopesLeaves_EMPTY.png";
            }).ToArray();
            Assert.That(images, Has.Length.EqualTo(11));
            foreach (var image in images)
            {
                Assert.That(image.preserveAspect, Is.True); Assert.That(image.type, Is.EqualTo(Image.Type.Simple));
                using (var mesh = new VertexHelper())
                {
                    Populate(image, mesh); var bounds = MeshRect(Vertices(mesh));
                    AssertInside(bounds, image.rectTransform.rect, image.name);
                    Assert.That(bounds.width / bounds.height, Is.EqualTo(image.sprite.rect.width / image.sprite.rect.height).Within(.002f), image.name);
                }
            }
        }
    }

    [Test]
    public void SlicedFrameCornersUseOneHeightScaleAndOnlyTheirEmptyMiddleSpansStretch()
    {
        using (var h = new Host(1920, 1080, true))
        {
            foreach (var row in Map().replacements.Where(r => r.old_id.Contains("Panel_") || r.old_id.Contains("Tab_") || r.old_id.StartsWith("FG_HUD_", StringComparison.Ordinal)))
            {
                var image = MappedImages(h.Root, row.old_id).Single();
                var part = Parts().parts.Single(p => p.id == row.replacement_id);
                float scale = row.target_guide_rect_xywh[3] / (float)part.native_content_size[1];
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1 / scale).Within(.0001f));
                using (var mesh = new VertexHelper())
                {
                    Populate(image, mesh); var vertices = Vertices(mesh);
                    AssertInside(MeshRect(vertices), image.rectTransform.rect, row.old_id);
                    var xs = vertices.Select(v => v.position.x).Distinct().OrderBy(v => v).ToArray();
                    var ys = vertices.Select(v => v.position.y).Distinct().OrderBy(v => v).ToArray();
                    Assert.That(xs.Length, Is.GreaterThanOrEqualTo(4)); Assert.That(ys.Length, Is.GreaterThanOrEqualTo(4));
                    Assert.That(xs[1] - xs[0], Is.EqualTo(image.sprite.border.x * scale).Within(.05f), row.old_id + " left corner");
                    Assert.That(ys[1] - ys[0], Is.EqualTo(image.sprite.border.y * scale).Within(.05f), row.old_id + " bottom corner");
                    Assert.That(xs[xs.Length - 1] - xs[xs.Length - 2], Is.EqualTo(image.sprite.border.z * scale).Within(.05f), row.old_id + " right corner");
                }
            }
        }
    }

    internal static ReplacementMap Map() => JsonUtility.FromJson<ReplacementMap>(File.ReadAllText(Art + "replacement_map_v4.json"));
    internal static PartManifest Parts() => JsonUtility.FromJson<PartManifest>(File.ReadAllText(Art + "new_parts_manifest.json"));
    internal static void AssertLegacyReplacementTrace()
    {
        var map = Map();
        Assert.That(map.replacements, Has.Length.EqualTo(32)); Assert.That(map.unchanged_v3, Has.Length.EqualTo(10));
        foreach (var row in map.replacements)
        {
            Assert.That(File.Exists(PreviousArt + row.old_id + ".png"), Is.False, row.old_id + " obsolete runtime PNG");
            Assert.That(File.Exists(PreviousArt + row.old_id + ".png.meta"), Is.False, row.old_id + " obsolete meta");
            Assert.That(Hash(Art + row.replacement_id + ".png"), Is.EqualTo(row.replacement_sha256));
            Assert.That(Parts().parts.Single(p => p.id == row.replacement_id).sha256, Is.EqualTo(row.replacement_sha256));
        }
        foreach (var keep in map.unchanged_v3) Assert.That(Hash(PreviousArt + keep.id + ".png"), Is.EqualTo(keep.sha256));
        Assert.That(File.Exists(PreviousArt + "FG_Background_CafeCounter_APPROVED.png"), Is.False);
        Assert.That(File.Exists(PreviousArt + "FG_Background_CafeCounter_APPROVED.png.meta"), Is.False);
        Assert.That(Hash(Art + "V4_Background_NoOldSign.png"), Is.EqualTo(map.background.replacement_sha256));
        var expected = map.unchanged_v3.Select(p => p.id + ".png").ToArray();
        CollectionAssert.AreEquivalent(expected, Directory.GetFiles(PreviousArt, "*.png").Select(Path.GetFileName).ToArray());
    }
    internal static void AssertCurrentBackground()
    {
        var source = Map().background; string path = Art + "V4_Background_NoOldSign.png";
        Assert.That(source.original_sha256, Is.EqualTo("ab3a83e125c47b375ae3b2f41360d80b9b12178e9e7e210d5620beb753c8f6dc"));
        Assert.That(source.replacement_sha256, Is.EqualTo("c14f16e8aee392ca70b6101eeabd6629c9488e801eb021ba789353dd78d719d3"));
        Assert.That(Hash(path), Is.EqualTo(source.replacement_sha256));
        Assert.That(source.modified_pixels, Is.EqualTo(24414)); Assert.That(source.changes_outside_mask, Is.Zero);
        var texture = Decode(path);
        try { Assert.That(texture.width, Is.EqualTo(1672)); Assert.That(texture.height, Is.EqualTo(941)); Assert.That(texture.GetPixels32().All(p => p.a == 255), Is.True); }
        finally { Object.DestroyImmediate(texture); }
        AssertImport(path, 1672, 941);
    }
    internal static void AssertPart(Part part)
    {
        string path = Art + part.id + ".png"; byte[] bytes = File.ReadAllBytes(path);
        Assert.That(Hash(path), Is.EqualTo(part.sha256)); Assert.That(bytes[24], Is.EqualTo(8)); Assert.That(bytes[25], Is.EqualTo(6));
        Assert.That(part.padding_px, Is.EqualTo(6));
        var texture = Decode(path);
        try
        {
            Assert.That(texture.width, Is.EqualTo(part.png_size[0])); Assert.That(texture.height, Is.EqualTo(part.png_size[1]));
            Assert.That(texture.width, Is.EqualTo(part.native_content_size[0] + 12)); Assert.That(texture.height, Is.EqualTo(part.native_content_size[1] + 12));
            var pixels = texture.GetPixels32(); Assert.That(pixels.Any(p => p.a == 0) && pixels.Any(p => p.a == 255), Is.True);
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                if (x < 6 || y < 6 || x >= texture.width - 6 || y >= texture.height - 6) Assert.That(pixels[y * texture.width + x].a, Is.Zero, part.id + " transparent padding");
        }
        finally { Object.DestroyImmediate(texture); }
        AssertImport(path, part.png_size[0], part.png_size[1]);
    }
    internal static void AssertCurrentDependencies(GameObject root)
    {
        var dependencies = EditorUtility.CollectDependencies(new Object[] { root }).OfType<Sprite>().Select(AssetDatabase.GetAssetPath).ToArray();
        foreach (var part in Parts().parts) Assert.That(dependencies, Does.Contain(Art + part.id + ".png"));
        foreach (var retained in Map().unchanged_v3) Assert.That(dependencies, Does.Contain(PreviousArt + retained.id + ".png"));
        var images = root.GetComponentsInChildren<Image>(true);
        Assert.That(images.Count(i => AssetDatabase.GetAssetPath(i.sprite) == Art + "V4_Background_NoOldSign.png"), Is.EqualTo(1));
        Assert.That(images.Count(i => AssetDatabase.GetAssetPath(i.sprite) == Art + "V4_Sign_RopesLeaves_EMPTY.png"), Is.EqualTo(1));
        foreach (string path in dependencies)
        {
            foreach (string forbidden in new[] { "/03_Reference/", "/05_Review/", "SOURCE_ONLY", "OCCLUSION_MASK", "FINAL_GUIDE_UNCHANGED", "PopupStore_BG_FrontView_Approved" })
                Assert.That(path, Does.Not.Contain(forbidden), "No reference sheet or obsolete background can be a runtime sprite.");
        }
    }
    internal static void AssertLayout(GameObject root, RectTransform rect)
    {
        foreach (var row in Map().replacements)
        {
            if (row.old_id.Contains("Fill_") || row.old_id.Contains("Dot_") || row.old_id == "FG_Icon_ClockBlue" || row.old_id == "FG_Patience_Track") continue;
            var image = MappedImages(root, row.old_id).Single(); AssertMappedRect(image, rect, row);
        }
        var sign = root.transform.Find("Approved Clean Sign").GetComponent<Image>();
        AssertMappedRect(sign, rect, new Replacement { replacement_id = Map().new_overlay.new_id, target_guide_rect_xywh = Map().new_overlay.target_guide_rect_xywh });
        PopupStoreFinalGuideTests.AssertGuideRect(root.transform.Find("Approved Front Background").GetComponent<RectTransform>(), rect, new Rect(0, 0, 1672, 941), "background");
    }
    internal static void AssertMappedRect(Image image, RectTransform root, Replacement row)
    {
        var part = Parts().parts.Single(p => p.id == row.replacement_id); var target = RectOf(row.target_guide_rect_xywh);
        bool aspect = part.nine_slice_lbrt_native == null || part.nine_slice_lbrt_native.Length == 0;
        if (part.id.Contains("OrderBubble")) aspect = false;
        float scale = target.height / part.native_content_size[1];
        if (aspect) scale = Mathf.Min(target.width / part.native_content_size[0], scale);
        // Aspect-only Images preserve the approved input rectangle. Unity contains their actual
        // mesh within it; the dedicated aspect test verifies that mesh independently.
        Rect content = target;
        float padding = 6 * scale;
        PopupStoreFinalGuideTests.AssertGuideRect(image.rectTransform, root, new Rect(content.x - padding, content.y - padding, content.width + 2 * padding, content.height + 2 * padding), row.replacement_id);
        Assert.That(image.preserveAspect, Is.EqualTo(aspect), row.replacement_id);
        if (part.nine_slice_lbrt_native != null && part.nine_slice_lbrt_native.Length > 0)
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), row.replacement_id);
    }
    internal static Image[] MappedImages(GameObject root, string id)
    {
        string path = null;
        if (id == "FG_HUD_ScoreFrame") path = "HUD/Approved Score Frame";
        else if (id == "FG_HUD_TimerFrame") path = "HUD/Approved Timer Frame";
        else if (id == "FG_Grade_Track") path = "HUD/Grade Progress Track";
        else if (id == "FG_Grade_Fill_Gold_DERIVED") path = "HUD/Grade Progress Track/Grade Progress Fill";
        else if (id == "FG_Icon_ClockPink") path = "HUD/Approved Pink Clock";
        else if (id.StartsWith("FG_OrderBubble_", StringComparison.Ordinal)) path = "Customer Place " + int.Parse(id.Substring(id.Length - 2), CultureInfo.InvariantCulture) + "/Order Balloon";
        else if (id.StartsWith("FG_OrderAlert_", StringComparison.Ordinal)) path = "Customer Place " + int.Parse(id.Substring(id.Length - 2), CultureInfo.InvariantCulture) + "/Order Balloon/Approved Order Alert";
        else if (id.StartsWith("FG_Panel_", StringComparison.Ordinal)) path = id.Substring(9) + " Panel";
        else if (id.StartsWith("FG_Tab_", StringComparison.Ordinal)) path = id.Substring(7).Replace("_Blank", "") + " Panel/Approved Category Tab";
        else if (id.StartsWith("FG_Arrow_", StringComparison.Ordinal)) { var pieces = id.Split('_'); path = pieces[2] + " Panel/" + (pieces[3] == "Left" ? "Previous Page" : "Next Page"); }
        else if (id == "FG_Icon_ClockBlue" || id == "FG_Patience_Track")
            return Enumerable.Range(1, 3).Select(i => root.transform.Find("Customer Place " + i + "/Patience Gauge/" + (id == "FG_Icon_ClockBlue" ? "Approved Blue Clock" : "Gauge Track")).GetComponent<Image>()).ToArray();
        Assert.That(path, Is.Not.Null, id + " must have an explicit owned role");
        var item = root.transform.Find(path); Assert.That(item, Is.Not.Null, path); return new[] { item.GetComponent<Image>() };
    }
    internal static Sprite[] BoundSprites(GameObject root, string id)
    {
        if (id.StartsWith("FG_Patience_Fill_", StringComparison.Ordinal))
        {
            string field = id.EndsWith("Green", StringComparison.Ordinal) ? "_normalSprite" : id.EndsWith("Yellow", StringComparison.Ordinal) ? "_cautionSprite" : "_urgentSprite";
            return new[] { Read<Sprite>(root.GetComponent<PopupStoreCustomerPatience>(), field) };
        }
        if (id.StartsWith("FG_Dot_", StringComparison.Ordinal))
        {
            string panel = id.Contains("Pink") ? "Plush" : id.Contains("Blue") ? "Box" : "Ribbon";
            var indicator = root.transform.Find(panel + " Panel/Actual Scroll Pages").GetComponent<PopupStoreScrollPageIndicator>();
            return new[] { Read<Sprite>(indicator, id.EndsWith("Inactive", StringComparison.Ordinal) ? "_inactiveSprite" : "_activeSprite") };
        }
        return MappedImages(root, id).Select(i => i.sprite).ToArray();
    }
    private static void AssertImport(string path, int width, int height)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter; Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite)); Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.isReadable, Is.False); Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100)); Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed)); Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(Math.Max(width, height)));
        Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray(); Assert.That(sprites, Has.Length.EqualTo(1));
        Assert.That(sprites[0].rect.size, Is.EqualTo(new Vector2(width, height)));
        string guid = AssetDatabase.AssetPathToGUID(path); Assert.That(guid.Length, Is.EqualTo(32)); Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));
    }
    private static Rect RectOf(int[] r) => new Rect(r[0], r[1], r[2], r[3]);
    private static GameObject Prefab() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    private static Texture2D Decode(string path) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True); return texture; }
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static void Populate(Image image, VertexHelper mesh) => typeof(Image).GetMethod("OnPopulateMesh", Fields, null, new[] { typeof(VertexHelper) }, null).Invoke(image, new object[] { mesh });
    private static List<UIVertex> Vertices(VertexHelper mesh) { var list = new List<UIVertex>(); mesh.GetUIVertexStream(list); return list; }
    private static Rect MeshRect(List<UIVertex> vertices) => Rect.MinMaxRect(vertices.Min(v => v.position.x), vertices.Min(v => v.position.y), vertices.Max(v => v.position.x), vertices.Max(v => v.position.y));
    private static Rect Bounds(RectTransform child, RectTransform root) { var corners = new Vector3[4]; child.GetWorldCorners(corners); var p = corners.Select(root.InverseTransformPoint).ToArray(); return Rect.MinMaxRect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x), p.Max(v => v.y)); }
    private static Rect TransformRect(Rect rect, RectTransform child, RectTransform root)
    { var p = new[] { rect.min, rect.max }.Select(v => root.InverseTransformPoint(child.TransformPoint(v))).ToArray(); return Rect.MinMaxRect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x), p.Max(v => v.y)); }
    private static Rect ToGuide(Rect local, RectTransform root)
    { float scale = Mathf.Min(root.rect.width / 1672f, root.rect.height / 941f); float ox = (root.rect.width - 1672 * scale) / 2, oy = (root.rect.height - 941 * scale) / 2; return new Rect((local.xMin - root.rect.xMin - ox) / scale, (root.rect.yMax - local.yMax - oy) / scale, local.width / scale, local.height / scale); }
    private static void AssertInside(Rect inside, Rect outside, string message)
    { Assert.That(inside.xMin, Is.GreaterThanOrEqualTo(outside.xMin - .1f), message); Assert.That(inside.xMax, Is.LessThanOrEqualTo(outside.xMax + .1f), message); Assert.That(inside.yMin, Is.GreaterThanOrEqualTo(outside.yMin - .1f), message); Assert.That(inside.yMax, Is.LessThanOrEqualTo(outside.yMax + .1f), message); }
    private sealed class Host : IDisposable
    {
        private readonly IDisposable _inner;
        internal readonly GameObject Root;
        internal readonly RectTransform Rect;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal Host(int width, int height, bool start)
        {
            var type = typeof(PopupStoreApprovedUiTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = (IDisposable)Activator.CreateInstance(type, Fields, null, new object[] { width, height, start }, CultureInfo.InvariantCulture);
            Root = Read<GameObject>(_inner, "Root"); Rect = Read<RectTransform>(_inner, "Rect"); Counter = Read<PopupStoreSalesCounter>(_inner, "Counter"); Orders = Root.GetComponent<PopupStoreOrderDisplay>();
        }
        public void Dispose() => _inner.Dispose();
    }
}
#endif
