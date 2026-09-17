using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;

public enum StaffAccountRuntimeMode { Unavailable, Legacy, Common }

/// <summary>Cost commit must be synchronous, non-notifying, and unchanged on false. Notify runs after level and cost commit.</summary>
public interface IStaffUpgradeWallet
{
    int Score { get; }
    long Gold { get; }
    int Diamonds { get; }
    bool TryApplyCost(UpgradeMoneyData cost, long expectedGold, int expectedDiamonds);
    void NotifyCost(UpgradeMoneyData cost);
}

/// <summary>
/// One Backend owner's current staff authority. Restored evidence is immutable; current data is replaced only by accepted
/// memory mutations or a different verified restore. Main-thread only; neither a purchase nor a persistence transaction.
/// </summary>
public sealed class StaffAccountRuntime
{
    private readonly Func<GameDataRestoreResult> _readRestore;
    private readonly Func<GameDataRestoreQuery> _readLegacyQuery;
    private readonly Func<bool> _canChange;
    private readonly Func<IReadOnlyList<StaffData>> _readCatalog;
    private readonly Func<bool> _isMigrationProtected;
    private GameDataRestoreEvidence _source;
    // A confirmed migration is not a replacement GameData query or a fabricated restore response.
    private StaffMigrationExecution _migrationSource;
    private GameDataSaveReceipt _migrationReceipt;
    private GameDataRestoreQuery _migrationQuery;
    private GameDataRestoreQuery _query;
    private StaffAccountSaveData _current;
    private StaffAccountRuntimeMode _mode;
    private bool _changing;
    public string LastNotificationError { get; private set; }

    public StaffAccountRuntime(Func<GameDataRestoreResult> readRestore, Func<GameDataRestoreQuery> readLegacyQuery,
        Func<bool> canChange, Func<IReadOnlyList<StaffData>> readCatalog, Func<bool> isMigrationProtected = null)
    {
        _readRestore = readRestore ?? throw new ArgumentNullException(nameof(readRestore));
        _readLegacyQuery = readLegacyQuery ?? throw new ArgumentNullException(nameof(readLegacyQuery));
        _canChange = canChange ?? throw new ArgumentNullException(nameof(canChange));
        _readCatalog = readCatalog ?? throw new ArgumentNullException(nameof(readCatalog));
        _isMigrationProtected = isMigrationProtected;
    }

    public StaffAccountRuntimeMode Mode { get { Refresh(); return _mode; } }
    public StaffAccountSaveData Snapshot { get { Refresh(); return _mode == StaffAccountRuntimeMode.Common ? _current : null; } }
    public bool CanMutate => !_changing && Authorized();
    public bool IsMigrationProtected
    {
        get
        {
            try { return _isMigrationProtected != null && _isMigrationProtected(); }
            catch { return true; } // A failed protection check must not permit a Stage reapplication.
        }
    }

    public void Refresh()
    {
        try
        {
            GameDataRestoreResult result = _readRestore();
            GameDataRestoreQuery query = _readLegacyQuery();
            // Read twice: query validation may invalidate the account while observing its identity.
            if (query == null)
            {
                ClearMigrationSource();
                _mode = StaffAccountRuntimeMode.Unavailable;
                return;
            }
            if (_migrationQuery != null && !ReferenceEquals(_migrationQuery, query)) ClearMigrationSource();
            if (!ReferenceEquals(result, _readRestore())) { _mode = StaffAccountRuntimeMode.Unavailable; return; }
            if (result.Status == GameDataRestoreStatus.MigrationRequired)
            {
                _query = query;
                // The old absence response stays unchanged. Re-reading it must not discard a confirmed install
                // or replace the subsequently grown current snapshot with the original migration candidate.
                _mode = _migrationSource != null && ReferenceEquals(_migrationQuery, query)
                    ? (_current != null ? StaffAccountRuntimeMode.Common : StaffAccountRuntimeMode.Unavailable)
                    : StaffAccountRuntimeMode.Legacy;
                return;
            }
            GameDataRestoreEvidence evidence = result.Evidence;
            if (result.Status != GameDataRestoreStatus.Ready || evidence == null || evidence.StaffAccount == null
                || !ReferenceEquals(evidence.Query, query)) { _mode = StaffAccountRuntimeMode.Unavailable; return; }
            ClearMigrationSource();
            if (!ReferenceEquals(_source, evidence))
            {
                _source = evidence;
                _current = evidence.StaffAccount;
            }
            _query = query;
            _mode = StaffAccountRuntimeMode.Common;
        }
        catch { _mode = StaffAccountRuntimeMode.Unavailable; }
    }

