#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-integrated checks: no detached ConfigureOffline furniture registration or approach teleport.</summary>
public sealed class ThirdFloorFairyAiHotfixTests
{
    [Test]
    public void SavedStageBindingRetriesAfterFloorStartAndMouseFramesReachAllThreeFurniture()
    {
        bool attempted = false;
        using (var host = new EnhancementFairyStage1Host(loading =>
        {
            var unbound = loading.SceneComponents<ThirdFloorFairyPlayArea>().Single();
            Assert.That(loading.Habitat, Is.Null);
            Assert.That((bool)EnhancementFairyStage1Host.Invoke(unbound, "BindToInitializedHabitat"), Is.False);
            Assert.That(unbound.IsRegistered, Is.False);
            Assert.That(FurnitureTapListeners(loading.Controller, unbound), Is.Zero);
            attempted = true;
        }))
        {
            Assert.That(attempted, Is.True);
            var area = PrepareSavedStageFurnitureInput(host);
            try
            {
                Assert.That(EnhancementFairyStage1Host.Get(area, "_cameraController"), Is.SameAs(host.Controller));
                Assert.That(EnhancementFairyStage1Host.Get(area, "_worldCamera"), Is.SameAs(host.Camera));
                Assert.That(EnhancementFairyStage1Host.Get(area, "_subscribedCamera"), Is.SameAs(host.Controller));
                Assert.That((bool)EnhancementFairyStage1Host.Get(area, "_offline"), Is.False);
                Assert.That(FurnitureTapListeners(host.Controller, area), Is.EqualTo(1));
                var handlers = WorldTapListeners(host.Controller);
                Assert.That(Array.FindIndex(handlers, listener => ReferenceEquals(listener.Target, area)),
                    Is.GreaterThan(Array.FindIndex(handlers, listener => ReferenceEquals(listener.Target, host.Habitat))));
                foreach (var point in area.Points) AssertSavedStageMouseTapAnimatesOnlyFurniture(host, area, point);
            }
            finally { EnhancementFairyStage1Host.Invoke(area, "OnDisable"); }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SavedStageFurnitureUnsubscribesAndRestoresOnceAfterPresentationReturns(bool toggleApprovedArt)
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var area = PrepareSavedStageFurnitureInput(host);
            var art = area.GetComponentInParent<ThirdFloorApprovedArt>();
            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    foreach (var point in area.Points) AssertSavedStageMouseTapAnimatesOnlyFurniture(host, area, point);
                    if (toggleApprovedArt) art.UseApprovedArt = false; else area.enabled = false;
                    // SDK-free EditMode host invokes the ordinary lifecycle callback explicitly;
                    // this is not a normal Play/GameView result or ConfigureOffline registration.
                    EnhancementFairyStage1Host.Invoke(area, "OnDisable");
                    Assert.That(area.IsRegistered, Is.False);
                    Assert.That(FurnitureTapListeners(host.Controller, area), Is.Zero);
                    Assert.That(area.Points.Any(area.IsTouchAnimating), Is.False);
                    if (toggleApprovedArt) art.UseApprovedArt = true; else area.enabled = true;
                    EnhancementFairyStage1Host.Invoke(area, "OnEnable");
                    Assert.That((bool)EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat"), Is.True);
                    Assert.That((bool)EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat"), Is.True);
                    Assert.That(FurnitureTapListeners(host.Controller, area), Is.EqualTo(1));
                }
            }
            finally { EnhancementFairyStage1Host.Invoke(area, "OnDisable"); }
        }
    }

