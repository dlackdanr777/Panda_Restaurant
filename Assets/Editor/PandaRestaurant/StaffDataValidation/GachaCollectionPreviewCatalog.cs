#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Memory-owned copies for the Editor preview. Resource assets are read only;
/// no runtime manager, account, CSV cache or scene object is initialized.
/// </summary>
public sealed class GachaCollectionPreviewCatalog : IDisposable
{
    private readonly List<GachaData> _items = new List<GachaData>();
    public IReadOnlyList<GachaData> Items { get; }

    public GachaCollectionPreviewCatalog()
    {
        Items = _items.AsReadOnly();
        try
        {
            foreach (StaffData staff in Resources.LoadAll<StaffData>("StaffData").OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                GachaStaffData wrapper = GachaStaffData.Create(staff);
                wrapper.hideFlags = HideFlags.HideAndDontSave;
                _items.Add(wrapper);
            }

            var sprites = Resources.LoadAll<Sprite>("ItemData/GachaItemData/Sprites")
                .Where(sprite => sprite.name.StartsWith("GOTCHA", StringComparison.Ordinal))
                .GroupBy(sprite => sprite.name.Split('_')[0], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            TextAsset csv = Resources.Load<TextAsset>("ItemData/GachaItemData/GachaItemList");
            if (csv == null) throw new InvalidOperationException("The offline item catalog resource is missing.");
            foreach (string line in csv.text.Split('\n').Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] cells = line.TrimEnd('\r').Split(',');
                if (cells.Length < 12) throw new InvalidOperationException("The offline item catalog contains an incomplete row.");
                string id = cells[0].Trim();
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!sprites.TryGetValue(id, out Sprite sprite))
                    throw new InvalidOperationException("The offline item sprite is missing: " + id);
                // Recipe rows have an empty upgrade type and max level in the same production CSV.
                // Keep them in the item catalog with the production loader's None / level-one defaults.
                if (!Enum.TryParse(cells[8].Trim(), true, out UpgradeType upgradeType)) upgradeType = UpgradeType.None;
                var item = new GachaItemData(id, cells[1], cells[2], Integer(cells[4]), Integer(cells[5]),
                    Integer(cells[6]), upgradeType, Decimal(cells[9]), Decimal(cells[10]),
                    Math.Max(1, Integer(cells[11])), sprite);
                item.hideFlags = HideFlags.HideAndDontSave;
                _items.Add(item);
            }
            if (_items.GroupBy(item => item.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
                throw new InvalidOperationException("The offline catalog contains duplicate identifiers.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static int Integer(string value) => int.TryParse(value.Trim(), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out int number) ? number : 0;

    private static float Decimal(string value) => float.TryParse(value.Trim(), NumberStyles.Float,
        CultureInfo.InvariantCulture, out float number) ? number : 0f;

    public void Dispose()
    {
        foreach (GachaData item in _items) if (item != null) Object.DestroyImmediate(item);
        _items.Clear();
    }
}
#endif
