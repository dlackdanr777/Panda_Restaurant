using System;
using System.Collections.Generic;

/// <summary>Scene/review-owner memory, deliberately outside the replaceable popup View.
/// The injected authority owns quota. This host owns unresolved requests and immutable local bindings.
/// It does not load an account, save inventory, grant rewards, or provide a default authority.</summary>
public sealed class PopupStoreAdmissionHost : IDisposable
{
    private readonly IPopupStoreBusinessStartPersistence _persistence;
    private readonly IPopupStoreBusinessAccessInformation _information;
    private readonly Func<PopupStoreRewardAccountSession> _sessionReader;
    private readonly PopupStoreBusinessStartContext _context;
    private readonly Dictionary<string, string> _bindings = new Dictionary<string, string>(StringComparer.Ordinal);
    private PopupStoreStartController _target;
    private PopupStoreRewardAccountSession _capturedSession;
    private PopupStoreBusinessStartRecord _approval;
    private bool _disposed, _busy, _querying, _notifying, _notifyAgain, _localStarted;
    private long _querySerial;
    private double _pendingSeconds, _querySeconds;
    public event Action Changed;
    public PopupStoreBusinessAccessSnapshot Access { get; private set; }
    public PopupStoreBusinessStartTransaction Transaction { get; private set; }
    public string LastError { get; private set; }
    public bool IsEditorReview { get; }
    public bool IsQuerying => _querying;
    public int BindingCount => _bindings.Count;
    public int LocalStartCount { get; private set; }
    public string LocalSessionId { get; private set; }
    public string BusinessSessionId => _approval?.BusinessSessionId;
    public PopupStoreBusinessStartContext Context => _context;

