using System;
using System.Collections.Generic;
using UnityEngine;

public partial class TableManager
{
    internal void TickPartyFoodClocks(TableUseIdentity use, double scaledNow)
    {
        if (use == null || use.IsSingle || !use.IsPartyCurrent(use.Table) || use.CompletionStarted
            || use.PolicyPending || use.ServiceAborted || use.Orders == null) return;
        foreach (var food in use.Orders.Orders)
            if (food.IsWaitingForDelivery && scaledNow - food.PreparedAt >= use.FoodWaitLimit)
            { AbortPartyService(use, "FoodWaitExpired"); return; }
    }

    private bool CheckPartyTendencies(TableUseIdentity use, IEnumerable<CustomerVisitMember> members)
    {
        if (use.ServiceAborted || use.CompletionStarted || use.PolicyPending) return false;
        foreach (var member in members)
            if (use.IsMemberCurrent(member) && !_satisfactionSystem.CheckCustomerTendency(member.Customer.NormalCustomerData.TendencyType))
            { AbortPartyService(use, "CustomerTendency"); return false; }
        return use.IsPartyCurrent(use.Table) && !use.ServiceAborted;
    }

    // Only approved service failures enter here. Data/movement/furniture failures
    // retain their technical boundary and never acquire this service penalty.
    internal bool AbortPartyService(TableUseIdentity use, string reason)
    {
        if (use == null || use.IsSingle || !use.IsPartyCurrent(use.Table) || use.CompletionStarted
            || use.PolicyPending || use.ServiceAborted) return false;
        use.ServiceAborted = true;
        use.ServiceAbortReason = reason;
        if (use.Completions.Count == 0)
            foreach (var member in use.Party.Members) use.Completions.Add(new PartyMemberCompletion(member));
        // State precedes lease release: staff recovery cannot resurrect food.
        if (use.Orders != null)
            foreach (var food in use.Orders.Orders)
                if (!food.Identity.ServingStarted) food.Cancel();
        use.OrderReservation?.Release();
        try { use.Orders?.Kitchen?.CancelUse(use); }
        catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        PartyEffect(use, ref use.ServicePenaltyApplied,
            () => UserInfo.AddSatisfaction(use.Stage, BasicRecipeRejectionSatisfaction * use.Party.Members.Count));
        if (!use.IsPartyCurrent(use.Table)) return true;
        try { use.Table.TableState = ETableState.PartyServiceAborted; }
        catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        use.Table.NotifyServiceChanged();
        TryFinishPartyInterruption(use);
        return true;
    }

    private void TryFinishPartyInterruption(TableUseIdentity use)
    {
        if (!use.ServiceAborted || use.CompletionStarted || use.PolicyPending || !use.IsPartyCurrent(use.Table)) return;
        if (!use.FurnitureRemoved && use.Orders != null)
            foreach (var food in use.Orders.Orders)
                if (food.Identity.ServingStarted && !food.CompletionRecorded) return;
        use.CompletionStarted = true;
        var table = use.Table;
        bool used = false;
        foreach (var result in use.Completions)
        {
            used |= result.MealStarted;
            if (!result.CoinFixed)
            {
                result.CoinAmount = (int)(result.CompletedPrice * GameManager.Instance.FeverRuntimeContext.FoodPriceMultiplier);
                result.CoinFixed = true;
                result.DropPosition = table.GetSeatPosition(result.Seat) + new Vector3(0, 1.2f, 0);
            }
        }
        use.MealTableUsed = used;
        if (use.FurnitureRemoved)
            foreach (var result in use.Completions)
                result.DropPosition = table.GetSeatPosition(result.Seat) + new Vector3(0, 1.2f, 0);
        if (used)
            try { table.TableFurniture.BeginPartyCleaning(use); }
            catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
        if (use.FurnitureRemoved && !HasUsableFurniture(table))
            try { table.TableState = ETableState.DontUse; }
            catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
        foreach (var result in use.Completions)
        {
            if (!use.IsCompletionContextCurrent) return;
            if (result.MealsCompleted > 0)
                PartyEffect(use, ref result.CoinDropped,
                    () => table.DropCoinAreas[result.Seat].DropCoin(result.DropPosition, result.CoinAmount, true));
            if (result.MealStarted) DropPartyGarbage(use, result);
        }
        foreach (var result in use.Completions)
            try { StartPartyExit(use, result); }
            catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
        table.NotifyServiceChanged();
        try { UpdateTable(); } catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
    }
}
