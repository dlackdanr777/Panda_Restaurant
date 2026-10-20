using System;
using System.Collections.Generic;
using UnityEngine;
using Muks.PathFinding.AStar;

public partial class TableManager
{
    private const int BasicRecipeRejectionSatisfaction = -5;

    private void PreparePartyOrders(TableData table, TableUseIdentity use)
    {
        if (!use.IsPartyCurrent(table) || use.IsSingle || !use.AllSeated || use.CompletionStarted || use.PolicyPending || use.ServiceAborted) return;
        if (use.Orders != null)
        {
            // A failed/empty attempt is terminal until explicit use cancellation. UI
            // refresh must not reroll it, including reentrant notifications below.
            if (use.Orders.State != PartyOrderBatchState.Accepted) return;
            foreach (var previous in use.Orders.Orders) if (!previous.CompletionRecorded) return;
        }
        if (use.Completions.Count == 0)
            foreach (var member in use.Party.Members) use.Completions.Add(new PartyMemberCompletion(member));
        var participants = new List<CustomerVisitMember>();
        foreach (var result in use.Completions)
            if (!result.FinalCompleted && result.RemainingOrders > 0) participants.Add(result.Member);
        if (!CheckPartyTendencies(use, participants)) return;
        var batch = new PartyOrderBatch(use);
        use.Orders = batch;
        var candidates = new List<(PartyMemberCompletion result, PartyFoodOrder order, int price, int tip, int satisfaction)>();
        bool allOwned = true, fullSink = false;
        try
        {
        foreach (var member in use.Party.Members)
        {
            if (!use.IsMemberCurrent(member)) throw new InvalidOperationException("The seated visit changed during menu preparation.");
            var result = use.GetCompletion(member);
            if (result.RemainingOrders <= 0 || result.FinalCompleted) continue;
            var customer = member.Customer;
            var food = customer.NormalCustomerData.GetRandomOrderFood();
            if (food == null) throw new InvalidOperationException("No initial menu was selected.");
            bool owned = UserInfo.IsGiveRecipe(food);
            allOwned &= owned;
            int level = owned ? UserInfo.GetRecipeLevel(food) : 1;
            float price = food.GetSellPrice(level);
            float priceValue = price * customer.CurrentFoodPriceMul * GameManager.Instance.GetFoodPriceMul(table.FloorType, food.FoodType);
            float tipValue = price * GameManager.Instance.TipMul * _satisfactionSystem.AddCustomerTipMul(customer.NormalCustomerData.TendencyType);
            float cookingTime = food.GetCookingTime(level) - GameManager.Instance.SubCookingTime;
            if (string.IsNullOrEmpty(food.Id) || float.IsNaN(priceValue) || float.IsInfinity(priceValue)
                || float.IsNaN(tipValue) || float.IsInfinity(tipValue)
                || float.IsNaN(cookingTime) || float.IsInfinity(cookingTime))
                throw new InvalidOperationException("Invalid menu calculation for seat " + member.SeatIndex);
            int plannedPrice = checked((int)priceValue);
            int tip = checked((int)Math.Floor(tipValue));
            var order = new PartyFoodOrder(batch, member, food, plannedPrice, tip);
            // Preserve the existing single-person cooking-time calculation and per-person rounding.
            order.Cooking = new CookingData(food, table,
                Mathf.Clamp(0.5f, cookingTime, 100000),
                price, () => CompletePartyCooking(order), order.Identity);
            candidates.Add((result, order, checked(result.TotalPrice + plannedPrice), checked(result.TotalTip + tip),
                checked(result.Satisfaction + (food.Id == customer.NormalCustomerData.RequiredDish ? 2 : 0))));
        }
        if (candidates.Count == 0) throw new InvalidOperationException("No participating member remains.");
        fullSink = allOwned && !UserInfo.GetBowlAddEnabled(use.Stage, table.FloorType);
        // Queries may invalidate the account, scene, table or a pooled visit. Validate
        // every member again before the callback-free commit of this round.
        if (!batch.IsCurrent || use.CompletionStarted || use.PolicyPending || use.ServiceAborted)
            throw new InvalidOperationException("The table use changed during menu preparation.");
        foreach (var member in use.Party.Members)
            if (!use.IsMemberCurrent(member)) throw new InvalidOperationException("The seated visit changed during menu preparation.");
        }
        catch (Exception error)
        {
            batch.State = PartyOrderBatchState.PreparationFailed;
            batch.LastDiagnostic = error.Message;
            PublishPartyState(batch, ETableState.PartyOrderPreparationFailed);
            return; // Explicit cancellation is safe; selected menus are never silently rerolled.
        }
        // No observer can see a partially committed member ledger or order batch.
        foreach (var candidate in candidates)
        {
            batch.Add(candidate.order);
            candidate.result.RemainingOrders--;
            candidate.result.TotalPrice = candidate.price;
            candidate.result.TotalTip = candidate.tip;
            candidate.result.Satisfaction = candidate.satisfaction;
        }
        foreach (var order in batch.Orders) order.StartDeliveryClock(Time.timeAsDouble);
        if (fullSink)
            foreach (var order in batch.Orders)
            {
                if (!batch.IsCurrent) return;
                try { UserInfo.AddSatisfaction(use.Stage, -4); }
                catch (Exception error) { batch.LastDiagnostic = error.ToString(); }
            }
        if (!batch.IsCurrent || use.PolicyPending || use.ServiceAborted || batch.State != PartyOrderBatchState.Preparing) return;
        batch.State = PartyOrderBatchState.Prepared;
        PublishPartyState(batch, ETableState.PartySeatedAwaitingOrders);
    }

