using System;
using System.Collections.Generic;
using System.Globalization;

public enum PopupStorePhaseState { Ready, Playing, Clearing, ClearNotice, Countdown, Completed, TimedOut, Abandoned, Faulted }
public enum PopupStorePhasePauseReason { ApplicationBackground, FocusLost, ViewHidden, ExitConfirmation }
public enum PopupStorePhaseCleanupStatus { Completed, Failed, Cancelled }

/// <summary>
/// An adapter assertion, not proof of an authorized sale. The future adapter must verify
/// Packing.IsCommittedReceipt and the owning business/phase before submitting this value.
/// </summary>
public sealed class PopupStorePhasePackingEvidence
{
    public string BusinessSessionId { get; }
    public string PhaseRunId { get; }
    public string OrderId { get; }
    public string ReceiptId { get; }
    public PopupStorePhasePackingEvidence(string businessSessionId, string phaseRunId, string orderId, string receiptId)
    { BusinessSessionId = businessSessionId; PhaseRunId = phaseRunId; OrderId = orderId; ReceiptId = receiptId; }
}

/// <summary>The external presentation owner acknowledges this specific phase, never a phase number alone.</summary>
public sealed class PopupStorePhaseCleanupSignal
{
    public string BusinessSessionId { get; }
    public string PhaseRunId { get; }
    public PopupStorePhaseCleanupStatus Status { get; }
    public PopupStorePhaseCleanupSignal(string businessSessionId, string phaseRunId, PopupStorePhaseCleanupStatus status)
    { BusinessSessionId = businessSessionId; PhaseRunId = phaseRunId; Status = status; }
}

/// <summary>
/// Explicit-command phase core, deliberately disconnected from the current game. No clock source,
/// events, receipt production, account writes or reward calls. The adapter settles a shared time
/// sample before submitting packing and never forwards accumulated background time on resume.
/// </summary>
public sealed class PopupStorePhaseSession
{
    private enum CleanupDestination { NextPhase, TimedOut, Abandoned }
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly HashSet<string> _receiptIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _orderIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<PopupStorePhasePauseReason> _pauseReasons = new HashSet<PopupStorePhasePauseReason>();
    private readonly List<int> _cleared = new List<int>();
    private readonly IReadOnlyList<int> _clearedView;
    private readonly List<PopupStorePhaseProgressSummary> _finishedSummaries = new List<PopupStorePhaseProgressSummary>();
    private int _phaseIndex;
    private CleanupDestination _cleanupDestination;
    private bool _currentCleared;

    public string BusinessSessionId { get; }
    public PopupStorePhaseProfileSnapshot Profile { get; }
    public PopupStorePhaseState State { get; private set; } = PopupStorePhaseState.Ready;
    public PopupStorePhaseSnapshot CurrentPhase => Profile.Phases[_phaseIndex];
    public string PhaseRunId { get; private set; } = string.Empty;
    public int PhaseGeneration { get; private set; }
    public double RemainingSeconds { get; private set; }
    public double TransitionRemainingSeconds { get; private set; }
    public int CountdownValue { get; private set; }
    public int PhaseSuccessfulOrders { get; private set; }
    public long TotalSuccessfulOrders { get; private set; }
    public PopupStoreGradeTier HighestClearedGrade { get; private set; } = PopupStoreGradeTier.None;
    public IReadOnlyList<int> ClearedPhases => _clearedView;
    public PopupStorePhaseResult FinalResult { get; private set; }
    public int FinalizationCount { get; private set; }
    public int TransitionCount { get; private set; }
    public bool IsPaused => _pauseReasons.Count != 0;
    public bool IsTerminal => FinalResult != null;
    public bool IsAwaitingCleanup => State == PopupStorePhaseState.Clearing;
    public PopupStorePhaseEndReason? PendingEndReason => State != PopupStorePhaseState.Clearing
        || _cleanupDestination == CleanupDestination.NextPhase ? (PopupStorePhaseEndReason?)null
        : _cleanupDestination == CleanupDestination.TimedOut ? PopupStorePhaseEndReason.TimedOut : PopupStorePhaseEndReason.Abandoned;
    public bool CanAcceptPacking => State == PopupStorePhaseState.Playing && !IsPaused && RemainingSeconds > 0;
    public bool CanStartCustomerEntry => CanAcceptPacking;
    public IReadOnlyList<PopupStorePhasePauseReason> PauseReasons
    {
        get
        {
            var copy = new List<PopupStorePhasePauseReason>();
            foreach (PopupStorePhasePauseReason reason in Enum.GetValues(typeof(PopupStorePhasePauseReason)))
                if (_pauseReasons.Contains(reason)) copy.Add(reason);
            return copy.AsReadOnly();
        }
    }