    [Test]
    public void SavedStageMouseKeepsDirectFairyCardPriorityAndExposedFurnitureStillResponds()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var area = PrepareSavedStageFurnitureInput(host);
            try
            {
                Vector2 pixel = FindSavedStageFurnitureMeshOverlap(host, area);
                string evidence = SavedStageFairyHitEvidence(host, pixel, out int rawHits, out _, out int meshHits);
                Assert.That(rawHits, Is.GreaterThan(0), evidence);
                Assert.That(meshHits, Is.GreaterThan(0), evidence);
                using (var input = new SavedStagePointerContext(host))
                {
                    EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", pixel, true, true, false);
                    EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", pixel, false, false, true);
                    Assert.That(host.Habitat.IsItemCardPending, Is.True, host.Controller.EditorLastWorldTapDiagnostic + "; " + evidence);
                    host.Advance(host.Habitat.Settings.TouchDuration + 1f / 30f);
                    Assert.That(host.Habitat.IsItemCardOpen, Is.True, host.Controller.EditorLastWorldTapDiagnostic + "; " + evidence);
                    Assert.That(area.Points.Any(area.IsTouchAnimating), Is.False);
                    host.Habitat.CloseItemCard();
                    Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
                }
                foreach (var point in area.Points) AssertSavedStageMouseTapAnimatesOnlyFurniture(host, area, point);
            }
            finally { EnhancementFairyStage1Host.Invoke(area, "OnDisable"); }
        }
    }

    private static ThirdFloorFairyPlayArea PrepareSavedStageFurnitureInput(EnhancementFairyStage1Host host)
    {
        FairyAiHotfixProbeReflection.UseProductionAspect(host);
        var roster = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(12).ToArray();
        Assert.That(roster.Length, Is.EqualTo(12));
        host.Restore(roster.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
        host.Move(ERestaurantFloorType.Floor3); host.Move(RestaurantType.Kitchen); host.Advance(.6f);
        var area = host.SceneComponents<ThirdFloorFairyPlayArea>().Single();
        Assert.That((bool)EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat"), Is.True);
        Assert.That(area.IsRuntimeBound, Is.True);
        return area;
    }

    private static Vector2 FindSavedStageFurnitureMeshOverlap(EnhancementFairyStage1Host host, ThirdFloorFairyPlayArea area)
    {
        foreach (var body in host.Bodies().Where(value => value.enabled && value.color.a >= .5f))
        {
            var vertices = body.sprite.vertices; var triangles = body.sprite.triangles;
            for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                Vector2 pixel = (ProjectSpriteVertex(host.Camera, body, vertices[triangles[triangle]])
                    + ProjectSpriteVertex(host.Camera, body, vertices[triangles[triangle + 1]])
                    + ProjectSpriteVertex(host.Camera, body, vertices[triangles[triangle + 2]])) / 3f;
                if ((bool)EnhancementFairyStage1Host.Invoke(area, "IsFurnitureTapTarget", pixel)) return pixel;
            }
        }
        Assert.Fail("No authored fairy mesh/furniture collider overlap; actors must not be teleported to manufacture it.");
        return default;
    }

    private static Delegate[] WorldTapListeners(CameraController controller)
        => ((Delegate)EnhancementFairyStage1Host.Get(controller, "OnWorldTapHandler"))?.GetInvocationList() ?? Array.Empty<Delegate>();
    private static int FurnitureTapListeners(CameraController controller, ThirdFloorFairyPlayArea area)
        => WorldTapListeners(controller).Count(listener => ReferenceEquals(listener.Target, area));

    private static void AssertSavedStageMouseTapAnimatesOnlyFurniture(EnhancementFairyStage1Host host, ThirdFloorFairyPlayArea area, FairyFurniturePoint point)
    {
        Physics2D.SyncTransforms();
        Vector2 pixel = host.Camera.WorldToScreenPoint(point.TouchCollider.bounds.center);
        string evidence = SavedStageFairyHitEvidence(host, pixel, out int rawHits, out int paddedHits, out int meshHits);
        if (point.Kind == FairyFurnitureKind.RockingHorse)
        {
            Assert.That(rawHits, Is.GreaterThan(0), evidence);
            Assert.That(paddedHits, Is.GreaterThan(0), evidence);
            Assert.That(meshHits, Is.Zero, "The original exposed horse center remains outside the fairy's tight mesh. " + evidence);
        }
        else if (meshHits > 0)
        {
            bool found = false;
            var vertices = point.HintSprite.sprite.vertices; var triangles = point.HintSprite.sprite.triangles;
            for (int i = 0; i + 2 < triangles.Length && !found; i += 3)
            {
                pixel = (ProjectSpriteVertex(host.Camera, point.HintSprite, vertices[triangles[i]])
                    + ProjectSpriteVertex(host.Camera, point.HintSprite, vertices[triangles[i + 1]])
                    + ProjectSpriteVertex(host.Camera, point.HintSprite, vertices[triangles[i + 2]])) / 3f;
                Vector3 world = host.Camera.ScreenToWorldPoint(new Vector3(pixel.x, pixel.y, -host.Camera.transform.position.z));
                found = point.TouchCollider.OverlapPoint(world) && !host.Bodies().Any(body => body.enabled && body.color.a >= .5f && ProjectedSpriteMeshContains(host.Camera, body, pixel));
            }
            Assert.That(found, Is.True, "An exposed authored furniture pixel must exist without moving an actor: " + point.Kind);
        }
        var ownership = UserInfo.GetGiveGachaItemCountDic().ToArray();
        var identities = host.Identity(); var positions = host.Positions();
        Vector3 restPosition = point.HintVisual.localPosition, restScale = point.HintVisual.localScale;
        Quaternion restRotation = point.HintVisual.localRotation;
        using (var input = new SavedStagePointerContext(host))
        {
            var pointerOverUi = typeof(CameraController).GetMethod("IsPointerOverUI", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Vector2) }, null);
            Assert.That((bool)pointerOverUi.Invoke(host.Controller, new object[] { pixel }), Is.False, point.Kind.ToString());
            EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", pixel, true, true, false);
            for (int held = 0; held < 4; held++) EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", pixel, false, true, false);
            Assert.That(area.IsTouchAnimating(point), Is.False, "Pointer down and holding cannot start multiple feedback cycles.");
            EnhancementFairyStage1Host.Invoke(host.Controller, "ProcessMouseInputFrame", pixel, false, false, true);
            Assert.That(area.IsTouchAnimating(point), Is.True, host.Controller.EditorLastWorldTapDiagnostic + "; " + area.EditorLastFurnitureTapDiagnostic);
            Assert.That(host.Habitat.IsItemCardOpen, Is.False);
            bool moved = false;
            for (int frame = 0; frame < 120; frame++)
            {
                EnhancementFairyStage1Host.Invoke(area, "AdvanceHints", 1f / 60f, host.Habitat.HouseActivitySeconds + frame / 60f);
                moved |= Vector3.Distance(restPosition, point.HintVisual.localPosition) > .001f
                    || Vector3.Distance(restScale, point.HintVisual.localScale) > .001f
                    || Quaternion.Angle(restRotation, point.HintVisual.localRotation) > .01f;
            }
            Assert.That(moved, Is.True, point.Kind.ToString());
            Assert.That(area.IsTouchAnimating(point), Is.False);
            Assert.That(point.HintVisual.localPosition, Is.EqualTo(restPosition));
            Assert.That(point.HintVisual.localScale, Is.EqualTo(restScale));
            Assert.That(point.HintVisual.localRotation, Is.EqualTo(restRotation));
            CollectionAssert.AreEqual(positions, host.Positions(), "Pure feedback cannot issue a fairy movement command.");
            CollectionAssert.AreEquivalent(ownership, UserInfo.GetGiveGachaItemCountDic());
            CollectionAssert.AreEquivalent(identities, host.Identity());
        }
    }

    private static string SavedStageFairyHitEvidence(EnhancementFairyStage1Host host, Vector2 pixel,
        out int rawHits, out int paddedHits, out int meshHits)
    {
        rawHits = paddedHits = meshHits = 0;
        float minimumSize = Mathf.Clamp(Screen.dpi * .7f / 2.54f, 44f, 96f);
        var hits = new List<KeyValuePair<float, string>>();
        foreach (object view in (IList)EnhancementFairyStage1Host.Get(host.Habitat, "_views"))
        {
            var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
            var root = (Transform)EnhancementFairyStage1Host.Get(view, "Root");
            var body = (SpriteRenderer)EnhancementFairyStage1Host.Get(view, "Body");
            if (root == null || body == null || brain == null || !body.enabled || body.sprite == null
                || body.color.a < .5f || !root.gameObject.activeInHierarchy) continue;
            Vector3 lower = host.Camera.WorldToScreenPoint(body.bounds.min);
            Vector3 upper = host.Camera.WorldToScreenPoint(body.bounds.max);
            if (lower.z <= 0f) continue;
            Vector2 center = (lower + upper) * .5f;
            var actual = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            Vector2 paddedSize = new Vector2(Mathf.Max(minimumSize, actual.width), Mathf.Max(minimumSize, actual.height));
            var padded = new Rect(center - paddedSize * .5f, paddedSize);
            if (!padded.Contains(pixel)) continue;
            paddedHits++;
            if (actual.Contains(pixel)) rawHits++;
            bool meshContains = ProjectedSpriteMeshContains(host.Camera, body, pixel);
            if (meshContains) meshHits++;
            hits.Add(new KeyValuePair<float, string>((pixel - center).sqrMagnitude,
                brain.ItemId + ":bodyBoundsContains=" + actual.Contains(pixel)
                + ",paddingOnly=" + !actual.Contains(pixel)
                + ",meshContains=" + meshContains
                + ",bodyRect=" + actual.ToString("F3") + ",paddedRect=" + padded.ToString("F3")
                + ",worldBounds=" + body.bounds));
        }
        return "pixel=" + pixel.ToString("F3") + "; minimumCardTouchSize=" + minimumSize.ToString("F3")
            + "; nearestLegacyCardHits=" + string.Join(" | ", hits.OrderBy(hit => hit.Key).Select(hit => hit.Value));
    }

    private static Vector2 ProjectSpriteVertex(Camera camera, SpriteRenderer body, Vector2 vertex)
    {
        if (body.flipX) vertex.x = -vertex.x;
        if (body.flipY) vertex.y = -vertex.y;
        return camera.WorldToScreenPoint(body.transform.TransformPoint(vertex));
    }

    private static bool ProjectedSpriteMeshContains(Camera camera, SpriteRenderer body, Vector2 pixel)
    {
        var vertices = body.sprite.vertices;
        var triangles = body.sprite.triangles;
        for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
        {
            Vector2 a = ProjectSpriteVertex(camera, body, vertices[triangles[triangle]]);
            Vector2 ab = ProjectSpriteVertex(camera, body, vertices[triangles[triangle + 1]]) - a;
            Vector2 ac = ProjectSpriteVertex(camera, body, vertices[triangles[triangle + 2]]) - a;
            Vector2 ap = pixel - a;
            // Independent screen-space barycentric measurement; production uses a local-space
            // ray and triangle edge signs. This also observes the actual renderer flip/transform.
            double aa = Vector2.Dot(ab, ab), bb = Vector2.Dot(ab, ac), cc = Vector2.Dot(ac, ac);
            double d = Vector2.Dot(ap, ab), e = Vector2.Dot(ap, ac);
            double denominator = aa * cc - bb * bb;
            if (Math.Abs(denominator) < .000001) continue;
            double u = (cc * d - bb * e) / denominator;
            double v = (aa * e - bb * d) / denominator;
            if (u >= -.00001 && v >= -.00001 && u + v <= 1.00001) return true;
        }
        return false;
    }

    private sealed class SavedStagePointerContext : IDisposable
    {
        private readonly EventSystem _previous, _native;
        private readonly bool _registeredSystem;
        private readonly List<BaseRaycaster> _registeredRaycasters = new List<BaseRaycaster>();
        private readonly List<GraphicRaycaster> _cameraCache;
        private readonly GraphicRaycaster[] _previousCache;

        internal SavedStagePointerContext(EnhancementFairyStage1Host host)
        {
            _previous = EventSystem.current;
            _native = host.SceneComponents<EventSystem>().Single();
            var systems = (IList)EnhancementFairyStage1Host.Field(typeof(EventSystem), "m_EventSystems", true).GetValue(null);
            _registeredSystem = !systems.Contains(_native);
            _cameraCache = (List<GraphicRaycaster>)EnhancementFairyStage1Host.Get(host.Controller, "_graphicRaycasters");
            _previousCache = _cameraCache.ToArray();
            try
            {
                if (_registeredSystem) EnhancementFairyStage1Host.Invoke(_native, "OnEnable");
                EventSystem.current = _native;
                foreach (var raycaster in host.SceneComponents<BaseRaycaster>().Where(value => value.isActiveAndEnabled))
                {
                    if (RaycasterManager.GetRaycasters().Contains(raycaster)) continue;
                    EnhancementFairyStage1Host.Invoke(raycaster, "OnEnable");
                    _registeredRaycasters.Add(raycaster);
                }
                // Camera.Start's global FindObjectsByType excludes PreviewScenes. Host only its
                // authored active Stage1 raycaster cache, without borrowing or changing user-scene UI.
                // This is an Edit Mode callback fixture, not proof of the actual PlayerLoop startup.
                _cameraCache.Clear();
                _cameraCache.AddRange(host.SceneComponents<GraphicRaycaster>().Where(value => value.isActiveAndEnabled));
                Assert.That(EventSystem.current, Is.SameAs(_native));
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            _cameraCache.Clear();
            _cameraCache.AddRange(_previousCache);
            foreach (var raycaster in _registeredRaycasters) EnhancementFairyStage1Host.Invoke(raycaster, "OnDisable");
            _registeredRaycasters.Clear();
            if (_registeredSystem) EnhancementFairyStage1Host.Invoke(_native, "OnDisable");
            // Null is not an accepted EventSystem.current assignment. Removing our only registration
            // restores the empty-list case; an earlier registered system is restored explicitly.
            if (_previous != null) EventSystem.current = _previous;
            Assert.That(EventSystem.current, Is.SameAs(_previous));
        }
    }

    [Test]
    public void SavedStageRegistersFurnitureThroughTheSameProductionBindingCore()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var area = host.SceneComponents<ThirdFloorFairyPlayArea>().Single();
            Assert.That((bool)EnhancementFairyStage1Host.Get(host.Habitat, "_runtime"), Is.True);
            Assert.That((bool)EnhancementFairyStage1Host.Get(area, "_offline"), Is.False);
            Assert.That((bool)EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat"), Is.True);
            Assert.That(area.IsRegistered, Is.True);
            Assert.That(area.IsRuntimeBound, Is.True);
            Assert.That(EnhancementFairyStage1Host.Get(area, "_habitat"), Is.SameAs(host.Habitat));
            Assert.That(EnhancementFairyStage1Host.Get(host.Habitat, "_playArea"), Is.SameAs(area));
            Assert.That(area.Points.All(point => point.HasValidReferences), Is.True);
            Assert.That(host.Habitat.BaseActiveLimit, Is.EqualTo(12));
            Assert.That(area.Points.All(point => point.TouchEnabled && point.HintVisual != null && point.TouchCollider != null), Is.True);
            Assert.That(area.transform.Find("ReusableCushionZzz"), Is.Null);
            var house = host.SceneComponents<ThirdFloorFairyHouse>().Single();
            Assert.That(house.Settings.TemporaryExtraLimit, Is.EqualTo(12));
            Assert.That(house.Settings.TemporaryPlaySeconds, Is.EqualTo(45f));
            Assert.That(house.Settings.MinimumIndividualStaySeconds, Is.EqualTo(25f));
            Assert.That(house.Settings.ReturnStaggerMinSeconds, Is.EqualTo(6f));
            Assert.That(house.Settings.ReturnStaggerMaxSeconds, Is.EqualTo(10f));
            Assert.That(house.Settings.MaximumConcurrentReturns, Is.EqualTo(1));
            Assert.That(house.Settings.HouseResidentLimit, Is.EqualTo(2));
            Assert.That(house.Settings.SpawnInterval, Is.EqualTo(.18f));
            Assert.That(house.Settings.EntranceDuration, Is.EqualTo(.45f));
            Assert.That(house.Settings.TapDispersalSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(house.Settings.SafeTapSpawnFirstDelay, Is.Zero);
        }
    }

    [TestCase(12)]
    [TestCase(18)]
    [TestCase(24)]
    public void SurplusStationaryResidentsWalkOutWithStableTargetsInsteadOfTeleporting(int count)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(count == 12 ? 12 : 40))
        {
            fixture.RequestToCount(count);
            fixture.UntilHouseQueueDrained(count, 180f);
            Assert.That(fixture.Habitat.ActiveCount, Is.EqualTo(count));
            Assert.That(fixture.Brains().All(brain => fixture.HousePhase(brain.ItemId) == "None"), Is.True,
                "The initial arrangement must happen after every emergence has completed");
            // Single initial arrangement reproduces a crowded doorway. No positions are
            // assigned after this setup; all following motion is the production Brain.
            var ids = fixture.Brains().Select(brain => brain.ItemId).ToArray();
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "CancelAllSpacing");
            for (int index = 0; index < ids.Length; index++)
                fixture.Brain(ids[index]).PlaceForHouse(fixture.Entrance + Vector2.right * (index - count / 2f) * .01f);
            var previous = ids.ToDictionary(id => id, id => fixture.Brain(id).GroundPosition);
            var previousBrains = ids.ToDictionary(id => id, id => fixture.Brain(id));
            var previousHouse = ids.ToDictionary(id => id, id => fixture.HousePhase(id));
            var targets = new Dictionary<string, Vector2>();
            var routeDeadlines = new Dictionary<string, float>();
            var physicallyCleared = new HashSet<string>();
            var firstRoutes = new HashSet<string>();
            var initialSides = ids.ToDictionary(id => id, id => Mathf.Sign(fixture.Brain(id).GroundPosition.x - fixture.Entrance.x));
            var initialOffsets = ids.ToDictionary(id => id, id => fixture.Brain(id).GroundPosition.x - fixture.Entrance.x);
            // An artificial pile needs one sprite-sized lane per follower. Measure a
            // conservative physical evacuation budget rather than demanding a teleport.
            float evacuationSeconds = fixture.House.Settings.SafeHouseResidentGraceSeconds + 5f
                + (fixture.House.Settings.SafeHouseCrowdingRadius * 2f
                    + ids.Sum(id => fixture.Body(id).bounds.size.x)
                    + ids.Length * fixture.House.Settings.SafeSpriteSpacingPadding) / fixture.CalmSpeed;
            bool moved = false;
            for (int frame = 0; frame < Mathf.CeilToInt(evacuationSeconds * 30f); frame++)
            {
                fixture.Step();
                int unassignedNear = 0;
                var activeIds = new HashSet<string>(fixture.Brains().Select(brain => brain.ItemId));
                var currentViews = fixture.Views.Cast<object>().ToDictionary(view =>
                    ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ItemId);
                foreach (string id in ids)
                {
                    // A genuine automatic house return may finish during this long
                    // stress; it is a valid visible exit, not a missing-ID failure.
                    if (!activeIds.Contains(id)) { physicallyCleared.Add(id); targets.Remove(id); continue; }
                    var brain = fixture.Brain(id);
                    if (!ReferenceEquals(previousBrains[id], brain))
                    {
                        // A later roster rotation is a new visible lifetime of the ID.
                        physicallyCleared.Add(id);
                        targets.Remove(id);
                        previousBrains[id] = brain;
                        previous[id] = brain.GroundPosition;
                    }
                    string housePhase = fixture.HousePhase(id);
                    float distance = Vector2.Distance(previous[id], brain.GroundPosition);
                    // Existing house return/fade owns its own doorway arrival pose and
                    // takes priority over spacing; its original timing has separate tests.
                    if (previousHouse[id] == "None" && housePhase == "None")
                        Assert.That(distance, Is.LessThanOrEqualTo(fixture.MaximumNormalSpeed / 30f + .001f),
                            "Crowding must not reposition a normally walking fairy: " + id + "; frame=" + frame);
                    if (fixture.Habitat.IsItemDispersing(id))
                    {
                        Vector2 target = fixture.Habitat.GetDispersalTarget(id);
                        float deadline = (float)EnhancementFairyStage1Host.Get(currentViews[id], "DispersalDeadline");
                        if (targets.TryGetValue(id, out var old))
                        {
                            bool previousRouteFinished = fixture.Clock >= routeDeadlines[id]
                                || Vector2.Distance(previous[id], old) <= fixture.Habitat.Settings.ArrivalDistance + fixture.MaximumNormalSpeed / 30f + .001f
                                || previousHouse[id] != "None";
                            if (!previousRouteFinished)
                                Assert.That(Vector2.Distance(old, target), Is.LessThan(.001f),
                                    "An in-progress route must keep its target: " + id + "; frame=" + frame
                                    + "; priorDeadline=" + routeDeadlines[id] + "; clock=" + fixture.Clock);
                            targets[id] = target;
                        }
                        else
                        {
                            targets.Add(id, target);
                            if (firstRoutes.Add(id) && Mathf.Abs(initialOffsets[id]) > .001f)
                                Assert.That((target.x - fixture.Entrance.x) * initialSides[id], Is.GreaterThan(0f), "The first route must leave on the fairy's current side");
                        }
                        routeDeadlines[id] = deadline;
                        Assert.That(Mathf.Abs(target.x - fixture.Entrance.x), Is.GreaterThan(fixture.House.Settings.SafeHouseCrowdingRadius));
                        Assert.That(brain.IsDirectedWalking, Is.True);
                        moved |= distance > .001f;
                    }
                    else targets.Remove(id);
                    var body = fixture.Body(id).bounds;
                    bool near = body.min.x < fixture.House.Entrance.position.x + fixture.House.Settings.SafeHouseCrowdingRadius
                        && body.max.x > fixture.House.Entrance.position.x - fixture.House.Settings.SafeHouseCrowdingRadius;
                    if (!near) physicallyCleared.Add(id);
                    if (near && !fixture.Habitat.IsItemDispersing(id) && housePhase == "None"
                        && fixture.Habitat.GetHouseZoneSeconds(id) > fixture.House.Settings.SafeHouseResidentGraceSeconds + 2f / 30f)
                        unassignedNear++;
                    previous[id] = brain.GroundPosition;
                    previousHouse[id] = housePhase;
                    Assert.That(brain.GroundPosition.x, Is.InRange(fixture.Habitat.Settings.SafeGroundArea.xMin, fixture.Habitat.Settings.SafeGroundArea.xMax));
                }
                if (frame >= 90)
                {
                    if (unassignedNear > 2)
                    {
                        string diagnostic = string.Join("; ", fixture.Brains().Where(brain => !fixture.Habitat.IsItemDispersing(brain.ItemId)
                            && fixture.HousePhase(brain.ItemId) == "None").Select(brain => brain.ItemId + ":x=" + brain.GroundPosition.x
                                + ",zone=" + fixture.Habitat.GetHouseZoneSeconds(brain.ItemId) + ",action=" + brain.Action
                                + ",target=" + brain.MovementTarget.x));
                        Assert.Fail("Excess fairies need a bounded outward route even while yielding. frame=" + frame
                            + "; clock=" + fixture.Clock + "; count=" + unassignedNear + "; " + diagnostic);
                    }
                }
                if (ids.Length - physicallyCleared.Count <= 2) break;
            }
            Assert.That(moved, Is.True);
            Assert.That(ids.Length - physicallyCleared.Count, Is.LessThanOrEqualTo(2),
                "The original pile must physically leave the house zone within its width/speed budget; later genuine re-entry has its own grace");
            fixture.AssertOwnedAndBounded(count);
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void HouseEmergenceWalksToAFixedClearDestinationAndReleasesItsSpecialState(int requested)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var before = new HashSet<string>(fixture.Brains().Select(brain => brain.ItemId));
            Assert.That(fixture.Habitat.RequestHouseEmergence(fixture.House, requested), Is.EqualTo(requested));
            var started = new Dictionary<string, Vector2>();
            var completed = new HashSet<string>();
            var previous = new Dictionary<string, Vector2>();
            for (int frame = 0; frame < 3600 && completed.Count < requested; frame++)
            {
                fixture.Step();
                foreach (var brain in fixture.Brains().Where(value => !before.Contains(value.ItemId)))
                {
                    if (completed.Contains(brain.ItemId)) continue;
                    if (fixture.Habitat.IsItemDispersing(brain.ItemId))
                    {
                        var target = fixture.Habitat.GetDispersalTarget(brain.ItemId);
                        if (started.TryGetValue(brain.ItemId, out var first))
                        {
                            Assert.That(Vector2.Distance(first, target), Is.LessThan(.001f));
                            Assert.That(fixture.IsInitialDispersal(brain.ItemId), Is.True);
                            Assert.That(Vector2.Distance(previous[brain.ItemId], brain.GroundPosition),
                                Is.LessThanOrEqualTo(fixture.CalmSpeed * fixture.House.Settings.SafeTapDispersalSpeedMultiplier / 30f + .001f));
                        }
                        else started.Add(brain.ItemId, target);
                        previous[brain.ItemId] = brain.GroundPosition;
                    }
                    else if (started.ContainsKey(brain.ItemId) && fixture.HousePhase(brain.ItemId) == "None")
                    {
                        Assert.That(Vector2.Distance(brain.GroundPosition, started[brain.ItemId]), Is.LessThan(fixture.Habitat.Settings.ArrivalDistance + .05f));
                        Assert.That(brain.IsDirectedWalking, Is.False);
                        Assert.That(fixture.IsInitialDispersal(brain.ItemId), Is.False);
                        completed.Add(brain.ItemId);
                    }
                }
            }
            Assert.That(started.Count, Is.EqualTo(requested));
            Assert.That(completed.Count, Is.EqualTo(requested));
            fixture.AssertOwnedAndBounded(12 + requested);
        }
    }

    [Test]
    public void CameraAndFloorChangesCancelFurnitureFeedbackAndSpacingWithoutChangingOwnership()
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var point = fixture.Area.Points.Single(value => value.Kind == FairyFurnitureKind.Cushion);
            fixture.Habitat.RequestHouseEmergence(fixture.House, 3); fixture.Advance(2f);
            var owned = UserInfo.GetGiveGachaItemCountDic().ToArray(); var positions = fixture.Host.Positions();
            Assert.That(fixture.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            EnhancementFairyStage1Host.Invoke(fixture.Area, "AdvanceHints", .1f, fixture.Clock);
            fixture.Host.Move(RestaurantType.Hall);
            CollectionAssert.AreEqual(positions, fixture.Host.Positions(), "Camera movement cannot teleport fairies.");
            EnhancementFairyStage1Host.Invoke(fixture.Area, "AdvanceHints", .05f, fixture.Clock);
            Assert.That(fixture.Area.IsTouchAnimating(point), Is.False);
            fixture.Host.Move(ERestaurantFloorType.Floor1); fixture.Step();
            Assert.That(fixture.Habitat.DispersingCount, Is.Zero);
            Assert.That(fixture.Area.transform.Find("ReusableCushionZzz"), Is.Null);
            fixture.Host.Move(ERestaurantFloorType.Floor3); fixture.Host.Move(RestaurantType.Kitchen); fixture.Advance(.6f);
            Assert.That(fixture.Habitat.IsVisible, Is.True); Assert.That(fixture.Area.IsRuntimeBound, Is.True);
            Assert.That(fixture.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
            CollectionAssert.AreEquivalent(owned, UserInfo.GetGiveGachaItemCountDic());
        }
    }

    [Test]
    public void RuntimeResidentsRemainAutonomousWhileFurnitureTouchesNeverCreateActivityActors()
    {
        using (var fixture = new FairyAiRuntimeStageFixture(12))
        {
            var identity = fixture.Host.Identity();
            var elapsed = fixture.Brains().ToDictionary(brain => brain.ItemId, brain => brain.ElapsedSeconds);
            int pool = fixture.Habitat.PooledObjectCount;
            for (int frame = 0; frame < 900; frame++)
            {
                if (frame % 90 == 0)
                {
                    var positions = fixture.Host.Positions();
                    foreach (var point in fixture.Area.Points) Assert.That(fixture.Area.TryTapAtWorldPoint(point.TouchCollider.bounds.center), Is.True);
                    CollectionAssert.AreEqual(positions, fixture.Host.Positions());
                }
                fixture.Step();
                EnhancementFairyStage1Host.Invoke(fixture.Area, "AdvanceHints", 1f / 30f, fixture.Clock);
                Assert.That(fixture.Area.transform.Find("ReusableCushionZzz"), Is.Null);
                Assert.That(fixture.Habitat.PendingHouseSpawnCount, Is.Zero);
            }
            Assert.That(fixture.Brains().All(brain => brain.ElapsedSeconds > elapsed[brain.ItemId]), Is.True);
            Assert.That(fixture.Habitat.PooledObjectCount, Is.EqualTo(pool));
            CollectionAssert.AreEquivalent(identity, fixture.Host.Identity());
            fixture.AssertOwnedAndBounded(12);
        }
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(6, false)]
    [TestCase(12, false)]
    [TestCase(12, true)]
    public void ActualHouseTrafficDoesNotHideMovingSpriteCrowdsBehindTransitStates(int extra, bool includeWideSprites)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(extra == 0 ? 12 : includeWideSprites ? 24 : 40, includeWideSprites))
        {
            // Keep the normal furniture scheduler, house timing and walking speeds enabled.
            var observation = new FairyHouseCrowdMetrics(fixture);
            fixture.RequestHouseBurst(extra);
            for (int frame = 0; frame < 5400; frame++)
            {
                fixture.Step();
                observation.Observe(1f / 30f);
                Assert.That(fixture.Habitat.ActiveCount, Is.LessThanOrEqualTo(24));
                Assert.That(fixture.Habitat.PendingHouseSpawnCount, Is.LessThanOrEqualTo(12));
                if (observation.ExitDirectionReversals != 0 || observation.ExitOutwardViolations != 0
                    || observation.FirstDispersalDeadlineMisses != 0)
                    Assert.Fail(observation.Describe());
            }
            Assert.That(observation.PeakActive, Is.EqualTo(12 + extra), "Dropping the accepted visitors cannot make the crowd check pass. " + observation.Describe());
            Assert.That(fixture.Habitat.HouseEmergenceCount, Is.EqualTo(extra));
            Assert.That(observation.FirstDispersalCount, Is.EqualTo(extra));
            Assert.That(observation.InitialVisitorCount, Is.EqualTo(extra));
            Assert.That(observation.InitialVisitorsCleared, Is.EqualTo(extra),
                "Temporary doorway overlap is allowed, but every actual visitor must leave the house zone with its entire rendered body. " + observation.Describe());
            Assert.That(observation.MaximumInitialVisitorClearanceSeconds,
                Is.LessThanOrEqualTo(observation.InitialClearanceBudgetSeconds),
                "The burst cannot become an indefinitely yielding crowd; the budget is calculated from actual sprite widths, house radius and calm walking speed. " + observation.Describe());
            if (extra >= 6)
            {
                Assert.That(observation.LeftExits, Is.GreaterThan(0), observation.Describe());
                Assert.That(observation.RightExits, Is.GreaterThan(0), observation.Describe());
            }
            fixture.AssertOwnedAndBounded(24);
        }
    }

    [Test]
    public void ThreeAlmostCoincidentSameDirectionWalkersCannotFormACircularYieldQueue()
    {
        using (var fixture = new FairyAiRuntimeStageFixture(12))
        {
            var views = fixture.Views.Cast<object>().ToArray();
            var brains = views.Select(view => (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ToArray();
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "CancelAllSpacing");
            // One starting arrangement isolates the near-identical ordering regression.
            // All later movement/yielding uses the real habitat and Brain at normal speed.
            for (int index = 3; index < brains.Length; index++)
                brains[index].PlaceForHouse(new Vector2(-14f + (index - 3) * 1.5f, fixture.Entrance.y));
            float startX = fixture.Entrance.x + 1f;
            var target = new Vector2(fixture.Entrance.x + fixture.House.Settings.SafeHouseCrowdingRadius + 4f, fixture.Entrance.y);
            for (int index = 0; index < 3; index++)
            {
                brains[index].PlaceForHouse(new Vector2(startX + index * .0007f, fixture.Entrance.y));
                EnhancementFairyStage1Host.Invoke(fixture.Habitat, "BeginSpacingWalk", views[index], target, 1f);
            }
            var previous = brains.Take(3).Select(brain => brain.GroundPosition).ToArray();
            var cleared = new bool[3];
            float maxWidth = brains.Take(3).Max(brain => fixture.Body(brain.ItemId).bounds.size.x);
            float deadline = (fixture.House.Settings.SafeHouseCrowdingRadius + 3f * maxWidth + 5f) / fixture.CalmSpeed + 3f;
            for (int frame = 0; frame < Mathf.CeilToInt(deadline * 30f) && cleared.Any(value => !value); frame++)
            {
                fixture.Step();
                for (int index = 0; index < 3; index++)
                {
                    if (fixture.Habitat.IsItemDispersing(brains[index].ItemId))
                        Assert.That(Vector2.Distance(previous[index], brains[index].GroundPosition),
                            Is.LessThanOrEqualTo(fixture.CalmSpeed / 30f + .001f));
                    previous[index] = brains[index].GroundPosition;
                    cleared[index] |= fixture.Body(brains[index].ItemId).bounds.min.x
                        > fixture.House.Entrance.position.x + fixture.House.Settings.SafeHouseCrowdingRadius;
                }
                if (frame == 14)
                    Assert.That(brains[2].GroundPosition.x - (startX + .0014f),
                        Is.GreaterThanOrEqualTo(fixture.CalmSpeed * .5f - .005f), "The actual leading walker must advance, even when three positions differ by less than the former epsilon");
            }
            Assert.That(cleared.All(value => value), Is.True, "All three real rendered bodies must leave the house zone within their width/speed budget");
            fixture.AssertOwnedAndBounded(12);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void ATemporarilyOccupiedExitDoesNotDelayTheFirstVisibleVisitor(int framesPerSecond)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var views = fixture.Views.Cast<object>().ToArray();
            var brains = views.Select(view => (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ToArray();
            var before = new HashSet<string>(brains.Select(brain => brain.ItemId));
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "CancelAllSpacing");
            // A single initial layout: the short left room is occupied but its
            // landing is open; the spacious right room has one moving door blocker.
            for (int index = 0; index < brains.Length - 1; index++)
                brains[index].PlaceForHouse(new Vector2(-46f + index * .1f, fixture.Entrance.y));
            var blocker = brains[brains.Length - 1];
            var rightLanding = fixture.Entrance + Vector2.right * fixture.House.Settings.SafeEntranceSpread;
            blocker.PlaceForHouse(rightLanding);
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "BeginSpacingWalk", views[views.Length - 1],
                fixture.Entrance + Vector2.right * 15f, 1f);
            foreach (object view in views) EnhancementFairyStage1Host.Invoke(fixture.Habitat, "Render", view);
            var timing = new FairyFastEmergenceMetrics(fixture);
            var request = timing.Request(1, 1);
            float dt = 1f / framesPerSecond;
            for (int frame = 0; frame < framesPerSecond; frame++)
            {
                fixture.Step(dt);
                timing.Observe();
            }
            FairyFastEmergenceMetrics.AssertRequestTiming(request, dt);
            Assert.That(request.Accepted, Is.EqualTo(1));
            Assert.That(fixture.Habitat.HouseEmergenceCount, Is.EqualTo(1), fixture.Habitat.GetAiDiagnosticSnapshot());
            var visitor = fixture.Brains().Single(brain => !before.Contains(brain.ItemId));
            Assert.That(fixture.Habitat.GetHouseExitSide(visitor.ItemId), Is.EqualTo(1));
            Assert.That(fixture.Habitat.PendingHouseSpawnCount, Is.Zero);
            fixture.AssertOwnedAndBounded(13);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void SavedStageRequestsOneTwoThreeAndLaterVisitorsAtTheNaturalAuthoredCadence(int framesPerSecond)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(40))
        {
            var timing = new FairyFastEmergenceMetrics(fixture);
            float dt = 1f / framesPerSecond;
            for (int stage = 1; stage <= 4; stage++)
            {
                int requested = fixture.House.Settings.SpawnCountForStage(stage);
                var request = timing.Request(stage, requested);
                Assert.That(request.Accepted, Is.EqualTo(Mathf.Min(stage, 3)));
                for (int frame = 0; frame < framesPerSecond / 2; frame++)
                {
                    fixture.Step(dt);
                    timing.Observe();
                    fixture.AssertOwnedAndBounded(24);
                }
            }
            for (int frame = 0; frame < framesPerSecond; frame++) { fixture.Step(dt); timing.Observe(); }
            foreach (var request in timing.Requests) FairyFastEmergenceMetrics.AssertRequestTiming(request, dt);
            Assert.That(fixture.Habitat.ActiveCount, Is.EqualTo(21));
            Assert.That(fixture.Habitat.HouseEmergenceCount, Is.EqualTo(9));
            Assert.That(fixture.Habitat.PendingHouseSpawnCount, Is.Zero);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void NaturalRequestsRespectRealOwnershipAndCannotReserveFutureSlotsAtTheHardCap(int framesPerSecond)
    {
        float dt = 1f / framesPerSecond;
        using (var fixture = new FairyAiRuntimeStageFixture(14))
        {
            var timing = new FairyFastEmergenceMetrics(fixture);
            var request = timing.Request(3, 3);
            Assert.That(request.Accepted, Is.EqualTo(2));
            for (int frame = 0; frame < framesPerSecond; frame++) { fixture.Step(dt); timing.Observe(); }
            FairyFastEmergenceMetrics.AssertRequestTiming(request, dt);
            Assert.That(timing.Request(4, 3).Accepted, Is.Zero);
            fixture.AssertOwnedAndBounded(14);
        }
        using (var fixture = new FairyAiRuntimeStageFixture(40))
        {
            var timing = new FairyFastEmergenceMetrics(fixture);
            for (int stage = 1; stage <= 5; stage++) timing.Request(stage, Mathf.Min(stage, 3));
            for (int frame = 0; frame < framesPerSecond * 3; frame++)
            {
                fixture.Step(dt); timing.Observe();
                fixture.AssertOwnedAndBounded(24);
            }
            Assert.That(fixture.Habitat.ActiveCount, Is.EqualTo(24));
            Assert.That(fixture.Habitat.HouseEmergenceCount, Is.EqualTo(12));
            Assert.That(timing.Request(6, 3).Accepted, Is.Zero);
            // The existing calm return is allowed to start naturally. Its future
            // slot must not become a newly accepted emergence before it is free.
            for (int frame = 0; frame < framesPerSecond * 60 && fixture.Habitat.ReturningHouseCount == 0; frame++) fixture.Step(dt);
            Assert.That(fixture.Habitat.ReturningHouseCount, Is.EqualTo(1));
            Assert.That(fixture.Habitat.ActiveCount, Is.EqualTo(24));
            float due = fixture.Habitat.HouseReturnDueAt;
            Assert.That(fixture.Habitat.RequestHouseEmergence(fixture.House, 3), Is.Zero);
            Assert.That(fixture.Habitat.PendingHouseSpawnCount, Is.Zero);
            Assert.That(fixture.Habitat.HouseReturnDueAt, Is.EqualTo(due));
        }
    }

    [TestCase(30, false)]
    [TestCase(60, false)]
    [TestCase(30, true)]
    [TestCase(60, true)]
    public void NaturalVisitorsMakeFiniteFullBodyProgressWithinTheirActualWidthAndCalmSpeedBudget(int fps, bool wideBurst)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(wideBurst ? 24 : 40, wideBurst))
        {
            var observation = new FairyHouseCrowdMetrics(fixture);
            float dt = 1f / fps;
            for (int frame = 0; frame <= 120 * fps; frame++)
            {
                if (frame > 0) fixture.Step(dt);
                if (wideBurst && frame == 0) fixture.RequestHouseBurst(12);
                else if (!wideBurst && frame <= fps * 3 / 2 && frame % (fps / 2) == 0)
                {
                    int stage = frame / (fps / 2) + 1;
                    int count = fixture.House.Settings.SpawnCountForStage(stage);
                    Assert.That(fixture.Habitat.RequestHouseEmergence(fixture.House, count), Is.EqualTo(count));
                }
                observation.Observe(frame == 0 ? 0f : dt);
                Assert.That(observation.MaximumInitialVisitorClearanceSeconds,
                    Is.LessThanOrEqualTo(observation.InitialClearanceBudgetSeconds + dt),
                    "A slower authored exit still needs finite physical progress; overlap durations remain separately reported. " + observation.Describe());
                fixture.AssertOwnedAndBounded(24);
            }
            Assert.That(observation.InitialVisitorsCleared, Is.EqualTo(wideBurst ? 12 : 9), observation.Describe());
            Assert.That(observation.InitialVisitors.Values.Any(visitor => visitor.RemovedBeforeClearance), Is.False,
                "Returning or pooling a stationary visitor must not stand in for walking out");
            Assert.That(observation.PeakActive, Is.EqualTo(wideBurst ? 24 : 21));
            Assert.That(observation.ExitDirectionReversals, Is.Zero);
            Assert.That(observation.ExitOutwardViolations, Is.Zero);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void InitialDispersalWalksAtCalmSpeedThenHandsOffToTheOriginalOrdinaryWalking(int fps)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var timing = new FairyFastEmergenceMetrics(fixture);
            var request = timing.Request(1, 1);
            float dt = 1f / fps;
            for (int frame = 0; frame < fps; frame++) { fixture.Step(dt); timing.Observe(); }
            FairyFastEmergenceMetrics.AssertRequestTiming(request, dt);
            string id = request.Visitors.Single().Id;
            var brain = fixture.Brain(id);
            Assert.That(fixture.IsInitialDispersal(id), Is.True);
            bool sawCalmStep = false;
            Vector2 target = fixture.Habitat.GetDispersalTarget(id);
            float routeBudget = Vector2.Distance(brain.GroundPosition, target) / fixture.CalmSpeed + 30f;
            for (int frame = 0; frame < Mathf.CeilToInt(routeBudget * fps) && fixture.IsInitialDispersal(id); frame++)
            {
                Vector2 previous = brain.GroundPosition;
                fixture.Step(dt);
                float distance = Vector2.Distance(previous, brain.GroundPosition);
                sawCalmStep |= distance > .001f;
                Assert.That(distance, Is.LessThanOrEqualTo(fixture.CalmSpeed * dt + .001f));
            }
            Assert.That(sawCalmStep, Is.True, "The initial route must produce actual walking");
            Assert.That(fixture.IsInitialDispersal(id), Is.False);
            Assert.That(brain.IsDirectedWalking, Is.False);
            Assert.That(Vector2.Distance(brain.GroundPosition, target), Is.LessThanOrEqualTo(fixture.Habitat.Settings.ArrivalDistance + .001f),
                "A timeout or interruption must not masquerade as completing the initial route");
            // The same real brain now follows its ordinary weighted actions. Do not
            // alter speed settings, teleport it or manually clear the route fields.
            bool sawOrdinaryWalking = false;
            for (int frame = 0; frame < fps * 20 && !sawOrdinaryWalking; frame++)
            {
                bool ordinaryBefore = !fixture.Habitat.IsItemDispersing(id) && fixture.HousePhase(id) == "None";
                Vector2 previous = brain.GroundPosition;
                fixture.Step(dt);
                if (!ordinaryBefore || fixture.Habitat.IsItemDispersing(id) || fixture.HousePhase(id) != "None") continue;
                float distance = Vector2.Distance(previous, brain.GroundPosition);
                Assert.That(distance, Is.LessThanOrEqualTo(fixture.MaximumNormalSpeed * dt + .001f));
                sawOrdinaryWalking |= distance > .001f;
            }
            Assert.That(sawOrdinaryWalking, Is.True);
        }
    }

    [Test]
    public void RemovingAnInitiallyDispersingVisitorClearsItsRouteBeforeItsPooledViewIsReused()
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var originalOwnership = UserInfo.GetGiveGachaItemCountDic().ToArray();
            var timing = new FairyFastEmergenceMetrics(fixture);
            var request = timing.Request(1, 1);
            for (int frame = 0; frame < 30; frame++) { fixture.Step(); timing.Observe(); }
            string id = request.Visitors.Single().Id;
            object view = fixture.View(id);
            var oldBrain = fixture.Brain(id);
            Assert.That(fixture.IsInitialDispersal(id), Is.True);
            fixture.Host.Restore(originalOwnership.Where(pair => pair.Key != id));
            fixture.Step();
            Assert.That(fixture.Brains().Any(brain => brain.ItemId == id), Is.False);
            Assert.That((bool)EnhancementFairyStage1Host.Get(view, "InitialHouseDispersal"), Is.False);
            Assert.That((bool)EnhancementFairyStage1Host.Get(view, "Dispersing"), Is.False);
            Assert.That(oldBrain.IsDirectedWalking, Is.False);
            Assert.That(((IList)EnhancementFairyStage1Host.Get(fixture.Habitat, "_pool")).Contains(view), Is.True);
            fixture.Host.Restore(originalOwnership);
            Assert.That(fixture.Habitat.RequestHouseEmergence(fixture.House, 1), Is.EqualTo(1));
            Assert.That(fixture.View(id), Is.SameAs(view));
            Assert.That(fixture.Brain(id), Is.Not.SameAs(oldBrain));
            Assert.That(fixture.IsInitialDispersal(id), Is.False, "A reused view must begin with the doorway phase, not a leaked previous route");
            fixture.Host.Move(ERestaurantFloorType.Floor1);
            fixture.Step();
            Assert.That((bool)EnhancementFairyStage1Host.Get(view, "InitialHouseDispersal"), Is.False);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void InitialVisitorsKeepActualCalmProgressWhileTheirBodiesStillTouchTheHouse(int fps)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24, true))
        {
            var crowd = new FairyHouseCrowdMetrics(fixture);
            fixture.RequestHouseBurst(12);
            float dt = 1f / fps;
            float worldCenter = fixture.House.Entrance.position.x;
            float worldRadius = fixture.House.Settings.SafeHouseCrowdingRadius * Mathf.Abs(fixture.Host.Floor.transform.lossyScale.x);
            int checkedSteps = 0;
            for (int frame = 0; frame < fps * 30 && crowd.InitialVisitorsCleared < 12; frame++)
            {
                var walkingInside = fixture.Brains().Where(brain => fixture.IsInitialDispersal(brain.ItemId)
                    && fixture.HousePhase(brain.ItemId) == "None"
                    && fixture.Body(brain.ItemId).bounds.max.x > worldCenter - worldRadius
                    && fixture.Body(brain.ItemId).bounds.min.x < worldCenter + worldRadius)
                    .ToDictionary(brain => brain.ItemId, brain => brain.GroundPosition);
                fixture.Step(dt);
                crowd.Observe(dt);
                foreach (var pair in walkingInside)
                {
                    Assert.That(Vector2.Distance(pair.Value, fixture.Brain(pair.Key).GroundPosition),
                        Is.EqualTo(fixture.CalmSpeed * dt).Within(.001f),
                        "A first visitor must walk at the authored calm speed while touching the doorway, even when another initial visitor is ahead: " + pair.Key);
                    checkedSteps++;
                }
            }
            Assert.That(checkedSteps, Is.GreaterThan(fps));
            Assert.That(crowd.InitialVisitorsCleared, Is.EqualTo(12), crowd.Describe());
            // This deadline follows a single body's physical trip out of the radius,
            // plus its own width and one reveal. It does not grant a whole queue's wait.
            float widest = crowd.InitialVisitors.Values.Max(visitor => visitor.MaximumWidth);
            float singleBodyTrip = (fixture.House.Settings.SafeHouseCrowdingRadius + widest) / fixture.CalmSpeed + 2f * dt;
            Assert.That(crowd.MaximumInitialVisitorClearanceSeconds, Is.LessThanOrEqualTo(singleBodyTrip), crowd.Describe());
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitialVisitorsOutsideTheHouseKeepMovingIfAlreadyOverlappingButYieldForASeparatedPaddingGap(bool alreadyOverlapping)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(40))
        {
            fixture.RequestHouseBurst(12);
            for (int frame = 0; frame < 90; frame++) fixture.Step();
            float radius = fixture.House.Settings.SafeHouseCrowdingRadius;
            float half = fixture.Brains().Max(brain => fixture.Habitat.GetSpacingHalfWidth(brain.ItemId));
            var candidates = fixture.Brains().Where(brain => fixture.IsInitialDispersal(brain.ItemId)
                && Mathf.Abs(fixture.Habitat.GetDispersalTarget(brain.ItemId).x - fixture.Entrance.x)
                    > radius + 3f * half + fixture.House.Settings.SafeSpriteSpacingPadding + 2f)
                .GroupBy(brain => Mathf.Sign(fixture.Habitat.GetDispersalTarget(brain.ItemId).x - fixture.Entrance.x))
                .First(group => group.Count() >= 2).Take(2).ToArray();
            float direction = Mathf.Sign(fixture.Habitat.GetDispersalTarget(candidates[0].ItemId).x - fixture.Entrance.x);
            Vector2 behind = fixture.Entrance + Vector2.right * direction * (radius + half + .2f);
            // One outside starting arrangement; both flags/targets came from actual
            // accepted visitors. The real spacing and Brain decide the next frame.
            candidates[0].PlaceForHouse(behind);
            candidates[1].PlaceForHouse(behind);
            foreach (var brain in candidates)
            {
                brain.BeginDirectedWalk(fixture.Habitat.GetDispersalTarget(brain.ItemId));
                EnhancementFairyStage1Host.Invoke(fixture.Habitat, "Render", fixture.View(brain.ItemId));
            }
            var follower = fixture.Body(candidates[0].ItemId).bounds;
            var leader = fixture.Body(candidates[1].ItemId).bounds;
            float scale = Mathf.Abs(fixture.Host.Floor.transform.lossyScale.x);
            float touchingDistance = direction > 0f ? (follower.max.x - leader.min.x) / scale : (leader.max.x - follower.min.x) / scale;
            float safeGap = Mathf.Max(fixture.House.Settings.SafeMinimumFairySpacing,
                fixture.Habitat.GetSpacingHalfWidth(candidates[0].ItemId) + fixture.Habitat.GetSpacingHalfWidth(candidates[1].ItemId)
                    + fixture.House.Settings.SafeSpriteSpacingPadding);
            Assert.That(safeGap, Is.GreaterThan(touchingDistance + .02f), "This case needs a real visible gap inside the existing padding band");
            float separation = alreadyOverlapping ? .05f : (touchingDistance + safeGap) * .5f;
            candidates[1].PlaceForHouse(behind + Vector2.right * direction * separation);
            candidates[1].BeginDirectedWalk(fixture.Habitat.GetDispersalTarget(candidates[1].ItemId));
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "Render", fixture.View(candidates[1].ItemId));
            float actualOverlap = Mathf.Min(fixture.Body(candidates[0].ItemId).bounds.max.x, fixture.Body(candidates[1].ItemId).bounds.max.x)
                - Mathf.Max(fixture.Body(candidates[0].ItemId).bounds.min.x, fixture.Body(candidates[1].ItemId).bounds.min.x);
            if (alreadyOverlapping) Assert.That(actualOverlap, Is.GreaterThan(.02f));
            else Assert.That(actualOverlap, Is.LessThan(0f));
            var bounds = fixture.Body(candidates[0].ItemId).bounds;
            float worldCenter = fixture.House.Entrance.position.x;
            float worldRadius = radius * Mathf.Abs(fixture.Host.Floor.transform.lossyScale.x);
            Assert.That(bounds.max.x <= worldCenter - worldRadius || bounds.min.x >= worldCenter + worldRadius, Is.True);
            Vector2 before = candidates[0].GroundPosition;
            fixture.Step();
            Assert.That((bool)EnhancementFairyStage1Host.Get(fixture.View(candidates[0].ItemId), "SpacingYielding"), Is.EqualTo(!alreadyOverlapping),
                "Outside the house, existing overlap must not freeze initial companions; a separated padding gap still uses ordinary yielding");
            if (alreadyOverlapping)
                Assert.That(Vector2.Distance(before, candidates[0].GroundPosition), Is.EqualTo(fixture.CalmSpeed / 30f).Within(.001f));
            else Assert.That(Vector2.Distance(before, candidates[0].GroundPosition), Is.LessThan(.0001f));
        }
    }

    [Test]
    public void AFirstExitWalkMakesRealProgressPastAnOrdinaryWalkerInsteadOfJoiningItsYieldChain()
    {
        using (var fixture = new FairyAiRuntimeStageFixture(24))
        {
            var original = fixture.Brains().ToArray();
            var timing = new FairyFastEmergenceMetrics(fixture);
            var request = timing.Request(1, 1);
            for (int frame = 0; frame < 15 && !request.Visitors.Any(visitor => visitor.DispersingAt >= 0f); frame++)
            { fixture.Step(); timing.Observe(); }
            var visitor = fixture.Brain(request.Visitors.Single().Id);
            Assert.That(fixture.IsInitialDispersal(visitor.ItemId), Is.True);
            float direction = Mathf.Sign(fixture.Habitat.GetDispersalTarget(visitor.ItemId).x - visitor.GroundPosition.x);
            var ordinary = original[0];
            // One reproducible ordinary blocker setup, followed only by actual
            // Brain/spacing movement. It does not receive the initial-exit flag.
            ordinary.PlaceForHouse(visitor.GroundPosition + Vector2.right * direction * .2f);
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "BeginSpacingWalk", fixture.View(ordinary.ItemId),
                ordinary.GroundPosition + Vector2.right * direction * 5f, 1f);
            EnhancementFairyStage1Host.Invoke(fixture.Habitat, "Render", fixture.View(ordinary.ItemId));
            Assert.That(fixture.IsInitialDispersal(ordinary.ItemId), Is.False);
            Assert.That(fixture.Body(visitor.ItemId).bounds.Intersects(fixture.Body(ordinary.ItemId).bounds), Is.True);
            Vector2 start = visitor.GroundPosition;
            for (int frame = 0; frame < 3; frame++) fixture.Step();
            Assert.That((visitor.GroundPosition.x - start.x) * direction,
                Is.EqualTo(fixture.CalmSpeed * .1f).Within(.002f), "A first exit must keep its authored calm speed instead of inheriting an ordinary walker's yield chain");
            Assert.That((bool)EnhancementFairyStage1Host.Get(fixture.View(visitor.ItemId), "SpacingYielding"), Is.False);
            fixture.AssertOwnedAndBounded(13);
        }
    }
}

