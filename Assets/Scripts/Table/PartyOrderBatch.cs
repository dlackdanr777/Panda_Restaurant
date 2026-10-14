using System;
using System.Collections.Generic;

public enum PartyOrderBatchState { Prepared, Accepting, Accepted, Rejected, Cancelled, Preparing, PreparationFailed }
public enum PartyFoodState { Prepared, Queued, Cooking, ReadyForServing, Cancelled, Carrying, Eating, CurrentMealCompleted }
public enum PartyOrderAcceptResult { Accepted, AcceptedPresentationFailed, AlreadyHandled, Stale, InvalidMenu, KitchenUnavailable, RegistrationFailed, RejectedRecipe, UnsupportedPolicy }

// Runtime only: this object is the initial-round identity owned by one TableUseIdentity.
// Neither the orders nor their planned amounts are account-save data or paid rewards.
public sealed class PartyOrderBatch
{
    internal readonly TableUseIdentity Use;
    public int Round { get; }
    private readonly List<PartyFoodOrder> _orders = new List<PartyFoodOrder>();
    public IReadOnlyList<PartyFoodOrder> Orders { get; }
    public PartyOrderBatchState State { get; internal set; } = PartyOrderBatchState.Preparing;
    public string LastDiagnostic { get; internal set; }
    internal KitchenUtensilGroup Kitchen;
    internal readonly HashSet<CustomerVisitMember> Exited = new HashSet<CustomerVisitMember>();
    internal PartyOrderBatch(TableUseIdentity use) { Use = use; Round = ++use.Round; Orders = _orders.AsReadOnly(); }
    internal void Add(PartyFoodOrder order) { _orders.Add(order); }
    internal bool IsCurrent => ReferenceEquals(Use.Orders, this) && !Use.IsSingle
        && Use.IsPartyCurrent(Use.Table) && Use.AllSeated;
    internal bool ValidateOrders()
    {
        if (!IsCurrent || Use.CompletionStarted || Use.PolicyPending || _orders.Count < 1 || _orders.Count > Use.Party.Members.Count) return false;
        var seen = new HashSet<CustomerVisitMember>();
        foreach (var order in _orders)
            if (!order.IsCurrent || !seen.Add(order.Member) || order.Food == null
                || string.IsNullOrEmpty(order.Food.Id) || order.Cooking.IsDefault()
                || float.IsNaN(order.Cooking.CookTime) || float.IsInfinity(order.Cooking.CookTime)
                || order.Cooking.CookTime <= 0) return false;
        return true;
    }
    internal void Cancel()
    {
        if (State != PartyOrderBatchState.Rejected) State = PartyOrderBatchState.Cancelled;
        foreach (var order in _orders) order.Cancel();
        if (Kitchen != null) Kitchen.CancelUse(Use);
    }
}

public sealed class PartyFoodOrder
{
    public PartyOrderBatch Batch { get; }
    public CustomerVisitMember Member { get; }
    public int SeatIndex { get; }
    public FoodData Food { get; }
    public int RemainingAdditionalOrders { get; }
    public int PlannedPrice { get; private set; }
    public int PlannedTip { get; private set; }
    public PartyFoodState State { get; internal set; } = PartyFoodState.Prepared;
    public CookingData Cooking { get; internal set; }
    internal readonly TableOrderIdentity Identity;
    internal bool CompletionRecorded;
    private readonly object _visit;
    internal PartyFoodOrder(PartyOrderBatch batch, CustomerVisitMember member, FoodData food, int price, int tip)
    {
        Batch = batch; Member = member; SeatIndex = member.SeatIndex; Food = food;
        _visit = member.VisitIdentity; PlannedPrice = price; PlannedTip = tip;
        RemainingAdditionalOrders = Math.Max(0, batch.Use.GetCompletion(member).RemainingOrders - 1);
        Identity = new TableOrderIdentity(batch.Use, this);
    }
    internal bool IsCurrent => Batch.IsCurrent && Batch.Use.IsMemberCurrent(Member)
        && ReferenceEquals(_visit, Member.VisitIdentity) && SeatIndex == Member.SeatIndex
        && Batch.Orders.ContainsReference(this);
    internal void Cancel()
    {
        Identity.ServingReservation?.Release();
        Identity.CancelCooking();
        State = PartyFoodState.Cancelled;
        PlannedPrice = 0; PlannedTip = 0;
    }
}

internal static class PartyOrderLookup
{
    internal static bool ContainsReference(this IReadOnlyList<PartyFoodOrder> orders, PartyFoodOrder candidate)
    { foreach (var order in orders) if (ReferenceEquals(order, candidate)) return true; return false; }
}
