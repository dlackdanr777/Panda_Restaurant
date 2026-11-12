#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>Editor-only shared authority simulation. It is not a disk/server adapter and never
/// reads UserInfo. Only this store owns business IDs, game days and atomic start/quota records.</summary>
internal sealed class PopupStoreBusinessStartFakeStore
{
    private sealed class Account
    {
        internal readonly Dictionary<string, int> Usage = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly Dictionary<string, PopupStoreBusinessStartRecord> Starts = new Dictionary<string, PopupStoreBusinessStartRecord>(StringComparer.Ordinal);
        internal readonly Dictionary<string, PopupStoreBusinessStartRequest> Fingerprints = new Dictionary<string, PopupStoreBusinessStartRequest>(StringComparer.Ordinal);
        internal readonly Dictionary<string, PopupStoreBusinessStartResponse> LimitRefusals = new Dictionary<string, PopupStoreBusinessStartResponse>(StringComparer.Ordinal);
        internal DateTimeOffset? LatestObservedUtc;
    }
    internal readonly struct Snapshot
    {
        internal readonly int Usage, Starts, RewardedStartsForDay;
        internal Snapshot(int usage, int starts, int rewardedStartsForDay)
        { Usage = usage; Starts = starts; RewardedStartsForDay = rewardedStartsForDay; }
    }
    private readonly object _gate = new object();
    private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>(StringComparer.Ordinal);
    private readonly HashSet<PopupStoreRewardAccountSession> _sessions = new HashSet<PopupStoreRewardAccountSession>();
    private readonly Dictionary<string, long> _noEffectFences = new Dictionary<string, long>(StringComparer.Ordinal);
    private long _attemptSequence, _businessSequence;
    private PopupStoreBusinessAccessPolicy _policy;
    private PopupStoreBusinessAccessSchedule _schedule;
    private long _configurationRevision;
    private bool _readingClock;
    internal Func<DateTimeOffset?> Clock;
    internal Action<long> BeforeSubmitLock;