    public PopupStoreAdmissionHost(IPopupStoreBusinessStartPersistence persistence,
        IPopupStoreBusinessAccessInformation information, Func<PopupStoreRewardAccountSession> sessionReader,
        PopupStoreBusinessStartContext context = null, bool editorReview = false)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _information = information ?? throw new ArgumentNullException(nameof(information));
        _sessionReader = sessionReader ?? throw new ArgumentNullException(nameof(sessionReader));
        _context = context ?? new PopupStoreBusinessStartContext();
        IsEditorReview = editorReview;
    }

    internal bool CanAttach(PopupStoreStartController target)
        => !_disposed && target != null && !string.IsNullOrEmpty(target.LocalSessionId)
            && (LocalSessionId == null || LocalSessionId == target.LocalSessionId || (_target != null && _target.IsRetired));

    internal bool Attach(PopupStoreStartController target)
    {
        if (!CanAttach(target)) return Fail("이전 시작 요청을 먼저 복구해야 합니다.");
        if (ReferenceEquals(_target, target)) return true;
        // A pending or approved-but-not-started game cannot be replaced by a fresh local identity.
        if (LocalSessionId != null && LocalSessionId != target.LocalSessionId
            && (_target == null || !_target.IsRetired))
        { LastError = "이전 시작 요청을 먼저 복구해야 합니다."; return false; }
        if (LocalSessionId != target.LocalSessionId)
        {
            Transaction = null; _approval = null; _localStarted = false;
            _capturedSession = ReadSession(); Access = null; _querying = false; ++_querySerial;
        }
        _target = target;
        LocalSessionId = target.LocalSessionId;
        if (_capturedSession == null) _capturedSession = ReadSession();
        return true;
    }

    public bool IsApprovedFor(PopupStoreStartController target)
        => !_disposed && target != null && ReferenceEquals(target, _target) && CurrentSession()
            && _approval != null && LocalSessionId == target.LocalSessionId
            && _bindings.TryGetValue(_approval.BusinessSessionId, out var local) && local == LocalSessionId;

    public bool RefreshAccess()
    {
        if (_disposed || _querying || !CurrentSession()) return Fail("영업권 조회 계정을 확인할 수 없습니다.");
        _querying = true; _querySeconds = 0; LastError = null;
        long serial = ++_querySerial;
        var session = _capturedSession;
        Notify();
        try
        {
            _information.QueryAccess(session, snapshot =>
            {
                if (_disposed || serial != _querySerial || !CurrentSession()
                    || !ReferenceEquals(session, _capturedSession)) return;
                _querying = false;
                ++_querySerial; // One terminal information response; duplicate/late callbacks cannot replace it.
                if (snapshot == null || !ReferenceEquals(snapshot.Session, session) || !snapshot.Success
                    || snapshot.DailyLimit < 1 || snapshot.DailyLimit > 3 || snapshot.Used < 0
                    || snapshot.Remaining != Math.Max(0, snapshot.DailyLimit - snapshot.Used)
                    || string.IsNullOrWhiteSpace(snapshot.PolicyVersion) || string.IsNullOrEmpty(snapshot.GameDay))
                { Access = null; LastError = snapshot?.Error ?? "영업권 조회 응답을 확인할 수 없습니다."; }
                else { Access = snapshot; LastError = null; }
                Notify();
            });
        }
        catch (Exception error)
        {
            if (serial == _querySerial) { _querying = false; Access = null; LastError = "영업권 조회 실패: " + error.GetType().Name; }
            Notify(); return false;
        }
        return true;
    }

    public bool RequestStart(PopupStoreStartController target)
    {
        if (_disposed || _busy || !ReferenceEquals(target, _target) || target == null
            || target.IsRetired || !CurrentSession()) return false;
        if (!target.CanRequestApproval) return Fail("시작 화면과 영업 준비 상태를 확인해 주세요.");
        _busy = true;
        try
        {
            if (Transaction != null)
            {
                if (Transaction.State == PopupStoreBusinessStartState.Started)
                { Transaction.RetryLocalCompletion(); return IsApprovedFor(target) && _localStarted; }
                if (Transaction.State == PopupStoreBusinessStartState.Submitting) return false;
                if (Transaction.State == PopupStoreBusinessStartState.Indeterminate
                    || Transaction.State == PopupStoreBusinessStartState.ReconciliationRequired)
                { _pendingSeconds = 0; return Transaction.TryReconcile(); }
                if (Transaction.State == PopupStoreBusinessStartState.Conflict
                    || Transaction.State == PopupStoreBusinessStartState.Invalidated) return false;
                // Only a definite no-effect response permits a new request and refreshed policy.
                Transaction = null;
                RefreshAccess();
                return false;
            }
            if (_querying || Access == null || !ReferenceEquals(Access.Session, _capturedSession))
            { if (!_querying) RefreshAccess(); return false; }
            if (Access.Remaining <= 0) return Fail("오늘의 영업권을 모두 사용했습니다.");
            var pending = _context.GetPendingRequest(_capturedSession.AccountId);
            if (pending != null) return Fail("기존 미확정 시작 요청의 복구가 필요합니다.");
            var request = new PopupStoreBusinessStartRequest(_capturedSession.AccountId,
                Guid.NewGuid().ToString("N"), PopupStoreBusinessMode.Rewarded, Access.PolicyVersion);
            Transaction = new PopupStoreBusinessStartTransaction(request, new ObservedPersistence(this),
                _sessionReader, _context, CompleteLocally);
            _pendingSeconds = 0; LastError = null;
            return Transaction.TrySubmit();
        }
        finally { _busy = false; Notify(); }
    }

    private void CompleteLocally(PopupStoreBusinessStartRecord record)
    {
        if (_disposed || !CurrentSession() || _target == null || _target.LocalSessionId != LocalSessionId)
            throw new InvalidOperationException("The approved local game is unavailable; preserve its admission.");
        if (_bindings.TryGetValue(record.BusinessSessionId, out var local) && local != LocalSessionId)
            throw new InvalidOperationException("One business approval cannot start two local games.");
        if (_approval != null && _approval.BusinessSessionId != record.BusinessSessionId)
            throw new InvalidOperationException("The local game already has a different approval.");
        _bindings[record.BusinessSessionId] = LocalSessionId;
        _approval = record;
        if (!_target.TryStartApprovedClock())
            throw new InvalidOperationException("승인은 보존됐습니다. 화면과 포커스를 복구한 뒤 시작을 재개하세요.");
        if (!_localStarted) { _localStarted = true; LocalStartCount++; }
        RefreshAccess();
    }

    /// <summary>A timeout keeps the original transaction. It never means that quota was not consumed.</summary>
    public void ObserveElapsed(double elapsed)
    {
        if (_disposed || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) return;
        if (!CurrentSession()) { Access = null; LastError = "계정 세션이 변경되어 시작을 중단했습니다."; Notify(); return; }
        if (_querying && (_querySeconds += elapsed) >= 10)
        { _querying = false; ++_querySerial; Access = null; LastError = "영업권 조회 응답을 기다리는 중입니다. 다시 조회해 주세요."; Notify(); }
        if (Transaction != null && (Transaction.State == PopupStoreBusinessStartState.Submitting
            || Transaction.State == PopupStoreBusinessStartState.ReconciliationRequired)
            && (_pendingSeconds += elapsed) >= 10)
        { Transaction.MarkResponseMissing(); _pendingSeconds = 0; Notify(); }
    }

    public bool RetryLocalStart(PopupStoreStartController target)
    {
        if (!ReferenceEquals(target, _target) || Transaction == null
            || Transaction.State != PopupStoreBusinessStartState.Started || !Transaction.LocalCompletionPending) return false;
        Transaction.RetryLocalCompletion(); Notify();
        return !Transaction.LocalCompletionPending;
    }

    private PopupStoreRewardAccountSession ReadSession()
    { try { return _sessionReader(); } catch { return null; } }
    private bool CurrentSession() => _capturedSession != null && ReferenceEquals(_capturedSession, ReadSession());
    private bool Fail(string error) { LastError = error; Notify(); return false; }
    private void Notify()
    {
        if (_disposed) return;
        if (_notifying) { _notifyAgain = true; return; }
        _notifying = true;
        try
        {
            do
            {
                _notifyAgain = false;
                if (Changed != null) foreach (Action handler in Changed.GetInvocationList())
                    try { handler(); }
                    catch (Exception error) { LastError = "시작 화면 갱신 실패: " + error.GetType().Name; }
            } while (_notifyAgain);
        }
        finally { _notifying = false; }
    }
    public void Dispose()
    { _disposed = true; ++_querySerial; Changed = null; _target = null; Access = null; }

    private sealed class ObservedPersistence : IPopupStoreBusinessStartPersistence
    {
        private readonly PopupStoreAdmissionHost _owner;
        internal ObservedPersistence(PopupStoreAdmissionHost owner) { _owner = owner; }
        public void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed)
            => _owner._persistence.Submit(session, request, response => Receive(completed, response));
        public void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed)
            => _owner._persistence.Query(session, request, response => Receive(completed, response));
        private void Receive(Action<PopupStoreBusinessStartResponse> completed, PopupStoreBusinessStartResponse response)
        { if (_owner._disposed) return; completed(response); _owner.Notify(); }
    }
}
