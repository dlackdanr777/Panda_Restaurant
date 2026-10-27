using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>Immutable local award, identified by the successful transaction rather than a reusable slot.</summary>
public sealed class PopupStoreScoreEntry
{
    public string ReceiptId { get; }
    public string OrderId { get; }
    public string CustomerId { get; }
    public int SlotIndex { get; }
    public int PlushCount { get; }
    public int Points { get; }

    internal PopupStoreScoreEntry(PopupStorePackingReceipt receipt, int plushCount, int points)
    {
        ReceiptId = receipt.ReceiptId;
        OrderId = receipt.OrderId;
        CustomerId = receipt.CustomerId;
        SlotIndex = receipt.SlotIndex;
        PlushCount = plushCount;
        Points = points;
    }
}

/// <summary>Session-local accounting only. Observes committed receipts without consuming delivery results.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreScore : MonoBehaviour
{
    [SerializeField] private PopupStorePacking _packing;
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private string _format = "점수 {0}";

    private readonly Dictionary<string, PopupStoreScoreEntry> _byReceipt
        = new Dictionary<string, PopupStoreScoreEntry>(StringComparer.Ordinal);
    private readonly List<PopupStoreScoreEntry> _entries = new List<PopupStoreScoreEntry>();
    private readonly int[] _successfulByLength = new int[4];
    private readonly HashSet<string> _reportedInvalidReceipts = new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<PopupStoreScoreEntry> _entriesView;
    private PopupStorePacking _owner;
    private PopupStorePacking _subscribed;
    private string _lastSourceError;
    private string _lastHudError;

    public int TotalScore { get; private set; }
    public event Action Changed;
    public int SuccessfulOrderCount => _entries.Count;
    public IReadOnlyCollection<string> ProcessedReceiptIds => _byReceipt.Keys;
    public IReadOnlyList<PopupStoreScoreEntry> Entries
        => _entriesView ?? (_entriesView = _entries.AsReadOnly());

    public int GetSuccessfulCount(int plushCount)
        => plushCount >= 1 && plushCount <= 3 ? _successfulByLength[plushCount] : 0;

    public bool TryGetEntry(string receiptId, out PopupStoreScoreEntry entry)
    {
        entry = null;
        return receiptId != null && _byReceipt.TryGetValue(receiptId, out entry);
    }

    /// <summary>Pure verification of the already observed ledger. Does not recover, consume or award receipts.</summary>
    public bool TryReadCompleteSnapshot(out int score, out int successfulOrders, out string error)
    {
        score = 0;
        successfulOrders = 0;
        error = null;
        var popup = GetComponentInParent<UIPopupStore>(true);
        if (popup == null || _owner == null || _owner != _packing
            || _owner.GetComponentInParent<UIPopupStore>(true) != popup)
        { error = "Score must be bound to its original local packing source."; return false; }
        var history = _owner.CommittedReceipts;
        if (history.Count != _entries.Count || history.Count != _byReceipt.Count)
        { error = "The score ledger does not contain every committed receipt."; return false; }
        long sum = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var receipt in history)
        {
            if (!_owner.IsCommittedReceipt(receipt)
                || !TryCalculate(receipt, out int plushCount, out int points, out _)
                || !ids.Add(receipt.ReceiptId) || !_byReceipt.TryGetValue(receipt.ReceiptId, out var entry)
                || entry.Points != points || entry.PlushCount != plushCount || entry.OrderId != receipt.OrderId
                || entry.CustomerId != receipt.CustomerId || entry.SlotIndex != receipt.SlotIndex)
            { error = "A committed receipt does not match its score record."; return false; }
            sum += points;
        }
        if (sum != TotalScore)
        { error = "The score total does not match its committed receipt records."; return false; }
        score = TotalScore;
        successfulOrders = history.Count;
        return true;
    }

    public bool Refresh()
    {
        if (!BindOwner()) { RefreshHud(); return false; }
        // Replaying authenticated history also recovers a temporarily disabled observer. Never drains the queue.
        var history = _owner.CommittedReceipts;
        for (int i = 0; i < history.Count; i++) TryRecordReceipt(history[i]);
        RefreshHud();
        return true;
    }

    public bool TryRecordReceipt(PopupStorePackingReceipt receipt)
    {
        if (!BindOwner() || !_owner.IsCommittedReceipt(receipt)) return false;
        if (_byReceipt.ContainsKey(receipt.ReceiptId)) return false;
        if (!TryCalculate(receipt, out int plushCount, out int points, out string error))
        {
            if (_reportedInvalidReceipts.Add(receipt.ReceiptId))
                Debug.LogError("PopupStoreScore: committed receipt was not scored; receipt="
                    + receipt.ReceiptId + "; error=" + error + ". Packing remains committed.", this);
            return false;
        }

        // No live counter, clock, current customer or HUD state participates in this immutable award.
        var entry = new PopupStoreScoreEntry(receipt, plushCount, points);
        _byReceipt.Add(entry.ReceiptId, entry);
        _entries.Add(entry);
        _successfulByLength[plushCount]++;
        TotalScore += points;
        var observers = Changed;
        if (observers != null)
            foreach (Action observer in observers.GetInvocationList())
                try { observer(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        RefreshHud();
#if UNITY_EDITOR
        Debug.Log("POPUP_STORE_SCORE receipt=" + entry.ReceiptId + " order=" + entry.OrderId
            + " customer=" + entry.CustomerId + " slot=" + entry.SlotIndex
            + " plush=" + entry.PlushCount + " points=" + entry.Points
            + " total=" + TotalScore + " successful=" + SuccessfulOrderCount, this);
#endif
        return true;
    }

    /// <summary>Calculation alone is not authorization: TryRecordReceipt also checks the local commit history.</summary>
    public static bool TryCalculate(PopupStorePackingReceipt receipt,
        out int plushCount, out int points, out string error)
    {
        plushCount = 0;
        points = 0;
        error = null;
        if (receipt == null || !receipt.Success || string.IsNullOrWhiteSpace(receipt.ReceiptId)
            || string.IsNullOrWhiteSpace(receipt.OrderId) || string.IsNullOrWhiteSpace(receipt.CustomerId)
            || receipt.SlotIndex < 0 || receipt.SlotIndex >= 3 || receipt.Materials == null
            || receipt.Materials.Count < 3 || receipt.Materials.Count > 5)
        { error = "Missing identity or invalid order length."; return false; }
        int count = receipt.Materials.Count - 2;
        for (int i = 0; i < receipt.Materials.Count; i++)
        {
            var material = receipt.Materials[i];
            var expected = i < count ? PopupStoreMaterialKind.Plush
                : i == count ? PopupStoreMaterialKind.Box : PopupStoreMaterialKind.Ribbon;
            if (material.Kind != expected || !PopupStoreMaterialCatalog.IsKnownId(material.Id, expected))
            { error = "Invalid material kind, ID or sequence at " + i + "."; return false; }
        }
        if (!string.Equals(receipt.BoxId, receipt.Materials[count].Id, StringComparison.Ordinal)
            || !string.Equals(receipt.RibbonId, receipt.Materials[count + 1].Id, StringComparison.Ordinal))
        { error = "Box or ribbon identity contradicts the receipt materials."; return false; }
        plushCount = count;
        points = 100 + (count - 1) * 40;
        return true;
    }

    public void RefreshHud()
    {
        try
        {
            var popup = GetComponentInParent<UIPopupStore>(true);
            if (popup == null || _scoreText == null || _scoreText.GetComponentInParent<UIPopupStore>(true) != popup)
                throw new InvalidOperationException("The score TMP must belong to this popup.");
            string text = string.Format(CultureInfo.InvariantCulture, _format ?? "점수 {0}", TotalScore);
            if (_scoreText.text != text) _scoreText.text = text;
            _lastHudError = null;
        }
        catch (Exception exception)
        {
            // Accounting has already completed. A missing/invalid view cannot cancel a successful sale.
            if (_lastHudError != exception.Message)
                Debug.LogError("PopupStoreScore: HUD update failed; score is preserved. " + exception.Message, this);
            _lastHudError = exception.Message;
        }
    }

    private bool BindOwner()
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        string error = null;
        if (popup == null || _packing == null || _packing.GetComponentInParent<UIPopupStore>(true) != popup)
            error = "The packing source must belong to this popup.";
        else if (_owner != null && _owner != _packing)
            error = "A session score cannot be rebound to another packing source.";
        if (error != null)
        {
            Unsubscribe();
            if (_lastSourceError != error) Debug.LogError("PopupStoreScore: " + error, this);
            _lastSourceError = error;
            return false;
        }
        _lastSourceError = null;
        if (_owner == null) _owner = _packing;
        if (isActiveAndEnabled && _subscribed != _owner)
        {
            Unsubscribe();
            _subscribed = _owner;
            _subscribed.SucceededCommitted += OnSucceeded;
        }
        return true;
    }

    private void OnSucceeded(PopupStorePackingReceipt receipt) => TryRecordReceipt(receipt);
    private void Unsubscribe()
    {
        if (_subscribed != null) _subscribed.SucceededCommitted -= OnSucceeded;
        _subscribed = null;
    }
    private void OnEnable() => Refresh();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
}
