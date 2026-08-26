public static class CookingRuntimeMultiplierCalculator
{
    // COOKING_RUNTIME_POLICY_2026_08_21_V1
    public const float MaximumCookingBonusPercent = 1000f;

    public static float CalculateEquipmentCookingMultiplier(
        float equipmentBonusPercent)
    {
        return CalculatePercentMultiplier(equipmentBonusPercent);
    }

    public static float CalculateChefPassiveCookingMultiplier(
        bool hasAssignedStaff,
        bool isStaffWorking,
        float chefPassiveBonusPercent)
    {
        if (!hasAssignedStaff || !isStaffWorking)
        {
            return 1f;
        }

        return CalculatePercentMultiplier(chefPassiveBonusPercent);
    }

    private static float CalculatePercentMultiplier(float bonusPercent)
    {
        if (double.IsNaN(bonusPercent)
            || double.IsInfinity(bonusPercent)
            || bonusPercent < 0f
            || bonusPercent > MaximumCookingBonusPercent)
        {
            return 1f;
        }

        double multiplier = 1d + (double)bonusPercent * 0.01d;
        if (double.IsNaN(multiplier)
            || double.IsInfinity(multiplier)
            || multiplier < 1d
            || multiplier > 11d)
        {
            return 1f;
        }

        return (float)multiplier;
    }
}
