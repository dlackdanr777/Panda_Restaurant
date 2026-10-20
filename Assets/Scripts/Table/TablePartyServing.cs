using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TableManager
{
    internal bool HasPendingOrder(TableData table)
    {
        var use = table == null ? null : table.CurrentUse;
        if (use == null || !use.IsPartyCurrent(table) || (use.CompletionStarted || use.PolicyPending || use.ServiceAborted) || use.OrderReservation != null) return false;
        return use.IsSingle ? table.TableState == ETableState.Seating && !table.CurrentFood.IsDefault()
            && !table.CurrentFood.Identity.OrderAccepted
            : use.Orders != null && use.Orders.State == PartyOrderBatchState.Prepared && use.Orders.ValidateOrders();
    }

    internal TableOrderIdentity GetReadyServingOrder(TableData table)
    {
        var use = table == null ? null : table.CurrentUse;
        if (use == null || !use.IsPartyCurrent(table) || (use.CompletionStarted || use.PolicyPending || use.ServiceAborted)) return null;
        if (use.IsSingle)
        {
            var order = table.CurrentFood.Identity;
            return table.TableState == ETableState.CanServing && IsFoodAtCounter(order)
                && order.ServingReservation == null ? order : null;
        }
        if (use.Orders == null || use.Orders.State != PartyOrderBatchState.Accepted) return null;
        foreach (var food in use.Orders.Orders)
            if (food.State == PartyFoodState.ReadyForServing && food.Identity.ServingReservation == null && food.IsCurrent)
                return food.Identity;
        return null;
    }

    // Derived view: the counter keeps identity references only; it never creates orders.
    internal static bool IsFoodAtCounter(TableOrderIdentity order)
    {
        if (order == null || !order.IsCurrent(order.Use.Table) || (order.Use.CompletionStarted || order.Use.PolicyPending || order.Use.ServiceAborted)
            || !order.CookingCompleted || order.ServingStarted || order.CookingCancelled) return false;
        if (order.PartyOrder != null)
            return order.PartyOrder.Batch.State == PartyOrderBatchState.Accepted
                && order.PartyOrder.State == PartyFoodState.ReadyForServing;
        var state = order.Use.Table.TableState;
        return (state == ETableState.CanServing || state == ETableState.UseStaff)
            && (order.ServingReservation == null || !order.ServingReservation.PickedUp);
    }

    internal TableData FindCleanableTable(Vector3 position, ERestaurantFloorType floor)
        => GetMinDistanceObject(RestaurantType.Hall, position, _furnitureSystem.GetTableDataList(floor),
            table => table.transform.position, table => table.TableFurniture != null && table.TableFurniture.CanClean, 1f);

    internal TableData FindWaiterTable(Vector3 position, ERestaurantFloorType floor, bool serving)
    {
        return GetMinDistanceObject(RestaurantType.Hall, position, _furnitureSystem.GetTableDataList(floor),
            table => table.transform.position, table => serving ? GetReadyServingOrder(table) != null : HasPendingOrder(table), 1f);
    }

    internal TableServiceReservation ReserveWaiterWork(TableData table, StaffWaiter staff, WaiterAction action, bool serving)
    {
        if (staff == null || table == null || staff.EquipFloorType != table.FloorType
            || !staff.IsWorkCurrent(action, staff.WorkLifetime, table.FloorType)) return null;
        var order = serving ? GetReadyServingOrder(table) : null;
        if (serving ? order == null : !HasPendingOrder(table)) return null;
        var use = table.CurrentUse;
        if (!serving && use.IsSingle) order = table.CurrentFood.Identity;
        var lease = new TableServiceReservation(use, order, use.Orders, staff, action, serving);
        if (serving) order.ServingReservation = lease;
        else use.OrderReservation = lease;
        try
        {
            if (use.IsSingle) table.TableState = ETableState.UseStaff;
            table.NotifyServiceChanged(); UpdateTable();
        }
        catch { lease.Release(); throw; }
        if (lease.IsCurrent) return lease;
        lease.Release(); return null;
    }

    internal bool AcceptWaiterOrder(TableServiceReservation lease)
    {
        if (lease == null || lease.IsServing || !lease.IsCurrent) return false;
        try
        {
            if (lease.Batch != null)
            {
                var result = TryAcceptPartyOrders(lease.Table, lease.Batch);
                return result == PartyOrderAcceptResult.Accepted || result == PartyOrderAcceptResult.AcceptedPresentationFailed
                    || result == PartyOrderAcceptResult.AlreadyHandled || result == PartyOrderAcceptResult.RejectedRecipe || result == PartyOrderAcceptResult.ServiceAborted;
            }
            OnCustomerOrder(lease.Table);
            return lease.Order.OrderAccepted || !lease.IsUseCurrent;
        }
        finally { lease.Release(); }
    }

    internal bool PickUpWaiterFood(TableServiceReservation lease)
    {
        if (lease == null || !lease.IsServing || !lease.IsCurrent || lease.PickedUp || !IsFoodAtCounter(lease.Order)) return false;
        lease.PickedUp = true;
        if (lease.Order.PartyOrder != null) lease.Order.PartyOrder.State = PartyFoodState.Carrying;
        else lease.Table.TableState = ETableState.StaffServing;
        lease.Table.NotifyServiceChanged();
        try { UpdateTable(); } catch (System.Exception error) { lease.Table.LastServiceDiagnostic = error.Message; }
        return lease.IsCurrent;
    }

    public bool TryServePartyOrder(PartyFoodOrder order) => TryDeliverFood(order == null ? null : order.Identity, null);

    internal bool TryDeliverFood(TableOrderIdentity order, TableServiceReservation lease)
    {
        if (order == null || !order.IsCurrent(order.Use.Table) || (order.Use.CompletionStarted || order.Use.PolicyPending || order.Use.ServiceAborted)
            || order.ServingStarted || !order.CookingCompleted) return false;
        if (lease == null)
        {
            if (order.ServingReservation != null || !IsFoodAtCounter(order)) return false;
        }
        else if (!lease.IsCurrent || !lease.IsServing || !lease.PickedUp || !ReferenceEquals(lease.Order, order)) return false;
        var table = order.Use.Table;
        if (order.PartyOrder == null)
        {
            // The legacy meal path still evaluates its existing tendency policy and rewards.
            OnServingSingle(table, order);
            bool delivered = order.ServingStarted;
            lease?.Release();
            return delivered;
        }
        var food = order.PartyOrder;
        if (food.Batch.State != PartyOrderBatchState.Accepted || (food.State != PartyFoodState.ReadyForServing && food.State != PartyFoodState.Carrying)) return false;
        if (!CheckPartyTendencies(food.Batch.Use, new[] { food.Member })) return false;
        order.ServingStarted = true;
        food.StopDeliveryClock();
        food.State = PartyFoodState.Eating;
        lease?.Release();
        StartCoroutine(EatPartyOrder(food));
        RefreshPartyMealState(food.Batch);
        return true;
    }

    private IEnumerator EatPartyOrder(PartyFoodOrder food)
    {
        yield return YieldCache.WaitForSeconds(0.5f);
        if (!CanContinuePartyMeal(food) || food.Identity.EatingAnimationStarted) yield break;
        food.Identity.EatingAnimationStarted = true;
        food.Batch.Use.GetCompletion(food.Member).MealStarted = true;
        var customer = food.Member.Customer;
        customer.transform.position = food.Batch.Use.Table.GetSeatPosition(food.SeatIndex);
        customer.ChangeState(CustomerState.Eat); customer.ShowFood(food.Food.Sprite);
        yield return YieldCache.WaitForSeconds(1.5f);
        if (!CanContinuePartyMeal(food)) yield break;
        food.Identity.EatingCompleted = true;
        food.State = PartyFoodState.CurrentMealCompleted;
        customer.HideFood(); customer.ChangeState(CustomerState.Idle);
        RefreshPartyMealState(food.Batch);
        CompletePartyMeal(food);
    }

    private static bool CanContinuePartyMeal(PartyFoodOrder food) => food != null && food.IsCurrent
        && food.Batch.State == PartyOrderBatchState.Accepted && !food.Batch.Use.CompletionStarted && !food.Batch.Use.PolicyPending
        && food.State == PartyFoodState.Eating && food.Identity.ServingStarted && !food.Identity.EatingCompleted;

    private void RefreshPartyMealState(PartyOrderBatch batch)
    {
        if (!batch.IsCurrent || batch.Use.PolicyPending || batch.Use.ServiceAborted) return;
        bool allCompleted = true, anyMeal = false, allReady = true;
        foreach (var food in batch.Orders)
        {
            allCompleted &= food.State == PartyFoodState.CurrentMealCompleted;
            anyMeal |= food.State == PartyFoodState.Carrying || food.State == PartyFoodState.Eating || food.State == PartyFoodState.CurrentMealCompleted;
            allReady &= food.State == PartyFoodState.ReadyForServing;
        }
        PublishPartyState(batch, allCompleted ? ETableState.PartyCurrentMealsCompleted : anyMeal ? ETableState.PartyMealsInProgress
            : allReady ? ETableState.PartyReadyForServing : ETableState.PartyWaitingForCooking);
        batch.Use.Table.NotifyServiceChanged();
    }
}