/// <summary>Uses the saved scene, production runtime initialization and the shared registration core.
/// Ownership lives only in the existing host's replaced dictionaries; it never invokes account transactions.</summary>
internal sealed class FairyAiRuntimeStageFixture : IDisposable
{
    internal readonly EnhancementFairyStage1Host Host;
    internal readonly ThirdFloorFairyPlayArea Area;
    internal readonly ThirdFloorFairyHouse House;
    internal EnhancementFairyHabitat Habitat => Host.Habitat;
    internal float Clock => Habitat.HouseActivitySeconds;
    internal float CalmSpeed => Habitat.Settings.WalkSpeed * Habitat.Settings.CalmEnergy;
    internal float MaximumNormalSpeed => Habitat.Settings.WalkSpeed * (Habitat.Settings.CalmEnergy + Habitat.Settings.PersonalityEnergyStep * 2f);
    internal Vector2 Entrance => Host.Floor.transform.InverseTransformPoint(House.Entrance.position);
    internal IList Views => (IList)EnhancementFairyStage1Host.Get(Habitat, "_views");

    internal FairyAiRuntimeStageFixture(int ownedCount, bool includeWideSprites = false)
    {
        Host = new EnhancementFairyStage1Host();
        try
        {
            Area = Host.SceneComponents<ThirdFloorFairyPlayArea>().Single();
            House = Host.SceneComponents<ThirdFloorFairyHouse>().Single();
            FairyAiHotfixProbeReflection.UseProductionAspect(Host);
            var catalog = Host.Catalog.Where(EnhancementFairyCatalog.IsEligible).ToArray();
            IEnumerable<GachaItemData> roster = catalog;
            if (includeWideSprites)
            {
                var widest = catalog.OrderByDescending(item => item.Sprite.bounds.size.x / Mathf.Max(.001f, item.Sprite.bounds.size.y)).Take(4).ToArray();
                var ordinary = catalog.Except(widest).ToArray();
                // One broad resident and three broad new visitors exercise both the doorway
                // obstruction and the exiting sprite's own real footprint.
                roster = widest.Take(1).Concat(ordinary.Take(11)).Concat(widest.Skip(1)).Concat(ordinary.Skip(11));
            }
            var items = roster.Take(ownedCount).ToArray();
            Assert.That(items.Length, Is.EqualTo(ownedCount));
            Host.Restore(items.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            Host.Move(ERestaurantFloorType.Floor3);
            Host.Move(RestaurantType.Kitchen);
            Host.Advance(.6f);
            // Preserve the existing separate presentation-only house return seed.
            EnhancementFairyStage1Host.Set(Habitat, "_houseReturnRandom", new System.Random(73118));
            Assert.That((bool)EnhancementFairyStage1Host.Invoke(Area, "BindToInitializedHabitat"), Is.True);
            Assert.That(Area.IsRuntimeBound, Is.True);
            Habitat.RegisterHouse(House, House.Entrance, House.Settings);
        }
        catch { Dispose(); throw; }
    }

    internal IEnumerable<EnhancementFairyBrain> Brains() => Enumerable.Range(0, Habitat.ActiveCount).Select(Habitat.GetActiveBrain);
    internal EnhancementFairyBrain Brain(string id) => Brains().Single(brain => brain.ItemId == id);
    internal object View(string id) => Views.Cast<object>().Single(view =>
        ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ItemId == id);
    internal bool IsInitialDispersal(string id) => (bool)EnhancementFairyStage1Host.Get(View(id), "InitialHouseDispersal");
    internal SpriteRenderer Body(string id) => Host.Floor.transform.Find("Fairy " + id + "/Item sprite").GetComponent<SpriteRenderer>();
    internal string HousePhase(string id) => EnhancementFairyStage1Host.Get(Views.Cast<object>()
        .Single(view => ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ItemId == id), "HouseMotion").ToString();
    // The shared host's Advance(seconds) always quantizes to 30Hz. Call its same
    // real Habitat core once so the 60Hz case actually runs with a 1/60s step.
    internal void Step(float dt = 1f / 30f) => Habitat.AdvancePreview(dt);
    internal void Advance(float seconds) => Host.Advance(seconds);
    internal void RequestToCount(int count)
    {
        int needed = count - Habitat.ActiveCount;
        for (int remaining = needed; remaining > 0; remaining -= 3)
            Assert.That(Habitat.RequestHouseEmergence(House, Mathf.Min(3, remaining)), Is.EqualTo(Mathf.Min(3, remaining)));
    }
    internal void RequestHouseBurst(int extra)
    {
        int remaining = extra, stage = 1;
        while (remaining > 0)
        {
            int requested = Mathf.Min(remaining, stage);
            Assert.That(Habitat.RequestHouseEmergence(House, requested), Is.EqualTo(requested));
            remaining -= requested;
            stage = Mathf.Min(3, stage + 1);
        }
    }
    internal void UntilHouseQueueDrained(int expectedCount, float timeout)
    {
        int frames = Mathf.CeilToInt(timeout * 30f);
        while (frames-- > 0 && (Habitat.PendingHouseSpawnCount > 0 || Brains().Any(brain => HousePhase(brain.ItemId) == "Emerging"))) Step();
        Assert.That(Habitat.PendingHouseSpawnCount, Is.Zero, Habitat.GetAiDiagnosticSnapshot());
        Assert.That(Habitat.ActiveCount, Is.EqualTo(expectedCount), "Space-aware emergence must still admit the requested guests in the authored room.");
    }
    internal void AssertOwnedAndBounded(int cap)
    {
        var ids = Brains().Select(brain => brain.ItemId).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        Assert.That(ids.All(id => UserInfo.GetGiveGachaItemCountDic().ContainsKey(id)), Is.True);
        Assert.That(ids.Length, Is.LessThanOrEqualTo(cap));
        Assert.That(Habitat.HouseHardCap, Is.EqualTo(24));
    }
    public void Dispose()
    {
        if (Area != null) EnhancementFairyStage1Host.Invoke(Area, "OnDisable");
        Host?.Dispose();
    }
}

/// <summary>Independent measurements from rendered bodies. Walking/dispersing labels do not
/// erase presence or overlap. Only actual birth/house/furniture presentations are separated.</summary>
internal sealed class FairyHouseCrowdMetrics
{
    internal sealed class InitialVisitor
    {
        internal string Id;
        internal float StartedAt, CompletedAt = -1f, ClearedAt = -1f, MaximumWidth;
        internal bool RemovedBeforeClearance;
    }
    internal sealed class BodySample
    {
        internal string Id, HousePhase;
        internal Vector2 Ground, DispersalTarget, MovementTarget;
        internal Bounds Bounds;
        internal float StepX, DwellSeconds, IntendedDirection;
        internal bool Eligible, BirthProtected, Replacing, Dispersing, NearHouse, Moving, SpacingYielding, DirectedWalking;
    }
    internal sealed class PairSample
    {
        internal string First, Second, Classification;
        internal float Ratio, RawSeconds, SameDirectionSeconds, NearHouseSameDirectionSeconds, StationarySeconds;
        internal bool BothNearHouse;
    }
    private sealed class History
    {
        internal Vector2 Ground;
        internal string HousePhase;
        internal float Dwell, EmergenceEndedAt = -1f;
        internal bool SawEmerging, CheckedFirstDispersal, ReportedDeadline;
    }
    private sealed class PairHistory { internal float Raw, Same, NearRaw, NearSame, Still; internal int LastFrame; }
    private readonly FairyAiRuntimeStageFixture _fixture;
    private readonly Dictionary<string, History> _history = new Dictionary<string, History>();
    private readonly Dictionary<string, PairHistory> _pairs = new Dictionary<string, PairHistory>();
    internal readonly Dictionary<string, InitialVisitor> InitialVisitors = new Dictionary<string, InitialVisitor>();
    private float _initialNearWidth;
    private int _initialNearCount;
    private int _frame;
    private float _cohortSeconds, _longPresenceExcessSeconds, _wholeRoomCohortSeconds;
    internal readonly List<BodySample> Bodies = new List<BodySample>();
    internal readonly List<PairSample> Pairs = new List<PairSample>();
    internal int PeakActive, ExitDirectionReversals, ExitOutwardViolations, FirstDispersalDeadlineMisses, FirstDispersalCount, LeftExits, RightExits;
    internal int BodyNearCount, LongPresenceCount, PersistentCohortSize, PeakBodyNearCount, PeakLongPresenceCount;
    internal float MaximumSameDirectionOverlapSeconds, MaximumRawPairOverlapSeconds, MaximumLongCohortSeconds, MaximumLongPresenceExcessSeconds;
    internal float WholeRoomMaximumSameRouteOverlapSeconds, WholeRoomMaximumRawOverlapSeconds, WholeRoomMaximumStationaryPairSeconds, WholeRoomMaximumCohortSeconds;
    internal int WholeRoomCohortSize;
    internal string WorstMovingPair = "none";
    internal string WholeRoomWorstMovingPair = "none";
    internal const float DeepOverlapFraction = .25f;

