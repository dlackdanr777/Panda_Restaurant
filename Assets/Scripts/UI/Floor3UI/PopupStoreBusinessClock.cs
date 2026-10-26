using System;
using System.Globalization;
using TMPro;
using UnityEngine;

public enum PopupStoreBusinessState { NotStarted, Running, Paused, Ended }

/// <summary>A popup-instance review clock. No daily limits, rewards, account storage or global time-scale changes.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreBusinessClock : MonoBehaviour
{
    [SerializeField, Min(0)] private double _initialSeconds = 90d;
    [SerializeField, Min(0)] private double _entryCutoffSeconds = 10d;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private string _timeFormat = "남은 시간 {0:00}:{1:00}";
    [SerializeField] private string _notStartedText = "남은 시간 —";
#if UNITY_EDITOR
    [Tooltip("Temporary PC review only. Player builds require an explicit session start.")]
    [SerializeField] private bool _autoStartForEditorReview = true;
#endif

    private PopupStoreBusinessState _state;
    private double _remainingSeconds;
    private bool _viewVisible, _applicationPaused, _manualPaused;
    private bool _applicationFocused = true;
    private bool _synchronizing, _starting, _hasTimeSample;
    private bool _sourceHealthy = true;
    private double _lastTimeSample;
    private string _lastError;
    // Tests may supply a read-only monotonic source. EditMode otherwise advances only through AdvanceTime.
    private Func<double> _timeSource;
#if UNITY_EDITOR
    private bool _cutoffLogged;
#endif

    public PopupStoreBusinessState State => _state;
    public double RemainingSeconds => _remainingSeconds;
    public int EndedCount { get; private set; }
    public event Action StateChanged;
    public event Action Ended;

    public bool CanStartCustomerEntry
    {
        get
        {
            SynchronizeNow();
            return CanUseRunningClock() && _remainingSeconds > _entryCutoffSeconds;
        }
    }

    public bool CanCommitPacking
    {
        get
        {
            SynchronizeNow();
            return CanUseRunningClock() && _remainingSeconds > 0d;
        }
    }

    public bool TryStartSession()
    {
        if (_starting || _state != PopupStoreBusinessState.NotStarted || !IsContextAvailable()) return false;
        if (!ValidateConfiguration(out string error)) { Report(error); return false; }
        if (!_timeText.isActiveAndEnabled)
        { Report("The time label must be active before starting a session."); return false; }
        _starting = true;
        try
        {
            if (!TryReadTime(out double now, out bool available, out error))
            { _sourceHealthy = false; Report(error); return false; }
            _sourceHealthy = true;
            _hasTimeSample = available;
            _lastTimeSample = now;
            _remainingSeconds = _initialSeconds;
            _lastError = null;
            bool endedAtStart = _remainingSeconds == 0d;
            if (endedAtStart)
            {
                _state = PopupStoreBusinessState.Ended;
                _hasTimeSample = false;
                EndedCount++;
            }
            else _state = PopupStoreBusinessState.Running;
            UpdateHud();
            LogState();
            LogCutoff();
            // Initialization is committed. Gates in observers may now settle newer real time normally.
            _starting = false;
            Publish(StateChanged);
            if (endedAtStart) Publish(Ended);
            return true;
        }
        finally { _starting = false; }
    }

    /// <summary>Explicit local elapsed time, used by memory tests and the one-way Editor review shortcut.</summary>
    public bool AdvanceTime(double elapsed)
    {
        if (!Finite(elapsed) || elapsed < 0d || _starting || _state != PopupStoreBusinessState.Running
            || !IsContextAvailable() || !ValidateConfiguration(out _)) return false;
        ConsumeElapsed(elapsed);
        return true;
    }

    /// <summary>Samples once before transaction gates as well as Update; repeated reads cannot consume a delta twice.</summary>
    public void SynchronizeNow()
    {
        if (_synchronizing || _starting) return;
        _synchronizing = true;
        try
        {
            // SetViewVisible(false) can settle the final foreground interval during OnDisable.
            if (_state != PopupStoreBusinessState.Running || !_viewVisible || _applicationPaused
                || !_applicationFocused || _manualPaused)
            { _hasTimeSample = false; return; }
            if (!ValidateConfiguration(out _)) return;
            if (!TryReadTime(out double now, out bool available, out string error))
            { _sourceHealthy = false; Report(error); return; }
            if (!available) { _sourceHealthy = true; _hasTimeSample = false; return; }
            if (!_hasTimeSample)
            {
                _sourceHealthy = true;
                _hasTimeSample = true;
                _lastTimeSample = now;
                return;
            }
            double elapsed = now - _lastTimeSample;
            if (!Finite(elapsed) || elapsed < 0d)
            { _sourceHealthy = false; Report("The local time source must advance monotonically."); return; }
            _sourceHealthy = true;
            // Commit the sample before callbacks. A nested CanCommitPacking read sees the settled state.
            _lastTimeSample = now;
            ConsumeElapsed(elapsed);
        }
        finally { _synchronizing = false; }
    }

    public void SetViewVisible(bool visible)
    {
        SetCondition(ref _viewVisible, visible);
        if (visible) TryEditorReviewStart();
    }

    public void SetApplicationPaused(bool paused)
    {
        SetCondition(ref _applicationPaused, paused);
        if (!paused) TryEditorReviewStart();
    }

    public void SetApplicationFocused(bool focused)
    {
        SetCondition(ref _applicationFocused, focused);
        if (focused) TryEditorReviewStart();
    }

    public void SetManualPaused(bool paused) => SetCondition(ref _manualPaused, paused);

    private void SetCondition(ref bool condition, bool value)
    {
        if (condition == value) { UpdateHud(); return; }
        SynchronizeNow();
        condition = value;
        if (_state == PopupStoreBusinessState.NotStarted || _state == PopupStoreBusinessState.Ended)
        { _hasTimeSample = false; UpdateHud(); return; }
        var next = IsContextAvailable() ? PopupStoreBusinessState.Running : PopupStoreBusinessState.Paused;
        if (next == _state) return;
        _hasTimeSample = false;
        if (next == PopupStoreBusinessState.Running) ResetTimeAnchor();
        _state = next;
        UpdateHud();
        LogState();
        Publish(StateChanged);
    }

    private void ResetTimeAnchor()
    {
        if (!TryReadTime(out double now, out bool available, out string error))
        { _sourceHealthy = false; Report(error); return; }
        _sourceHealthy = true;
        _hasTimeSample = available;
        _lastTimeSample = now;
    }

    private bool TryReadTime(out double now, out bool available, out string error)
    {
        now = 0d;
        error = null;
        available = _timeSource != null || Application.isPlaying;
        if (!available) return true;
        try { now = _timeSource != null ? _timeSource() : Time.realtimeSinceStartupAsDouble; }
        catch (Exception exception)
        { error = "The local time source failed: " + exception.GetType().Name; return false; }
        if (!Finite(now) || now < 0d)
        { error = "The local time source must return a finite non-negative value."; return false; }
        return true;
    }

    private void ConsumeElapsed(double elapsed)
    {
        if (_state != PopupStoreBusinessState.Running) return;
        _remainingSeconds = Math.Max(0d, _remainingSeconds - elapsed);
        bool ended = _remainingSeconds == 0d;
        if (ended)
        {
            // Terminal state is irreversible and visible before any observer or packing gate runs.
            _state = PopupStoreBusinessState.Ended;
            _hasTimeSample = false;
            EndedCount++;
        }
        UpdateHud();
        LogCutoff();
        if (!ended) return;
        LogState();
        Publish(StateChanged);
        Publish(Ended);
    }

    private bool IsContextAvailable()
        => isActiveAndEnabled && _viewVisible && !_applicationPaused && _applicationFocused && !_manualPaused;

    private bool CanUseRunningClock()
        => _sourceHealthy && _state == PopupStoreBusinessState.Running && IsContextAvailable()
            && ValidateConfiguration(out _) && _timeText.isActiveAndEnabled;

    private bool ValidateConfiguration(out string error)
    {
        error = null;
        var popup = GetComponent<UIPopupStore>();
        if (popup == null || _timeText == null || !_timeText.enabled
            || _timeText.GetComponentInParent<UIPopupStore>(true) != popup)
            error = "The time label must belong to this popup store and remain enabled.";
        else if (!Finite(_initialSeconds) || _initialSeconds < 0d
            || !Finite(_entryCutoffSeconds) || _entryCutoffSeconds < 0d)
            error = "Business duration and entry cutoff must be finite and non-negative.";
        else if (string.IsNullOrEmpty(_timeFormat)) error = "A time display format is required.";
        else
        {
            try { string.Format(CultureInfo.InvariantCulture, _timeFormat, 0d, 0d); }
            catch (FormatException) { error = "The time display format must accept minute and second arguments."; }
        }
        return error == null;
    }

    private void UpdateHud()
    {
        if (!ValidateConfiguration(out _)) return;
        string text;
        if (_state == PopupStoreBusinessState.NotStarted) text = _notStartedText ?? string.Empty;
        else
        {
            double seconds = Math.Ceiling(_remainingSeconds);
            text = string.Format(CultureInfo.InvariantCulture, _timeFormat, Math.Floor(seconds / 60d), seconds % 60d);
        }
        if (_timeText.text != text) _timeText.text = text;
    }

    private void Publish(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void Report(string error)
    {
        if (_lastError == error) return;
        _lastError = error;
        Debug.LogError("PopupStoreBusinessClock: " + error, this);
    }

    private void LogState()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_BUSINESS_STATE state=" + _state + "; remaining="
                + _remainingSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "; ended=" + EndedCount, this);
#endif
    }

    private void LogCutoff()
    {
#if UNITY_EDITOR
        if (_cutoffLogged || _state == PopupStoreBusinessState.NotStarted || _remainingSeconds > _entryCutoffSeconds) return;
        _cutoffLogged = true;
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_BUSINESS_ENTRY_CLOSED remaining="
                + _remainingSeconds.ToString("0.###", CultureInfo.InvariantCulture), this);
