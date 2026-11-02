using System;

public enum PopupStoreRewardTransactionState
{
    Prepared, Submitting, Granted, RejectedBeforeSend, Indeterminate,
    ReconciliationRequired, Conflict, Invalidated, NotAppliedConfirmed, NoReward
}

/// <summary>One account-bound execution of one immutable reward request. Not a wallet and not
/// attached to any popup. Serialized callbacks are required; separate executors may share an
/// atomic authority. There is no SDK, file, clock, RNG, UI or UserInfo dependency here.</summary>
public sealed class PopupStoreRewardTransaction
{
    private readonly IPopupStoreRewardPersistence _repository;
    private readonly Func<PopupStoreRewardAccountSession> _readSession;
    private readonly PopupStoreRewardAccountSession _session;
    private readonly Action<PopupStoreRewardGrant> _completeLocally;
    private long _operation;
    private bool _terminalOperation, _waiting, _completing, _checkingSession;

    public PopupStoreRewardRequest Request { get; }
    public PopupStoreRewardTransactionState State { get; private set; }
    public PopupStoreRewardGrant Grant { get; private set; }
    public string LastError { get; private set; }
    public int SubmitCount { get; private set; }
    public int QueryCount { get; private set; }
    public int ConfirmationCount { get; private set; }
    public bool LocalCompletionPending { get; private set; }

    public PopupStoreRewardTransaction(PopupStoreRewardRequest request, IPopupStoreRewardPersistence repository,
        Func<PopupStoreRewardAccountSession> readSession, Action<PopupStoreRewardGrant> completeLocally = null)
        : this(request, repository, readSession, completeLocally, false) { }

    internal PopupStoreRewardTransaction(PopupStoreRewardRequest request, IPopupStoreRewardPersistence repository,
        Func<PopupStoreRewardAccountSession> readSession, Action<PopupStoreRewardGrant> completeLocally,
        bool recovering)
    {
        Request = request;
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _readSession = readSession ?? throw new ArgumentNullException(nameof(readSession));
        _completeLocally = completeLocally;
        try { _session = _readSession(); } catch { _session = null; }
        State = recovering ? PopupStoreRewardTransactionState.ReconciliationRequired
            : PopupStoreRewardTransactionState.Prepared;
        if (request == null || !request.TryValidate(out _))
        { State = PopupStoreRewardTransactionState.RejectedBeforeSend; LastError = "Invalid immutable reward request."; }
        EnsureCurrent();
    }

    public bool TrySubmit()
    {
        if (!EnsureCurrent() || !ValidateRequest()) return false;
        if (State == PopupStoreRewardTransactionState.Granted || State == PopupStoreRewardTransactionState.NoReward)
            return true;
        if (_waiting || (State != PopupStoreRewardTransactionState.Prepared
            && State != PopupStoreRewardTransactionState.RejectedBeforeSend
            && State != PopupStoreRewardTransactionState.NotAppliedConfirmed)) return false;
        return Send(false);
    }

    public bool TryReconcile()
    {
        if (!EnsureCurrent() || !ValidateRequest()) return false;
        if (State == PopupStoreRewardTransactionState.Granted || State == PopupStoreRewardTransactionState.NoReward)
            return true;
        if (_waiting || State == PopupStoreRewardTransactionState.Submitting
            || State == PopupStoreRewardTransactionState.Conflict) return false;
        return Send(true);
    }

    private bool Send(bool query)
    {
        long operation = ++_operation;
        _terminalOperation = false; _waiting = true; LastError = null;
        State = query ? PopupStoreRewardTransactionState.ReconciliationRequired
            : PopupStoreRewardTransactionState.Submitting;
        try
        {
            if (query)
            {
                QueryCount++;
                _repository.Query(_session, Request, response => Receive(operation, true, response));
            }
            else
            {
                SubmitCount++;
                _repository.Submit(_session, Request, response => Receive(operation, false, response));
            }
        }
        catch (Exception error)
        {
            // A callback may have already confirmed success (or started a later query). An exception
            // after entering the adapter never proves no write and never undoes a terminal outcome.
            if (operation == _operation && !_terminalOperation && EnsureCurrent())
                Unknown(query, "Repository call outcome is unknown: " + error.Message);
        }
        return true; // Accepted for processing, not a claim that the grant succeeded.
    }