    internal int InitialVisitorCount => InitialVisitors.Count;
    internal int InitialVisitorsCleared => InitialVisitors.Values.Count(visitor => visitor.ClearedAt >= 0f);
    internal float MaximumInitialVisitorClearanceSeconds => InitialVisitors.Count == 0 ? 0f
        : InitialVisitors.Values.Max(visitor => visitor.CompletedAt < 0f ? 0f
            : (visitor.ClearedAt >= 0f ? visitor.ClearedAt : _fixture.Clock) - visitor.CompletedAt);
    internal float InitialClearanceBudgetSeconds => (2f * _fixture.House.Settings.SafeHouseCrowdingRadius
        + _initialNearWidth + InitialVisitors.Values.Sum(visitor => visitor.MaximumWidth)
        + _fixture.House.Settings.SafeSpriteSpacingPadding * (InitialVisitors.Count + _initialNearCount))
        / Mathf.Max(.01f, _fixture.CalmSpeed) + _fixture.House.Settings.SafeHouseResidentGraceSeconds + .1f;

    internal FairyHouseCrowdMetrics(FairyAiRuntimeStageFixture fixture)
    {
        _fixture = fixture;
        Observe(0f);
        _initialNearWidth = Bodies.Where(body => body.NearHouse).Sum(body => body.Bounds.size.x)
            / Mathf.Max(.001f, Mathf.Abs(fixture.Host.Floor.transform.lossyScale.x));
        _initialNearCount = Bodies.Count(body => body.NearHouse);
    }

