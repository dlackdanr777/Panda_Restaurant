using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStoreCustomerState { Empty, Entering, Waiting, Packed, Leaving }

/// <summary>Three authored customers, local to this popup instance. No restaurant AI, order generation or account storage.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreCustomerEntrance : MonoBehaviour
{
    [Tooltip("Disable before Play to inspect the original static sample orders.")]
    [SerializeField] private bool _automaticEntry = true;
    [SerializeField] private PopupStoreOrderDisplay _orders;
    [Tooltip("The visible popup boundary. Customers begin beyond its right edge.")]
    [SerializeField] private RectTransform _entryBoundary;
    [Tooltip("Right-edge clearance and vertical starting offset, in boundary-local UI units.")]
    [SerializeField] private Vector2 _startOffset = new Vector2(64, 0);
    [SerializeField, Min(0)] private float _entryDuration = 1.2f;
    [Tooltip("Interval between slot starts. Four seconds is a review default, not a business-session rule.")]
    [SerializeField, Min(0)] private float _entryInterval = 4;
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Header("Departure after delivery visuals end")]
    [SerializeField] private PopupStorePackingPresentation _presentation;
    [SerializeField] private RectTransform _exitBoundary;
    [Tooltip("Left-edge clearance and vertical exit offset, in boundary-local UI units.")]
    [SerializeField] private Vector2 _exitOffset = new Vector2(64, 0);
    [SerializeField, Min(0)] private float _exitDuration = 1.2f;
    [SerializeField] private AnimationCurve _exitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private readonly Slot[] _slots = new Slot[3];
    private readonly Vector3[] _corners = new Vector3[4];
    private bool _started, _initialized, _running, _appliedAutomatic;
    private float _visibleElapsed;
    private string _lastError;
    private PopupStorePackingPresentation _boundPresentation;
    private readonly HashSet<string> _handledVisuals = new HashSet<string>(StringComparer.Ordinal);

    // The inspection mode is chosen before the session; changing it cannot resurrect released orders.
    public bool ControlsOrders => _initialized || (_automaticEntry && (Application.isPlaying || _started));
    public bool IsReady { get; private set; }
    public bool IsRunning => _running;
    public int StartedEntranceCount { get; private set; }
    public int ArrivedCount { get; private set; }
    public int StartedDepartureCount { get; private set; }
    public int ReleasedCount { get; private set; }

    public PopupStoreCustomerState GetState(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return PopupStoreCustomerState.Empty;
        if (!ControlsOrders)
            return _orders != null && _orders.TryGetEntryBinding(slotIndex, out var legacy, out _)
                ? legacy.IsPacked ? PopupStoreCustomerState.Packed : PopupStoreCustomerState.Waiting
                : PopupStoreCustomerState.Empty;
        var slot = _slots[slotIndex];
        if (!IsReady || !isActiveAndEnabled || !BindingMatches(slotIndex, slot)) return PopupStoreCustomerState.Empty;
        if (slot.Released) return PopupStoreCustomerState.Empty;
        if (slot.State == PopupStoreCustomerState.Leaving) return slot.State;
        return slot.Order.IsPacked ? PopupStoreCustomerState.Packed : slot.State;
    }

    public bool IsWaiting(int slotIndex)
        => _running && GetState(slotIndex) == PopupStoreCustomerState.Waiting;

    public bool BeginVisibleSession()
    {
        if (!isActiveAndEnabled) return false;
        _started = true;
        SynchronizeOrders();
        if (_orders != null && _orders.IsReady) _orders.ApplyCustomerEntryState();
        return IsReady;
    }

    public void PauseVisibleSession()
    {
        _running = false;
        IsReady = false;
        UnbindPresentation();
        // Keep elapsed time, original destinations, identities and Packed data on this instance.
        if (_orders != null && _orders.IsReady) _orders.ApplyCustomerEntryState();
    }

    /// <summary>Called after OrderDisplay has resolved its snapshot, before it publishes visibility.</summary>
    public void SynchronizeOrders()
    {
        if (!ControlsOrders)
        {
            UnbindPresentation();
            RestoreHomes();
            _running = false;
            IsReady = false;
            _appliedAutomatic = false;
            return;
        }
        _started = true;
        _appliedAutomatic = true;
        IsReady = false;
        _running = false;
        if (!isActiveAndEnabled || _orders == null || !_orders.IsReady) return;
        if (!Validate(out string error)) { Report(error); return; }
        BindPresentation();
        if (!_initialized)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_orders.TryGetEntryBinding(i, out var order, out var image)) continue;
                var slot = new Slot(order, image);
                _slots[i] = slot;
                for (int j = 0; j < i; j++)
                    if (_slots[j] != null && (ReferenceEquals(_slots[j].Order, order) || _slots[j].OrderId == slot.OrderId))
                    { Cancel(i, slot, "One order identity cannot occupy two entrance slots."); break; }
            }
            _initialized = true;
        }
        IsReady = true;
        _running = true;
        _lastError = null;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.Blocked) continue;
            if (!BindingMatches(i, slot)) { Cancel(i, slot, "The original customer/order/image binding changed."); continue; }
            if (!Position(slot, out error)) Cancel(i, slot, error);
        }
        // Includes closed/cancelled receipts. This is a visual cleanup, never a second delivery or sale.
        foreach (var outcome in _presentation.VisualOutcomes) TryStartDeparture(outcome);
    }

    public void TickEntrance(float unscaledDelta)
    {
        if (!ControlsOrders)
        {
            if (_appliedAutomatic)
            {
                SynchronizeOrders();
                if (_orders != null && _orders.IsReady) _orders.ApplyCustomerEntryState();
            }
            return;
        }
        if (!isActiveAndEnabled || !_running || _orders == null || !_orders.IsReady) return;
        if (!Finite(unscaledDelta) || unscaledDelta < 0 || !Validate(out string error))
        {
            Report("Entrance delta and settings must be finite and non-negative.");
            PauseVisibleSession();
            return;
        }
        _visibleElapsed += unscaledDelta;
        if (!Finite(_visibleElapsed))
        { Report("Entrance elapsed time must remain finite."); PauseVisibleSession(); return; }
        bool changed = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.Blocked) continue;
            if (!BindingMatches(i, slot))
            { Cancel(i, slot, "The original customer/order/image binding changed."); changed = true; continue; }
            if (slot.Released) continue;
            if (slot.State == PopupStoreCustomerState.Leaving)
            {
                slot.ExitElapsed += unscaledDelta;
                slot.ExitProgress = _exitDuration > 0 ? Mathf.Clamp01(slot.ExitElapsed / _exitDuration) : 1;
                if (!PositionDeparture(slot, out error))
                { Cancel(i, slot, error); changed = true; continue; }
                if (slot.ExitProgress >= 1)
                {
                    slot.Released = true;
                    slot.State = PopupStoreCustomerState.Empty;
                    ReleasedCount++;
                    // Hide before resetting the reusable transform. Completed order history remains intact.
                    slot.Image.gameObject.SetActive(false);
                    RestoreHome(slot);
                    changed = true;
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Debug.Log("POPUP_STORE_CUSTOMER_RELEASED receipt=" + slot.DepartureReceiptId
                            + "; slot=" + i + "; customer=" + slot.CustomerId, this);
#endif
                }
                continue;
            }
            if (slot.Order.IsPacked) continue;
            if (slot.State == PopupStoreCustomerState.Empty && _visibleElapsed >= i * _entryInterval)
            {
                slot.State = PopupStoreCustomerState.Entering;
                slot.StartTime = i * _entryInterval;
                StartedEntranceCount++;
                changed = true;
            }
            if (slot.State == PopupStoreCustomerState.Entering)
            {
                slot.Progress = _entryDuration > 0 ? Mathf.Clamp01((_visibleElapsed - slot.StartTime) / _entryDuration) : 1;
                if (!Position(slot, out error))
                { Cancel(i, slot, error); changed = true; continue; }
                if (slot.Progress >= 1)
                {
                    slot.State = PopupStoreCustomerState.Waiting;
                    ArrivedCount++;
                    changed = true;
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Debug.Log("POPUP_STORE_CUSTOMER_ARRIVED slot=" + i + "; customer=" + slot.CustomerId + "; order=" + slot.OrderId, this);
#endif
                }
            }
        }
        // Existing progress refreshes without feedback. Arrival never calls TryPack or mutates the counter.
        if (changed) _orders.ApplyCustomerEntryState();
    }

    private bool BindingMatches(int index, Slot slot)
    {
        if (slot == null || slot.Blocked || _orders == null
            || !_orders.TryGetEntryBinding(index, out var order, out var image)) return false;
        return ReferenceEquals(order, slot.Order) && image == slot.Image
            && order.RuntimeId == slot.OrderId && order.CustomerId == slot.CustomerId
            && image.enabled && image.sprite == slot.Sprite && image.transform.parent == slot.Parent;
    }

    private bool Position(Slot slot, out string error)
    {
        error = null;
        var rect = slot.Image.rectTransform;
        if (slot.Released) { RestoreHome(slot); return true; }
        if (slot.State == PopupStoreCustomerState.Leaving) return PositionDeparture(slot, out error);
        if (slot.Order.IsPacked || slot.State == PopupStoreCustomerState.Waiting)
        { rect.anchoredPosition3D = slot.Home; return true; }
        // Resolve the authored anchored destination again after layout/CanvasScaler changes.
        rect.anchoredPosition3D = slot.Home;
        Vector3 destination = rect.localPosition;
        rect.GetWorldCorners(_corners);
        float leftEdge = float.PositiveInfinity;
        foreach (var corner in _corners) leftEdge = Mathf.Min(leftEdge, _entryBoundary.InverseTransformPoint(corner).x);
        float distance = Mathf.Max(0, _entryBoundary.rect.xMax + _startOffset.x - leftEdge);
        Vector3 offset = slot.Parent.InverseTransformVector(_entryBoundary.TransformVector(new Vector3(distance, _startOffset.y, 0)));
        float travel = slot.Progress <= 0 ? 0 : slot.Progress >= 1 ? 1 : _moveCurve.Evaluate(slot.Progress);
        if (!Finite(distance) || !Finite(travel) || !Finite(offset.x) || !Finite(offset.y) || !Finite(offset.z))
        { error = "Entrance positions and curve output must be finite."; return false; }
        rect.localPosition = Vector3.Lerp(destination + offset, destination, Mathf.Clamp01(travel));
        return true;
    }

    private bool PositionDeparture(Slot slot, out string error)
    {
        error = null;
        var rect = slot.Image.rectTransform;
        rect.anchoredPosition3D = slot.Home;
        Vector3 origin = rect.localPosition;
        rect.GetWorldCorners(_corners);
        float rightEdge = float.NegativeInfinity;
        foreach (var corner in _corners) rightEdge = Mathf.Max(rightEdge, _exitBoundary.InverseTransformPoint(corner).x);
        float distance = Mathf.Min(0, _exitBoundary.rect.xMin - _exitOffset.x - rightEdge);
        Vector3 offset = slot.Parent.InverseTransformVector(_exitBoundary.TransformVector(new Vector3(distance, _exitOffset.y, 0)));
        float travel = slot.ExitProgress <= 0 ? 0 : slot.ExitProgress >= 1 ? 1 : _exitCurve.Evaluate(slot.ExitProgress);
        if (!Finite(distance) || !Finite(travel) || !Finite(offset.x) || !Finite(offset.y) || !Finite(offset.z))
        { error = "Departure positions and curve output must be finite."; return false; }
        rect.localPosition = Vector3.Lerp(origin, origin + offset, Mathf.Clamp01(travel));
        return true;
    }

    private void BindPresentation()
    {
        if (_boundPresentation == _presentation) return;
        UnbindPresentation();
        _boundPresentation = _presentation;
        _boundPresentation.VisualEnded += HandleVisualEnded;
    }

    private void UnbindPresentation()
    {
        if (_boundPresentation != null) _boundPresentation.VisualEnded -= HandleVisualEnded;
        _boundPresentation = null;
    }

    private void HandleVisualEnded(PopupStoreVisualOutcome outcome)
    {
        // Closing can run after other components' OnDisable. Recover cancellations only on rebind.
        if (outcome == null || outcome.Kind != PopupStoreVisualOutcomeKind.VisualCompleted) return;
        if (TryStartDeparture(outcome)) _orders.ApplyCustomerEntryState();
    }

    private bool TryStartDeparture(PopupStoreVisualOutcome outcome)
    {
        if (!ControlsOrders || !IsReady || !_running || !isActiveAndEnabled || outcome == null
            || outcome.Receipt == null || _presentation == null) return false;
        bool recorded = false;
        foreach (var terminal in _presentation.VisualOutcomes)
            if (ReferenceEquals(terminal, outcome)) { recorded = true; break; }
        var receipt = outcome.Receipt;
        if (!recorded || string.IsNullOrEmpty(receipt.ReceiptId) || !_handledVisuals.Add(receipt.ReceiptId)) return false;
        int index = receipt.SlotIndex;
        var slot = index >= 0 && index < _slots.Length ? _slots[index] : null;
        string reason = null;
        if (!BindingMatches(index, slot) || slot.Released || slot.State != PopupStoreCustomerState.Waiting
            || !slot.Order.IsPacked || !ReferenceEquals(slot.Order.PackingReceipt, receipt)
            || slot.OrderId != receipt.OrderId || slot.CustomerId != receipt.CustomerId)
            reason = "Receipt no longer identifies the original packed customer/order/slot.";
        else if (!_orders.TryGetCommittedCustomerBinding(receipt, out var current, out reason))
            reason = reason ?? "The original committed customer binding is unavailable.";
        else if (outcome.Target == null || current != outcome.Target || current != slot.Image.rectTransform)
            reason = "The delivered customer rectangle is not the original slot image.";
        if (reason != null)
        {
            Debug.LogWarning("POPUP_STORE_DEPARTURE_REJECTED receipt=" + receipt.ReceiptId
                + "; slot=" + index + "; reason=" + reason, this);
            return false;
        }
        slot.DepartureReceiptId = receipt.ReceiptId;
        slot.State = PopupStoreCustomerState.Leaving;
        slot.ExitElapsed = 0;
        slot.ExitProgress = 0;
        StartedDepartureCount++;
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_CUSTOMER_LEAVING receipt=" + receipt.ReceiptId + "; slot=" + index
                + "; customer=" + slot.CustomerId + "; outcome=" + outcome.Kind, this);
