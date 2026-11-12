using System;
using System.Collections.Generic;

public enum PopupStoreBusinessStartState
{
    Prepared, Submitting, Started, RejectedBeforeSend, Indeterminate,
    ReconciliationRequired, Conflict, Invalidated, NotAppliedConfirmed
}

/// <summary>One device's start-admission context, not a global manager or persisted account state.
/// Reuse it across that device's executors. An unresolved account request blocks a new request ID
/// until the original is reconciled. Independent devices have independent contexts; the authority
/// still enforces the shared quota. A future durable host must restore pending requests before use.</summary>
public sealed class PopupStoreBusinessStartContext
{
    internal sealed class Lease
    {
        internal readonly PopupStoreBusinessStartRequest Request;
        internal Lease(PopupStoreBusinessStartRequest request) { Request = request; }
    }
    private readonly object _gate = new object();
    private readonly Dictionary<string, Lease> _pending = new Dictionary<string, Lease>(StringComparer.Ordinal);

    public PopupStoreBusinessStartRequest GetPendingRequest(string accountId)
    {
        if (accountId == null) return null;
        lock (_gate) return _pending.TryGetValue(accountId, out var lease) ? lease.Request : null;
    }

    internal bool TryEnter(PopupStoreBusinessStartRequest request, bool query, out Lease lease)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(request.AccountId, out lease))
                return query && lease.Request.Matches(request);
            lease = new Lease(request);
            _pending.Add(request.AccountId, lease);
            return true;
        }
    }

    internal void Resolve(Lease lease)
    {
        if (lease == null) return;
        lock (_gate)
            if (_pending.TryGetValue(lease.Request.AccountId, out var current) && ReferenceEquals(lease, current))
                _pending.Remove(lease.Request.AccountId);
    }
}

/// <summary>Diagnostic memory checkpoint, never authority for an approved start or consumed quota.</summary>
public sealed class PopupStoreBusinessStartCheckpoint
{
    public PopupStoreBusinessStartRequest Request { get; }
    public PopupStoreBusinessStartState ObservedState { get; }
    public string BusinessSessionId { get; }
    internal PopupStoreBusinessStartCheckpoint(PopupStoreBusinessStartRequest request,
        PopupStoreBusinessStartState state, string businessSessionId)
    { Request = request; ObservedState = state; BusinessSessionId = businessSessionId; }
}

/// <summary>Account/session-bound admission only. No play transition, reward draw, payment or live
/// account mutation. Callbacks and public calls on one executor must be serialized by its host.</summary>
public sealed class PopupStoreBusinessStartTransaction
{
    private readonly IPopupStoreBusinessStartPersistence _repository;
    private readonly Func<PopupStoreRewardAccountSession> _readSession;
    private readonly PopupStoreRewardAccountSession _session;
    private readonly PopupStoreBusinessStartContext _context;
    private readonly Action<PopupStoreBusinessStartRecord> _completeLocally;
    private readonly string _expectedRecoveredBusiness;
    private PopupStoreBusinessStartContext.Lease _lease;
    private long _operation;
    private bool _terminalOperation, _waiting, _completing, _checkingSession;

    public PopupStoreBusinessStartRequest Request { get; }
    public PopupStoreBusinessStartState State { get; private set; }
    public PopupStoreBusinessStartRecord Record { get; private set; }
    public string LastError { get; private set; }
    public int SubmitCount { get; private set; }
    public int QueryCount { get; private set; }
    public int ConfirmationCount { get; private set; }
    public bool LocalCompletionPending { get; private set; }

    public PopupStoreBusinessStartTransaction(PopupStoreBusinessStartRequest request,
        IPopupStoreBusinessStartPersistence repository, Func<PopupStoreRewardAccountSession> readSession,
        PopupStoreBusinessStartContext context, Action<PopupStoreBusinessStartRecord> completeLocally = null)
        : this(request, repository, readSession, context, completeLocally, null) { }

