using System;
using System.Globalization;

/// <summary>Pure day calculation for the test authority. No device clock, daily reset mutation,
/// PlayerPrefs or account storage. Each day has an independent counter: no carry-over/reset job.</summary>
public static class PopupStoreDailyUsage
{
    public static bool TryGetDayKey(DateTimeOffset instant, out string dayKey)
    {
        dayKey = null;
        // KST UTC+9, reset 12:00 KST => UTC-3 calendar date. Normalize offset before arithmetic.
        try { dayKey = instant.UtcDateTime.AddHours(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        catch (ArgumentOutOfRangeException) { return false; }
        return true;
    }

    public static int Remaining(int limit, int used)
    {
        if (limit < 0 || used < 0) throw new ArgumentOutOfRangeException("Quota values must be nonnegative.");
        return used >= limit ? 0 : limit - used;
    }
}
