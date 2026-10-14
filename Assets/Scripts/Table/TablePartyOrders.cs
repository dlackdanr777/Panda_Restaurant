using System;
using UnityEngine;
using Muks.PathFinding.AStar;

public partial class TableManager
{
    private const int BasicRecipeRejectionSatisfaction = -5;

    private void PreparePartyOrders(TableData table, TableUseIdentity use)
    {
        if (!use.IsPartyCurrent(table) || use.IsSingle || !use.AllSeated || use.CompletionStarted || use.PolicyPending) return;
        if (use.Orders != null)
            foreach (var previous in use.Orders.Orders) if (!previous.CompletionRecorded) return;
        if (use.Completions.Count == 0)
            foreach (var member in use.Party.Members) use.Completions.Add(new PartyMemberCompletion(member));
        var batch = new PartyOrderBatch(use);
        use.Orders = batch;
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
            int level = UserInfo.IsGiveRecipe(food) ? UserInfo.GetRecipeLevel(food) : 1;
            float price = food.GetSellPrice(level);
            int plannedPrice = (int)(price * customer.CurrentFoodPriceMul * GameManager.Instance.GetFoodPriceMul(table.FloorType, food.FoodType));
            int tip = Mathf.FloorToInt(price * GameManager.Instance.TipMul * _satisfactionSystem.AddCustomerTipMul(customer.NormalCustomerData.TendencyType));
            var order = new PartyFoodOrder(batch, member, food, plannedPrice, tip);
            // Preserve the existing single-person cooking-time calculation and per-person rounding.
            order.Cooking = new CookingData(food, table,
                Mathf.Clamp(0.5f, food.GetCookingTime(level) - GameManager.Instance.SubCookingTime, 100000),
                price, () => CompletePartyCooking(order), order.Identity);
            batch.Add(order);
            result.RemainingOrders--;
            result.TotalPrice += plannedPrice; result.TotalTip += tip;
            if (food.Id == customer.NormalCustomerData.RequiredDish) result.Satisfaction += 2;
        }
        }
        catch (Exception error)
        {
            batch.State = PartyOrderBatchState.PreparationFailed;
            batch.LastDiagnostic = error.Message;
            PublishPartyState(batch, ETableState.PartyOrderPreparationFailed);
            return; // Explicit cancellation is safe; selected menus are never silently rerolled.
        }
        bool allOwned = true;
        foreach (var order in batch.Orders) allOwned &= UserInfo.IsGiveRecipe(order.Food.Id);
        if (allOwned && !UserInfo.GetBowlAddEnabled(use.Stage, table.FloorType))
            foreach (var order in batch.Orders)
            {
                if (!batch.IsCurrent) return;
                try { UserInfo.AddSatisfaction(use.Stage, -4); }
                catch (Exception error) { batch.LastDiagnostic = error.ToString(); }
            }
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
        if (batch.State == PartyOrderBatchState.PreparationFailed) return PartyOrderAcceptResult.InvalidMenu;
        if (batch.State != PartyOrderBatchState.Prepared) return PartyOrderAcceptResult.AlreadyHandled;
        if (!batch.ValidateOrders()) return PartyOrderAcceptResult.InvalidMenu;
        bool allOwned = true;
        foreach (var order in batch.Orders) allOwned &= UserInfo.IsGiveRecipe(order.Food.Id);
        if (!allOwned)
        {
            if (batch.Round == 1) { RejectPartyOrders(batch); return PartyOrderAcceptResult.RejectedRecipe; }
            HoldUnsupportedPartyPolicy(batch.Use, "AdditionalRoundRecipeNotOwned");
            return PartyOrderAcceptResult.UnsupportedPolicy;
        }
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
        if (!order.IsCurrent || order.Batch.Use.PolicyPending || order.Batch.State != PartyOrderBatchState.Accepted
            || !order.Identity.CookingTerminal || order.Identity.CookingCancelled
            || order.Identity.CookingCompleted) return;
        order.Identity.CookingCompleted = true;
        order.State = PartyFoodState.ReadyForServing;
        RefreshPartyMealState(order.Batch);

    }

    private bool PublishPartyState(PartyOrderBatch batch, ETableState state)
    {
        if (!batch.IsCurrent || batch.Use.PolicyPending) return false;
        bool ok = true;
        try { batch.Use.Table.TableState = state; }
        catch (Exception error) { batch.LastDiagnostic = error.Message; ok = false; }
        if (!batch.IsCurrent || batch.Use.PolicyPending) return false;
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
