using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class PopupStoreOrderSlot
{
    [SerializeField] private Image _customerImage;
    [SerializeField] private Image _orderFrame;
    [SerializeField] private Image[] _materialIcons = new Image[5];

    public Image CustomerImage => _customerImage;
    public Image OrderFrame => _orderFrame;
    public IReadOnlyList<Image> MaterialIcons => _materialIcons;
}

[Serializable]
public sealed class PopupStoreOrderCustomerVisual
{
    [SerializeField] private string _customerId;
    [SerializeField] private Sprite _sprite;

    public string CustomerId => _customerId;
    public Sprite Sprite => _sprite;
}

/// <summary>Displays local orders and packing completion. No spawning, delivery, inventory or account writes.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreOrderDisplay : MonoBehaviour
{
    [SerializeField] private PopupStoreMaterialCatalog _catalog;
    [SerializeField] private PopupStoreOrderSlot[] _slots = new PopupStoreOrderSlot[3];
    [SerializeField] private PopupStoreOrderCustomerVisual[] _customerVisuals = Array.Empty<PopupStoreOrderCustomerVisual>();
    [SerializeField] private PopupStoreOrder[] _sampleOrders = Array.Empty<PopupStoreOrder>();
    [SerializeField] private Vector2 _iconSize = new Vector2(64, 64);
    [SerializeField, Min(0)] private float _spacing = 8;
    [SerializeField] private Vector2 _padding = new Vector2(18, 18);

    // Capture successful bindings so malformed Inspector edits cannot leave the old order visible.
    private SlotView[] _lastViews;
    private string _lastError;
    private IReadOnlyList<PopupStoreMaterialEntry>[] _displayedMaterials = Array.Empty<IReadOnlyList<PopupStoreMaterialEntry>>();
    private string[] _displayedCustomerIds = Array.Empty<string>();
    private PopupStoreOrder[] _displayedOrders = Array.Empty<PopupStoreOrder>();
    private string[] _displayedOrderIds = Array.Empty<string>();
    private bool _packingLocked;
    // Preserve the recipient that actually belonged to the committed order, including across harmless refreshes.
    [NonSerialized] private readonly PackedDeliveryTarget[] _packedDeliveryTargets = new PackedDeliveryTarget[3];

    public bool IsReady { get; private set; }
    public int DisplayedCount { get; private set; }
    public event Action Refreshed;
    public IReadOnlyList<PopupStoreOrderSlot> Slots => _slots == null
        ? (IReadOnlyList<PopupStoreOrderSlot>)Array.Empty<PopupStoreOrderSlot>()
        : Array.AsReadOnly(_slots);

    public IReadOnlyList<PopupStoreMaterialEntry> GetDisplayedMaterials(int slotIndex)
        => IsReady && slotIndex >= 0 && slotIndex < _displayedMaterials.Length && !IsOrderPacked(slotIndex)
            ? _displayedMaterials[slotIndex] : Array.Empty<PopupStoreMaterialEntry>();

    public string GetDisplayedCustomerId(int slotIndex)
        => IsReady && slotIndex >= 0 && slotIndex < _displayedCustomerIds.Length
            ? _displayedCustomerIds[slotIndex] : null;

    public string GetDisplayedOrderId(int slotIndex)
        => IsReady && slotIndex >= 0 && slotIndex < _displayedOrderIds.Length
            ? _displayedOrderIds[slotIndex] : null;

    public bool IsOrderPacked(int slotIndex)
        => IsReady && slotIndex >= 0 && slotIndex < _displayedOrders.Length && _displayedOrders[slotIndex].IsPacked;

    /// <summary>Reads the original packed recipient; never rematches, fulfils or changes an order.</summary>
    public bool TryGetPackedDeliveryTarget(PopupStorePackingReceipt receipt, out RectTransform target, out string reason)
    {
        target = null;
        reason = null;
        var popup = GetComponent<UIPopupStore>();
        if (!isActiveAndEnabled || !IsReady || popup == null || !popup.isActiveAndEnabled)
        { reason = "The order display and its popup must be active and ready."; return false; }
        if (receipt == null || !receipt.Success || string.IsNullOrEmpty(receipt.ReceiptId)
            || string.IsNullOrEmpty(receipt.OrderId) || string.IsNullOrEmpty(receipt.CustomerId))
        { reason = "A successful receipt with its original order and customer identity is required."; return false; }

        int slot = receipt.SlotIndex;
        if (slot < 0 || slot >= DisplayedCount || slot >= _packedDeliveryTargets.Length
            || _sampleOrders == null || slot >= _sampleOrders.Length
            || slot >= _displayedOrders.Length || slot >= _displayedOrderIds.Length || slot >= _displayedCustomerIds.Length
            || _slots == null || slot >= _slots.Length || _lastViews == null || slot >= _lastViews.Length)
        { reason = "The receipt slot is not a currently displayed order slot."; return false; }

        var binding = _packedDeliveryTargets[slot];
        var order = _displayedOrders[slot];
        if (binding == null || order == null || !ReferenceEquals(_sampleOrders[slot], order)
            || !ReferenceEquals(binding.Order, order))
        { reason = "The original packed order has been replaced or is no longer bound to this slot."; return false; }
        if (!string.Equals(receipt.CustomerId, order.CustomerId, StringComparison.Ordinal)
            || !string.Equals(receipt.CustomerId, _displayedCustomerIds[slot], StringComparison.Ordinal)
            || !string.Equals(receipt.OrderId, order.RuntimeId, StringComparison.Ordinal)
            || !string.Equals(receipt.OrderId, _displayedOrderIds[slot], StringComparison.Ordinal))
        { reason = "The receipt customer or order identity no longer matches the displayed slot."; return false; }
        if (!order.IsPacked || !ReferenceEquals(order.PackingReceipt, receipt) || !ReferenceEquals(binding.Receipt, receipt))
        { reason = "The receipt is not the exact successful packing result bound to this order."; return false; }

        var image = binding.Customer;
        if (image == null || _slots[slot] == null || _lastViews[slot] == null
            || _slots[slot].CustomerImage != image || _lastViews[slot].Customer != image)
        { reason = "The packed customer's original image binding has changed."; return false; }
        if (!image.isActiveAndEnabled || image.sprite == null || !Owned(image)
            || image.GetComponentInParent<UIPopupStore>(true) != popup)
        { reason = "The packed customer's image is inactive, missing its sprite or outside this popup."; return false; }

        target = image.rectTransform;
        return true;
    }

    /// <summary>Reads the displayed order snapshot without selecting a recipient or changing either side.</summary>
    public PopupStoreExactMatchResult QueryExactMatches(PopupStoreSalesCounter counter)
    {
        var popup = GetComponent<UIPopupStore>();
        if (!isActiveAndEnabled || !IsReady || popup == null || !popup.isActiveAndEnabled
            || counter == null || !counter.isActiveAndEnabled || !counter.IsReady
            || counter.GetComponentInParent<UIPopupStore>(true) != popup)
            return PopupStoreExactMatchResult.Empty;

        var available = new IReadOnlyList<PopupStoreMaterialEntry>[_displayedMaterials.Length];
        for (int i = 0; i < available.Length; i++) available[i] = GetDisplayedMaterials(i);
        var slots = PopupStoreExactOrderMatch.FindExactSlots(_catalog, available, counter.Materials);
        if (slots.Count == 0) return PopupStoreExactMatchResult.Empty;
        var candidates = new PopupStoreExactOrderCandidate[slots.Count];
        for (int i = 0; i < slots.Count; i++)
            candidates[i] = new PopupStoreExactOrderCandidate(slots[i], _displayedCustomerIds[slots[i]], _displayedOrderIds[slots[i]]);
        return new PopupStoreExactMatchResult(candidates);
    }

    internal bool BeginPackingLock()
    {
        if (_packingLocked || !isActiveAndEnabled || !IsReady) return false;
        _packingLocked = true;
        return true;
    }

    internal void EndPackingLock()
    {
        _packingLocked = false;
        if (isActiveAndEnabled && !IsReady) Refresh();
    }

    internal bool TryCommitPacking(PopupStoreSalesCounter counter, PopupStoreExactOrderCandidate candidate,
        out PopupStorePackingReceipt receipt)
    {
        receipt = null;
        if (!_packingLocked || !CurrentOrdersMatchSnapshot()) return false;
        var current = QueryExactMatches(counter);
        if (current.Kind != PopupStoreExactMatchKind.Single) return false;
        var selected = current.Candidates[0];
        int slot = selected.SlotIndex;
        if (slot != candidate.SlotIndex || selected.OrderId != candidate.OrderId || selected.CustomerId != candidate.CustomerId
            || _sampleOrders == null || slot >= _sampleOrders.Length
            || !ReferenceEquals(_sampleOrders[slot], _displayedOrders[slot])) return false;
        var order = _displayedOrders[slot];
        // An Inspector/data edit that has not reached the displayed snapshot must not fulfil an old order.
        if (order.IsPacked || order.RuntimeId != selected.OrderId || order.CustomerId != selected.CustomerId
            || !order.TryResolve(_catalog, out var liveMaterials, out _)
            || !PopupStoreExactOrderMatch.IsExact(_catalog, liveMaterials, counter.Materials)) return false;

        var prepared = new PopupStorePackingReceipt(selected.OrderId, slot, selected.CustomerId, counter.Materials);
        // No events, layout changes or callbacks between these two state changes.
        if (!counter.ConsumePackingMaterialsSilently()) return false;
        order.CommitPacking(prepared);
        _packedDeliveryTargets[slot] = new PackedDeliveryTarget(order, prepared, _lastViews[slot].Customer);
        receipt = prepared;
        return true;
    }

    private bool CurrentOrdersMatchSnapshot()
    {
        if (_sampleOrders == null || _sampleOrders.Length != _displayedOrders.Length) return false;
        for (int i = 0; i < _sampleOrders.Length; i++)
        {
            var order = _sampleOrders[i];
            if (order == null || !ReferenceEquals(order, _displayedOrders[i])
                || order.CustomerId != _displayedCustomerIds[i] || order.RuntimeId != _displayedOrderIds[i]
                || !order.TryResolve(_catalog, out var live, out _)
                || !PopupStoreExactOrderMatch.IsExact(_catalog, _displayedMaterials[i], live)) return false;
        }
        return true;
    }

    internal void PublishPackingCompleted()
    {
        if (IsReady && isActiveAndEnabled)
            for (int i = 0; i < _displayedOrders.Length; i++)
                if (_displayedOrders[i].IsPacked) HideCompletedOrder(_lastViews[i]);
        // State is already committed. One observer failure cannot prevent the remaining UI updates.
        var handlers = Refreshed;
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    public bool Refresh()
    {
        if (_packingLocked) return IsReady;
        IsReady = false;
        DisplayedCount = 0;
        _displayedMaterials = Array.Empty<IReadOnlyList<PopupStoreMaterialEntry>>();
        _displayedCustomerIds = Array.Empty<string>();
        _displayedOrders = Array.Empty<PopupStoreOrder>();
        _displayedOrderIds = Array.Empty<string>();
        var views = CaptureViews(out string error);
        if (error != null) return Reject(error, views);
        if (!TryResolveOrders(out var resolved, out var portraits, out error)) return Reject(error, views);

        HideViews(_lastViews);
        HideViews(views);
        _lastViews = views;
        for (int i = 0; i < resolved.Length; i++) ShowOrder(views[i], portraits[i], resolved[i]);
        _displayedMaterials = new IReadOnlyList<PopupStoreMaterialEntry>[resolved.Length];
        _displayedCustomerIds = new string[resolved.Length];
        _displayedOrders = new PopupStoreOrder[resolved.Length];
        _displayedOrderIds = new string[resolved.Length];
        for (int i = 0; i < resolved.Length; i++)
        {
            _displayedMaterials[i] = Array.AsReadOnly((PopupStoreMaterialEntry[])resolved[i].Clone());
            _displayedCustomerIds[i] = _sampleOrders[i].CustomerId;
            _displayedOrders[i] = _sampleOrders[i];
            _displayedOrderIds[i] = _sampleOrders[i].EnsureRuntimeId();
            if (_sampleOrders[i].IsPacked) HideCompletedOrder(views[i]);
        }
        DisplayedCount = resolved.Length;
        IsReady = true;
        _lastError = null;
        Refreshed?.Invoke();
        return true;
    }

    [ContextMenu("Refresh Orders")]
    private void RefreshOrders() => Refresh();

#if UNITY_EDITOR
    [ContextMenu("Diagnostics/Log Exact Order Matches (Play Mode)")]
    private void LogExactOrderMatches()
    {
        var popup = GetComponent<UIPopupStore>();
        var counters = popup != null ? popup.GetComponentsInChildren<PopupStoreSalesCounter>(true) : null;
        if (!Application.isPlaying || !isActiveAndEnabled || !IsReady || popup == null
            || !popup.isActiveAndEnabled || counters == null || counters.Length != 1
            || !counters[0].isActiveAndEnabled || !counters[0].IsReady)
        {
            Debug.Log("POPUP_STORE_EXACT_MATCH: unavailable; use the active, ready popup in Play Mode.", this);
            return;
        }

        var counter = counters[0];
        var result = QueryExactMatches(counter);
        var text = new System.Text.StringBuilder("POPUP_STORE_EXACT_MATCH: popup=")
            .Append(popup.GetInstanceID()).Append("; counter=[");
        for (int i = 0; i < counter.Materials.Count; i++)
        {
            if (i != 0) text.Append(", ");
            text.Append(counter.Materials[i].Kind).Append(':').Append(counter.Materials[i].Id);
        }
        text.Append("]; displayed=").Append(DisplayedCount).Append("; result=").Append(result.Kind)
            .Append("; count=").Append(result.Candidates.Count).Append("; candidates=[");
        for (int i = 0; i < result.Candidates.Count; i++)
        {
            if (i != 0) text.Append(", ");
            text.Append("slot=").Append(result.Candidates[i].SlotIndex)
                .Append('/').Append(result.Candidates[i].CustomerId);
        }
        Debug.Log(text.Append(']').ToString(), this);
    }
#endif

    private SlotView[] CaptureViews(out string error)
    {
        error = null;
        var views = new SlotView[_slots != null ? _slots.Length : 0];
        if (_slots == null || _slots.Length != 3)
            error = "Exactly three authored order slots are required.";
        if (_slots == null) return views;
        var uniqueImages = new HashSet<Image>();
        var uniquePlaces = new HashSet<Transform>();
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (!TryCapture(slot, out views[i]))
            { if (error == null) error = "Each order slot requires its own customer, frame and five child material icons."; continue; }
            var view = views[i];
            if (!uniquePlaces.Add(view.Frame.transform.parent) || !uniqueImages.Add(view.Customer)
                || !uniqueImages.Add(view.Frame))
                if (error == null) error = "Order slots cannot share a customer place or image references.";
            foreach (var icon in view.Icons)
                if (!uniqueImages.Add(icon) && error == null) error = "Material icons must be distinct across all order slots.";
            if (view.Frame.sprite == null && error == null) error = "Every order frame needs its authored sprite.";
        }
        if (!Positive(_iconSize.x) || !Positive(_iconSize.y) || !NonNegative(_spacing)
            || !NonNegative(_padding.x) || !NonNegative(_padding.y))
        { if (error == null) error = "Icon size must be positive; spacing and padding must be non-negative."; }
        if (error == null)
            foreach (var view in views)
                if (!Positive(view.Frame.rectTransform.rect.width - _padding.x * 2 - _spacing * 4)
                    || !Positive(view.Frame.rectTransform.rect.height - _padding.y * 2))
                { error = "Each order frame must have positive inner space for five material icons."; break; }
        return views;
    }

    private bool TryCapture(PopupStoreOrderSlot slot, out SlotView view)
    {
        view = null;
        if (slot == null || !Owned(slot.CustomerImage) || !Owned(slot.OrderFrame)
            || slot.CustomerImage == slot.OrderFrame || slot.CustomerImage.transform.parent == null
            || slot.CustomerImage.transform.parent != slot.OrderFrame.transform.parent
            || slot.OrderFrame.transform.parent.parent != transform
            || slot.MaterialIcons == null || slot.MaterialIcons.Count != 5) return false;
        var icons = new Image[5];
        for (int i = 0; i < icons.Length; i++)
        {
            var icon = slot.MaterialIcons[i];
            if (!Owned(icon) || icon.transform.parent != slot.OrderFrame.transform) return false;
            icons[i] = icon;
        }
        view = new SlotView { Customer = slot.CustomerImage, Frame = slot.OrderFrame, Icons = icons };
        return true;
    }

    private bool TryResolveOrders(out PopupStoreMaterialEntry[][] resolved, out Sprite[] portraits, out string error)
    {
        resolved = Array.Empty<PopupStoreMaterialEntry[]>();
        portraits = Array.Empty<Sprite>();
        error = null;
        if (_catalog == null)
        { error = "The approved material catalog is required."; return false; }
        if (!_catalog.TryValidate(out error)) return false;
        if (_sampleOrders == null || _sampleOrders.Length > 3)
        { error = "Sample orders must be a non-null array containing zero to three orders."; return false; }
        if (_customerVisuals == null || _customerVisuals.Length == 0)
        { error = "Authored customer visuals are required."; return false; }
        var customers = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (var visual in _customerVisuals)
        {
            if (visual == null || string.IsNullOrWhiteSpace(visual.CustomerId) || visual.Sprite == null
                || customers.ContainsKey(visual.CustomerId))
            { error = "Customer visuals require distinct IDs and non-null sprites."; return false; }
            customers.Add(visual.CustomerId, visual.Sprite);
        }
        var entries = new PopupStoreMaterialEntry[_sampleOrders.Length][];
        var sprites = new Sprite[_sampleOrders.Length];
        for (int i = 0; i < _sampleOrders.Length; i++)
        {
            var order = _sampleOrders[i];
            if (order == null)
            { error = "Sample orders cannot contain a null order."; return false; }
            if (!order.TryResolve(_catalog, out entries[i], out error)) return false;
            if (!customers.TryGetValue(order.CustomerId, out sprites[i]))
            { error = "Missing authored customer visual: " + order.CustomerId; return false; }
        }
        resolved = entries;
        portraits = sprites;
        return true;
    }

    private void ShowOrder(SlotView view, Sprite portrait, PopupStoreMaterialEntry[] materials)
    {
        view.Customer.sprite = portrait;
        view.Customer.preserveAspect = true;
        view.Customer.raycastTarget = false;
        view.Customer.gameObject.SetActive(true);
        view.Frame.raycastTarget = false;
        view.Frame.gameObject.SetActive(true);

        Vector2 inner = view.Frame.rectTransform.rect.size - _padding * 2;
        float scale = Mathf.Min(1, (inner.x - _spacing * (materials.Length - 1)) / (_iconSize.x * materials.Length), inner.y / _iconSize.y);
        Vector2 size = _iconSize * scale;
        float width = size.x * materials.Length + _spacing * (materials.Length - 1);
        for (int i = 0; i < materials.Length; i++)
        {
            var icon = view.Icons[i];
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-width * .5f + size.x * .5f + i * (size.x + _spacing), 0);
            icon.sprite = materials[i].Sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(true);
        }
    }

    private bool Reject(string error, SlotView[] views)
    {
        _displayedMaterials = Array.Empty<IReadOnlyList<PopupStoreMaterialEntry>>();
        _displayedCustomerIds = Array.Empty<string>();
        _displayedOrders = Array.Empty<PopupStoreOrder>();
        _displayedOrderIds = Array.Empty<string>();
        HideViews(_lastViews);
        HideViews(views);
        if (_lastError != error) Debug.LogError("PopupStoreOrderDisplay: " + error, this);
        _lastError = error;
        Refreshed?.Invoke();
        return false;
    }

    private void HideViews(SlotView[] views)
    {
        if (views == null) return;
        foreach (var view in views)
        {
            if (view == null) continue;
            if (Owned(view.Customer)) { view.Customer.sprite = null; view.Customer.gameObject.SetActive(false); }
            if (Owned(view.Frame)) view.Frame.gameObject.SetActive(false);
            foreach (var icon in view.Icons)
                if (Owned(icon)) { icon.sprite = null; icon.gameObject.SetActive(false); }
        }
    }

    private void HideCompletedOrder(SlotView view)
    {
        if (view == null) return;
        if (Owned(view.Frame)) view.Frame.gameObject.SetActive(false);
        foreach (var icon in view.Icons)
            if (Owned(icon)) { icon.sprite = null; icon.gameObject.SetActive(false); }
        // The original customer portrait remains in place; no departure or delivery is performed.
    }

    private bool Owned(Component component)
        => component != null && component.GetComponentInParent<PopupStoreOrderDisplay>(true) == this;
    private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool NonNegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        HideViews(_lastViews);
        IsReady = false;
        DisplayedCount = 0;
        _displayedMaterials = Array.Empty<IReadOnlyList<PopupStoreMaterialEntry>>();
        _displayedCustomerIds = Array.Empty<string>();
        _displayedOrders = Array.Empty<PopupStoreOrder>();
        _displayedOrderIds = Array.Empty<string>();
        Refreshed?.Invoke();
    }

    private sealed class PackedDeliveryTarget
    {
        internal readonly PopupStoreOrder Order;
        internal readonly PopupStorePackingReceipt Receipt;
        internal readonly Image Customer;

        internal PackedDeliveryTarget(PopupStoreOrder order, PopupStorePackingReceipt receipt, Image customer)
        {
            Order = order;
            Receipt = receipt;
            Customer = customer;
        }
    }

    private sealed class SlotView
    {
        internal Image Customer, Frame;
        internal Image[] Icons;
    }
}
