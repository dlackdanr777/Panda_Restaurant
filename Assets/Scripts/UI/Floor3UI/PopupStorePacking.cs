using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStorePackingStatus { Unavailable, Busy, None, Multiple, Stale, Succeeded }

/// <summary>Receipt value; deliberately holds no live catalog or sprite reference.</summary>
public readonly struct PopupStorePackedMaterial
{
    public PopupStoreMaterialKind Kind { get; }
    public string Id { get; }

    public PopupStorePackedMaterial(PopupStoreMaterialKind kind, string id)
    {
        Kind = kind;
        Id = id;
    }
}

public sealed class PopupStorePackingReceipt
{
    public string ReceiptId { get; }
    public string OrderId { get; }
    public int SlotIndex { get; }
    public string CustomerId { get; }
    public IReadOnlyList<PopupStorePackedMaterial> Materials { get; }
    public string BoxId { get; }
    public string RibbonId { get; }
    public bool Success => true;

    public PopupStorePackingReceipt(string orderId, int slotIndex, string customerId,
        IReadOnlyList<PopupStoreMaterialEntry> materials)
    {
        if (materials == null) throw new ArgumentNullException(nameof(materials));
        var copy = new PopupStorePackedMaterial[materials.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            var material = materials[i];
            if (material == null) throw new ArgumentException("Receipt materials must be resolved.", nameof(materials));
            copy[i] = new PopupStorePackedMaterial(material.Kind, material.Id);
            if (material.Kind == PopupStoreMaterialKind.Box) BoxId = material.Id;
            if (material.Kind == PopupStoreMaterialKind.Ribbon) RibbonId = material.Id;
        }
        ReceiptId = Guid.NewGuid().ToString("N");
        OrderId = orderId;
        SlotIndex = slotIndex;
        CustomerId = customerId;
        Materials = Array.AsReadOnly(copy);
    }
}

public sealed class PopupStorePackingResult
{
    public PopupStorePackingStatus Status { get; }
    public bool Success => Status == PopupStorePackingStatus.Succeeded && Receipt != null;
    public PopupStoreExactMatchResult Matches { get; }
    public PopupStorePackingReceipt Receipt { get; }

    public PopupStorePackingResult(PopupStorePackingStatus status,
        PopupStoreExactMatchResult matches = null, PopupStorePackingReceipt receipt = null)
    {
        Status = status;
        Matches = matches ?? PopupStoreExactMatchResult.Empty;
        Receipt = receipt;
    }
}