    private void ClearMigrationSource()
    {
        _migrationSource = null;
        _migrationReceipt = null;
        _migrationQuery = null;
    }

    /// <summary>
    /// Only the current coordinator-owned, successful migration execution can install its frozen candidate.
    /// No UserInfo restore, Stage rewrite, currency change or notification is performed here.
    /// </summary>
    internal bool TryInstallMigration(StaffMigrationExecution execution, GameDataSaveReceipt receipt, out string error)
    {
        error = null;
        Refresh();
        if (_changing || execution == null || receipt == null || execution.Preparation == null)
        { error = "이전 성공 근거가 없거나 직원 변경 처리 중입니다."; return false; }
        GameDataRestoreQuery query = execution.Preparation.Query;
        StaffAccountSaveData candidate = execution.Preparation.Candidate;
        if (_mode == StaffAccountRuntimeMode.Common && ReferenceEquals(_query, query)
            && ReferenceEquals(_migrationSource, execution) && ReferenceEquals(_migrationReceipt, receipt))
            return true; // Exactly the installed proof: preserve all later growth, do not reapply the candidate.
        if (_mode != StaffAccountRuntimeMode.Legacy || query == null || !ReferenceEquals(_query, query))
        { error = "현재 조회의 기존 직원 상태에만 이전 결과를 설치할 수 있습니다."; return false; }

        _changing = true;
        try
        {
            if (!execution.ValidateInstallation(this, receipt, out error)
                || !StaffAccountSaveConverter.Validate(candidate, out error)) return false;
            if (_mode != StaffAccountRuntimeMode.Legacy || !ReferenceEquals(_query, query))
            { error = "이전 성공 확인 중 계정·조회·직원 기준이 변경되었습니다."; return false; }
            // All external validation has finished. These assignments form a non-notifying memory commit.
            _source = null;
            _migrationSource = execution;
            _migrationReceipt = receipt;
            _migrationQuery = query;
            _current = candidate;
            _mode = StaffAccountRuntimeMode.Common;
            return true;
        }
        catch
        {
            error = "이전 성공 후 공용 직원 설치를 확인할 수 없습니다.";
            return false;
        }
        finally { _changing = false; }
    }

