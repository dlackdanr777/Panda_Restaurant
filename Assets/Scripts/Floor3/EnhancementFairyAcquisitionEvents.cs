using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Only committed first acquisitions enter this queue. Loading ownership never does.</summary>
public sealed class EnhancementFairyArrivalQueue
{
    private readonly HashSet<string> _seenItems = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingItems = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> _pendingOrder = new List<string>();
    // Session-only presentation claims survive a paused/recreated scene. They never
    // persist ownership or consume the pending visual arrival.
    private readonly HashSet<string> _popPresented = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _voicePresented = new HashSet<string>(StringComparer.Ordinal);
    public int PendingCount => _pendingItems.Count;
    public IEnumerable<string> PendingItems => _pendingOrder;
    public string FirstPending => _pendingOrder.Count == 0 ? null : _pendingOrder[0];

    public bool Confirm(string transactionId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(itemId)
            || !_seenItems.Add(itemId)) return false;
        _pendingItems.Add(itemId);
        _pendingOrder.Add(itemId);
        return true;
    }

    public bool IsPending(string itemId) => _pendingItems.Contains(itemId);
    public bool TryPresentPop(string itemId) => IsPending(itemId) && _popPresented.Add(itemId);
    public bool TryPresentVoice(string itemId) => IsPending(itemId) && _voicePresented.Add(itemId);
    public void PauseSoundPresentation(string itemId)
    {
        if (_popPresented.Contains(itemId)) _voicePresented.Add(itemId);
    }
    public bool Consume(string itemId)
    {
        if (!_pendingItems.Remove(itemId)) return false;
        _pendingOrder.Remove(itemId);
        return true;
    }
    public void Clear()
    {
        _seenItems.Clear(); _pendingItems.Clear(); _pendingOrder.Clear();
        _popPresented.Clear(); _voicePresented.Clear();
    }
}

public static class EnhancementFairyAcquisitionEvents
{
    public static readonly EnhancementFairyArrivalQueue Queue = new EnhancementFairyArrivalQueue();
    public static event Action Changed;

    /// <summary>Account switch/logout: discard previous account arrivals, retaining live scene listeners.</summary>
    public static void ResetSession(string accountId)
    {
        Queue.Clear();
        Changed?.Invoke();
    }

    /// <summary>Call after persisted transaction success, once for each IsNew enhancement item.</summary>
    public static bool PublishConfirmed(string transactionId, string itemId)
    {
        if (!Queue.Confirm(transactionId, itemId)) return false;
        Changed?.Invoke();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetSession()
    {
        Queue.Clear();
        Changed = null;
    }
}

public static class EnhancementFairyCatalog
{
    public static bool IsEligible(GachaData data)
    {
        // Explicit data type and upgrade enum policy; recipe IDs/names/sprite paths are irrelevant.
        var item = data as GachaItemData;
        return item != null && !string.IsNullOrEmpty(item.Id)
            && item.UpgradeType >= UpgradeType.UPGRADE01 && item.UpgradeType < UpgradeType.Length;
    }
}