    // Shared command for either bubble and the future waiter workflow. Supplying the captured
    // batch lets an asynchronous caller reject a request from a previous table use.
    public PartyOrderAcceptResult TryAcceptPartyOrders(TableData table, PartyOrderBatch expected = null)
    {
        var batch = table == null ? null : table.PartyOrders;
        if (batch == null || (expected != null && !ReferenceEquals(batch, expected)) || !batch.IsCurrent)
            return PartyOrderAcceptResult.Stale;
        if (batch.Use.PolicyPending) return PartyOrderAcceptResult.UnsupportedPolicy;
        if (batch.Use.ServiceAborted) return PartyOrderAcceptResult.ServiceAborted;
        if (batch.State == PartyOrderBatchState.PreparationFailed) return PartyOrderAcceptResult.InvalidMenu;
        if (batch.State != PartyOrderBatchState.Prepared) return PartyOrderAcceptResult.AlreadyHandled;
        if (!batch.ValidateOrders()) return PartyOrderAcceptResult.InvalidMenu;
        bool allOwned = true;
        foreach (var order in batch.Orders) allOwned &= UserInfo.IsGiveRecipe(order.Food.Id);
        if (!allOwned)
        {
            if (batch.Round == 1) { RejectPartyOrders(batch); return PartyOrderAcceptResult.RejectedRecipe; }
            AbortPartyService(batch.Use, "AdditionalRoundRecipeNotOwned");
            return PartyOrderAcceptResult.ServiceAborted;
        }
        var participants = new List<CustomerVisitMember>();
        foreach (var order in batch.Orders) participants.Add(order.Member);
        if (!CheckPartyTendencies(batch.Use, participants)) return PartyOrderAcceptResult.ServiceAborted;
        if (_kitchenSystem == null || !_kitchenSystem.TryGetOrderKitchen(table.FloorType, out var kitchen))
            return PartyOrderAcceptResult.KitchenUnavailable;
        batch.State = PartyOrderBatchState.Accepting;
        if (!kitchen.TryRegisterPartyBatch(batch))
        {
            if (batch.State == PartyOrderBatchState.Accepting) batch.State = PartyOrderBatchState.Prepared;
            return PartyOrderAcceptResult.RegistrationFailed;
        }
        return PublishPartyState(batch, ETableState.PartyWaitingForCooking)
            ? PartyOrderAcceptResult.Accepted : PartyOrderAcceptResult.AcceptedPresentationFailed;
    }

    private void CompletePartyCooking(PartyFoodOrder order)
    {
        if (!order.IsCurrent || order.Batch.Use.PolicyPending || order.Batch.Use.ServiceAborted || order.Batch.State != PartyOrderBatchState.Accepted
            || !order.Identity.CookingTerminal || order.Identity.CookingCancelled
            || order.Identity.CookingCompleted) return;
        order.Identity.CookingCompleted = true;
        order.State = PartyFoodState.ReadyForServing;
        RefreshPartyMealState(order.Batch);

    }

    private bool PublishPartyState(PartyOrderBatch batch, ETableState state)
    {
        if (!batch.IsCurrent || batch.Use.PolicyPending || batch.Use.ServiceAborted) return false;
        bool ok = true;
        try { batch.Use.Table.TableState = state; }
        catch (Exception error) { batch.LastDiagnostic = error.Message; ok = false; }
        if (!batch.IsCurrent || batch.Use.PolicyPending || batch.Use.ServiceAborted) return false;
        try { batch.Use.Table.RefreshPartyOrderButtons(); UpdateTable(); }
        catch (Exception error) { batch.LastDiagnostic = error.Message; ok = false; }
        return ok;
    }

    private void RejectPartyOrders(PartyOrderBatch batch)
    {
        if (!batch.IsCurrent || batch.State != PartyOrderBatchState.Prepared) return;
        batch.State = PartyOrderBatchState.Rejected;
        batch.Use.CompletionStarted = true;
        foreach (var order in batch.Orders) order.Cancel();
        // The account method clamps once after applying the two-person base penalty.
        // The existing single-person environment/tendency policy is not applied to this round.
        try { UserInfo.AddSatisfaction(UserInfo.CurrentStage, BasicRecipeRejectionSatisfaction * batch.Orders.Count); }
        catch (Exception error) { batch.LastDiagnostic = error.Message; }
        if (!batch.IsCurrent) return;
        PublishPartyState(batch, ETableState.PartyRejecting);
        foreach (var member in batch.Use.Party.Members)
        {
            if (!batch.Use.IsMemberCurrent(member)) continue;
            var customer = member.Customer;
            customer.StopWaiting(); customer.StopWaitingForFood(); customer.StopMove();
            Vector3 position = batch.Use.Table.GetSeatPosition(member.SeatIndex);
            position.y = batch.Use.Table.TableFurniture.transform.position.y + AStar.Instance.NodeSize * .5f;
            customer.transform.position = position;
            customer.ChangeState(CustomerState.Idle); customer.SetLayer("Customer", 0);
            customer.HideFood(); customer.StartAnger(); customer.FixSpritePosition(true);
            customer.Move(GameManager.Instance.OutDoorPos, 0, () =>
            {
                if (!batch.IsCurrent || batch.State != PartyOrderBatchState.Rejected
                    || !batch.Use.IsMemberCurrent(member) || !customer.ConfirmReachedMoveTarget()
                    || !batch.Exited.Add(member)) return;
                customer.StopMove(); customer.StopAnger();
                // Keep both visits alive until both have actually reached the exit. Despawning
                // the first member would end the party and release the second member's seat.
                if (batch.Exited.Count != batch.Orders.Count) return;
                batch.Use.Table.EndUse(batch.Use);
                try { UpdateTable(); } catch (Exception error) { batch.LastDiagnostic = error.Message; }
            });
        }
    }
}
