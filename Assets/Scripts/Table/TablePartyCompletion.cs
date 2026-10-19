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
    internal int CompletedPrice;
    internal bool MealStarted;
    internal bool FinalCompleted, VisitCounted, CustomerCounted, TipPaid, CoinFixed, CoinDropped;
    internal bool SatisfactionApplied, FeverApplied, ExitStarted, Returned, ExitFailureResolved;
    internal string ExitFailureReason;
    internal Vector3 DropPosition;
    internal PartyMemberCompletion(CustomerVisitMember member)
    { Member = member; Seat = member.SeatIndex; RemainingOrders = member.Customer.OrderCount; }
}

public partial class TableManager
{
    private void CompletePartyMeal(PartyFoodOrder food)
    {
        if (!food.IsCurrent || food.CompletionRecorded || food.Batch.Use.PolicyPending
            || !food.Identity.EatingCompleted || food.State != PartyFoodState.CurrentMealCompleted) return;
        food.CompletionRecorded = true;
        var use = food.Batch.Use;
        var result = use.GetCompletion(food.Member);
        result.MealsCompleted++;
        result.CompletedPrice += food.PlannedPrice;
        if (result.RemainingOrders == 0) CompletePartyMember(use, result);
        if (use.ServiceAborted) { TryFinishPartyInterruption(use); return; }
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
        if (use == null || use.IsSingle || !use.IsPartyCurrent(use.Table) || use.PolicyPending || use.ServiceAborted) return;
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
            DropPartyGarbage(use, result);
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

    private void DropPartyGarbage(TableUseIdentity use, PartyMemberCompletion result)
    {
        var table = use.Table;
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
    }

    private void StartPartyExit(TableUseIdentity use, PartyMemberCompletion result)
    {
        if (result.ExitStarted || result.Returned || result.ExitFailureResolved
            || !use.IsMemberCurrent(result.Member)) return;
        result.ExitStarted = true;
        var customer = result.Member.Customer;
        customer.StopWaiting(); customer.StopWaitingForFood(); customer.StopMove();
        Vector3 start = use.Table.GetSeatPosition(result.Seat);
        start.y = use.Table.TableFurniture.transform.position.y + AStar.Instance.NodeSize * .5f;
        if (use.HasArrived(result.Member)) customer.transform.position = start; // Existing chair-to-walkway alignment.
        customer.ChangeState(CustomerState.Idle); customer.SetLayer("Customer", 0);
        customer.HideFood(); customer.FixSpritePosition(true);
        try { if (use.ServiceAborted) customer.StartAnger(); else customer.StartHappy(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        int movementSequence = customer.MovementSequenceId + 1;
        customer.Move(GameManager.Instance.OutDoorPos, 0, () => {
            if (customer.MovementSequenceId != movementSequence || !IsPartyExitCurrent(use, result, customer)) return;
            if (!customer.ConfirmReachedMoveTarget())
            {
                ResolvePartyExitFailure(use, result, customer, movementSequence, "DestinationNotReached");
                return;
            }
            result.Returned = true;
            if (!use.Party.CompleteDeparture(result.Member, CustomerVisitDepartureResolution.ReachedExit))
            { result.Returned = false; return; }
            try { if (use.ServiceAborted) customer.StopAnger(); else customer.StopHappy(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
            ObjectPoolManager.Instance.DespawnNormalCustomer(customer);
            TryReleasePartyAfterExitResolution(use);
        }, () => ResolvePartyExitFailure(use, result, customer, movementSequence, "AStarPathUnavailable"));
    }

    private bool IsPartyExitCurrent(TableUseIdentity use, PartyMemberCompletion result, NormalCustomer customer)
    {
        return use != null && result != null && customer != null && !use.SettlementCompleted
            && result.ExitStarted && !result.Returned && !result.ExitFailureResolved
            && ReferenceEquals(use.GetCompletion(result.Member), result)
            && ReferenceEquals(result.Member.Customer, customer)
            && ReferenceEquals(customer.VisitMember, result.Member)
            && result.Member.DepartureResolution == CustomerVisitDepartureResolution.None
            && use.IsPartyCurrent(use.Table) && use.IsMemberCurrent(result.Member);
    }

    private void ResolvePartyExitFailure(TableUseIdentity use, PartyMemberCompletion result,
        NormalCustomer customer, int movementSequence, string reason)
    {
        if (customer == null || customer.MovementSequenceId != movementSequence || !customer.HasPathFailure
            || !IsPartyExitCurrent(use, result, customer)) return;

        // Detach before pool return so OnDisable cannot end the partner's live use.
        result.ExitFailureResolved = true;
        result.ExitFailureReason = reason;
        if (!use.Party.CompleteDeparture(result.Member, CustomerVisitDepartureResolution.TechnicalCleanup))
        {
            result.ExitFailureResolved = false;
            result.ExitFailureReason = null;
            return;
        }

        try { customer.StopMove(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        try { customer.StopHappy(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        try { customer.StopAnger(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        try { ObjectPoolManager.Instance.DespawnNormalCustomer(customer); }
        catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        use.Table.LastServiceDiagnostic = "PartyExitTechnicalCleanup; reason=" + reason
            + "; member=" + result.Member.Customer.CustomerData.Id;
        TryReleasePartyAfterExitResolution(use);
    }

    private void TryReleasePartyAfterExitResolution(TableUseIdentity use)
    {
        if (use == null || use.SettlementCompleted || !use.IsPartyCurrent(use.Table)) return;
        foreach (var result in use.Completions)
            if (!result.Returned && !result.ExitFailureResolved) return;

        use.SettlementCompleted = true;
        if (use.FurnitureRemoved && !HasUsableFurniture(use.Table))
            try { use.Table.TableState = ETableState.DontUse; }
            catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        else if (use.ServiceAborted && !use.MealTableUsed)
            try { use.Table.TableState = ETableState.Empty; }
            catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        use.Table.ReleaseCompletedParty(use);
        try { UpdateTable(); } catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
    }

    private void HoldUnsupportedPartyPolicy(TableUseIdentity use, string reason)
    {
        if (use == null || use.IsSingle || use.CompletionStarted || use.ServiceAborted || !use.IsPartyCurrent(use.Table)) return;
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
