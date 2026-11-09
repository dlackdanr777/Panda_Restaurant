using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public enum PopupStoreBusinessState { NotStarted, Running, Paused, Ended, Abandoned }

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
    [SerializeField] private bool _autoStartForEditorReview = false;
#endif

    private PopupStoreBusinessState _state;
    private double _remainingSeconds;
    private bool _viewVisible, _applicationPaused, _manualPaused, _exitConfirmationPaused;
    private bool _accountAccessSuspended;
    private PopupStoreAccountRecoveryPresentation _accountRecovery;
    private bool _applicationFocused = true;
    private bool _synchronizing, _starting, _hasTimeSample;
    private bool _sourceHealthy = true;
    private double _lastTimeSample;
    private string _lastError;
    // Tests may supply a read-only monotonic source. EditMode otherwise advances only through AdvanceTime.
    private Func<double> _timeSource;
    private readonly HashSet<string> _penalizedOrders = new HashSet<string>(StringComparer.Ordinal);
    private bool _cutoffLogged;
    private IPopupStorePhasePreviewClock _phasePreview;
    private bool _previewAdvancing;
    public bool IsPhasePreview => _phasePreview != null;
    public IPopupStorePhasePreviewClock PhasePreviewAuthority => _phasePreview;

    /// <summary>A fresh Editor instance opts in explicitly; an existing business can never be rebound.</summary>
    public bool TryBindPhasePreview(IPopupStorePhasePreviewClock authority)
    {
        if (authority == null || _state != PopupStoreBusinessState.NotStarted || _starting
            || GetComponent<UIPopupStore>() == null || (_phasePreview != null && !ReferenceEquals(_phasePreview, authority)))
            return false;
        _phasePreview = authority;
        SynchronizePreviewContext();
        return true;
    }

    /// <summary>Mirrors an adapter command such as packing/cleanup without reading or consuming time.</summary>
    public bool RefreshPhasePreview()
    {
        if (_phasePreview == null || _starting || _previewAdvancing) return false;
        double remaining = _phasePreview.RemainingSeconds;
        if (!Finite(remaining) || remaining < 0d)
        { _sourceHealthy = false; Report("The Editor phase preview returned an invalid remaining time."); return false; }
        // StateChanged consumers read CanCommitPacking. Their nested read must observe this
        // command's state, not start another time sample during a packing/cleanup callback.
        bool wasSynchronizing = _synchronizing;
        _synchronizing = true;
        try
        {
            _remainingSeconds = remaining;
            UpdateHud();
            Publish(StateChanged);
            return true;
        }
        finally { _synchronizing = wasSynchronizing; }
    }

    private void SynchronizePreviewContext()
        => _phasePreview?.SetContext(_viewVisible, _applicationPaused, _applicationFocused,
            _exitConfirmationPaused || _manualPaused);

    internal bool CanAdvancePatience
    {
        get
        {
            if (_phasePreview != null && !_phasePreview.CanPlay) return false;
            return _state == PopupStoreBusinessState.Running;
        }
    }

    public PopupStoreBusinessState State => _state;
    public bool IsManualPaused => _manualPaused;
    public bool IsExitConfirmationPaused => _exitConfirmationPaused;
    public uint VisualResumeVersion { get; private set; }
    public double RemainingSeconds => _remainingSeconds;
    /// <summary>Accepted Running time only, capped at the business deadline. Never advances while paused.</summary>
    public double ElapsedSeconds { get; private set; }
    public event Action<double> TimeAdvanced;
    public int ComplaintPenaltyCount => _penalizedOrders.Count;
    public double ComplaintPenaltySeconds { get; private set; }
    public bool CanAdvanceVisuals => _accountRecovery != null
        ? _accountRecovery.IsCurrent && IsViewContextAvailable()
        : _state != PopupStoreBusinessState.NotStarted && IsContextAvailable();
    public bool HasStartApproval => GetComponent<PopupStoreStartController>()?.IsApproved == true;
    internal bool CanPrepareForAdmission => _accountRecovery == null && _state == PopupStoreBusinessState.NotStarted && IsViewContextAvailable()
        && ValidateConfiguration(out _) && _timeText.isActiveAndEnabled;
    public int EndedCount { get; private set; }
    public int AbandonCount { get; private set; }
    public PopupStoreBusinessEndReason? EndReason => _state == PopupStoreBusinessState.Ended
        ? PopupStoreBusinessEndReason.Completed : _state == PopupStoreBusinessState.Abandoned
        ? PopupStoreBusinessEndReason.DirectExit : (PopupStoreBusinessEndReason?)null;
    public event Action StateChanged;
    // Presentation/input context can change while the business state stays Paused or Ended.
    public event Action ContextChanged;
    public event Action Ended;

    public bool CanStartCustomerEntry
    {
        get
        {
            SynchronizeNow();
            if (_phasePreview != null && !_phasePreview.CanEnter) return false;
            return CanUseRunningClock() && _remainingSeconds > _entryCutoffSeconds;
        }
    }

    public bool CanCommitPacking
    {
        get
        {
            SynchronizeNow();
            if (_phasePreview != null && !_phasePreview.CanPlay) return false;
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
            // An injected time source can invoke callbacks. Recheck admission, focus and visibility at commit.
            if (_state != PopupStoreBusinessState.NotStarted || !IsContextAvailable()
                || !ValidateConfiguration(out _) || !_timeText.isActiveAndEnabled) return false;
            // Freeze this approved business's reward policy before any start/terminal observer runs.
            // Preparation never draws, pays, or changes admission; an invalid policy remains a reward error.
            GetComponent<PopupStoreRewardSession>()?.PreparePolicyForApprovedStart();
            if (_phasePreview != null)
            {
                SynchronizePreviewContext();
                if (!_phasePreview.TryStart() || !Finite(_phasePreview.RemainingSeconds)
                    || _phasePreview.RemainingSeconds <= 0d) return false;
            }
            _sourceHealthy = true;
            _hasTimeSample = available;
            _lastTimeSample = now;
            _remainingSeconds = _initialSeconds;
            if (_phasePreview != null) _remainingSeconds = _phasePreview.RemainingSeconds;
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

    /// <summary>Irreversible local direct exit. Keeps committed history and remaining time; never publishes Ended.</summary>
    internal bool TryAbandonSession()
    {
        if (_starting) return false;
        SynchronizeNow();
        if (!_sourceHealthy || (_state != PopupStoreBusinessState.Running
            && _state != PopupStoreBusinessState.Paused)) return false;
        // Commit before observers can attempt packing, resume, or another exit.
        _state = PopupStoreBusinessState.Abandoned;
        _hasTimeSample = false;
        AbandonCount++;
        UpdateHud();
        LogState();
        Publish(StateChanged);
        Publish(ContextChanged);
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
            if (!HasStartApproval || _state != PopupStoreBusinessState.Running || !_viewVisible || _applicationPaused
                || !_applicationFocused || _manualPaused || _exitConfirmationPaused)
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

    public void SetManualPaused(bool paused) => SetCondition(ref _manualPaused, paused, true);

    // Owned by the local exit overlay, independently of app/focus and legacy manual pause.
    internal void SetExitConfirmationPaused(bool paused) => SetCondition(ref _exitConfirmationPaused, paused);
    internal void SetAccountAccessSuspended(bool suspended) => SetCondition(ref _accountAccessSuspended, suspended);
    internal TMP_Text PhaseTimeText => _timeText;
    internal void UsePhaseTimePlaceholder() { _notStartedText = "—"; UpdateHud(); }
    internal bool BindAccountRecovery(PopupStoreAccountRecoveryPresentation owner)
    {
        if (owner == null || owner.gameObject != gameObject || _accountRecovery != null || IsPhasePreview
            || _state != PopupStoreBusinessState.NotStarted) return false;
        _accountRecovery = owner;
        return true;
    }

    private void SetCondition(ref bool condition, bool value, bool activeSessionOnly = false)
    {
        if (condition == value) { UpdateHud(); return; }
        if (activeSessionOnly && (_state == PopupStoreBusinessState.NotStarted || IsTerminal)) return;
        SynchronizeNow();
        // The last foreground sample may finalize business. Never strand committed delivery behind a terminal manual pause.
        if (activeSessionOnly && (_state == PopupStoreBusinessState.NotStarted || IsTerminal)) return;
        bool wasAvailable = IsContextAvailable();
        condition = value;
        SynchronizePreviewContext();
        bool available = IsContextAvailable();
        if (!wasAvailable && available) unchecked { VisualResumeVersion++; }
        if (_state == PopupStoreBusinessState.NotStarted || IsTerminal)
        { _hasTimeSample = false; UpdateHud(); Publish(ContextChanged); return; }
        var next = available ? PopupStoreBusinessState.Running : PopupStoreBusinessState.Paused;
        // Observers also need context changes while another pause reason still holds the same enum state.
        if (next == _state) { Publish(ContextChanged); return; }
        _hasTimeSample = false;
        if (next == PopupStoreBusinessState.Running) ResetTimeAnchor();
        _state = next;
        UpdateHud();
        LogState();
        Publish(StateChanged);
    }

    /// <summary>Each visual consumer skips its first automatic update after resuming. Explicit test ticks keep their supplied delta.</summary>
    public float FilterAutomaticVisualDelta(float delta, ref uint observedResumeVersion)
    {
        if (!CanAdvanceVisuals) return 0f;
        if (observedResumeVersion != VisualResumeVersion)
        { observedResumeVersion = VisualResumeVersion; return 0f; }
        return delta;
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
        if (_phasePreview != null)
        {
            ConsumePreviewElapsed(elapsed);
            return;
        }
        double consumed = Math.Min(_remainingSeconds, elapsed);
        _remainingSeconds = Math.Max(0d, _remainingSeconds - elapsed);
        ElapsedSeconds += consumed;
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
        // Publish the committed absolute position before terminal notifications. Repeated or nested
        // observers cannot charge this interval twice, including the final partial frame at zero.
        double position = ElapsedSeconds;
        if (consumed > 0d && TimeAdvanced != null)
            foreach (Action<double> handler in TimeAdvanced.GetInvocationList())
                try { handler(position); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        if (!ended) return;
        LogState();
        Publish(StateChanged);
        Publish(Ended);
    }

    internal bool CanAcceptComplaintBatch(int count, double seconds)
        => count > 0 && count <= 3 && CanUseRunningClock() && Finite(seconds) && seconds >= 0
            && (_phasePreview == null || _phasePreview.CanPlay)
            && Finite(seconds * count) && Finite(ComplaintPenaltySeconds + seconds * count);

    /// <summary>One atomic batch at an already-settled Running instant. Penalties are not elapsed time.</summary>
    internal int ApplyComplaintPenalties(IReadOnlyList<PopupStorePatienceTimer> expired, double seconds)
    {
        if (!CanUseRunningClock() || expired == null || !Finite(seconds) || seconds < 0) return 0;
        var patience = GetComponent<PopupStoreCustomerPatience>();
        if (patience == null || !patience.isActiveAndEnabled) return 0;
        var accepted = new List<PopupStorePatienceTimer>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var timer in expired)
            if (timer != null && timer.Owner == patience && timer.Clock == this && timer.IsExpired
                && patience.IsCurrent(timer) && timer.Order.IsExpired && !timer.Order.IsPacked
                && !string.IsNullOrEmpty(timer.OrderId) && !_penalizedOrders.Contains(timer.OrderId)
                && identities.Add(timer.OrderId)) accepted.Add(timer);
        if (!CanAcceptComplaintBatch(accepted.Count, seconds)) return 0;
        double total = seconds * accepted.Count;
        if (!Finite(total) || !Finite(ComplaintPenaltySeconds + total)) return 0;
        // Consume every identity before any state/UI callback can re-enter, even if the batch reaches zero.
        foreach (var timer in accepted) _penalizedOrders.Add(timer.OrderId);
        double before = _remainingSeconds;
        if (_phasePreview != null)
        {
            // Reservations above precede reentrant observers. Only the phase owner deducts this batch;
            // no TimeAdvanced notification means the other customers lose no extra patience.
            if (!_phasePreview.TryPenalty(total))
            { _sourceHealthy = false; Report("The Editor phase preview rejected a committed complaint batch."); return 0; }
            ComplaintPenaltySeconds += total;
            _remainingSeconds = _phasePreview.RemainingSeconds;
            UpdateHud();
            LogCutoff();
            Publish(StateChanged);
            return accepted.Count;
        }
        ComplaintPenaltySeconds += total;
        _remainingSeconds = Math.Max(0, _remainingSeconds - total);
        bool ended = _remainingSeconds == 0;
        if (ended)
        {
            _state = PopupStoreBusinessState.Ended;
            _hasTimeSample = false;
            EndedCount++;
        }
        UpdateHud();
        LogCutoff();
#if UNITY_EDITOR
        if (Application.isPlaying)
            foreach (var timer in accepted)
                Debug.Log("POPUP_STORE_COMPLAINT_PENALTY order=" + timer.OrderId + "; customer=" + timer.CustomerId
                    + "; slot=" + timer.SlotIndex + "; seconds=" + seconds.ToString(CultureInfo.InvariantCulture)
                    + "; batch=" + accepted.Count + "; before=" + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; after=" + _remainingSeconds.ToString("0.###", CultureInfo.InvariantCulture), this);
#endif
        // No ElapsedSeconds or TimeAdvanced update: other customers pay only actual elapsed time.
        if (ended) LogState();
        Publish(StateChanged);
        if (ended) Publish(Ended);
        return accepted.Count;
    }

    private bool IsTerminal => _state == PopupStoreBusinessState.Ended || _state == PopupStoreBusinessState.Abandoned;

    private void ConsumePreviewElapsed(double elapsed)
    {
        if (_previewAdvancing) return;
        _previewAdvancing = true;
        try
        {
            SynchronizePreviewContext();
            bool playing = _phasePreview.CanPlay;
            double before = _phasePreview.RemainingSeconds;
            bool entering = _phasePreview.CanEnter && before > _entryCutoffSeconds;
            if (!Finite(before) || before < 0d)
            { _sourceHealthy = false; Report("The Editor phase preview returned an invalid remaining time."); return; }
            // Exactly one sample is forwarded. No transition loops or carried-over countdown deltas.
            _phasePreview.Advance(elapsed);
            double remaining = _phasePreview.RemainingSeconds;
            if (!Finite(remaining) || remaining < 0d)
            { _sourceHealthy = false; Report("The Editor phase preview returned an invalid remaining time."); return; }
            _remainingSeconds = remaining;
            double consumed = playing ? Math.Min(before, elapsed) : 0d;
            ElapsedSeconds += consumed;
            UpdateHud();
            LogCutoff();
            // The phase state is settled before customer observers. A phase deadline blocks new
            // expiration/packing commits, while already committed delivery remains free to finish.
            double position = ElapsedSeconds;
            if (consumed > 0d && TimeAdvanced != null)
                foreach (Action<double> handler in TimeAdvanced.GetInvocationList())
                    try { handler(position); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
            // Preview phases do not change the ordinary Running enum. Notify existing button
            // consumers when their phase gates change, after patience settles this same sample.
            if (playing != _phasePreview.CanPlay
                || entering != (_phasePreview.CanEnter && _phasePreview.RemainingSeconds > _entryCutoffSeconds))
            {
                bool wasSynchronizing = _synchronizing;
                _synchronizing = true;
                try { Publish(StateChanged); }
                finally { _synchronizing = wasSynchronizing; }
            }
        }
        catch (Exception exception)
        {
            _sourceHealthy = false;
            Report("The Editor phase preview failed: " + exception.GetType().Name);
        }
        finally { _previewAdvancing = false; }
    }

    private bool IsContextAvailable()
        => HasStartApproval && !_accountAccessSuspended && IsViewContextAvailable();

    private bool IsViewContextAvailable()
        => _state != PopupStoreBusinessState.Abandoned && isActiveAndEnabled && _viewVisible && !_applicationPaused && _applicationFocused
            && !_manualPaused && !_exitConfirmationPaused;

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
