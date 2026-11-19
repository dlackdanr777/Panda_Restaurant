using System;
using UnityEngine;

/// <summary>Inspector-owned presentation settings. Ownership and the permanent MaxActive are never changed.</summary>
[Serializable]
public sealed class EnhancementFairyHouseSettings
{
    [Header("Temporary display capacity (base limit comes from Enhancement Fairy Settings)")]
    [Range(0, 12)] public int TemporaryExtraLimit = 12;
    [Range(1, 3)] public int MaximumShakeStage = 3;
    [Range(0, 18)] public int FirstTapSpawnCount = 1;
    [Range(0, 18)] public int SecondTapSpawnCount = 2;
    [Range(0, 18)] public int ThirdAndLaterSpawnCount = 3;
    [Min(.1f)] public float TapSequenceResetSeconds = 3f;
    [Header("House visual only")]
    public Vector3 ShakeAngles = new Vector3(3f, 5f, 7f);
    [Min(.05f)] public float ShakeDuration = .45f;
    [Range(1, 5)] public int ShakeCycles = 2;
    [Header("Automatic visual hint (never requests fairies)")]
    public bool EnableIdleHint = true;
    public Vector2 FirstHintDelay = new Vector2(6f, 10f);
    public Vector2 HintInterval = new Vector2(15f, 25f);
    [Range(0f, 2f)] public float HintRotationDegrees = 1.25f;
    [Range(.1f, 2f)] public float HintDuration = .6f;
    [Header("Sequential exit")]
    [Tooltip("Delay before the first guest of a new tap queue. Zero responds immediately.")]
    [Min(0f)] public float TapSpawnFirstDelay = 0f;
    [InspectorName("Tap Spawn Interval"), Min(.05f)] public float SpawnInterval = .18f;
    [Min(.1f)] public float EntranceDuration = .45f;
    [Tooltip("Ordinary walking pace for the first fixed outward route after emerging. Limited to a small adjustment; autonomous behaviour and home returns keep their original speeds.")]
    [Range(1f, 1.2f)] public float TapDispersalSpeedMultiplier = 1f;
    [Min(.1f)] public float EntranceSpread = 3.5f;
    [Tooltip("A pending visual emergence expires after this much visible activity time if no in-bounds exit plan or capacity is available. Temporary doorway overlap does not delay a spawn. Further taps never extend an existing request.")]
    [Min(1f)] public float ExitClearanceTimeoutSeconds = 60f;
    [Header("Room spacing after the doorway animation")]
    [Tooltip("Ground-plane radius around the entrance. Default fits the current house and fairy sprites.")]
    [Min(.5f)] public float HouseCrowdingRadius = 3.75f;
    [Range(0, 6)] public int HouseResidentLimit = 2;
    [Tooltip("Grace for surplus bodies within the house zone before an outward walk. Movement does not reset dwell; protected birth, emergence, return and furniture activities are excluded.")]
    [Range(.1f, 2f)] public float HouseResidentGraceSeconds = 1.25f;
    [Tooltip("Actual sprite half-widths plus padding can require more separation than this minimum.")]
    [Min(.1f)] public float MinimumFairySpacing = 1.5f;
    [Min(0f)] public float SpriteSpacingPadding = .2f;
    [Tooltip("Prefer free destinations within this entrance distance; search the existing ground bounds if this region is full.")]
    [Min(1f)] public float DispersionSearchDistance = 18f;
    [Header("Oldest eligible fairy returns first")]
    [Tooltip("Visible third-floor activity time after the last successful house emergence. Empty taps never restart this timer.")]
    [Min(.1f)] public float TemporaryPlaySeconds = 45f;
    [Min(0f)] public float MinimumIndividualStaySeconds = 25f;
    [Tooltip("Wait after a return has completely finished, before another return may begin.")]
    [Min(.05f)] public float ReturnStaggerMinSeconds = 6f;
    [Min(.05f)] public float ReturnStaggerMaxSeconds = 10f;
    [Range(1, 4)] public int MaximumConcurrentReturns = 1;
    [Tooltip("Preferred minimum visible travel time. Longer journeys take longer so they never exceed the normal calm walking speed or Return Move Speed.")]
    [Min(.1f)] public float ReturnTravelSeconds = 4f;
    [Tooltip("World units per second upper limit; also capped by the existing calm fairy walk speed.")]
    [Min(.1f)] public float ReturnMoveSpeed = 1.05f;
    [Tooltip("Safety guard floor. The actual guard is extended to cover distance / chosen speed plus a margin, so distant fairies still reach the entrance.")]
    [Min(.2f)] public float ReturnTimeoutSeconds = 60f;
    [Min(.05f)] public float ReturnFadeSeconds = .6f;