    internal void Observe(float dt)
    {
        _frame++;
        Bodies.Clear(); Pairs.Clear();
        BodyNearCount = LongPresenceCount = PersistentCohortSize = WholeRoomCohortSize = 0;
        float clock = _fixture.Clock, center = _fixture.Entrance.x, radius = _fixture.House.Settings.SafeHouseCrowdingRadius;
        float worldCenter = _fixture.House.Entrance.position.x;
        float worldRadius = radius * Mathf.Abs(_fixture.Host.Floor.transform.lossyScale.x);
        var ids = new HashSet<string>();
        var arrivalQueue = (EnhancementFairyArrivalQueue)EnhancementFairyStage1Host.Get(_fixture.Habitat, "_arrivals");
        foreach (object view in _fixture.Views)
        {
            var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
            var body = (SpriteRenderer)EnhancementFairyStage1Host.Get(view, "Body");
            if (brain == null || body == null || !body.enabled || !body.gameObject.activeInHierarchy || body.sprite == null) continue;
            string id = brain.ItemId;
            ids.Add(id);
            if (!_history.TryGetValue(id, out var history)) _history.Add(id, history = new History { Ground = brain.GroundPosition });
            var sample = new BodySample
            {
                Id = id, Ground = brain.GroundPosition, Bounds = body.bounds,
                HousePhase = EnhancementFairyStage1Host.Get(view, "HouseMotion").ToString(),
                Replacing = (bool)EnhancementFairyStage1Host.Get(view, "Replacing"),
                Dispersing = _fixture.Habitat.IsItemDispersing(id),
                DispersalTarget = _fixture.Habitat.GetDispersalTarget(id),
                MovementTarget = brain.MovementTarget,
                DirectedWalking = brain.IsDirectedWalking,
                SpacingYielding = (bool)EnhancementFairyStage1Host.Get(view, "SpacingYielding"),
                StepX = brain.GroundPosition.x - history.Ground.x,
                BirthProtected = (float)EnhancementFairyStage1Host.Get(view, "ArrivalRemaining") > 0f
                    || (float)EnhancementFairyStage1Host.Get(view, "PriorityUntil") > clock || arrivalQueue.IsPending(id)
            };
            sample.Moving = Mathf.Abs(sample.StepX) > .001f;
            if ((brain.Action == EnhancementFairyAction.Walk || brain.Action == EnhancementFairyAction.Visit)
                && Mathf.Abs(sample.MovementTarget.x - sample.Ground.x) > .001f)
                sample.IntendedDirection = Mathf.Sign(sample.MovementTarget.x - sample.Ground.x);
            sample.NearHouse = sample.Bounds.max.x > worldCenter - worldRadius && sample.Bounds.min.x < worldCenter + worldRadius;
            sample.Eligible = !sample.BirthProtected && sample.HousePhase == "None"
                && !sample.Replacing && body.color.a >= .95f;
            // Movement cannot reset continuous presence. The whole sprite must leave the zone.
            history.Dwell = sample.Eligible && sample.NearHouse ? history.Dwell + dt : 0f;
            sample.DwellSeconds = history.Dwell;
            if (sample.NearHouse) BodyNearCount++;
            if (sample.Eligible && sample.NearHouse && sample.DwellSeconds >= 3f) LongPresenceCount++;
            if (sample.HousePhase == "Emerging") history.SawEmerging = true;
            if (sample.HousePhase == "Emerging" && !InitialVisitors.ContainsKey(id))
                InitialVisitors.Add(id, new InitialVisitor { Id = id, StartedAt = clock });
            if (InitialVisitors.TryGetValue(id, out var visitor))
            {
                visitor.MaximumWidth = Mathf.Max(visitor.MaximumWidth, sample.Bounds.size.x
                    / Mathf.Max(.001f, Mathf.Abs(_fixture.Host.Floor.transform.lossyScale.x)));
                if (visitor.CompletedAt < 0f && sample.HousePhase == "None") visitor.CompletedAt = clock;
                if (visitor.CompletedAt >= 0f && visitor.ClearedAt < 0f && !sample.NearHouse) visitor.ClearedAt = clock;
            }
            if (history.SawEmerging && history.HousePhase == "Emerging" && sample.HousePhase == "None") history.EmergenceEndedAt = clock;
            if (history.SawEmerging && !history.CheckedFirstDispersal && sample.HousePhase == "None" && sample.Dispersing)
            {
                Vector2 exit = (Vector2)EnhancementFairyStage1Host.Get(view, "HouseEnd");
                float side = Mathf.Sign(exit.x - center);
                if (side < 0f) LeftExits++; else RightExits++;
                if ((sample.DispersalTarget.x - center) * side < -.001f) ExitDirectionReversals++;
                if ((sample.DispersalTarget.x - exit.x) * side < -.05f) ExitOutwardViolations++;
                FirstDispersalCount++;
                history.CheckedFirstDispersal = true;
            }
            if (history.EmergenceEndedAt >= 0f && !history.CheckedFirstDispersal && !history.ReportedDeadline
                && sample.Eligible && clock - history.EmergenceEndedAt > 3f)
            { FirstDispersalDeadlineMisses++; history.ReportedDeadline = true; }
            history.Ground = sample.Ground;
            history.HousePhase = sample.HousePhase;
            Bodies.Add(sample);
        }
        foreach (string missing in _history.Keys.Where(id => !ids.Contains(id)).ToArray()) _history.Remove(missing);
        foreach (var visitor in InitialVisitors.Values)
            if (visitor.ClearedAt < 0f && !ids.Contains(visitor.Id)) visitor.RemovedBeforeClearance = true;
        var parents = Enumerable.Range(0, Bodies.Count).ToArray();
        var wholeRoomParents = Enumerable.Range(0, Bodies.Count).ToArray();
        for (int a = 0; a < Bodies.Count; a++)
        for (int b = a + 1; b < Bodies.Count; b++)
        {
            var first = Bodies[a]; var second = Bodies[b];
            bool bothNearHouse = first.NearHouse && second.NearHouse;
            float overlap = Mathf.Min(first.Bounds.max.x, second.Bounds.max.x) - Mathf.Max(first.Bounds.min.x, second.Bounds.min.x);
            float ratio = overlap / Mathf.Max(.001f, Mathf.Min(first.Bounds.size.x, second.Bounds.size.x));
            float vertical = Mathf.Min(first.Bounds.max.y, second.Bounds.max.y) - Mathf.Max(first.Bounds.min.y, second.Bounds.min.y);
            if (ratio < DeepOverlapFraction || vertical < Mathf.Min(first.Bounds.size.y, second.Bounds.size.y) * .2f) continue;
            string key = string.CompareOrdinal(first.Id, second.Id) < 0 ? first.Id + "|" + second.Id : second.Id + "|" + first.Id;
            if (!_pairs.TryGetValue(key, out var pair)) _pairs.Add(key, pair = new PairHistory());
            if (pair.LastFrame != _frame - 1) pair.Raw = pair.Same = pair.NearRaw = pair.NearSame = pair.Still = 0f;
            pair.LastFrame = _frame;
            pair.Raw += dt;
            bool sameDirection = first.Moving && second.Moving && first.StepX * second.StepX > 0f;
            // A following walker may deliberately yield for one or many frames.
            // Its unchanged route still belongs to the same moving crowd; a pause
            // cannot reset the measured continuous overlap.
            bool sameRoute = (first.Dispersing || second.Dispersing)
                && first.IntendedDirection != 0f && first.IntendedDirection == second.IntendedDirection;
            bool opposite = first.Moving && second.Moving && first.StepX * second.StepX < 0f;
            bool stationary = !first.Moving && !second.Moving;
            bool eligible = first.Eligible && second.Eligible;
            string classification = !eligible ? "protected-presentation" : sameDirection ? "same-direction-moving"
                : sameRoute ? "same-route-yielding"
                : opposite ? "opposite-direction-passing" : stationary ? "stationary" : "single-moving-passing";
            pair.Same = eligible && (sameDirection || sameRoute) ? pair.Same + dt : 0f;
            pair.NearRaw = bothNearHouse ? pair.NearRaw + dt : 0f;
            pair.NearSame = bothNearHouse && eligible && (sameDirection || sameRoute) ? pair.NearSame + dt : 0f;
            pair.Still = eligible && stationary ? pair.Still + dt : 0f;
            MaximumRawPairOverlapSeconds = Mathf.Max(MaximumRawPairOverlapSeconds, pair.NearRaw);
            if (pair.NearSame > MaximumSameDirectionOverlapSeconds)
            { MaximumSameDirectionOverlapSeconds = pair.NearSame; WorstMovingPair = key; }
            WholeRoomMaximumRawOverlapSeconds = Mathf.Max(WholeRoomMaximumRawOverlapSeconds, pair.Raw);
            WholeRoomMaximumStationaryPairSeconds = Mathf.Max(WholeRoomMaximumStationaryPairSeconds, pair.Still);
            if (pair.Same > WholeRoomMaximumSameRouteOverlapSeconds)
            { WholeRoomMaximumSameRouteOverlapSeconds = pair.Same; WholeRoomWorstMovingPair = key; }
            Pairs.Add(new PairSample { First = first.Id, Second = second.Id, Ratio = ratio,
                RawSeconds = pair.Raw, SameDirectionSeconds = pair.Same, Classification = classification,
                BothNearHouse = bothNearHouse, NearHouseSameDirectionSeconds = pair.NearSame, StationarySeconds = pair.Still });
            // Two permitted residents and a single ordinary passer must not be treated as
            // a three-fairy persistent coalition. Opposite-direction passes remain explicit data.
            if (eligible && (sameDirection || sameRoute || stationary))
                wholeRoomParents[Find(wholeRoomParents, b)] = Find(wholeRoomParents, a);
            if (bothNearHouse && eligible && (sameDirection || sameRoute || stationary) && first.DwellSeconds >= 3f && second.DwellSeconds >= 3f)
                parents[Find(parents, b)] = Find(parents, a);
        }
        var groups = new Dictionary<int, int>();
        var wholeRoomGroups = new Dictionary<int, int>();
        for (int index = 0; index < Bodies.Count; index++)
        {
            if (Bodies[index].Eligible)
            {
                int wholeRoot = Find(wholeRoomParents, index);
                wholeRoomGroups[wholeRoot] = wholeRoomGroups.TryGetValue(wholeRoot, out int wholeCount) ? wholeCount + 1 : 1;
            }
            if (!Bodies[index].Eligible || !Bodies[index].NearHouse || Bodies[index].DwellSeconds < 3f) continue;
            int root = Find(parents, index);
            groups[root] = groups.TryGetValue(root, out int count) ? count + 1 : 1;
        }
        if (groups.Count > 0) PersistentCohortSize = groups.Values.Max();
        if (wholeRoomGroups.Count > 0) WholeRoomCohortSize = wholeRoomGroups.Values.Max();
        _wholeRoomCohortSeconds = WholeRoomCohortSize > 2 ? _wholeRoomCohortSeconds + dt : 0f;
        WholeRoomMaximumCohortSeconds = Mathf.Max(WholeRoomMaximumCohortSeconds, _wholeRoomCohortSeconds);
        _cohortSeconds = PersistentCohortSize > 2 ? _cohortSeconds + dt : 0f;
        _longPresenceExcessSeconds = LongPresenceCount > 2 ? _longPresenceExcessSeconds + dt : 0f;
        MaximumLongCohortSeconds = Mathf.Max(MaximumLongCohortSeconds, _cohortSeconds);
        MaximumLongPresenceExcessSeconds = Mathf.Max(MaximumLongPresenceExcessSeconds, _longPresenceExcessSeconds);
        PeakActive = Mathf.Max(PeakActive, _fixture.Habitat.ActiveCount);
        PeakBodyNearCount = Mathf.Max(PeakBodyNearCount, BodyNearCount);
        PeakLongPresenceCount = Mathf.Max(PeakLongPresenceCount, LongPresenceCount);
    }
    private static int Find(int[] parent, int index)
    { while (parent[index] != index) index = parent[index]; return index; }
    internal string Describe() => "peakActive=" + PeakActive + "; firstDispersals=" + FirstDispersalCount
        + "; left/right=" + LeftExits + "/" + RightExits + "; directionReversals=" + ExitDirectionReversals
        + "; outwardViolations=" + ExitOutwardViolations + "; firstDispersalOver3s=" + FirstDispersalDeadlineMisses
        + "; nearBodyPeak=" + PeakBodyNearCount + "; longPresencePeak=" + PeakLongPresenceCount
        + "; longPresenceExcessSeconds=" + MaximumLongPresenceExcessSeconds
        + "; sameDirectionOverlapSeconds=" + MaximumSameDirectionOverlapSeconds + "; worstPair=" + WorstMovingPair
        + "; persistent3plusCohortSeconds=" + MaximumLongCohortSeconds + "; rawOverlapSeconds=" + MaximumRawPairOverlapSeconds
        + "; wholeRoomSameRouteOverlapSeconds=" + WholeRoomMaximumSameRouteOverlapSeconds + "; wholeRoomWorstPair=" + WholeRoomWorstMovingPair
        + "; wholeRoomStationaryPairSeconds=" + WholeRoomMaximumStationaryPairSeconds
        + "; wholeRoom3plusCohortSeconds=" + WholeRoomMaximumCohortSeconds + "; wholeRoomRawOverlapSeconds=" + WholeRoomMaximumRawOverlapSeconds
        + "; initialVisitorsCleared=" + InitialVisitorsCleared + "/" + InitialVisitorCount
        + "; longestInitialClearanceSeconds=" + MaximumInitialVisitorClearanceSeconds
        + "; actualWidthSpeedClearanceBudget=" + InitialClearanceBudgetSeconds;
}

/// <summary>Frame-observed timing of the real Habitat request path. This does not
/// pretend that Stage1Host executes ThirdFloorFairyHouse input/Update lifecycle.</summary>
internal sealed class FairyFastEmergenceMetrics
{
    internal sealed class Visitor
    {
        internal string Id;
        internal float StartedAt, CompletedAt = -1f, DispersingAt = -1f, ClearedAt = -1f, InitialEndedAt = -1f;
        internal float AlphaAtStart, ScaleAtStart, AlphaAtCompletion, ScaleAtCompletion;
        internal Vector2 HouseStart, HouseEnd, GroundAtCompletion, InitialEndGround, LastGround;
        internal float LastObservedAt, EmergencePathLength, MaximumEmergenceStep, MaximumEmergenceSpeed;
        internal float MaximumInitialStep, MaximumInitialSpeed, MaximumTransitionStep, MaximumTransitionSpeed;
        internal float MaximumHouseLift, HouseLiftAtCompletion, MaximumInitialStationarySeconds, MaximumInitialYieldSeconds;
        internal float InitialStationarySeconds, InitialYieldSeconds;
        internal bool LastInitial;
        internal string Phase;
        internal float ActualHopDistance => Vector2.Distance(HouseStart, GroundAtCompletion);
    }
    internal sealed class RequestRecord
    {
        internal int Stage, Requested, Accepted;
        internal float RequestedAt, Interval, Duration, CalmSpeed, InitialMultiplier;
        internal readonly List<Visitor> Visitors = new List<Visitor>();
    }
    private readonly FairyAiRuntimeStageFixture _fixture;
    internal readonly List<RequestRecord> Requests = new List<RequestRecord>();
    private readonly Dictionary<string, Visitor> _visitors = new Dictionary<string, Visitor>();
    internal IEnumerable<Visitor> Visitors => _visitors.Values;
    internal int PeakActive;

