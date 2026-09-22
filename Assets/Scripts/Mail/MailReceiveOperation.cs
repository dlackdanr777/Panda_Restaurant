using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// These records describe a main-thread memory operation, never a durable receipt or server transaction.
public sealed class MailRewardItem
{
    public string Id { get; }
    public int Count { get; }
    public MailRewardItem(string id, int count) { Id = id; Count = count; }
}

public static class MailRewardParser
{
    public static bool TryParseResponse(string json, out IReadOnlyList<MailRewardItem> rewards, out string error)
    {
        rewards = null;
        error = null;
        try
        {
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            return Parse(root["postItems"], out rewards, out error);
        }
        catch { error = "우편 수령 응답을 읽을 수 없습니다."; return false; }
    }

    public static bool TryParse(string json, out IReadOnlyList<MailRewardItem> rewards, out string error)
    {
        rewards = null;
        error = null;
        try
        {
            return Parse(JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }),
                out rewards, out error);
        }
        catch { error = "우편 보상 원본을 읽을 수 없습니다."; return false; }
    }

    private static bool Parse(JToken token, out IReadOnlyList<MailRewardItem> rewards, out string error)
    {
        rewards = null;
        error = "우편 보상은 유효한 아이템과 양수 정수 수량의 배열이어야 합니다.";
        if (!(token is JArray array)) return false;
        var result = new List<MailRewardItem>();
        foreach (JToken row in array)
        {
            if (!(row is JObject entry) || !(entry["item"] is JObject item)) return false;
            JToken name = item["itemName"] ?? item["itemID"];
            if (name == null || name.Type != JTokenType.String) return false;
            string id = (string)name;
            if (!string.IsNullOrEmpty(id) && id.StartsWith("{", StringComparison.Ordinal))
            {
                var embedded = JObject.Parse(id, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (embedded["itemID"]?.Type != JTokenType.String) return false;
                id = (string)embedded["itemID"];
            }
            JToken countToken = entry["itemCount"];
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || countToken == null
                || (countToken.Type != JTokenType.Integer && countToken.Type != JTokenType.String)
                || !int.TryParse(countToken.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count <= 0)
                return false;
            result.Add(new MailRewardItem(id, count));
        }
        rewards = result.AsReadOnly();
        error = null;
        return true;
    }
}

public sealed class MailReceiveTarget
{
    public PostType PostType { get; }
    public string InDate { get; }
    public IReadOnlyList<MailRewardItem> Rewards { get; }
    public GameDataRestoreQuery SourceQuery { get; }
    public MailReceiveTarget(PostType postType, string inDate, IReadOnlyList<MailRewardItem> rewards, GameDataRestoreQuery sourceQuery)
    {
        PostType = postType; InDate = inDate; SourceQuery = sourceQuery;
        Rewards = rewards == null ? null : Array.AsReadOnly(rewards.Select(item => item == null ? null : new MailRewardItem(item.Id, item.Count)).ToArray());
    }
}

public sealed class MailReceiveResponse
{
    public bool IsSuccess { get; }
    public string RawJson { get; }
    public string Error { get; }
    public MailReceiveResponse(bool success, string rawJson, string error = null)
    { IsSuccess = success; RawJson = rawJson; Error = error; }
}

public interface IMailReceiveProtection
{
    GameDataRestoreQuery Query { get; }
    bool IsCurrent { get; }
    bool TryMarkRequestStarted();
    void MarkIndeterminate(string reason);
    bool TryCompleteKnownOutcome();
    bool TryCancelBeforeRequest();
}

public interface IMailReceiveEnvironment
{
    bool TryAcquire(out IMailReceiveProtection protection, out string error);
    bool TryValidate(IReadOnlyList<MailRewardItem> rewards, out string error);
    MailRewardApplyResult Apply(MailRewardItem reward);
}

public interface IMailReceiveTransport
{
    // One public SDK call. Implementations must not perform automatic/popup consumption retries.
    void ReceiveOnce(MailReceiveTarget target, Action<MailReceiveResponse> onResponse);
}

public sealed class MailRewardApplyResult
{
    // Quantity acknowledged under the existing reward-kind policy, not newly owned staff or SDK calls.
    // Ownership rewards remain one Give call; stackable rewards can report a partially handled quantity.
    public int AppliedCount { get; }
    public bool OutcomeUnknown { get; }
    public string Error { get; }
    public bool IsComplete { get; }
    private MailRewardApplyResult(int applied, bool unknown, string error, bool complete)
    { AppliedCount = applied; OutcomeUnknown = unknown; Error = error; IsComplete = complete; }
    public static MailRewardApplyResult Applied(int count) => new MailRewardApplyResult(count, false, null, true);
    public static MailRewardApplyResult Rejected(string error) => new MailRewardApplyResult(0, false, error, false);
    public static MailRewardApplyResult Partial(int appliedCount, string error) => new MailRewardApplyResult(appliedCount, false, error, false);
    public static MailRewardApplyResult Unknown(int appliedCount, string error) => new MailRewardApplyResult(appliedCount, true, error, false);
}

