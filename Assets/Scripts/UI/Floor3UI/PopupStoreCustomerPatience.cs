using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A retained, popup-local timer identified by its order instance and slot.</summary>
public sealed class PopupStorePatienceTimer
{
    public PopupStoreOrder Order { get; }
    public string OrderId { get; }
    public string CustomerId { get; }
    public int SlotIndex { get; }
    public int PlushCount { get; }
    public double InitialPatience { get; }
    public double RemainingPatience { get; internal set; }
    public bool IsExpired { get; internal set; }
    public bool IsRunning => Eligible && !IsExpired && IdentityMatches && !Order.IsPacked
        && Owner != null && Owner.isActiveAndEnabled && Owner.IsReady && Owner.IsCurrent(this)
        && Clock != null && Clock.isActiveAndEnabled
        && Clock.State == PopupStoreBusinessState.Running;

    internal readonly PopupStoreCustomerPatience Owner;
    internal readonly PopupStoreBusinessClock Clock;
    internal bool Eligible, ExpirationPublished, PackingStopRecorded;
    internal double LastElapsed;
    internal bool IdentityMatches => Order != null && Order.RuntimeId == OrderId
        && Order.CustomerId == CustomerId && Order.PlushIds != null && Order.PlushIds.Count == PlushCount;

    internal PopupStorePatienceTimer(PopupStoreCustomerPatience owner, PopupStoreBusinessClock clock,
        PopupStoreOrder order, int slot, double duration)
    {
        Owner = owner; Clock = clock; Order = order; SlotIndex = slot;
        OrderId = order.RuntimeId; CustomerId = order.CustomerId; PlushCount = order.PlushIds.Count;
        InitialPatience = RemainingPatience = duration;
        LastElapsed = clock.ElapsedSeconds;
    }
}

[Serializable]
public sealed class PopupStorePatienceSlot
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _timeText;
    public GameObject Root => _root;
    public Image Fill => _fill;
    public TMP_Text TimeText => _timeText;
}

