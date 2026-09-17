using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public enum GachaMachineKind { Item, Staff }
public enum GachaPaymentKind { DiamondsSingle, DiamondsEleven, TicketSingle }
public enum GachaAcquisitionKind { Item, Staff, Recipe }

/// <summary>Account-scoped monotonic history survives consumed inventory and is separate from legacy SkinToken.</summary>
public sealed class GachaEconomySaveData
{
    public int ItemTickets { get; }
    public int StaffTickets { get; }
    public int ItemCounter { get; }
    public int StaffCounter { get; }
    public IReadOnlyList<string> Acquired { get; }
    public string LastTransactionId { get; }
    public string LastTransactionJson { get; }
    public GachaExchangeCatalogSaveData Exchange { get; }
    public static GachaEconomySaveData Empty => new GachaEconomySaveData();
    public GachaEconomySaveData(int itemTickets = 0, int staffTickets = 0, int itemCounter = 0,
        int staffCounter = 0, IEnumerable<string> acquired = null, string lastTransactionId = "", string lastTransactionJson = "",
        GachaExchangeCatalogSaveData exchange = null)
    {
        ItemTickets = itemTickets; StaffTickets = staffTickets; ItemCounter = itemCounter; StaffCounter = staffCounter;
        Acquired = Array.AsReadOnly((acquired ?? Array.Empty<string>()).ToArray());
        LastTransactionId = lastTransactionId ?? ""; LastTransactionJson = lastTransactionJson ?? "";
        Exchange = exchange ?? GachaExchangeCatalogSaveData.Empty;
    }
    public int Counter(GachaMachineKind machine) => machine == GachaMachineKind.Staff ? StaffCounter : ItemCounter;
    public int Tickets(GachaMachineKind machine) => machine == GachaMachineKind.Staff ? StaffTickets : ItemTickets;
    public static string Key(GachaAcquisitionKind kind, string id) => kind.ToString() + ":" + id;
    internal bool Validate(out string error)
    {
        error = null;
        if (ItemTickets < 0 || StaffTickets < 0 || ItemCounter < 0 || ItemCounter >= 100 || StaffCounter < 0 || StaffCounter >= 100)
            error = "뽑기권 또는 머신별 보장 횟수가 잘못되었습니다.";
        else if (Acquired.Any(string.IsNullOrWhiteSpace) || Acquired.Distinct(StringComparer.Ordinal).Count() != Acquired.Count)
            error = "최초 획득 이력이 손상되었습니다.";
        return error == null && Exchange.Validate(out error);
    }
    internal JObject ToJson() => new JObject
    {
        ["ItemTickets"] = ItemTickets, ["StaffTickets"] = StaffTickets,
        ["ItemCounter"] = ItemCounter, ["StaffCounter"] = StaffCounter,
        ["Acquired"] = new JArray(Acquired), ["LastTransactionId"] = LastTransactionId, ["LastTransactionJson"] = LastTransactionJson,
        ["Exchange"] = Exchange.ToJson()
    };
    internal static bool TryRead(JToken token, out GachaEconomySaveData value, out string error)
    {
        value = null; error = null;
        if (!(token is JObject root) || (root.Count != 7 && root.Count != 8) || !(root["Acquired"] is JArray history)
            || history.Any(x => x.Type != JTokenType.String) || root["LastTransactionId"]?.Type != JTokenType.String
            || root["LastTransactionJson"]?.Type != JTokenType.String)
        { error = "경제 저장 형식이 잘못되었습니다."; return false; }
        var names = new[] { "ItemTickets", "StaffTickets", "ItemCounter", "StaffCounter" };
        var numbers = new int[4];
        for (int i = 0; i < names.Length; i++)
        {
            var entry = root[names[i]];
            if (entry?.Type != JTokenType.Integer || !int.TryParse(entry.ToString(), out numbers[i]))
            { error = "경제 저장 정수 필드가 잘못되었습니다."; return false; }
        }
        var exchange = GachaExchangeCatalogSaveData.Empty;
        if (root.Count == 8 && !GachaExchangeCatalogSaveData.TryRead(root["Exchange"], out exchange, out error)) return false;
        value = new GachaEconomySaveData(numbers[0], numbers[1], numbers[2], numbers[3], history.Values<string>(),
            (string)root["LastTransactionId"], (string)root["LastTransactionJson"], exchange);
        return value.Validate(out error);
    }
}
