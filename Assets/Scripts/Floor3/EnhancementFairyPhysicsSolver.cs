using UnityEngine;

/// <summary>
/// Small deterministic presentation solver. Bodies are allocated once by the habitat; Step allocates nothing.
/// Reuses capsule-ball velocity/gravity/boundary principles, with mass-weighted pair impulses and a room box.
/// </summary>
public sealed class EnhancementFairyPhysicsSolver
{
    public enum BodyMode { Autonomous, Dragging, Physics }

    public sealed class Body
    {
        public int Id;
        public bool Enabled;
        public bool Eligible;
        public BodyMode Mode;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 HalfExtents = new Vector2(.65f, .85f);
        public float Radius = .65f;
        public float Mass = 1f;
        public float Age;
        public int BounceCount;
        public float Angle;
        public float Squash;
        public bool JustActivated;
        public bool Finished;
        public bool DragExpired;
        public bool Settling;
        internal Vector2 DragTarget;
        internal float AngularVelocity;
        internal float QuietSeconds;
        internal Vector2 SettleFrom;
        internal float SettleElapsed;
        internal float SettleAngle;
        internal float SettleSquash;
        internal Vector2 BoundaryEntryCenter;
        internal float BoundaryEntryWeight;
    }

    public const float FixedStepSeconds = 1f / 120f;
    private const float SettleSeconds = .3f;
    private float _accumulator;

    public void Reset() { _accumulator = 0f; }

    public int CountActive(Body[] bodies, int count)
    {
        if (bodies == null) return 0;
        int active = 0;
        for (int i = 0; i < Mathf.Min(count, bodies.Length); i++)
            if (bodies[i] != null && bodies[i].Enabled && bodies[i].Mode != BodyMode.Autonomous) active++;
        return active;
    }

    /// <summary>The habitat checks input eligibility and the active cap before taking ownership.</summary>
    public void BeginDrag(Body body, EnhancementFairyPhysicsSettings settings)
    {
        if (body == null || settings == null || !settings.Enabled || !body.Enabled || !body.Eligible) return;
        Sanitize(body, settings);
        CaptureBoundaryEntry(body);
        body.Mode = BodyMode.Dragging;
        body.Velocity = Vector2.zero;
        body.DragTarget = body.Position;
        body.Age = body.QuietSeconds = body.AngularVelocity = 0f;
        body.BounceCount = 0;
        body.Settling = body.Finished = body.DragExpired = false;
        body.JustActivated = true;
        body.Squash = .045f;
    }

    public void SetDragTarget(Body body, Vector2 target, EnhancementFairyPhysicsSettings settings)
    {
        if (body == null || settings == null || body.Mode != BodyMode.Dragging || !Finite(target)) return;
        body.DragTarget = ClampCenter(target, body, settings, true);
    }

    /// <summary>Accepts raw recent pointer velocity; the configured multiplier and hard speed clamp apply once.</summary>
    public void Release(Body body, Vector2 pointerVelocity, EnhancementFairyPhysicsSettings settings)
    {
        if (body == null || settings == null || body.Mode != BodyMode.Dragging) return;
        body.Mode = BodyMode.Physics;
        body.Velocity = ClampVelocity(Finite(pointerVelocity) ? pointerVelocity * settings.SafeThrowMultiplier : Vector2.zero, settings);
        body.Age = body.QuietSeconds = 0f;
        body.Settling = false;
        body.AngularVelocity = Mathf.Clamp(-body.Velocity.x * 2f, -30f, 30f);
    }

    /// <summary>Screen/pool cancellation never adds throw energy. The habitat resumes its original brain at this ground point.</summary>
    public void Cancel(Body body, EnhancementFairyPhysicsSettings settings)
    {
        if (body == null || settings == null) return;
        Sanitize(body, settings);
        Finish(body, settings);
    }

