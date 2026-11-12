using System;
using System.Collections.Generic;

public readonly struct PopupStoreExactOrderCandidate
{
    public int SlotIndex { get; }
    public string CustomerId { get; }
    public string OrderId { get; }

    public PopupStoreExactOrderCandidate(int slotIndex, string customerId, string orderId = null)
    {
        SlotIndex = slotIndex;
        CustomerId = customerId;
        OrderId = orderId;
    }
}

public enum PopupStoreExactMatchKind { None, Single, Multiple }

/// <summary>A copied diagnostic result. Candidates remain separate even for the same customer ID.</summary>
public sealed class PopupStoreExactMatchResult
{
    public static readonly PopupStoreExactMatchResult Empty = new PopupStoreExactMatchResult(null);

    public IReadOnlyList<PopupStoreExactOrderCandidate> Candidates { get; }
    public PopupStoreExactMatchKind Kind => Candidates.Count == 0 ? PopupStoreExactMatchKind.None
        : Candidates.Count == 1 ? PopupStoreExactMatchKind.Single : PopupStoreExactMatchKind.Multiple;

    public PopupStoreExactMatchResult(IReadOnlyList<PopupStoreExactOrderCandidate> candidates)
    {
        var copy = new PopupStoreExactOrderCandidate[candidates != null ? candidates.Count : 0];
        for (int i = 0; i < copy.Length; i++) copy[i] = candidates[i];
        Candidates = Array.AsReadOnly(copy);
    }
}

/// <summary>Pure exact-order comparison. Does not choose a recipient, pack, fulfil or consume anything.</summary>
public static class PopupStoreExactOrderMatch
{
    public static bool IsExact(PopupStoreMaterialCatalog catalog,
        IReadOnlyList<PopupStoreMaterialEntry> order, IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null || counter == null || order.Count < 3 || order.Count > 5
            || counter.Count != order.Count || !TryReadCatalog(catalog, out var registered)) return false;
        return MatchesRegisteredOrder(registered, order, counter);
    }

    public static IReadOnlyList<int> FindExactSlots(PopupStoreMaterialCatalog catalog,
        IReadOnlyList<IReadOnlyList<PopupStoreMaterialEntry>> orders,
        IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (orders == null || orders.Count == 0 || orders.Count > 3 || counter == null
            || counter.Count < 3 || counter.Count > 5 || !TryReadCatalog(catalog, out var registered))
            return Array.Empty<int>();

        var matches = new List<int>();
        for (int i = 0; i < orders.Count; i++)
            if (MatchesRegisteredOrder(registered, orders[i], counter)) matches.Add(i);
        return matches.AsReadOnly();
    }

    private static bool MatchesRegisteredOrder(Dictionary<string, PopupStoreMaterialKind> registered,
        IReadOnlyList<PopupStoreMaterialEntry> order, IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null || order.Count < 3 || order.Count > 5 || counter.Count != order.Count) return false;
        int plushCount = order.Count - 2;
        for (int i = 0; i < order.Count; i++)
        {
            var kind = i < plushCount ? PopupStoreMaterialKind.Plush
                : i == plushCount ? PopupStoreMaterialKind.Box : PopupStoreMaterialKind.Ribbon;
            var expected = order[i];
            var actual = counter[i];
            if (!IsRegistered(registered, expected, kind) || !IsRegistered(registered, actual, kind)
                || !string.Equals(expected.Id, actual.Id, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool IsRegistered(Dictionary<string, PopupStoreMaterialKind> registered,
        PopupStoreMaterialEntry material, PopupStoreMaterialKind expectedKind)
        => material != null && material.Kind == expectedKind && !string.IsNullOrEmpty(material.Id)
            && registered.TryGetValue(material.Id, out var kind) && kind == expectedKind;

    private static bool TryReadCatalog(PopupStoreMaterialCatalog catalog,
        out Dictionary<string, PopupStoreMaterialKind> registered)
    {
        registered = null;
        if (catalog == null) return false;
        var entriesById = new Dictionary<string, PopupStoreMaterialKind>(StringComparer.Ordinal);
        foreach (var kind in new[] { PopupStoreMaterialKind.Plush, PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon })
        {
            // TryGetMaterials retains the existing full-catalog validation and registered ID policy.
            if (!catalog.TryGetMaterials(kind, out var entries, out _)) return false;
            foreach (var entry in entries) entriesById.Add(entry.Id, kind);
        }
        registered = entriesById;
        return true;
    }
}
