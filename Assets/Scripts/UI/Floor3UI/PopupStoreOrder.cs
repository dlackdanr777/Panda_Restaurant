using System;
using System.Collections.Generic;

/// <summary>Authored popup-store order data only. Resolving does not create or fulfil an order.</summary>
[Serializable]
public sealed class PopupStoreOrder
{
    [UnityEngine.SerializeField] private string _customerId;
    [UnityEngine.SerializeField] private string[] _plushIds = Array.Empty<string>();
    [UnityEngine.SerializeField] private string _boxId;
    [UnityEngine.SerializeField] private string _ribbonId;

    // Popup-instance memory only. Rebinding the same order never recreates a completed order.
    [NonSerialized] private string _runtimeId;
    [NonSerialized] private PopupStorePackingReceipt _packingReceipt;

    public string CustomerId => _customerId;
    public IReadOnlyList<string> PlushIds => _plushIds;
    public string BoxId => _boxId;
    public string RibbonId => _ribbonId;
    public string RuntimeId => _runtimeId ?? string.Empty;
    public bool IsPacked => _packingReceipt != null;
    public PopupStorePackingReceipt PackingReceipt => _packingReceipt;

    public PopupStoreOrder CreateRuntimeCopy()
    {
        var copy = new PopupStoreOrder
        {
            _customerId = _customerId,
            _plushIds = _plushIds == null ? null : (string[])_plushIds.Clone(),
            _boxId = _boxId,
            _ribbonId = _ribbonId
        };
        copy.EnsureRuntimeId();
        return copy;
    }

    internal string EnsureRuntimeId()
        => _runtimeId ?? (_runtimeId = Guid.NewGuid().ToString("N"));

    internal void CommitPacking(PopupStorePackingReceipt receipt) => _packingReceipt = receipt;

    public bool TryResolve(PopupStoreMaterialCatalog catalog, out PopupStoreMaterialEntry[] materials, out string error)
    {
        materials = Array.Empty<PopupStoreMaterialEntry>();
        error = null;
        if (string.IsNullOrWhiteSpace(_customerId))
        { error = "An order customer ID is required."; return false; }
        if (_plushIds == null || _plushIds.Length < 1 || _plushIds.Length > 3)
        { error = "An order requires one to three plush IDs."; return false; }
        if (catalog == null)
        { error = "An approved material catalog is required."; return false; }
        if (!catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out error)
            || !catalog.TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out error)
            || !catalog.TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out error)) return false;

        var resolved = new PopupStoreMaterialEntry[_plushIds.Length + 2];
        for (int i = 0; i < _plushIds.Length; i++)
        {
            if (!TryFind(plushes, _plushIds[i], out resolved[i]))
            { error = "Unknown plush ID or wrong material category: " + _plushIds[i]; return false; }
        }
        if (!TryFind(boxes, _boxId, out resolved[_plushIds.Length]))
        { error = "Unknown box ID or wrong material category: " + _boxId; return false; }
        if (!TryFind(ribbons, _ribbonId, out resolved[_plushIds.Length + 1]))
        { error = "Unknown ribbon ID or wrong material category: " + _ribbonId; return false; }

        materials = resolved;
        return true;
    }

    private static bool TryFind(PopupStoreMaterialEntry[] entries, string id, out PopupStoreMaterialEntry found)
    {
        foreach (var entry in entries)
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            { found = entry; return true; }
        found = null;
        return false;
    }
}
