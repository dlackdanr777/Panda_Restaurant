using System;
using System.Globalization;
using UnityEngine;

public enum PopupStoreBusinessDayReset { KstNoon }

/// <summary>One authored access policy. These values describe Editor review admission, never server authority.</summary>
[Serializable]
public struct PopupStoreBusinessAccessSetting
{
    [SerializeField, Range(1, 3)] private int _dailyFreeLimit;
    [SerializeField] private string _version;
    [Tooltip("Inclusive game date, yyyy-MM-dd. The game date changes at 12:00 KST.")]
    [SerializeField] private string _effectiveGameDay;
    public int DailyFreeLimit => _dailyFreeLimit;
    public string Version => _version;
    public string EffectiveGameDay => _effectiveGameDay;

    public PopupStoreBusinessAccessSetting(int dailyFreeLimit, string version, string effectiveGameDay)
    { _dailyFreeLimit = dailyFreeLimit; _version = version; _effectiveGameDay = effectiveGameDay; }

    public bool TryValidate(out string error)
    {
        error = null;
        if (_dailyFreeLimit < 1 || _dailyFreeLimit > 3) error = "Daily free limit must be 1, 2 or 3.";
        else if (!IsValidVersion(_version)) error = "Policy version must contain 1-64 ASCII letters, digits, '.', '_' or '-'.";
        else if (!TryParseGameDay(_effectiveGameDay, out _)) error = "Effective game date must be a valid yyyy-MM-dd date.";
        return error == null;
    }

    public static bool IsValidVersion(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
        foreach (char c in value)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false;
        return true;
    }
    public static bool TryParseGameDay(string value, out DateTime date)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

/// <summary>Detached current/future policy snapshot. No device clock, account state or reward data.</summary>
public sealed class PopupStoreBusinessAccessSchedule
{
    public PopupStoreBusinessAccessSetting Current { get; }
    public PopupStoreBusinessAccessSetting? Future { get; }
    public PopupStoreBusinessDayReset ResetBasis => PopupStoreBusinessDayReset.KstNoon;
    private readonly PopupStoreBusinessAccessPolicy _currentPolicy, _futurePolicy;

    private PopupStoreBusinessAccessSchedule(PopupStoreBusinessAccessSetting current, PopupStoreBusinessAccessSetting? future)
    {
        Current = current; Future = future;
        _currentPolicy = new PopupStoreBusinessAccessPolicy(current.Version, current.DailyFreeLimit);
        if (future.HasValue) _futurePolicy = new PopupStoreBusinessAccessPolicy(future.Value.Version, future.Value.DailyFreeLimit);
    }

    public static bool TryCreate(PopupStoreBusinessAccessSetting current, PopupStoreBusinessAccessSetting? future,
        out PopupStoreBusinessAccessSchedule schedule, out string error)
    {
        schedule = null;
        if (!current.TryValidate(out error)) return false;
        if (future.HasValue)
        {
            if (!future.Value.TryValidate(out error)) return false;
            if (string.CompareOrdinal(future.Value.EffectiveGameDay, current.EffectiveGameDay) <= 0)
            { error = "Future policy must start after the current policy's game date."; return false; }
            if (string.Equals(current.Version, future.Value.Version, StringComparison.Ordinal))
            { error = "A changed policy needs a distinct version."; return false; }
        }
        schedule = new PopupStoreBusinessAccessSchedule(current, future); return true;
    }

    public bool TryResolve(string gameDay, out PopupStoreBusinessAccessPolicy policy, out string error)
    {
        policy = null; error = null;
        if (!PopupStoreBusinessAccessSetting.TryParseGameDay(gameDay, out _))
        { error = "The authority game date is invalid."; return false; }
        if (string.CompareOrdinal(gameDay, Current.EffectiveGameDay) < 0)
        { error = "No access policy applies before " + Current.EffectiveGameDay + ". Use an explicitly dated isolated review session."; return false; }
        policy = Future.HasValue && string.CompareOrdinal(gameDay, Future.Value.EffectiveGameDay) >= 0 ? _futurePolicy : _currentPolicy;
        return true;
    }