/// <summary>Consumes accepted business time and commits local expiration. No account or restaurant AI writes.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreCustomerPatience : MonoBehaviour
{
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private PopupStoreOrderDisplay _orders;
    [SerializeField, Min(0)] private double _onePlushSeconds = 18;
    [SerializeField, Min(0)] private double _twoPlushSeconds = 22;
    [SerializeField, Min(0)] private double _threePlushSeconds = 26;
    [SerializeField, Min(0)] private double _complaintPenaltySeconds = 3;
    [SerializeField] private PopupStorePatienceSlot[] _slots = new PopupStorePatienceSlot[3];
    [SerializeField] private bool _showGauge = true, _showTime = true;
    [SerializeField] private string _timeFormat = "{0:0}s";
    [SerializeField] private Color _normalColor = new Color(.45f, .70f, .49f);
    [SerializeField] private Color _cautionColor = new Color(.92f, .69f, .31f);
    [SerializeField] private Color _urgentColor = new Color(.84f, .40f, .36f);
    [SerializeField, Range(0, 1)] private float _cautionThreshold = .5f, _urgentThreshold = .2f;

    private readonly PopupStorePatienceTimer[] _timers = new PopupStorePatienceTimer[3];
    // Remember terminal identities, including after their slot is reused. A refresh cannot resurrect them.
    private readonly Dictionary<string, PopupStorePatienceTimer> _history = new Dictionary<string, PopupStorePatienceTimer>(StringComparer.Ordinal);
    private PopupStoreBusinessClock _boundClock;
    private PopupStoreOrderDisplay _boundOrders;
    private bool _refreshing, _refreshRequested;
    private string _lastError;

    public bool IsReady { get; private set; }
    public int ExpiredCount { get; private set; }
    public event Action<PopupStorePatienceTimer> Expired;
    public PopupStorePatienceTimer GetTimer(int slotIndex)
        => slotIndex >= 0 && slotIndex < _timers.Length ? _timers[slotIndex] : null;

    /// <summary>Consumers of a retained expiration signal must revalidate it before acting on its slot.</summary>
    public bool IsCurrent(PopupStorePatienceTimer timer)
        => timer != null && timer.Owner == this && timer.IdentityMatches
            && timer.SlotIndex >= 0 && timer.SlotIndex < _timers.Length
            && ReferenceEquals(_timers[timer.SlotIndex], timer)
            && _orders != null && _orders.TryGetEntryBinding(timer.SlotIndex, out var order, out _)
            && ReferenceEquals(order, timer.Order)
            && _orders.GetDisplayedOrderId(timer.SlotIndex) == timer.OrderId
            && _orders.GetDisplayedCustomerId(timer.SlotIndex) == timer.CustomerId;

    public bool Refresh()
    {
        if (_refreshing) { _refreshRequested = true; return IsReady; }
        _refreshing = true;
        try
        {
            if (!Validate(out string error))
            {
                IsReady = false; Unbind(); HideAll();
                if (_lastError != error) Debug.LogError("PopupStoreCustomerPatience: " + error, this);
                _lastError = error;
                return false;
            }
            Bind();
            // Settle only previously started orders BEFORE admitting a new Waiting order.
            _clock.SynchronizeNow();
            Settle(_clock.ElapsedSeconds);
            IsReady = true;
            _lastError = null;
            if (!isActiveAndEnabled || !_orders.isActiveAndEnabled || !_orders.IsReady)
            {
                // Whole-popup close retains the snapshot for final clock settlement. A broken/disabled
                // display in an otherwise active popup is unavailable and must not keep charging.
                if (isActiveAndEnabled)
                    foreach (var timer in _timers) if (timer != null) timer.Eligible = false;
                HideAll(); return true;
            }

            for (int i = 0; i < _timers.Length; i++)
            {
                var previous = _timers[i];
                if (previous != null && previous.Order.IsExpired && IsCurrent(previous))
                    GetComponent<PopupStoreCustomerEntrance>()?.TryStartComplaint(previous, false);
                if (!TryWaiting(i, out var order))
                {
                    if (previous != null) previous.Eligible = false;
                    var entrance = GetComponent<PopupStoreCustomerEntrance>();
                    if (!_orders.TryGetEntryBinding(i, out var current, out _)
                        || (entrance != null && entrance.ControlsOrders && entrance.GetState(i) == PopupStoreCustomerState.Empty)
                        || (previous != null && !ReferenceEquals(previous.Order, current))) _timers[i] = null;
                    continue;
                }
                if (_history.TryGetValue(order.RuntimeId, out var timer))
                {
                    if (!ReferenceEquals(timer.Order, order) || !timer.IdentityMatches || timer.SlotIndex != i)
                    { if (previous != null) previous.Eligible = false; _timers[i] = null; continue; }
                }
                else
                {
                    double duration = order.PlushIds.Count == 1 ? _onePlushSeconds
                        : order.PlushIds.Count == 2 ? _twoPlushSeconds : _threePlushSeconds;
                    timer = new PopupStorePatienceTimer(this, _clock, order, i, duration);
                    _history.Add(timer.OrderId, timer);
                    LogTimer("STARTED", timer);
                }
                if (previous != null && !ReferenceEquals(previous, timer)) previous.Eligible = false;
                _timers[i] = timer;
                timer.LastElapsed = _clock.ElapsedSeconds;
                timer.Eligible = !order.IsPacked;
            }
            // Handles an Inspector-authored zero duration once, without a separate time source.
            Settle(_clock.ElapsedSeconds);
            // Direct GameObject deactivation may settle the last foreground interval after this
            // component became inactive. Complete that retained timeout on the next valid binding;
            // an already-ended business clock still rejects it, and committed orders cannot repeat.
            TryProcessExpiredBatch(_timers);
            UpdateViews();
            return true;
        }
        finally
        {
            _refreshing = false;
            // A terminal observer may close/reopen or advance a slot. Reconcile the final binding
            // after the current atomic refresh instead of dropping that notification.
            if (_refreshRequested) { _refreshRequested = false; Refresh(); }
        }
    }

    private bool TryWaiting(int slot, out PopupStoreOrder order)
    {
        order = null;
        if (!isActiveAndEnabled || _orders == null || !_orders.isActiveAndEnabled || !_orders.IsReady
            || !_orders.TryGetEntryBinding(slot, out order, out _) || order.IsPacked || order.IsExpired
            || order.PlushIds == null || order.PlushIds.Count < 1 || order.PlushIds.Count > 3
            || string.IsNullOrEmpty(order.RuntimeId) || string.IsNullOrEmpty(order.CustomerId)
            || _orders.GetDisplayedMaterials(slot).Count != order.PlushIds.Count + 2) return false;
        var entrance = GetComponent<PopupStoreCustomerEntrance>();
        if (entrance != null && entrance.ControlsOrders && !entrance.IsWaiting(slot)) return false;
        var frame = _orders.Slots[slot]?.OrderFrame;
        return frame != null && frame.isActiveAndEnabled;
    }

    private void OnTimeAdvanced(double elapsed)
    {
        Settle(elapsed);
        UpdateViews();
    }

    private void Settle(double elapsed)
    {
        if (!Finite(elapsed) || elapsed < 0) return;
        List<PopupStorePatienceTimer> expired = null;
        foreach (var timer in _timers)
        {
            if (timer == null || elapsed < timer.LastElapsed) continue;
            double delta = elapsed - timer.LastElapsed;
            timer.LastElapsed = elapsed; // Commit before notifications or any nested clock reads.
            // IsPacked is already set before counter.Changed, earlier than the order UI notification.
            if (!timer.IdentityMatches) { timer.Eligible = false; continue; }
            if (timer.Order.IsPacked)
            {
                timer.Eligible = false;
                if (!timer.PackingStopRecorded) { timer.PackingStopRecorded = true; LogTimer("PACKED", timer); }
                continue;
            }
            if (!timer.Eligible || timer.IsExpired) continue;
            if (delta == 0 && _clock.State != PopupStoreBusinessState.Running) continue;
            // A malformed/replaced live binding may never charge the retained old order.
            if (_orders != null && _orders.IsReady && !IsCurrent(timer)) { timer.Eligible = false; continue; }
            timer.RemainingPatience = Math.Max(0, timer.RemainingPatience - delta);
            if (timer.RemainingPatience > 0) continue;
            timer.IsExpired = true;
            ExpiredCount++;
            if (expired == null) expired = new List<PopupStorePatienceTimer>();
            expired.Add(timer);
        }
        // All slots reach the same clock position before user callbacks can pack, close or refresh.
        if (expired != null)
        {
            TryProcessExpiredBatch(expired);
            foreach (var timer in expired) PublishExpired(timer);
        }
    }

    /// <summary>Accepts only this popup's current expired timers, never a customer ID alone.</summary>
    public int TryProcessExpiredBatch(IReadOnlyList<PopupStorePatienceTimer> expired)
    {
        // A natural deadline already committed by the clock wins a simultaneous timeout boundary.
        // Do not resample time here: this batch belongs to one already-settled clock instant.
        if (!isActiveAndEnabled || !IsReady || _clock == null || !_clock.isActiveAndEnabled
            || _clock.State != PopupStoreBusinessState.Running || expired == null
            || !NonNegative(_complaintPenaltySeconds) || !Finite(_complaintPenaltySeconds * 3)) return 0;
        var accepted = new List<PopupStorePatienceTimer>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var timer in expired)
        {
            if (timer == null || !timer.IsExpired || !IsCurrent(timer) || timer.Order.IsPacked || timer.Order.IsExpired
                || !TryWaiting(timer.SlotIndex, out var order) || !ReferenceEquals(order, timer.Order)
                || !ids.Add(timer.OrderId)) continue;
            accepted.Add(timer);
        }
        if (!_clock.CanAcceptComplaintBatch(accepted.Count, _complaintPenaltySeconds)) return 0;
        // All terminal decisions precede all notifications. A sibling cannot pack between two expirations.
        foreach (var timer in accepted)
        {
            if (!timer.Order.TryCommitExpiration()) return 0; // Validated, callback-free transition above.
            timer.Eligible = false;
        }
        var entrance = GetComponent<PopupStoreCustomerEntrance>();
        foreach (var timer in accepted) entrance?.TryStartComplaint(timer, false);
        int applied = _clock.ApplyComplaintPenalties(accepted, _complaintPenaltySeconds);
        _orders.ApplyCustomerEntryState();
        UpdateViews();
        return applied;
    }

    private void PublishExpired(PopupStorePatienceTimer timer)
    {
        if (timer.ExpirationPublished) return;
        timer.ExpirationPublished = true;
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_PATIENCE_EXPIRED order=" + timer.OrderId + "; customer=" + timer.CustomerId
                + "; slot=" + timer.SlotIndex + "; initial=" + timer.InitialPatience.ToString(CultureInfo.InvariantCulture), this);