    private PopupStoreBusinessStartTransaction(PopupStoreBusinessStartRequest request,
        IPopupStoreBusinessStartPersistence repository, Func<PopupStoreRewardAccountSession> readSession,
        PopupStoreBusinessStartContext context, Action<PopupStoreBusinessStartRecord> completeLocally,
        PopupStoreBusinessStartCheckpoint checkpoint)
    {
        Request = request;
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _readSession = readSession ?? throw new ArgumentNullException(nameof(readSession));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _completeLocally = completeLocally;
        _expectedRecoveredBusiness = checkpoint?.BusinessSessionId;
        try { _session = _readSession(); } catch { _session = null; }
        State = checkpoint == null ? PopupStoreBusinessStartState.Prepared : PopupStoreBusinessStartState.ReconciliationRequired;
        if (!ValidateRequest() || !EnsureCurrent()) return;
        if (checkpoint != null && !_context.TryEnter(Request, true, out _lease))
        { State = PopupStoreBusinessStartState.Conflict; LastError = "Another unresolved start must be recovered first."; }
    }

    public PopupStoreBusinessStartCheckpoint Capture()
        => new PopupStoreBusinessStartCheckpoint(Request, State, Record?.BusinessSessionId ?? _expectedRecoveredBusiness);

    public static PopupStoreBusinessStartTransaction Restore(PopupStoreBusinessStartCheckpoint checkpoint,
        IPopupStoreBusinessStartPersistence repository, Func<PopupStoreRewardAccountSession> readSession,
        PopupStoreBusinessStartContext context, Action<PopupStoreBusinessStartRecord> completeLocally = null)
    {
        if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
        return new PopupStoreBusinessStartTransaction(checkpoint.Request, repository, readSession, context, completeLocally, checkpoint);
    }

    public bool TrySubmit()
    {
        if (!EnsureCurrent() || !ValidateRequest()) return false;
        if (State == PopupStoreBusinessStartState.Started) return true;
        if (_waiting || (State != PopupStoreBusinessStartState.Prepared
            && State != PopupStoreBusinessStartState.RejectedBeforeSend
            && State != PopupStoreBusinessStartState.NotAppliedConfirmed)) return false;
        return Send(false);
    }

    public bool TryReconcile()
    {
        if (!EnsureCurrent() || !ValidateRequest()) return false;
        if (State == PopupStoreBusinessStartState.Started) return true;
        if (_waiting || State == PopupStoreBusinessStartState.Submitting || State == PopupStoreBusinessStartState.Conflict)
            return false;
        return Send(true);
    }

    private bool Send(bool query)
    {
        if (!_context.TryEnter(Request, query, out var lease))
        { LastError = "This device must reconcile its original pending start before sending any new start."; return false; }
        _lease = lease;
        long operation = ++_operation;
        _terminalOperation = false; _waiting = true; LastError = null;
        State = query ? PopupStoreBusinessStartState.ReconciliationRequired : PopupStoreBusinessStartState.Submitting;
        try
        {
            if (query)
            { QueryCount++; _repository.Query(_session, Request, response => Receive(operation, true, response)); }
            else
            { SubmitCount++; _repository.Submit(_session, Request, response => Receive(operation, false, response)); }
        }
        catch (Exception error)
        {
            if (operation == _operation && !_terminalOperation && EnsureCurrent())
                Unknown(query, "Start repository outcome is unknown: " + error.Message);
        }
        return true; // Dispatched, not a promise of successful admission.
    }

