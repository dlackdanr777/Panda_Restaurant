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
    [Header("Sequential exit")]
    [Min(.05f)] public float SpawnInterval = .18f;
    [Min(.1f)] public float EntranceDuration = .45f;
    [Min(.1f)] public float EntranceSpread = 3.5f;
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
    public float SafeSpawnInterval => Finite(SpawnInterval, .18f, .05f, 3f);
    public float SafeEntranceDuration => Finite(EntranceDuration, .45f, .1f, 5f);
    public float SafeEntranceSpread => Finite(EntranceSpread, 3.5f, .1f, 10f);
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
        SpawnInterval = SafeSpawnInterval;
        EntranceDuration = SafeEntranceDuration;
        EntranceSpread = SafeEntranceSpread;
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
}
