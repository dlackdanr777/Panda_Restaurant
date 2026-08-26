using System;

internal static class StaffPaymentTipCalculator
{
    private const float MaximumPayoutBonusPercent = 1000f;

    internal static int CalculateFoodPaymentTipPayout(
        int baseTip,
        float payoutBonusPercent)
    {
        if (baseTip <= 0)
        {
            return 0;
        }

        float safePercent = payoutBonusPercent;
        if (float.IsNaN(safePercent)
            || float.IsInfinity(safePercent)
            || safePercent < 0f
            || safePercent > MaximumPayoutBonusPercent)
        {
            safePercent = 0f;
        }

        double multiplier = 1d + (double)safePercent * 0.01d;
        double calculated = Math.Floor((double)baseTip * multiplier);
        if (double.IsNaN(calculated) || calculated <= 0d)
        {
            return 0;
        }

        if (double.IsInfinity(calculated) || calculated >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)calculated;
    }
}