    public void Step(Body[] bodies, int count, float deltaTime, EnhancementFairyPhysicsSettings settings,
        int reservedCollisionSlots = 0)
    {
        if (bodies == null || settings == null) return;
        count = Mathf.Clamp(count, 0, bodies.Length);
        if (!settings.Enabled)
        {
            for (int i = 0; i < count; i++)
            {
                Body body = bodies[i];
                if (body == null) continue;
                if (body.Mode != BodyMode.Autonomous) Cancel(body, settings);
                // Capped contacts can squash an autonomous fairy without granting it a physics slot.
                // Disabling must restore those cosmetic-only participants as well, without moving their AI.
                body.Velocity = Vector2.zero;
                body.Angle = body.AngularVelocity = body.Squash = 0f;
                body.JustActivated = body.DragExpired = body.Settling = false;
                ClearBoundaryEntry(body);
            }
            Reset();
            return;
        }
        if (!Finite(deltaTime) || deltaTime <= 0f) return;
        // A pending pointer owns a future drag slot. Reserve only collision activations;
        // already active fairies keep the normal hard cap and are never evicted by a reservation.
        int collisionActivationLimit = settings.SafeMaximumPhysicalFairies
            - Mathf.Clamp(reservedCollisionSlots, 0, settings.SafeMaximumPhysicalFairies);
        // Capped catch-up prevents a long suspended frame from becoming a large tunnel-producing jump.
        _accumulator += Mathf.Min(deltaTime, .25f);
        int ticks = 0;
        while (_accumulator + .000001f >= FixedStepSeconds && ticks++ < 30)
        {
            _accumulator = Mathf.Max(0f, _accumulator - FixedStepSeconds);
            SimulateFixed(bodies, count, settings, collisionActivationLimit);
        }
    }

