using System;
using System.Collections.Generic;

public partial class KitchenUtensilGroup
{
    internal bool IsOrderKitchenReady
    {
        get
        {
            if (_burnerDatas == null || _burnerTimers == null || _burnerDatas.Length != _burnerTimers.Length) return false;
            bool usable = false;
            for (int index = 0; index < _burnerDatas.Length; index++)
            {
                if (_burnerDatas[index] == null || _burnerTimers[index] == null) return false;
                usable |= _burnerDatas[index].IsUsable;
            }
            return usable;
        }
    }
#if UNITY_EDITOR
    // Failure injection occurs while building a private candidate queue, never after commit.
    internal Func<int, bool> PartyPreparationCheck;
#endif
    internal bool TryRegisterPartyBatch(PartyOrderBatch batch)
    {
        if (!IsOrderKitchenReady || batch == null || batch.State != PartyOrderBatchState.Accepting
            || batch.Use.Table.FloorType != FloorType || !batch.ValidateOrders()) return false;
        try
        {
            var candidate = new Queue<CookingData>(_cookingQueue);
            var identities = new HashSet<TableOrderIdentity>();
            for (int index = 0; index < batch.Orders.Count; index++)
            {
                var order = batch.Orders[index];
                if (!identities.Add(order.Identity) || order.Identity.CookingTerminal
                    || order.State != PartyFoodState.Prepared) return false;
#if UNITY_EDITOR
                if (PartyPreparationCheck != null && !PartyPreparationCheck(index)) return false;
#endif
                candidate.Enqueue(order.Cooking);
            }
            if (batch.State != PartyOrderBatchState.Accepting || !batch.ValidateOrders()) return false;
            // Unity main-thread transaction: no yields, observers, UI or burner allocation
            // between candidate publication and the complete batch's state transition.
            _cookingQueue = candidate;
            foreach (var order in batch.Orders) order.State = PartyFoodState.Queued;
            batch.Kitchen = this;
            batch.State = PartyOrderBatchState.Accepted;
            return true;
        }
        catch (Exception error)
        {
            batch.LastDiagnostic = error.Message;
            return false;
        }
    }

    internal void CancelUse(TableUseIdentity use)
    {
        if (use == null) return;
        var remaining = new Queue<CookingData>();
        foreach (var food in _cookingQueue)
        {
            if (food.Identity != null && ReferenceEquals(food.Identity.Use, use)) food.Identity.CancelCooking();
            else remaining.Enqueue(food);
        }
        _cookingQueue = remaining;
        if (_burnerDatas == null) return;
        for (int index = 0; index < _burnerDatas.Length; index++)
        {
            var burner = _burnerDatas[index];
            if (burner.CookingData.IsDefault() || burner.CookingData.Identity == null
                || !ReferenceEquals(burner.CookingData.Identity.Use, use)) continue;
            burner.CookingData.Identity.CancelCooking();
            try { ReleaseCancelledStaff(burner); }
            catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
            // Reset commits the model before touching timer/smoke presentation.
            // A missing view must not leave subsequent cancelled burners occupied.
            try { ResetBurnerData(index); }
            catch (Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        }
    }

    private static void ReleaseCancelledStaff(KitchenBurnerData burner)
    {
        burner.SetUseStaff(null);
        burner.SetStaffUsable(false);
        burner.SetAddCookSpeedMul(0);
        if (burner.KitchenUtensil != null) burner.KitchenUtensil.SetStaffWorking(false);
    }
}