    public int SafeExtraLimit => Mathf.Clamp(TemporaryExtraLimit, 0, 12);
    public int SafeMaximumShakeStage => Mathf.Clamp(MaximumShakeStage, 1, 3);
    public float SafeTapSequenceResetSeconds => Finite(TapSequenceResetSeconds, 3f, .1f, 30f);
    public float SafeShakeDuration => Finite(ShakeDuration, .45f, .05f, 3f);
    public float SafeTapSpawnFirstDelay => Finite(TapSpawnFirstDelay, 0f, 0f, 3f);
    public float SafeSpawnInterval => Finite(SpawnInterval, .18f, .05f, 3f);
    public float SafeEntranceDuration => Finite(EntranceDuration, .45f, .1f, 5f);
    public float SafeTapDispersalSpeedMultiplier => Finite(TapDispersalSpeedMultiplier, 1f, 1f, 1.2f);
    public float SafeEntranceSpread => Finite(EntranceSpread, 3.5f, .1f, 10f);
    public float SafeExitClearanceTimeoutSeconds => Finite(ExitClearanceTimeoutSeconds, 60f, 1f, 120f);
    public float SafeHouseCrowdingRadius => Finite(HouseCrowdingRadius, 3.75f, .5f, 15f);
    public int SafeHouseResidentLimit => Mathf.Clamp(HouseResidentLimit, 0, 6);
    public float SafeHouseResidentGraceSeconds => Finite(HouseResidentGraceSeconds, 1.25f, .1f, 2f);
    public float SafeMinimumFairySpacing => Finite(MinimumFairySpacing, 1.5f, .1f, 10f);
    public float SafeSpriteSpacingPadding => Finite(SpriteSpacingPadding, .2f, 0f, 3f);
    public float SafeDispersionSearchDistance => Finite(DispersionSearchDistance, 18f, 1f, 120f);
    public float SafeTemporaryPlaySeconds => Finite(TemporaryPlaySeconds, 45f, .1f, 600f);
    public float SafeMinimumIndividualStaySeconds => Finite(MinimumIndividualStaySeconds, 25f, 0f, 600f);
    public float SafeReturnStaggerMinSeconds => Finite(ReturnStaggerMinSeconds, 6f, .05f, 60f);
    public float SafeReturnStaggerMaxSeconds => Mathf.Max(SafeReturnStaggerMinSeconds, Finite(ReturnStaggerMaxSeconds, 10f, .05f, 60f));
    public int SafeMaximumConcurrentReturns => Mathf.Clamp(MaximumConcurrentReturns, 1, 4);
    public float SafeReturnTravelSeconds => Finite(ReturnTravelSeconds, 4f, .1f, 30f);
    public float SafeReturnMoveSpeed => Finite(ReturnMoveSpeed, 1.05f, .1f, 40f);
    public float SafeReturnTimeoutSeconds => Finite(ReturnTimeoutSeconds, 60f, .2f, 180f);
    public float SafeReturnFadeSeconds => Finite(ReturnFadeSeconds, .6f, .05f, 3f);

    public int SpawnCountForStage(int stage) => Mathf.Clamp(stage <= 1 ? FirstTapSpawnCount
        : stage == 2 ? SecondTapSpawnCount : ThirdAndLaterSpawnCount, 0, 18);
    public float ShakeAngleForStage(int stage) => Finite(stage <= 1 ? ShakeAngles.x
        : stage == 2 ? ShakeAngles.y : ShakeAngles.z, 3f, 0f, 30f);

    public void Validate()
    {
        TemporaryExtraLimit = SafeExtraLimit;
        MaximumShakeStage = SafeMaximumShakeStage;
        FirstTapSpawnCount = Mathf.Clamp(FirstTapSpawnCount, 0, 18);
        SecondTapSpawnCount = Mathf.Clamp(SecondTapSpawnCount, 0, 18);
        ThirdAndLaterSpawnCount = Mathf.Clamp(ThirdAndLaterSpawnCount, 0, 18);
        TapSequenceResetSeconds = SafeTapSequenceResetSeconds;
        ShakeAngles = new Vector3(ShakeAngleForStage(1), ShakeAngleForStage(2), ShakeAngleForStage(3));
        ShakeDuration = SafeShakeDuration;
        ShakeCycles = Mathf.Clamp(ShakeCycles, 1, 5);
        FirstHintDelay = HintRange(FirstHintDelay, 6f, 10f);
        HintInterval = HintRange(HintInterval, 15f, 25f);
        HintRotationDegrees = Finite(HintRotationDegrees, 1.25f, 0f, 2f);
        HintDuration = Finite(HintDuration, .6f, .1f, 2f);
        TapSpawnFirstDelay = SafeTapSpawnFirstDelay;
        SpawnInterval = SafeSpawnInterval;
        EntranceDuration = SafeEntranceDuration;
        TapDispersalSpeedMultiplier = SafeTapDispersalSpeedMultiplier;
        EntranceSpread = SafeEntranceSpread;
        ExitClearanceTimeoutSeconds = SafeExitClearanceTimeoutSeconds;
        HouseCrowdingRadius = SafeHouseCrowdingRadius;
        HouseResidentLimit = SafeHouseResidentLimit;
        HouseResidentGraceSeconds = SafeHouseResidentGraceSeconds;
        MinimumFairySpacing = SafeMinimumFairySpacing;
        SpriteSpacingPadding = SafeSpriteSpacingPadding;
        DispersionSearchDistance = SafeDispersionSearchDistance;
        TemporaryPlaySeconds = SafeTemporaryPlaySeconds;
        MinimumIndividualStaySeconds = SafeMinimumIndividualStaySeconds;
        ReturnStaggerMinSeconds = SafeReturnStaggerMinSeconds;
        ReturnStaggerMaxSeconds = SafeReturnStaggerMaxSeconds;
        MaximumConcurrentReturns = SafeMaximumConcurrentReturns;
        ReturnTravelSeconds = SafeReturnTravelSeconds;
        ReturnMoveSpeed = SafeReturnMoveSpeed;
        ReturnTimeoutSeconds = SafeReturnTimeoutSeconds;
        ReturnFadeSeconds = SafeReturnFadeSeconds;
    }

    private static float Finite(float value, float fallback, float min, float max) =>
        Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);

    private static Vector2 HintRange(Vector2 value, float fallbackMin, float fallbackMax)
    {
        float minimum = Finite(value.x, fallbackMin, .1f, 300f);
        return new Vector2(minimum, Finite(value.y, fallbackMax, minimum, 300f));
    }
}
