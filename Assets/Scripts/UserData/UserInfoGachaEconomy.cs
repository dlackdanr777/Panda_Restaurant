using System;
using System.Collections.Generic;
using System.Linq;

public static partial class UserInfo
{
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
        if (transaction.Results.Any(r => r.Data is GachaItemData)) Notify(() => OnGiveGachaItemHandler?.Invoke());
        if (transaction.Results.Any(r => r.Data is GachaRecipeData)) Notify(() => OnGiveRecipeHandler?.Invoke());
        if (transaction.Results.Any(r => r.Kind == GachaAcquisitionKind.Staff && r.IsNew)) Notify(OnGiveStaffEvent);
        if (transaction.IsDraw) Notify(() => OnUseGachaMachineHandler?.Invoke());
        foreach (var result in transaction.Results.Where(r => r.IsNew)) Notify(() => AddNotification(result.Id));
    }
}