    /// <summary>Operational changes start on a later game date. Editing an Asset only affects a newly injected review snapshot.</summary>
    public bool TryWithFuture(PopupStoreBusinessAccessSetting next, string observedGameDay,
        out PopupStoreBusinessAccessSchedule schedule, out string error)
    {
        schedule = null;
        if (!TryResolve(observedGameDay, out _, out error) || !next.TryValidate(out error)) return false;
        if (string.CompareOrdinal(next.EffectiveGameDay, observedGameDay) <= 0)
        { error = "A policy change may apply only from the next game date or later."; return false; }
        var active = Future.HasValue && string.CompareOrdinal(observedGameDay, Future.Value.EffectiveGameDay) >= 0 ? Future.Value : Current;
        if (string.Equals(next.Version, Current.Version, StringComparison.Ordinal)
            || (Future.HasValue && string.Equals(next.Version, Future.Value.Version, StringComparison.Ordinal)))
        { error = "A policy update must use a new version."; return false; }
        return TryCreate(active, next, out schedule, out error);
    }
}

[CreateAssetMenu(fileName = "PopupStoreBusinessAccessSettings", menuName = "Panda Restaurant/Popup Store/Business Access Settings")]
public sealed class PopupStoreBusinessAccessSettings : ScriptableObject
{
    [Tooltip("Editor review configuration only; changing this asset grants no production entitlement.")]
    [SerializeField] private PopupStoreBusinessAccessSetting _current = new PopupStoreBusinessAccessSetting(1, "popup-review-access-v1", "2026-11-03");
    [SerializeField] private bool _hasFuturePolicy;
    [SerializeField] private PopupStoreBusinessAccessSetting _future;
    [SerializeField] private PopupStoreBusinessDayReset _resetBasis = PopupStoreBusinessDayReset.KstNoon;
    public PopupStoreBusinessAccessSetting Current => _current;
    public bool HasFuturePolicy => _hasFuturePolicy;
    public PopupStoreBusinessAccessSetting Future => _future;
    public PopupStoreBusinessDayReset ResetBasis => _resetBasis;

    public bool TryCreateSchedule(out PopupStoreBusinessAccessSchedule schedule, out string error)
    {
        schedule = null;
        if (_resetBasis != PopupStoreBusinessDayReset.KstNoon)
        { error = "Only the existing attendance boundary, 12:00 KST, is supported."; return false; }
        return PopupStoreBusinessAccessSchedule.TryCreate(_current, _hasFuturePolicy ? _future : (PopupStoreBusinessAccessSetting?)null, out schedule, out error);
    }
    public bool TryValidate(out string error) => TryCreateSchedule(out _, out error);
}

/// <summary>Read-only access information from the same authority that approves starts. It authorizes no local play by itself.</summary>
public sealed class PopupStoreBusinessAccessSnapshot
{
    public PopupStoreRewardAccountSession Session { get; }
    public bool Success { get; }
    public string GameDay { get; }
    public string PolicyVersion { get; }
    public int DailyLimit { get; }
    public int Used { get; }
    public int Remaining => Success ? PopupStoreDailyUsage.Remaining(DailyLimit, Used) : 0;
    public DateTimeOffset? ObservedAtUtc { get; }
    public string Error { get; }

    public PopupStoreBusinessAccessSnapshot(PopupStoreRewardAccountSession session, string gameDay, string policyVersion,
        int dailyLimit, int used, DateTimeOffset? observedAtUtc, string error = null)
    {
        Session = session; GameDay = gameDay; PolicyVersion = policyVersion;
        DailyLimit = dailyLimit; Used = used; ObservedAtUtc = observedAtUtc;
        Error = error;
        if (Error == null && (session == null || !PopupStoreBusinessAccessSetting.TryParseGameDay(gameDay, out _)
            || !PopupStoreBusinessAccessSetting.IsValidVersion(policyVersion) || dailyLimit < 0 || used < 0
            || !observedAtUtc.HasValue || observedAtUtc.Value.Offset != TimeSpan.Zero
            || !PopupStoreDailyUsage.TryGetDayKey(observedAtUtc.Value, out string actualDay)
            || !string.Equals(gameDay, actualDay, StringComparison.Ordinal)))
            Error = "The authority returned invalid access information.";
        Success = Error == null;
    }
    public static PopupStoreBusinessAccessSnapshot Failure(PopupStoreRewardAccountSession session, string error)
        => new PopupStoreBusinessAccessSnapshot(session, null, null, 0, 0, null, error ?? "Access information unavailable.");
}

public interface IPopupStoreBusinessAccessInformation
{
    void QueryAccess(PopupStoreRewardAccountSession session, Action<PopupStoreBusinessAccessSnapshot> completed);
}
