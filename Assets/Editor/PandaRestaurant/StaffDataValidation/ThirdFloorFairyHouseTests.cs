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

/// <summary>Detached real Habitat/Brain/input execution; no account, acquisition transaction, or disk save.</summary>
public sealed class ThirdFloorFairyHouseTests
{
    [Test]
    public void ActualStage1HouseUsesApprovedTransparentAssetAndSharesTheArtToggle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Actual house asset verification requires SDK-free Edit Mode.");
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        Scene scene = default;
        Texture2D decoded = null;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
            var house = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdFloorFairyHouse>(true)).Single();
            var art = house.GetComponentInParent<ThirdFloorApprovedArt>();
            Assert.That(art, Is.Not.Null);
            Assert.That(house.transform.parent.name, Is.EqualTo("ApprovedContent"));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(house.gameObject),
                Is.EqualTo("Assets/Prefabs/Floor3/ThirdFloorFairyHouse.prefab"));
            Assert.That(house.transform.localPosition.x, Is.EqualTo(-36.6f).Within(.001f));
            Assert.That(house.transform.localPosition.y, Is.EqualTo(7.85f).Within(.001f));
            Assert.That(house.Visual, Is.Not.Null);
            Assert.That(house.Entrance, Is.Not.Null);
            Assert.That(house.TouchCollider, Is.Not.Null);
            Assert.That(house.Visual, Is.Not.SameAs(house.transform));
            Assert.That(house.TouchCollider.transform.IsChildOf(house.Visual), Is.False);
            Assert.That(house.Entrance.IsChildOf(house.Visual), Is.False);
            Assert.That(house.TouchCollider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Furniture")));
            Assert.That(house.TouchCollider.enabled, Is.True);
            Assert.That(house.TouchCollider.isTrigger, Is.True);
            Assert.That(house.Visual.localScale, Is.EqualTo(Vector3.one * .68f));
            Assert.That(house.Visual.localPosition.y, Is.EqualTo(-.22f).Within(.001f));
            var sprite = house.Visual.GetComponent<SpriteRenderer>();
            Assert.That(sprite, Is.Not.Null);
            var floor = art.GetComponentInParent<Floor3Controller>();
            Assert.That(floor, Is.Not.Null);
            for (int index = 1; index <= 7; index++)
            {
                string furniturePath = "Object" + index + "/SpriteRenderer";
                var originalVisual = floor.transform.Find(furniturePath);
                Assert.That(originalVisual, Is.Not.Null, "The original furniture visual must exist: " + furniturePath);
                var original = originalVisual.GetComponent<SpriteRenderer>();
                Assert.That(original, Is.Not.Null, furniturePath);
                Assert.That(original.sortingLayerName, Is.EqualTo("Rack"), furniturePath);
                Assert.That(original.sortingOrder, Is.Zero, furniturePath);
                Assert.That(sprite.sortingLayerID, Is.EqualTo(original.sortingLayerID));
                Assert.That(sprite.sortingOrder, Is.EqualTo(original.sortingOrder));
            }
            var fairySettings = Resources.Load<EnhancementFairySettings>(EnhancementFairySettings.ResourcePath);
            Assert.That(SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(fairySettings.SortingLayer)),
                Is.GreaterThan(SortingLayer.GetLayerValueFromID(sprite.sortingLayerID)));
            Assert.That(house.Settings.TemporaryExtraLimit, Is.EqualTo(12));
            Assert.That(house.Settings.TemporaryPlaySeconds, Is.EqualTo(45f));
            Assert.That(house.Settings.MinimumIndividualStaySeconds, Is.EqualTo(25f));
            Assert.That(house.Settings.ReturnStaggerMinSeconds, Is.EqualTo(6f));
            Assert.That(house.Settings.ReturnStaggerMaxSeconds, Is.EqualTo(10f));
            Assert.That(house.Settings.MaximumConcurrentReturns, Is.EqualTo(1));
            Assert.That(house.Settings.TapSpawnFirstDelay, Is.Zero);
            Assert.That(house.Settings.SpawnInterval, Is.EqualTo(.18f));
            Assert.That(house.Settings.EntranceDuration, Is.EqualTo(.45f));
            Assert.That(house.Settings.TapDispersalSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(sprite.color, Is.EqualTo(Color.white));
            string path = AssetDatabase.GetAssetPath(sprite.sprite);
            Assert.That(path, Is.EqualTo("Assets/Sprites/3층/FairyHouse/3F_FairyHouse_Standalone_Transparent.png"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            decoded = new Texture2D(2, 2);
            Assert.That(decoded.LoadImage(File.ReadAllBytes(path)), Is.True);
            var pixels = decoded.GetPixels32();
            Assert.That(pixels.Any(pixel => pixel.a == 0) && pixels.Any(pixel => pixel.a == 255), Is.True,
                "Actual decoded pixels contain both transparency and opaque approved art.");
            art.UseApprovedArt = true;
            Assert.That(house.gameObject.activeInHierarchy, Is.True);
            art.UseApprovedArt = false;
            Assert.That(house.gameObject.activeInHierarchy, Is.False);
            art.UseApprovedArt = true;
            Assert.That(house.gameObject.activeInHierarchy, Is.True);
        }
        finally
        {
            if (decoded != null) Object.DestroyImmediate(decoded);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            witness.AssertUnchanged("authored third-floor fairy house prefab verification");
        }
    }

    [Test]
    public void DefaultHouseLimitReferencesMaxActiveAndNeverChangesIt()
    {
        using (var f = new Fixture(30))
        {
            Assert.That(f.Habitat.BaseActiveLimit, Is.EqualTo(12));
            Assert.That(f.Habitat.HouseHardCap, Is.EqualTo(24));
            f.Settings.MaxActive = 4;
            f.Habitat.RestoreOwnership(f.OwnedIds);
            Assert.That(f.Habitat.BaseActiveLimit, Is.EqualTo(4));
            Assert.That(f.Habitat.HouseHardCap, Is.EqualTo(16));
            f.Request(3);
            f.Advance(1f);
            Assert.That(f.Settings.MaxActive, Is.EqualTo(4));
            Assert.That(f.Habitat.ActiveCount, Is.LessThanOrEqualTo(16));
        }
    }

    [Test]
    public void OneTwoThreeRequestsUseOnlyUnshownOwnedItemsWithoutAcquisitionEffects()
    {
        using (var f = new Fixture(30))
        {
            var original = f.ActiveIds();
            foreach (int count in new[] { 1, 2, 3 })
            {
                Assert.That(f.Request(count), Is.EqualTo(count));
                f.DrainHouseQueue();
                f.AssertUniqueOwned();
            }
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(18));
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(6));
            Assert.That(original.IsSubsetOf(f.ActiveIds()), Is.True, "No initial fairy may be instantly reconciled away for an extra slot.");
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
            Assert.That(f.Arrivals.PendingCount, Is.Zero);
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(30));
            Assert.That(f.Settings.MaxActive, Is.EqualTo(12));
        }
    }

    [TestCase(18)]
    [TestCase(19)]
    [TestCase(23)]
    [TestCase(24)]
    [TestCase(30)]
    public void ExpandedCapacityShowsOnlyOwnedItemsUpToTwentyFour(int owned)
    {
        using (var f = new Fixture(owned))
        {
            for (int tap = 0; tap < 10; tap++) f.Request(3);
            f.DrainHouseQueue();
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(Mathf.Min(owned, 24)));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(Mathf.Min(owned, 24)));
            Assert.That(f.Request(3), Is.Zero);
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Settings.MaxActive, Is.EqualTo(12));
            f.AssertUniqueOwned();
        }
    }

    [Test]
    public void DefaultFortyFiveSecondsStartAtTheLastActualSpawnAndOrdinaryRotationCannotRemoveVisitors()
    {
        using (var f = new Fixture(30))
        {
            f.Settings.RotationSeconds = 12f;
            var original = f.ActiveIds();
            Assert.That(f.Config.TemporaryPlaySeconds, Is.EqualTo(45f));
            f.Request(3);
            f.DrainHouseQueue();
            float firstDeadline = f.Habitat.HouseReturnDueAt;
            Assert.That(firstDeadline, Is.EqualTo(f.Habitat.HouseActivitySeconds + 45f).Within(.001f));
            f.Advance(30f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(15));
            Assert.That(original.IsSubsetOf(f.ActiveIds()), Is.True, "The old 12-second roster rotation must not cut the visit short.");
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            Assert.That(f.Request(1), Is.EqualTo(1));
            f.Until(() => f.Habitat.ActiveCount == 16, 15f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(16));
            float lastSpawnAt = f.Habitat.HouseActivitySeconds;
            Assert.That(f.Habitat.HouseReturnDueAt, Is.GreaterThan(firstDeadline),
                "The default immediate successful spawn restarts the stay; a failed tap still cannot do so.");
            Assert.That(f.Habitat.HouseReturnDueAt, Is.EqualTo(lastSpawnAt + 45f).Within(.001f));
            f.Advance(44.8f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(16));
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero, "No return before the full default 45 seconds since the last real spawn.");
            f.Until(() => f.Habitat.ReturningHouseCount == 1, .4f);
            Assert.That(f.Habitat.HouseActivitySeconds - lastSpawnAt, Is.GreaterThanOrEqualTo(45f - .001f));
        }
    }

    [Test]
    public void AcceptedHouseRequestCancelsAnAlreadyStartedOrdinaryRotationFade()
    {
        using (var f = new Fixture(30))
        {
            f.Settings.RotationSeconds = 12f;
            f.Advance(1f);
            var original = f.ActiveIds();
            SpriteRenderer fading = null;
            f.Until(() =>
            {
                fading = f.ActiveIds().Select(id => f.Host.transform.Find("Fairy " + id + "/Item sprite")
                    .GetComponent<SpriteRenderer>()).FirstOrDefault(body => body.color.a > 0f && body.color.a < .99f);
                return fading != null;
            }, 12f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            Assert.That(f.Request(1), Is.EqualTo(1));
            f.DrainHouseQueue();
            f.Advance(f.Settings.RotationFadeSeconds + .5f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(13));
            Assert.That(original.IsSubsetOf(f.ActiveIds()), Is.True,
                "A rotation fade started before the accepted house tap must not silently release a resident during the visit.");
            Assert.That(fading.gameObject.activeInHierarchy, Is.True);
            Assert.That(fading.color.a, Is.EqualTo(1f));
        }
    }

    [Test]
    public void YoungHouseVisitorRemainsProtectedFromOrdinaryRotationAfterOverflowHasEnded()
    {
        using (var f = new Fixture(3, baseLimit: 1))
        {
            f.FastReturns();
            f.Config.TemporaryPlaySeconds = .1f;
            f.Config.MinimumIndividualStaySeconds = 25f;
            string older = f.Habitat.GetActiveBrain(0).ItemId;
            f.Advance(26f);
            Assert.That(f.Request(1), Is.EqualTo(1));
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, 15f);
            string newcomer = f.ActiveIds().Single(id => id != older);
            float appearedAt = f.Habitat.HouseActivitySeconds;
            f.Until(() => f.Habitat.ActiveCount == 1, 3f);
            Assert.That(f.Habitat.GetActiveBrain(0).ItemId, Is.EqualTo(newcomer), "The older resident returned first.");
            f.Settings.RotationSeconds = 1f;
            float remaining = 24.5f - (f.Habitat.HouseActivitySeconds - appearedAt);
            f.Advance(remaining);
            Assert.That(f.Habitat.GetActiveBrain(0).ItemId, Is.EqualTo(newcomer),
                "Finishing overflow must not let normal roster rotation bypass the new visitor's 25-second minimum.");
            f.Until(() => f.Habitat.ActiveCount == 1 && f.Habitat.GetActiveBrain(0).ItemId != newcomer, 3f);
            Assert.That(f.Habitat.HouseActivitySeconds - appearedAt, Is.GreaterThanOrEqualTo(25f - .001f));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void FullCapacityFailedTapsDoNotPostponeTheDefaultReturnDeadline()
    {
        using (var f = new Fixture(40, input: true))
        {
            for (int tap = 0; tap < 10; tap++) f.Tap();
            f.DrainHouseQueue();
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24));
            float due = f.Habitat.HouseReturnDueAt;
            f.Advance(30f);
            for (int tap = 0; tap < 100; tap++)
            {
                Assert.That(f.Tap(), Is.True, "The house still shakes even though no additional fairy fits.");
                Assert.That(f.House.LastAcceptedSpawnCount, Is.Zero);
                f.Step(.05f);
                Assert.That(f.Habitat.HouseReturnDueAt, Is.EqualTo(due));
                Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            }
            f.Until(() => f.Habitat.ReturningHouseCount > 0, 11f);
            Assert.That(f.Habitat.HouseActivitySeconds, Is.InRange(due, due + .1f));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(24));
        }
    }

    [Test]
    public void NewVisitorReceivesTheFullTwentyFiveSecondMinimumWhenOlderResidentIsProtected()
    {
        using (var f = new Fixture(2, baseLimit: 1))
        {
            f.Config.TemporaryPlaySeconds = .1f;
            Assert.That(f.Config.MinimumIndividualStaySeconds, Is.EqualTo(25f));
            f.Settings.NewArrivalPrioritySeconds = 100f;
            string older = f.Habitat.GetActiveBrain(0).ItemId;
            Assert.That(f.Habitat.ConfirmAcquisition("memory-only-oldest-protection", f.Items[0]), Is.True);
            Assert.That(f.Request(1), Is.EqualTo(1));
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, 15f);
            string newcomer = f.ActiveIds().Single(id => id != older);
            float appearedAt = f.Habitat.HouseActivitySeconds;
            f.Advance(24.8f);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            Assert.That(f.ActiveIds(), Does.Contain(newcomer));
            f.Until(() => f.Habitat.ReturningHouseCount == 1, .4f);
            Assert.That(f.Habitat.HouseActivitySeconds - appearedAt, Is.GreaterThanOrEqualTo(25f - .001f));
            CollectionAssert.AreEqual(new[] { newcomer }, f.Habitat.ReturningHouseItemIds);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ReturnCompletionStartsTheVariableSixToTenSecondGapWithOnlyOneFairyTravelling()
    {
        using (var f = new Fixture(20, baseLimit: 6))
        {
            f.FastReturns();
            f.Config.ReturnTravelSeconds = .4f;
            f.Config.ReturnFadeSeconds = .25f;
            f.Config.ReturnStaggerMinSeconds = 6f;
            f.Config.ReturnStaggerMaxSeconds = 10f;
            Set(f.Habitat, "_houseReturnRandom", new System.Random(12345));
            f.ShowBaseResidentsOneAtATime();
            string[] oldest = f.ActiveIdsInOrder();
            f.Request(3);
            f.Request(3);
            var starts = new List<string>();
            var gaps = new List<float>();
            bool wasReturning = false;
            float completedAt = float.NaN;
            for (int frame = 0; frame < 2400; frame++)
            {
                f.Step(.05f);
                int count = f.Habitat.ReturningHouseCount;
                Assert.That(count, Is.InRange(0, 1));
                bool returning = count == 1;
                if (returning && !wasReturning)
                {
                    starts.Add(f.Habitat.ReturningHouseItemIds.Single());
                    if (!float.IsNaN(completedAt)) gaps.Add(f.Habitat.HouseActivitySeconds - completedAt);
                }
                if (!returning && wasReturning) completedAt = f.Habitat.HouseActivitySeconds;
                wasReturning = returning;
                if (starts.Count == 6 && !returning) break;
            }
            CollectionAssert.AreEqual(oldest, starts, "The gap variation must preserve oldest-first order.");
            Assert.That(gaps.Count, Is.EqualTo(5));
            foreach (float gap in gaps) Assert.That(gap, Is.InRange(6f - .001f, 10f + .1f));
            Assert.That(gaps.Max() - gaps.Min(), Is.GreaterThan(.1f), "The configured range must vary the intervals rather than use one fixed delay.");
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(6));
        }
    }

    [TestCase("floor-hidden")]
    [TestCase("ui-hidden")]
    [TestCase("hierarchy-disabled")]
    public void HiddenThirdFloorFreezesItsActivityClockAndSafelyClearsTransientWork(string reason)
    {
        using (var f = new Fixture(30))
        {
            f.Request(3);
            f.Advance(1f);
            float before = f.Habitat.HouseActivitySeconds;
            if (reason == "floor-hidden") f.Habitat.SetFloorVisible(true, false);
            else if (reason == "ui-hidden") Invoke(f.Habitat, "OnUiOpened");
            else
            {
                f.Host.SetActive(false);
                Invoke(f.Habitat, "OnDisable");
            }
            f.Advance(90f);
            Assert.That(f.Habitat.HouseActivitySeconds, Is.EqualTo(before));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            Assert.That(f.Habitat.ActiveCount, Is.Zero);
            f.Host.SetActive(true);
            f.Habitat.SetFloorVisible(true, true);
            f.Step(.05f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            Assert.That(f.Habitat.HouseActivitySeconds, Is.EqualTo(before + .05f).Within(.001f));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(30));
        }
    }

    [Test]
    public void OrdinaryEmergingAndReturningFairiesKeepTheirOriginalLayerAndDepthOrderAboveTheHouse()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor3/ThirdFloorFairyHouse.prefab");
        Assert.That(prefab, Is.Not.Null);
        var houseSprite = prefab.GetComponent<ThirdFloorFairyHouse>().Visual.GetComponent<SpriteRenderer>();
        using (var f = new Fixture(10, baseLimit: 1))
        {
            f.FastReturns();
            f.Settings.GroundArea = new Rect(-13.4f, 7.85f, 60f, 0f);
            f.Habitat.ResetOfflineSession(f.OwnedIds);
            f.Entrance.localPosition = new Vector3(0f, 9f, 0f);
            f.Step(.05f);
            f.AssertFairySortingAbove(houseSprite);
            f.Request(1);
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, 15f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(2));
            f.AssertFairySortingAbove(houseSprite);
            f.Until(() => f.Habitat.ReturningHouseCount == 1, 2f);
            for (int frame = 0; frame < 10; frame++)
            {
                f.AssertFairySortingAbove(houseSprite);
                f.Step(.05f);
            }
        }
    }

    [Test]
    public void EmergenceStartsAtTheDoorAndStaggersIntoTheSameAutonomousBrains()
    {
        using (var f = new Fixture(10, baseLimit: 1))
        {
            f.Entrance.localPosition = new Vector3(0f, 2f, 0f);
            Assert.That(f.Request(3), Is.EqualTo(3));
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, 15f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(2));
            var first = f.Habitat.GetActiveBrain(1);
            Assert.That(first.GroundPosition, Is.EqualTo((Vector2)f.Entrance.localPosition),
                "The exit begins at the actual elevated doorway, before reaching the ground bounds.");
            f.Until(() => f.Habitat.HouseEmergenceCount == 2, 15f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(3));
            var second = f.Habitat.GetActiveBrain(2);
            Assert.That(second.GroundPosition, Is.EqualTo((Vector2)f.Entrance.localPosition));
            Assert.That(first.GroundPosition, Is.Not.EqualTo(second.GroundPosition));
            f.DrainHouseQueue();
            f.Advance(f.Config.SafeEntranceDuration + .2f);
            Assert.That(f.Habitat.GetActiveBrain(1), Is.SameAs(first));
            Assert.That(f.Habitat.GetActiveBrain(2), Is.SameAs(second));
            Assert.That(first.ElapsedSeconds, Is.GreaterThan(0f));
            Assert.That(second.ElapsedSeconds, Is.GreaterThan(0f));
            Assert.That(first.GroundPosition.y, Is.EqualTo(f.Settings.GroundArea.y));
            Assert.That(second.GroundPosition.y, Is.EqualTo(f.Settings.GroundArea.y));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
        }
    }

    [Test]
    public void ReturningFairyTravelsToDoorAndShrinksBeforeReturningItsObjectToThePool()
    {
        using (var f = new Fixture(10, baseLimit: 1))
        {
            f.Config.TemporaryPlaySeconds = .2f;
            f.Config.MinimumIndividualStaySeconds = 0f;
            f.Config.ReturnMoveSpeed = 2f;
            f.Config.ReturnFadeSeconds = .5f;
            f.Entrance.localPosition = new Vector3(0f, 2f, 0f);
            var oldest = f.Habitat.GetActiveBrain(0);
            // Render the resident before the synchronous tap emergence so it is
            // genuinely the oldest visible fairy, not merely first in the list.
            f.Advance(.05f);
            oldest.PlaceForBirth(new Vector2(4f, 0f));
            var root = f.Host.transform.Find("Fairy " + oldest.ItemId);
            var body = root.Find("Item sprite").GetComponent<SpriteRenderer>();
            f.Request(1);
            f.Until(() => f.Habitat.HouseEmergenceCount == 1, f.Config.SafeExitClearanceTimeoutSeconds + 1f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(2));
            f.Until(() => f.Habitat.ReturningHouseCount == 1, f.Config.SafeTemporaryPlaySeconds + 1f);
            Assert.That(f.Habitat.ReturningHouseItemIds.Single(), Is.EqualTo(oldest.ItemId));
            Assert.That(f.HousePhase(oldest.ItemId), Is.EqualTo("Returning"));
            float before = Vector2.Distance(oldest.GroundPosition, f.Entrance.localPosition);
            float returnStartedAt = f.Habitat.HouseActivitySeconds;
            float speedLimit = Mathf.Min(f.Settings.WalkSpeed * f.Settings.CalmEnergy, f.Config.SafeReturnMoveSpeed);
            float travelBudget = Mathf.Max(f.Config.SafeReturnTravelSeconds, before / Mathf.Max(.001f, speedLimit));
            f.Advance(.2f);
            Assert.That(Vector2.Distance(oldest.GroundPosition, f.Entrance.localPosition), Is.LessThan(before));
            float travelled = before - Vector2.Distance(oldest.GroundPosition, f.Entrance.localPosition);
            Assert.That(travelled, Is.LessThanOrEqualTo(f.Settings.WalkSpeed * f.Settings.CalmEnergy * .2f + .001f));
            f.Until(() => Vector2.Distance(oldest.GroundPosition, f.Entrance.localPosition) <= .05f, travelBudget + .2f);
            Assert.That(f.Habitat.HouseActivitySeconds - returnStartedAt, Is.GreaterThanOrEqualTo(3.8f));
            float fullScale = body.transform.localScale.x;
            f.Advance(.2f);
            Assert.That(root.gameObject.activeSelf, Is.True, "The door fade must remain visible before release.");
            Assert.That(body.color.a, Is.InRange(.01f, .99f));
            Assert.That(body.transform.localScale.x, Is.LessThan(fullScale));
            f.Advance(.6f);
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(body.sprite, Is.Null, "The original pooled view releases its presentation references.");
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(1));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(2));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(10));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(12)]
    [TestCase(13)]
    public void ScarceOwnershipDoesNotInventDuplicateOrUnregisteredFairies(int owned)
    {
        using (var f = new Fixture(owned))
        {
            // Restore filters both duplicated ownership IDs and an uncatalogued ID.
            f.Habitat.RestoreOwnership(f.OwnedIds.Concat(f.OwnedIds).Concat(new[] { "not-in-catalog", "RECIPE" }));
            int accepted = f.Request(3);
            f.DrainHouseQueue();
            Assert.That(accepted, Is.EqualTo(Mathf.Min(3, Mathf.Max(0, owned - 12))));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(owned));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(owned));
            f.AssertUniqueOwned();
        }
    }

    [Test]
    public void RepeatedRequestsKeepQueueAndPoolBoundedAndReturnToTwelve()
    {
        using (var f = new Fixture(40))
        {
            f.FastReturns();
            for (int tap = 0; tap < 100; tap++)
            {
                Assert.That(f.Request(3), Is.InRange(0, 3));
                Assert.That(f.Habitat.PendingHouseSpawnCount, Is.LessThanOrEqualTo(12));
            }
            for (int frame = 0; frame < 1200; frame++)
            {
                f.Step(.05f);
                Assert.That(f.Habitat.ActiveCount, Is.LessThanOrEqualTo(24));
                Assert.That(f.Habitat.PooledObjectCount, Is.LessThanOrEqualTo(24));
                Assert.That(f.Habitat.ReturningHouseCount, Is.LessThanOrEqualTo(1));
                f.AssertUniqueOwned();
                if (f.Habitat.PendingHouseSpawnCount == 0 && f.Habitat.ActiveCount == 12
                    && f.Habitat.ReturningHouseCount == 0) break;
            }
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12), "Repeated tapping cannot create an unlimited backlog.");
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(40));
            int allocated = f.Habitat.PooledObjectCount;
            f.Request(3);
            f.Advance(1f);
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(allocated), "The second outing must reuse the existing pool.");
        }
    }

    [Test]
    public void CrowdedDoorwayCannotDelayTheAcceptedBurstOrLeaveLateRequestsAfterCancellation()
    {
        using (var f = new Fixture(40))
        {
            // A deliberately crowded floor exercises prompt presentation independently
            // of the later natural walking/dispersal policy.
            f.Settings.GroundArea = new Rect(-10f, 0f, 20f, 0f);
            f.Habitat.ResetOfflineSession(f.OwnedIds);
            for (int tap = 0; tap < 4; tap++) Assert.That(f.Request(3), Is.EqualTo(3));
            f.DrainHouseQueue();
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24));
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            float deadline = f.Habitat.HouseReturnDueAt;
            for (int tap = 0; tap < 20; tap++)
            {
                Assert.That(f.Request(3), Is.Zero);
                f.Step(.05f);
                Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
                Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24));
                Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(24));
                Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12));
                Assert.That(f.Habitat.HouseReturnDueAt, Is.EqualTo(deadline));
                f.AssertUniqueOwned();
            }
            f.Habitat.SetFloorVisible(true, false);
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            f.Advance(70f);
            f.Habitat.SetFloorVisible(true, true);
            f.Advance(.5f);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12), "Hidden/reopened rooms cannot replay old tap requests.");
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(40));
            f.AssertUniqueOwned();
        }
    }

    [Test]
    public void ExcessFairiesPlayThenOldestResidentsReturnWithAStagger()
    {
        using (var f = new Fixture(20, baseLimit: 2))
        {
            f.FastReturns();
            f.Config.TemporaryPlaySeconds = .6f;
            f.Config.ReturnStaggerMinSeconds = f.Config.ReturnStaggerMaxSeconds = .6f;
            f.Config.ReturnTravelSeconds = .6f;
            f.ShowBaseResidentsOneAtATime();
            var oldest = f.ActiveIdsInOrder();
            for (int i = 0; i < f.Habitat.ActiveCount; i++)
                f.Habitat.GetActiveBrain(i).PlaceForBirth(new Vector2(4f, 0f));
            f.Request(2);
            f.DrainHouseQueue();
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(4));
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero, "Excess is allowed to play before returning.");
            f.Until(() => f.Habitat.ReturningHouseCount > 0, 3f);
            CollectionAssert.AreEqual(new[] { oldest[0] }, f.Habitat.ReturningHouseItemIds);
            f.Advance(.35f);
            Assert.That(f.Habitat.ReturningHouseCount, Is.EqualTo(1), "Return starts must be staggered, not simultaneous.");
            f.Until(() => f.Habitat.ReturningHouseCount == 0, 2f);
            f.Advance(.35f);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero, "The gap begins after the first return has finished.");
            f.Until(() => f.Habitat.ReturningHouseCount == 1, 2f);
            CollectionAssert.AreEqual(new[] { oldest[1] }, f.Habitat.ReturningHouseItemIds);
            f.Advance(20f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(2));
            Assert.That(oldest.Intersect(f.ActiveIds()), Is.Empty);
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(20));
        }
    }

    [Test]
    public void TouchDuringReturnWaitsForSpaceAndConvergesWithoutDuplicatingViews()
    {
        using (var f = new Fixture(40))
        {
            f.FastReturns();
            f.Config.TemporaryPlaySeconds = .5f;
            f.Request(3);
            f.Request(3);
            f.Until(() => f.Habitat.HouseEmergenceCount > 0, 30f);
            f.Until(() => f.Habitat.ReturningHouseCount > 0, 40f);
            Assert.That(f.Request(3), Is.InRange(0, 3));
            for (int frame = 0; frame < 1200; frame++)
            {
                f.Step(.05f);
                Assert.That(f.Habitat.ActiveCount, Is.LessThanOrEqualTo(24));
                Assert.That(f.Habitat.PendingHouseSpawnCount, Is.LessThanOrEqualTo(12));
                Assert.That(f.Habitat.PooledObjectCount, Is.LessThanOrEqualTo(24));
                Assert.That(f.Habitat.ReturningHouseCount, Is.LessThanOrEqualTo(1));
                f.AssertUniqueOwned();
                if (f.Habitat.PendingHouseSpawnCount == 0 && f.Habitat.ActiveCount == 12
                    && f.Habitat.ReturningHouseCount == 0) break;
            }
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
        }
    }

    [Test]
    public void PendingAndProtectedAcquisitionKeepsItsOriginalBirthAndIsNotSentHome()
    {
        using (var f = new Fixture(12))
        {
            f.FastReturns();
            f.Settings.NewArrivalPrioritySeconds = 8f;
            f.Config.TemporaryPlaySeconds = .2f;
            var acquired = f.Items[12];
            Assert.That(f.Habitat.ConfirmAcquisition("memory-only-house-protection", acquired), Is.True);
            int births = f.Habitat.ArrivalPresentationCount;
            Assert.That(births, Is.EqualTo(1));
            f.Habitat.RestoreOwnership(f.OwnedIds.Concat(f.Items.Skip(12).Take(8).Select(item => item.Id)));
            Assert.That(f.Request(3), Is.GreaterThan(0));
            for (int frame = 0; frame < 60; frame++)
            {
                f.Step(.05f);
                Assert.That(f.ActiveIds(), Does.Contain(acquired.Id));
                Assert.That(f.Habitat.ReturningHouseItemIds, Does.Not.Contain(acquired.Id));
                Assert.That(f.ActiveIds().Count, Is.EqualTo(f.Habitat.ActiveCount));
            }
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(births));
            f.Advance(f.Settings.BirthDuration + 1f);
            Assert.That(f.Arrivals.PendingCount, Is.Zero, "The original acquisition reaches its normal completed landing.");
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void AcquisitionTakingAQueuedHouseSlotStillReturnsOverflowAfterItsProtectedBirth()
    {
        using (var f = new Fixture(13))
        {
            f.FastReturns();
            f.Config.TapSpawnFirstDelay = .2f; // Keep a real configurable reservation window for acquisition priority.
            var queued = f.Items.Single(item => f.OwnedIds.Contains(item.Id) && !f.ActiveIds().Contains(item.Id));
            Assert.That(f.Request(1), Is.EqualTo(1));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.EqualTo(1));
            // A newly confirmed acquisition has presentation priority over the reserved house exit.
            Assert.That(f.Habitat.ConfirmAcquisition("memory-only-queued-house-birth", queued), Is.True);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(13));
            f.Advance(.1f);
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.Zero);
            Assert.That(f.Habitat.ActiveBirthItemIds, Does.Contain(queued.Id));
            f.Advance(f.Settings.BirthDuration + .2f);
            Assert.That(f.Arrivals.PendingCount, Is.Zero);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            f.Advance(f.Settings.NewArrivalPrioritySeconds + f.Config.TemporaryPlaySeconds
                + f.Config.ReturnTimeoutSeconds + f.Config.ReturnFadeSeconds + 1f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12),
                "An acquisition that consumed the queue must not leave an infinite temporary display slot.");
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(13));
            f.AssertUniqueOwned();
        }
    }

    [TestCase("floor-hidden")]
    [TestCase("owner-unregistered")]
    [TestCase("hierarchy-disabled")]
    [TestCase("session-reset")]
    public void LifecycleCancellationFinishesOverflowAndDoesNotResumeAnOldQueue(string reason)
    {
        using (var f = new Fixture(30))
        {
            f.Request(3);
            f.Request(3);
            f.Until(() => f.Habitat.HouseEmergenceCount > 0, 15f);
            Assert.That(f.Habitat.ActiveCount, Is.GreaterThan(12));
            switch (reason)
            {
                case "floor-hidden": f.Habitat.SetFloorVisible(true, false); break;
                case "owner-unregistered": f.Habitat.UnregisterHouse(f.Owner); break;
                case "hierarchy-disabled":
                    f.Host.SetActive(false);
                    Invoke(f.Habitat, "OnDisable"); // ordinary lifecycle is not automatically scheduled by Edit Mode
                    break;
                case "session-reset": f.Habitat.ResetOfflineSession(f.OwnedIds); break;
            }
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.ReturningHouseCount, Is.Zero);
            f.Host.SetActive(true);
            f.Habitat.SetFloorVisible(true, true);
            f.Advance(1f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(12));
            f.AssertUniqueOwned();
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(30));
        }
    }

    [Test]
    public void TapStagesOneTwoThreeAndLaterClampAndResetAfterConfiguredInterval()
    {
        using (var f = new Fixture(40, input: true))
        {
            int[] counts = { 1, 2, 3, 3, 3 };
            for (int i = 0; i < counts.Length; i++)
            {
                Assert.That(f.Tap(), Is.True);
                Assert.That(f.House.CurrentShakeStage, Is.EqualTo(Mathf.Min(i + 1, 3)));
                Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(counts[i]));
                f.Step(.1f);
            }
            f.Advance(f.Config.TapSequenceResetSeconds + .1f);
            Assert.That(f.Tap(), Is.True);
            Assert.That(f.House.CurrentShakeStage, Is.EqualTo(1));
            Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(1));
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void ActualHouseTapBurstsStartDuringTheShakeAndFinishOnTheConfiguredScheduleAtBothFrameRates(int framesPerSecond)
    {
        using (var f = new Fixture(40, input: true))
        {
            float dt = 1f / framesPerSecond;
            Assert.That(f.Config.TapSpawnFirstDelay, Is.Zero);
            Assert.That(f.Config.SpawnInterval, Is.EqualTo(.18f));
            Assert.That(f.Config.EntranceDuration, Is.EqualTo(.45f));
            Assert.That(f.Config.TapDispersalSpeedMultiplier, Is.EqualTo(1f));
            int[] requests = { 1, 2, 3, 3 };
            for (int tap = 0; tap < requests.Length; tap++)
                f.AssertTapBurst(requests[tap], Mathf.Min(tap + 1, 3), dt);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(9));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(21));
            f.Advance(f.Config.TapSequenceResetSeconds + .1f);
            f.AssertTapBurst(1, 1, dt);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(10));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(22));
            f.AssertUniqueOwned();
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void ConfiguredFirstTapDelayAppliesOnceThenUsesTheConfiguredCadence(int framesPerSecond)
    {
        using (var f = new Fixture(20, input: true))
        {
            f.Config.TapSpawnFirstDelay = .2f;
            f.Config.FirstTapSpawnCount = 3;
            f.AssertTapBurst(3, 1, 1f / framesPerSecond, .2f);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(3));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(15));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void RapidActualTapsKeepTwentyFourUniqueAndFullCapacityNeverReservesFutureReturnSlots(int framesPerSecond)
    {
        using (var f = new Fixture(40, input: true))
        {
            f.FastReturns();
            int accepted = 0;
            for (int tap = 0; tap < 100; tap++)
            {
                Assert.That(f.Tap(), Is.True);
                accepted += f.House.LastAcceptedSpawnCount;
                Assert.That(f.Habitat.ActiveCount + f.Habitat.PendingHouseSpawnCount, Is.LessThanOrEqualTo(24));
                f.AssertUniqueOwned();
            }
            Assert.That(accepted, Is.EqualTo(12));
            float dt = 1f / framesPerSecond;
            float spawnBudget = f.Config.SafeTapSpawnFirstDelay
                + f.Habitat.PendingHouseSpawnCount * f.Config.SafeSpawnInterval + dt;
            for (int frame = 0; frame < Mathf.CeilToInt(spawnBudget / dt)
                && f.Habitat.PendingHouseSpawnCount > 0; frame++)
            {
                f.Step(dt);
                Assert.That(f.Habitat.ActiveCount, Is.LessThanOrEqualTo(24));
                f.AssertUniqueOwned();
            }
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24));
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            f.Until(() => f.Habitat.ReturningHouseCount == 1, 3f);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(24), "A returning sprite still occupies its display slot.");
            float due = f.Habitat.HouseReturnDueAt;
            Assert.That(f.Tap(), Is.True, "A full house still shakes.");
            Assert.That(f.House.LastAcceptedSpawnCount, Is.Zero);
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero, "A due return is not permission to reserve a future slot.");
            Assert.That(f.Habitat.HouseReturnDueAt, Is.EqualTo(due));
            f.Until(() => f.Habitat.ActiveCount < 24, 5f);
            f.Advance(1f);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(12), "Rejected full-cap taps cannot spawn later after a slot opens.");
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(40));
            f.AssertUniqueOwned();
        }
    }

    [TestCase(12, 0)]
    [TestCase(13, 1)]
    [TestCase(14, 2)]
    [TestCase(15, 3)]
    public void ActualThreeFairyTapReducesToAvailableOwnershipWithoutADeferredShortfall(int owned, int expected)
    {
        using (var f = new Fixture(owned, input: true))
        {
            f.Config.FirstTapSpawnCount = 3;
            Assert.That(f.Tap(), Is.True);
            Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(3));
            Assert.That(f.House.LastAcceptedSpawnCount, Is.EqualTo(expected));
            Assert.That(f.House.IsShaking, Is.True);
            f.DrainHouseQueue();
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(expected));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(owned));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            f.Advance(5f);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(expected));
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            f.AssertUniqueOwned();
        }
    }

    [Test]
    public void ShakeOnlyMovesVisualAndRestoresAuthoredTransformEvenAfterInterruptedRetap()
    {
        using (var f = new Fixture(0, input: true))
        {
            Vector3 position = f.Visual.localPosition, scale = f.Visual.localScale;
            Quaternion rotation = f.Visual.localRotation;
            Vector3 colliderPosition = f.Hit.transform.position;
            Quaternion colliderRotation = f.Hit.transform.rotation;
            bool sawRotation = false;
            for (int tap = 0; tap < 5; tap++)
            {
                Assert.That(f.Tap(), Is.True, "No waiting fairy must still allow the house to shake.");
                Assert.That(f.House.LastAcceptedSpawnCount, Is.Zero);
                for (int frame = 0; frame < 5; frame++)
                {
                    f.Step(.025f);
                    float angle = Quaternion.Angle(rotation, f.Visual.localRotation);
                    sawRotation |= angle > .1f;
                    Assert.That(angle, Is.LessThanOrEqualTo(7.001f));
                    Assert.That(f.Visual.localPosition, Is.EqualTo(position));
                    Assert.That(f.Visual.localScale, Is.EqualTo(scale));
                    Assert.That(f.Hit.transform.position, Is.EqualTo(colliderPosition));
                    Assert.That(f.Hit.transform.rotation, Is.EqualTo(colliderRotation));
                }
            }
            Assert.That(sawRotation, Is.True);
            f.Advance(f.Config.ShakeDuration + .2f);
            Assert.That(f.House.IsShaking, Is.False);
            Assert.That(f.Visual.localPosition, Is.EqualTo(position));
            Assert.That(f.Visual.localScale, Is.EqualTo(scale));
            Assert.That(f.Visual.localRotation, Is.EqualTo(rotation));
            f.Tap();
            f.Step(.05f);
            f.House.enabled = false;
            Invoke(f.House, "OnDisable");
            Assert.That(f.Visual.localRotation, Is.EqualTo(rotation));
            Assert.That(f.Visual.localPosition, Is.EqualTo(position));
            Assert.That(f.Visual.localScale, Is.EqualTo(scale));
        }
    }

    [TestCase("out-and-back-drag")]
    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("floor-hidden")]
    [TestCase("disabled")]
    [TestCase("outside-hit-area")]
    public void ExistingCameraCompletionRejectsInvalidHouseGesturesAndNextTapRecovers(string reason)
    {
        using (var f = new Fixture(30, input: true))
        {
            Vector2 point = f.ScreenPoint;
            Invoke(f.CameraInput, "BeginWorldTap", point);
            switch (reason)
            {
                case "out-and-back-drag": Invoke(f.CameraInput, "TrackWorldTap", point + Vector2.right * 300f); break;
                case "camera-moving": Set(f.CameraInput, "_isMoveAction", true); break;
                case "world-block": f.CameraInput.SetWorldInputBlocked(f, true); break;
                case "floor-hidden": f.Habitat.SetFloorVisible(true, false); break;
                case "disabled": f.House.enabled = false; break;
                case "outside-hit-area":
                    point = f.WorldCamera.WorldToScreenPoint(f.Hit.transform.position + Vector3.right * 10f);
                    Invoke(f.CameraInput, "BeginWorldTap", point);
                    break;
            }
            Assert.That(Invoke(f.CameraInput, "CompleteWorldTap", point), Is.False, reason);
            Assert.That(f.House.LastRequestedSpawnCount, Is.Zero);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.Zero);
            Set(f.CameraInput, "_isMoveAction", false);
            f.CameraInput.SetWorldInputBlocked(f, false);
            f.Habitat.SetFloorVisible(true, true);
            f.House.enabled = true;
            f.House.ConfigureOffline(f.Habitat, f.Visual, f.Hit, f.Entrance, f.WorldCamera, f.CameraInput);
            Invoke(f.CameraInput, "BeginWorldTap", f.ScreenPoint);
            Assert.That(Invoke(f.CameraInput, "CompleteWorldTap", f.ScreenPoint), Is.True);
            Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExistingEarlierWorldTapConsumerKeepsPriorityOverTheHouse()
    {
        using (var f = new Fixture(30, input: true, earlierConsumer: true))
        {
            Invoke(f.CameraInput, "BeginWorldTap", f.ScreenPoint);
            Assert.That(Invoke(f.CameraInput, "CompleteWorldTap", f.ScreenPoint), Is.True);
            Assert.That(f.EarlierTapCount, Is.EqualTo(1));
            Assert.That(f.House.LastRequestedSpawnCount, Is.Zero, "The existing fairy inspection handler may consume a tap first.");
            Assert.That(f.Habitat.HouseEmergenceCount, Is.Zero);
        }
    }

    [TestCase(VisibleState.Appearing)]
    [TestCase(VisibleState.Appeared)]
    [TestCase(VisibleState.Disappearing)]
    public void RealVisibleMapOrClosingPopupBlocksHouseUntilItsViewHasDisappeared(VisibleState state)
    {
        using (var f = new Fixture(30, input: true))
        {
            // Inject only local presentation dependencies into the real visibility guard.
            // InitializeRuntime (and its UserInfo/catalog access) is deliberately not called.
            var main = f.Create("Inactive local floor state").AddComponent<MainScene>();
            Set(main, "_currentFloor", ERestaurantFloorType.Floor3);
            Set(f.CameraInput, "_mainScene", main);
            Set(f.Habitat, "_runtime", true);
            Set(f.Habitat, "_worldCamera", f.WorldCamera);
            Set(f.Habitat, "_cameraController", f.CameraInput);
            var map = f.Create("Actual map UIView", true).AddComponent<UIFloor3>();
            map.VisibleState = state;
            Set(f.Habitat, "_sceneViews", new UIView[] { map });
            Assert.That(f.Tap(), Is.False);
            Assert.That(f.House.LastRequestedSpawnCount, Is.Zero);
            map.VisibleState = VisibleState.Disappeared;
            Assert.That(f.Tap(), Is.True);
            Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(1));
            Set(f.Habitat, "_runtime", false);
        }
    }

    [Test]
    public void AmbientHouseHintMovesOnlyTheVisualWithoutRequestingOrCreatingFairies()
    {
        using (var f = new Fixture(30, input: true))
        {
            f.Config.FirstHintDelay = new Vector2(.1f, .1f);
            var ids = f.ActiveIds();
            int pooled = f.Habitat.PooledObjectCount;
            Vector3 position = f.Visual.localPosition, scale = f.Visual.localScale;
            Quaternion rotation = f.Visual.localRotation;
            Vector3 hitPosition = f.Hit.transform.position;
            Vector2 hitSize = f.Hit.size;
            bool moved = false;
            float maxAngle = 0f;
            for (int i = 0; i < 45; i++)
            {
                f.Step(.02f);
                moved |= f.House.IsIdleHintPlaying;
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rotation, f.Visual.localRotation));
            }
            Assert.That(moved, Is.True);
            Assert.That(maxAngle, Is.InRange(.05f, 1.51f));
            Assert.That(f.Visual.localPosition, Is.EqualTo(position));
            Assert.That(f.Visual.localScale, Is.EqualTo(scale));
            Assert.That(f.Visual.localRotation, Is.EqualTo(rotation));
            Assert.That(f.Hit.transform.position, Is.EqualTo(hitPosition));
            Assert.That(f.Hit.size, Is.EqualTo(hitSize));
            Assert.That(f.House.LastRequestedSpawnCount, Is.Zero);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.Zero);
            Assert.That(f.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(pooled));
            CollectionAssert.AreEquivalent(ids, f.ActiveIds());
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(30));
        }
    }

    [Test]
    public void ActualHouseTapImmediatelyReplacesTheAmbientHintWithItsOriginalSpawnAndShake()
    {
        using (var f = new Fixture(30, input: true))
        {
            f.Config.FirstHintDelay = new Vector2(.1f, .1f);
            Quaternion rotation = f.Visual.localRotation;
            f.Advance(.25f);
            Assert.That(f.House.IsIdleHintPlaying, Is.True);
            Assert.That(f.Tap(), Is.True);
            Assert.That(f.House.IsIdleHintPlaying, Is.False);
            Assert.That(f.House.IsShaking, Is.True);
            Assert.That(f.House.CurrentShakeStage, Is.EqualTo(1));
            Assert.That(f.House.LastRequestedSpawnCount, Is.EqualTo(1));
            f.Config.EnableIdleHint = false;
            f.Advance(1f);
            Assert.That(f.Habitat.HouseEmergenceCount, Is.EqualTo(1));
            Assert.That(f.Visual.localRotation, Is.EqualTo(rotation));
            f.AssertUniqueOwned();
        }
    }

    [TestCase("floor-hidden")]
    [TestCase("camera-moving")]
    [TestCase("world-block")]
    [TestCase("offscreen")]
    [TestCase("disabled")]
    public void HouseAmbientHintStopsAndRestoresWhenPresentationIsUnavailable(string reason)
    {
        using (var f = new Fixture(30, input: true))
        {
            f.Config.FirstHintDelay = new Vector2(.1f, .1f);
            Quaternion rotation = f.Visual.localRotation;
            f.Advance(.25f);
            Assert.That(f.House.IsIdleHintPlaying, Is.True);
            switch (reason)
            {
                case "floor-hidden": f.Habitat.SetFloorVisible(true, false); break;
                case "camera-moving": Set(f.CameraInput, "_isMoveAction", true); break;
                case "world-block": f.CameraInput.SetWorldInputBlocked(f, true); break;
                case "offscreen": f.WorldCamera.transform.position += Vector3.right * 100f; break;
                case "disabled": f.House.enabled = false; Invoke(f.House, "OnDisable"); break;
            }
            f.Step(.05f);
            Assert.That(f.House.IsIdleHintPlaying, Is.False);
            Assert.That(f.Visual.localRotation, Is.EqualTo(rotation));
            Assert.That(f.Habitat.HouseEmergenceCount, Is.Zero);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
        private readonly Texture2D _texture;
        private readonly Sprite _sprite;
        internal readonly GameObject Host;
        internal readonly EnhancementFairyHabitat Habitat;
        internal readonly EnhancementFairySettings Settings;
        internal readonly EnhancementFairyHouseSettings Config = new EnhancementFairyHouseSettings();
        internal readonly EnhancementFairyArrivalQueue Arrivals = new EnhancementFairyArrivalQueue();
        internal readonly List<GachaItemData> Items = new List<GachaItemData>();
        internal readonly string[] OwnedIds;
        internal readonly Transform Entrance;
        internal readonly object Owner;
        internal readonly ThirdFloorFairyHouse House;
        internal readonly Transform Visual;
        internal readonly BoxCollider2D Hit;
        internal readonly Camera WorldCamera;
        internal readonly CameraController CameraInput;
        internal int EarlierTapCount;

        internal Fixture(int owned, int baseLimit = 12, bool input = false, bool earlierConsumer = false)
        {
            try
            {
                Settings = ScriptableObject.CreateInstance<EnhancementFairySettings>();
                Settings.MaxActive = baseLimit;
                Settings.RotationSeconds = 10000f;
                // Stage1 has 60 units of floor, with the house 13.4 units from its
                // left edge. A ten-unit fixture cannot fit 24 full-size sprites.
                Settings.GroundArea = new Rect(-13.4f, 0f, 60f, 0f);
                Settings.BirthArea = new Rect(-4f, 1f, 8f, 3f);
                Settings.BirthStartDelay = new Vector2(.1f, .1f);
                _texture = new Texture2D(4, 4);
                _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), Vector2.one * .5f);
                for (int i = 0; i < 40; i++) Items.Add(Item("ITEM" + i.ToString("D2"), UpgradeType.UPGRADE01));
                Items.Add(Item("RECIPE", UpgradeType.None));
                OwnedIds = Items.Take(owned).Select(item => item.Id).ToArray();
                Host = Create("Detached real fairy house test habitat", true);
                Habitat = Host.AddComponent<EnhancementFairyHabitat>();
                Habitat.ConfigureOffline(Items, OwnedIds, Settings, Arrivals);
                Entrance = Create("House entrance", true).transform;
                Entrance.SetParent(Host.transform, false);
                if (!input)
                {
                    Owner = this;
                    Habitat.RegisterHouse(Owner, Entrance, Config);
                    return;
                }
                var root = Create("Detached house root", true);
                root.transform.SetParent(Host.transform, false);
                Visual = Create("Independent house visual", true).transform;
                Visual.SetParent(root.transform, false);
                Visual.localPosition = new Vector3(.25f, .4f, 0f);
                Visual.localScale = new Vector3(1.2f, .9f, 1f);
                Visual.localRotation = Quaternion.Euler(0f, 0f, 13f);
                Visual.gameObject.AddComponent<SpriteRenderer>().sprite = _sprite;
                var touch = Create("Stationary house hit area", true);
                touch.transform.SetParent(root.transform, false);
                Hit = touch.AddComponent<BoxCollider2D>();
                Hit.size = new Vector2(3f, 3f);
                Hit.isTrigger = true;
                var cameraRoot = Create("Inactive test input camera");
                WorldCamera = cameraRoot.AddComponent<Camera>();
                WorldCamera.orthographic = true;
                WorldCamera.orthographicSize = 10f;
                WorldCamera.pixelRect = new Rect(0f, 0f, 1024f, 768f);
                WorldCamera.transform.position = new Vector3(0f, 0f, -20f);
                CameraInput = cameraRoot.AddComponent<CameraController>();
                if (earlierConsumer) CameraInput.OnWorldTapHandler += _ => { EarlierTapCount++; return true; };
                House = root.AddComponent<ThirdFloorFairyHouse>();
                Set(House, "_settings", Config);
                House.ConfigureOffline(Habitat, Visual, Hit, Entrance, WorldCamera, CameraInput);
                Owner = House;
                Physics2D.SyncTransforms();
            }
            catch { Dispose(); throw; }
        }

        internal GameObject Create(string name, bool active = false)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, _scene);
            if (active) go.SetActive(true);
            return go;
        }

        private GachaItemData Item(string id, UpgradeType type)
        {
            var item = ScriptableObject.CreateInstance<GachaItemData>();
            Set(item, "_id", id);
            Set(item, "_upgradeType", type);
            Set(item, "_sprite", _sprite);
            return item;
        }

        internal Vector2 ScreenPoint => WorldCamera.WorldToScreenPoint(Hit.transform.position);
        internal bool Tap() => House.TryTapAtWorldPoint(Hit.transform.position);
        internal int Request(int count) => Habitat.RequestHouseEmergence(Owner, count);
        internal string HousePhase(string id)
        {
            foreach (object view in (System.Collections.IEnumerable)Field(typeof(EnhancementFairyHabitat), "_views").GetValue(Habitat))
            {
                var brain = (EnhancementFairyBrain)Field(view.GetType(), "Brain").GetValue(view);
                if (brain != null && brain.ItemId == id) return Field(view.GetType(), "HouseMotion").GetValue(view).ToString();
            }
            return "Missing";
        }

        internal void AssertTapBurst(int expected, int expectedStage, float dt, float firstDelay = 0f)
        {
            var before = ActiveIds();
            float tappedAt = Habitat.HouseActivitySeconds;
            var starts = new Dictionary<string, float>();
            var finishes = new Dictionary<string, float>();
            var dispersalStarts = new Dictionary<string, float>();
            Assert.That(Tap(), Is.True);
            Assert.That(House.CurrentShakeStage, Is.EqualTo(expectedStage));
            Assert.That(House.LastRequestedSpawnCount, Is.EqualTo(expected));
            Assert.That(House.LastAcceptedSpawnCount, Is.EqualTo(expected));
            Quaternion restRotation = Visual.localRotation;
            bool sawMovingShake = false;
            // Spawn cadence and animation completion each round to a frame boundary.
            float completionBound = firstDelay + (expected - 1) * Config.SafeSpawnInterval
                + Config.SafeEntranceDuration + 2f * dt + .001f;
            int frames = Mathf.CeilToInt(completionBound / dt) + 1;
            for (int frame = 0; frame < frames; frame++)
            {
                float elapsed = Habitat.HouseActivitySeconds - tappedAt;
                foreach (string id in ActiveIds())
                {
                    if (before.Contains(id)) continue;
                    string phase = HousePhase(id);
                    if (!starts.ContainsKey(id))
                    {
                        starts.Add(id, elapsed);
                        Assert.That(phase, Is.EqualTo("Emerging"), "Observe the actual start, not the end of the doorway animation.");
                        if (starts.Count == 1)
                            Assert.That(House.IsShaking, Is.True, "The first fairy must appear while the house is still shaking.");
                    }
                    if (phase != "Emerging" && !finishes.ContainsKey(id)) finishes.Add(id, elapsed);
                    if (Habitat.IsItemDispersing(id) && !dispersalStarts.ContainsKey(id)) dispersalStarts.Add(id, elapsed);
                }
                sawMovingShake |= House.IsShaking && Quaternion.Angle(restRotation, Visual.localRotation) > .01f;
                Assert.That(Habitat.ActiveCount, Is.LessThanOrEqualTo(24));
                AssertUniqueOwned();
                if (finishes.Count == expected) break;
                Step(dt);
            }
            Assert.That(starts.Count, Is.EqualTo(expected));
            Assert.That(finishes.Count, Is.EqualTo(expected));
            Assert.That(dispersalStarts.Count, Is.EqualTo(expected), "Every completed emergence hands the same fairy to its real outward walk.");
            float[] times = starts.Values.OrderBy(value => value).ToArray();
            Assert.That(times[0], Is.GreaterThanOrEqualTo(firstDelay - .001f));
            Assert.That(times[0], Is.LessThanOrEqualTo(firstDelay == 0f ? .1f : firstDelay + dt + .001f));
            for (int index = 1; index < times.Length; index++)
                Assert.That(times[index] - times[index - 1], Is.EqualTo(Config.SafeSpawnInterval).Within(dt + .001f));
            foreach (var finish in finishes)
                Assert.That(finish.Value - starts[finish.Key],
                    Is.EqualTo(Config.SafeEntranceDuration).Within(dt + .001f),
                    "Each fairy keeps the configured natural doorway animation duration.");
            Assert.That(finishes.Values.Max(), Is.LessThanOrEqualTo(completionBound));
            Assert.That(sawMovingShake, Is.True);
            Assert.That(Habitat.PendingHouseSpawnCount, Is.Zero);
        }
        internal string[] ActiveIdsInOrder() => Enumerable.Range(0, Habitat.ActiveCount).Select(i => Habitat.GetActiveBrain(i).ItemId).ToArray();
        internal HashSet<string> ActiveIds() => new HashSet<string>(ActiveIdsInOrder(), StringComparer.Ordinal);
        internal void AssertUniqueOwned()
        {
            var ids = ActiveIds();
            Assert.That(ids.Count, Is.EqualTo(Habitat.ActiveCount), "Only one view per owned item ID.");
            Assert.That(ids.IsSubsetOf(new HashSet<string>(OwnedIds)), Is.True, "No unowned, recipe, or missing-catalog ID may be created.");
        }

        internal void AssertFairySortingAbove(SpriteRenderer houseSprite)
        {
            for (int i = 0; i < Habitat.ActiveCount; i++)
            {
                var brain = Habitat.GetActiveBrain(i);
                var body = Host.transform.Find("Fairy " + brain.ItemId + "/Item sprite").GetComponent<SpriteRenderer>();
                Assert.That(body.sortingLayerName, Is.EqualTo(Settings.SortingLayer));
                Assert.That(body.sortingOrder, Is.EqualTo(500 - Mathf.RoundToInt(brain.GroundPosition.y * 10f)));
                Assert.That(SortingLayer.GetLayerValueFromID(body.sortingLayerID),
                    Is.GreaterThan(SortingLayer.GetLayerValueFromID(houseSprite.sortingLayerID)));
            }
        }

        internal void Step(float dt)
        {
            Habitat.AdvancePreview(dt);
            if (House != null) House.AdvancePreview(dt);
        }

        internal void FastReturns()
        {
            // Only selected lifecycle/capacity tests shorten time. Default-timing tests
            // below advance the full 45/25/6..10 seconds through the same runtime update.
            Config.TemporaryPlaySeconds = .5f;
            Config.MinimumIndividualStaySeconds = 0f;
            Config.ReturnStaggerMinSeconds = .05f;
            Config.ReturnStaggerMaxSeconds = .1f;
            Config.ReturnTravelSeconds = .1f;
            Config.ReturnMoveSpeed = Settings.WalkSpeed = 40f;
            Config.ReturnTimeoutSeconds = 10f;
            Config.ReturnFadeSeconds = .1f;
            // Keep the real autonomous actions: a permanently frozen resident just
            // beyond the doorway would make the exit impossible, not exercise returns.
        }

        internal void ShowBaseResidentsOneAtATime()
        {
            // Make actual first-visibility ordering observable rather than relying on
            // an arbitrary tie between several sprites revealed on the same frame.
            Habitat.ResetOfflineSession(Array.Empty<string>());
            int count = Mathf.Min(OwnedIds.Length, Settings.MaxActive);
            for (int i = 1; i <= count; i++)
            {
                Habitat.RestoreOwnership(OwnedIds.Take(i));
                Advance(.1f);
            }
            Habitat.RestoreOwnership(OwnedIds);
        }

        internal void Advance(float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / .05f);
            for (int frame = 0; frame < frames; frame++) Step(.05f);
        }

        internal void DrainHouseQueue()
        {
            // A tap burst is bounded by its configured first delay and cadence;
            // doorway occupancy must not turn it into a delayed admission queue.
            float budget = Config.SafeTapSpawnFirstDelay
                + Habitat.PendingHouseSpawnCount * Config.SafeSpawnInterval
                + Config.SafeEntranceDuration + 1f;
            Until(() => Habitat.PendingHouseSpawnCount == 0, budget);
        }

        internal void Until(Func<bool> predicate, float timeout)
        {
            int frames = Mathf.CeilToInt(timeout / .05f);
            while (!predicate() && frames-- > 0) Step(.05f);
            Assert.That(predicate(), Is.True, "The actual presentation state did not advance before the bounded deadline. "
                + "active=" + Habitat.ActiveCount + "; pending=" + Habitat.PendingHouseSpawnCount
                + "; emerged=" + Habitat.HouseEmergenceCount + "; returning=" + Habitat.ReturningHouseCount
                + "; activitySeconds=" + Habitat.HouseActivitySeconds + "; exit=" + Habitat.LastHouseExitDecision);
        }

        public void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var item in Items) Object.DestroyImmediate(item);
            if (Settings != null) Object.DestroyImmediate(Settings);
            if (_sprite != null) Object.DestroyImmediate(_sprite);
            if (_texture != null) Object.DestroyImmediate(_texture);
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.Name, name);
    }

    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
#endif