    private PopupStorePhaseSession(string businessSessionId, PopupStorePhaseProfileSnapshot profile)
    { BusinessSessionId = businessSessionId; Profile = profile; _clearedView = _cleared.AsReadOnly(); }

    public static bool TryCreate(string businessSessionId, PopupStorePhaseProfileSnapshot profile,
        out PopupStorePhaseSession session, out string error)
    {
        session = null; error = null;
        if (string.IsNullOrWhiteSpace(businessSessionId)) error = "A business session ID is required.";
        else if (profile == null || profile.Phases.Count != 6)
            error = "A validated six-phase profile snapshot is required.";
        if (error != null) return false;
        session = new PopupStorePhaseSession(businessSessionId, profile);
        return true;
    }

    public bool TryStart()
    {
        if (State != PopupStorePhaseState.Ready || IsPaused) return false;
        BeginPhase();
        return true;
    }

    public bool TryRecordPacking(PopupStorePhasePackingEvidence evidence)
    {
        if (!CanAcceptPacking || evidence == null || !IdentityMatches(evidence.BusinessSessionId, evidence.PhaseRunId)
            || string.IsNullOrWhiteSpace(evidence.OrderId) || string.IsNullOrWhiteSpace(evidence.ReceiptId)
            || _receiptIds.Contains(evidence.ReceiptId) || _orderIds.Contains(evidence.OrderId)) return false;
        _receiptIds.Add(evidence.ReceiptId);
        _orderIds.Add(evidence.OrderId);
        PhaseSuccessfulOrders++;
        TotalSuccessfulOrders++;
        if (PhaseSuccessfulOrders == CurrentPhase.TargetSuccessfulOrders)
        {
            _currentCleared = true;
            _cleared.Add(CurrentPhase.PhaseNumber);
            HighestClearedGrade = CurrentPhase.Grade;
            _cleanupDestination = CleanupDestination.NextPhase;
            ChangeState(PopupStorePhaseState.Clearing);
        }
        return true;
    }

    /// <summary>
    /// Paused cleanup acknowledgments are rejected and must be retried after all pause reasons clear.
    /// Failed/cancelled cleanup is terminal Faulted, never a fabricated successful delivery.
    /// </summary>
    public bool TryCompleteCleanup(PopupStorePhaseCleanupSignal signal)
    {
        if (State != PopupStorePhaseState.Clearing || IsPaused || signal == null
            || !IdentityMatches(signal.BusinessSessionId, signal.PhaseRunId)
            || !Enum.IsDefined(typeof(PopupStorePhaseCleanupStatus), signal.Status)) return false;
        if (signal.Status != PopupStorePhaseCleanupStatus.Completed)
            Finalize(signal.Status == PopupStorePhaseCleanupStatus.Failed
                ? PopupStorePhaseEndReason.CleanupFailed : PopupStorePhaseEndReason.CleanupCancelled);
        else if (_cleanupDestination == CleanupDestination.TimedOut) Finalize(PopupStorePhaseEndReason.TimedOut);
        else if (_cleanupDestination == CleanupDestination.Abandoned) Finalize(PopupStorePhaseEndReason.Abandoned);
        else
        {
            TransitionRemainingSeconds = CurrentPhase.StageClearNoticeSeconds;
            ChangeState(PopupStorePhaseState.ClearNotice);
        }
        return true;
    }