/// <summary>Commits one exact local order. No delivery, rewards, account writes or animation.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePacking : MonoBehaviour
{
    [SerializeField] private PopupStoreSalesCounter _counter;
    [SerializeField] private PopupStoreOrderDisplay _orders;
    [SerializeField] private Button _packButton;

    private readonly Queue<PopupStorePackingReceipt> _receipts = new Queue<PopupStorePackingReceipt>();
    private PopupStoreSalesCounter _boundCounter;
    private PopupStoreOrderDisplay _boundOrders;
    private Button _boundPackButton;
    private bool _busy;
    private string _lastError;

    public bool IsReady { get; private set; }
    public PopupStorePackingResult LastResult { get; private set; }
        = new PopupStorePackingResult(PopupStorePackingStatus.Unavailable);
    public PopupStorePackingReceipt LastReceipt { get; private set; }
    public int SuccessfulPackCount { get; private set; }

    public bool Refresh()
    {
        if (_busy) return IsReady;
        Unbind();
        IsReady = false;
        if (!ValidateReferences(out string error))
        {
            if (OwnsPackButton()) _packButton.interactable = false;
            if (_lastError != error) Debug.LogError("PopupStorePacking: " + error, this);
            _lastError = error;
            return false;
        }
        _lastError = null;
        IsReady = true;
        _boundCounter = _counter;
        _boundOrders = _orders;
        _boundPackButton = _packButton;
        _boundCounter.Changed += OnCounterChanged;
        _boundOrders.Refreshed += UpdateButtonState;
        _boundPackButton.onClick.AddListener(OnPackClicked);
        UpdateButtonState();
        return true;
    }

    public PopupStorePackingResult TryPack()
    {
        // An observer may try again while successful state is being published. It cannot replace that result.
        if (_busy) return new PopupStorePackingResult(PopupStorePackingStatus.Busy);
        if (!CanPack()) return Remember(PopupStorePackingStatus.Unavailable);

        bool counterLocked = false;
        bool ordersLocked = false;
        _busy = true;
        UpdateButtonState();
        try
        {
            var matches = _orders.QueryExactMatches(_counter);
            if (matches.Kind == PopupStoreExactMatchKind.None)
                return Remember(PopupStorePackingStatus.None, matches);
            if (matches.Kind == PopupStoreExactMatchKind.Multiple)
                return Remember(PopupStorePackingStatus.Multiple, matches);

            counterLocked = _counter.BeginPackingLock();
            if (counterLocked) ordersLocked = _orders.BeginPackingLock();
            if (!counterLocked || !ordersLocked) return Remember(PopupStorePackingStatus.Busy, matches);
            if (!_orders.TryCommitPacking(_counter, matches.Candidates[0], out var receipt))
                return Remember(PopupStorePackingStatus.Stale, matches);

            // All local success data is visible before the first event or visual update.
            LastReceipt = receipt;
            _receipts.Enqueue(receipt);
            SuccessfulPackCount++;
            LastResult = new PopupStorePackingResult(PopupStorePackingStatus.Succeeded, matches, receipt);
            try { _counter.PublishPackingConsumed(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            try { _orders.PublishPackingCompleted(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            return LastResult;
        }
        finally
        {
            // Keep the transaction guard through callbacks caused by restoring a view during publication.
            try { if (ordersLocked) _orders.EndPackingLock(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            try { if (counterLocked) _counter.EndPackingLock(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            _busy = false;
            // OnEnable during publication deliberately could not rebind while busy.
            if (isActiveAndEnabled && !IsReady) Refresh();
            else UpdateButtonState();
        }
    }

    public bool TryTakeReceipt(out PopupStorePackingReceipt receipt)
    {
        receipt = _receipts.Count > 0 ? _receipts.Dequeue() : null;
        return receipt != null;
    }

    private PopupStorePackingResult Remember(PopupStorePackingStatus status, PopupStoreExactMatchResult matches = null)
        => LastResult = new PopupStorePackingResult(status, matches);

    private bool CanPack()
        => isActiveAndEnabled && IsReady && _counter != null && _counter.isActiveAndEnabled && _counter.IsReady
            && _orders != null && _orders.isActiveAndEnabled && _orders.IsReady;

    private bool ValidateReferences(out string error)
    {
        error = null;
        var popup = GetComponentInParent<UIPopupStore>(true);
        if (popup == null || _counter == null || _orders == null || !OwnsPackButton()
            || _counter.GetComponentInParent<UIPopupStore>(true) != popup
            || _orders.GetComponentInParent<UIPopupStore>(true) != popup)
            error = "The counter, order display and pack button must belong to this popup.";
        return error == null;
    }

    private bool OwnsPackButton()
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        return popup != null && _packButton != null && _packButton.GetComponentInParent<UIPopupStore>(true) == popup;
    }

    private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason) => UpdateButtonState();

    private void UpdateButtonState()
    {
        if (_boundPackButton != null) _boundPackButton.interactable = !_busy && CanPack();
    }

    private void OnPackClicked()
    {
#if UNITY_EDITOR
        int before = _counter != null ? _counter.Count : 0;
#endif
        var result = TryPack();
#if UNITY_EDITOR
        var completed = new List<string>();
        if (_orders != null)
            for (int i = 0; i < _orders.DisplayedCount; i++)
                if (_orders.IsOrderPacked(i)) completed.Add(_orders.GetDisplayedOrderId(i));
        Debug.Log("POPUP_STORE_PACK status=" + result.Status + " success=" + result.Success
            + " materialsBefore=" + before + " materialsAfter=" + (_counter != null ? _counter.Count : 0)
            + " completedOrderIds=[" + string.Join(",", completed) + "]"
            + " receiptId=" + (result.Receipt != null ? result.Receipt.ReceiptId : "none"), this);
#endif
    }

    private void Unbind()
    {
        if (_boundCounter != null) _boundCounter.Changed -= OnCounterChanged;
        if (_boundOrders != null) _boundOrders.Refreshed -= UpdateButtonState;
        if (_boundPackButton != null) _boundPackButton.onClick.RemoveListener(OnPackClicked);
        _boundCounter = null;
        _boundOrders = null;
        _boundPackButton = null;
    }

    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        if (_boundPackButton != null) _boundPackButton.interactable = false;
        Unbind();
        IsReady = false;
    }
    private void OnDestroy() => Unbind();
}
