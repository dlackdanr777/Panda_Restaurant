#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises the production furniture, habitat and brain in an account-free preview scene.</summary>
public sealed class ThirdFloorFairyPlayAreaTests
{
    [Test]
    public void DerivedRoomChangesOnlyTheHorseRestorationRegionAndPreservesTheOriginalAssetIdentity()
    {
        const string originalPath = "Assets/Sprites/3층/Approved/3F_Left_Fairy_Playroom_Approved.png";
        const string derivedPath = "Assets/Sprites/3층/PlayArea/3F_Left_Fairy_Playroom_PlayArea.png";
        Assert.That(AssetDatabase.AssetPathToGUID(originalPath), Is.EqualTo("6cf46143e6004af1b63ec7a74d86dfc6"));
        Assert.That(AssetDatabase.AssetPathToGUID(derivedPath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(originalPath)));
        Texture2D original = null, derived = null;
        try
        {
            original = Decode(originalPath);
            derived = Decode(derivedPath);
            Assert.That(original.width, Is.EqualTo(1672));
            Assert.That(original.height, Is.EqualTo(941));
            Assert.That(derived.width, Is.EqualTo(original.width));
            Assert.That(derived.height, Is.EqualTo(original.height));
            var before = original.GetPixels32();
            var after = derived.GetPixels32();
            int outsideChanges = 0, restoredChanges = 0;
            for (int y = 0; y < original.height; y++)
                for (int x = 0; x < original.width; x++)
                {
                    int index = y * original.width + x;
                    Color32 oldPixel = before[index], newPixel = after[index];
                    if (oldPixel.r == newPixel.r && oldPixel.g == newPixel.g && oldPixel.b == newPixel.b && oldPixel.a == newPixel.a) continue;
                    int topY = original.height - y - 1;
                    if (x >= 388 && x < 558 && topY >= 574 && topY < 733) restoredChanges++;
                    else outsideChanges++;
                }
            Assert.That(outsideChanges, Is.Zero, "Cushions, house, boxes and every other approved room pixel remain identical.");
            Assert.That(restoredChanges, Is.GreaterThan(100), "The baked horse is removed from its local source region.");
            var oldRoom = AssetDatabase.LoadAllAssetsAtPath(originalPath).OfType<Sprite>().Single(sprite => sprite.name == "ApprovedRoom");
            var newRoom = AssetDatabase.LoadAllAssetsAtPath(derivedPath).OfType<Sprite>().Single(sprite => sprite.name == "ApprovedRoom");
            Assert.That(newRoom.rect, Is.EqualTo(oldRoom.rect));
            Assert.That(newRoom.pivot, Is.EqualTo(oldRoom.pivot));
            Assert.That(newRoom.pixelsPerUnit, Is.EqualTo(oldRoom.pixelsPerUnit));
        }
        finally
        {
            if (original != null) Object.DestroyImmediate(original);
            if (derived != null) Object.DestroyImmediate(derived);
        }
    }