#endif
    }

    private void TryEditorReviewStart()
    {
#if UNITY_EDITOR
        if (Application.isPlaying && _autoStartForEditorReview && _state == PopupStoreBusinessState.NotStarted)
            TryStartSession();
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Review/Shorten Remaining Time To 20 Seconds")]
    private void ShortenReviewToTwentySeconds()
    {
        if (!Application.isPlaying) return;
        SynchronizeNow();
        if (_state == PopupStoreBusinessState.Running && _remainingSeconds > 20d)
            AdvanceTime(_remainingSeconds - 20d);
    }

    [ContextMenu("Review/Inspect Business Clock")]
    private void InspectBusinessClock()
    {
        SynchronizeNow();
        Debug.Log("POPUP_STORE_BUSINESS_CLOCK state=" + _state + "; remaining="
            + _remainingSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "; visible=" + _viewVisible
            + "; appPaused=" + _applicationPaused + "; focused=" + _applicationFocused
            + "; manualPaused=" + _manualPaused + "; ended=" + EndedCount, this);
    }
#endif

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private void OnEnable()
    {
        if (Application.isPlaying) _applicationFocused = Application.isFocused;
        SetViewVisible(true);
    }
    private void OnDisable() => SetViewVisible(false);
    private void OnApplicationPause(bool paused) => SetApplicationPaused(paused);
    private void OnApplicationFocus(bool focused) => SetApplicationFocused(focused);
    private void Update() => SynchronizeNow();
}
