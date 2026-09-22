using System;

public interface IGachaExchangeClock
{
    /// <summary>UTC from a trusted server anchor, or an explicitly supplied offline fake clock.</summary>
    bool TryGetUtcNow(out DateTime utc);
}

/// <summary>Never reads wall-clock/device date. The caller records only successful existing server responses.</summary>
public sealed class GachaExchangeAnchoredClock : IGachaExchangeClock
{
    private readonly Func<double> _monotonicSeconds;
    private DateTime _anchorUtc, _latestUtc;
    private double _anchorSeconds;
    private bool _hasAnchor;
    public GachaExchangeAnchoredClock(Func<double> monotonicSeconds)
        => _monotonicSeconds = monotonicSeconds ?? throw new ArgumentNullException(nameof(monotonicSeconds));
    public void RecordServerUtc(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Server anchor must be UTC.");
        double now = _monotonicSeconds();
        if (double.IsNaN(now) || double.IsInfinity(now)) return;
        if (_hasAnchor && TryGetUtcNow(out var current) && utc < current) utc = current;
        _anchorUtc = _latestUtc = utc; _anchorSeconds = now; _hasAnchor = true;
    }
    public bool TryGetUtcNow(out DateTime utc)
    {
        utc = default;
        if (!_hasAnchor) return false;
        double elapsed = _monotonicSeconds() - _anchorSeconds;
        if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) return false;
        try { utc = _anchorUtc.AddSeconds(elapsed); }
        catch (ArgumentOutOfRangeException) { return false; }
        if (utc < _latestUtc) utc = _latestUtc;
        _latestUtc = utc; return true;
    }
}

public sealed class GachaExchangeRefreshStatus
{
    public int Remaining { get; }
    public int Limit => 3;
    public bool ClockReady { get; }
    public string DayKey { get; }
    public string Message { get; }
    internal GachaExchangeRefreshStatus(int remaining, bool ready, string day, string message)
    { Remaining = remaining; ClockReady = ready; DayKey = day; Message = message; }
}
