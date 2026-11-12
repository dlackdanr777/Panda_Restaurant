using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum PopupStoreMaterialKind { Plush, Box, Ribbon }

[Serializable]
public sealed class PopupStoreMaterialEntry
{
    [SerializeField] private string _id;
    [SerializeField] private PopupStoreMaterialKind _kind;
    [SerializeField] private Sprite _sprite;
    [SerializeField, Min(0)] private int _displayOrder;

    public string Id => _id;
    public PopupStoreMaterialKind Kind => _kind;
    public Sprite Sprite => _sprite;
    public int DisplayOrder => _displayOrder;
}

/// <summary>Authored display data only; material lists do not grant or consume inventory.</summary>
public sealed class PopupStoreMaterialCatalog : ScriptableObject
{
    [SerializeField] private PopupStoreMaterialEntry[] _entries = Array.Empty<PopupStoreMaterialEntry>();

    public bool TryValidate(out string error)
    {
        error = null;
        if (_entries == null || _entries.Length == 0)
        { error = "The material catalog is empty."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sprites = new HashSet<Sprite>();
        var orders = new HashSet<(PopupStoreMaterialKind, int)>();
        foreach (var entry in _entries)
        {
            if (entry == null)
            { error = "The material catalog contains a null entry."; return false; }
            if (!IsKnownId(entry.Id, entry.Kind))
            { error = "Unknown material ID or mismatched category: " + entry.Id; return false; }
            if (!ids.Add(entry.Id))
            { error = "Duplicate material ID: " + entry.Id; return false; }
            if (entry.Sprite == null)
            { error = "Missing material sprite: " + entry.Id; return false; }
            if (!sprites.Add(entry.Sprite))
            { error = "Duplicate material sprite: " + entry.Id; return false; }
            if (entry.DisplayOrder < 0 || !orders.Add((entry.Kind, entry.DisplayOrder)))
            { error = "Invalid or duplicate display order in category: " + entry.Id; return false; }
        }
        // The supported IDs above form a fixed set of 32 plushes, 3 boxes and 3 ribbons.
        if (ids.Count != 38)
        { error = "The material catalog must contain all 38 supported IDs."; return false; }
        return true;
    }

    public bool TryGetMaterials(PopupStoreMaterialKind kind, out PopupStoreMaterialEntry[] materials, out string error)
    {
        materials = Array.Empty<PopupStoreMaterialEntry>();
        if (!Enum.IsDefined(typeof(PopupStoreMaterialKind), kind))
        { error = "Unknown material list category."; return false; }
        if (!TryValidate(out error)) return false;
        materials = _entries.Where(entry => entry.Kind == kind)
            .OrderBy(entry => entry.DisplayOrder).ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
        return true;
    }

    internal static bool IsKnownId(string id, PopupStoreMaterialKind kind)
    {
        if (string.IsNullOrEmpty(id)) return false;
        switch (kind)
        {
            case PopupStoreMaterialKind.Plush:
                return id.Length == 7 && id.StartsWith("STAFF", StringComparison.Ordinal)
                    && int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    && number >= 1 && number <= 32;
            case PopupStoreMaterialKind.Box:
                return id == "BOX_PINK" || id == "BOX_BLUE" || id == "BOX_MINT";
            case PopupStoreMaterialKind.Ribbon:
                return id == "RIBBON_PINK" || id == "RIBBON_YELLOW" || id == "RIBBON_MINT";
            default:
                return false;
        }
    }
}
