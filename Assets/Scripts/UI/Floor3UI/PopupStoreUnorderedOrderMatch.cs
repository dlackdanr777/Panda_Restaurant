using System;
using System.Collections.Generic;

/// <summary>
/// Packing and icon guidance compare counted material identities, never mutable list order.
/// Snapshot integrity continues to use PopupStoreExactOrderMatch's strict comparison.
/// </summary>
public static class PopupStoreUnorderedOrderMatch
{
    public static bool IsExact(PopupStoreMaterialCatalog catalog,
        IReadOnlyList<PopupStoreMaterialEntry> order, IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null || counter == null || order.Count < 3 || order.Count > 5
            || order.Count != counter.Count || !TryReadCatalog(catalog, out var registered)) return false;
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

    /// <summary>Each duplicate consumes one available unit, from the leftmost order icon.</summary>
    public static bool[] MatchMask(IReadOnlyList<PopupStoreMaterialEntry> order,
        IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null) return Array.Empty<bool>();
        var mask = new bool[order.Count];
        if (order.Count < 3 || order.Count > 5 || counter == null || counter.Count > 5
            || !IsCanonicalOrder(order, null)) return mask;
        var counts = new Dictionary<MaterialIdentity, int>();
        foreach (var material in counter)
        {
            // Malformed input cannot produce reassuring partial guidance.
            if (!IsRegistered(material, null)) return mask;
            var key = new MaterialIdentity(material);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }
        for (int i = 0; i < order.Count; i++)
        {
            var key = new MaterialIdentity(order[i]);
            if (!counts.TryGetValue(key, out int count) || count == 0) continue;
            mask[i] = true;
            counts[key] = count - 1;
        }
        return mask;
    }

    private static bool MatchesRegisteredOrder(Dictionary<string, PopupStoreMaterialKind> registered,
        IReadOnlyList<PopupStoreMaterialEntry> order, IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null || counter.Count != order.Count || !IsCanonicalOrder(order, registered)) return false;
        var counts = new Dictionary<MaterialIdentity, int>();
        foreach (var expected in order)
        {
            var key = new MaterialIdentity(expected);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }
        foreach (var actual in counter)
        {
            if (!IsRegistered(actual, registered)) return false;
            var key = new MaterialIdentity(actual);
            if (!counts.TryGetValue(key, out int count) || count == 0) return false;
            counts[key] = count - 1;
        }
        // Equal lengths and one debit per actual item imply that every expected count is zero.
        return true;
    }

    private static bool IsCanonicalOrder(IReadOnlyList<PopupStoreMaterialEntry> order,
        Dictionary<string, PopupStoreMaterialKind> registered)
    {
        if (order.Count < 3 || order.Count > 5) return false;
        int plushCount = order.Count - 2;
        for (int i = 0; i < order.Count; i++)
        {
            var kind = i < plushCount ? PopupStoreMaterialKind.Plush
                : i == plushCount ? PopupStoreMaterialKind.Box : PopupStoreMaterialKind.Ribbon;
            if (!IsRegistered(order[i], registered) || order[i].Kind != kind) return false;
        }
        return true;
    }

    private static bool IsRegistered(PopupStoreMaterialEntry material,
        Dictionary<string, PopupStoreMaterialKind> registered)
        => material != null && PopupStoreMaterialCatalog.IsKnownId(material.Id, material.Kind)
            && (registered == null || registered.TryGetValue(material.Id, out var kind) && kind == material.Kind);

    private static bool TryReadCatalog(PopupStoreMaterialCatalog catalog,
        out Dictionary<string, PopupStoreMaterialKind> registered)
    {
        registered = null;
        if (catalog == null) return false;
        var map = new Dictionary<string, PopupStoreMaterialKind>(StringComparer.Ordinal);
        foreach (var kind in new[] { PopupStoreMaterialKind.Plush, PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon })
        {
            if (!catalog.TryGetMaterials(kind, out var entries, out _)) return false;
            foreach (var entry in entries) map.Add(entry.Id, kind);
        }
        registered = map;
        return true;
    }

    private readonly struct MaterialIdentity : IEquatable<MaterialIdentity>
    {
        private readonly string _id;
        private readonly PopupStoreMaterialKind _kind;
        internal MaterialIdentity(PopupStoreMaterialEntry material) { _id = material.Id; _kind = material.Kind; }
        public bool Equals(MaterialIdentity other)
            => _kind == other._kind && string.Equals(_id, other._id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MaterialIdentity other && Equals(other);
        public override int GetHashCode() => ((int)_kind * 397) ^ StringComparer.Ordinal.GetHashCode(_id);
    }
}
