using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed class GachaExchangeSlotSaveData
{
    public string ProductId { get; }
    public long Price { get; }
    public int Quantity { get; }
    public int PurchaseCount { get; }
    public GachaExchangeSlotSaveData(string productId, long price, int quantity, int purchaseCount = 0)
    { ProductId = productId; Price = price; Quantity = quantity; PurchaseCount = purchaseCount; }
    internal JObject ToJson() => new JObject { ["ProductId"] = ProductId, ["Price"] = Price, ["Quantity"] = Quantity, ["PurchaseCount"] = PurchaseCount };
}

public sealed class GachaExchangeDisplaySaveData
{
    public int Version { get; }
    public IReadOnlyList<GachaExchangeSlotSaveData> Slots { get; }
    public bool IsInitialized => Version > 0;
    public GachaExchangeDisplaySaveData(int version = 0, IEnumerable<GachaExchangeSlotSaveData> slots = null)
    { Version = version; Slots = Array.AsReadOnly((slots ?? Array.Empty<GachaExchangeSlotSaveData>()).ToArray()); }
    internal JObject ToJson() => new JObject { ["Version"] = Version, ["Slots"] = new JArray(Slots.Select(x => x.ToJson())) };
    internal GachaExchangeDisplaySaveData RecordPurchase(string productId, int count)
        => new GachaExchangeDisplaySaveData(Version, Slots.Select(x => x.ProductId == productId
            ? new GachaExchangeSlotSaveData(x.ProductId, x.Price, x.Quantity, checked(x.PurchaseCount + count)) : x));
}

/// <summary>Account-owned shop offers and quota commit in the same existing StaffAccount row.</summary>
public sealed class GachaExchangeCatalogSaveData
{
    public GachaExchangeDisplaySaveData Staff { get; }
    public GachaExchangeDisplaySaveData Items { get; }
    public string RefreshDateKey { get; }
    public int RefreshUsed { get; }
    public string LastRefreshRequestId { get; }
    public string LastRefreshReceiptJson { get; }
    public static GachaExchangeCatalogSaveData Empty => new GachaExchangeCatalogSaveData();
    public GachaExchangeCatalogSaveData(GachaExchangeDisplaySaveData staff = null, GachaExchangeDisplaySaveData items = null,
        string refreshDateKey = "", int refreshUsed = 0, string lastRefreshRequestId = "", string lastRefreshReceiptJson = "")
    {
        Staff = staff ?? new GachaExchangeDisplaySaveData(); Items = items ?? new GachaExchangeDisplaySaveData();
        RefreshDateKey = refreshDateKey ?? ""; RefreshUsed = refreshUsed;
        LastRefreshRequestId = lastRefreshRequestId ?? ""; LastRefreshReceiptJson = lastRefreshReceiptJson ?? "";
    }
    public GachaExchangeDisplaySaveData Display(GachaExchangeCategory category)
        => category == GachaExchangeCategory.Staff ? Staff : category == GachaExchangeCategory.Items ? Items : null;
    internal GachaExchangeCatalogSaveData RecordPurchase(GachaExchangeCategory category, string id, int quantity)
        => new GachaExchangeCatalogSaveData(category == GachaExchangeCategory.Staff ? Staff.RecordPurchase(id, quantity) : Staff,
            category == GachaExchangeCategory.Items ? Items.RecordPurchase(id, quantity) : Items,
            RefreshDateKey, RefreshUsed, LastRefreshRequestId, LastRefreshReceiptJson);
    internal bool Validate(out string error)
    {
        error = null;
        if (RefreshUsed < 0 || RefreshUsed > 3 || (RefreshDateKey.Length == 0 && RefreshUsed != 0) ||
            (RefreshDateKey.Length != 0 && !DateTime.TryParseExact(RefreshDateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) ||
            LastRefreshRequestId.Length > 128 || LastRefreshReceiptJson.Length > 65536)
        { error = "교환소 새로고침 저장 정보가 잘못되었습니다."; return false; }
        foreach (var display in new[] { Staff, Items })
        {
            if (display == null || display.Version < 0 || display.Slots.Count > 6 ||
                (display.Version == 0 && display.Slots.Count != 0) ||
                display.Slots.Any(x => x == null || string.IsNullOrWhiteSpace(x.ProductId) || x.Price <= 0 ||
                    x.Quantity <= 0 || x.Quantity > 1000 || x.PurchaseCount < 0) ||
                display.Slots.Select(x => x.ProductId).Distinct(StringComparer.Ordinal).Count() != display.Slots.Count)
            { error = "교환소 진열 저장 정보가 잘못되었습니다."; return false; }
        }
        return true;
    }
    internal JObject ToJson() => new JObject { ["Staff"] = Staff.ToJson(), ["Items"] = Items.ToJson(),
        ["RefreshDateKey"] = RefreshDateKey, ["RefreshUsed"] = RefreshUsed,
        ["LastRefreshRequestId"] = LastRefreshRequestId, ["LastRefreshReceiptJson"] = LastRefreshReceiptJson };
    internal static bool TryRead(JToken token, out GachaExchangeCatalogSaveData value, out string error)
    {
        value = null; error = "교환소 저장 형식이 잘못되었습니다.";
        if (!(token is JObject root) || root.Count != 6 || !ReadDisplay(root["Staff"], out var staff) ||
            !ReadDisplay(root["Items"], out var items) || root["RefreshDateKey"]?.Type != JTokenType.String ||
            root["LastRefreshRequestId"]?.Type != JTokenType.String || root["LastRefreshReceiptJson"]?.Type != JTokenType.String ||
            !Int(root["RefreshUsed"], out int used)) return false;
        value = new GachaExchangeCatalogSaveData(staff, items, (string)root["RefreshDateKey"], used,
            (string)root["LastRefreshRequestId"], (string)root["LastRefreshReceiptJson"]);
        return value.Validate(out error);
    }
    private static bool ReadDisplay(JToken token, out GachaExchangeDisplaySaveData value)
    {
        value = null;
        if (!(token is JObject root) || root.Count != 2 || !Int(root["Version"], out int version) ||
            !(root["Slots"] is JArray slots) || slots.Count > 6) return false;
        var list = new List<GachaExchangeSlotSaveData>();
        foreach (var tokenSlot in slots)
        {
            if (!(tokenSlot is JObject slot) || slot.Count != 4 || slot["ProductId"]?.Type != JTokenType.String ||
                slot["Price"]?.Type != JTokenType.Integer || !long.TryParse(slot["Price"].ToString(), out long price) ||
                !Int(slot["Quantity"], out int quantity) || !Int(slot["PurchaseCount"], out int purchased)) return false;
            list.Add(new GachaExchangeSlotSaveData((string)slot["ProductId"], price, quantity, purchased));
        }
        value = new GachaExchangeDisplaySaveData(version, list); return true;
    }
    private static bool Int(JToken token, out int value)
    { value = 0; return token?.Type == JTokenType.Integer && int.TryParse(token.ToString(), out value); }
}