    internal PopupStoreBusinessStartFakeStore(Func<DateTimeOffset?> clock, PopupStoreBusinessAccessPolicy policy = null)
    { Clock = clock; _policy = policy ?? new PopupStoreBusinessAccessPolicy(); }
    internal PopupStoreBusinessStartFakeStore(PopupStoreBusinessAccessSchedule schedule, Func<DateTimeOffset?> clock)
    { _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule)); Clock = clock; }
    internal void UsePolicy(PopupStoreBusinessAccessPolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        lock (_gate)
        {
            if (_schedule != null) throw new InvalidOperationException("Scheduled review policies may change only on a future game date.");
            _policy = policy; _configurationRevision++;
        }
    }
    internal bool TryScheduleFuture(PopupStoreRewardAccountSession session, PopupStoreBusinessAccessSetting next, out string error)
    {
        lock (_gate)
        {
            if (_schedule == null) { error = "This isolated store has no authored schedule."; return false; }
            if (!TryReadAccess(session, false, out _, out string day, out _, out error)) return false;
            if (!_schedule.TryWithFuture(next, day, out var schedule, out error)) return false;
            _schedule = schedule; _configurationRevision++; return true;
        }
    }
    internal PopupStoreBusinessAccessSnapshot ReadAccess(PopupStoreRewardAccountSession session)
    {
        lock (_gate)
        {
            if (!TryReadAccess(session, false, out var instant, out string day, out var policy, out string error))
                return PopupStoreBusinessAccessSnapshot.Failure(session, error);
            var account = _accounts[session.AccountId];
            int used = account.Usage.TryGetValue(day, out int value) ? value : 0;
            return new PopupStoreBusinessAccessSnapshot(session, day, policy.Version, policy.RewardedDailyLimit, used, instant);
        }
    }

    // Called under the authority lock. Read-side time metadata prevents an injected clock rewind
    // from recovering an earlier day's entitlement. Accepted request replay bypasses this method.
    private bool TryReadAccess(PopupStoreRewardAccountSession session, bool allowUndatedPractice,
        out DateTimeOffset? instant, out string day, out PopupStoreBusinessAccessPolicy policy, out string error)
    {
        instant = null; day = null; policy = null; error = null;
        if (session == null || !_sessions.Contains(session) || !_accounts.TryGetValue(session.AccountId, out var account))
        { error = "Fake authentication/account mismatch."; return false; }
        if (_readingClock) { error = "A reentrant authority clock read is not available."; return false; }
        long revision = _configurationRevision;
        _readingClock = true;
        try { instant = Clock?.Invoke(); } catch { instant = null; }
        finally { _readingClock = false; }
        if (!_sessions.Contains(session)) { error = "Fake session changed during clock read."; return false; }
        if (revision != _configurationRevision) { error = "Policy changed during clock read."; return false; }
        bool hasDay = instant.HasValue && PopupStoreDailyUsage.TryGetDayKey(instant.Value, out day);
        if (!hasDay)
        {
            instant = null;
            if (_schedule == null && allowUndatedPractice && _policy.PracticeClockFailure == PopupStorePracticeClockFailure.AllowWithoutDayKey)
            { policy = _policy; return true; }
            error = "Authoritative day unavailable."; return false;
        }
        instant = instant.Value.ToUniversalTime();
        if (account.LatestObservedUtc.HasValue && instant.Value < account.LatestObservedUtc.Value)
        { error = "Authoritative time moved backwards; access remains blocked."; return false; }
        if (_schedule != null)
        {
            if (!_schedule.TryResolve(day, out policy, out error)) return false;
        }
        else policy = _policy;
        account.LatestObservedUtc = instant;
        return true;
    }
    internal void RegisterAccount(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Fake account required.");
        lock (_gate)
        {
            if (_accounts.ContainsKey(id)) throw new InvalidOperationException("Fake account already registered.");
            _accounts.Add(id, new Account());
        }
    }
    internal PopupStoreRewardAccountSession OpenSession(string id)
    {
        lock (_gate)
        {
            if (!_accounts.ContainsKey(id)) throw new InvalidOperationException("Unknown fake account.");
            var session = new PopupStoreRewardAccountSession(id); _sessions.Add(session); return session;
        }
    }
    internal void Revoke(PopupStoreRewardAccountSession session) { lock (_gate) _sessions.Remove(session); }
    internal Snapshot Read(string id, string day)
    {
        lock (_gate)
        {
            var account = _accounts[id]; int uses = day != null && account.Usage.TryGetValue(day, out int n) ? n : 0;
            int started = 0;
            foreach (var record in account.Starts.Values)
                if (record.Mode == PopupStoreBusinessMode.Rewarded && string.Equals(record.StartDayKey, day, StringComparison.Ordinal)) started++;
            return new Snapshot(uses, account.Starts.Count, started);
        }
    }
    internal long IssuedBusinessCount { get { lock (_gate) return _businessSequence; } }
    internal PopupStoreBusinessStartRecord Find(string accountId, string requestId)
    { lock (_gate) return _accounts[accountId].Starts.TryGetValue(requestId, out var record) ? record : null; }

    // A shape-valid client record is NOT sufficient. Candidate eligibility requires the exact
    // authority-owned start record and a currently registered authentication token first.
    internal bool CanConsiderReward(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRecord record,
        int successfulOrders, PopupStoreBusinessEndReason reason)
    {
        lock (_gate)
            return session != null && _sessions.Contains(session) && record != null && record.Matches(record.Request)
                && string.Equals(session.AccountId, record.AccountId, StringComparison.Ordinal)
                && _accounts.TryGetValue(session.AccountId, out var account)
                && account.Starts.TryGetValue(record.StartRequestId, out var stored) && ReferenceEquals(stored, record)
                && PopupStoreBusinessAccessPolicy.CanConsiderReward(record, session.AccountId, successfulOrders, reason);
    }

    internal PopupStoreBusinessStartResponse Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request)
    {
        long attempt = Interlocked.Increment(ref _attemptSequence);
        if (attempt <= 0) throw new InvalidOperationException("Fake attempt sequence exhausted.");
        BeforeSubmitLock?.Invoke(attempt);
        return Evaluate(session, request, true, false, attempt);
    }
    internal PopupStoreBusinessStartResponse Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request, bool proveNotApplied = false)
        => Evaluate(session, request, false, proveNotApplied, 0);

    private PopupStoreBusinessStartResponse Evaluate(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
        bool commit, bool proveNotApplied, long attempt)
    {
        lock (_gate)
        {
            if (session == null || !_sessions.Contains(session) || request == null
                || !string.Equals(session.AccountId, request.AccountId, StringComparison.Ordinal)
                || !_accounts.TryGetValue(session.AccountId, out var account))
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Fake authentication/account mismatch.");
            if (!request.TryValidate(out string invalid)) return Reply(request, PopupStoreBusinessStartResponseKind.Conflict, invalid);
            // Replay the original accepted business BEFORE consulting the clock or today's policy.
            if (account.Starts.TryGetValue(request.StartRequestId, out var existing))
                return existing.Matches(request) ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Started, existing)
                    : Reply(request, PopupStoreBusinessStartResponseKind.Conflict, "Start identity/mode/policy contradicts the original record.");
            if (account.Fingerprints.TryGetValue(request.StartRequestId, out var fingerprint) && !fingerprint.Matches(request))
                return Reply(request, PopupStoreBusinessStartResponseKind.Conflict, "An earlier attempt already bound this request fingerprint.");
            if (!string.IsNullOrEmpty(request.ExpectedBusinessSessionId))
                return Reply(request, PopupStoreBusinessStartResponseKind.Conflict, "A client cannot choose a new authoritative business ID.");
            string key = PopupStoreBusinessStartRequest.CreateRequestKey(request.AccountId, request.StartRequestId);
            if (!commit)
            {
                if (proveNotApplied) _noEffectFences[key] = Interlocked.Read(ref _attemptSequence);
                return Reply(request, proveNotApplied ? PopupStoreBusinessStartResponseKind.NotAppliedConfirmed : PopupStoreBusinessStartResponseKind.NotFound);
            }
            if (_noEffectFences.TryGetValue(key, out long fence) && attempt <= fence)
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Older attempt was fenced before mutation.");
            if (account.LimitRefusals.TryGetValue(request.StartRequestId, out var refusal)) return refusal;
            if (!account.Fingerprints.ContainsKey(request.StartRequestId)) account.Fingerprints.Add(request.StartRequestId, request);
            if (_schedule == null && !string.Equals(request.PolicyVersion, _policy.Version, StringComparison.Ordinal))
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "New starts require the current registered test policy.");
            bool hasAccess = TryReadAccess(session, request.Mode == PopupStoreBusinessMode.Practice,
                out var instant, out string day, out var policy, out string accessError);
            // The injected clock is test code and can re-enter or revoke credentials. Never commit
            // using the authentication/policy/record snapshot from before invoking it.
            if (!_sessions.Contains(session))
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Fake session changed during clock read.");
            if (account.Starts.TryGetValue(request.StartRequestId, out existing))
                return existing.Matches(request) ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Started, existing)
                    : Reply(request, PopupStoreBusinessStartResponseKind.Conflict, "Start changed during clock read.");
            if (!hasAccess) return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, accessError);
            if (!string.Equals(request.PolicyVersion, policy.Version, StringComparison.Ordinal))
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "New starts require the policy for the authority's approval game date.");
            if (_noEffectFences.TryGetValue(key, out fence) && attempt <= fence)
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Attempt fenced during clock read.");
            int used = day != null && account.Usage.TryGetValue(day, out int prior) ? prior : 0;
            if (request.Mode == PopupStoreBusinessMode.Rewarded && used >= policy.RewardedDailyLimit)
            {
                refusal = Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Daily rewarded start limit reached.");
                account.LimitRefusals[request.StartRequestId] = refusal;
                return refusal; // Definitive limit refusals replay; a new day requires a NEW start request.
            }
            if (_businessSequence == long.MaxValue || (request.Mode == PopupStoreBusinessMode.Rewarded && used == int.MaxValue))
                return Reply(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, "Fake counter exhausted.");
            int after = used + (request.Mode == PopupStoreBusinessMode.Rewarded ? 1 : 0);
            var record = new PopupStoreBusinessStartRecord(request, "fake-business-" + (_businessSequence + 1), day,
                instant.HasValue ? instant.Value.ToUniversalTime() : (DateTimeOffset?)null, after);
            if (!record.Matches(request)) return Reply(request, PopupStoreBusinessStartResponseKind.Conflict, "Authority could not form a valid record.");
            // Both changes become observable under the same lock; a rejected start issues no ID.
            account.Starts.Add(request.StartRequestId, record);
            if (request.Mode == PopupStoreBusinessMode.Rewarded) account.Usage[day] = after;
            _businessSequence++;
            return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Started, record);
        }
    }
    private static PopupStoreBusinessStartResponse Reply(PopupStoreBusinessStartRequest request, PopupStoreBusinessStartResponseKind kind, string error = null)
        => new PopupStoreBusinessStartResponse(request, kind, error: error);
}

