using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Offline only: no file, PlayerPrefs, singleton, SDK or account side effects.</summary>
public sealed class GachaEconomyMemoryStore : IGachaEconomyStore
{
    private GachaEconomySnapshot _current;
    private GachaEconomyTransaction _pending;
    private Action<GachaStoreResult, string> _callback;
    private readonly HashSet<string> _committed = new HashSet<string>(StringComparer.Ordinal);
    public bool FailNextSave { get; set; }
    public bool DeferNextSave { get; set; }
    public int SaveAttempts { get; private set; }
    public int CommitCount { get; private set; }
    public string SavedJson { get; private set; }
    public GachaEconomyMemoryStore(GachaEconomySnapshot initial) { _current = initial; SavedJson = Serialize(initial); }
    public GachaEconomySnapshot Capture() => _current;
    public bool CanStart(out string error) { error = _pending == null ? null : "이전 모의 저장 확인을 기다려 주세요."; return error == null; }
    public void Save(GachaEconomyTransaction transaction, Action<GachaStoreResult, string> completed)
    {
        SaveAttempts++;
        if (_committed.Contains(transaction.Id)) { completed(GachaStoreResult.Confirmed, null); return; }
        if (_pending != null || !transaction.Before.HasSameState(_current))
        { completed(GachaStoreResult.RejectedBeforeCommit, "저장 원본 상태가 변경되었습니다."); return; }
        _pending = transaction; _callback = completed;
        if (DeferNextSave) { DeferNextSave = false; return; }
        var result = FailNextSave ? GachaStoreResult.RejectedBeforeCommit : GachaStoreResult.Confirmed;
        FailNextSave = false; CompletePending(result);
    }
    public void CompletePending(GachaStoreResult result)
    {
        if (_pending == null) return;
        var tx = _pending; var callback = _callback;
        if (result == GachaStoreResult.Confirmed)
        {
            if (!_committed.Contains(tx.Id))
            {
                string json = Serialize(tx.After); // serialization must succeed before either state is committed.
                _current = Restore(json); SavedJson = json; _committed.Add(tx.Id); CommitCount++;
            }
            _pending = null; _callback = null;
        }
        else if (result == GachaStoreResult.RejectedBeforeCommit) { _pending = null; _callback = null; }
        callback(result, result == GachaStoreResult.Confirmed ? null : "오프라인 저장 응답: " + result);
    }
    public static string Serialize(GachaEconomySnapshot snapshot)
    {
        if (!StaffAccountSaveConverter.TrySerialize(snapshot.Account, out string account, out string error)) throw new InvalidOperationException(error);
        return new JObject { ["AccountId"] = snapshot.AccountId, ["Account"] = account, ["Diamonds"] = snapshot.Diamonds,
            ["ItemCounts"] = JObject.FromObject(snapshot.ItemCounts), ["ItemLevels"] = JObject.FromObject(snapshot.ItemLevels),
            ["RecipeLevels"] = JObject.FromObject(snapshot.RecipeLevels), ["TotalDrawCount"] = snapshot.TotalDrawCount }.ToString(Formatting.None);
    }
    public static GachaEconomySnapshot Restore(string json)
    {
        var root = JObject.Parse(json); var account = StaffAccountSaveConverter.Read((string)root["Account"]);
        if (account.Status != StaffAccountSaveReadStatus.Success) throw new InvalidOperationException(account.Error);
        return new GachaEconomySnapshot((string)root["AccountId"], account.Data, (int)root["Diamonds"],
            root["ItemCounts"].ToObject<Dictionary<string,int>>(), root["ItemLevels"].ToObject<Dictionary<string,int>>(),
            root["RecipeLevels"].ToObject<Dictionary<string,int>>(), (int)root["TotalDrawCount"]);
    }
}
