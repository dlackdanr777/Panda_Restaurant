using UnityEngine;
#if UNITY_EDITOR
// Owned Editor regressions inspect runtime identity without exposing it to players.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif

// One runtime lease, owned by the existing table use or individual order. It is not
// a second order list and never owns menus, money or a new cooking request.
internal sealed class TableServiceReservation
{
    internal readonly TableUseIdentity Use;
    internal readonly TableOrderIdentity Order;
    internal readonly PartyOrderBatch Batch;
    internal readonly StaffWaiter Owner;
    internal readonly WaiterAction Action;
    internal readonly long StaffLifetime;
    internal readonly ERestaurantFloorType Floor;
    internal readonly bool IsServing;
    internal bool PickedUp;
    internal bool Released;
    internal TableData Table => Use.Table;
    internal FoodData Food => Order.PartyOrder == null ? Table.CurrentFood.FoodData : Order.PartyOrder.Food;
    internal bool OwnsSlot => !Released && (IsServing
        ? ReferenceEquals(Order.ServingReservation, this) : ReferenceEquals(Use.OrderReservation, this));
    internal bool IsUseCurrent => Use.IsPartyCurrent(Table) && (Batch == null
        ? Order != null && Order.IsCurrent(Table) : ReferenceEquals(Use.Orders, Batch) && Batch.IsCurrent);
    internal bool IsCurrent => OwnsSlot && IsUseCurrent && Owner != null
        && Owner.IsWorkCurrent(Action, StaffLifetime, Floor);

    internal TableServiceReservation(TableUseIdentity use, TableOrderIdentity order,
        PartyOrderBatch batch, StaffWaiter owner, WaiterAction action, bool serving)
    {
        Use = use; Order = order; Batch = batch; Owner = owner; Action = action;
        StaffLifetime = owner.WorkLifetime; Floor = owner.EquipFloorType; IsServing = serving;
    }

    internal void Release()
    {
        if (!OwnsSlot) { Released = true; return; }
        if (IsServing) Order.ServingReservation = null;
        else Use.OrderReservation = null;
        Released = true;
        if (!IsUseCurrent) return;
        try
        {
        if (IsServing && !Order.ServingStarted)
        {
            if (Order.PartyOrder != null && Order.PartyOrder.State == PartyFoodState.Carrying)
                Order.PartyOrder.State = PartyFoodState.ReadyForServing;
            else if (Use.IsSingle && (Table.TableState == ETableState.UseStaff || Table.TableState == ETableState.StaffServing))
                Table.TableState = ETableState.CanServing;
        }
        else if (!IsServing && Use.IsSingle && !Order.OrderAccepted && Table.TableState == ETableState.UseStaff)
            Table.TableState = ETableState.Seating;
        }
        catch (System.Exception error) { Table.LastServiceDiagnostic = error.Message; }
        Table.NotifyServiceChanged();
    }
}
