using System;

public static class FeverRuntimeMultiplierCalculator
{
    // FEVER_POLICY_2026_08_19_V2
    public const float MaxNormalCustomerMoveMultiplier = 3f;
    public const float MaxStaffMoveMultiplier = 3f;
    public const float MaxCookingMultiplier = 5f;

    public static float CalculateNormalCustomerMoveMultiplier(
        float existingMultiplier,
        float normalCustomerSkillMultiplier,
        float feverMultiplier)
    {
        if (!IsFiniteNonNegative(existingMultiplier)
            || !IsFiniteNonNegative(normalCustomerSkillMultiplier)
            || !IsFiniteNonNegative(feverMultiplier))
        {
            return 1f;
        }

        double result =
            (double)existingMultiplier
            * normalCustomerSkillMultiplier
            * feverMultiplier;
        return ClampProduct(result, 0f, MaxNormalCustomerMoveMultiplier);
    }

    public static float CalculateStaffMoveMultiplier(
        float existingMultiplier,
        float personalMoveMultiplier,
        float allStaffMoveMultiplier,
        float feverMultiplier)
    {
        if (!IsFiniteNonNegative(existingMultiplier)
            || !IsFiniteNonNegative(personalMoveMultiplier)
            || !IsFiniteNonNegative(allStaffMoveMultiplier)
            || !IsFiniteNonNegative(feverMultiplier))
        {
            return 1f;
        }

        double result =
            (double)existingMultiplier
            * personalMoveMultiplier
            * allStaffMoveMultiplier
            * feverMultiplier;
        return ClampProduct(result, 0.5f, MaxStaffMoveMultiplier);
    }

    public static float CalculateRoleActionMultiplier(
        float existingActionMultiplier,
        float feverRoleMultiplier)
    {
        if (!IsFiniteNonNegative(existingActionMultiplier)
            || !IsFiniteNonNegative(feverRoleMultiplier))
        {
            return 1f;
        }

        double result = (double)existingActionMultiplier * feverRoleMultiplier;
        if (double.IsInfinity(result) || result > float.MaxValue)
        {
            return float.MaxValue;
        }

        return (float)result;
    }

    public static float CalculateCookingMultiplier(
        float existingCookingMultiplier,
        float assignedStaffRoleMultiplier,
        float assignedCookingSkillMultiplier,
        float globalCookingSkillMultiplier,
        float feverMultiplier,
        float burnerMultiplier,
        float sameFoodTypeMultiplier)
    {
        if (!IsFiniteNonNegative(existingCookingMultiplier)
            || !IsFiniteNonNegative(assignedStaffRoleMultiplier)
            || !IsFiniteNonNegative(assignedCookingSkillMultiplier)
            || !IsFiniteNonNegative(globalCookingSkillMultiplier)
            || !IsFiniteNonNegative(feverMultiplier)
            || !IsFiniteNonNegative(burnerMultiplier)
            || !IsFiniteNonNegative(sameFoodTypeMultiplier))
        {
            return 1f;
        }

        double result =
            (double)existingCookingMultiplier
            * assignedStaffRoleMultiplier
            * assignedCookingSkillMultiplier
            * globalCookingSkillMultiplier
            * feverMultiplier
            * burnerMultiplier
            * sameFoodTypeMultiplier;
        return ClampProduct(result, 0f, MaxCookingMultiplier);
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= 0f;
    }

    private static float ClampProduct(double value, float minimum, float maximum)
    {
        if (value <= minimum)
        {
            return minimum;
        }

        if (value >= maximum)
        {
            return maximum;
        }

        return (float)value;
    }
}