    private static void SimulateFixed(Body[] bodies, int count, EnhancementFairyPhysicsSettings settings,
        int collisionActivationLimit)
    {
        int active = 0;
        float smallestRadius = 100f;
        float fastest = 0f;
        for (int i = 0; i < count; i++)
        {
            Body body = bodies[i];
            if (body == null) continue;
            if (!body.Enabled)
            {
                if (body.Mode != BodyMode.Autonomous) CancelDisabled(body, settings);
                body.Velocity = Vector2.zero;
                body.Angle = body.AngularVelocity = body.Squash = 0f;
                body.JustActivated = false;
                ClearBoundaryEntry(body);
                continue;
            }
            Sanitize(body, settings);
            body.Squash *= Mathf.Exp(-FixedStepSeconds * 13f);
            if (body.Eligible || body.Mode != BodyMode.Autonomous) smallestRadius = Mathf.Min(smallestRadius, body.Radius);
            if (body.Mode == BodyMode.Autonomous) continue;
            if (++active > settings.SafeMaximumPhysicalFairies) { Finish(body, settings); continue; }
            fastest = Mathf.Max(fastest, body.Velocity.magnitude);
        }
        // The maximum distance in a microstep is smaller than the smallest eligible collision radius.
        // This also covers small sprites when designers increase the throw-speed Inspector setting.
        int microsteps = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.Max(fastest, settings.SafeMaxThrowSpeed)
            * FixedStepSeconds / Mathf.Max(.12f, smallestRadius)), 1, 8);
        float dt = FixedStepSeconds / microsteps;
        for (int sub = 0; sub < microsteps; sub++)
        {
            for (int i = 0; i < count; i++)
            {
                Body body = bodies[i];
                if (body == null || !body.Enabled || body.Mode == BodyMode.Autonomous) continue;
                Advance(body, dt, settings);
            }
            // Two small position passes remove dense overlaps without allocating a contact collection.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < count; i++)
                {
                    Body a = bodies[i];
                    if (a == null || !a.Enabled) continue;
                    for (int j = i + 1; j < count; j++)
                    {
                        Body b = bodies[j];
                        if (b == null || !b.Enabled) continue;
                        ResolvePair(a, b, bodies, count, settings, pass == 0, collisionActivationLimit);
                    }
                }
        }
    }

    private static void Advance(Body body, float dt, EnhancementFairyPhysicsSettings settings)
    {
        body.Age += dt;
        // Visible AI sprites may begin close enough to a wall that their conservative
        // physics extents overlap it. Recover at the existing follow rate, without a
        // one-frame clamp or allowing the initial overlap to grow farther outward.
        body.BoundaryEntryWeight *= Mathf.Exp(-settings.SafeDragFollowSpeed * dt);
        if (body.Mode == BodyMode.Dragging)
        {
            if (body.Age >= settings.SafeMaximumDragSeconds)
            {
                body.DragExpired = true;
                body.Mode = BodyMode.Physics;
                body.Age = 0f;
                body.Velocity = Vector2.zero;
                return;
            }
            Vector2 old = body.Position;
            float blend = 1f - Mathf.Exp(-settings.SafeDragFollowSpeed * dt);
            body.Position = ClampCenter(Vector2.Lerp(old, body.DragTarget, blend), body, settings, true);
            body.Velocity = ClampVelocity((body.Position - old) / dt, settings);
            body.Angle = Mathf.Lerp(body.Angle, Mathf.Clamp(-body.Velocity.x,
                -settings.SafeAngularReactionLimit * .6f, settings.SafeAngularReactionLimit * .6f), 1f - Mathf.Exp(-10f * dt));
            return;
        }
        if (body.Settling) { AdvanceSettle(body, dt, settings); return; }
        if (body.Age >= settings.SafeMaximumPhysicalDuration - SettleSeconds)
        { BeginSettle(body); AdvanceSettle(body, dt, settings); return; }
        body.Velocity.y -= settings.SafeGravity * dt;
        body.Velocity *= Mathf.Exp(-settings.SafeAirDamping * dt);
        body.Velocity = ClampVelocity(body.Velocity, settings);
        body.Position += body.Velocity * dt;
        ResolveBounds(body, settings, true);
        float floor = settings.SafeBounds.yMin + body.HalfExtents.y;
        bool grounded = body.Position.y <= floor + .003f && Mathf.Abs(body.Velocity.y) < .05f;
        if (grounded)
            body.Velocity.x = Mathf.MoveTowards(body.Velocity.x, 0f, settings.SafeGroundFriction * dt);
        body.QuietSeconds = grounded && body.Velocity.sqrMagnitude <= settings.SafeMinimumSettlingSpeed * settings.SafeMinimumSettlingSpeed
            ? body.QuietSeconds + dt : 0f;
        body.AngularVelocity += (-body.Angle * 30f - body.AngularVelocity * 8f) * dt;
        body.Angle = Mathf.Clamp(body.Angle + body.AngularVelocity * dt,
            -settings.SafeAngularReactionLimit, settings.SafeAngularReactionLimit);
        if (body.QuietSeconds > .12f) BeginSettle(body);
    }

    private static void ResolveBounds(Body body, EnhancementFairyPhysicsSettings settings, bool bounce)
    {
        Vector2 clamped = ClampCenter(body.Position, body, settings, false);
        if (body.Position.x < clamped.x && body.Velocity.x < 0f || body.Position.x > clamped.x && body.Velocity.x > 0f)
        {
            if (bounce) Impact(body, Mathf.Abs(body.Velocity.x), body.Velocity.x, settings);
            body.Velocity.x = -body.Velocity.x * settings.SafeWallRestitution;
        }
        if (body.Position.y > clamped.y && body.Velocity.y > 0f)
            body.Velocity.y = -body.Velocity.y * settings.SafeWallRestitution;
        if (body.Position.y < clamped.y && body.Velocity.y < 0f)
        {
            float impactSpeed = -body.Velocity.y;
            if (bounce && impactSpeed > settings.SafeMinimumSettlingSpeed * 1.5f)
                Impact(body, impactSpeed, body.Velocity.x, settings);
            if (bounce && body.BounceCount < settings.SafeMaximumBounceCount
                && impactSpeed > settings.SafeMinimumSettlingSpeed * 2f)
            {
                body.BounceCount++;
                body.Velocity.y = impactSpeed * settings.SafeFloorRestitution;
                body.Velocity.x *= .88f;
            }
            else body.Velocity.y = 0f;
        }
        body.Position = clamped;
    }

    private static void ResolvePair(Body a, Body b, Body[] bodies, int count,
        EnhancementFairyPhysicsSettings settings, bool allowImpulse, int collisionActivationLimit)
    {
        if (a.Mode == BodyMode.Autonomous && b.Mode == BodyMode.Autonomous) return;
        // Grabbing is kinematic and does not push resting fairies until the player releases it.
        if (a.Mode == BodyMode.Dragging || b.Mode == BodyMode.Dragging || a.Settling || b.Settling) return;
        if (a.Mode == BodyMode.Autonomous && !a.Eligible || b.Mode == BodyMode.Autonomous && !b.Eligible) return;
        Vector2 delta = b.Position - a.Position;
        float radii = a.Radius + b.Radius;
        float distanceSquared = delta.sqrMagnitude;
        if (distanceSquared >= radii * radii) return;
        float distance = Mathf.Sqrt(Mathf.Max(0f, distanceSquared));
        Vector2 normal = distance > .0001f ? delta / distance : new Vector2(a.Id <= b.Id ? 1f : -1f, 0f);
        Vector2 relative = b.Velocity - a.Velocity;
        float closing = Vector2.Dot(relative, normal);
        float impact = Mathf.Max(0f, -closing);
        if (allowImpulse && impact > settings.SafeMinimumSettlingSpeed)
        {
            if (a.Mode == BodyMode.Autonomous) TryActivate(a, bodies, count, settings, collisionActivationLimit);
            if (b.Mode == BodyMode.Autonomous) TryActivate(b, bodies, count, settings, collisionActivationLimit);
            Impact(a, impact, -normal.x, settings);
            Impact(b, impact, normal.x, settings);
        }
        float invA = a.Mode == BodyMode.Physics ? 1f / a.Mass : 0f;
        float invB = b.Mode == BodyMode.Physics ? 1f / b.Mass : 0f;
        float inverseTotal = invA + invB;
        if (inverseTotal <= 0f) return;
        if (allowImpulse && closing < -.001f)
        {
            float impulse = -(1f + settings.SafeFairyRestitution) * closing / inverseTotal;
            a.Velocity = ClampVelocity(a.Velocity - normal * (impulse * invA), settings);
            b.Velocity = ClampVelocity(b.Velocity + normal * (impulse * invB), settings);
        }
        Vector2 correction = normal * (Mathf.Max(0f, radii - distance - .001f) * .8f / inverseTotal);
        if (invA > 0f) { a.Position -= correction * invA; ResolveBounds(a, settings, false); }
        if (invB > 0f) { b.Position += correction * invB; ResolveBounds(b, settings, false); }
    }

    private static void TryActivate(Body body, Body[] bodies, int count, EnhancementFairyPhysicsSettings settings,
        int collisionActivationLimit)
    {
        if (!body.Enabled || !body.Eligible || body.Mode != BodyMode.Autonomous) return;
        int active = 0;
        for (int i = 0; i < count; i++)
            if (bodies[i] != null && bodies[i].Enabled && bodies[i].Mode != BodyMode.Autonomous) active++;
        if (active >= Mathf.Min(settings.SafeMaximumPhysicalFairies, collisionActivationLimit)) return;
        CaptureBoundaryEntry(body);
        body.Mode = BodyMode.Physics;
        body.Age = body.QuietSeconds = body.AngularVelocity = 0f;
        body.BounceCount = 0;
        body.Settling = body.Finished = body.DragExpired = false;
        body.JustActivated = true;
    }

    private static void Impact(Body body, float speed, float direction, EnhancementFairyPhysicsSettings settings)
    {
        body.Squash = Mathf.Max(body.Squash, Mathf.Clamp(speed * .012f, .025f, .13f));
        if (body.Mode != BodyMode.Autonomous)
            body.AngularVelocity = Mathf.Clamp(body.AngularVelocity - direction * speed * 1.8f, -45f, 45f);
    }

    private static void BeginSettle(Body body)
    {
        // The existing settle interpolation can finish any remaining entry correction,
        // even when an unusually slow Inspector follow speed was selected.
        ClearBoundaryEntry(body);
        body.Settling = true;
        body.SettleElapsed = 0f;
        body.SettleFrom = body.Position;
        body.SettleAngle = body.Angle;
        body.SettleSquash = body.Squash;
        body.Velocity = Vector2.zero;
    }

    private static void AdvanceSettle(Body body, float dt, EnhancementFairyPhysicsSettings settings)
    {
        body.SettleElapsed += dt;
        float t = Mathf.Clamp01(body.SettleElapsed / SettleSeconds);
        float ease = t * t * (3f - 2f * t);
        Vector2 target = ClampCenter(body.SettleFrom, body, settings, false);
        target.y = settings.SafeBounds.yMin + body.HalfExtents.y;
        body.Position = Vector2.Lerp(body.SettleFrom, target, ease);
        body.Angle = body.SettleAngle * (1f - ease);
        body.Squash = body.SettleSquash * (1f - ease);
        if (t >= 1f) Finish(body, settings);
    }

    private static void Finish(Body body, EnhancementFairyPhysicsSettings settings)
    {
        ClearBoundaryEntry(body);
        body.Position = ClampCenter(body.Position, body, settings, false);
        body.Position.y = settings.SafeBounds.yMin + body.HalfExtents.y;
        body.Velocity = Vector2.zero;
        body.Angle = body.AngularVelocity = body.Squash = 0f;
        body.Mode = BodyMode.Autonomous;
        body.Settling = false;
        body.Finished = true;
    }

    private static void CancelDisabled(Body body, EnhancementFairyPhysicsSettings settings)
    { Sanitize(body, settings); Finish(body, settings); }

    private static void CaptureBoundaryEntry(Body body)
    {
        body.BoundaryEntryCenter = body.Position;
        body.BoundaryEntryWeight = Finite(body.Position) ? 1f : 0f;
    }

    private static void ClearBoundaryEntry(Body body)
    {
        body.BoundaryEntryCenter = Vector2.zero;
        body.BoundaryEntryWeight = 0f;
    }

    public static Vector2 ClampCenter(Vector2 position, Body body, EnhancementFairyPhysicsSettings settings, bool dragging)
    {
        Rect room = settings.SafeBounds;
        float halfX = Mathf.Clamp(body.HalfExtents.x, .12f, room.width * .49f);
        float halfY = Mathf.Clamp(body.HalfExtents.y, .12f, room.height * .49f);
        float minX = room.xMin + halfX;
        float maxX = room.xMax - halfX;
        float minY = room.yMin + halfY;
        float maxY = room.yMax - halfY;
        if (dragging) maxY = Mathf.Min(maxY, minY + settings.SafeMaxDragHeight);
        if (body.BoundaryEntryWeight > 0f && Finite(body.BoundaryEntryCenter))
        {
            float weight = Mathf.Clamp01(body.BoundaryEntryWeight);
            Vector2 entry = body.BoundaryEntryCenter;
            minX = Mathf.Lerp(minX, Mathf.Min(minX, entry.x), weight);
            maxX = Mathf.Lerp(maxX, Mathf.Max(maxX, entry.x), weight);
            minY = Mathf.Lerp(minY, Mathf.Min(minY, entry.y), weight);
            // Compare against the current limit, rather than retaining a raw drag
            // offset: releasing above MaxDragHeight must not lift the real ceiling.
            maxY = Mathf.Lerp(maxY, Mathf.Max(maxY, entry.y), weight);
        }
        if (!Finite(position)) position = new Vector2(room.center.x, minY);
        return new Vector2(Mathf.Clamp(position.x, minX, maxX), Mathf.Clamp(position.y, minY, maxY));
    }

    private static void Sanitize(Body body, EnhancementFairyPhysicsSettings settings)
    {
        Rect room = settings.SafeBounds;
        body.Radius = EnhancementFairyPhysicsSettings.Safe(body.Radius, .12f, 4f, .65f);
        body.Mass = EnhancementFairyPhysicsSettings.Safe(body.Mass, .1f, 10f, 1f);
        body.HalfExtents = new Vector2(EnhancementFairyPhysicsSettings.Safe(body.HalfExtents.x, .12f, room.width * .49f, .65f),
            EnhancementFairyPhysicsSettings.Safe(body.HalfExtents.y, .12f, room.height * .49f, .85f));
        body.Velocity = ClampVelocity(body.Velocity, settings);
        if (!Finite(body.Position)) body.Position = ClampCenter(body.Position, body, settings, false);
        body.Angle = EnhancementFairyPhysicsSettings.Safe(body.Angle,
            -settings.SafeAngularReactionLimit, settings.SafeAngularReactionLimit, 0f);
        body.AngularVelocity = EnhancementFairyPhysicsSettings.Safe(body.AngularVelocity, -90f, 90f, 0f);
        body.Squash = EnhancementFairyPhysicsSettings.Safe(body.Squash, -.1f, .15f, 0f);
        body.Age = EnhancementFairyPhysicsSettings.Safe(body.Age, 0f, 40f, 0f);
    }

    private static Vector2 ClampVelocity(Vector2 velocity, EnhancementFairyPhysicsSettings settings)
        => Finite(velocity) ? Vector2.ClampMagnitude(velocity, settings.SafeMaxThrowSpeed) : Vector2.zero;
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
}
