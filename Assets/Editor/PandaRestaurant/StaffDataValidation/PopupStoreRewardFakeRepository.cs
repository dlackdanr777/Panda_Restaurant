#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>Editor tests only. This shared in-memory authority survives transaction objects,
/// NOT application processes. Its lock/ledger/CAS contract is not a claim about Backend.</summary>
internal sealed class PopupStoreRewardFakeStore
{
    internal readonly struct Snapshot
    {
        internal readonly int Balance, Grants;
        internal readonly long Version;
        internal Snapshot(int balance, long version, int grants)
        { Balance = balance; Version = version; Grants = grants; }
    }
    private sealed class Account
    {
        internal int Balance;
        internal long Version;
        internal readonly Dictionary<string, PopupStoreRewardRequest> Decisions = new Dictionary<string, PopupStoreRewardRequest>(StringComparer.Ordinal);
        internal readonly Dictionary<string, PopupStoreRewardGrant> Grants = new Dictionary<string, PopupStoreRewardGrant>(StringComparer.Ordinal);
    }
    private readonly object _gate = new object();
    private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>(StringComparer.Ordinal);
    private readonly HashSet<PopupStoreRewardAccountSession> _sessions = new HashSet<PopupStoreRewardAccountSession>();
    private readonly Dictionary<string, long> _noEffectFences = new Dictionary<string, long>(StringComparer.Ordinal);
    private int _grantSequence;
    private long _attemptSequence;
    internal Action<long> BeforeSubmitLock;

    internal void RegisterAccount(string accountId, int balance = 100)
    {
        if (string.IsNullOrWhiteSpace(accountId) || balance < 0) throw new ArgumentException("Invalid fake account.");
        lock (_gate)
        {
            if (_accounts.ContainsKey(accountId)) throw new InvalidOperationException("Fake account already registered.");
            _accounts.Add(accountId, new Account { Balance = balance });
        }
    }
    internal PopupStoreRewardAccountSession OpenSession(string accountId)
    {
        lock (_gate)
        {
            if (!_accounts.ContainsKey(accountId)) throw new InvalidOperationException("Unknown fake account.");
            var session = new PopupStoreRewardAccountSession(accountId);
            _sessions.Add(session);
            return session;
        }
    }
    internal void RevokeSession(PopupStoreRewardAccountSession session) { lock (_gate) _sessions.Remove(session); }

    // Separate authority setup. A Submit request cannot call this or register its own business.
    internal void RegisterDecision(PopupStoreRewardRequest approved)
    {
        if (approved == null || !approved.TryValidate(out _)) throw new ArgumentException("Invalid approved decision.");
        lock (_gate)
        {
            if (!_accounts.TryGetValue(approved.AccountId, out var account)) throw new InvalidOperationException("Unknown fake account.");
            if (account.Decisions.ContainsKey(approved.BusinessSessionId)) throw new InvalidOperationException("Business already authorized.");
            account.Decisions.Add(approved.BusinessSessionId, approved);
        }
    }
    internal Snapshot Read(string accountId)
    {
        lock (_gate)
        {
            var account = _accounts[accountId];
            return new Snapshot(account.Balance, account.Version, account.Grants.Count);
        }
    }
    internal PopupStoreRewardGrant ReadGrant(string accountId, string key)
    { lock (_gate) return _accounts[accountId].Grants.TryGetValue(key, out var grant) ? grant : null; }

    internal bool TryWriteBalance(PopupStoreRewardAccountSession session, int value, long expectedVersion)
    {
        lock (_gate)
        {
            if (session == null || !_sessions.Contains(session) || !_accounts.TryGetValue(session.AccountId, out var account)
                || value < 0 || expectedVersion != account.Version || account.Version == long.MaxValue) return false;
            account.Balance = value;
            account.Version++;
            return true;
        }
    }

    internal PopupStoreRewardResponse Submit(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request)
    {
        // Register an attempt before waiting for the authority lock. A reconciliation fence can
        // therefore prevent an older, delayed submission from committing after no-effect proof.
        long attempt = Interlocked.Increment(ref _attemptSequence);
        if (attempt <= 0) throw new InvalidOperationException("Fake attempt sequence exhausted.");
        BeforeSubmitLock?.Invoke(attempt);
        return Evaluate(session, request, true, false, attempt);
    }
    internal PopupStoreRewardResponse Query(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request, bool confirmNotApplied = false)
        => Evaluate(session, request, false, confirmNotApplied, 0);