    internal FairyFastEmergenceMetrics(FairyAiRuntimeStageFixture fixture) { _fixture = fixture; PeakActive = fixture.Habitat.ActiveCount; }

    internal RequestRecord Request(int stage, int count)
    {
        var request = new RequestRecord { Stage = stage, Requested = count, RequestedAt = _fixture.Clock,
            Interval = _fixture.House.Settings.SafeSpawnInterval, Duration = _fixture.House.Settings.SafeEntranceDuration,
            CalmSpeed = _fixture.CalmSpeed, InitialMultiplier = _fixture.House.Settings.SafeTapDispersalSpeedMultiplier };
        Requests.Add(request);
        request.Accepted = _fixture.Habitat.RequestHouseEmergence(_fixture.House, count);
        Observe(); // A zero-delay first visitor can already be rendered inside the request.
        return request;
    }

    internal void Observe()
    {
        PeakActive = Mathf.Max(PeakActive, _fixture.Habitat.ActiveCount);
        float clock = _fixture.Clock;
        float center = _fixture.House.Entrance.position.x;
        float radius = _fixture.House.Settings.SafeHouseCrowdingRadius * Mathf.Abs(_fixture.Host.Floor.transform.lossyScale.x);
        foreach (object view in _fixture.Views)
        {
            var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
            var body = (SpriteRenderer)EnhancementFairyStage1Host.Get(view, "Body");
            if (brain == null || body == null || !body.enabled || !body.gameObject.activeInHierarchy || body.sprite == null) continue;
            string phase = EnhancementFairyStage1Host.Get(view, "HouseMotion").ToString();
            bool initial = (bool)EnhancementFairyStage1Host.Get(view, "InitialHouseDispersal");
            float lift = (float)EnhancementFairyStage1Host.Get(view, "HouseLift");
            if (!_visitors.TryGetValue(brain.ItemId, out var visitor))
            {
                if (phase != "Emerging") continue;
                var request = Requests.FirstOrDefault(value => value.Visitors.Count < value.Accepted);
                if (request == null) throw new InvalidOperationException("An unaccepted visitor emerged: " + brain.ItemId);
                visitor = new Visitor { Id = brain.ItemId, StartedAt = clock, AlphaAtStart = body.color.a,
                    ScaleAtStart = (float)EnhancementFairyStage1Host.Get(view, "HouseScale"),
                    HouseStart = (Vector2)EnhancementFairyStage1Host.Get(view, "HouseStart"),
                    HouseEnd = (Vector2)EnhancementFairyStage1Host.Get(view, "HouseEnd"),
                    LastGround = brain.GroundPosition, LastObservedAt = clock, Phase = phase };
                _visitors.Add(brain.ItemId, visitor);
                request.Visitors.Add(visitor);
            }
            float dt = clock - visitor.LastObservedAt;
            if (dt > .000001f)
            {
                float step = Mathf.Abs(brain.GroundPosition.x - visitor.LastGround.x);
                float speed = step / dt;
                if (visitor.Phase == "Emerging")
                {
                    visitor.EmergencePathLength += step;
                    visitor.MaximumEmergenceStep = Mathf.Max(visitor.MaximumEmergenceStep, step);
                    visitor.MaximumEmergenceSpeed = Mathf.Max(visitor.MaximumEmergenceSpeed, speed);
                }
                if (visitor.LastInitial && visitor.Phase == "None" && phase == "None")
                {
                    visitor.MaximumInitialStep = Mathf.Max(visitor.MaximumInitialStep, step);
                    visitor.MaximumInitialSpeed = Mathf.Max(visitor.MaximumInitialSpeed, speed);
                    visitor.InitialStationarySeconds = step < .00001f ? visitor.InitialStationarySeconds + dt : 0f;
                    bool yielding = (bool)EnhancementFairyStage1Host.Get(view, "SpacingYielding");
                    visitor.InitialYieldSeconds = yielding && step < .00001f ? visitor.InitialYieldSeconds + dt : 0f;
                    visitor.MaximumInitialStationarySeconds = Mathf.Max(visitor.MaximumInitialStationarySeconds, visitor.InitialStationarySeconds);
                    visitor.MaximumInitialYieldSeconds = Mathf.Max(visitor.MaximumInitialYieldSeconds, visitor.InitialYieldSeconds);
                }
                if (visitor.Phase == "Emerging" && phase == "None" || visitor.LastInitial && !initial && phase == "None")
                {
                    visitor.MaximumTransitionStep = Mathf.Max(visitor.MaximumTransitionStep, step);
                    visitor.MaximumTransitionSpeed = Mathf.Max(visitor.MaximumTransitionSpeed, speed);
                }
                if (visitor.LastInitial && !initial && visitor.InitialEndedAt < 0f)
                { visitor.InitialEndedAt = clock; visitor.InitialEndGround = brain.GroundPosition; }
            }
            if (phase == "Emerging") visitor.MaximumHouseLift = Mathf.Max(visitor.MaximumHouseLift, lift);
            if (visitor.CompletedAt < 0f && phase == "None")
            {
                visitor.CompletedAt = clock;
                visitor.AlphaAtCompletion = body.color.a;
                visitor.ScaleAtCompletion = (float)EnhancementFairyStage1Host.Get(view, "HouseScale");
                visitor.GroundAtCompletion = brain.GroundPosition;
                visitor.HouseLiftAtCompletion = lift;
            }
            if (visitor.DispersingAt < 0f && _fixture.Habitat.IsItemDispersing(brain.ItemId)) visitor.DispersingAt = clock;
            if (visitor.CompletedAt >= 0f && visitor.ClearedAt < 0f
                && (body.bounds.max.x <= center - radius || body.bounds.min.x >= center + radius)) visitor.ClearedAt = clock;
            visitor.Phase = phase;
            visitor.LastGround = brain.GroundPosition;
            visitor.LastObservedAt = clock;
            visitor.LastInitial = initial;
        }
    }

