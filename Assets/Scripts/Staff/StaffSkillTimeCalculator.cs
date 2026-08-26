using System;

public static class StaffSkillTimeCalculator
{
    private const int MinimumStaffLevel = 1;
    private const int MaximumStaffLevel = 5;

    private static readonly LevelRule[] LevelRules =
    {
        new LevelRule(0.00m, 1.00m),
        new LevelRule(0.00m, 1.00m),
        new LevelRule(0.00m, 1.00m),
        new LevelRule(0.25m, 0.97m),
        new LevelRule(0.50m, 0.92m)
    };

    public static int CalculateDurationSeconds(
        float baseDurationSeconds,
        int staffLevel,
        float permanentDurationBonusRate)
    {
        decimal baseDuration = ValidatePositiveSeconds(baseDurationSeconds, nameof(baseDurationSeconds));
        decimal permanentDurationBonus = ValidateNonNegativeRate(permanentDurationBonusRate, nameof(permanentDurationBonusRate));
        LevelRule levelRule = GetLevelRule(staffLevel);

        decimal duration = baseDuration * (1m + levelRule.DurationBonusRate + permanentDurationBonus);
        return RoundToPositiveSeconds(duration);
    }

    public static int CalculateCooldownSeconds(
        float baseCooldownSeconds,
        int staffLevel)
    {
        decimal baseCooldown = ValidatePositiveSeconds(baseCooldownSeconds, nameof(baseCooldownSeconds));
        LevelRule levelRule = GetLevelRule(staffLevel);

        decimal cooldown = baseCooldown * levelRule.CooldownMultiplier;
        return RoundToPositiveSeconds(cooldown);
    }

    private static LevelRule GetLevelRule(int staffLevel)
    {
        int clampedLevel = staffLevel < MinimumStaffLevel
            ? MinimumStaffLevel
            : staffLevel > MaximumStaffLevel
                ? MaximumStaffLevel
                : staffLevel;

        return LevelRules[clampedLevel - MinimumStaffLevel];
    }

    private static decimal ValidatePositiveSeconds(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Time must be finite and greater than zero.");
        }

        return ConvertToDecimal(value, parameterName);
    }

    private static decimal ValidateNonNegativeRate(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Rate must be finite and non-negative.");
        }

        return ConvertToDecimal(value, parameterName);
    }

    private static decimal ConvertToDecimal(float value, string parameterName)
    {
        try
        {
            return Convert.ToDecimal(value);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value is outside the supported decimal range.");
        }
    }

    private static int RoundToPositiveSeconds(decimal value)
    {
        decimal roundedSeconds = decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        if (roundedSeconds < 1m)
        {
            return 1;
        }

        return checked((int)roundedSeconds);
    }

    private readonly struct LevelRule
    {
        public decimal DurationBonusRate { get; }
        public decimal CooldownMultiplier { get; }

        public LevelRule(decimal durationBonusRate, decimal cooldownMultiplier)
        {
            DurationBonusRate = durationBonusRate;
            CooldownMultiplier = cooldownMultiplier;
        }
    }
}