#endif
        return true;
    }

    private bool Validate(out string error)
    {
        error = null;
        var popup = GetComponent<UIPopupStore>();
        if (popup == null || _orders == null || _orders.gameObject != gameObject || _entryBoundary == null
            || _entryBoundary.GetComponentInParent<UIPopupStore>(true) != popup
            || _presentation == null || _presentation.gameObject != gameObject || _exitBoundary == null
            || _exitBoundary.GetComponentInParent<UIPopupStore>(true) != popup)
            error = "Entrance orders and visible boundary must belong to this popup.";
        else if (!Finite(_entryDuration) || _entryDuration < 0 || !Finite(_entryInterval) || _entryInterval < 0
            || !Finite(_startOffset.x) || _startOffset.x < 0 || !Finite(_startOffset.y)
            || !Finite(_entryBoundary.rect.width) || _entryBoundary.rect.width <= 0
            || _moveCurve == null || _moveCurve.length == 0
            || !Finite(_exitDuration) || _exitDuration < 0 || !Finite(_exitOffset.x) || _exitOffset.x < 0
            || !Finite(_exitOffset.y) || !Finite(_exitBoundary.rect.width) || _exitBoundary.rect.width <= 0
            || _exitCurve == null || _exitCurve.length == 0)
            error = "Entrance requires a positive boundary width and finite duration, interval, offset and curve.";
        else foreach (var key in _moveCurve.keys)
            if (!Finite(key.time) || !Finite(key.value)) { error = "Entrance curve keys must be finite."; break; }
        if (error == null) foreach (var key in _exitCurve.keys)
            if (!Finite(key.time) || !Finite(key.value)) { error = "Departure curve keys must be finite."; break; }
        return error == null;
    }

    private void Cancel(int index, Slot slot, string reason)
    {
        if (slot.Blocked) return;
        slot.Blocked = true;
        slot.State = PopupStoreCustomerState.Empty;
        RestoreHome(slot);
        Debug.LogWarning("POPUP_STORE_ENTRY_CANCELLED slot=" + index + "; reason=" + reason, this);
    }

    private void RestoreHomes() { foreach (var slot in _slots) RestoreHome(slot); }
    private void RestoreHome(Slot slot)
    {
        if (slot != null && slot.Image != null && slot.Image.transform.parent == slot.Parent
            && slot.Image.GetComponentInParent<UIPopupStore>(true) == GetComponent<UIPopupStore>())
            slot.Image.rectTransform.anchoredPosition3D = slot.Home;
    }
    private void Report(string error)
    {
        if (_lastError == error) return;
        _lastError = error;
        Debug.LogError("PopupStoreCustomerEntrance: " + error, this);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnEnable() { if (Application.isPlaying) BeginVisibleSession(); }
    private void OnDisable() => PauseVisibleSession();
    private void OnDestroy() => UnbindPresentation();
    private void Update() { if (Application.isPlaying) TickEntrance(Time.unscaledDeltaTime); }

    private sealed class Slot
    {
        internal readonly PopupStoreOrder Order;
        internal readonly string OrderId, CustomerId;
        internal readonly Image Image;
        internal readonly Sprite Sprite;
        internal readonly Transform Parent;
        internal readonly Vector3 Home;
        internal PopupStoreCustomerState State;
        internal float Progress, StartTime;
        internal bool Blocked;
        internal bool Released;
        internal float ExitElapsed, ExitProgress;
        internal string DepartureReceiptId;
        internal Slot(PopupStoreOrder order, Image image)
        {
            Order = order; OrderId = order.RuntimeId; CustomerId = order.CustomerId;
            Image = image; Sprite = image.sprite; Parent = image.transform.parent;
            Home = image.rectTransform.anchoredPosition3D;
        }
    }
}
