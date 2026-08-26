using System;

public static class StaffRolePerformanceCalculator
{
    public const float CleanerMinimumDurationSeconds = 0.2f;

    public static float CalculatePermanentRoleWorkMultiplier(
        float levelOneSpeed,
        float currentSpeed,
        float permanentGachaBonusRate)
    {
        decimal validatedLevelOneSpeed = ValidatePositiveValue(levelOneSpeed, nameof(levelOneSpeed));
        decimal validatedCurrentSpeed = ValidatePositiveValue(currentSpeed, nameof(currentSpeed));
        decimal validatedPermanentGachaBonusRate = ValidateNonNegativeRate(
            permanentGachaBonusRate,
            nameof(permanentGachaBonusRate));

        return CalculateFloatResult(
            () => CalculatePermanentRoleWorkMultiplierDecimal(
                validatedLevelOneSpeed,
                validatedCurrentSpeed,
                validatedPermanentGachaBonusRate),
            nameof(CalculatePermanentRoleWorkMultiplier));
    }

    public static float CalculateWaiterWorkDurationSeconds(
        float baseWorkDurationSeconds,
        float referenceSpeed,
        float currentSpeed,
        float permanentGachaBonusRate,
        float feverWorkBonusRate)
    {
        decimal validatedBaseWorkDuration = ValidatePositiveValue(
            baseWorkDurationSeconds,
            nameof(baseWorkDurationSeconds));
        decimal validatedReferenceSpeed = ValidatePositiveValue(referenceSpeed, nameof(referenceSpeed));
        decimal validatedCurrentSpeed = ValidatePositiveValue(currentSpeed, nameof(currentSpeed));
        decimal validatedPermanentGachaBonusRate = ValidateNonNegativeRate(
            permanentGachaBonusRate,
            nameof(permanentGachaBonusRate));
        decimal validatedFeverWorkBonusRate = ValidateNonNegativeRate(
            feverWorkBonusRate,
            nameof(feverWorkBonusRate));

        return CalculateFloatResult(
            () => validatedBaseWorkDuration
                * (validatedReferenceSpeed / validatedCurrentSpeed)
                / (1m + validatedPermanentGachaBonusRate + validatedFeverWorkBonusRate),
            nameof(CalculateWaiterWorkDurationSeconds));
    }

    public static float CalculateCleanerFinalDurationSeconds(
        float baseCleaningDurationSeconds,
        float levelOneSpeed,
        float currentSpeed,
        float permanentGachaBonusRate,
        float feverWorkBonusRate)
    {
        decimal validatedBaseCleaningDuration = ValidatePositiveValue(
            baseCleaningDurationSeconds,
            nameof(baseCleaningDurationSeconds));
        decimal validatedLevelOneSpeed = ValidatePositiveValue(levelOneSpeed, nameof(levelOneSpeed));
        decimal validatedCurrentSpeed = ValidatePositiveValue(currentSpeed, nameof(currentSpeed));
        decimal validatedPermanentGachaBonusRate = ValidateNonNegativeRate(
            permanentGachaBonusRate,
            nameof(permanentGachaBonusRate));
        decimal validatedFeverWorkBonusRate = ValidateNonNegativeRate(
            feverWorkBonusRate,
            nameof(feverWorkBonusRate));

        return CalculateFloatResult(
            () =>
            {
                decimal permanentRoleWorkMultiplier = CalculatePermanentRoleWorkMultiplierDecimal(
                    validatedLevelOneSpeed,
                    validatedCurrentSpeed,
                    validatedPermanentGachaBonusRate);
                decimal calculatedDuration = validatedBaseCleaningDuration
                    / permanentRoleWorkMultiplier
                    / (1m + validatedFeverWorkBonusRate);
                decimal minimumDuration = Convert.ToDecimal(CleanerMinimumDurationSeconds);

                return Math.Max(minimumDuration, calculatedDuration);
            },
            nameof(CalculateCleanerFinalDurationSeconds));
    }