    [Test]
    public void SeparatedHorseKeepsSourcePixelsAlphaAndItsAuthoredRockingBoardPivot()
    {
        const string path = "Assets/Sprites/3층/PlayArea/3F_RockingHorse_Separated.png";
        Texture2D original = null, horse = null;
        try
        {
            original = Decode("Assets/Sprites/3층/Approved/3F_Left_Fairy_Playroom_Approved.png");
            horse = Decode(path);
            Assert.That(horse.width, Is.EqualTo(170));
            Assert.That(horse.height, Is.EqualTo(157));
            var source = original.GetPixels32();
            var cutout = horse.GetPixels32();
            int opaque = 0, transparent = 0, changedRgb = 0;
            for (int y = 0; y < horse.height; y++)
                for (int x = 0; x < horse.width; x++)
                {
                    var pixel = cutout[y * horse.width + x];
                    if (pixel.a == 0) { transparent++; continue; }
                    opaque++;
                    var expected = source[(210 + y) * original.width + 388 + x];
                    if (pixel.r != expected.r || pixel.g != expected.g || pixel.b != expected.b) changedRgb++;
                }
            Assert.That(opaque, Is.GreaterThan(1000));
            Assert.That(transparent, Is.GreaterThan(1000));
            Assert.That(changedRgb, Is.Zero, "The extracted horse uses the approved source colors; only alpha is separated.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0f, 0f, 170f, 157f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100f));
            Assert.That(sprite.pivot.x, Is.EqualTo(86f).Within(.01f));
            Assert.That(sprite.pivot.y, Is.EqualTo(13f).Within(.01f));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
        }
        finally
        {
            if (original != null) Object.DestroyImmediate(original);
            if (horse != null) Object.DestroyImmediate(horse);
        }
    }

    [Test]
    public void AuthoredPlayAreaUsesIndependentStationaryTargetsAndPreservesHousePolicies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Play-area verification requires SDK-free Edit Mode.");
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            var area = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdFloorFairyPlayArea>(true)).Single();
            var art = area.GetComponentInParent<ThirdFloorApprovedArt>();
            Assert.That(art, Is.Not.Null);
            Assert.That(area.transform.parent.name, Is.EqualTo("ApprovedContent"));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(area.gameObject), Is.EqualTo("Assets/Prefabs/Floor3/ThirdFloorFairyPlayArea.prefab"));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(FairyFurnitureKind)), area.Points.Select(point => point.Kind));
            foreach (var point in area.Points)
            {
                Assert.That(point.TouchEnabled, Is.True, point.Kind.ToString());
                Assert.That(point.HintVisual, Is.Not.Null);
                Assert.That(point.HintSprite, Is.Not.Null);
                Assert.That(point.HintSprite.sprite, Is.Not.Null);
                Assert.That(point.TouchCollider, Is.Not.Null);
                Assert.That(point.TouchCollider.enabled, Is.True);
                Assert.That(point.TouchCollider.transform.IsChildOf(point.HintVisual), Is.False, "Only artwork moves; its touch target is stationary.");
                Assert.That(point.HintVisual.IsChildOf(area.transform), Is.True);
                Assert.That(point.HintSprite.transform.IsChildOf(point.HintVisual), Is.True);
                Assert.That(point.Approach, Is.Not.Null, "Existing house crowd-avoidance anchors remain connected.");
                Assert.That(point.Activity, Is.Not.Null);
                Assert.That(point.Activity.IsChildOf(point.HintVisual), Is.False,
                    "Pure prop feedback must not move the existing house crowd-avoidance anchor.");
                Assert.That(point.Exit, Is.Not.Null);
            }
            Assert.That(area.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            CollectionAssert.AreEquivalent(area.Points.Select(point => point.TouchCollider), area.GetComponentsInChildren<Collider2D>(true));
            Assert.That(area.transform.Find("ReusableCushionZzz"), Is.Null);
            var cushion = area.Points.Single(point => point.Kind == FairyFurnitureKind.Cushion);
            Assert.That(cushion.TouchScale, Is.EqualTo(new Vector2(1.04f, .92f)));
            Assert.That(cushion.PressDuration, Is.EqualTo(.12f));
            Assert.That(cushion.ReleaseDuration, Is.EqualTo(.2f));
            var horse = area.Points.Single(point => point.Kind == FairyFurnitureKind.RockingHorse);
            Assert.That(horse.TouchRotationDegrees, Is.EqualTo(7f));
            Assert.That(horse.TouchDuration, Is.EqualTo(1f));
            var box = area.Points.Single(point => point.Kind == FairyFurnitureKind.Box);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(box.HintSprite.sprite)),
                Is.EqualTo("3576505a850d49a89be67ca22a477699"));
            Assert.That(art.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer.sprite != null
                && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sprite)) == "2f9364cd57ea4964932d0ee3fdcff778"), Is.True);
            Assert.That(box.TouchRotationDegrees, Is.EqualTo(2f));
            Assert.That(box.TouchTranslationPixels, Is.InRange(2f, 4f));
            Assert.That(box.TouchDuration, Is.InRange(.4f, .6f));
            AssertHousePolicy(art.GetComponentInChildren<ThirdFloorFairyHouse>(true).Settings);
            art.UseApprovedArt = false;
            Assert.That(area.gameObject.activeInHierarchy, Is.False);
            art.UseApprovedArt = true;
            Assert.That(area.gameObject.activeInHierarchy, Is.True);
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            witness.AssertUnchanged("authored visual-only fairy furniture verification");
        }
    }

    [Test]
    public void SeparatedStarBoxesKeepOpaqueSourcePixelsAndDerivedRoomChangesOnlyTheirRegion()
    {
        const string sourcePath = "Assets/Sprites/3층/PlayArea/3F_Left_Fairy_Playroom_WithoutCushion.png";
        const string backgroundPath = "Assets/Sprites/3층/PlayArea/3F_Left_Fairy_Playroom_TouchProps.png";
        const string spritePath = "Assets/Sprites/3층/PlayArea/3F_StarBoxes_Separated.png";
        Texture2D source = null, derived = null, boxes = null;
        try
        {
            source = Decode(sourcePath); derived = Decode(backgroundPath); boxes = Decode(spritePath);
            Assert.That(source.height, Is.EqualTo(941));
            Assert.That(derived.width, Is.EqualTo(source.width)); Assert.That(derived.height, Is.EqualTo(source.height));
            Assert.That(boxes.width, Is.EqualTo(250)); Assert.That(boxes.height, Is.EqualTo(195));
            var before = source.GetPixels32(); var after = derived.GetPixels32(); var crop = boxes.GetPixels32();
            int outsideChanges = 0, insideChanges = 0, opaque = 0, transparent = 0;
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    int index = y * source.width + x;
                    if (before[index].Equals(after[index])) continue;
                    int topY = source.height - y - 1;
                    if (x >= 1245 && x < 1495 && topY >= 535 && topY < 730) insideChanges++;
                    else outsideChanges++;
                }
            for (int y = 0; y < boxes.height; y++)
                for (int x = 0; x < boxes.width; x++)
                {
                    var pixel = crop[y * boxes.width + x];
                    if (pixel.a == 0) transparent++;
                    if (pixel.a != 255) continue;
                    opaque++;
                    var expected = before[(source.height - 730 + y) * source.width + 1245 + x];
                    Assert.That(new Color32(pixel.r, pixel.g, pixel.b, 255), Is.EqualTo(new Color32(expected.r, expected.g, expected.b, 255)));
                }
            Assert.That(outsideChanges, Is.Zero); Assert.That(insideChanges, Is.GreaterThan(1000));
            Assert.That(opaque, Is.GreaterThan(10000)); Assert.That(transparent, Is.GreaterThan(10000));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100f));
            Assert.That(sprite.pivot.x, Is.EqualTo(125f).Within(.01f));
            Assert.That(sprite.pivot.y, Is.EqualTo(14f).Within(.01f));
            var oldRoom = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Sprite>().Single(value => value.name == "ApprovedRoom");
            var newRoom = AssetDatabase.LoadAllAssetsAtPath(backgroundPath).OfType<Sprite>().Single(value => value.name == "ApprovedRoom");
            Assert.That(newRoom.rect, Is.EqualTo(oldRoom.rect)); Assert.That(newRoom.pivot, Is.EqualTo(oldRoom.pivot));
        }
        finally
        {
            if (source != null) Object.DestroyImmediate(source);
            if (derived != null) Object.DestroyImmediate(derived);
            if (boxes != null) Object.DestroyImmediate(boxes);
        }
    }

    [Test]
    public void InvalidInspectorAnimationNumbersAreNormalizedBeforeAnimation()
    {
        using (var f = new Fixture(0))
        {
            var point = f.Horse;
            point.TouchScale = new Vector2(float.NaN, float.PositiveInfinity);
            point.PressDuration = float.NaN; point.ReleaseDuration = float.NegativeInfinity;
            point.TouchRotationDegrees = float.NaN; point.TouchTranslationPixels = float.PositiveInfinity;
            point.TouchDuration = -1f; point.TouchCycles = 0;
            point.Validate();
            Assert.That(float.IsNaN(point.TouchScale.x) || float.IsInfinity(point.TouchScale.y), Is.False);
            Assert.That(point.TouchScale.x, Is.GreaterThan(0f)); Assert.That(point.TouchScale.y, Is.GreaterThan(0f));
            Assert.That(point.PressDuration, Is.GreaterThan(0f)); Assert.That(point.ReleaseDuration, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(point.TouchRotationDegrees) || float.IsInfinity(point.TouchTranslationPixels), Is.False);
            Assert.That(point.TouchDuration, Is.GreaterThan(0f)); Assert.That(point.TouchCycles, Is.InRange(1, 6));
        }
    }

    [TestCase(FairyFurnitureKind.Cushion)]
    [TestCase(FairyFurnitureKind.RockingHorse)]
    [TestCase(FairyFurnitureKind.Box)]
    public void EachTouchAnimatesOnlyItsArtworkThenRestoresExactly(FairyFurnitureKind kind)
    {
        using (var f = new Fixture(3))
        {
            var point = f.Point(kind);
            var positions = f.ActiveIds().ToDictionary(id => id, id => f.Brain(id).GroundPosition);
            var actions = f.ActiveIds().ToDictionary(id => id, id => f.Brain(id).Action);
            var poses = f.Area.Points.ToDictionary(value => value, value => new PoseSnapshot(value.HintVisual));
            var collider = new PoseSnapshot(point.TouchCollider.transform);
            var globalRandom = UnityEngine.Random.state;
            int pool = f.Habitat.PooledObjectCount;
            Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            Assert.That(f.Area.IsTouchAnimating(point), Is.True);
            float duration = kind == FairyFurnitureKind.Cushion ? point.PressDuration + point.ReleaseDuration : point.TouchDuration;
            float maxAngle = 0f, maxPixels = 0f, minScaleY = 1f, maxScaleX = 1f;
            Vector3 screenStart = f.WorldCamera.WorldToScreenPoint(point.HintVisual.position);
            for (int frame = 0; frame < Mathf.CeilToInt(duration * 60f); frame++)
            {
                f.Area.AdvanceHintPreview(1f / 60f);
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(poses[point].Rotation, point.HintVisual.localRotation));
                Vector3 screen = f.WorldCamera.WorldToScreenPoint(point.HintVisual.position);
                maxPixels = Mathf.Max(maxPixels, Mathf.Abs(screen.x - screenStart.x));
                if (kind == FairyFurnitureKind.Box) Assert.That(screen.y, Is.EqualTo(screenStart.y).Within(.01f), "The box never lifts into the air.");
                minScaleY = Mathf.Min(minScaleY, point.HintVisual.localScale.y / poses[point].Scale.y);
                maxScaleX = Mathf.Max(maxScaleX, point.HintVisual.localScale.x / poses[point].Scale.x);
                collider.AssertExact(point.TouchCollider.transform);
                foreach (var other in f.Area.Points.Where(value => value != point)) poses[other].AssertExact(other.HintVisual);
                foreach (string id in positions.Keys)
                {
                    Assert.That(f.Brain(id).GroundPosition, Is.EqualTo(positions[id]), "Furniture never directs or teleports a fairy.");
                    Assert.That(f.Brain(id).Action, Is.EqualTo(actions[id]));
                }
            }
            f.Area.AdvanceHintPreview(.1f);
            Assert.That(f.Area.IsTouchAnimating(point), Is.False);
            poses[point].AssertExact(point.HintVisual);
            if (kind == FairyFurnitureKind.Cushion)
            {
                Assert.That(minScaleY, Is.InRange(.919f, .94f));
                Assert.That(maxScaleX, Is.InRange(1.03f, 1.041f));
                Assert.That(point.HintVisual.localPosition, Is.EqualTo(poses[point].Position));
            }
            else
            {
                Assert.That(maxAngle, Is.GreaterThan(kind == FairyFurnitureKind.RockingHorse ? 3f : .5f));
                Assert.That(maxAngle, Is.LessThanOrEqualTo(point.TouchRotationDegrees + .01f));
                if (kind == FairyFurnitureKind.Box) Assert.That(maxPixels, Is.InRange(1.5f, point.TouchTranslationPixels + .02f));
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(globalRandom));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(pool));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(3));
            Assert.That(f.Area.transform.Find("ReusableCushionZzz"), Is.Null);
        }
    }

    [TestCase(FairyFurnitureKind.Cushion)]
    [TestCase(FairyFurnitureKind.RockingHorse)]
    [TestCase(FairyFurnitureKind.Box)]
    public void RepeatedTouchesDoNotStackRestartOrCreateFairiesAndCanReplay(FairyFurnitureKind kind)
    {
        using (var f = new Fixture(0))
        {
            var point = f.Point(kind);
            var rest = new PoseSnapshot(point.HintVisual);
            for (int cycle = 0; cycle < 8; cycle++)
            {
                Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
                f.Area.AdvanceHintPreview(.05f);
                var moving = new PoseSnapshot(point.HintVisual);
                for (int tap = 0; tap < 12; tap++) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
                moving.AssertExact(point.HintVisual);
                for (int frame = 0; frame < 40; frame++) f.Area.AdvanceHintPreview(.05f);
                Assert.That(f.Area.IsTouchAnimating(point), Is.False, "Repeated hits cannot reset the elapsed timer.");
                rest.AssertExact(point.HintVisual);
                Assert.That(f.Habitat.ActiveCount, Is.Zero);
                Assert.That(f.Habitat.PooledObjectCount, Is.Zero);
                Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            }
        }
    }

    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("other-floor")]
    [TestCase("area-disabled")]
    [TestCase("point-disabled")]
    [TestCase("touch-disabled")]
    [TestCase("sprite-hidden")]
    [TestCase("collider-disabled")]
    public void InterruptedTouchesRestoreAllFurnitureAndRecover(string reason)
    {
        using (var f = new Fixture(2))
        {
            var rests = f.Area.Points.ToDictionary(point => point, point => new PoseSnapshot(point.HintVisual));
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            f.Area.AdvanceHintPreview(.08f);
            switch (reason)
            {
                case "camera-moving": Set(f.InputCamera, "_isMoveAction", true); break;
                case "world-block": f.InputCamera.SetWorldInputBlocked(f, true); break;
                case "other-floor": Set(f.Main, "_currentFloor", ERestaurantFloorType.Floor1); break;
                case "area-disabled": f.Area.enabled = false; Invoke(f.Area, "OnDisable"); break;
                case "point-disabled": foreach (var point in f.Area.Points) point.Enabled = false; break;
                case "touch-disabled": foreach (var point in f.Area.Points) point.TouchEnabled = false; break;
                case "sprite-hidden": foreach (var point in f.Area.Points) point.HintSprite.enabled = false; break;
                case "collider-disabled": foreach (var point in f.Area.Points) point.TouchCollider.enabled = false; break;
            }
            f.Area.AdvanceHintPreview(.05f);
            foreach (var point in f.Area.Points)
            {
                Assert.That(f.Area.IsTouchAnimating(point), Is.False, reason);
                rests[point].AssertExact(point.HintVisual);
                Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.False, reason);
            }
            foreach (var point in f.Area.Points) point.Enabled = point.TouchEnabled = point.HintSprite.enabled = point.TouchCollider.enabled = true;
            Set(f.InputCamera, "_isMoveAction", false);
            f.InputCamera.SetWorldInputBlocked(f, false);
            Set(f.Main, "_currentFloor", ERestaurantFloorType.Floor3);
            f.Area.enabled = true; Invoke(f.Area, "OnEnable");
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
        }
    }

    [TestCase(FairyFurnitureKind.Cushion)]
    [TestCase(FairyFurnitureKind.RockingHorse)]
    public void UserTouchCancelsIdleHintAndRestoresWithoutOverlap(FairyFurnitureKind kind)
    {
        using (var f = new Fixture(0))
        {
            var point = f.Point(kind);
            point.EnableIdleHint = true; point.FirstHintDelay = new Vector2(.1f, .1f); point.HintInterval = new Vector2(15f, 25f);
            var rest = new PoseSnapshot(point.HintVisual);
            f.Area.AdvanceHintPreview(.1f); f.Area.AdvanceHintPreview(.2f);
            Assert.That(Vector3.Distance(point.HintVisual.localScale, rest.Scale) > .001f || Quaternion.Angle(point.HintVisual.localRotation, rest.Rotation) > .01f, Is.True);
            Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            f.Area.AdvanceHintPreview(.1f);
            Assert.That(f.Area.IsTouchAnimating(point), Is.True);
            for (int frame = 0; frame < 40; frame++) f.Area.AdvanceHintPreview(.05f);
            Assert.That(f.Area.IsTouchAnimating(point), Is.False);
            rest.AssertExact(point.HintVisual);
        }
    }

    [TestCase(VisibleState.Appearing)]
    [TestCase(VisibleState.Appeared)]
    [TestCase(VisibleState.Disappearing)]
    public void ExistingMapAndPopupVisibilityGuardBlocksEveryFurniture(VisibleState state)
    {
        using (var f = new Fixture(2))
        {
            var map = new GameObject("Detached map visibility witness");
            map.SetActive(false); map.transform.SetParent(f.Host.transform, false);
            var view = map.AddComponent<UIFloor3>(); view.VisibleState = state; map.SetActive(true);
            Set(f.Habitat, "_runtime", true); Set(f.Habitat, "_worldCamera", f.WorldCamera);
            Set(f.Habitat, "_cameraController", f.InputCamera); Set(f.Habitat, "_sceneViews", new UIView[] { view });
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.False);
            view.VisibleState = VisibleState.Disappeared;
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            Set(f.Habitat, "_runtime", false);
        }
    }

    [TestCase("drag-return")]
    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("other-floor")]
    [TestCase("outside")]
    public void ExistingCameraCompletedTapRejectsInvalidGesturesAndRecovers(string reason)
    {
        using (var f = new Fixture(2))
        {
            Vector2 pixel = f.WorldCamera.WorldToScreenPoint(f.Horse.TouchCollider.bounds.center);
            InvokeArgs(f.InputCamera, "BeginWorldTap", pixel);
            switch (reason)
            {
                case "drag-return": InvokeArgs(f.InputCamera, "TrackWorldTap", pixel + Vector2.right * 100f); break;
                case "camera-moving": Set(f.InputCamera, "_isMoveAction", true); break;
                case "world-block": f.InputCamera.SetWorldInputBlocked(f, true); break;
                case "other-floor": Set(f.Main, "_currentFloor", ERestaurantFloorType.Floor1); break;
                case "outside": pixel += Vector2.up * 400f; InvokeArgs(f.InputCamera, "BeginWorldTap", pixel); break;
            }
            Assert.That(InvokeArgs(f.InputCamera, "CompleteWorldTap", pixel), Is.False, reason);
            Assert.That(f.Area.Points.Any(f.Area.IsTouchAnimating), Is.False);
            Set(f.InputCamera, "_isMoveAction", false); f.InputCamera.SetWorldInputBlocked(f, false);
            Set(f.Main, "_currentFloor", ERestaurantFloorType.Floor3);
            pixel = f.WorldCamera.WorldToScreenPoint(f.Horse.TouchCollider.bounds.center);
            InvokeArgs(f.InputCamera, "BeginWorldTap", pixel);
            Assert.That(InvokeArgs(f.InputCamera, "CompleteWorldTap", pixel), Is.True);
            Assert.That(f.Area.IsTouchAnimating(f.Horse), Is.True);
        }
    }

    [Test]
    public void FurnitureTouchesPreserveHouseStayCapOldestReturnAndSlowInterval()
    {
        using (var f = new Fixture(40, autonomousActions: true))
        {
            AssertHousePolicy(f.HouseSettings);
            f.Settings.WalkSpeed = f.HouseSettings.ReturnMoveSpeed = 40f;
            f.HouseSettings.ReturnTravelSeconds = f.HouseSettings.ReturnFadeSeconds = .1f;
            f.ShowBaseResidentsOneAtATime();
            string oldest = f.Id(0);
            for (int tap = 0; tap < 5; tap++) f.Habitat.RequestHouseEmergence(f, 3);
            f.Until(() => f.Habitat.PendingHouseSpawnCount == 0, 40f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24));
            float due = f.Habitat.HouseReturnDueAt;
            f.Advance(Mathf.Max(0f, due - f.Clock - 1f));
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            Assert.That(f.Habitat.HouseReturnDueAt, Is.EqualTo(due));
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            f.Until(() => f.Habitat.ReturningHouseCount == 1, 2f);
            CollectionAssert.AreEqual(new[] { oldest }, f.Habitat.ReturningHouseItemIds);
            f.Until(() => f.Habitat.ReturningHouseCount == 0, 5f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(23)); Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(40));
            Assert.That(f.Settings.MaxActive, Is.EqualTo(12));
            f.Advance(5.9f); Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            f.AssertUniqueOwned();
        }
    }

    [Test]
    public void FurnitureTouchesPreserveBirthProtectionAndYoungHouseVisitorMinimumStay()
    {
        using (var f = new Fixture(2, autonomousActions: true))
        {
            f.Settings.MaxActive = 1; f.Settings.NewArrivalPrioritySeconds = 100f;
            f.Settings.BirthArea = new Rect(20f, 1f, 4f, 3f);
            f.HouseSettings.TemporaryPlaySeconds = .1f;
            f.Habitat.ResetOfflineSession(f.OwnedIds); f.Advance(.5f);
            string older = f.Id(0);
            Assert.That(f.Habitat.ConfirmAcquisition("memory-only-protected-older-touch", f.Items[0]), Is.True);
            Assert.That(f.Habitat.ActiveBirthItemIds, Does.Contain(older));
            Assert.That(f.Habitat.RequestHouseEmergence(f, 1), Is.EqualTo(1));
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, 30f);
            string newcomer = f.ActiveIds().Single(id => id != older);
            float spawnedAt = f.Clock;
            foreach (var point in f.Area.Points) Assert.That(f.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            f.Advance(24.7f);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            f.Until(() => f.Habitat.ReturningHouseCount == 1, .5f);
            Assert.That(f.Clock - spawnedAt, Is.GreaterThanOrEqualTo(25f - .051f));
            CollectionAssert.AreEqual(new[] { newcomer }, f.Habitat.ReturningHouseItemIds);
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void FurnitureTouchDoesNotDelayRosterRotationOrGrowThePool()
    {
        using (var f = new Fixture(3, baseLimit: 1))
        {
            string id = f.Id(0); f.Settings.RotationSeconds = 1f;
            Assert.That(f.Area.TryTapAtWorldPoint(f.Horse.TouchCollider.bounds.center), Is.True);
            f.Until(() => f.Id(0) != id, 4f);
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(1));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(3));
            Assert.That(f.Area.transform.Find("ReusableCushionZzz"), Is.Null);
        }
    }

    private static void AssertHousePolicy(EnhancementFairyHouseSettings settings)
    {
        Assert.That(settings.TemporaryExtraLimit, Is.EqualTo(12)); Assert.That(settings.TemporaryPlaySeconds, Is.EqualTo(45f));
        Assert.That(settings.MinimumIndividualStaySeconds, Is.EqualTo(25f));
        Assert.That(settings.ReturnStaggerMinSeconds, Is.EqualTo(6f)); Assert.That(settings.ReturnStaggerMaxSeconds, Is.EqualTo(10f));
    }

    private sealed class PoseSnapshot
    {
        internal readonly Vector3 Position, Scale; internal readonly Quaternion Rotation;
        internal PoseSnapshot(Transform t) { Position = t.localPosition; Scale = t.localScale; Rotation = t.localRotation; }
        internal void AssertExact(Transform t)
        { Assert.That(t.localPosition, Is.EqualTo(Position)); Assert.That(t.localScale, Is.EqualTo(Scale)); Assert.That(t.localRotation, Is.EqualTo(Rotation)); }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
        private readonly Texture2D _texture; private readonly Sprite _sprite;
        internal readonly GameObject Host; internal readonly EnhancementFairyHabitat Habitat; internal readonly EnhancementFairySettings Settings;
        internal readonly EnhancementFairyHouseSettings HouseSettings = new EnhancementFairyHouseSettings();
        internal readonly EnhancementFairyArrivalQueue Arrivals = new EnhancementFairyArrivalQueue();
        internal readonly List<GachaItemData> Items = new List<GachaItemData>();
        internal readonly string[] OwnedIds; internal readonly ThirdFloorFairyPlayArea Area;
        internal FairyFurniturePoint Horse => Point(FairyFurnitureKind.RockingHorse);
        internal readonly Camera WorldCamera; internal readonly CameraController InputCamera; internal readonly MainScene Main;

        internal Fixture(int owned, int baseLimit = 12, bool autonomousActions = false)
        {
            try
            {
                Settings = ScriptableObject.CreateInstance<EnhancementFairySettings>();
                Settings.MaxActive = baseLimit; Settings.RotationSeconds = 10000f;
                Settings.GroundArea = new Rect(-13.4f, 0f, 60f, 0f); Settings.BirthArea = new Rect(-4f, 1f, 8f, 3f);
                Settings.BirthStartDelay = new Vector2(.1f, .1f);
                if (!autonomousActions)
                {
                    Settings.HopProbability = Settings.VisitProbability = Settings.WalkProbability = 0f;
                    Settings.LookProbability = Settings.TurnProbability = 0f; Settings.RestProbability = 1f;
                }
                _texture = new Texture2D(4, 4); _texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); _texture.Apply();
                _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(.5f, 0f));
                for (int i = 0; i < 40; i++)
                {
                    var item = ScriptableObject.CreateInstance<GachaItemData>();
                    Set(item, "_id", "ITEM" + i.ToString("D2")); Set(item, "_upgradeType", UpgradeType.UPGRADE01); Set(item, "_sprite", _sprite); Items.Add(item);
                }
                OwnedIds = Items.Take(owned).Select(item => item.Id).ToArray();
                Host = Create("Detached visual-only furniture habitat");
                Habitat = Host.AddComponent<EnhancementFairyHabitat>(); Habitat.ConfigureOffline(Items, OwnedIds, Settings, Arrivals);
                var entrance = Create("House entrance").transform; entrance.SetParent(Host.transform, false);
                Habitat.RegisterHouse(this, entrance, HouseSettings);
                var root = Create("Detached furniture area"); root.transform.SetParent(Host.transform, false);
                Area = root.AddComponent<ThirdFloorFairyPlayArea>();
                var cameraRoot = Create("Detached furniture input camera"); cameraRoot.SetActive(false);
                WorldCamera = cameraRoot.AddComponent<Camera>(); WorldCamera.orthographic = true; WorldCamera.orthographicSize = 12f;
                WorldCamera.pixelRect = new Rect(0f, 0f, 1024f, 768f); WorldCamera.transform.position = new Vector3(-2f, 0f, -20f);
                InputCamera = cameraRoot.AddComponent<CameraController>(); Set(InputCamera, "_cam", WorldCamera);
                var mainRoot = Create("Detached furniture floor state"); mainRoot.SetActive(false);
                Main = mainRoot.AddComponent<MainScene>(); Set(Main, "_currentFloor", ERestaurantFloorType.Floor3); Set(InputCamera, "_mainScene", Main);
                Area.ConfigureOffline(Habitat, new[] { CreatePoint(FairyFurnitureKind.Cushion, -12.9f), CreatePoint(FairyFurnitureKind.RockingHorse, -7.35f), CreatePoint(FairyFurnitureKind.Box, 11.9f) }, WorldCamera, InputCamera);
                Physics2D.SyncTransforms(); Advance(.5f);
            }
            catch { Dispose(); throw; }
        }
        private FairyFurniturePoint CreatePoint(FairyFurnitureKind kind, float x)
        {
            var point = new FairyFurniturePoint { Kind = kind, Enabled = true, TouchEnabled = true };
            point.Approach = Anchor(kind + " approach", new Vector3(x, 0f)); point.Exit = Anchor(kind + " exit", new Vector3(x + .4f, 0f));
            point.Activity = Anchor(kind + " spacing anchor", new Vector3(x, 1f));
            point.HintVisual = Anchor(kind + " visual", new Vector3(x, 0f)); point.HintVisual.localScale = new Vector3(1.2f, .9f, 1f);
            point.HintSprite = point.HintVisual.gameObject.AddComponent<SpriteRenderer>(); point.HintSprite.sprite = _sprite;
            point.TouchCollider = Anchor(kind + " stationary input", new Vector3(x, 0f)).gameObject.AddComponent<BoxCollider2D>();
            ((BoxCollider2D)point.TouchCollider).size = new Vector2(2f, 2f); point.TouchCollider.isTrigger = true;
            point.TouchScale = new Vector2(1.04f, .92f); point.PressDuration = .12f; point.ReleaseDuration = .2f;
            point.TouchDuration = kind == FairyFurnitureKind.Box ? .5f : 1f;
            point.TouchRotationDegrees = kind == FairyFurnitureKind.RockingHorse ? 7f : 2f;
            point.TouchTranslationPixels = kind == FairyFurnitureKind.Box ? 3f : 0f; point.TouchCycles = 2;
            return point;
        }
        private Transform Anchor(string name, Vector3 position) { var t = Create(name).transform; t.SetParent(Area.transform, false); t.localPosition = position; return t; }
        private GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, _scene); return go; }
        internal FairyFurniturePoint Point(FairyFurnitureKind kind) => Area.Points.Single(point => point.Kind == kind);
        internal float Clock => Habitat.HouseActivitySeconds;
        internal string Id(int index) => Habitat.GetActiveBrain(index).ItemId;
        internal EnhancementFairyBrain Brain(string id) => Enumerable.Range(0, Habitat.ActiveCount).Select(Habitat.GetActiveBrain).Single(brain => brain.ItemId == id);
        internal HashSet<string> ActiveIds() => new HashSet<string>(Enumerable.Range(0, Habitat.ActiveCount).Select(Id), StringComparer.Ordinal);
        internal void AssertUniqueOwned() { var ids = ActiveIds(); Assert.That(ids.Count, Is.EqualTo(Habitat.ActiveCount)); Assert.That(ids.IsSubsetOf(new HashSet<string>(OwnedIds)), Is.True); }
        internal void ShowBaseResidentsOneAtATime()
        {
            Habitat.ResetOfflineSession(Array.Empty<string>());
            for (int i = 1; i <= Mathf.Min(OwnedIds.Length, Settings.MaxActive); i++) { Habitat.RestoreOwnership(OwnedIds.Take(i)); Advance(.1f); }
            Habitat.RestoreOwnership(OwnedIds);
        }
        internal void Step(float seconds) { Habitat.AdvancePreview(seconds); Area?.AdvanceHintPreview(seconds); }
        internal void Advance(float seconds) { for (int frame = 0, frames = Mathf.CeilToInt(seconds / .05f); frame < frames; frame++) Step(.05f); }
        internal void Until(Func<bool> predicate, float timeout) { int frames = Mathf.CeilToInt(timeout / .05f); while (!predicate() && frames-- > 0) Step(.05f); Assert.That(predicate(), Is.True, Habitat.GetAiDiagnosticSnapshot()); }
        public void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var item in Items) Object.DestroyImmediate(item);
            if (Settings != null) Object.DestroyImmediate(Settings); if (_sprite != null) Object.DestroyImmediate(_sprite); if (_texture != null) Object.DestroyImmediate(_texture);
        }
    }

    private static void Set(object target, string field, object value)
    {
        var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }
    private static Texture2D Decode(string path)
    {
        var texture = new Texture2D(2, 2);
        if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
        Object.DestroyImmediate(texture); throw new InvalidOperationException("The production PNG could not be decoded: " + path);
    }
    private static object Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static object InvokeArgs(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
#endif