#endif
        var handlers = Expired;
        if (handlers == null) return;
        foreach (Action<PopupStorePatienceTimer> handler in handlers.GetInvocationList())
            try { handler(timer); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void UpdateViews()
    {
        if (!IsReady || !isActiveAndEnabled || _slots == null) { HideAll(); return; }
        for (int i = 0; i < _slots.Length; i++)
        {
            var view = _slots[i];
            var timer = _timers[i];
            bool visible = timer != null && IsCurrent(timer) && TryWaiting(i, out _);
            view.Root.SetActive(visible && (_showGauge || _showTime));
            view.Fill.transform.parent.gameObject.SetActive(visible && _showGauge);
            view.Fill.enabled = visible && _showGauge;
            view.TimeText.enabled = visible && _showTime;
            if (!visible) continue;
            float ratio = timer.InitialPatience > 0 ? (float)(timer.RemainingPatience / timer.InitialPatience) : 0;
            view.Fill.fillAmount = Mathf.Clamp01(ratio);
            view.Fill.color = ratio <= _urgentThreshold ? _urgentColor : ratio <= _cautionThreshold ? _cautionColor : _normalColor;
            string text = string.Format(CultureInfo.InvariantCulture, _timeFormat, Math.Ceiling(timer.RemainingPatience));
            if (view.TimeText.text != text) view.TimeText.text = text;
        }
    }

    private bool Validate(out string error)
    {
        error = null;
        if (_clock == null || _clock.gameObject != gameObject || _orders == null || _orders.gameObject != gameObject
            || GetComponent<UIPopupStore>() == null)
            error = "Clock and order display must belong to this popup.";
        else if (!NonNegative(_onePlushSeconds) || !NonNegative(_twoPlushSeconds) || !NonNegative(_threePlushSeconds)
            || !NonNegative(_complaintPenaltySeconds) || !Finite(_complaintPenaltySeconds * 3)
            || !Finite(_urgentThreshold) || !Finite(_cautionThreshold) || _urgentThreshold < 0
            || _cautionThreshold > 1 || _urgentThreshold > _cautionThreshold)
            error = "Durations must be finite and non-negative; color thresholds must be ordered in 0-1.";
        else if (_slots == null || _slots.Length != 3 || _orders.Slots.Count != 3)
            error = "Three independent patience views are required.";
        else
        {
            var roots = new HashSet<GameObject>();
            var images = new HashSet<Image>();
            var texts = new HashSet<TMP_Text>();
            for (int i = 0; i < _slots.Length; i++)
            {
                var view = _slots[i];
                var frame = _orders.Slots[i]?.OrderFrame;
                if (view == null || view.Root == null || frame == null || view.Root.transform.parent != frame.transform.parent
                    || view.Root == frame.gameObject || view.Fill == null || view.TimeText == null
                    || !Owned(view.Root.transform) || !Owned(frame) || !Owned(view.Fill) || !Owned(view.TimeText)
                    || view.Root == _orders.Slots[i].CustomerImage?.gameObject
                    || !view.Fill.transform.IsChildOf(view.Root.transform) || !view.TimeText.transform.IsChildOf(view.Root.transform)
                    || view.Fill.transform.parent == view.Root.transform || view.Fill.transform.parent == null
                    || view.Fill.type != Image.Type.Filled || !roots.Add(view.Root) || !images.Add(view.Fill) || !texts.Add(view.TimeText))
                { error = "Each order needs its own sibling gauge root, filled Image and child TMP label."; break; }
            }
            if (error == null)
                try
                {
                    if (string.IsNullOrEmpty(_timeFormat)) error = "A time format is required.";
                    else string.Format(CultureInfo.InvariantCulture, _timeFormat, 0d);
                }
                catch (FormatException) { error = "The time format must accept remaining seconds as argument zero."; }
        }
        return error == null;
    }

    private void Bind()
    {
        if (_boundClock == _clock && _boundOrders == _orders) return;
        Unbind();
        _boundClock = _clock; _boundOrders = _orders;
        _boundClock.TimeAdvanced += OnTimeAdvanced;
        _boundClock.StateChanged += OnStateChanged;
        _boundOrders.Refreshed += OnOrdersChanged;
        _boundOrders.SlotChanged += OnSlotChanged;
    }
    private void Unbind()
    {
        if (_boundClock != null) { _boundClock.TimeAdvanced -= OnTimeAdvanced; _boundClock.StateChanged -= OnStateChanged; }
        if (_boundOrders != null) { _boundOrders.Refreshed -= OnOrdersChanged; _boundOrders.SlotChanged -= OnSlotChanged; }
        _boundClock = null; _boundOrders = null;
    }
    private void OnStateChanged() { if (_clock != null) Settle(_clock.ElapsedSeconds); UpdateViews(); }
    private void OnOrdersChanged() => Refresh();
    private void OnSlotChanged(int slot) => Refresh();
    private void HideAll()
    {
        if (_slots == null) return;
        foreach (var view in _slots)
            if (view?.Root != null && Owned(view.Root.transform) && view.Root != gameObject
                && !IsOrderView(view.Root)) view.Root.SetActive(false);
    }
    private bool Owned(Component item) => item != null && item.GetComponentInParent<UIPopupStore>(true) == GetComponent<UIPopupStore>();
    private bool IsOrderView(GameObject root)
    {
        if (_orders == null) return false;
        foreach (var slot in _orders.Slots)
            if (slot != null && (root == slot.OrderFrame?.gameObject || root == slot.CustomerImage?.gameObject)) return true;
        return false;
    }
    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        if (_clock != null) { _clock.SynchronizeNow(); Settle(_clock.ElapsedSeconds); }
        Unbind(); HideAll();
    }
    private void OnDestroy() => Unbind();
    private void LogTimer(string action, PopupStorePatienceTimer timer)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_PATIENCE_" + action + " order=" + timer.OrderId + "; customer=" + timer.CustomerId
                + "; slot=" + timer.SlotIndex + "; plush=" + timer.PlushCount
                + "; initial=" + timer.InitialPatience.ToString(CultureInfo.InvariantCulture)
                + "; remaining=" + timer.RemainingPatience.ToString("0.###", CultureInfo.InvariantCulture), this);
#endif
    }
#if UNITY_EDITOR
    [ContextMenu("Review/Inspect Customer Patience")]
    private void InspectPatience()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        Refresh();
        foreach (var timer in _timers) if (timer != null) LogTimer("STATE", timer);
    }
#endif
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool NonNegative(double value) => Finite(value) && value >= 0;
}