    private void Receive(long operation, bool query, PopupStoreBusinessStartResponse response)
    {
        if (operation != _operation || _terminalOperation || !EnsureCurrent()
            || operation != _operation || _terminalOperation) return;
        if (response == null || response.Request == null || !Request.Matches(response.Request))
        { Unknown(query, "Uncorrelated reply; reconcile the original start request."); return; }
        if (response.Kind != PopupStoreBusinessStartResponseKind.Started && response.Record != null)
        { Unknown(query, "Contradictory non-success reply contains a start record."); return; }
        switch (response.Kind)
        {
            case PopupStoreBusinessStartResponseKind.Started:
                if (response.Record == null || !response.Record.Matches(Request)
                    || (_expectedRecoveredBusiness != null && !string.Equals(_expectedRecoveredBusiness,
                        response.Record.BusinessSessionId, StringComparison.Ordinal)))
                { Unknown(query, "The start record does not match the original request/checkpoint."); return; }
                Record = response.Record;
                Finish(PopupStoreBusinessStartState.Started);
                ConfirmationCount++;
                LocalCompletionPending = _completeLocally != null;
                RetryLocalCompletion();
                break;
            case PopupStoreBusinessStartResponseKind.RejectedBeforeSend:
                if (query) Unknown(true, response.Error ?? "A query rejection cannot prove the original start had no effect.");
                else Finish(PopupStoreBusinessStartState.RejectedBeforeSend, response.Error);
                break;
            case PopupStoreBusinessStartResponseKind.NotAppliedConfirmed:
                if (query) Finish(PopupStoreBusinessStartState.NotAppliedConfirmed, response.Error);
                else Unknown(false, "Only authoritative reconciliation may prove no start was committed.");
                break;
            case PopupStoreBusinessStartResponseKind.Conflict:
                // Retain the pending request until an authority/recovery path resolves the conflict.
                Finish(PopupStoreBusinessStartState.Conflict, response.Error, false);
                break;
            case PopupStoreBusinessStartResponseKind.NotFound:
                Unknown(true, response.Error ?? "A missing read does not authorize another start.");
                break;
            default:
                Unknown(query, response.Error ?? "Start outcome remains unknown.");
                break;
        }
    }

    public void MarkResponseMissing()
    {
        if (!EnsureCurrent() || _terminalOperation) return;
        if (State == PopupStoreBusinessStartState.Submitting)
            Unknown(false, "Start response missing; reconcile the same request.");
        else if (State == PopupStoreBusinessStartState.ReconciliationRequired)
            Unknown(true, "Start reconciliation response missing.");
    }

    /// <summary>Idempotent local handling keyed by BusinessSessionId only. It must not consume quota,
    /// draw a reward or perform a payout. An exception cannot undo the already approved business.</summary>
    public bool RetryLocalCompletion()
    {
        if (!EnsureCurrent() || State != PopupStoreBusinessStartState.Started || Record == null || _completing)
            return false;
        if (!LocalCompletionPending) return true;
        _completing = true;
        try
        {
            _completeLocally(Record);
            if (!EnsureCurrent()) return false;
            LocalCompletionPending = false; LastError = null; return true;
        }
        catch (Exception error)
        { LastError = "Start remains committed; retry idempotent local completion: " + error.Message; return false; }
        finally { _completing = false; }
    }

    public void Invalidate()
    {
        State = PopupStoreBusinessStartState.Invalidated;
        _terminalOperation = true; _waiting = false; _operation++;
        LastError = "Account/session changed. Recover the original start before requesting another.";
        // Invalidation does not cancel the authority or release an unresolved admission lease.
    }

    private bool EnsureCurrent()
    {
        if (State == PopupStoreBusinessStartState.Invalidated || _checkingSession) return false;
        long operation = _operation;
        PopupStoreRewardAccountSession current;
        _checkingSession = true;
        try { current = _readSession(); } catch { current = null; }
        finally { _checkingSession = false; }
        if (State == PopupStoreBusinessStartState.Invalidated || operation != _operation) return false;
        if (_session == null || !ReferenceEquals(current, _session) || Request == null
            || !string.Equals(_session.AccountId, Request.AccountId, StringComparison.Ordinal))
        { Invalidate(); return false; }
        return true;
    }

    private bool ValidateRequest()
    {
        if (Request != null && Request.TryValidate(out _)) return true;
        State = PopupStoreBusinessStartState.RejectedBeforeSend; LastError = "Invalid immutable start request."; return false;
    }
    private void Unknown(bool query, string error)
    {
        State = query ? PopupStoreBusinessStartState.ReconciliationRequired : PopupStoreBusinessStartState.Indeterminate;
        _waiting = false; LastError = error;
    }
    private void Finish(PopupStoreBusinessStartState state, string error = null, bool release = true)
    {
        State = state; LastError = error; _terminalOperation = true; _waiting = false;
        if (release) _context.Resolve(_lease);
    }
}