public sealed class MailRewardProgress
{
    public MailRewardItem Reward { get; }
    public MailRewardApplyResult Result { get; }
    internal MailRewardProgress(MailRewardItem reward, MailRewardApplyResult result) { Reward = reward; Result = result; }
}

public enum MailReceiveStatus { Idle, Preflighting, Receiving, Applying, MemoryApplied, RejectedBeforeSend, Indeterminate, ConsumedPendingApplication }
public enum MailReceiveEntryStatus { Pending, Receiving, Applying, AppliedInMemory, Indeterminate, ConsumedPendingApplication, RejectedBeforeSend }

public sealed class MailReceiveEntry
{
    private readonly List<MailRewardProgress> _progress = new List<MailRewardProgress>();
    public MailReceiveTarget Target { get; }
    public MailReceiveEntryStatus Status { get; internal set; }
    public string RawResponse { get; internal set; }
    public IReadOnlyList<MailRewardProgress> Progress => _progress.AsReadOnly();
    internal bool ResponseClaimed;
    internal MailReceiveEntry(MailReceiveTarget target) { Target = target; }
    internal void AddProgress(MailRewardItem item, MailRewardApplyResult result) => _progress.Add(new MailRewardProgress(item, result));
}

/// <summary>
/// One persistent MailManager owns this operation across window lifetimes. Fixed IDs are consumed sequentially.
/// Unknown and partially applied operations deliberately have no reset/retry API. This ledger is memory-only.
/// </summary>
public sealed class MailReceiveOperation
{
    private readonly IMailReceiveEnvironment _environment;
    private readonly IMailReceiveTransport _transport;
    private readonly Action<MailReceiveEntry> _onMemoryApplied;
    private readonly Action<MailReceiveOperation> _onChanged;
    private readonly Dictionary<string, MailReceiveEntry> _sent = new Dictionary<string, MailReceiveEntry>(StringComparer.Ordinal);
    private MailReceiveEntry[] _entries = Array.Empty<MailReceiveEntry>();
    private IMailReceiveProtection _protection;
    private int _index;
    private bool _pumping;
    private bool _notifying;
    private bool _needsSend;
    private bool _started;
    public MailReceiveStatus Status { get; private set; }
    public string Error { get; private set; }
    public int PublicCallCount { get; private set; }
    public IReadOnlyList<MailReceiveEntry> Entries => Array.AsReadOnly(_entries);
    public bool IsBusy => Status == MailReceiveStatus.Preflighting
        || Status == MailReceiveStatus.Receiving || Status == MailReceiveStatus.Applying
        || Status == MailReceiveStatus.Indeterminate || Status == MailReceiveStatus.ConsumedPendingApplication;
    public bool CanStart => !IsBusy && !_pumping && !_notifying;

    public MailReceiveOperation(IMailReceiveEnvironment environment, IMailReceiveTransport transport,
        Action<MailReceiveEntry> onMemoryApplied = null, Action<MailReceiveOperation> onChanged = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _onMemoryApplied = onMemoryApplied; _onChanged = onChanged;
    }

    public MailReceiveEntry GetKnownEntry(PostType postType, string inDate, string accountInDate)
        => _sent.TryGetValue(Key(postType, inDate, accountInDate), out MailReceiveEntry result) ? result : null;

