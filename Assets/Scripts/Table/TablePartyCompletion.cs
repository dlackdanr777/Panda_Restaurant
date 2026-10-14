using System;
using System.Collections.Generic;
using UnityEngine;
using Muks.PathFinding.AStar;
using Muks.Tween;

// One per member, owned by the table use. Monetary values are individually rounded
// by the existing menu/payment rules, never by a combined party total.
internal sealed class PartyMemberCompletion
{
    internal readonly CustomerVisitMember Member;
    internal readonly int Seat;
    internal int RemainingOrders, TotalPrice, TotalTip, Satisfaction, MealsCompleted;
    internal int CoinAmount, TipAmount, GarbageCount, GarbageDropped;
    internal bool FinalCompleted, VisitCounted, CustomerCounted, TipPaid, CoinFixed, CoinDropped;
    internal bool SatisfactionApplied, FeverApplied, ExitStarted, Returned;
    internal Vector3 DropPosition;
    internal PartyMemberCompletion(CustomerVisitMember member)
    { Member = member; Seat = member.SeatIndex; RemainingOrders = member.Customer.OrderCount; }
}

public partial class TableManager
{
    private void CompletePartyMeal(PartyFoodOrder food)
    {
        if (!food.IsCurrent || food.CompletionRecorded || food.Batch.Use.PolicyPending) return;
        food.CompletionRecorded = true;
        var use = food.Batch.Use;
        var result = use.GetCompletion(food.Member);
        result.MealsCompleted++;
        if (result.RemainingOrders == 0) CompletePartyMember(use, result);
        foreach (var order in food.Batch.Orders)
            if (!order.CompletionRecorded) return;
        if (!food.Batch.IsCurrent || use.CompletionStarted || use.PolicyPending) return;
        bool final = true;
        foreach (var member in use.Completions) final &= member.FinalCompleted;
        if (final) BeginPartyDeparture(use);
        else PreparePartyOrders(use.Table, use);
    }

    private void CompletePartyMember(TableUseIdentity use, PartyMemberCompletion result)
    {
        if (result.FinalCompleted || !use.IsMemberCurrent(result.Member)) return;
        result.FinalCompleted = true;
        var game = GameManager.Instance;
        result.CoinAmount = (int)(result.TotalPrice * game.FeverRuntimeContext.FoodPriceMultiplier);
        result.CoinFixed = true;
        result.TipAmount = StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout(result.TotalTip,
            game.StaffSkillEffectRegistry.GetHighestPercent(StaffSkillEffectType.RestaurantTipPayoutPercent));
        result.DropPosition = use.Table.GetSeatPosition(result.Seat) + new Vector3(0, 1.2f, 0);
        var customer = result.Member.Customer;
        customer.StopWaitingForFood();
        PartyEffect(use, ref result.VisitCounted, () => UserInfo.CustomerVisits(customer.CustomerData));
        PartyEffect(use, ref result.CustomerCounted, UserInfo.AddCustomerCount);
        // Preserve the existing .5 second personal payment delay, independently of
        // a partner's additional rounds or the customer's subsequent pool return.
        Tween.Wait(.5f, () => {
            if (use.IsCompletionContextCurrent)
                PartyEffect(use, ref result.TipPaid, () => UserInfo.AddTip(use.Stage, result.TipAmount));
        });
    }

    private void PartyEffect(TableUseIdentity use, ref bool claimed, Action effect)
    {
        if (claimed || !use.IsCompletionContextCurrent) return;
        // These account methods commit before notifying UI. A notification failure
        // must not pay again, or prevent the other member's independent result.
        claimed = true;
        try { effect(); }
        catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
    }