    internal static void AssertRequestTiming(RequestRecord request, float dt)
    {
        Assert.That(request.Visitors.Count, Is.EqualTo(request.Accepted), "Each accepted request must become a real visible sprite");
        if (request.Accepted == 0) return;
        var visitors = request.Visitors.OrderBy(visitor => visitor.StartedAt).ToArray();
        Assert.That(visitors[0].StartedAt - request.RequestedAt, Is.InRange(0f, .1001f), "First visible Emerging must not wait for exit clearance");
        foreach (var visitor in visitors)
        {
            Assert.That(visitor.AlphaAtStart, Is.GreaterThan(0f));
            Assert.That(visitor.ScaleAtStart, Is.GreaterThan(0f));
            Assert.That(visitor.CompletedAt, Is.GreaterThanOrEqualTo(visitor.StartedAt));
            Assert.That(visitor.CompletedAt - visitor.StartedAt, Is.InRange(request.Duration - dt - .001f, request.Duration + dt + .001f));
            Assert.That(visitor.AlphaAtCompletion, Is.GreaterThanOrEqualTo(.99f));
            Assert.That(visitor.ScaleAtCompletion, Is.EqualTo(1f).Within(.001f));
            Assert.That(visitor.DispersingAt - visitor.CompletedAt, Is.InRange(0f, dt + .001f), "Landing must hand off to the actual dispersion walk");
            Assert.That(visitor.ActualHopDistance, Is.GreaterThan(0f).And.LessThanOrEqualTo(request.CalmSpeed * request.Duration / 1.5f + .001f));
            Assert.That(visitor.EmergencePathLength, Is.EqualTo(visitor.ActualHopDistance).Within(.001f), "The short hop must progress continuously and outward");
            Assert.That(visitor.MaximumEmergenceStep, Is.LessThanOrEqualTo(request.CalmSpeed * dt + .001f));
            Assert.That(visitor.MaximumEmergenceSpeed, Is.LessThanOrEqualTo(request.CalmSpeed + .005f), "A longer duration must not conceal a high-speed horizontal launch");
            Assert.That(visitor.MaximumInitialSpeed, Is.LessThanOrEqualTo(request.CalmSpeed * request.InitialMultiplier + .005f));
            Assert.That(visitor.MaximumTransitionStep, Is.LessThanOrEqualTo(request.CalmSpeed * dt + .001f));
            Assert.That(visitor.MaximumHouseLift, Is.GreaterThan(0f), "The short hop keeps its visible vertical arc");
            Assert.That(visitor.HouseLiftAtCompletion, Is.Zero.Within(.001f));
            Assert.That(Vector2.Distance(visitor.GroundAtCompletion, visitor.HouseEnd), Is.LessThanOrEqualTo(.001f));
        }
        for (int index = 1; index < visitors.Length; index++)
            Assert.That(Mathf.Abs(visitors[index].StartedAt - visitors[index - 1].StartedAt - request.Interval), Is.LessThanOrEqualTo(dt + .001f));
        Assert.That(visitors.Max(visitor => visitor.CompletedAt) - request.RequestedAt,
            Is.LessThanOrEqualTo((visitors.Length - 1) * request.Interval + request.Duration + 2f * dt + .001f),
            "Completion follows the authored stagger and hop duration; the former 0.3-second burst requirement does not apply");
    }
}

internal static class FairyAiHotfixProbeReflection
{
    internal static object Property(object owner, string name)
    {
        var info = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (info == null) throw new MissingMemberException(owner.GetType().Name, name);
        return info.GetValue(owner);
    }

    internal static void UseProductionAspect(EnhancementFairyStage1Host host)
    {
        // Camera.Awake has unrelated account/UI lifecycle. Reproduce only its pure aspect formula
        // on this PreviewScene camera, using the actual serialized target aspect.
        float targetAspect = (float)EnhancementFairyStage1Host.Get(host.Controller, "_targetAspect");
        float aspect = 1920f / 1080f;
        float heightScale = aspect / targetAspect;
        if (heightScale < 1f) host.Camera.orthographicSize /= heightScale;
        host.Camera.aspect = aspect;
        host.Camera.pixelRect = new Rect(0f, 0f, 1920f, 1080f);
    }
}
#endif
