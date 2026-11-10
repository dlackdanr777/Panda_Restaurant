using System;
using System.Collections.Generic;
using System.Linq;

public static partial class UserInfo
{
    public static event Action OnChangePandaTokensHandler;

    public static bool CanChangePandaTokens(long delta, out string error)
        => Muks.BackEnd.BackendManager.Instance.StaffRuntime.CanChangePandaTokens(delta, out error);

    public static bool TryAddPandaTokens(long value, out string error)
    {
        if (!Muks.BackEnd.BackendManager.Instance.StaffRuntime.TryChangePandaTokens(value, out error)) return false;
        if (value != 0) NotifyPandaTokensChanged();
        return true;
    }

    internal static void NotifyPandaTokensChanged()
    {
        var observers = OnChangePandaTokensHandler;
        if (observers == null) return;
        foreach (Action observer in observers.GetInvocationList())
        {
            try { observer(); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("Panda token listener: " + ex.Message); }
        }
    }

    public static bool CanPurchaseStaffSkin(StaffSkinData skin, out string error)
    {
        error = null;
        if (skin == null || !ReferenceEquals(SkinDataManager.Instance.GetStaffSkinData(skin.Id), skin)
            || skin.BuyPrice < 0)
        { error = "구매할 스킨 자료가 올바르지 않습니다."; return false; }
        if (IsGiveStaffSkin(skin.Id))
        { error = "이미 보유한 스킨입니다."; return false; }
        if (!IsGiveStaff(CurrentStage, skin.EquipId))
        { error = "직원을 보유하고 있지 않습니다."; return false; }
        return CanChangePandaTokens(-((long)skin.BuyPrice), out error);
    }

    public static bool TryPurchaseStaffSkin(StaffSkinData skin, out string error)
    {
        if (!CanPurchaseStaffSkin(skin, out error)
            || !Muks.BackEnd.BackendManager.Instance.StaffRuntime.TryChangePandaTokens(-((long)skin.BuyPrice), out error))
            return false;
        _giveStaffSkinSet.Add(skin.Id);
        // Ownership and balance are committed before listeners can re-enter a purchase.
        NotifyPandaTokensChanged();
        try { OnGiveStaffSkinHandler?.Invoke(); }
        catch (Exception ex) { UnityEngine.Debug.LogWarning("Staff skin listener: " + ex.Message); }
        Muks.BackEnd.BackendManager.Instance.RequestGameDataAutosave();
        return true;
    }

    // Set only by the coordinator-owned economy store; ordinary give/consume/upgrade paths respect it.
    internal static object EconomyInventoryReservation { get; private set; }
    internal static bool TryReserveEconomyInventory(object owner)
    {
        if (owner == null || EconomyInventoryReservation != null) return false;
        EconomyInventoryReservation = owner; return true;
    }
    internal static void ReleaseEconomyInventory(object owner)
    { if (ReferenceEquals(EconomyInventoryReservation, owner)) EconomyInventoryReservation = null; }
    internal static GachaEconomySnapshot CaptureGachaEconomy(string accountId, StaffAccountSaveData account)
        => new GachaEconomySnapshot(accountId, account, _dia, _giveGachaItemCountDic, _giveGachaItemLevelDic, _giveRecipeLevelDic, _totalUseGachaMachineCount);
    internal static bool EconomyInventoryMatches(GachaEconomySnapshot source)
        => source != null && source.TotalDrawCount == _totalUseGachaMachineCount
            && GachaEconomySnapshot.Same(source.ItemCounts, _giveGachaItemCountDic)
            && GachaEconomySnapshot.Same(source.ItemLevels, _giveGachaItemLevelDic)
            && GachaEconomySnapshot.Same(source.RecipeLevels, _giveRecipeLevelDic);
    internal static void CommitGachaEconomyInventory(GachaEconomyTransaction transaction,
        Dictionary<string,int> counts, Dictionary<string,int> levels, Dictionary<string,int> recipes)
    {
        // All dictionaries were allocated and all proofs checked before the currency/account commit.
        _giveGachaItemCountDic = counts; _giveGachaItemLevelDic = levels; _giveRecipeLevelDic = recipes;
        _totalUseGachaMachineCount = transaction.After.TotalDrawCount;
    }
    internal static void NotifyGachaEconomy(GachaEconomyTransaction transaction, Func<bool> isCurrent)
    {
        void Notify(Action action) { try { if (isCurrent()) action?.Invoke(); } catch (Exception ex) { UnityEngine.Debug.LogWarning(ex.Message); } }
        if (transaction.Before.Account.PandaTokens != transaction.After.Account.PandaTokens) Notify(NotifyPandaTokensChanged);
        if (transaction.Results.Any(r => r.Data is GachaItemData)) Notify(() => OnGiveGachaItemHandler?.Invoke());
        if (transaction.Results.Any(r => r.Data is GachaRecipeData)) Notify(() => OnGiveRecipeHandler?.Invoke());
        if (transaction.Results.Any(r => r.Kind == GachaAcquisitionKind.Staff && r.IsNew)) Notify(OnGiveStaffEvent);
        if (transaction.IsDraw) Notify(() => OnUseGachaMachineHandler?.Invoke());
        foreach (var result in transaction.Results.Where(r => r.IsNew)) Notify(() => AddNotification(result.Id));
    }
}
