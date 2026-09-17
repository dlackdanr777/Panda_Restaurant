using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Only committed first acquisitions enter this queue. Loading ownership never does.</summary>
public sealed class EnhancementFairyArrivalQueue
{
    private readonly HashSet<string> _seenItems = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingItems = new HashSet<string>(StringComparer.Ordinal);
    public int PendingCount => _pendingItems.Count;
    public IEnumerable<string> PendingItems => _pendingItems;

    public bool Confirm(string transactionId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(itemId)
            || !_seenItems.Add(itemId)) return false;
        _pendingItems.Add(itemId);
        return true;
    }

    public bool IsPending(string itemId) => _pendingItems.Contains(itemId);
    public bool Consume(string itemId) => _pendingItems.Remove(itemId);
    public void Clear() { _seenItems.Clear(); _pendingItems.Clear(); }
}

public static class EnhancementFairyAcquisitionEvents
{
    public static readonly EnhancementFairyArrivalQueue Queue = new EnhancementFairyArrivalQueue();
    public static event Action Changed;

    /// <summary>Account switch/logout: discard previous account arrivals, retaining live scene listeners.</summary>
    public static void ResetSession(string accountId)
    {
        Queue.Clear();
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