    /// <summary>
    /// One explicit sample advances at most one visible transition. Excess delta is discarded;
    /// it never skips 3/2/1 or reduces the next phase's full playing time.
    /// </summary>
    public bool TryAdvanceTime(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0 || IsPaused || IsTerminal)
            return false;
        switch (State)
        {
            case PopupStorePhaseState.Playing:
                RemainingSeconds = Math.Max(0, RemainingSeconds - elapsedSeconds);
                if (RemainingSeconds == 0)
                {
                    _cleanupDestination = CleanupDestination.TimedOut;
                    ChangeState(PopupStorePhaseState.Clearing);
                }
                return true;
            case PopupStorePhaseState.ClearNotice:
                TransitionRemainingSeconds = Math.Max(0, TransitionRemainingSeconds - elapsedSeconds);
                if (TransitionRemainingSeconds == 0)
                {
                    if (_phaseIndex == Profile.Phases.Count - 1) Finalize(PopupStorePhaseEndReason.Completed);
                    else
                    {
                        CountdownValue = 3;
                        TransitionRemainingSeconds = CurrentPhase.CountdownStepSeconds;
                        ChangeState(PopupStorePhaseState.Countdown);
                    }
                }
                return true;
            case PopupStorePhaseState.Countdown:
                TransitionRemainingSeconds = Math.Max(0, TransitionRemainingSeconds - elapsedSeconds);
                if (TransitionRemainingSeconds == 0)
                {
                    CountdownValue--;
                    if (CountdownValue == 0)
                    {
                        _finishedSummaries.Add(CurrentSummary());
                        _phaseIndex++;
                        BeginPhase();
                    }
                    else TransitionRemainingSeconds = CurrentPhase.CountdownStepSeconds;
                }
                return true;
            default: return false;
        }
    }

    public bool TrySetPauseReason(PopupStorePhasePauseReason reason, bool paused)
    {
        if (IsTerminal || !Enum.IsDefined(typeof(PopupStorePhasePauseReason), reason)) return false;
        return paused ? _pauseReasons.Add(reason) : _pauseReasons.Remove(reason);
    }

    /// <summary>
    /// Abandon is explicit even while paused. Active work waits for cleanup; an already accepted
    /// timeout cannot be relabelled. Ready/notice/countdown have no outstanding work and end directly.
    /// </summary>
    public bool TryAbandon()
    {
        if (IsTerminal) return false;
        if (State == PopupStorePhaseState.Ready || State == PopupStorePhaseState.ClearNotice || State == PopupStorePhaseState.Countdown)
        { Finalize(PopupStorePhaseEndReason.Abandoned); return true; }
        if (State == PopupStorePhaseState.Clearing)
        {
            if (_cleanupDestination != CleanupDestination.NextPhase) return false;
            _cleanupDestination = CleanupDestination.Abandoned;
            return true;
        }
        if (State != PopupStorePhaseState.Playing) return false;
        _cleanupDestination = CleanupDestination.Abandoned;
        ChangeState(PopupStorePhaseState.Clearing);
        return true;
    }

    private bool IdentityMatches(string businessSessionId, string runId)
        => string.Equals(BusinessSessionId, businessSessionId, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(PhaseRunId) && string.Equals(PhaseRunId, runId, StringComparison.Ordinal);

    private void BeginPhase()
    {
        PhaseGeneration++;
        PhaseRunId = _instanceId + ":" + PhaseGeneration.ToString(CultureInfo.InvariantCulture);
        PhaseSuccessfulOrders = 0;
        RemainingSeconds = CurrentPhase.TimeLimitSeconds;
        TransitionRemainingSeconds = 0;
        CountdownValue = 0;
        _currentCleared = false;
        ChangeState(PopupStorePhaseState.Playing);
    }

    private PopupStorePhaseProgressSummary CurrentSummary()
        => new PopupStorePhaseProgressSummary(CurrentPhase.PhaseNumber, PhaseRunId, CurrentPhase.Grade,
            PhaseSuccessfulOrders, CurrentPhase.TimeLimitSeconds - RemainingSeconds, _currentCleared);

    private void Finalize(PopupStorePhaseEndReason reason)
    {
        if (FinalResult != null) return;
        var summaries = new List<PopupStorePhaseProgressSummary>(_finishedSummaries);
        if (PhaseGeneration > 0) summaries.Add(CurrentSummary());
        FinalResult = new PopupStorePhaseResult(BusinessSessionId, Profile, reason,
            PhaseGeneration == 0 ? 0 : CurrentPhase.PhaseNumber, HighestClearedGrade, TotalSuccessfulOrders, _cleared, summaries);
        FinalizationCount++;
        TransitionRemainingSeconds = 0;
        CountdownValue = 0;
        ChangeState(reason == PopupStorePhaseEndReason.Completed ? PopupStorePhaseState.Completed
            : reason == PopupStorePhaseEndReason.TimedOut ? PopupStorePhaseState.TimedOut
            : reason == PopupStorePhaseEndReason.Abandoned ? PopupStorePhaseState.Abandoned : PopupStorePhaseState.Faulted);
    }

    private void ChangeState(PopupStorePhaseState state)
    {
        if (State == state) return;
        State = state;
        TransitionCount++;
    }
}
