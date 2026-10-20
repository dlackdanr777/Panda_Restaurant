using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// One versioned string column. Runtime identities and in-progress meals never enter it.
public sealed class WaitingPartySaveData
{
    internal static JObject ParseRawObject(string raw)
    {
        using (var text = new System.IO.StringReader(raw))
        using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
        {
            var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new JsonReaderException("Unexpected trailing Stage JSON");
            return value;
        }
    }
    public const string FieldName = "WaitingPartyQueue";
    public IReadOnlyList<IReadOnlyList<string>> Teams { get; }
    public bool IsProtected { get; }
    public bool IsLegacy { get; }
    public bool NeedsValidation { get; private set; }
    public bool CanSave => !IsProtected && !NeedsValidation;
    public WaitingPartySaveData RequireValidation() { NeedsValidation = Teams.Count > 0; return this; }
    internal void ConfirmValidation() { if (!IsProtected) NeedsValidation = false; }
    // Pure data check. A missing catalog/limit means not ready, never an unknown ID.
    // Runtime activation still confirms the snapshot only after complete restoration.
    public bool ValidateForRestore(int? teamLimit, Func<string, bool> knownId, out string reason)
    {
        reason = IsProtected ? Reason : teamLimit == null || knownId == null ? "WaitingValidationNotReady"
            : Teams.Count > teamLimit.Value ? "WaitingTeamLimitExceeded"
            : Teams.SelectMany(t => t).Any(id => !knownId(id)) ? "InvalidNormalCustomerId" : null;
        return reason == null;
    }
    public string Original { get; }
    public string Reason { get; }

    private WaitingPartySaveData(IEnumerable<IEnumerable<string>> teams, bool legacy, string original, string reason)
    {
        Teams = Array.AsReadOnly((teams ?? Array.Empty<IEnumerable<string>>()).Select(t => (IReadOnlyList<string>)Array.AsReadOnly(t.ToArray())).ToArray());
        IsLegacy = legacy; Original = original; Reason = reason; IsProtected = reason != null;
    }
    public WaitingPartySaveData Protect(string reason) => new WaitingPartySaveData(Teams, IsLegacy, Original, reason);
    public static WaitingPartySaveData Blocked(string original, string reason) => new WaitingPartySaveData(null, false, original, reason);
    public static WaitingPartySaveData FromTeams(IEnumerable<IEnumerable<string>> teams)
    {
        var list = teams.Select(t => t.ToArray()).ToArray();
        if (list.Any(t => t.Length < 1 || t.Length > 2 || t.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Invalid waiting party members");
        return new WaitingPartySaveData(list, false, null, null);
    }
    public static WaitingPartySaveData FromLegacy(IEnumerable<string> ids)
    {
        var list = (ids ?? Array.Empty<string>()).ToArray();
        if (list.Any(string.IsNullOrWhiteSpace)) return Blocked(JsonConvert.SerializeObject(list), "InvalidLegacyWaitingIds");
        return new WaitingPartySaveData(list.Select(id => new[] { id }), true, JsonConvert.SerializeObject(list), null);
    }
    public string Serialize()
    {
        if (IsProtected) throw new InvalidOperationException("Waiting save protected: " + Reason);
        return new JObject { ["version"] = 1, ["teams"] = new JArray(Teams.Select(t => new JArray(t))) }.ToString(Formatting.None);
    }
    public static WaitingPartySaveData ParseNew(string raw)
    {
        try
        {
            var value = ParseRawObject(raw);
            if (value["version"]?.Type != JTokenType.Integer) return Blocked(raw, "InvalidWaitingVersion");
            if ((long)value["version"] != 1) return Blocked(raw, "UnsupportedWaitingVersion");
            if (!(value["teams"] is JArray teams)) return Blocked(raw, "InvalidWaitingTeams");
            var result = new List<IEnumerable<string>>();
            foreach (var token in teams)
            {
                if (!(token is JArray members) || members.Count < 1 || members.Count > 2
                    || members.Any(id => id.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)id)))
                    return Blocked(raw, "InvalidWaitingMembers");
                result.Add(members.Select(id => (string)id).ToArray());
            }
            return new WaitingPartySaveData(result, false, raw, null);
        }
        catch (Exception) { return Blocked(raw, "MalformedWaitingQueue"); }
    }
    public static WaitingPartySaveData ParsePlain(JObject row)
    {
        if (row.TryGetValue(FieldName, out var value))
            return value.Type == JTokenType.String ? ParseNew((string)value) : Blocked(value.ToString(Formatting.None), "InvalidWaitingFieldType");
        if (!row.TryGetValue("WaitingCustomerIdList", out var old)) return FromLegacy(Array.Empty<string>());
        if (!(old is JArray ids) || ids.Any(id => id.Type != JTokenType.String)) return Blocked(old.ToString(Formatting.None), "InvalidLegacyWaitingIds");
        return FromLegacy(ids.Select(id => (string)id));
    }

    // Run before SDK FlattenRows, whose lossy fallback must not turn corruption into empty.
    public static LitJson.JsonData FlattenOtherStageFields(string raw)
    {
        var copy = ParseRawObject(raw);
        if (!(copy["rows"] is JArray rows) || rows.Count != 1 || !(rows[0] is JObject row))
            throw new InvalidOperationException("Invalid Stage envelope");
        row.Remove(FieldName); row.Remove("WaitingCustomerIdList");
        // Public SDK core used by FlattenRows, operating only on this parsing copy.
        return BackEnd.BackendReturnObject.Flatten(LitJson.JsonMapper.ToObject(copy.ToString(Formatting.None))["rows"]);
    }

    public static WaitingPartySaveData PreflightResponse(string raw)
    {
        try
        {
            var root = ParseRawObject(raw);
            if (!(root["rows"] is JArray rows) || rows.Count != 1 || !(rows[0] is JObject row)) return Blocked(raw, "InvalidStageEnvelope");
            if (row.TryGetValue(FieldName, out var field))
            {
                if (!(field is JObject wrapper) || wrapper.Count != 1 || wrapper["S"]?.Type != JTokenType.String)
                    return Blocked(raw, "InvalidWaitingAttribute");
                var parsed = ParseNew((string)wrapper["S"]);
                return parsed.IsProtected ? Blocked(raw, parsed.Reason) : parsed;
            }
            if (!row.TryGetValue("WaitingCustomerIdList", out var legacy)) return FromLegacy(Array.Empty<string>());
            if (!(legacy is JObject list) || list.Count != 1 || !(list["L"] is JArray ids)
                || ids.Any(id => !(id is JObject item) || item.Count != 1 || item["S"]?.Type != JTokenType.String))
                return Blocked(raw, "InvalidLegacyWaitingAttribute");
            return FromLegacy(ids.Select(id => (string)id["S"]));
        }
        catch (Exception) { return Blocked(raw, "MalformedStageEnvelope"); }
    }
}
