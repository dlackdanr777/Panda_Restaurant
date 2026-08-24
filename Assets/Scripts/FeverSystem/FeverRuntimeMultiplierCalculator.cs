using System;

public static class FeverRuntimeMultiplierCalculator
{
    // FEVER_POLICY_2026_08_19_V2
    // COOKING_RUNTIME_POLICY_2026_08_21_V1
    // COOKING_RUNTIME_CAP_POLICY_2026_08_24_V1
    public const float MaxNormalCustomerMoveMultiplier = 3f;
    public const float MaxStaffMoveMultiplier = 3f;
    public const float MaxCookingMultiplier =
        CookingRuntimePolicyCalculator.MaximumCookingMultiplier;

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
        float sharedBaseCookingMultiplier,
        float localEquipmentCookingMultiplier,
        float chefPassiveCookingMultiplier,
        float assignedCookingSkillMultiplier,
        float feverMultiplier,
        float burnerTouchMultiplier,
        float sameFoodTypeMultiplier)
    {
        if (!IsFiniteNonNegative(sharedBaseCookingMultiplier)
            || !IsFiniteNonNegative(localEquipmentCookingMultiplier)
            || !IsFiniteNonNegative(chefPassiveCookingMultiplier)
            || !IsFiniteNonNegative(assignedCookingSkillMultiplier)
            || !IsFiniteNonNegative(feverMultiplier)
            || !IsFiniteNonNegative(burnerTouchMultiplier)
            || !IsFiniteNonNegative(sameFoodTypeMultiplier))
        {
            return 1f;
        }

        double rawAutomaticMultiplier =
            (double)sharedBaseCookingMultiplier
            * localEquipmentCookingMultiplier
            * chefPassiveCookingMultiplier
            * assignedCookingSkillMultiplier
            * feverMultiplier
            * sameFoodTypeMultiplier;
        float finiteRawAutomaticMultiplier = rawAutomaticMultiplier >= float.MaxValue
            ? float.MaxValue
            : (float)rawAutomaticMultiplier;
        float softCappedAutomaticMultiplier =
            CookingRuntimePolicyCalculator.ApplyAutomaticSoftCap(
                finiteRawAutomaticMultiplier);
        return CookingRuntimePolicyCalculator.ApplyTouchAndHardCap(
            softCappedAutomaticMultiplier,
            burnerTouchMultiplier);
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