    public bool TryStart(IReadOnlyList<MailReceiveTarget> targets, out string error)
    {
        error = null;
        if (!CanStart) { error = "진행 중이거나 결과를 확인해야 하는 우편 수령이 있습니다."; return false; }
        Status = MailReceiveStatus.Preflighting;
        Error = null; PublicCallCount = 0; _index = 0; _started = false; _needsSend = false; _protection = null;
        _entries = Array.Empty<MailReceiveEntry>();
        try
        {
            if (targets == null || targets.Count == 0) return Reject("수령할 우편이 없습니다.", out error);
            var copied = new List<MailReceiveEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (MailReceiveTarget target in targets)
            {
                if (target == null || (target.PostType != PostType.Admin && target.PostType != PostType.Coupon)
                    || string.IsNullOrWhiteSpace(target.InDate) || target.SourceQuery == null || target.Rewards == null
                    || target.Rewards.Any(item => item == null || string.IsNullOrWhiteSpace(item.Id) || item.Count <= 0))
                    return Reject("우편의 원본 계정·ID·보상 자료가 유효하지 않습니다.", out error);
                string key = Key(target.PostType, target.InDate, target.SourceQuery.AccountInDate);
                if (!seen.Add(key) || _sent.ContainsKey(key)) return Reject("이미 처리했거나 결과 미확정인 우편입니다.", out error);
                copied.Add(new MailReceiveEntry(new MailReceiveTarget(target.PostType, target.InDate, target.Rewards, target.SourceQuery)));
            }
            _entries = copied.ToArray();
            if (!_environment.TryAcquire(out _protection, out string reason) || _protection == null)
                return Reject(reason ?? "현재 계정의 지급·저장 보호를 확보할 수 없습니다.", out error);
            if (_entries.Any(entry => !ReferenceEquals(entry.Target.SourceQuery, _protection.Query)) || !IsCurrent())
                return Reject("우편 목록과 현재 인증·복원 세대가 다릅니다. 목록을 다시 확인해 주세요.", out error);
            if (!_environment.TryValidate(_entries.SelectMany(entry => entry.Target.Rewards).ToArray(), out reason) || !IsCurrent())
                return Reject(reason ?? "현재 보상을 지급할 수 없습니다.", out error);
            Status = MailReceiveStatus.Receiving;
            _needsSend = true;
            Pump();
            if (Status == MailReceiveStatus.RejectedBeforeSend && PublicCallCount == 0)
            { error = Error; return false; }
            return true; // Accepted is not consumption, memory application, or storage success.
        }
        catch (Exception ex)
        {
            if (_started) { StopUnknown("우편 전송 이후 처리 예외: " + ex.GetType().Name); error = Error; return true; }
            return Reject("우편 사전 확인 실패: " + ex.GetType().Name, out error);
        }
    }

    private bool Reject(string reason, out string error)
    {
        Error = error = reason;
        Status = MailReceiveStatus.RejectedBeforeSend;
        if (_index < _entries.Length) _entries[_index].Status = MailReceiveEntryStatus.RejectedBeforeSend;
        _protection?.TryCancelBeforeRequest();
        Notify();
        return false;
    }

