#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CustomerTableUseRegressionTests;
using Pair = CustomerPartyOrderTests.Pair;
using Object = UnityEngine.Object;

// Captured production callbacks are advanced explicitly here. Real movement is
// recorded separately by the owned Stage1 preview; these are not motion tests.
public sealed class CustomerPartyServingTests
{
    private static void Cook(Pair p) { Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.Accepted)); p.Pump(); p.Pump(); p.Pump(); }
    private static FoodDistributionCounter Counter(Pair p)
    {
        var c = p.S.Create<FoodDistributionCounter>();
        Set(c, "_bubble", p.S.Create<Transform>().gameObject); Set(c, "_bubbleText", p.S.Create<TMPro.TextMeshPro>());
        Set(c, "_foodStartTr", c.transform); Set(c, "_foodInterval", 1.2f);
        Set(c, "_foodPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Restaurant/식당_배달접시.prefab"));
        c.Init(p.S.Table.FloorType, new List<TableData> { p.S.Table }); return c;
    }
    private static void FinishMeal(Pair p, PartyFoodOrder food)
    {
        p.S.Manager.StopAllCoroutines();
        var routine = (IEnumerator)Invoke(p.S.Manager, "EatPartyOrder", food);
        Assert.That(routine.MoveNext(), Is.True); Assert.That(routine.MoveNext(), Is.True);
        Assert.That(food.Identity.EatingAnimationStarted, Is.True);
        Assert.That(routine.MoveNext(), Is.False);
    }
    private static void Order(Pair p, (StaffWaiter staff, WaiterAction action) w)
    {
        Invoke(w.action, "OrderAction", p.S.Table); p.S.Arrive(w.staff); p.S.TakeWait(.5f)(); p.S.TakeWait(.5f)();
    }
    [TestCase(false)] [TestCase(true)]
    public void OneWaiterVisit_AcceptsStableWholeBatch_AndManualCannotDuplicate(bool same)
    {
        using (var p = new Pair(same: same))
        {
            var orders = p.Batch.Orders.ToArray(); var w = p.S.CreateWaiter();
            Order(p,w); Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Accepted));
            Assert.That(p.S.Queue.Count, Is.EqualTo(2)); Assert.That(p.S.Table.CurrentUse.OrderReservation, Is.Null);
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled));
            Assert.That(p.Batch.Orders, Is.EqualTo(orders)); Assert.That(p.S.Queue.Count, Is.EqualTo(2));
            w.action.Destructor(); Assert.That(p.Batch.Orders, Is.EqualTo(orders)); Assert.That(p.S.Queue.Count, Is.EqualTo(2));
        }
    }
    [TestCase(true,false)] [TestCase(false,false)]
    public void WaiterUsesSharedRecipeRejection_Once(bool first, bool second)
    {
        using (var p = new Pair(first,second))
        {
            UserInfo.AddSatisfaction(UserInfo.CurrentStage, 30); var w = p.S.CreateWaiter(); Order(p,w);
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Rejected)); Assert.That(p.S.Queue, Is.Empty);
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(20)); Assert.That(UserInfo.TotalCookCount, Is.Zero);
            w.action.Destructor(); Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(20));
        }
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void ManualFirstOrCancelBeforeAccept_PreservesMenusAndAmounts(int moment)
    {
        using (var p = new Pair())
        {
            var orders = p.Batch.Orders.ToArray(); var prices = orders.Select(o => o.PlannedPrice).ToArray();
            var w=p.S.CreateWaiter(); Invoke(w.action,"OrderAction",p.S.Table);
            var late=Get<Action>(w.staff,"_moveCompleted");
            if (moment==1) { p.S.Arrive(w.staff); p.S.TakeWait(.5f)(); }
            if (moment==2) { p.Accept(); p.S.Arrive(w.staff); p.S.TakeWait(.5f)(); p.S.TakeWait(.5f)(); }
            w.action.Destructor(); late();
            Assert.That(p.S.Table.CurrentUse.OrderReservation, Is.Null);
            if(moment!=2) Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.Accepted));
            Assert.That(p.S.Queue.Count,Is.EqualTo(2)); Assert.That(p.Batch.Orders,Is.EqualTo(orders));
            Assert.That(orders.Select(o=>o.PlannedPrice),Is.EqualTo(prices)); Assert.That(orders[0].RemainingAdditionalOrders,Is.EqualTo(1));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void PlateReservationPickupCancelAndDelivery_CountIndividualOrders(bool same)
    {
        using(var p=new Pair(same:same))
        {
            var c=Counter(p); Cook(p); var foods=p.Batch.Orders.ToArray(); var w=p.S.CreateWaiter();
            Assert.That(c.DisplayOrders,Is.EqualTo(foods.Select(f=>f.Identity)));
            var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
            Assert.That(lease.Order,Is.SameAs(foods[0].Identity)); Assert.That(c.DisplayOrders.Count,Is.EqualTo(2));
            Assert.That(p.S.Manager.TryServePartyOrder(foods[0]),Is.False);
            Assert.That(p.S.Manager.PickUpWaiterFood(lease),Is.True); Assert.That(c.DisplayOrders.Single(),Is.SameAs(foods[1].Identity));
            Assert.That(foods[0].State,Is.EqualTo(PartyFoodState.Carrying)); lease.Release(); lease.Release();
            Assert.That(c.DisplayOrders.Count,Is.EqualTo(2)); Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2));
            Assert.That(p.S.Manager.TryServePartyOrder(foods[1]),Is.True); Assert.That(p.S.Manager.TryServePartyOrder(foods[1]),Is.False);
            Assert.That(c.DisplayOrders.Single(),Is.SameAs(foods[0].Identity));
            lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true); p.S.Manager.PickUpWaiterFood(lease);
            Assert.That(p.S.Manager.TryDeliverFood(lease.Order,lease),Is.True); Assert.That(p.S.Manager.TryDeliverFood(lease.Order,lease),Is.False);
            Assert.That(c.DisplayOrders,Is.Empty); Assert.That(foods.All(f=>f.Identity.ServingStarted),Is.True);
        }
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void RealWaiterCallbacks_CancelAtEachStage_KeepOneCookedFood(int stage)
    {
        using(var p=new Pair())
        {
            Cook(p); var c=Counter(p); var food=p.Batch.Orders[0]; var w=p.S.CreateWaiter();
            Invoke(w.action,"ServingAction",p.S.Table); var oldMove=Get<Action>(w.staff,"_moveCompleted");
            Action late=null;
            if(stage>=1) { p.S.Arrive(w.staff); late=p.S.TakeWait(.5f); if(stage>=2) late(); }
            if(stage>=3) { p.S.Arrive(w.staff); p.S.TakeWait(.25f)(); late=p.S.TakeWait(.5f); if(stage>=4) late(); }
            w.action.Destructor(); oldMove(); late?.Invoke();
            Assert.That(food.Identity.ServingReservation,Is.Null); Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2));
            Assert.That(p.S.Queue,Is.Empty); Assert.That(food.RemainingAdditionalOrders,Is.EqualTo(1));
            if(stage<4) { Assert.That(c.DisplayOrders.Count,Is.EqualTo(2)); Assert.That(p.S.Manager.TryServePartyOrder(food),Is.True); }
            else { Assert.That(c.DisplayOrders.Count,Is.EqualTo(1)); Assert.That(p.S.Manager.TryServePartyOrder(food),Is.False); }
        }
    }
    [TestCase("disable")] [TestCase("clear")] [TestCase("pool")] [TestCase("scene")]
    public void StaffLifecycle_ReleasesCarry_AndOldCallbackCannotReleaseNewLease(string reason)
    {
        using(var p=new Pair())
        {
            Cook(p); var w=p.S.CreateWaiter(); Invoke(w.action,"ServingAction",p.S.Table);
            p.S.Arrive(w.staff); p.S.TakeWait(.5f)(); var late=Get<Action>(w.staff,"_moveCompleted");
            if(reason=="disable") Invoke(w.staff,"OnDisable");
            else if(reason=="clear") w.staff.SetStaffData(null,p.S.Table.FloorType);
            else if(reason=="pool") w.staff.ObjectPoolDespawnEvent();
            else Invoke(w.staff,"OnChangeSceneEvent");
            Assert.That(p.Batch.Orders[0].State,Is.EqualTo(PartyFoodState.ReadyForServing));
            var next=p.S.CreateWaiter(); var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,next.staff,next.action,true);
            late(); w.action.Destructor(); Assert.That(lease.IsCurrent,Is.True);
            Assert.That(p.S.Manager.PickUpWaiterFood(lease),Is.True); Assert.That(p.S.Manager.TryDeliverFood(lease.Order,lease),Is.True);
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void ParallelMeals_KeepPartnerState_ThenCompleteAdditionalRound(bool reverse)
    {
        using(var p=new Pair())
        {
            p.Accept(); p.Pump(); p.Pump();
            var a=p.Batch.Orders[0]; var b=p.Batch.Orders[1];
            Assert.That(a.State,Is.EqualTo(PartyFoodState.ReadyForServing)); Assert.That(b.State,Is.EqualTo(PartyFoodState.Cooking));
            if(reverse) { p.Pump(); var swap=a;a=b;b=swap; }
            Assert.That(p.S.Manager.TryServePartyOrder(a),Is.True);
            p.S.Manager.StopAllCoroutines(); var eat=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",a); eat.MoveNext(); eat.MoveNext();
            if(!reverse) p.Pump();
            Assert.That(a.State,Is.EqualTo(PartyFoodState.Eating)); Assert.That(b.State,Is.EqualTo(PartyFoodState.ReadyForServing));
            Assert.That(p.S.Manager.TryServePartyOrder(b),Is.True); Assert.That(eat.MoveNext(),Is.False);
            Assert.That(a.State,Is.EqualTo(PartyFoodState.CurrentMealCompleted)); Assert.That(b.State,Is.EqualTo(PartyFoodState.Eating));
            FinishMeal(p,b); Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.PartySeatedAwaitingOrders));
            Assert.That(p.S.Table.OccupantCount,Is.EqualTo(2)); Assert.That(p.Batch.Orders.Single().Member,Is.SameAs(p.Party.Members[0]));
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero); Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            CustomerPartyCompletionTests.CookRound(p);
            CustomerPartyCompletionTests.Eat(p,p.Batch.Orders.Single());
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(303));
            Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(1));
            p.ArriveExit(0); Assert.That(p.Party.Members[1].Customer.gameObject.activeSelf,Is.True);
            p.ArriveExit(1); Assert.That(p.S.Table.CurrentUse,Is.Null);
        }
    }
    [Test]
    public void PresentationFailure_CannotLoseCommittedDelivery_OrPreventOtherObservers()
    {
        using(var p=new Pair())
        {
            Cook(p); var w=p.S.CreateWaiter(); var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
            p.S.Manager.PickUpWaiterFood(lease); int notified=0;
            p.S.Table.OnServiceChangedHandler+=()=>throw new InvalidOperationException("owned display failure");
            p.S.Table.OnServiceChangedHandler+=()=>notified++;
            Assert.That(p.S.Manager.TryDeliverFood(lease.Order,lease),Is.True); FinishMeal(p,p.Batch.Orders[0]);
            Assert.That(notified,Is.GreaterThan(0)); Assert.That(p.S.Table.LastServiceDiagnostic,Does.Contain("owned display failure"));
        }
    }
    [Test]
    public void DestructorReentrancy_AndCancelledUse_DoNotLeakReservationOrDisableSingleButton()
    {
        using(var p=new Pair())
        {
            Cook(p); var w=p.S.CreateWaiter(); Invoke(w.action,"ServingAction",p.S.Table);
            p.S.Table.OnServiceChangedHandler+=()=>Invoke(w.action,"ServingAction",p.S.Table);
            w.action.Destructor(); Assert.That(p.Batch.Orders.All(f=>f.Identity.ServingReservation==null),Is.True);
            var next=p.S.CreateWaiter(); Invoke(next.action,"ServingAction",p.S.Table);
            Assert.That(Get<Button>(p.S.Table.ServingButton,"_button").interactable,Is.False);
            p.S.Manager.CancelPartyGuide(p.S.Table); Assert.That(Get<Button>(p.S.Table.ServingButton,"_button").interactable,Is.True);
            p.S.GuideAndSeat(); p.S.OrderAndCook(false); p.S.Eat(); p.S.OrderAndCook(false); p.S.Eat(); p.S.Settle(); p.S.AssertSettled(2);
        }
    }
    [TestCase("account")] [TestCase("use")] [TestCase("floor")]
    public void InvalidContext_OrFloorRejectsLateDeliveryWithoutRestoringOldFood(string reason)
    {
        using(var p=new Pair())
        {
            Cook(p); var w=p.S.CreateWaiter(); var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
            p.S.Manager.PickUpWaiterFood(lease);
            if(reason=="use") p.S.Manager.CancelPartyGuide(p.S.Table);
            else if(reason=="floor") Set(w.staff,"_equipFloorType",ERestaurantFloorType.Floor2);
            else p.S.Swap(typeof(UserInfo),"_currentStage",EStage.Stage2);
            Assert.That(p.S.Manager.TryDeliverFood(lease.Order,lease),Is.False); lease.Release();
            Assert.That(lease.Order.ServingReservation,Is.Null); Assert.That(lease.Order.ServingStarted,Is.False);
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void ActualPoolOrSameAssignment_PreservesFoodAndDirection(bool pool)
    {
        using(var p=new Pair())
        {
            Cook(p); var w=p.S.CreateWaiter(); var data=w.staff.StaffData;
            Invoke(w.action,"ServingAction",p.S.Table); p.S.Arrive(w.staff); p.S.TakeWait(.5f)();
            var late=Get<Action>(w.staff,"_moveCompleted"); var food=p.Batch.Orders[0];
            var old=food.Identity.ServingReservation; long lifetime=w.staff.WorkLifetime;
            w.staff.SetSpriteDir(1); float scale=w.staff.transform.localScale.x;
            if(pool)
            {
                ObjectPoolManager.Instance.DespawnStaff(EquipStaffType.Waiter,w.staff);
                var reused=ObjectPoolManager.Instance.SpawnStaff(EquipStaffType.Waiter,Vector3.zero,w.staff.transform.parent);
                Assert.That(reused,Is.SameAs(w.staff));
                reused.Init(EquipStaffType.Waiter,p.S.Manager,Get<KitchenSystem>(p.S.Manager,"_kitchenSystem"),p.S.Controller,Get<FeverSystem>(p.S.Manager,"_feverSystem"));
            }
            w.staff.SetStaffData(data,p.S.Table.FloorType); w.staff.SetSpriteDir(1);
            Assert.That(w.staff.transform.localScale.x,Is.EqualTo(scale)); Assert.That(w.staff.WorkLifetime,Is.GreaterThan(lifetime));
            Assert.That(old.Released,Is.True); Assert.That(food.State,Is.EqualTo(PartyFoodState.ReadyForServing));
            var action=(WaiterAction)Get<IStaffAction>(w.staff,"_staffAction");
            var fresh=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,action,true); late();
            Assert.That(fresh.IsCurrent,Is.True); Assert.That(fresh.Order,Is.SameAs(food.Identity));
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2)); Assert.That(p.S.Queue,Is.Empty);
        }
    }
    [Test]
    public void CounterInspectorSpacing_SurvivesSavedReloadAndFoodRefresh()
    {
        string path="Assets/PartyServingOwned-"+Guid.NewGuid().ToString("N")+".prefab";
        try
        {
            using(var p=new Pair())
            {
                var c=Counter(p); Set(c,"_foodInterval",2.75f);
                PrefabUtility.SaveAsPrefabAsset(c.gameObject,path); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FoodDistributionCounter>();
                Set(c,"_foodInterval",Get<float>(saved,"_foodInterval")); Cook(p);
                var plates=Get<List<GameObject>>(c,"_foodList");
                Assert.That(plates.Count,Is.EqualTo(2)); Assert.That(plates[1].transform.localPosition,Is.EqualTo(new Vector3(-2.75f,0,0)));
                p.S.Manager.TryServePartyOrder(p.Batch.Orders[0]); p.S.Table.NotifyServiceChanged();
                Assert.That(Get<float>(c,"_foodInterval"),Is.EqualTo(2.75f)); Assert.That(c.DisplayOrders.Count,Is.EqualTo(1));
            }
        }
        finally { if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) AssetDatabase.DeleteAsset(path); }
    }
}
#endif
