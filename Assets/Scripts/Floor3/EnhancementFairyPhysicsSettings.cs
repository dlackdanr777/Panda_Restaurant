using System;
using UnityEngine;

/// <summary>Local, visual-only playground interaction. Does not alter fairy ownership or abilities.</summary>
[Serializable]
public sealed class EnhancementFairyPhysicsSettings
{
    public bool Enabled = true;
    [Tooltip("Floor3-local connected interior across the left playground and right map room. Keep X inside the ordinary Ground Area for a seamless AI return.")]
    public Rect Bounds = new Rect(-50f, 7.85f, 60f, 13f);
    [Min(1f)] public float DragThresholdPixels = 12f;
    [Min(1f)] public float DragReferenceDpi = 160f;
    [Min(.1f)] public float DragFollowSpeed = 20f;
    [Min(.1f)] public float MaxDragHeight = 10f;
    [Min(1f)] public float MaximumDragSeconds = 10f;
    [Min(0f)] public float ThrowVelocityMultiplier = 1.1f;
    [Min(.1f)] public float MaxThrowSpeed = 18f;
    [Min(.1f)] public float Gravity = 22f;
    [Range(0f, .9f)] public float FloorRestitution = .6f;
    [Range(0f, .9f)] public float WallRestitution = .48f;
    [Range(0f, .9f)] public float FairyRestitution = .55f;
    [Min(0f)] public float AirDamping = .2f;
    [Min(0f)] public float GroundFriction = 5f;
    [Range(0, 5)] public int MaximumBounceCount = 3;
    [Min(.01f)] public float MinimumSettlingSpeed = .3f;
    [Min(.5f)] public float MaximumPhysicalDuration = 6f;
    [Range(0f, 25f)] public float AngularReactionLimit = 10f;
    [Range(.4f, 1.2f)] public float CollisionRadiusScale = .85f;
    [Range(1, 5)] public int MaximumPhysicalFairies = 5;

    public Rect SafeBounds => new Rect(Safe(Bounds.x, -1000f, 1000f, -50f),
        Safe(Bounds.y, -1000f, 1000f, 7.85f), Safe(Bounds.width, 3f, 100f, 60f),
        Safe(Bounds.height, 3f, 50f, 13f));
    public float SafeDragFollowSpeed => Safe(DragFollowSpeed, .1f, 50f, 20f);
    public float SafeMaxDragHeight => Safe(MaxDragHeight, .1f, SafeBounds.height, 10f);
    public float SafeMaximumDragSeconds => Safe(MaximumDragSeconds, 1f, 20f, 10f);
    public float SafeThrowMultiplier => Safe(ThrowVelocityMultiplier, 0f, 3f, 1.1f);
    public float SafeMaxThrowSpeed => Safe(MaxThrowSpeed, .1f, 30f, 18f);
    public float SafeGravity => Safe(Gravity, .1f, 50f, 22f);
    public float SafeFloorRestitution => Safe(FloorRestitution, 0f, .9f, .6f);
    public float SafeWallRestitution => Safe(WallRestitution, 0f, .9f, .48f);
    public float SafeFairyRestitution => Safe(FairyRestitution, 0f, .9f, .55f);
    public float SafeAirDamping => Safe(AirDamping, 0f, 10f, .2f);
    public float SafeGroundFriction => Safe(GroundFriction, 0f, 30f, 5f);
    public int SafeMaximumBounceCount => Mathf.Clamp(MaximumBounceCount, 0, 5);
    public float SafeMinimumSettlingSpeed => Safe(MinimumSettlingSpeed, .01f, 2f, .3f);
    public float SafeMaximumPhysicalDuration => Safe(MaximumPhysicalDuration, .5f, 12f, 6f);
    public float SafeAngularReactionLimit => Safe(AngularReactionLimit, 0f, 25f, 10f);
    public float SafeCollisionRadiusScale => Safe(CollisionRadiusScale, .4f, 1.2f, .85f);
    public int SafeMaximumPhysicalFairies => Mathf.Clamp(MaximumPhysicalFairies, 1, 5);

    public float GetDragThreshold(float dpi)
    {
        float reference = Safe(DragReferenceDpi, 72f, 480f, 160f);
        float pixels = Safe(DragThresholdPixels, 3f, 80f, 12f);
        return pixels * Mathf.Clamp(Safe(dpi, 0f, 1200f, 0f) / reference, 1f, 4f);
    }

    internal static float Safe(float value, float min, float max, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
}