    private void Receive(long operation, bool query, PopupStoreRewardResponse response)
    {
        if (operation != _operation || _terminalOperation || !EnsureCurrent()
            || operation != _operation || _terminalOperation) return;
        if (response == null || response.Request == null || !Request.Matches(response.Request))
        { Unknown(query, "Uncorrelated or missing response; original grant must be reconciled."); return; }
        if (response.Kind != PopupStoreRewardResponseKind.Granted && response.Grant != null)
        { Unknown(query, "Contradictory response contains a grant without a confirmed success."); return; }
        switch (response.Kind)
        {
            case PopupStoreRewardResponseKind.Granted:
                if (response.Grant == null || !response.Grant.Matches(Request))
                { Unknown(query, "The returned grant does not match this authorized request."); return; }
                Grant = response.Grant;
                Finish(PopupStoreRewardTransactionState.Granted);
                ConfirmationCount++;
                LocalCompletionPending = _completeLocally != null;
                RetryLocalCompletion();
                break;
            case PopupStoreRewardResponseKind.NoReward:
                if (Request.Grade != PopupStoreGradeTier.None || Request.Diamonds != 0)
                { Unknown(query, "NoReward contradicts the expected decision."); return; }
                Finish(PopupStoreRewardTransactionState.NoReward);
                break;
            case PopupStoreRewardResponseKind.Conflict:
                Finish(PopupStoreRewardTransactionState.Conflict, response.Error);
                break;
            case PopupStoreRewardResponseKind.RejectedBeforeSend:
                if (query) Unknown(true, response.Error ?? "Query rejection cannot prove the original write failed.");
                else Finish(PopupStoreRewardTransactionState.RejectedBeforeSend, response.Error);
                break;
            case PopupStoreRewardResponseKind.NotAppliedConfirmed:
                if (query) Finish(PopupStoreRewardTransactionState.NotAppliedConfirmed, response.Error);
                else Unknown(false, "Only an authoritative reconciliation may prove no effect.");
                break;
            case PopupStoreRewardResponseKind.NotFound:
                Unknown(true, response.Error ?? "A missing read is not authoritative no-write proof.");
                break;
            default:
                Unknown(query, response.Error ?? "Outcome remains unknown.");
                break;
        }
    }

    /// <summary>Called by the host's timeout/cancellation boundary. Does not cancel the authority's
    /// write, release an entitlement or enable another submission. Late original success is valid.</summary>
    public void MarkResponseMissing()
    {
        if (!EnsureCurrent() || _terminalOperation) return;
        if (State == PopupStoreRewardTransactionState.Submitting)
            Unknown(false, "Submission response missing; reconcile the original key.");
        else if (State == PopupStoreRewardTransactionState.ReconciliationRequired)
            Unknown(true, "Reconciliation response missing.");
    }

    /// <summary>Only retries local presentation/cache completion, never Submit. The consumer MUST
    /// be idempotent by GrantId, especially if it partially applied then threw. It must not credit
    /// currency or replay BalanceAfter. This model cannot roll back arbitrary consumer side effects.</summary>
    public bool RetryLocalCompletion()
    {
        if (!EnsureCurrent() || State != PopupStoreRewardTransactionState.Granted || Grant == null || _completing)
            return false;
        if (!LocalCompletionPending) return true;
        _completing = true;
        try
        {
            _completeLocally(Grant);
            if (!EnsureCurrent()) return false;
            LocalCompletionPending = false; LastError = null;
            return true;
        }
        catch (Exception error)
        {
            LastError = "Grant is committed; local completion needs an idempotent retry: " + error.Message;
            return false;
        }
        finally { _completing = false; }
    }

    public void Invalidate()
    {
        State = PopupStoreRewardTransactionState.Invalidated;
        _terminalOperation = true; _waiting = false; _operation++;
        LastError = "The account or execution generation changed; recover with a new bound executor.";
    }

    private bool EnsureCurrent()
    {
        if (State == PopupStoreRewardTransactionState.Invalidated || _checkingSession) return false;
        long operation = _operation;
        PopupStoreRewardAccountSession current;
        _checkingSession = true;
        try { current = _readSession(); } catch { current = null; }
        finally { _checkingSession = false; }
        if (State == PopupStoreRewardTransactionState.Invalidated || operation != _operation) return false;
        if (_session == null || !ReferenceEquals(current, _session) || Request == null
            || !string.Equals(_session.AccountId, Request.AccountId, StringComparison.Ordinal))
        { Invalidate(); return false; }
        return true;
    }

    private bool ValidateRequest()
    {
        if (Request != null && Request.TryValidate(out string error)) return true;
        State = PopupStoreRewardTransactionState.RejectedBeforeSend;
        LastError = "Invalid immutable reward request.";
        return false;
    }

    private void Unknown(bool query, string error)
    {
        State = query ? PopupStoreRewardTransactionState.ReconciliationRequired
            : PopupStoreRewardTransactionState.Indeterminate;
        _waiting = false; LastError = error;
        // Keep this operation eligible for its delayed confirmed reply until a new query starts.
    }

    private void Finish(PopupStoreRewardTransactionState state, string error = null)
    { State = state; LastError = error; _terminalOperation = true; _waiting = false; }
}