    private void Pump()
    {
        if (_pumping) return;
        _pumping = true;
        try
        {
            while (_needsSend && Status == MailReceiveStatus.Receiving)
            {
                _needsSend = false;
                MailReceiveEntry entry = _entries[_index];
                string reason = null;
                if (!IsCurrent() || !_environment.TryValidate(entry.Target.Rewards, out reason) || !IsCurrent()
                    || !_protection.TryMarkRequestStarted())
                {
                    Error = reason ?? "전송 전 계정·지급 상태가 변경되었습니다.";
                    entry.Status = MailReceiveEntryStatus.RejectedBeforeSend;
                    if (!_started) { Reject(Error, out _); return; }
                    if (!IsCurrent() || !_protection.TryCompleteKnownOutcome()) { StopUnknown(Error); return; }
                    Status = MailReceiveStatus.RejectedBeforeSend;
                    Notify();
                    return;
                }
                _started = true;
                entry.Status = MailReceiveEntryStatus.Receiving;
                _sent.Add(Key(entry.Target.PostType, entry.Target.InDate, entry.Target.SourceQuery.AccountInDate), entry);
                PublicCallCount++;
                try { _transport.ReceiveOnce(entry.Target, response => Receive(entry, response)); }
                catch (Exception ex)
                {
                    if (!entry.ResponseClaimed && ReferenceEquals(entry, _entries[_index]))
                        StopUnknown("전송 시작 후 예외: " + ex.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            if (!_started) Reject("전송 전 우편 확인 예외: " + ex.GetType().Name, out _);
            else StopUnknown("우편 처리 중 예외: " + ex.GetType().Name);
        }
        finally { _pumping = false; }
    }

    private void Receive(MailReceiveEntry entry, MailReceiveResponse response)
    {
        if (_index >= _entries.Length || !ReferenceEquals(entry, _entries[_index]) || entry.ResponseClaimed
            || (Status != MailReceiveStatus.Receiving && Status != MailReceiveStatus.Indeterminate)) return;
        if (response?.RawJson != null) entry.RawResponse = response.RawJson;
        if (response == null || !response.IsSuccess)
        { StopUnknown(response?.Error ?? "서버 수령 여부를 확정할 수 없습니다."); return; }

        entry.ResponseClaimed = true; // Before any external validation/apply/notification can reenter.
        Status = MailReceiveStatus.Applying;
        entry.Status = MailReceiveEntryStatus.Applying;
        try
        {
            if (!IsCurrent()) { StopConsumed("원래 계정·복원 세대가 아니므로 수령 응답을 적용하지 않습니다."); return; }
            if (!MailRewardParser.TryParseResponse(response.RawJson, out IReadOnlyList<MailRewardItem> rewards, out string reason)
                || !SameRewards(entry.Target.Rewards, rewards))
            { StopConsumed(reason ?? "수령 응답이 고정한 우편 보상과 다릅니다."); return; }
            if (!_environment.TryValidate(rewards, out reason) || !IsCurrent())
            { StopConsumed(reason ?? "수령 후 지급 준비 상태가 변경되었습니다."); return; }
            foreach (MailRewardItem reward in rewards)
            {
                if (!IsCurrent()) { StopConsumed("보상 처리 도중 계정·복원 상태가 변경되었습니다."); return; }
                MailRewardApplyResult applied;
                try { applied = _environment.Apply(reward); }
                catch (Exception ex) { applied = MailRewardApplyResult.Unknown(0, "항목 적용 결과 확인 필요: " + ex.GetType().Name); }
                if (applied == null || applied.AppliedCount < 0 || applied.AppliedCount > reward.Count)
                    applied = MailRewardApplyResult.Unknown(0, "항목 지급 결과가 올바르지 않습니다.");
                entry.AddProgress(reward, applied);
                if (!applied.IsComplete || applied.OutcomeUnknown || applied.AppliedCount != reward.Count)
                { StopConsumed(applied.Error ?? "일부 보상이 반영되지 않았습니다. 전체 보상을 다시 지급하지 않습니다."); return; }
            }
            if (!IsCurrent()) { StopConsumed("보상 반영 후 계정·복원 세대가 변경되었습니다."); return; }
            entry.Status = MailReceiveEntryStatus.AppliedInMemory;
            _onMemoryApplied?.Invoke(entry); // This callback must NOT claim persistence success.
            if (!IsCurrent()) { StopConsumed("완료 알림 중 계정·복원 세대가 변경되었습니다."); return; }
            _index++;
            if (_index == _entries.Length)
            {
                // Keep busy during release, which may synchronously drain queued save callbacks.
                if (!_protection.TryCompleteKnownOutcome()) { _index--; StopConsumed("우편 보호 종료 상태를 확인할 수 없습니다."); return; }
                Status = MailReceiveStatus.MemoryApplied;
                Error = null;
                Notify();
                return;
            }
            Status = MailReceiveStatus.Receiving;
            _needsSend = true;
            Pump();
        }
        catch (Exception ex) { StopConsumed("보상/완료 처리 예외: " + ex.GetType().Name); }
    }

    public bool MarkResponseMissing(string reason = "우편 응답 미도착: 서버 수령 여부 확인 필요")
    {
        if (Status != MailReceiveStatus.Receiving || _index >= _entries.Length || _entries[_index].Status != MailReceiveEntryStatus.Receiving) return false;
        StopUnknown(reason);
        return true;
    }

    private void StopUnknown(string reason)
    {
        Error = reason; Status = MailReceiveStatus.Indeterminate; _needsSend = false;
        if (_index < _entries.Length && _entries[_index].Status != MailReceiveEntryStatus.RejectedBeforeSend)
            _entries[_index].Status = MailReceiveEntryStatus.Indeterminate;
        _protection?.MarkIndeterminate(reason);
        Notify();
    }
    private void StopConsumed(string reason)
    {
        Error = reason; Status = MailReceiveStatus.ConsumedPendingApplication; _needsSend = false;
        if (_index < _entries.Length) _entries[_index].Status = MailReceiveEntryStatus.ConsumedPendingApplication;
        _protection?.MarkIndeterminate(reason);
        Notify();
    }
    private bool IsCurrent()
    {
        try { return _protection != null && _protection.IsCurrent; }
        catch { return false; }
    }
    private void Notify()
    {
        bool wasNotifying = _notifying;
        _notifying = true;
        try { _onChanged?.Invoke(this); }
        catch { /* UI observer failure cannot resend, reapply, or unlock a consumed operation. */ }
        finally { _notifying = wasNotifying; }
    }
    private static string Key(PostType type, string id, string account) => account + "\n" + type + "\n" + id;
    private static bool SameRewards(IReadOnlyList<MailRewardItem> expected, IReadOnlyList<MailRewardItem> actual)
    {
        if (actual == null || expected.Count != actual.Count) return false;
        var matched = new bool[actual.Count];
        foreach (MailRewardItem item in expected)
        {
            int found = -1;
            for (int i = 0; i < actual.Count; i++)
                if (!matched[i] && item.Id == actual[i].Id && item.Count == actual[i].Count) { found = i; break; }
            if (found < 0) return false;
            matched[found] = true;
        }
        return true;
    }
}