    public static float CalculateChefRoleCookMultiplier(
        float baseCookingEfficiencyRate,
        float levelOneSpeed,
        float currentSpeed,
        float permanentGachaBonusRate)
    {
        decimal validatedBaseCookingEfficiencyRate = ValidateNonNegativeRate(
            baseCookingEfficiencyRate,
            nameof(baseCookingEfficiencyRate));
        decimal validatedLevelOneSpeed = ValidatePositiveValue(levelOneSpeed, nameof(levelOneSpeed));
        decimal validatedCurrentSpeed = ValidatePositiveValue(currentSpeed, nameof(currentSpeed));
        decimal validatedPermanentGachaBonusRate = ValidateNonNegativeRate(
            permanentGachaBonusRate,
            nameof(permanentGachaBonusRate));

        return CalculateFloatResult(
            () => CalculateChefRoleCookMultiplierDecimal(
                validatedBaseCookingEfficiencyRate,
                validatedLevelOneSpeed,
                validatedCurrentSpeed,
                validatedPermanentGachaBonusRate),
            nameof(CalculateChefRoleCookMultiplier));
    }

    public static float CalculateChefTotalCookMultiplier(
        float baseCookingEfficiencyRate,
        float levelOneSpeed,
        float currentSpeed,
        float permanentGachaBonusRate,
        float cookingChannelMultiplier)
    {
        decimal validatedBaseCookingEfficiencyRate = ValidateNonNegativeRate(
            baseCookingEfficiencyRate,
            nameof(baseCookingEfficiencyRate));
        decimal validatedLevelOneSpeed = ValidatePositiveValue(levelOneSpeed, nameof(levelOneSpeed));
        decimal validatedCurrentSpeed = ValidatePositiveValue(currentSpeed, nameof(currentSpeed));
        decimal validatedPermanentGachaBonusRate = ValidateNonNegativeRate(
            permanentGachaBonusRate,
            nameof(permanentGachaBonusRate));
        decimal validatedCookingChannelMultiplier = ValidatePositiveValue(
            cookingChannelMultiplier,
            nameof(cookingChannelMultiplier));

        return CalculateFloatResult(
            () => CalculateChefRoleCookMultiplierDecimal(
                    validatedBaseCookingEfficiencyRate,
                    validatedLevelOneSpeed,
                    validatedCurrentSpeed,
                    validatedPermanentGachaBonusRate)
                * validatedCookingChannelMultiplier,
            nameof(CalculateChefTotalCookMultiplier));
    }

    private static decimal CalculatePermanentRoleWorkMultiplierDecimal(
        decimal levelOneSpeed,
        decimal currentSpeed,
        decimal permanentGachaBonusRate)
    {
        decimal result = (currentSpeed / levelOneSpeed) * (1m + permanentGachaBonusRate);
        if (result <= 0m)
        {
            throw new OverflowException();
        }

        return result;
    }

    private static decimal CalculateChefRoleCookMultiplierDecimal(
        decimal baseCookingEfficiencyRate,
        decimal levelOneSpeed,
        decimal currentSpeed,
        decimal permanentGachaBonusRate)
    {
        decimal permanentRoleWorkMultiplier = CalculatePermanentRoleWorkMultiplierDecimal(
            levelOneSpeed,
            currentSpeed,
            permanentGachaBonusRate);

        return 1m + (baseCookingEfficiencyRate * permanentRoleWorkMultiplier);
    }

    private static decimal ValidatePositiveValue(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and greater than zero.");
        }

        return ConvertToDecimal(value, parameterName);
    }

    private static decimal ValidateNonNegativeRate(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Rate must be finite and non-negative.");
        }

        return ConvertToDecimal(value, parameterName);
    }

    private static decimal ConvertToDecimal(float value, string parameterName)
    {
        try
        {
            decimal convertedValue = Convert.ToDecimal(value);
            if (value != 0f && convertedValue == 0m)
            {
                throw new OverflowException();
            }

            return convertedValue;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value is outside the supported decimal range.");
        }
    }

    private static float CalculateFloatResult(Func<decimal> calculation, string operationName)
    {
        try
        {
            decimal decimalResult = calculation();
            if (decimalResult <= 0m)
            {
                throw new OverflowException();
            }

            float floatResult = Convert.ToSingle(decimalResult);
            if (float.IsNaN(floatResult) || float.IsInfinity(floatResult))
            {
                throw new OverflowException();
            }

            return floatResult;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                "calculation",
                operationName + " produced a value outside the supported numeric range.");
        }
        catch (DivideByZeroException)
        {
            throw new ArgumentOutOfRangeException(
                "calculation",
                operationName + " produced a value outside the supported numeric range.");
        }
    }
}