    private bool Authorized()
    {
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable) return false;
        GameDataRestoreQuery query = _query;
        try { if (!_canChange()) return false; }
        catch { return false; }
        Refresh();
        return _mode != StaffAccountRuntimeMode.Unavailable && ReferenceEquals(query, _query);
    }

    // The purchase owns the staff mutation fence; ordinary mutation authorization intentionally rejects it.
    // No callback is invoked between the non-notifying currency commit and immutable snapshot assignment.
    internal bool TryCommitPurchase(StaffGachaPurchaseExecution execution, GameDataSaveReceipt receipt,
        IStaffPurchaseWallet wallet, out string error)
    {
        error = null;
        Refresh();
        if (_changing || execution == null || wallet == null || _mode != StaffAccountRuntimeMode.Common
            || !ReferenceEquals(_query, execution.Query) || !ReferenceEquals(_current, execution.Source))
        { error = "구매 확정에 필요한 현재 공용 상태가 아닙니다."; return false; }
        _changing = true;
        try
        {
            if (!execution.ValidateConfirmation(this, receipt, out error)
                || !StaffAccountSaveConverter.Validate(execution.Plan.AccountResult.UpdatedAccount, out error)) return false;
            if (_mode != StaffAccountRuntimeMode.Common || !ReferenceEquals(_query, execution.Query)
                || !ReferenceEquals(_current, execution.Source))
            { error = "구매 성공 확인 중 공용 상태가 변경되었습니다."; return false; }
            if (!wallet.TryCommitReservedCost(execution, out error)) return false;
            _current = execution.Plan.AccountResult.UpdatedAccount;
            return true;
        }
        finally { _changing = false; }
    }

    public bool IsOwned(string id) => GetLevel(id).HasValue;
    internal bool TryCommitEconomy(BackendGachaEconomyStore store, GameDataSaveReceipt receipt, out string error)
    {
        error = null; Refresh();
        if (_changing || store == null || _mode != StaffAccountRuntimeMode.Common)
        { error = "경제 거래 확정 중이거나 계정이 준비되지 않았습니다."; return false; }
        _changing = true;
        try
        {
            if (!store.ValidateCommit(this, receipt, out error) || !StaffAccountSaveConverter.Validate(store.Candidate, out error)) return false;
            if (!store.CommitReservedCost(out error)) return false;
            _current = store.Candidate;
            return true;
        }
        finally { _changing = false; }
    }
    internal bool TryCommitQuestStaffGrant(QuestStaffGrantExecution operation, GameDataSaveReceipt receipt,
        IQuestStaffTutorialState state, out string error)
    {
        error = null; Refresh();
        if (_changing || operation == null || state == null || _mode != StaffAccountRuntimeMode.Common
            || !ReferenceEquals(_query, operation.Query) || !ReferenceEquals(_current, operation.Source))
        { error = "현재 무료 직원 획득의 공용 상태가 아닙니다."; return false; }
        _changing = true;
        try
        {
            if (!operation.ValidateCommit(receipt, out error) || !StaffAccountSaveConverter.Validate(operation.Result, out error)) return false;
            // The state contract validates first and commits the mask without callbacks; neither assignment throws.
            if (!state.TryCommit(operation.Before, operation.GrantedMask, out error)) return false;
            _current = operation.Result;
            return true;
        }
        finally { _changing = false; }
    }
    internal bool TryCommitFirstTutorial(FirstTutorialExecution operation, GameDataSaveReceipt receipt,
        IFirstTutorialState state, out string error)
    {
        error = null; Refresh();
        if (_changing || operation == null || state == null || _mode != StaffAccountRuntimeMode.Common
            || !ReferenceEquals(_query, operation.Query) || !ReferenceEquals(_current, operation.Source))
        { error = "현재 튜토리얼 공용 직원 상태가 아닙니다."; return false; }
        _changing = true;
        try
        {
            if (!operation.ValidateCoreCommit(receipt, out error) || !StaffAccountSaveConverter.Validate(operation.Result, out error)) return false;
            if (!state.TryCommit(operation, out error)) return false;
            _current = operation.Result; // Same level for owned STAFF11; no duplicate reward or token mutation.
            return true;
        }
        finally { _changing = false; }
    }
    public int? GetLevel(string id)
    {
        StaffAccountSaveData current = Snapshot;
        if (current == null || string.IsNullOrEmpty(id)) return null;
        foreach (StaffAccountStaffRecord staff in current.Staff) if (staff.Id == id) return staff.Level;
        return null;
    }
    public List<StaffData> GetOwnedStaff()
    {
        StaffAccountSaveData current = Snapshot;
        if (current == null || !TryCatalog(out IReadOnlyList<StaffData> catalog)) return new List<StaffData>();
        var byId = catalog.ToDictionary(staff => staff.Id, StringComparer.Ordinal);
        var result = new List<StaffData>();
        foreach (StaffAccountStaffRecord staff in current.Staff)
        {
            if (!byId.TryGetValue(staff.Id, out StaffData data)) return new List<StaffData>();
            result.Add(data);
        }
        return result;
    }
    public bool CanUpgrade(StaffData staff)
    {
        int? level = staff == null ? null : GetLevel(staff.Id);
        return CanMutate && level.HasValue && IsRegistered(staff) && staff.CanUpgradeFromSavedLevel(level.Value);
    }

    public bool TryGive(StaffData staff, Action notify, out bool added)
    {
        added = false;
        if (!CanMutate || Mode != StaffAccountRuntimeMode.Common) return false;
        _changing = true;
        try
        {
            GameDataRestoreQuery query = _query;
            StaffAccountSaveData before = _current;
            if (!IsRegistered(staff) || !StillSame(query, before, StaffAccountRuntimeMode.Common)) return false;
            if (before.Staff.Any(record => record.Id == staff.Id)) return true;
            var records = new List<StaffAccountStaffRecord>(before.Staff) { new StaffAccountStaffRecord(staff.Id, 1) };
            _current = new StaffAccountSaveData(before.Version, records, before.PandaTokens, before.GachaEconomy);
            added = true;
            Notify(notify);
            return true;
        }
        finally { _changing = false; }
    }

    /// <summary>Both common and managed-legacy growth use the original StaffData policy and the same no-event cost boundary.</summary>
    public bool TryUpgrade(StageInfo stage, StaffData staff, IStaffUpgradeWallet wallet, Action notify, out string error)
    {
        error = null;
        if (stage == null || wallet == null || !CanMutate) { error = "현재 계정의 직원 변경을 허용할 수 없습니다."; return false; }
        _changing = true;
        try
        {
            GameDataRestoreQuery query = _query;
            StaffAccountRuntimeMode mode = _mode;
            StaffAccountSaveData before = _current;
            StaffStageRuntimeSnapshot legacy = mode == StaffAccountRuntimeMode.Legacy ? stage.CaptureStaffRuntimeSnapshot() : null;
            int level;
            if (!IsRegistered(staff) || (mode == StaffAccountRuntimeMode.Common
                ? !TryLevel(before, staff.Id, out level) : !stage.TryGetLegacyStaffLevel(staff.Id, out level)))
            { error = "유효한 보유 직원이 아닙니다."; return false; }
            if (!staff.CanUpgradeFromSavedLevel(level)) { error = "현재 레벨에서는 성장할 수 없습니다."; return false; }
            UpgradeMoneyData cost = staff.GetUpgradeMoneyData(level);
            int minimumScore = staff.GetUpgradeMinScore(level);
            long gold = wallet.Gold;
            int diamonds = wallet.Diamonds;
            if (wallet.Score < minimumScore) { error = "성장에 필요한 평점이 부족합니다."; return false; }
            if (cost == null || cost.Price < 0 || (cost.MoneyType != MoneyType.Gold && cost.MoneyType != MoneyType.Dia))
            { error = "성장 비용 자료가 올바르지 않습니다."; return false; }
            if ((cost.MoneyType == MoneyType.Gold && gold < cost.Price) || (cost.MoneyType == MoneyType.Dia && diamonds < cost.Price))
            { error = "성장에 필요한 재화가 부족합니다."; return false; }
            StaffAccountSaveData next = mode == StaffAccountRuntimeMode.Common
                ? new StaffAccountSaveData(before.Version, before.Staff.Select(record => record.Id == staff.Id
                    ? new StaffAccountStaffRecord(record.Id, level + 1) : record).ToArray(), before.PandaTokens, before.GachaEconomy) : null;

            // External reads are complete. The two commits below have no events, SDK calls or asynchronous work.
            int currentScore = wallet.Score;
            long currentGold = wallet.Gold;
            int currentDiamonds = wallet.Diamonds;
            bool sameLegacy = legacy == null || (!legacy.IsApplying && legacy.HasSameState(stage.CaptureStaffRuntimeSnapshot()));
            if (currentScore < minimumScore || currentGold != gold || currentDiamonds != diamonds
                || !sameLegacy || !StillSame(query, before, mode))
            { error = "성장 확인 중 계정·보유·레벨·재화 상태가 변경되었습니다."; return false; }
            if (!wallet.TryApplyCost(cost, gold, diamonds)) { error = "성장 재화 차감을 확정하지 못했습니다."; return false; }
            if (mode == StaffAccountRuntimeMode.Common) _current = next;
            else stage.CommitLegacyStaffUpgrade(staff.Id);
            // Keep reentry locked while both currency and staff subscribers see the committed state.
            Notify(() => wallet.NotifyCost(cost));
            Notify(notify);
            return true;
        }
        finally { _changing = false; }
    }

    private bool StillSame(GameDataRestoreQuery query, StaffAccountSaveData before, StaffAccountRuntimeMode mode)
        => Authorized() && _mode == mode && ReferenceEquals(_query, query)
            && (mode != StaffAccountRuntimeMode.Common || ReferenceEquals(_current, before));

    private bool IsRegistered(StaffData staff)
    {
        if (staff == null || !TryCatalog(out IReadOnlyList<StaffData> catalog)) return false;
        StaffData registered = catalog.FirstOrDefault(item => item.Id == staff.Id);
        return ReferenceEquals(registered, staff);
    }
    private bool TryCatalog(out IReadOnlyList<StaffData> catalog)
    {
        catalog = null;
        try
        {
            IReadOnlyList<StaffData> read = _readCatalog();
            if (GameDataRestoreContext.TryBuildStaffMaximums(() => read, out _, out _) != GameDataRestoreStatus.Ready) return false;
            catalog = read;
            return true;
        }
        catch { return false; }
    }
    private static bool TryLevel(StaffAccountSaveData data, string id, out int level)
    {
        level = 0;
        foreach (StaffAccountStaffRecord record in data.Staff)
            if (record.Id == id) { level = record.Level; return true; }
        return false;
    }
    private void Notify(Action action)
    {
        try { action?.Invoke(); }
        catch (Exception ex) { LastNotificationError = ex.GetType().Name; } // A listener failure cannot uncommit an accepted memory mutation.
    }

    public bool TryAddSaveField(Param values, out string error)
    {
        error = null;
        if (values == null) { error = "저장 자료가 없습니다."; return false; }
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable) { error = "현재 공용/기존 직원 복원 근거가 없습니다."; return false; }
        try
        {
            JObject fields = JObject.Parse(values.GetJson());
            if (_mode == StaffAccountRuntimeMode.Legacy)
            {
                if (fields.Property(GameDataRestoreContext.StaffAccountFieldName) == null) return true;
                error = "이전 전 계정에 StaffAccount를 임의로 저장할 수 없습니다.";
                return false;
            }
            if (!StaffAccountSaveConverter.TrySerialize(_current, out string json, out error)) return false;
            JToken old = fields[GameDataRestoreContext.StaffAccountFieldName];
            if (old != null)
            {
                if (old.Type == JTokenType.String && string.Equals((string)old, json, StringComparison.Ordinal)) return true;
                error = "기존 Param의 StaffAccount가 현재 공용 상태와 다릅니다.";
                return false;
            }
            values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
            return true;
        }
        catch { error = "공용 직원 저장 자료를 생성할 수 없습니다."; return false; }
    }

    public bool ValidateSaveField(GameDataSavePayload payload, out string error)
    {
        error = null;
        Refresh();
        if (_mode == StaffAccountRuntimeMode.Unavailable || payload == null)
        { error = "현재 직원 복원 근거/저장 자료가 없습니다."; return false; }
        try
        {
            JObject fields = JObject.Parse(payload.CreateParamCopy().GetJson());
            JProperty field = fields.Property(GameDataRestoreContext.StaffAccountFieldName);
            if (field == null) return true; // A genuine partial update does not overwrite common data.
            if (_mode != StaffAccountRuntimeMode.Common || field.Value.Type != JTokenType.String
                || !StaffAccountSaveConverter.TrySerialize(_current, out string current, out error)
                || !string.Equals((string)field.Value, current, StringComparison.Ordinal))
            { error = "명시된 StaffAccount는 현재 검증된 공용 상태와 일치해야 합니다."; return false; }
            return true;
        }
        catch { error = "전송 전 StaffAccount 검증에 실패했습니다."; return false; }
    }
}
