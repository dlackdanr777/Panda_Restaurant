#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Muks.UI;

public sealed class EnhancementFairyTests
{
    [Test]
    public void EligibilityUsesActualUpgradeTypeAndRejectsRecipesAndInvalidTypes()
    {
        using (var f = new Fixture())
        {
            Assert.That(EnhancementFairyCatalog.IsEligible(f.Items[0]), Is.True);
            Assert.That(EnhancementFairyCatalog.IsEligible(f.Recipe), Is.False);
            Set(f.Items[0], "_upgradeType", UpgradeType.Length);
            Assert.That(EnhancementFairyCatalog.IsEligible(f.Items[0]), Is.False);
            Set(f.Items[0], "_upgradeType", (UpgradeType)(-1));
            Assert.That(EnhancementFairyCatalog.IsEligible(f.Items[0]), Is.False);
            Assert.That(EnhancementFairyCatalog.IsEligible(null), Is.False);
        }
    }

    [Test]
    public void OwnershipRestoreIsSilentAndDuplicateMaterialsCreateOnlyOneFairy()
    {
        using (var f = new Fixture())
        {
            f.Habitat.RestoreOwnership(new[] { "ITEM0", "ITEM0", "ITEM1", "RECIPE" });
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(2));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(2));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
            f.Habitat.RestoreOwnership(new[] { "ITEM0", "ITEM1" });
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(2));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
        }
    }

    [Test]
    public void ConfirmedFirstAcquisitionPopsOnceAndRecipeNeverEntersTheHabitat()
    {
        using (var f = new Fixture())
        {
            Assert.That(f.Habitat.ConfirmAcquisition("transaction-a", f.Items[0]), Is.True);
            Assert.That(f.Habitat.ConfirmAcquisition("transaction-a", f.Items[0]), Is.False);
            Assert.That(f.Habitat.ConfirmAcquisition("transaction-b", f.Items[0]), Is.False);
            Assert.That(f.Habitat.ConfirmAcquisition("recipe", f.Recipe), Is.False);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(1));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(f.Arrivals.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void LockAndHiddenFloorDeferArrivalAndSuspendSimulation()
    {
        using (var f = new Fixture())
        {
            f.Habitat.SetFloorVisible(false, true);
            f.Habitat.ConfirmAcquisition("new", f.Items[0]);
            Assert.That(f.Habitat.ActiveCount, Is.Zero);
            Assert.That(f.Habitat.enabled, Is.False);
            Assert.That(f.Arrivals.PendingCount, Is.EqualTo(1));
            f.Habitat.SetFloorVisible(true, false);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
            f.Habitat.SetFloorVisible(true, true);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            var brain = f.Habitat.GetActiveBrain(0);
            f.Habitat.AdvancePreview(0.1f);
            float elapsed = brain.ElapsedSeconds;
            f.Habitat.SetFloorVisible(true, false);
            for (int i = 0; i < 100; i++) f.Habitat.AdvancePreview(0.1f);
            Assert.That(brain.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(f.Habitat.GetComponentsInChildren<SpriteRenderer>().Length, Is.Zero);
            f.Habitat.SetFloorVisible(true, true);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ActiveCapKeepsAllOwnershipAndReusesBoundedPoolWhileRotating()
    {
        using (var f = new Fixture())
        {
            f.Settings.MaxActive = 2;
            f.Settings.RotationSeconds = 1f;
            f.Habitat.RestoreOwnership(new[] { "ITEM0", "ITEM1", "ITEM2", "ITEM3", "ITEM4" });
            var seen = new HashSet<string>();
            for (int frame = 0; frame < 250; frame++)
            {
                f.Habitat.AdvancePreview(0.1f);
                Assert.That(f.Habitat.ActiveCount, Is.LessThanOrEqualTo(2));
                Assert.That(f.Habitat.PooledObjectCount, Is.LessThanOrEqualTo(2));
                for (int i = 0; i < f.Habitat.ActiveCount; i++) seen.Add(f.Habitat.GetActiveBrain(i).ItemId);
            }
            Assert.That(f.Habitat.OwnedTypeCount, Is.EqualTo(5));
            Assert.That(seen.Count, Is.EqualTo(5), "Every owned type must get a turn without allocating more views.");
        }
    }

    [Test]
    public void NewArrivalsHavePriorityAndOverflowArrivalsWaitForTheirOwnPop()
    {
        using (var f = new Fixture())
        {
            f.Settings.MaxActive = 1;
            f.Settings.NewArrivalPrioritySeconds = 1f;
            f.Habitat.RestoreOwnership(new[] { "ITEM0" });
            f.Habitat.ConfirmAcquisition("new1", f.Items[1]);
            Assert.That(f.Habitat.GetActiveBrain(0).ItemId, Is.EqualTo("ITEM1"));
            f.Habitat.ConfirmAcquisition("new2", f.Items[2]);
            Assert.That(f.Habitat.GetActiveBrain(0).ItemId, Is.EqualTo("ITEM1"));
            Assert.That(f.Arrivals.PendingCount, Is.EqualTo(1));
            for (int i = 0; i < 20; i++) f.Habitat.AdvancePreview(0.1f);
            Assert.That(f.Habitat.GetActiveBrain(0).ItemId, Is.EqualTo("ITEM2"));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(2));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void SceneReconstructionDoesNotReplayConsumedArrivalOrDuplicateObjects()
    {
        using (var f = new Fixture())
        {
            f.Habitat.ConfirmAcquisition("new", f.Items[0]);
            UnityEngine.Object.DestroyImmediate(f.Host);
            f.Host = new GameObject("Recreated floor");
            f.Habitat = f.Host.AddComponent<EnhancementFairyHabitat>();
            f.Habitat.ConfigureOffline(f.Items, new[] { "ITEM0" }, f.Settings, f.Arrivals);
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(1));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(1));
            Assert.That(f.Habitat.ConfirmAcquisition("retry", f.Items[0]), Is.False);
        }
    }

    [Test]
    public void UnseenArrivalSurvivesSceneReplacementUntilFloorBecomesVisible()
    {
        using (var f = new Fixture())
        {
            f.Habitat.SetFloorVisible(false, false);
            f.Habitat.ConfirmAcquisition("new", f.Items[0]);
            UnityEngine.Object.DestroyImmediate(f.Host);
            f.Host = new GameObject("Recreated floor");
            f.Habitat = f.Host.AddComponent<EnhancementFairyHabitat>();
            f.Habitat.ConfigureOffline(f.Items, new[] { "ITEM0" }, f.Settings, f.Arrivals);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(f.Arrivals.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void InactiveHierarchyDefersPopAndOfflineAccountResetClearsDeduplication()
    {
        using (var f = new Fixture())
        {
            f.Host.SetActive(false);
            f.Habitat.ConfirmAcquisition("hidden", f.Items[0]);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.Zero);
            Assert.That(f.Arrivals.PendingCount, Is.EqualTo(1));
            f.Host.SetActive(true);
            // EditMode does not schedule ordinary MonoBehaviour lifecycle callbacks.
            // The explicit resume is also the public path used by the integrated offline host.
            f.Habitat.SetFloorVisible(true, true);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            f.Habitat.ResetOfflineSession(Array.Empty<string>());
            Assert.That(f.Habitat.ActiveCount, Is.Zero);
            Assert.That(f.Habitat.ConfirmAcquisition("fresh-account", f.Items[0]), Is.True);
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(f.Habitat.PooledObjectCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OfflineSessionResetStopsOldPuffsAndOnlyNewItemPlaysAnArrival()
    {
        using (var f = new Fixture())
        {
            for (int i = 0; i < 4; i++) f.Habitat.ConfirmAcquisition("old" + i, f.Items[i]);
            Assert.That(CountEnabledPuffs(f.Host), Is.EqualTo(4));
            f.Habitat.ResetOfflineSession(new[] { "ITEM0", "ITEM1", "ITEM2", "ITEM3" });
            Assert.That(CountEnabledPuffs(f.Host), Is.Zero);
            f.Habitat.ConfirmAcquisition("new-account", f.Items[4]);
            Assert.That(CountEnabledPuffs(f.Host), Is.EqualTo(1));
            Assert.That(f.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(f.Habitat.ActiveCount, Is.EqualTo(5));
        }
    }

    private static int CountEnabledPuffs(GameObject host)
    {
        int count = 0;
        foreach (var renderer in host.GetComponentsInChildren<SpriteRenderer>())
            if (renderer.name == "Arrival puff" && renderer.enabled) count++;
        return count;
    }

    [Test]
    public void ActionWeightsAreConfigurableAndZeroWeightsSafelyRest()
    {
        using (var f = new Fixture())
        {
            f.Settings.HopProbability = f.Settings.VisitProbability = f.Settings.WalkProbability = 0f;
            f.Settings.LookProbability = f.Settings.TurnProbability = f.Settings.RestProbability = 0f;
            f.Settings.InitialWaitSeconds = new Vector2(0.1f, 0.1f);
            var brain = new EnhancementFairyBrain("ITEM0", f.Settings);
            for (int i = 0; i < 100; i++)
            {
                brain.Step(0.1f, Vector2.one, true);
                Assert.That(brain.Action, Is.EqualTo(EnhancementFairyAction.Rest));
            }
        }
    }

    [Test]
    public void ConfiguredHopDurationAndHeightControlTheActualVisualOffset()
    {
        using (var f = new Fixture())
        {
            f.Settings.HopProbability = 1f;
            f.Settings.VisitProbability = f.Settings.WalkProbability = f.Settings.LookProbability = 0f;
            f.Settings.TurnProbability = f.Settings.RestProbability = 0f;
            f.Settings.InitialWaitSeconds = new Vector2(0.1f, 0.1f);
            f.Settings.HopSeconds = 2f;
            f.Settings.MaximumHops = 1;
            f.Settings.HopHeight = 1f;
            f.Settings.IdleLift = f.Settings.IdleBobAmplitude = 0f;
            var brain = new EnhancementFairyBrain("ITEM0", f.Settings);
            brain.Step(0.1f, Vector2.zero, false);
            for (int i = 0; i < 5; i++) brain.Step(0.1f, Vector2.zero, false);
            Assert.That(brain.Action, Is.EqualTo(EnhancementFairyAction.Hop));
            Assert.That(brain.VisualHeight, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.001f));
        }
    }

    [Test]
    public void ClosingViewStillBlocksArrivalAfterNavigationHasRemovedIt()
    {
        using (var f = new Fixture())
        {
            var viewHost = new GameObject("Closing UI");
            try
            {
                var view = viewHost.AddComponent<FairyClosingTestView>();
                Set(f.Habitat, "_sceneViews", new UIView[] { view });
                var check = typeof(EnhancementFairyHabitat).GetMethod("HasClosingViews", BindingFlags.Instance | BindingFlags.NonPublic);
                view.VisibleState = VisibleState.Disappearing;
                Assert.That((bool)check.Invoke(f.Habitat, null), Is.True);
                view.VisibleState = VisibleState.Disappeared;
                Assert.That((bool)check.Invoke(f.Habitat, null), Is.False);
                view.VisibleState = VisibleState.Disappearing;
                viewHost.SetActive(false);
                Assert.That((bool)check.Invoke(f.Habitat, null), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(viewHost); }
        }
    }

    [Test]
    public void WalkingDistanceUsesElapsedSecondsAcrossDifferentVisualUpdateRates()
    {
        using (var f = new Fixture())
        {
            f.Settings.InitialWaitSeconds = new Vector2(.1f, .1f);
            f.Settings.GroundArea = new Rect(-1000, -1000, 2000, 2000);
            f.Settings.WalkRadius = new Vector2(100, 100);
            f.Settings.WalkSeconds = new Vector2(10, 10);
            f.Settings.WalkProbability = 1;
            f.Settings.HopProbability = f.Settings.VisitProbability = f.Settings.LookProbability = 0;
            f.Settings.TurnProbability = f.Settings.RestProbability = 0;
            var fast = new EnhancementFairyBrain("elapsed-walk", f.Settings);
            var slow = new EnhancementFairyBrain("elapsed-walk", f.Settings);
            fast.Step(.1f, Vector2.zero, false); slow.Step(.1f, Vector2.zero, false);
            Vector2 start = fast.GroundPosition;
            for (int i = 0; i < 30; i++) fast.Step(.01f, Vector2.zero, false);
            for (int i = 0; i < 3; i++) slow.Step(.1f, Vector2.zero, false);
            Assert.That(fast.Action, Is.EqualTo(EnhancementFairyAction.Walk));
            Assert.That(slow.Action, Is.EqualTo(EnhancementFairyAction.Walk));
            Assert.That(Vector2.Distance(start, fast.GroundPosition), Is.GreaterThan(.15f));
            Assert.That(Vector2.Distance(fast.GroundPosition, slow.GroundPosition), Is.LessThan(.002f));
            Assert.That(fast.ElapsedSeconds, Is.EqualTo(slow.ElapsedSeconds).Within(.00001f));
        }
    }

    [Test]
    public void HopOffsetUsesElapsedSecondsAcrossDifferentVisualUpdateRates()
    {
        using (var f = new Fixture())
        {
            f.Settings.InitialWaitSeconds = new Vector2(.1f, .1f);
            f.Settings.HopProbability = 1;
            f.Settings.WalkProbability = f.Settings.VisitProbability = f.Settings.LookProbability = 0;
            f.Settings.TurnProbability = f.Settings.RestProbability = 0;
            f.Settings.HopSeconds = 2;
            f.Settings.MaximumHops = 1;
            var fast = new EnhancementFairyBrain("elapsed-hop", f.Settings);
            var slow = new EnhancementFairyBrain("elapsed-hop", f.Settings);
            fast.Step(.1f, Vector2.zero, false); slow.Step(.1f, Vector2.zero, false);
            for (int i = 0; i < 30; i++) fast.Step(.01f, Vector2.zero, false);
            for (int i = 0; i < 3; i++) slow.Step(.1f, Vector2.zero, false);
            Assert.That(fast.Action, Is.EqualTo(EnhancementFairyAction.Hop));
            Assert.That(fast.VisualHeight, Is.EqualTo(slow.VisualHeight).Within(.00001f));
            Assert.That(fast.Squash, Is.EqualTo(slow.Squash).Within(.00001f));
        }
    }

    [Test]
    public void InvalidElapsedTimeDoesNotCorruptHabitatOrMoveFairies()
    {
        using (var f = new Fixture())
        {
            f.Habitat.RestoreOwnership(new[] { "ITEM0" });
            f.Habitat.AdvancePreview(.02f);
            var brain = f.Habitat.GetActiveBrain(0);
            float elapsed = brain.ElapsedSeconds;
            Vector2 position = brain.GroundPosition;
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
                f.Habitat.AdvancePreview(invalid);
            Assert.That(brain.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(brain.GroundPosition, Is.EqualTo(position));
            f.Habitat.AdvancePreview(.02f);
            Assert.That(brain.ElapsedSeconds, Is.EqualTo(elapsed + .02f).Within(.00001f));
        }
    }

    public sealed class FairyClosingTestView : UIView
    {
        public override void Init() { }
        public override void Show() { }
        public override void Hide() { }
    }

    [Test]
    public void FairiesHaveNoInputPhysicsOrGameplayComponents()
    {
        using (var f = new Fixture())
        {
            f.Habitat.RestoreOwnership(new[] { "ITEM0", "ITEM1" });
            Assert.That(f.Host.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(f.Host.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(f.Host.GetComponentsInChildren<Graphic>(true), Is.Empty);
            Assert.That(f.Host.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(f.Host.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.EqualTo(1));
            foreach (var renderer in f.Host.GetComponentsInChildren<SpriteRenderer>(true))
                Assert.That(renderer.gameObject.layer, Is.EqualTo(2));
        }
    }

    [Test]
    public void BehaviourStaysInsideFloorAndDoesNotConsumeGachaRandomness()
    {
        using (var f = new Fixture())
        {
            var randomBefore = UnityEngine.Random.state;
            f.Habitat.RestoreOwnership(new[] { "ITEM0", "ITEM1", "ITEM2" });
            var observedActions = new HashSet<EnhancementFairyAction>();
            for (int frame = 0; frame < 2000; frame++)
            {
                f.Habitat.AdvancePreview(0.1f);
                for (int i = 0; i < f.Habitat.ActiveCount; i++)
                {
                    var brain = f.Habitat.GetActiveBrain(i);
                    var bounds = f.Settings.SafeGroundArea;
                    Assert.That(brain.GroundPosition.x, Is.InRange(bounds.xMin, bounds.xMax));
                    Assert.That(brain.GroundPosition.y, Is.InRange(bounds.yMin, bounds.yMax));
                    observedActions.Add(brain.Action);
                }
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(randomBefore));
            Assert.That(observedActions, Does.Contain(EnhancementFairyAction.Rest));
            Assert.That(observedActions, Does.Contain(EnhancementFairyAction.Walk));
            Assert.That(observedActions, Does.Contain(EnhancementFairyAction.Hop));
            Assert.That(observedActions, Does.Contain(EnhancementFairyAction.Visit));
        }
    }

    [Test]
    public void EachIdHasReproducibleIndependentBehaviour()
    {
        using (var f = new Fixture())
        {
            var first = new EnhancementFairyBrain("ITEM0", f.Settings);
            var duplicate = new EnhancementFairyBrain("ITEM0", f.Settings);
            var other = new EnhancementFairyBrain("ITEM1", f.Settings);
            Assert.That(first.GroundPosition, Is.Not.EqualTo(other.GroundPosition));
            for (int i = 0; i < 100; i++)
            {
                first.Step(0.1f, Vector2.zero, false);
                other.Step(0.1f, Vector2.zero, false);
                duplicate.Step(0.1f, Vector2.zero, false);
                Assert.That(first.GroundPosition, Is.EqualTo(duplicate.GroundPosition));
                Assert.That(first.Action, Is.EqualTo(duplicate.Action));
                Assert.That(first.VisualHeight, Is.EqualTo(duplicate.VisualHeight));
            }
        }
    }

    private static void Set(object instance, string name, object value)
    {
        for (Type type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(instance, value);
            return;
        }
        throw new MissingFieldException(name);
    }

    private sealed class Fixture : IDisposable
    {
        public GameObject Host;
        public EnhancementFairyHabitat Habitat;
        public readonly EnhancementFairySettings Settings;
        public readonly EnhancementFairyArrivalQueue Arrivals = new EnhancementFairyArrivalQueue();
        public readonly List<GachaItemData> Items = new List<GachaItemData>();
        public readonly GachaItemData Recipe;
        private readonly Texture2D _texture;
        private readonly Sprite _sprite;

        public Fixture()
        {
            Settings = ScriptableObject.CreateInstance<EnhancementFairySettings>();
            _texture = new Texture2D(4, 4);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
            for (int i = 0; i < 5; i++) Items.Add(Item("ITEM" + i, UpgradeType.UPGRADE01));
            Recipe = Item("RECIPE", UpgradeType.None);
            Items.Add(Recipe);
            Host = new GameObject("Detached fairy test host");
            Habitat = Host.AddComponent<EnhancementFairyHabitat>();
            Habitat.ConfigureOffline(Items, Array.Empty<string>(), Settings, Arrivals);
        }

        private GachaItemData Item(string id, UpgradeType type)
        {
            var item = ScriptableObject.CreateInstance<GachaItemData>();
            Set(item, "_id", id);
            Set(item, "_upgradeType", type);
            Set(item, "_sprite", _sprite);
            return item;
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Host);
            foreach (var item in Items) UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(Settings);
            UnityEngine.Object.DestroyImmediate(_sprite);
            UnityEngine.Object.DestroyImmediate(_texture);
        }
    }
}
#endif
