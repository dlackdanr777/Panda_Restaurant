using System;

public static class CookingRuntimePolicyCalculator
{
    public const string PolicyMarker = "COOKING_RUNTIME_CAP_POLICY_2026_08_24_V1";
    public const float AutomaticSoftCapStart = 48f;
    public const float AutomaticExcessRetention = 0.25f;
    public const float MaximumCookingMultiplier = 96f;
    public const float MinimumCookDurationSeconds = 1f;

    public static float ApplyAutomaticSoftCap(float rawAutomaticMultiplier)
    {
        if (!IsFiniteNonNegative(rawAutomaticMultiplier))
        {
            return 1f;
        }

        if (rawAutomaticMultiplier <= AutomaticSoftCapStart)
        {
            return rawAutomaticMultiplier;
        }

        double softCapped = AutomaticSoftCapStart
                            + ((double)rawAutomaticMultiplier - AutomaticSoftCapStart)
                            * AutomaticExcessRetention;
        return ToFiniteFloat(softCapped);
    }

    public static float ApplyTouchAndHardCap(
        float softCappedAutomaticMultiplier,
        float touchMultiplier)
    {
        if (!IsFiniteNonNegative(softCappedAutomaticMultiplier)
            || !IsFiniteNonNegative(touchMultiplier))
        {
            return 1f;
        }

        double finalMultiplier =
            (double)softCappedAutomaticMultiplier * touchMultiplier;
        if (finalMultiplier <= 0d)
        {
            return 0f;
        }

        if (finalMultiplier >= MaximumCookingMultiplier)
        {
            return MaximumCookingMultiplier;
        }

        return (float)finalMultiplier;
    }

    public static float CalculateNextRemainingTime(
        float initialCookTime,
        float currentRemainingTime,
        float elapsedRealSecondsBeforeFrame,
        float deltaSeconds,
        float finalCookingMultiplier)
    {
        if (!IsFinitePositive(initialCookTime))
        {
            return 0f;
        }

        float safeCurrentRemaining = NormalizeRemainingTime(
            currentRemainingTime,
            initialCookTime);
        if (safeCurrentRemaining <= 0f)
        {
            return 0f;
        }

        if (!IsFiniteNonNegative(deltaSeconds)
            || !IsFiniteNonNegative(finalCookingMultiplier))
        {
            return safeCurrentRemaining;
        }

        double safeElapsed = IsFiniteNonNegative(elapsedRealSecondsBeforeFrame)
            ? elapsedRealSecondsBeforeFrame
            : 0d;
        double nextElapsed = safeElapsed + deltaSeconds;
        double candidateRemaining = safeCurrentRemaining
                                    - (double)deltaSeconds * finalCookingMultiplier;
        double minimumRemaining = CalculateMinimumRemainingTimeDouble(
            initialCookTime,
            nextElapsed);
        double nextRemaining = Math.Max(
            Math.Max(candidateRemaining, minimumRemaining),
            0d);
        return ToRemainingTime(nextRemaining, initialCookTime);
    }

    public static float CalculateMinimumRemainingTime(
        float initialCookTime,
        float elapsedRealSeconds)
    {
        if (!IsFinitePositive(initialCookTime))
        {
            return 0f;
        }

        if (!IsFiniteNonNegative(elapsedRealSeconds))
        {
            return initialCookTime;
        }

        return ToRemainingTime(
            CalculateMinimumRemainingTimeDouble(initialCookTime, elapsedRealSeconds),
            initialCookTime);
    }

    private static double CalculateMinimumRemainingTimeDouble(
        double initialCookTime,
        double elapsedRealSeconds)
    {
        if (elapsedRealSeconds >= MinimumCookDurationSeconds)
        {
            return 0d;
        }

        return initialCookTime
               * (1d - elapsedRealSeconds / MinimumCookDurationSeconds);
    }

    private static float NormalizeRemainingTime(
        float currentRemainingTime,
        float initialCookTime)
    {
        if (!IsFiniteNonNegative(currentRemainingTime))
        {
            return initialCookTime;
        }

        return currentRemainingTime > initialCookTime
            ? initialCookTime
            : currentRemainingTime;
    }

    private static float ToRemainingTime(double value, float initialCookTime)
    {
        if (value <= 0d)
        {
            return 0f;
        }

        if (value >= initialCookTime)
        {
            return initialCookTime;
        }

        return (float)value;
    }

    private static float ToFiniteFloat(double value)
    {
        if (value >= float.MaxValue)
        {
            return float.MaxValue;
        }

        return (float)value;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value)
               && !float.IsInfinity(value)
               && value > 0f;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value)
               && !float.IsInfinity(value)
               && value >= 0f;
    }
}