    private PopupStoreRewardResponse Evaluate(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request, bool commit, bool confirmNotApplied, long attempt)
    {
        lock (_gate)
        {
            if (session == null || !_sessions.Contains(session) || request == null
                || !string.Equals(session.AccountId, request.AccountId, StringComparison.Ordinal)
                || !_accounts.TryGetValue(session.AccountId, out var account))
                return Reply(request, PopupStoreRewardResponseKind.RejectedBeforeSend, "Fake authentication or account mismatch.");
            if (!request.TryValidate(out string error))
                return Reply(request, PopupStoreRewardResponseKind.Conflict, error);
            if (!account.Decisions.TryGetValue(request.BusinessSessionId, out var approved))
                return Reply(request, PopupStoreRewardResponseKind.RejectedBeforeSend, "Business has no authoritative decision.");
            if (!approved.Matches(request))
                return Reply(request, PopupStoreRewardResponseKind.Conflict, "Request contradicts the authorized business decision.");
            if (account.Grants.TryGetValue(approved.GrantKey, out var existing))
                return new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Granted, existing);
            if (approved.Grade == PopupStoreGradeTier.None)
                return Reply(request, PopupStoreRewardResponseKind.NoReward);
            if (!commit)
            {
                if (confirmNotApplied) _noEffectFences[approved.GrantKey] = Interlocked.Read(ref _attemptSequence);
                return Reply(request, confirmNotApplied ? PopupStoreRewardResponseKind.NotAppliedConfirmed : PopupStoreRewardResponseKind.NotFound);
            }
            if (_noEffectFences.TryGetValue(approved.GrantKey, out long fence) && attempt <= fence)
                return Reply(request, PopupStoreRewardResponseKind.RejectedBeforeSend, "This older attempt was authoritatively fenced before mutation.");

            // Validate all arithmetic BEFORE either member of the atomic ledger/balance pair changes.
            if ((long)account.Balance + approved.Diamonds > int.MaxValue || account.Version == long.MaxValue || _grantSequence == int.MaxValue)
                return Reply(request, PopupStoreRewardResponseKind.RejectedBeforeSend, "Fake balance/version overflow; nothing was written.");
            int balance = account.Balance + approved.Diamonds;
            long version = account.Version + 1;
            var grant = new PopupStoreRewardGrant(approved, "fake-grant-" + (_grantSequence + 1), balance, version);
            account.Grants.Add(approved.GrantKey, grant);
            account.Balance = balance;
            account.Version = version;
            _grantSequence++;
            return new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Granted, grant);
        }
    }
    private static PopupStoreRewardResponse Reply(PopupStoreRewardRequest request, PopupStoreRewardResponseKind kind, string error = null)
        => new PopupStoreRewardResponse(request, kind, error: error);
}

public enum PopupStoreFakeSubmitFault
{
    None, RejectBeforeSend, CommitThenIndeterminate, CommitThenDrop, CommitThenDefer,
    UnknownWithoutCommit, ThrowAfterCommit
}
public enum PopupStoreFakeQueryFault { None, NotFound, Indeterminate, ConfirmNotApplied, Defer, Throw }

/// <summary>Fake adapter with explicitly controlled delivery. The store commits before callbacks;
/// callback failure never rolls a commit back. Deferred delivery never means deferred commitment.</summary>
internal sealed class PopupStoreRewardFakeRepository : IPopupStoreRewardPersistence
{
    internal readonly PopupStoreRewardFakeStore Store;
    internal PopupStoreFakeSubmitFault NextSubmitFault;
    internal PopupStoreFakeQueryFault NextQueryFault;
    internal Func<PopupStoreRewardResponse, PopupStoreRewardResponse> TransformNextReply;
    internal bool ThrowAfterNextDelivery;
    internal int SubmitCalls, QueryCalls;
    private readonly Queue<Action> _deferred = new Queue<Action>();
    private Action _lastDelivery;
    internal int DeferredCount => _deferred.Count;
    internal PopupStoreRewardFakeRepository(PopupStoreRewardFakeStore store) { Store = store; }

    public void Submit(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request, Action<PopupStoreRewardResponse> completed)
    {
        SubmitCalls++;
        var fault = NextSubmitFault; NextSubmitFault = PopupStoreFakeSubmitFault.None;
        PopupStoreRewardResponse response;
        if (fault == PopupStoreFakeSubmitFault.RejectBeforeSend)
            response = new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.RejectedBeforeSend, error: "Injected pre-send rejection.");
        else if (fault == PopupStoreFakeSubmitFault.UnknownWithoutCommit)
            response = new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate, error: "Injected transport uncertainty.");
        else response = Store.Submit(session, request);
        if (fault == PopupStoreFakeSubmitFault.ThrowAfterCommit) throw new InvalidOperationException("Injected exception after commit.");
        if (fault == PopupStoreFakeSubmitFault.CommitThenIndeterminate)
            response = new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate, error: "Commit response lost.");
        Deliver(response, completed, fault == PopupStoreFakeSubmitFault.CommitThenDefer,
            fault == PopupStoreFakeSubmitFault.CommitThenDrop);
    }
    public void Query(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request, Action<PopupStoreRewardResponse> completed)
    {
        QueryCalls++;
        var fault = NextQueryFault; NextQueryFault = PopupStoreFakeQueryFault.None;
        if (fault == PopupStoreFakeQueryFault.Throw) throw new InvalidOperationException("Injected query failure.");
        var response = fault == PopupStoreFakeQueryFault.NotFound
            ? new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.NotFound)
            : fault == PopupStoreFakeQueryFault.Indeterminate
                ? new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate)
                : Store.Query(session, request, fault == PopupStoreFakeQueryFault.ConfirmNotApplied);
        Deliver(response, completed, fault == PopupStoreFakeQueryFault.Defer, false);
    }
    private void Deliver(PopupStoreRewardResponse response, Action<PopupStoreRewardResponse> completed, bool defer, bool drop)
    {
        var transform = TransformNextReply; TransformNextReply = null;
        if (transform != null) response = transform(response);
        bool throwAfter = ThrowAfterNextDelivery; ThrowAfterNextDelivery = false;
        Action delivery = () => { completed(response); if (throwAfter) throw new InvalidOperationException("Injected adapter exception after callback."); };
        _lastDelivery = delivery;
        if (defer) _deferred.Enqueue(delivery);
        else if (!drop) delivery();
    }
    internal void DeliverNext() { _deferred.Dequeue()(); }
    internal void ReplayLast() { _lastDelivery?.Invoke(); }
}
#endif