public enum PopupStoreFakeStartFault { None, RejectBeforeSend, CommitThenIndeterminate, CommitThenDrop, CommitThenDefer, UnknownWithoutCommit, ThrowAfterCommit }
public enum PopupStoreFakeStartQueryFault { None, NotFound, Indeterminate, ConfirmNotApplied, Defer, Throw }
public enum PopupStoreFakeAccessFault { None, Fail, Defer, Drop, Throw }

internal sealed class PopupStoreBusinessStartFakeRepository : IPopupStoreBusinessStartPersistence, IPopupStoreBusinessAccessInformation
{
    internal readonly PopupStoreBusinessStartFakeStore Store;
    internal PopupStoreFakeStartFault NextSubmitFault;
    internal PopupStoreFakeStartQueryFault NextQueryFault;
    internal Func<PopupStoreBusinessStartResponse, PopupStoreBusinessStartResponse> TransformNextReply;
    internal bool ThrowAfterNextDelivery;
    internal int SubmitCalls, QueryCalls;
    internal int AccessQueryCalls;
    internal PopupStoreFakeAccessFault NextAccessFault;
    internal Func<PopupStoreBusinessAccessSnapshot, PopupStoreBusinessAccessSnapshot> TransformNextAccessReply;
    private readonly Queue<Action> _deferred = new Queue<Action>();
    private Action _lastDelivery;
    private Action _lastAccessDelivery;
    internal PopupStoreBusinessStartFakeRepository(PopupStoreBusinessStartFakeStore store) { Store = store; }
    public void QueryAccess(PopupStoreRewardAccountSession session, Action<PopupStoreBusinessAccessSnapshot> completed)
    {
        AccessQueryCalls++;
        var fault = NextAccessFault; NextAccessFault = PopupStoreFakeAccessFault.None;
        if (fault == PopupStoreFakeAccessFault.Throw) throw new InvalidOperationException("Injected access information failure.");
        var response = fault == PopupStoreFakeAccessFault.Fail
            ? PopupStoreBusinessAccessSnapshot.Failure(session, "Injected access information failure.") : Store.ReadAccess(session);
        var transform = TransformNextAccessReply; TransformNextAccessReply = null;
        if (transform != null) response = transform(response);
        _lastAccessDelivery = () => completed(response);
        if (fault == PopupStoreFakeAccessFault.Defer) _deferred.Enqueue(_lastAccessDelivery);
        else if (fault != PopupStoreFakeAccessFault.Drop) _lastAccessDelivery();
    }
    internal void ReplayLastAccess() { _lastAccessDelivery?.Invoke(); }
    public void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request, Action<PopupStoreBusinessStartResponse> completed)
    {
        SubmitCalls++;
        var fault = NextSubmitFault; NextSubmitFault = PopupStoreFakeStartFault.None;
        var response = fault == PopupStoreFakeStartFault.RejectBeforeSend
            ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: "Injected pre-send rejection.")
            : fault == PopupStoreFakeStartFault.UnknownWithoutCommit
                ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate)
                : Store.Submit(session, request);
        if (fault == PopupStoreFakeStartFault.ThrowAfterCommit) throw new InvalidOperationException("Injected adapter exception after commit.");
        if (fault == PopupStoreFakeStartFault.CommitThenIndeterminate)
            response = new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate);
        Deliver(response, completed, fault == PopupStoreFakeStartFault.CommitThenDefer, fault == PopupStoreFakeStartFault.CommitThenDrop);
    }
    public void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request, Action<PopupStoreBusinessStartResponse> completed)
    {
        QueryCalls++;
        var fault = NextQueryFault; NextQueryFault = PopupStoreFakeStartQueryFault.None;
        if (fault == PopupStoreFakeStartQueryFault.Throw) throw new InvalidOperationException("Injected query failure.");
        var response = fault == PopupStoreFakeStartQueryFault.NotFound ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.NotFound)
            : fault == PopupStoreFakeStartQueryFault.Indeterminate ? new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate)
            : Store.Query(session, request, fault == PopupStoreFakeStartQueryFault.ConfirmNotApplied);
        Deliver(response, completed, fault == PopupStoreFakeStartQueryFault.Defer, false);
    }
    private void Deliver(PopupStoreBusinessStartResponse response, Action<PopupStoreBusinessStartResponse> completed, bool defer, bool drop)
    {
        var transform = TransformNextReply; TransformNextReply = null; if (transform != null) response = transform(response);
        bool throwAfter = ThrowAfterNextDelivery; ThrowAfterNextDelivery = false;
        Action callback = () => { completed(response); if (throwAfter) throw new InvalidOperationException("Injected adapter exception after callback."); };
        _lastDelivery = callback;
        if (defer) _deferred.Enqueue(callback); else if (!drop) callback();
    }
    internal void DeliverNext() { _deferred.Dequeue()(); }
    internal void ReplayLast() { _lastDelivery?.Invoke(); }
}
#endif