    internal void BeginPartyDeparture(TableUseIdentity use)
    {
        if (use == null || use.IsSingle || !use.IsPartyCurrent(use.Table) || use.PolicyPending) return;
        foreach (var result in use.Completions)
            if (!result.FinalCompleted || !result.CoinFixed) return;
        foreach (var order in use.Orders.Orders)
            if (!order.CompletionRecorded || order.Identity.ServingReservation != null) return;
        if (use.CompletionStarted) return;
        use.CompletionStarted = true;
        use.OrderReservation?.Release();
        var table = use.Table;
        // Commit both dirty state and the sink marker before any save/UI observer.
        try { table.TableFurniture.BeginPartyCleaning(use); }
        catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
        foreach (var result in use.Completions)
        {
            if (!use.IsCompletionContextCurrent) return;
            var customer = result.Member.Customer;
            PartyEffect(use, ref result.CoinDropped, () => table.DropCoinAreas[result.Seat].DropCoin(result.DropPosition, result.CoinAmount, true));
            result.GarbageCount = UnityEngine.Random.Range(1, 3);
            for (int i = 0; i < result.GarbageCount; i++)
            {
                bool dropped = false;
                Tween.Wait(.02f * (i + 1), () => {
                    if (!use.IsCompletionContextCurrent || dropped) return;
                    dropped = true; result.GarbageDropped++;
                    try { table.DropGarbageArea.DropGarbage(result.DropPosition); }
                    catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
                });
            }
            PartyEffect(use, ref result.SatisfactionApplied,
                () => UserInfo.AddSatisfaction(use.Stage, result.Satisfaction * customer.AddSatisfactionMul));
            PartyEffect(use, ref result.FeverApplied, () => _feverSystem.AddFeverGauge(customer.AddFeverGaugeMul));
        }
        // All results are captured before any member may return to the pool.
        foreach (var result in use.Completions)
            try { StartPartyExit(use, result); }
            catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
        table.NotifyServiceChanged();
        try { UpdateTable(); } catch (Exception error) { table.LastServiceDiagnostic = error.ToString(); }
    }

    private void StartPartyExit(TableUseIdentity use, PartyMemberCompletion result)
    {
        if (result.ExitStarted || !use.IsMemberCurrent(result.Member)) return;
        result.ExitStarted = true;
        var customer = result.Member.Customer;
        customer.StopWaiting(); customer.StopWaitingForFood(); customer.StopMove();
        Vector3 start = use.Table.GetSeatPosition(result.Seat);
        start.y = use.Table.TableFurniture.transform.position.y + AStar.Instance.NodeSize * .5f;
        customer.transform.position = start; // Existing chair-to-walkway stand-up alignment.
        customer.ChangeState(CustomerState.Idle); customer.SetLayer("Customer", 0);
        customer.HideFood(); customer.FixSpritePosition(true);
        try { customer.StartHappy(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        customer.Move(GameManager.Instance.OutDoorPos, 0, () => {
            if (result.Returned || !use.IsMemberCurrent(result.Member) || !customer.ConfirmReachedMoveTarget()) return;
            result.Returned = true;
            use.Party.CompleteDeparture(result.Member);
            try { customer.StopHappy(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
            ObjectPoolManager.Instance.DespawnNormalCustomer(customer);
            foreach (var other in use.Completions) if (!other.Returned) return;
            use.SettlementCompleted = true;
            use.Table.ReleaseCompletedParty(use);
            try { UpdateTable(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        });
    }

    private void HoldUnsupportedPartyPolicy(TableUseIdentity use, string reason)
    {
        if (use == null || use.IsSingle || use.CompletionStarted || !use.IsPartyCurrent(use.Table)) return;
        use.PolicyPending = true;
        use.Table.LastServiceDiagnostic = reason;
        use.OrderReservation?.Release();
        if (use.Orders != null)
            foreach (var order in use.Orders.Orders) order.Identity.ServingReservation?.Release();
        try { use.Table.TableState = ETableState.PartyPolicyPending; }
        catch (Exception error) { use.Table.LastServiceDiagnostic = reason + ": " + error.Message; }
        use.Table.NotifyServiceChanged();
    }
}
