#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Body = EnhancementFairyPhysicsSolver.Body;
using Mode = EnhancementFairyPhysicsSolver.BodyMode;

/// <summary>Production solver and original Stage1/Camera input replay; no account or saved-scene mutation.</summary>
public sealed class EnhancementFairyPhysicsTests
{
    [TestCase(30, .5f)] [TestCase(60, .5f)] [TestCase(120, .5f)]
    [TestCase(30, 3f)] [TestCase(60, 3f)] [TestCase(120, 3f)]
    [TestCase(30, 7f)] [TestCase(60, 7f)] [TestCase(120, 7f)]
    [TestCase(60, 10f)]
    public void DropFromDifferentHeightsBouncesThenRestoresFiniteGroundPose(int fps, float height)
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var body = MakeBody(1, new Vector2(0f, .85f + height)); var bodies = new[] { body };
        solver.BeginDrag(body, settings); solver.Release(body, Vector2.zero, settings);
        float peakSquash = 0f, lastY = body.Position.y, firstImpact = -1f; bool descended = false, bounced = false;
        var reboundHeights = new float[3];
        for (int frame = 0; frame < fps * 7; frame++)
        {
            solver.Step(bodies, 1, 1f / fps, settings); AssertFinite(body, settings);
            descended |= body.Position.y < lastY - .0001f;
            bounced |= body.BounceCount > 0 && body.Velocity.y > .05f;
            peakSquash = Mathf.Max(peakSquash, body.Squash); lastY = body.Position.y;
            if (body.BounceCount > 0)
            {
                if (firstImpact < 0f) firstImpact = (frame + 1f) / fps;
                int rebound = Mathf.Min(body.BounceCount - 1, reboundHeights.Length - 1);
                reboundHeights[rebound] = Mathf.Max(reboundHeights[rebound], body.Position.y - settings.SafeBounds.yMin - body.HalfExtents.y);
            }
        }
        Assert.That(descended && bounced, Is.True, "A slow release must fall and rebound, not teleport to the floor.");
        Assert.That(body.BounceCount, Is.InRange(1, settings.MaximumBounceCount));
        Assert.That(peakSquash, Is.GreaterThan(.02f)); AssertRestored(body, settings);
        TestContext.WriteLine("PHYSICS_DROP fps=" + fps + "; height=" + height + "; firstImpactSeconds=" + firstImpact
            + "; reboundHeights=" + string.Join(",", reboundHeights) + "; bounces=" + body.BounceCount + "; squash=" + peakSquash);
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void FastThrowClampsSpeedAndWallReflectionReducesHorizontalEnergy(int fps)
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var body = MakeBody(1, new Vector2(settings.Bounds.xMax - 1f, 4f)); var bodies = new[] { body };
        solver.BeginDrag(body, settings); solver.Release(body, new Vector2(10000f, 0f), settings);
        Assert.That(body.Velocity.magnitude, Is.EqualTo(settings.MaxThrowSpeed).Within(.0001f));
        bool reflected = false; float firstReflectedSpeed = 0f;
        for (int frame = 0; frame < fps; frame++)
        {
            solver.Step(bodies, 1, 1f / fps, settings); AssertFinite(body, settings);
            if (!reflected && body.Velocity.x < 0f) { reflected = true; firstReflectedSpeed = -body.Velocity.x; }
        }
        Assert.That(reflected, Is.True); Assert.That(firstReflectedSpeed, Is.LessThan(settings.MaxThrowSpeed * .6f));
        Assert.That(Mathf.Abs(body.Angle), Is.LessThanOrEqualTo(settings.AngularReactionLimit));
    }

    [Test]
    public void FixedStepsKeepThirtySixtyAndOneTwentyHzTrajectoriesConsistent()
    {
        Vector2[] reference = ThrowTrajectory(120);
        foreach (int fps in new[] { 30, 60 })
        {
            Vector2[] actual = ThrowTrajectory(fps);
            for (int i = 0; i < reference.Length; i++)
                Assert.That(Vector2.Distance(actual[i], reference[i]), Is.LessThan(.015f), "Trajectory sample=" + i + "; fps=" + fps);
        }
    }

    [TestCase(2)] [TestCase(3)] [TestCase(5)] [TestCase(24)]
    public void PairImpulsePropagatesThroughNeighboursAndNeverMakesMoreThanFivePhysical(int count)
    {
        var settings = SolverSettings(); settings.Bounds = new Rect(-20f, 0f, 40f, 12f);
        var solver = new EnhancementFairyPhysicsSolver(); var bodies = new Body[count];
        for (int i = 0; i < count; i++) bodies[i] = MakeBody(i + 1, new Vector2(-12f + i * 1.31f, .85f));
        solver.BeginDrag(bodies[0], settings); solver.Release(bodies[0], Vector2.right * 15f, settings);
        var activated = new HashSet<int>(); int peak = 0; float deliveredSpeed = 0f;
        for (int frame = 0; frame < 720; frame++)
        {
            solver.Step(bodies, count, 1f / 120f, settings);
            peak = Mathf.Max(peak, solver.CountActive(bodies, count));
            foreach (var body in bodies)
            { AssertFinite(body, settings); if (body.Mode == Mode.Physics) activated.Add(body.Id); }
            deliveredSpeed = Mathf.Max(deliveredSpeed, bodies[1].Velocity.x);
            Assert.That(solver.CountActive(bodies, count), Is.LessThanOrEqualTo(5));
        }
        Assert.That(deliveredSpeed, Is.GreaterThan(.3f), "B must receive real motion, not only a cosmetic squash.");
        Assert.That(activated.Count, Is.GreaterThanOrEqualTo(Mathf.Min(count, 3)), "A → B → C impulse propagation.");
        if (count >= 5) Assert.That(peak, Is.EqualTo(5));
        foreach (var body in bodies) Assert.That(body.Mode, Is.EqualTo(Mode.Autonomous));
        TestContext.WriteLine("PHYSICS_CHAIN displayed=" + count + "; peakPhysical=" + peak + "; propagated=" + activated.Count + "; Bspeed=" + deliveredSpeed);
    }

    [Test]
    public void CompletelyCoincidentBodiesAndInvalidValuesNeverProduceNaNOrInfiniteMotion()
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var a = MakeBody(1, new Vector2(0f, 3f)); var b = MakeBody(2, a.Position); var bodies = new[] { a, b };
        solver.BeginDrag(a, settings); solver.Release(a, Vector2.right * 8f, settings);
        solver.BeginDrag(b, settings); solver.Release(b, Vector2.left * 8f, settings);
        solver.Step(bodies, 2, 1f / 120f, settings);
        Assert.That(Vector2.Distance(a.Position, b.Position), Is.GreaterThan(1f));
        a.Position = new Vector2(float.NaN, float.PositiveInfinity); a.Velocity = Vector2.one * float.PositiveInfinity;
        a.Angle = float.NaN; a.Radius = float.NaN; a.Mass = float.NegativeInfinity;
        solver.Step(bodies, 2, float.NaN, settings); solver.Step(bodies, 2, float.PositiveInfinity, settings);
        for (int i = 0; i < 720; i++) { solver.Step(bodies, 2, 1f / 120f, settings); AssertFinite(a, settings); AssertFinite(b, settings); }
        AssertRestored(a, settings); AssertRestored(b, settings);
    }

    [Test]
    public void CollisionProtectsIneligibleFairiesAndUsesDifferentSpriteRadiiAndMasses()
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var a = MakeBody(1, new Vector2(-3f, .85f)); var b = MakeBody(2, new Vector2(-1.4f, .85f));
        b.Radius = .8f; b.Mass = 2f; b.Eligible = false; Vector2 initial = b.Position;
        var bodies = new[] { a, b }; solver.BeginDrag(a, settings); solver.Release(a, Vector2.right * 8f, settings);
        for (int i = 0; i < 120; i++) solver.Step(bodies, 2, 1f / 120f, settings);
        Assert.That(b.Mode, Is.EqualTo(Mode.Autonomous)); Assert.That(b.Position, Is.EqualTo(initial));
        Assert.That(b.Velocity, Is.EqualTo(Vector2.zero));
        a.Position = new Vector2(-3f, .85f); a.Mode = Mode.Autonomous; b.Eligible = true;
        solver.BeginDrag(a, settings); solver.Release(a, Vector2.right * 8f, settings);
        bool transferred = false;
        for (int i = 0; i < 120; i++) { solver.Step(bodies, 2, 1f / 120f, settings); transferred |= b.Velocity.x > .3f; }
        Assert.That(transferred, Is.True); Assert.That(b.Radius, Is.EqualTo(.8f)); Assert.That(b.Mass, Is.EqualTo(2f));
    }

    [Test]
    public void TurningOffPhysicsAlsoClearsTheLimitedSquashOnAnAutonomousCapReceiver()
    {
        var settings = SolverSettings(); settings.MaximumPhysicalFairies = 1;
        var solver = new EnhancementFairyPhysicsSolver();
        var a = MakeBody(1, new Vector2(-2f, .85f)); var b = MakeBody(2, new Vector2(-.65f, .85f));
        var bodies = new[] { a, b }; solver.BeginDrag(a, settings); solver.Release(a, Vector2.right * 8f, settings);
        bool limitedSquash = false;
        for (int frame = 0; frame < 120 && !limitedSquash; frame++)
        { solver.Step(bodies, 2, 1f / 120f, settings); limitedSquash = b.Squash > .01f; }
        Assert.That(limitedSquash, Is.True); Assert.That(b.Mode, Is.EqualTo(Mode.Autonomous));
        settings.Enabled = false; solver.Step(bodies, 2, 1f / 60f, settings);
        Assert.That(a.Squash, Is.Zero); Assert.That(b.Squash, Is.Zero); Assert.That(b.Angle, Is.Zero);
        Assert.That(b.Velocity, Is.EqualTo(Vector2.zero));
    }

    [TestCase("cancel")] [TestCase("disabled")] [TestCase("toggle-off")]
    public void ThirtyRepeatedInterruptionsRestoreScaleRotationGroundAndZeroVelocity(string reason)
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var body = MakeBody(1, new Vector2(0f, .85f)); var bodies = new[] { body };
        for (int cycle = 0; cycle < 30; cycle++)
        {
            settings.Enabled = body.Enabled = true;
            solver.BeginDrag(body, settings); solver.SetDragTarget(body, new Vector2(2f, 7f), settings);
            for (int frame = 0; frame < 24; frame++) solver.Step(bodies, 1, 1f / 120f, settings);
            solver.Release(body, Vector2.one * 20f, settings);
            if (reason == "cancel") solver.Cancel(body, settings);
            else { if (reason == "disabled") body.Enabled = false; else settings.Enabled = false; solver.Step(bodies, 1, 1f / 60f, settings); }
            AssertRestored(body, settings);
        }
    }

    [Test]
    public void DragIsSmoothClampedAndCannotKeepAHouseReturnBlockedIndefinitely()
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver();
        var body = MakeBody(1, new Vector2(0f, .85f)); var bodies = new[] { body }; Vector2 start = body.Position;
        solver.BeginDrag(body, settings); solver.SetDragTarget(body, new Vector2(1000f, 1000f), settings);
        solver.Step(bodies, 1, 1f / 120f, settings);
        Assert.That(Vector2.Distance(start, body.Position), Is.InRange(.01f, 3f), "No instantaneous pointer teleport.");
        for (int i = 0; i < 2100; i++) { solver.Step(bodies, 1, 1f / 120f, settings); AssertFinite(body, settings); }
        Assert.That(body.DragExpired, Is.True); AssertRestored(body, settings);
    }

    [Test]
    public void TwentyFourDisplayedBodiesWithFivePhysicalUseReusedStorageWithoutPerStepAllocations()
    {
        var settings = SolverSettings(); settings.Bounds = new Rect(-20f, 0f, 40f, 12f);
        var solver = new EnhancementFairyPhysicsSolver(); var bodies = new Body[24];
        for (int i = 0; i < bodies.Length; i++) bodies[i] = MakeBody(i, new Vector2(-16f + i * 1.3f, .85f));
        for (int i = 0; i < 5; i++) { bodies[i].Mode = Mode.Physics; bodies[i].Velocity = new Vector2(2f, 3f); }
        for (int i = 0; i < 120; i++) solver.Step(bodies, bodies.Length, 1f / 60f, settings);
        var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        var allocated = allocationMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
        var stopwatch = new System.Diagnostics.Stopwatch(); long before = allocated?.Invoke() ?? -1;
        stopwatch.Start();
        for (int frame = 0; frame < 1800; frame++)
        {
            for (int i = 0; i < 5; i++)
                if (bodies[i].Mode == Mode.Autonomous)
                { bodies[i].Mode = Mode.Physics; bodies[i].Age = 0f; bodies[i].Velocity = new Vector2(2f, 3f); }
            solver.Step(bodies, bodies.Length, 1f / 60f, settings);
        }
        stopwatch.Stop(); long bytes = allocated == null ? -1 : allocated() - before;
        if (allocated != null) Assert.That(bytes, Is.Zero, "Only production Step was measured after warm-up; body/array allocation happens before this region.");
        Assert.That(solver.CountActive(bodies, bodies.Length), Is.LessThanOrEqualTo(5));
        TestContext.WriteLine("PHYSICS_SOLVER_EDITOR_BENCH displayed=24; frames=1800; wallMs=" + stopwatch.Elapsed.TotalMilliseconds
            + "; managedBytes=" + bytes + "; This is an Editor CPU microbenchmark, not GameView or mobile performance.");
    }

    [Test]
    public void InspectorInvalidRangesAreClampedAndDpiThresholdRetainsAMinimum()
    {
        var settings = SolverSettings();
        Assert.That(settings.GetDragThreshold(0f), Is.EqualTo(12f));
        Assert.That(settings.GetDragThreshold(320f), Is.EqualTo(24f));
        settings.Bounds = new Rect(float.NaN, float.NegativeInfinity, -3f, float.PositiveInfinity);
        settings.MaxThrowSpeed = float.NaN; settings.Gravity = float.PositiveInfinity;
        settings.FloorRestitution = 4f; settings.WallRestitution = -2f;
        settings.MaximumPhysicalFairies = 100; settings.AngularReactionLimit = float.NaN;
        settings.DragThresholdPixels = float.NaN; settings.DragReferenceDpi = 0f;
        Assert.That(settings.SafeMaximumPhysicalFairies, Is.EqualTo(5));
        Assert.That(settings.SafeFloorRestitution, Is.EqualTo(.9f)); Assert.That(settings.SafeWallRestitution, Is.Zero);
        Assert.That(settings.SafeBounds.width, Is.GreaterThanOrEqualTo(3f));
        Assert.That(EnhancementFairyPhysicsSolver.Finite(settings.SafeBounds.position), Is.True);
        Assert.That(settings.GetDragThreshold(float.NaN), Is.GreaterThanOrEqualTo(3f));
        var body = MakeBody(1, Vector2.zero); var solver = new EnhancementFairyPhysicsSolver(); var bodies = new[] { body };
        solver.BeginDrag(body, settings); solver.Release(body, Vector2.one * 1000f, settings);
        for (int i = 0; i < 800; i++) solver.Step(bodies, 1, 1f / 120f, settings);
        AssertRestored(body, settings);
    }

    [TestCase(-1)] [TestCase(7)]
    public void Stage1_TwentyShortTapsUseRuntimeRegistrationAndOpenExactlyTwentyExistingCards(int pointer)
    {
        using (var f = new StageFixture())
        {
            Assert.That(EnhancementFairyStage1Host.Get(f.Host.Controller, "_worldPointerGestureHandler"), Is.SameAs(f.Host.Habitat),
                "Registered by production Floor3.Start/InitializeRuntime, without a test-only gesture registration.");
            int cards = 0, pool = f.Host.Habitat.PooledObjectCount;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                Vector2 point = f.Place(); f.Input(pointer, point, TouchPhase.Began);
                Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(f.Id));
                Assert.That(f.Host.Habitat.IsItemCardPending, Is.False);
                f.Input(pointer, point + Vector2.one, TouchPhase.Moved, .04f);
                f.Input(pointer, point, TouchPhase.Ended, .03f);
                Assert.That(f.Host.Habitat.IsItemCardPending, Is.True);
                f.Advance(.3f); Assert.That(f.Host.Habitat.IsItemCardOpen, Is.True); cards++;
                f.Host.Habitat.CloseItemCard(); Assert.That(f.Host.Controller.IsWorldInputBlocked, Is.False);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
            }
            Assert.That(cards, Is.EqualTo(20)); Assert.That(f.Sound.Plays.Count, Is.EqualTo(20));
            Assert.That(f.Host.Habitat.PooledObjectCount, Is.EqualTo(pool)); f.AssertOwnership();
        }
    }

    [TestCase(-1)] [TestCase(7)]
    public void Stage1_TwentyDragsAndLateReleasesOpenNoCardsMoveNoCameraAndResumeTheSameBrain(int pointer)
    {
        using (var f = new StageFixture())
        {
            Vector3 camera = f.Host.Camera.transform.position; var brain = f.Brain; int pool = f.Host.Habitat.PooledObjectCount;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                Vector2 point = f.Place(); Vector2 end = point + new Vector2(90f, 110f);
                f.Input(pointer, point, TouchPhase.Began); f.Input(pointer, end, TouchPhase.Moved, .06f); f.Advance(.15f);
                Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(f.Id));
                Assert.That(f.Host.Controller.IsCameraMoving, Is.False); Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                Assert.That(f.Brain.GroundPosition.y, Is.EqualTo(f.Settings.GroundArea.y).Within(.0001f));
                f.Input(pointer, end, TouchPhase.Ended, .02f); f.Input(pointer, end, TouchPhase.Ended, .01f);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(1));
                f.Advance(f.Settings.Physics.SafeMaximumPhysicalDuration + .2f);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero); Assert.That(f.Brain, Is.SameAs(brain));
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
                Assert.That(f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body), Is.True); AssertRestored(body, f.Settings.Physics);
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
            }
            Assert.That(f.Sound.Plays, Is.Empty, "Dragging never requests the short-tap bubble.");
            Assert.That(f.Host.Habitat.PooledObjectCount, Is.EqualTo(pool)); f.AssertOwnership();
        }
    }

    [TestCase(30, 0f)] [TestCase(60, 0f)] [TestCase(120, 0f)] [TestCase(240, 0f)] [TestCase(1000, 0f)]
    [TestCase(30, .03f)] [TestCase(60, .03f)] [TestCase(120, .03f)] [TestCase(240, .03f)] [TestCase(1000, .03f)]
    [TestCase(30, .07f)] [TestCase(60, .07f)] [TestCase(120, .07f)] [TestCase(240, .07f)] [TestCase(1000, .07f)]
    [TestCase(30, .15f)] [TestCase(60, .15f)] [TestCase(120, .15f)] [TestCase(240, .15f)] [TestCase(1000, .15f)]
    public void Stage1_FrameFedFlickRetainsHorizontalThrowAcrossHighRatesAndShortStationaryTails(int fps, float tail)
    {
        using (var f = new StageFixture())
        {
            Vector2 point = f.Place(); Vector3 camera = f.Host.Camera.transform.position;
            Vector3 initial = f.Host.Floor.transform.InverseTransformPoint(f.Sprite.bounds.center);
            f.Input(-1, point, TouchPhase.Began);
            void Feed(Vector3 from, Vector3 to, float seconds, int rate, TouchPhase phase)
            {
                float elapsed = 0f;
                while (elapsed < seconds - .000001f)
                {
                    float dt = Mathf.Min(1f / rate, seconds - elapsed); elapsed += dt;
                    Vector3 local = Vector3.Lerp(from, to, elapsed / seconds);
                    point = f.Host.Camera.WorldToScreenPoint(f.Host.Floor.transform.TransformPoint(local));
                    f.InputFrame(-1, point, phase, dt);
                }
            }
            Vector3 raised = initial + Vector3.up * 4f;
            Feed(initial, raised, .2f, 60, TouchPhase.Moved);
            Feed(raised, raised, .16f, 60, TouchPhase.Moved);
            const float rawSpeed = 8f, flickSeconds = .24f;
            Vector3 end = raised + Vector3.right * (rawSpeed * flickSeconds);
            Feed(raised, end, flickSeconds, fps, TouchPhase.Moved);
            Feed(end, end, tail, fps, fps >= 240 ? TouchPhase.Moved : TouchPhase.Stationary);
            // End shares the final polled frame timestamp, as the real mouse wrapper can do.
            f.Input(-1, point, TouchPhase.Ended, 0f);
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body), Is.True);
            float expected = rawSpeed * Mathf.Clamp01(1f - tail / .12f) * f.Settings.Physics.SafeThrowMultiplier;
            float released = body.Velocity.x, x = body.Position.x;
            Assert.That(body.Mode, Is.EqualTo(Mode.Physics));
            Assert.That(released, Is.EqualTo(expected).Within(.15f), "Frame polling must preserve the same 120ms trajectory window at every rate.");
            Assert.That(Mathf.Abs(body.Velocity.y), Is.LessThan(.02f));
            Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
            f.Advance(.15f); float travel = body.Position.x - x;
            if (expected > 0f) Assert.That(travel, Is.InRange(expected * .13f, expected * .16f), "A real horizontal throw must travel after release, not only change into Physics mode.");
            else Assert.That(Mathf.Abs(travel), Is.LessThan(.005f), "A deliberate 150ms stop must release as a drop.");
            Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            TestContext.WriteLine("PHYSICS_FRAME_FLICK fps=" + fps + "; stationaryTail=" + tail + "; expectedVx=" + expected
                + "; actualVx=" + released + "; horizontalTravelAfter150ms=" + travel);
        }
    }

    [TestCase("cooldown")] [TestCase("priority")] [TestCase("birth")] [TestCase("returning")]
    [TestCase("cap")] [TestCase("horse")] [TestCase("house")] [TestCase("padding")]
    public void Stage1_UnavailableFairyOrAuthoredPropNeverLeaksADragToTheRaycastableBackground(string reason)
    {
        using (var f = new StageFixture())
        {
            ThirdFloorFairyPlayArea area = null; FairyFurniturePoint horse = null; ThirdFloorFairyHouse house = null;
            Vector2 point = f.Place(); int physical = 0;
            switch (reason)
            {
                case "cooldown": EnhancementFairyStage1Host.Set(f.View, "PhysicsCooldownUntil", float.MaxValue); break;
                case "priority": EnhancementFairyStage1Host.Set(f.View, "PriorityUntil", float.MaxValue); break;
                case "birth": EnhancementFairyStage1Host.Set(f.View, "ArrivalRemaining", 1f); break;
                case "returning":
                    var field = EnhancementFairyStage1Host.Field(f.View.GetType(), "HouseMotion");
                    field.SetValue(f.View, Enum.Parse(field.FieldType, "Returning")); break;
                case "padding":
                    EnhancementFairyStage1Host.Set(f.View, "Scale", (float)EnhancementFairyStage1Host.Get(f.View, "Scale") * .25f);
                    f.Advance(1f / 60f);
                    Vector3 lower = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.min), upper = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.max);
                    point = new Vector2(upper.x + 2f, (lower.y + upper.y) * .5f);
                    Assert.That(upper.x - lower.x + 4f, Is.LessThan(44f), "A small visible sprite exposes the existing minimum-size card target outside its mesh.");
                    break;
                case "cap":
                    var ids = new[] { f.Id }.Concat(f.Host.Catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && item.Sprite != null)
                        .Select(item => item.Id)).Distinct().Take(6).ToArray();
                    Assert.That(ids.Length, Is.EqualTo(6));
                    f.Host.Restore(ids.Select(id => new KeyValuePair<string, int>(id, 1))); point = f.Place();
                    for (int i = 0; i < f.Host.Habitat.ActiveCount; i++)
                        if (f.Host.Habitat.GetActiveBrain(i).ItemId != f.Id)
                            f.Host.Habitat.GetActiveBrain(i).PlaceForBirth(new Vector2(-25f, f.Settings.GroundArea.y));
                    f.Advance(1f / 60f);
                    foreach (string id in ids.Where(id => id != f.Id))
                    {
                        Assert.That(f.Host.Habitat.TryGetPhysicsBody(id, out Body other), Is.True);
                        other.Mode = Mode.Physics; other.Enabled = true;
                    }
                    physical = 5; break;
                case "horse":
                    area = f.Host.SceneComponents<ThirdFloorFairyPlayArea>().Single(value => value.isActiveAndEnabled);
                    EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat");
                    Assert.That(area.IsRegistered, Is.True);
                    horse = area.Points.Single(value => value.Kind == FairyFurnitureKind.RockingHorse);
                    AssertVisible(f.Host.Camera, horse.TouchCollider.bounds.center);
                    point = f.Host.Camera.WorldToScreenPoint(horse.TouchCollider.bounds.center); break;
                case "house":
                    house = f.Host.SceneComponents<ThirdFloorFairyHouse>().Single(value => value.isActiveAndEnabled);
                    // Replay this prop's normal subscription step against its original serialized references.
                    // Application.isPlaying is false in the account-free Editor host, so Start cannot poll it.
                    EnhancementFairyStage1Host.Set(house, "_habitat", f.Host.Habitat);
                    EnhancementFairyStage1Host.Set(house, "_cameraController", f.Host.Controller);
                    EnhancementFairyStage1Host.Set(house, "_worldCamera", f.Host.Camera);
                    EnhancementFairyStage1Host.Invoke(house, "RegisterAndSubscribe");
                    AssertVisible(f.Host.Camera, house.TouchCollider.bounds.center);
                    point = f.Host.Camera.WorldToScreenPoint(house.TouchCollider.bounds.center); break;
            }
            Assert.That(f.Host.Habitat.BlocksCameraDrag(point), Is.True);
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
            {
                Vector3 camera = f.Host.Camera.transform.position;
                f.Input(7, point, TouchPhase.Began);
                Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False, "This is tap-only reservation, not an eligible physical drag.");
                Assert.That((bool)EnhancementFairyStage1Host.Get(f.Host.Controller, "_isDragging"), Is.False);
                f.Input(7, point + Vector2.left * 100f, TouchPhase.Moved, .05f);
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                Assert.That((bool)EnhancementFairyStage1Host.Get(f.Host.Controller, "_isDraggingEnabled"), Is.False);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(physical));
                f.Input(7, point + Vector2.left * 100f, TouchPhase.Ended);
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
                if (horse != null) Assert.That(area.IsTouchAnimating(horse), Is.False);
                if (house != null) Assert.That(house.IsShaking, Is.False);
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                f.Input(7, point, TouchPhase.Began); f.Input(7, point, TouchPhase.Ended);
                if (reason == "cooldown" || reason == "priority" || reason == "cap" || reason == "padding")
                    Assert.That(f.Host.Habitat.IsItemCardPending, Is.True, "A short tap still reaches the existing fairy card handler.");
                if (horse != null) Assert.That(area.IsTouchAnimating(horse), Is.True);
                if (house != null) Assert.That(house.IsShaking, Is.True);
            }
        }
    }

    [TestCase("other-pointer-end")] [TestCase("owner-cancel")]
    public void Stage1_TapOnlyReservationCancelsWithoutALateCardAndAllowsANewTap(string reason)
    {
        using (var f = new StageFixture())
        {
            Vector2 point = f.Place(); Vector3 camera = f.Host.Camera.transform.position;
            EnhancementFairyStage1Host.Set(f.View, "PhysicsCooldownUntil", float.MaxValue);
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
            {
                f.Input(7, point, TouchPhase.Began);
                Assert.That((bool)EnhancementFairyStage1Host.Get(f.Host.Controller, "_isDragging"), Is.False);
                if (reason == "other-pointer-end") f.Input(8, point, TouchPhase.Ended);
                else f.Host.Controller.CancelWorldPointerGesture(f.Host.Habitat);
                f.Input(7, point, TouchPhase.Ended); f.Advance(.3f);
                Assert.That(f.Host.Habitat.IsItemCardPending || f.Host.Habitat.IsItemCardOpen, Is.False,
                    "A canceled tap-only reservation must consume releases from either pointer.");
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                f.Input(7, point, TouchPhase.Began); f.Input(7, point, TouchPhase.Ended); f.Advance(.3f);
                Assert.That(f.Host.Habitat.IsItemCardOpen, Is.True, "A fresh complete short tap must work after cancellation.");
                f.Host.Habitat.CloseItemCard();
            }
        }
    }

    [TestCase(-1)] [TestCase(7)]
    public void Stage1_TwentyBackgroundDragsKeepTheExistingCameraPathAndNeverMoveAFairy(int pointer)
    {
        using (var f = new StageFixture())
        using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host))
        {
            Vector2 fairy = f.Brain.GroundPosition;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                Vector3 camera = f.Host.Camera.transform.position; Vector2 point = background.ScreenPosition;
                Vector2 moved = point + Vector2.left * Mathf.Max(30f, FairyBirthInputTests.DragThreshold(f.Host) * 2f);
                f.Input(pointer, point, TouchPhase.Began); f.Input(pointer, moved, TouchPhase.Moved, .05f);
                Assert.That(EnhancementFairyStage1Host.Get(f.Host.Controller, "_isDraggingEnabled"), Is.True);
                Assert.That(f.Host.Camera.transform.position, Is.Not.EqualTo(camera));
                Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.Null.Or.Empty);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero); Assert.That(f.Brain.GroundPosition, Is.EqualTo(fairy));
                f.Input(pointer, point, TouchPhase.Moved, .05f); f.Input(pointer, point, TouchPhase.Ended, .01f);
                if (f.Host.Controller.IsCameraMoving) f.Host.CompleteCameraTween();
                Assert.That(Vector3.Distance(f.Host.Camera.transform.position, camera), Is.LessThan(.0001f));
            }
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False); Assert.That(f.Sound.Plays, Is.Empty);
        }
    }

    [TestCase("cancel")] [TestCase("multitouch")] [TestCase("input-block")]
    [TestCase("floor")] [TestCase("pause")] [TestCase("off")]
    [TestCase("disable")] [TestCase("pool-return")]
    public void Stage1_InterruptedDragCleansInputAndPhysicalPoseWithoutThrowOrLatePopup(string reason)
    {
        using (var f = new StageFixture())
        {
            Vector2 point = f.Place(); Vector2 end = point + new Vector2(80f, 120f);
            f.Input(7, point, TouchPhase.Began); f.Input(7, end, TouchPhase.Moved, .05f); f.Advance(.1f);
            Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(f.Id));
            switch (reason)
            {
                case "cancel": f.Input(7, end, TouchPhase.Canceled); break;
                case "multitouch": f.Input(8, end, TouchPhase.Began); break;
                case "input-block": f.Host.Controller.SetWorldInputBlocked(f, true); break;
                case "floor": f.Host.Move(ERestaurantFloorType.Floor2); break;
                case "pause": EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "OnApplicationPause", true); break;
                case "off": f.Settings.Physics.Enabled = false; break;
                case "disable": EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "OnDisable"); break;
                case "pool-return": f.Host.Restore(Array.Empty<KeyValuePair<string, int>>()); break;
            }
            f.Advance(.05f); f.Input(7, end, TouchPhase.Ended); f.Advance(.3f);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
            Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False);
            Assert.That(f.Host.Habitat.PendingGestureItemId, Is.Null.Or.Empty);
            Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.Null.Or.Empty);
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            Assert.That(f.Sound.Plays, Is.Empty);
            if (f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body)) AssertRestored(body, f.Settings.Physics);
            f.Host.Controller.SetWorldInputBlocked(f, false);
        }
    }

    [TestCase("Emerging")] [TestCase("Returning")] [TestCase("Fading")]
    [TestCase("birth")] [TestCase("priority")]
    public void Stage1_ProtectedPresentationsNeverEnterAPhysicalGesture(string state)
    {
        using (var f = new StageFixture())
        {
            Vector2 point = f.Place(); object view = f.View;
            if (state == "priority") EnhancementFairyStage1Host.Set(view, "PriorityUntil", float.MaxValue);
            else if (state == "birth") EnhancementFairyStage1Host.Set(view, "ArrivalRemaining", 1f);
            else
            {
                var field = EnhancementFairyStage1Host.Field(view.GetType(), "HouseMotion");
                field.SetValue(view, Enum.Parse(field.FieldType, state));
            }
            f.Input(7, point, TouchPhase.Began); f.Input(7, point + Vector2.up * 150f, TouchPhase.Moved, .05f);
            Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.Null.Or.Empty);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero); Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False);
            f.Input(7, point, TouchPhase.Canceled); Assert.That(f.Sound.Plays, Is.Empty);
        }
    }

    [Test]
    public void Stage1_PhysicsOffRestoresOldShortTapAndBackgroundCameraDrag()
    {
        using (var f = new StageFixture())
        {
            f.Settings.Physics.Enabled = false; Vector2 point = f.Place();
            f.Input(-1, point, TouchPhase.Began); Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False);
            f.Input(-1, point, TouchPhase.Ended); f.Advance(.3f);
            Assert.That(f.Host.Habitat.IsItemCardOpen, Is.True); f.Host.Habitat.CloseItemCard();
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host))
            {
                point = background.ScreenPosition;
                f.Input(-1, point, TouchPhase.Began); f.Input(-1, point + Vector2.left * 40f, TouchPhase.Moved, .05f);
                Assert.That(f.Host.Controller.IsCameraMoving, Is.True); Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
                f.Input(-1, point, TouchPhase.Moved); f.Input(-1, point, TouchPhase.Ended);
                if (f.Host.Controller.IsCameraMoving) f.Host.CompleteCameraTween();
            }
        }
    }

    [Test]
    public void Stage1_PhysicsOffClearsLimitedAutonomousCollisionSquash()
    {
        using (var f = new StageFixture())
        {
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body), Is.True);
            body.Squash = .1f; // The solver's cap-reached fallback tested above leaves the recipient autonomous.
            Assert.That(body.Mode, Is.EqualTo(Mode.Autonomous));
            f.Settings.Physics.Enabled = false; f.Advance(.05f);
            Assert.That(body.Squash, Is.Zero); Assert.That(body.Angle, Is.Zero); Assert.That(body.Velocity, Is.EqualTo(Vector2.zero));
        }
    }

    [Test]
    public void Stage1_RightMapRoomFairyKeepsShortTapAndSupportsThrowThenAnUnchangedAiHandoff()
    {
        using (var f = new StageFixture())
        {
            f.Host.Move(RestaurantType.Hall);
            f.Brain.PlaceForBirth(new Vector2(0f, f.Settings.GroundArea.y)); f.Advance(.05f);
            AssertVisible(f.Host.Camera, f.Sprite.bounds.center);
            Vector2 point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
            f.Input(7, point, TouchPhase.Began);
            Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.True);
            Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(f.Id));
            f.Input(7, point, TouchPhase.Ended); f.Advance(.3f);
            Assert.That(f.Host.Habitat.IsItemCardOpen, Is.True); Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
            f.Host.Habitat.CloseItemCard(); f.Advance(.05f);
            Vector3 camera = f.Host.Camera.transform.position; var brain = f.Brain;
            Body body = ThrowVisibleFairy(f, f.Id);
            Assert.That(body.Velocity.x, Is.GreaterThan(1f));
            float x = body.Position.x; f.Advance(.15f);
            Assert.That(body.Position.x - x, Is.GreaterThan(.2f));
            bool handedOff = false;
            for (int frame = 0; frame < 400; frame++)
            {
                bool physical = body.Mode != Mode.Autonomous;
                f.Advance(1f / 60f);
                if (physical && body.Mode == Mode.Autonomous)
                {
                    handedOff = true;
                    Assert.That(brain.GroundPosition.x, Is.EqualTo(body.Position.x).Within(.001f), "Right-room AI must resume at its final physical x without clamping into the left room.");
                }
            }
            Assert.That(handedOff, Is.True); AssertRestored(body, f.Settings.Physics);
            Assert.That(f.Brain, Is.SameAs(brain)); Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
            f.AssertOwnership();
        }
    }

    [TestCase("house")] [TestCase("horse")]
    public void Stage1_DirectFairySpriteInFrontOfAnAuthoredPropOwnsTheDrag(string prop)
    {
        using (var f = new StageFixture())
        {
            ThirdFloorFairyPlayArea area = null; FairyFurniturePoint horse = null; ThirdFloorFairyHouse house = null;
            Collider2D collider;
            if (prop == "horse")
            {
                area = f.Host.SceneComponents<ThirdFloorFairyPlayArea>().Single(value => value.isActiveAndEnabled);
                EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat");
                Assert.That(area.IsRegistered, Is.True);
                horse = area.Points.Single(value => value.Kind == FairyFurnitureKind.RockingHorse);
                collider = horse.TouchCollider;
            }
            else
            {
                house = f.Host.SceneComponents<ThirdFloorFairyHouse>().Single(value => value.isActiveAndEnabled);
                EnhancementFairyStage1Host.Set(house, "_habitat", f.Host.Habitat);
                EnhancementFairyStage1Host.Set(house, "_cameraController", f.Host.Controller);
                EnhancementFairyStage1Host.Set(house, "_worldCamera", f.Host.Camera);
                EnhancementFairyStage1Host.Invoke(house, "RegisterAndSubscribe");
                collider = house.TouchCollider;
            }
            float localX = f.Host.Floor.transform.InverseTransformPoint(collider.bounds.center).x;
            f.Brain.PlaceForBirth(new Vector2(localX, f.Settings.GroundArea.y)); f.Advance(.05f);
            Vector2 point = DirectFairyPropOverlapPoint(f, collider);
            Vector3 camera = f.Host.Camera.transform.position;
            Body body = ThrowVisibleFairy(f, f.Id, point);
            Assert.That(body.Mode, Is.EqualTo(Mode.Physics));
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(1));
            Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            if (horse != null) Assert.That(area.IsTouchAnimating(horse), Is.False, "Dragging the visible fairy must not press the horse behind it.");
            if (house != null) Assert.That(house.IsShaking, Is.False, "Dragging the visible fairy must not shake or summon from the house behind it.");
            f.AssertOwnership();
        }
    }

    [TestCase(RestaurantType.Kitchen)] [TestCase(RestaurantType.Hall)]
    public void Stage1_ThreeSequentialPickupsKeepEarlierFairiesAirborneAndNeverTakeTheCamera(RestaurantType room)
    {
        using (var f = new StageFixture())
        {
            var ids = new[] { f.Id }.Concat(f.Host.Catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && item.Sprite != null)
                .Select(item => item.Id)).Distinct().Take(3).ToArray();
            Assert.That(ids.Length, Is.EqualTo(3));
            var ownership = ids.Select(id => new KeyValuePair<string, int>(id, 1)).ToArray();
            f.Host.Restore(ownership); f.Host.Move(room); f.Advance(.05f);
            float[] positions = room == RestaurantType.Kitchen ? new[] { -46f, -35f, -24f } : new[] { -12f, -2f, 7f };
            for (int index = 0; index < f.Host.Habitat.ActiveCount; index++)
            {
                var brain = f.Host.Habitat.GetActiveBrain(index);
                brain.PlaceForBirth(new Vector2(positions[Array.IndexOf(ids, brain.ItemId)], f.Settings.GroundArea.y));
            }
            f.Advance(.05f);
            var bodies = new Body[3]; Vector3 camera = f.Host.Camera.transform.position;
            int pool = f.Host.Habitat.PooledObjectCount;
            for (int index = 0; index < ids.Length; index++)
            {
                float[] ages = bodies.Take(index).Select(body => body.Age).ToArray();
                Vector2[] before = bodies.Take(index).Select(body => body.Position).ToArray();
                bodies[index] = ThrowVisibleFairy(f, ids[index]);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(index + 1));
                for (int earlier = 0; earlier < index; earlier++)
                {
                    Assert.That(bodies[earlier].Mode, Is.EqualTo(Mode.Physics), "Starting another drag must leave the earlier throw active.");
                    Assert.That(bodies[earlier].Age, Is.GreaterThan(ages[earlier]), "Another pickup must not reset an earlier fairy's physics age.");
                    Assert.That(Vector2.Distance(bodies[earlier].Position, before[earlier]), Is.GreaterThan(.05f));
                }
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            }
            Assert.That(f.Sound.Plays, Is.Empty); Assert.That(f.Host.Habitat.PooledObjectCount, Is.EqualTo(pool));
            CollectionAssert.AreEquivalent(ownership, UserInfo.GetGiveGachaItemCountDic());
            f.Advance(f.Settings.Physics.SafeMaximumPhysicalDuration + .2f);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
            TestContext.WriteLine("PHYSICS_SEQUENTIAL_PICKUPS room=" + room + "; accepted=3; previousThrowsContinued=true; cameraDelta="
                + Vector3.Distance(camera, f.Host.Camera.transform.position));
        }
    }

    [Test]
    public void Stage1_PendingFairyRemainsAReservedPickupWhenAnAirborneFairyCrossesItsBody()
    {
        using (var f = new StageFixture())
        {
            string[] ids = PlaceOwnedFairies(f, new[] { -35f, -46f });
            Body incoming = ThrowVisibleFairy(f, ids[1]);
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(ids[0], out Body pending), Is.True);
            // Create a real imminent solver contact, after launching A through the production input path.
            incoming.Position = pending.Position + Vector2.left * (incoming.Radius + pending.Radius + .025f);
            incoming.Velocity = Vector2.right * 6f;
            Vector2 point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
            Vector3 camera = f.Host.Camera.transform.position;
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
            {
                f.Input(-1, point, TouchPhase.Began);
                Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(ids[0]));
                Assert.That(pending.Eligible, Is.False, "Pending input reserves this fairy from collision activation before the movement threshold.");
                f.Advance(.05f);
                Assert.That(Vector2.Distance(incoming.Position, pending.Position), Is.LessThan(incoming.Radius + pending.Radius),
                    "A must actually reach B's collision area during the pending interval.");
                Assert.That(pending.Mode, Is.EqualTo(Mode.Autonomous)); Assert.That(pending.Eligible, Is.False);
                Assert.That(incoming.Mode, Is.EqualTo(Mode.Physics));
                f.Input(-1, point + Vector2.up * 100f, TouchPhase.Moved, .05f);
                Assert.That(pending.Mode, Is.EqualTo(Mode.Dragging));
                Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(ids[0]));
                Assert.That((bool)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_gestureDidDrag"), Is.True);
                Assert.That(incoming.Mode, Is.EqualTo(Mode.Physics));
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(2));
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                f.Input(-1, point + Vector2.up * 100f, TouchPhase.Ended);
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            }
        }
    }

    [Test]
    public void Stage1_PendingPickupReservesTheFifthSlotFromAnUnrelatedRealCollision()
    {
        using (var f = new StageFixture())
        {
            string[] ids = PlaceOwnedFairies(f, new[] { -23f, -46f, -41f, -36f, -31f, -28f });
            var active = new Body[4];
            for (int i = 0; i < active.Length; i++) active[i] = ThrowVisibleFairy(f, ids[i + 1]);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(4));
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(ids[0], out Body pending), Is.True);
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(ids[5], out Body receiver), Is.True);
            active[0].Position = receiver.Position + Vector2.left * (active[0].Radius + receiver.Radius + .025f);
            active[0].Velocity = Vector2.right * 6f;
            Vector2 point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
            Vector3 camera = f.Host.Camera.transform.position;
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
            {
                f.Input(-1, point, TouchPhase.Began);
                Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(ids[0]));
                Assert.That(pending.Eligible, Is.False);
                float age = active[0].Age; f.Advance(.05f);
                Assert.That(receiver.Squash, Is.GreaterThan(0f), "The unreserved fairy must receive an actual capped contact, not merely remain far away.");
                Assert.That(receiver.Mode, Is.EqualTo(Mode.Autonomous));
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(4), "Collision propagation must leave one slot reserved for the pending pickup.");
                Assert.That(active[0].Age, Is.GreaterThan(age));
                f.Input(-1, point + Vector2.up * 100f, TouchPhase.Moved, .05f); f.Advance(1f / 60f);
                Assert.That(pending.Mode, Is.EqualTo(Mode.Dragging));
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(5));
                foreach (Body existing in active) Assert.That(existing.Mode, Is.EqualTo(Mode.Physics), "Reserving a collision slot must never terminate an existing physical fairy.");
                Assert.That(receiver.Mode, Is.EqualTo(Mode.Autonomous));
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                f.Input(-1, point + Vector2.up * 100f, TouchPhase.Ended);
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            }
        }
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void Stage1_PhysicalRenderPausesBrainThenResumesWithoutGroundOrScaleDrift(int fps)
    {
        using (var f = new StageFixture())
        {
            Vector2 point = f.Place(); var brain = f.Brain; float aiTime = brain.ElapsedSeconds;
            f.Input(7, point, TouchPhase.Began); f.Input(7, point + new Vector2(110f, 160f), TouchPhase.Moved, .05f);
            f.Advance(.2f); Assert.That(brain.ElapsedSeconds, Is.EqualTo(aiTime));
            f.Input(7, point + new Vector2(150f, 180f), TouchPhase.Ended, .03f);
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body), Is.True);
            float previousX = body.Position.x; bool transitioned = false;
            for (int frame = 0; frame < fps * 7; frame++)
            {
                bool wasPhysical = body.Mode != Mode.Autonomous;
                f.Host.Habitat.AdvancePreview(1f / fps);
                Assert.That(brain.GroundPosition.y, Is.EqualTo(f.Settings.GroundArea.y).Within(.0001f));
                Assert.That(EnhancementFairyPhysicsSolver.Finite((Vector2)f.Sprite.transform.position), Is.True);
                if (wasPhysical && body.Mode == Mode.Autonomous)
                {
                    transitioned = true;
                    Assert.That(Mathf.Abs(brain.GroundPosition.x - previousX), Is.LessThan(.15f), "AI handoff must use the final physical ground point.");
                    float scale = (float)EnhancementFairyStage1Host.Get(f.View, "Scale");
                    Assert.That(f.Sprite.transform.localScale.x, Is.EqualTo(scale).Within(.001f));
                    Assert.That(f.Sprite.transform.localScale.y, Is.EqualTo(scale).Within(.001f));
                }
                previousX = body.Position.x;
            }
            Assert.That(transitioned, Is.True); Assert.That(brain.ElapsedSeconds, Is.GreaterThan(aiTime));
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
        }
    }

    [TestCase("left-wall-cushion", RestaurantType.Kitchen, -52f, 0f, 60)]
    [TestCase("left-horse", RestaurantType.Kitchen, -44f, 0f, 60)]
    [TestCase("left-house-rug", RestaurantType.Kitchen, -36.65f, 0f, 60)]
    [TestCase("left-box", RestaurantType.Kitchen, -23.5f, 0f, 60)]
    [TestCase("right-connection", RestaurantType.Hall, -18.5f, 0f, 60)]
    [TestCase("right-map", RestaurantType.Hall, -2.8f, 0f, 60)]
    [TestCase("right-high-shelf", RestaurantType.Hall, 6f, 12f, 60)]
    [TestCase("right-door", RestaurantType.Hall, 12f, 0f, 60)]
    [TestCase("right-wall", RestaurantType.Hall, 13f, 0f, 60)]
    [TestCase("left-wall-cushion", RestaurantType.Kitchen, -52f, 0f, 30)]
    [TestCase("right-wall", RestaurantType.Hall, 13f, 0f, 30)]
    [TestCase("right-high-shelf", RestaurantType.Hall, 6f, 12f, 30)]
    public void Stage1_InteriorLocationsKeepShortTapAndDragThenWalkBackWithoutBoundarySnaps(
        string location, RestaurantType room, float x, float height, int fps)
    {
        using (var f = new StageFixture())
        {
            Rect aiBounds = f.Settings.SafeGroundArea, physicalBounds = f.Settings.Physics.SafeBounds;
            Assert.That(aiBounds.xMin, Is.EqualTo(-50f)); Assert.That(aiBounds.xMax, Is.EqualTo(10f));
            Assert.That(physicalBounds.xMin, Is.EqualTo(-53.2f).Within(.001f));
            Assert.That(physicalBounds.xMax, Is.EqualTo(13.65f).Within(.001f));
            Assert.That(physicalBounds.yMax, Is.EqualTo(22.4f).Within(.001f));
            f.Host.Move(room); var brain = f.Brain; Vector3 camera = f.Host.Camera.transform.position;
            // The production presentation placement deliberately accepts the visible room's ends.
            // PlaceForBirth would silently clamp these test cases back into the old AI rectangle.
            brain.PlaceForHouse(new Vector2(x, aiBounds.yMin + height)); f.Advance(.05f);
            if (location == "left-wall-cushion" || location == "right-wall")
            {
                Bounds drawn = FairyMeshBoundsInFloor(f);
                float shift = location == "left-wall-cushion"
                    ? physicalBounds.xMin + .025f - drawn.min.x : physicalBounds.xMax - .025f - drawn.max.x;
                x = brain.GroundPosition.x + shift;
                brain.PlaceForHouse(new Vector2(x, aiBounds.yMin)); f.Advance(1f / 60f);
                Assert.That(x < aiBounds.xMin || x > aiBounds.xMax, Is.True, "This must exercise the previously excluded visible edge.");
            }
            AssertVisible(f.Host.Camera, f.Sprite.bounds.center);
            Vector2 point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
            Assert.That((bool)EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "IsDirectFairyMeshHit", f.View, point), Is.True);
            Assert.That((bool)EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "PhysicsEligible", f.View), Is.True,
                "A normal visible indoor fairy must not be rejected by the conservative capsule margin.");
            f.Input(-1, point, TouchPhase.Began);
            Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(f.Id));
            f.Input(-1, point, TouchPhase.Ended); f.Advance(.3f);
            Assert.That(f.Host.Habitat.IsItemCardOpen, Is.True, location + " short tap opens the existing card.");
            Assert.That(f.Sound.Plays.Count, Is.EqualTo(1), "The existing short-tap bubble is preserved.");
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
            f.Host.Habitat.CloseItemCard();

            brain.PlaceForHouse(new Vector2(x, aiBounds.yMin + height)); f.Advance(.3f);
            point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
            Assert.That(f.Host.Habitat.TryGetPhysicsBody(f.Id, out Body body), Is.True);
            Vector2 start = body.Position; Bounds originalVisual = FairyMeshBoundsInFloor(f);
            Vector2 target = point + Vector2.up * Mathf.Max(18f, f.Settings.Physics.GetDragThreshold(Screen.dpi) + 2f);
            using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
            {
                f.Input(-1, point, TouchPhase.Began);
                Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(f.Id));
                f.Input(-1, target, TouchPhase.Moved, 1f / fps);
                Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(f.Id));
                Assert.That(Vector2.Distance(body.Position, start), Is.LessThan(.001f), "Capture itself must retain the exact current position.");
                Assert.That(Vector3.Distance(FairyMeshBoundsInFloor(f).center, originalVisual.center), Is.LessThan(.001f));
                f.InputFrame(-1, target, TouchPhase.Stationary, 1f / fps);
                float firstStepDistance = Vector2.Distance(FairyMeshBoundsInFloor(f).center, originalVisual.center);
                Assert.That(firstStepDistance, Is.LessThan(1.8f), "The first simulation frame must not jump from a shelf to the floor/old drag ceiling.");
                AssertMeshInsideInterior(FairyMeshBoundsInFloor(f), physicalBounds);
                for (int frame = 0; frame < fps / 2; frame++)
                {
                    f.InputFrame(-1, target, TouchPhase.Stationary, 1f / fps);
                    AssertMeshInsideInterior(FairyMeshBoundsInFloor(f), physicalBounds);
                }
                if (height > 10f)
                {
                    Assert.That(body.Position.y, Is.GreaterThanOrEqualTo(start.y - .01f),
                        "Holding a high indoor fairy must not pull it down to the former ten-unit drag cap.");
                    Assert.That(FairyMeshBoundsInFloor(f).min.y, Is.GreaterThanOrEqualTo(originalVisual.min.y - .02f));
                    target = f.Host.Camera.WorldToScreenPoint(f.Host.Floor.transform.TransformPoint(
                        new Vector3(x, physicalBounds.yMax + 1f, 0f)));
                    for (int frame = 0; frame < fps / 2; frame++)
                    {
                        f.InputFrame(-1, target, frame == 0 ? TouchPhase.Moved : TouchPhase.Stationary, 1f / fps);
                        Assert.That(body.Position.y + body.HalfExtents.y, Is.LessThanOrEqualTo(physicalBounds.yMax + .002f),
                            "Extending capture height must not extend the actual ceiling.");
                        AssertMeshInsideInterior(FairyMeshBoundsInFloor(f), physicalBounds);
                    }
                    Assert.That(body.Position.y + body.HalfExtents.y, Is.EqualTo(physicalBounds.yMax).Within(.015f));
                    Assert.That(FairyMeshBoundsInFloor(f).max.y, Is.LessThanOrEqualTo(physicalBounds.yMax + .015f));
                }
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera));
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
                Vector2 beforeRelease = body.Position;
                f.Input(-1, target, TouchPhase.Ended, 0f);
                Assert.That(body.Mode, Is.EqualTo(Mode.Physics));
                Assert.That(Vector2.Distance(body.Position, beforeRelease), Is.LessThan(.001f), "Release must not reclamp the current pose.");
                Assert.That(body.Velocity.magnitude, Is.LessThan(.001f), "A held pointer is a drop, not an artificial fling.");

                bool handedOff = false; float handoffX = float.NaN, largestAiStep = 0f;
                float maximumWalk = Mathf.Max(.1f, f.Settings.WalkSpeed)
                    * Mathf.Max(.1f, f.Settings.CalmEnergy + 2f * f.Settings.PersonalityEnergyStep);
                for (int frame = 0; frame < fps * 12; frame++)
                {
                    bool wasPhysical = body.Mode != Mode.Autonomous;
                    float previousVisualX = FairyMeshBoundsInFloor(f).center.x;
                    float previousBodyX = body.Position.x, previousAiX = brain.GroundPosition.x;
                    f.Host.Habitat.AdvancePreview(1f / fps);
                    Assert.That(EnhancementFairyPhysicsSolver.Finite(body.Position) && EnhancementFairyPhysicsSolver.Finite(body.Velocity), Is.True);
                    Assert.That(body.Position.x, Is.InRange(physicalBounds.xMin, physicalBounds.xMax));
                    Assert.That(body.Position.y, Is.InRange(physicalBounds.yMin, physicalBounds.yMax));
                    if (wasPhysical || body.Mode != Mode.Autonomous)
                        AssertMeshInsideInterior(FairyMeshBoundsInFloor(f), physicalBounds);
                    if (wasPhysical && body.Mode == Mode.Autonomous)
                    {
                        handedOff = true; handoffX = brain.GroundPosition.x;
                        Assert.That(Mathf.Abs(handoffX - previousBodyX), Is.LessThanOrEqualTo(maximumWalk / fps + .03f),
                            "Finishing outside the old AI bounds must not clamp back to -50 or 10.");
                        Assert.That(Mathf.Abs(FairyMeshBoundsInFloor(f).center.x - previousVisualX), Is.LessThan(.15f));
                        Assert.That(brain.GroundPosition.y, Is.EqualTo(aiBounds.yMin).Within(.001f));
                    }
                    else if (!wasPhysical)
                    {
                        float step = Mathf.Abs(brain.GroundPosition.x - previousAiX);
                        largestAiStep = Mathf.Max(largestAiStep, step);
                        Assert.That(step, Is.LessThanOrEqualTo(maximumWalk / fps + .003f),
                            "The unchanged brain must walk home at its usual speed, including its first post-physics frame.");
                    }
                }
                Assert.That(handedOff, Is.True); Assert.That(f.Brain, Is.SameAs(brain));
                Assert.That(brain.GroundPosition.x, Is.InRange(aiBounds.xMin - .001f, aiBounds.xMax + .001f));
                Assert.That(f.Settings.SafeGroundArea, Is.EqualTo(aiBounds), "The ordinary AI travel rectangle must remain unchanged.");
                AssertRestored(body, f.Settings.Physics);
                Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
                Assert.That(f.Sound.Plays.Count, Is.EqualTo(1), "Dragging adds no card or short-tap bubble.");
                Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera)); f.AssertOwnership();
                TestContext.WriteLine("PHYSICS_INTERIOR location=" + location + "; fps=" + fps + "; groundX=" + x
                    + "; height=" + height + "; firstVisualStep=" + firstStepDistance + "; handoffX=" + handoffX
                    + "; largestAiStep=" + largestAiStep + "; shortTap=1; dragCards=0");
            }
        }
    }

    [Test]
    public void Stage1_TransparentFairyMeshCornerStillLetsTheAuthoredHorseReceiveItsTap()
    {
        using (var f = new StageFixture())
        {
            var area = f.Host.SceneComponents<ThirdFloorFairyPlayArea>().Single(value => value.isActiveAndEnabled);
            EnhancementFairyStage1Host.Invoke(area, "BindToInitializedHabitat");
            var horse = area.Points.Single(value => value.Kind == FairyFurnitureKind.RockingHorse);
            float x = f.Host.Floor.transform.InverseTransformPoint(horse.TouchCollider.bounds.center).x;
            f.Brain.PlaceForHouse(new Vector2(x, f.Settings.SafeGroundArea.yMin)); f.Advance(.05f);
            Bounds bounds = f.Sprite.bounds; Vector2 pixel = Vector2.zero; bool found = false;
            for (int row = 1; row < 20 && !found; row++)
            for (int column = 1; column < 20 && !found; column++)
            {
                Vector3 world = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, column / 20f),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, row / 20f), bounds.center.z);
                pixel = f.Host.Camera.WorldToScreenPoint(world);
                found = horse.TouchCollider.OverlapPoint(world)
                    && !(bool)EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "IsDirectFairyMeshHit", f.View, pixel);
            }
            Assert.That(found, Is.True, "The case must use a real transparent mesh corner overlapping the authored horse collider.");
            Vector3 camera = f.Host.Camera.transform.position;
            f.Input(-1, pixel, TouchPhase.Began);
            Assert.That(f.Host.Habitat.PendingGestureItemId, Is.Null.Or.Empty);
            Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False);
            f.Input(-1, pixel, TouchPhase.Ended);
            Assert.That(area.IsTouchAnimating(horse), Is.True);
            Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
            Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero); Assert.That(f.Sound.Plays, Is.Empty);
            Assert.That(f.Host.Camera.transform.position, Is.EqualTo(camera)); f.AssertOwnership();
        }
    }

    private static Bounds FairyMeshBoundsInFloor(StageFixture f)
    {
        var bounds = new Bounds(); bool first = true;
        foreach (Vector2 vertex in f.Sprite.sprite.vertices)
        {
            Vector3 local = new Vector3(f.Sprite.flipX ? -vertex.x : vertex.x, f.Sprite.flipY ? -vertex.y : vertex.y, 0f);
            Vector3 point = f.Host.Floor.transform.InverseTransformPoint(f.Sprite.transform.TransformPoint(local));
            if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
            else bounds.Encapsulate(point);
        }
        Assert.That(first, Is.False); return bounds;
    }

    private static void AssertMeshInsideInterior(Bounds drawn, Rect room)
    {
        const float edgeTolerance = .03f;
        Assert.That(drawn.min.x, Is.GreaterThanOrEqualTo(room.xMin - edgeTolerance), "The rendered mesh must stay inside the left wall.");
        Assert.That(drawn.max.x, Is.LessThanOrEqualTo(room.xMax + edgeTolerance), "The rendered mesh must stay inside the right wall.");
        Assert.That(drawn.min.y, Is.GreaterThanOrEqualTo(room.yMin - edgeTolerance), "The rendered mesh must not penetrate the floor.");
        Assert.That(drawn.max.y, Is.LessThanOrEqualTo(room.yMax + edgeTolerance), "The rendered mesh must not penetrate the ceiling.");
    }

    [Test]
    public void Stage1_RegrabbingEveryPointEightSecondsCannotPostponeAnAlreadyDueHouseReturnPastTheSessionBound()
    {
        using (var f = new StageFixture())
        {
            var entrance = new GameObject("Owned physics return test entrance");
            entrance.transform.SetParent(f.Host.Floor.transform, false);
            entrance.transform.localPosition = new Vector3(-36.6f, f.Settings.GroundArea.y, 0f);
            var house = new EnhancementFairyHouseSettings();
            try
            {
                var ids = new[] { f.Id }.Concat(f.Host.Catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && item.Sprite != null).Select(item => item.Id)).Distinct().Take(13).ToArray();
                Assert.That(ids.Length, Is.EqualTo(13), "Use real catalog IDs and in-memory ownership, never synthetic production fairies.");
                f.Host.Restore(ids.Select(id => new KeyValuePair<string, int>(id, 1)));
                f.Host.Habitat.RegisterHouse(f, entrance.transform, house);
                AssertVisible(f.Host.Camera, entrance.transform.position);
                Assert.That(f.Host.Habitat.CanUseHouse, Is.True, "The production house gate must accept the visible left-room entrance.");
                Assert.That(f.Host.Habitat.RequestHouseEmergence(f, 1), Is.EqualTo(1)); f.Advance(.8f);
                Assert.That(f.Host.Habitat.ActiveCount, Is.EqualTo(13));
                foreach (object view in (IList)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_views"))
                {
                    var brain = (EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain");
                    if (brain.ItemId != f.Id) brain.PlaceForBirth(new Vector2(0f, f.Settings.GroundArea.y));
                    EnhancementFairyStage1Host.Set(view, "DisplayedAt", float.PositiveInfinity);
                }
                Vector2 point = f.Place(); object selected = f.View;
                float started = (float)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_clock");
                EnhancementFairyStage1Host.Set(selected, "DisplayedAt", started - 30f);
                f.Input(7, point, TouchPhase.Began);
                Vector2 target = f.Host.Camera.WorldToScreenPoint(f.Host.Floor.transform.TransformPoint(new Vector3(-40f, f.Settings.GroundArea.y + 8f, 0f)));
                f.Input(7, target, TouchPhase.Moved, .05f);
                EnhancementFairyStage1Host.Set(f.Host.Habitat, "_houseReturnDueAt", started);
                EnhancementFairyStage1Host.Set(f.Host.Habitat, "_nextHouseReturnAt", started);
                f.Advance(.1f); f.Input(7, target, TouchPhase.Ended);
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.EqualTo(1));
                int regrabs = 0; float elapsed = 0f, finishedElapsed = -1f;
                Action<float> advance = seconds =>
                {
                    for (int frame = 0; frame < Mathf.RoundToInt(seconds * 60f); frame++)
                    {
                        f.Advance(1f / 60f);
                        if (finishedElapsed < 0f && f.Host.Habitat.PhysicalFairyCount == 0)
                            finishedElapsed = (float)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_clock") - started;
                    }
                };
                while (elapsed < 17f && f.Host.Habitat.PhysicalFairyCount > 0)
                {
                    advance(.7f);
                    if (f.Host.Habitat.PhysicalFairyCount == 0) break;
                    point = f.Host.Camera.WorldToScreenPoint(f.Sprite.bounds.center);
                    f.Input(7, point, TouchPhase.Began);
                    Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(f.Id), "The same airborne fairy is actually regrabbed.");
                    f.Input(7, target, TouchPhase.Moved, .02f); regrabs++;
                    advance(.1f); f.Input(7, target, TouchPhase.Ended);
                    elapsed = (float)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_clock") - started;
                }
                elapsed = (float)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_clock") - started;
                float bound = f.Settings.Physics.SafeMaximumDragSeconds + f.Settings.Physics.SafeMaximumPhysicalDuration;
                Assert.That(regrabs, Is.GreaterThanOrEqualTo(15));
                Assert.That(finishedElapsed, Is.InRange(bound - .02f, bound + .04f), "Capture the first finished frame, not the next regrab's observation time.");
                Assert.That(f.Host.Habitat.PhysicalFairyCount, Is.Zero);
                Assert.That(f.Host.Controller.IsWorldPointerGestureActive, Is.False);
                Assert.That(EnhancementFairyStage1Host.Get(selected, "HouseMotion").ToString(), Is.EqualTo("Returning"),
                    "The oldest eligible fairy receives its existing due return immediately after the bounded physical session ends.");
                f.Input(7, target, TouchPhase.Ended); Assert.That(f.Host.Habitat.IsItemCardOpen || f.Host.Habitat.IsItemCardPending, Is.False);
                Assert.That(f.Host.Habitat.HouseHardCap, Is.EqualTo(24)); Assert.That(house.TemporaryPlaySeconds, Is.EqualTo(45f));
                TestContext.WriteLine("PHYSICS_REGRAB_LIMIT regrabs=" + regrabs + "; completedSeconds=" + finishedElapsed + "; observedSeconds=" + elapsed + "; sessionBound=" + bound + "; returning=" + f.Host.Habitat.ReturningHouseCount);
            }
            finally { f.Host.Habitat.UnregisterHouse(f); Object.DestroyImmediate(entrance); }
        }
    }

    private static string[] PlaceOwnedFairies(StageFixture f, float[] positions)
    {
        var ids = new[] { f.Id }.Concat(f.Host.Catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && item.Sprite != null)
            .Select(item => item.Id)).Distinct().Take(positions.Length).ToArray();
        Assert.That(ids.Length, Is.EqualTo(positions.Length));
        f.Host.Restore(ids.Select(id => new KeyValuePair<string, int>(id, 1)));
        for (int index = 0; index < f.Host.Habitat.ActiveCount; index++)
        {
            var brain = f.Host.Habitat.GetActiveBrain(index);
            brain.PlaceForBirth(new Vector2(positions[Array.IndexOf(ids, brain.ItemId)], f.Settings.GroundArea.y));
        }
        // Restore creates new views at Fade = 0. Complete the actual runtime fade before attempting a pickup.
        f.Advance(Mathf.Max(.1f, f.Settings.RotationFadeSeconds) + .05f);
        foreach (string id in ids)
            Assert.That(f.Host.Bodies().Single(value => value.transform.parent.name == "Fairy " + id).color.a,
                Is.GreaterThanOrEqualTo(.99f), "The restored fairy must finish its real visibility fade before the collision fixture begins.");
        return ids;
    }

    private static Body ThrowVisibleFairy(StageFixture f, string id, Vector2? initialPixel = null)
    {
        SpriteRenderer sprite = f.Host.Bodies().Single(value => value.transform.parent.name == "Fairy " + id);
        AssertVisible(f.Host.Camera, sprite.bounds.center);
        Vector2 point = initialPixel ?? (Vector2)f.Host.Camera.WorldToScreenPoint(sprite.bounds.center);
        Assert.That(sprite.color.a, Is.GreaterThanOrEqualTo(.5f), "A fading-in fairy is intentionally ineligible for a pickup.");
        object view = ((IList)EnhancementFairyStage1Host.Get(f.Host.Habitat, "_views")).Cast<object>()
            .Single(value => ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(value, "Brain")).ItemId == id);
        Assert.That((bool)EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "IsDirectFairyMeshHit", view, point), Is.True,
            "The pointer must begin on the intended fairy's actual rendered mesh.");
        Ray ray = f.Host.Camera.ScreenPointToRay(point);
        var plane = new Plane(f.Host.Floor.transform.forward, f.Host.Floor.transform.position);
        Assert.That(plane.Raycast(ray, out float distance), Is.True);
        Vector3 initial = f.Host.Floor.transform.InverseTransformPoint(ray.GetPoint(distance));
        Vector3 raised = initial + Vector3.up * 8f;
        using (var background = new FairyBirthInputTests.OwnedDragBackground(f.Host, point))
        {
            f.Input(-1, point, TouchPhase.Began);
            Assert.That(f.Host.Habitat.PendingGestureItemId, Is.EqualTo(id), "The intended visible fairy must own this press even while others fly or an authored prop is behind it.");
            for (int frame = 1; frame <= 10; frame++)
            {
                Vector3 local = Vector3.Lerp(initial, raised, frame / 10f);
                point = f.Host.Camera.WorldToScreenPoint(f.Host.Floor.transform.TransformPoint(local));
                f.InputFrame(-1, point, TouchPhase.Moved, 1f / 60f);
            }
            for (int frame = 1; frame <= 6; frame++)
            {
                Vector3 local = raised + Vector3.right * (.2f * frame / 6f);
                point = f.Host.Camera.WorldToScreenPoint(f.Host.Floor.transform.TransformPoint(local));
                f.InputFrame(-1, point, TouchPhase.Moved, 1f / 60f);
            }
            Assert.That(f.Host.Habitat.DraggedFairyItemId, Is.EqualTo(id));
            f.Input(-1, point, TouchPhase.Ended, 0f);
        }
        Assert.That(f.Host.Habitat.TryGetPhysicsBody(id, out Body body), Is.True);
        Assert.That(body.Mode, Is.EqualTo(Mode.Physics));
        Assert.That(body.Velocity.x, Is.GreaterThan(1f));
        return body;
    }

    private static Vector2 DirectFairyPropOverlapPoint(StageFixture f, Collider2D collider)
    {
        Bounds bounds = f.Sprite.bounds;
        for (int y = 1; y < 10; y++)
        for (int x = 1; x < 10; x++)
        {
            Vector3 world = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 10f),
                Mathf.Lerp(bounds.min.y, bounds.max.y, y / 10f), bounds.center.z);
            Vector2 pixel = f.Host.Camera.WorldToScreenPoint(world);
            if (collider.OverlapPoint(world) && (bool)EnhancementFairyStage1Host.Invoke(f.Host.Habitat, "IsDirectFairyMeshHit", f.View, pixel))
            {
                AssertVisible(f.Host.Camera, world);
                return pixel;
            }
        }
        Assert.Fail("The authored prop collider and visible fairy mesh must actually overlap before testing input priority.");
        return Vector2.zero;
    }

    private static void AssertVisible(Camera camera, Vector3 worldPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        Assert.That(viewport.z, Is.GreaterThan(0f));
        Assert.That(viewport.x, Is.InRange(0f, 1f), "Input replay must start on the actual visible room.");
        Assert.That(viewport.y, Is.InRange(0f, 1f), "Input replay must start on the actual visible room.");
    }
    private static EnhancementFairyPhysicsSettings SolverSettings() => new EnhancementFairyPhysicsSettings { Bounds = new Rect(-12f, 0f, 24f, 12f) };
    private static Body MakeBody(int id, Vector2 center) => new Body { Id = id, Enabled = true, Eligible = true, Position = center, Radius = .65f,
        HalfExtents = new Vector2(.65f, .85f), Mass = 1f, Mode = Mode.Autonomous };
    private static void AssertFinite(Body body, EnhancementFairyPhysicsSettings settings)
    {
        Assert.That(EnhancementFairyPhysicsSolver.Finite(body.Position) && EnhancementFairyPhysicsSolver.Finite(body.Velocity)
            && EnhancementFairyPhysicsSolver.Finite(body.Angle) && EnhancementFairyPhysicsSolver.Finite(body.Squash), Is.True);
        Assert.That(body.Velocity.magnitude, Is.LessThanOrEqualTo(settings.SafeMaxThrowSpeed + .001f));
        Assert.That(body.Position.y, Is.GreaterThanOrEqualTo(settings.SafeBounds.yMin + body.HalfExtents.y - .002f));
        Assert.That(body.Position.y, Is.LessThanOrEqualTo(settings.SafeBounds.yMax - body.HalfExtents.y + .002f));
        Assert.That(body.Position.x, Is.InRange(settings.SafeBounds.xMin + body.HalfExtents.x - .002f,
            settings.SafeBounds.xMax - body.HalfExtents.x + .002f));
        Assert.That(Mathf.Abs(body.Angle), Is.LessThanOrEqualTo(settings.SafeAngularReactionLimit + .001f));
    }
    private static void AssertRestored(Body body, EnhancementFairyPhysicsSettings settings)
    {
        AssertFinite(body, settings); Assert.That(body.Mode, Is.EqualTo(Mode.Autonomous));
        Assert.That(body.Velocity, Is.EqualTo(Vector2.zero)); Assert.That(body.Angle, Is.Zero); Assert.That(body.Squash, Is.Zero);
        Assert.That(body.Position.y, Is.EqualTo(settings.SafeBounds.yMin + body.HalfExtents.y).Within(.001f));
    }
    private static Vector2[] ThrowTrajectory(int fps)
    {
        var settings = SolverSettings(); var solver = new EnhancementFairyPhysicsSolver(); var body = MakeBody(1, new Vector2(-5f, 6f));
        var bodies = new[] { body }; var samples = new Vector2[12]; solver.BeginDrag(body, settings); solver.Release(body, new Vector2(11f, 4f), settings);
        for (int frame = 1; frame <= fps * 6; frame++)
        { solver.Step(bodies, 1, 1f / fps, settings); AssertFinite(body, settings); if (frame % (fps / 2) == 0) samples[frame / (fps / 2) - 1] = body.Position; }
        return samples;
    }

    private sealed class StageFixture : IDisposable
    {
        internal readonly EnhancementFairyStage1Host Host;
        internal readonly FairyBirthSoundScope Sound;
        internal readonly StaffGachaOfflineFonts Fonts;
        internal readonly EnhancementFairySettings Settings;
        private readonly EnhancementFairySettings _originalSettings;
        private readonly KeyValuePair<string, int>[] _ownership;
        internal readonly string Id;
        private float _pointerTime = 1f;
        internal EnhancementFairyBrain Brain => Host.Habitat.GetActiveBrain(0);
        internal SpriteRenderer Sprite => Host.Bodies().Single(body => body.transform.parent.name == "Fairy " + Id);
        internal object View => ((IList)EnhancementFairyStage1Host.Get(Host.Habitat, "_views")).Cast<object>()
            .Single(view => ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(view, "Brain")).ItemId == Id);

        internal StageFixture()
        {
            Host = new EnhancementFairyStage1Host();
            try
            {
                Sound = new FairyBirthSoundScope(Host); Fonts = new StaffGachaOfflineFonts();
                foreach (var ui in Host.SceneComponents<UIItemGacha>()) { ui.gameObject.SetActive(false); Fonts.BindBeforeActivation(ui.gameObject); }
                _originalSettings = Host.Habitat.Settings; Settings = Object.Instantiate(_originalSettings);
                // Isolate the test's rest timing without changing authored settings or replacing the runtime registration.
                Settings.RotationSeconds = 10000f; Settings.RestMinSeconds = Settings.RestMaxSeconds = 10000f;
                Settings.InitialWaitSeconds = Vector2.one * 10000f;
                EnhancementFairyStage1Host.Set(Host.Habitat, "_settings", Settings);
                Id = Sound.Items[0].Id; _ownership = new[] { new KeyValuePair<string, int>(Id, 1) };
                Host.Restore(_ownership); Sound.StartFloor3(); Host.Move(RestaurantType.Kitchen); Advance(1f / 60f); Place();
            }
            catch { Dispose(); throw; }
        }
        internal Vector2 Place()
        {
            Brain.PlaceForBirth(new Vector2(-40f, Settings.GroundArea.y)); Advance(1f / 60f);
            AssertVisible(Host.Camera, Sprite.bounds.center);
            return Host.Camera.WorldToScreenPoint(Sprite.bounds.center);
        }
        internal void Input(int id, Vector2 position, TouchPhase phase, float seconds = .02f)
        { _pointerTime += seconds; Host.Controller.ProcessPointerInputFrame(id, position, phase, _pointerTime); }
        internal void InputFrame(int id, Vector2 position, TouchPhase phase, float seconds)
        {
            _pointerTime += seconds; Sound.Clock += seconds;
            Host.Controller.ProcessPointerInputFrame(id, position, phase, _pointerTime);
            Host.Habitat.AdvancePreview(seconds);
        }
        internal void Advance(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++)
            { Sound.Clock += 1d / 60d; _pointerTime += 1f / 60f; Host.Habitat.AdvancePreview(1f / 60f); }
        }
        internal void AssertOwnership() => CollectionAssert.AreEquivalent(_ownership, UserInfo.GetGiveGachaItemCountDic());
        public void Dispose()
        {
            try { Sound?.Dispose(); Fonts?.Dispose(); }
            finally
            {
                if (Host != null && Host.Habitat != null && _originalSettings != null) EnhancementFairyStage1Host.Set(Host.Habitat, "_settings", _originalSettings);
                if (Settings != null) Object.DestroyImmediate(Settings); Host?.Dispose();
            }
        }
    }
}
#endif
