#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Pair = CustomerPartyOrderTests.Pair;

public sealed class CustomerPartyFailureBoundaryTests
{
    [Test]
    public void SecondMenuLookupFailure_DoesNotPartiallyCommitFirstMember()
    {
        using (var p = new Pair(invalidMenu: true))
        {
            var use = p.S.Table.CurrentUse;
            TestContext.WriteLine("Observed remaining=" + string.Join(",", use.Completions.Select(x => x.RemainingOrders))
                + " price=" + string.Join(",", use.Completions.Select(x => x.TotalPrice))
                + " tip=" + string.Join(",", use.Completions.Select(x => x.TotalTip))
                + " satisfaction=" + string.Join(",", use.Completions.Select(x => x.Satisfaction))
                + " orders=" + p.Batch.Orders.Count);
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(use.Completions.Select(x => x.RemainingOrders), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(use.Completions.All(x => x.TotalPrice == 0 && x.TotalTip == 0 && x.Satisfaction == 0), Is.True);
            Assert.That(p.Batch.Orders, Is.Empty);
            Assert.That(p.S.Queue, Is.Empty);
            Assert.That(p.Burner.CookingData.IsDefault(), Is.True);
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.InvalidMenu));
        }
    }

    private static string Ledger(TableUseIdentity use) => string.Join("|", use.Completions.Select(x =>
        $"{x.RemainingOrders},{x.TotalPrice},{x.TotalTip},{x.Satisfaction},{x.MealsCompleted},{x.FinalCompleted},{x.VisitCounted},{x.CustomerCounted},{x.TipPaid},{x.CoinAmount},{x.CoinFixed},{x.CoinDropped},{x.SatisfactionApplied},{x.FeverApplied}"));

    private static void NoWork(Pair p)
    {
        Assert.That(p.S.Queue, Is.Empty); Assert.That(p.Burner.CookingData.IsDefault(), Is.True);
        Assert.That(p.S.Table.CurrentUse.OrderReservation, Is.Null);
        Assert.That(p.Batch.Orders.All(o => o.Identity.ServingReservation == null), Is.True);
        Assert.That(p.S.Table.OccupantCount, Is.EqualTo(2));
        var saved = p.S.Stage.GetTableData(p.S.Table.FloorType, p.S.Table.TableType);
        Assert.That(saved.NeedCleaning, Is.False);
        Assert.That(saved.CleaningBowlProcessed, Is.False);
        Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType), Is.Zero);
        Assert.That(p.S.Coins.CurrentMoney, Is.Zero); Assert.That(p.S.Table.DropGarbageArea.Count, Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void InvalidSecondCalculation_CommitsNoMember_AndRepeatedFailureDoesNotReroll(bool nan)
    {
        using (var p = new Pair(beforeGuide: x => {
            if (nan) Set(x.SecondFood, "_cookingTime", float.NaN);
            else Set(x.SecondFood, "_foodType", (FoodType)(-1));
        }))
        {
            var use=p.S.Table.CurrentUse; var batch=p.Batch; var state=UnityEngine.Random.state;
            Assert.That(batch.State, Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(batch.LastDiagnostic, Is.Not.Empty); Assert.That(batch.Orders, Is.Empty);
            Assert.That(use.Completions.Select(x=>x.RemainingOrders), Is.EqualTo(new[]{2,1}));
            Assert.That(use.Completions.All(x=>x.TotalPrice==0 && x.TotalTip==0 && x.Satisfaction==0), Is.True);
            for(int i=0;i<3;i++) {Invoke(p.S.Manager,"PreparePartyOrders",p.S.Table,use);p.S.Table.RefreshPartyOrderButtons();}
            Assert.That(p.Batch, Is.SameAs(batch)); Assert.That(UnityEngine.Random.state, Is.EqualTo(state)); NoWork(p);
            Assert.That(p.S.Stage.Satisfaction, Is.Zero);
        }
    }

    private sealed class LookupObserver : IEqualityComparer<string>
    {
        internal string Key; internal Action Callback;
        public bool Equals(string a,string b) => StringComparer.Ordinal.Equals(a,b);
        public int GetHashCode(string key)
        {
            if(key==Key && Callback!=null) {var action=Callback;Callback=null;action();}
            return StringComparer.Ordinal.GetHashCode(key);
        }
    }

    [TestCase("execution")] [TestCase("visit")]
    public void InvalidationDuringSecondLookup_CannotCommitOrPublishToReplacement(string kind)
    {
        TableUseIdentity captured=null; PartyOrderBatch attempt=null;
        using(var p=new Pair(requireBatch:false,beforeGuide:x=>{
            var comparer=new LookupObserver{Key=x.SecondFood.Id};
            var map=new Dictionary<string,FoodData>(Get<Dictionary<string,FoodData>>(FoodDataManager.Instance,"_foodDataDic"),comparer);
            Set(FoodDataManager.Instance,"_foodDataDic",map);
            comparer.Callback=()=>{
                captured=x.S.Table.CurrentUse;attempt=captured.Orders;
                if(kind=="execution") Invoke(x.S.Manager,"InvalidateExecution");
                else x.Party.Members[1].Customer.SetData(x.SecondData,x.S.Controller,x.S.Manager);
            };
        }))
        {
            Assert.That(captured,Is.Not.Null);Assert.That(attempt.State,Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(captured.Completions.Select(x=>x.RemainingOrders),Is.EqualTo(new[]{2,1}));
            Assert.That(captured.Completions.All(x=>x.TotalPrice==0&&x.TotalTip==0&&x.Satisfaction==0),Is.True);
            Assert.That(attempt.Orders,Is.Empty);Assert.That(p.S.Queue,Is.Empty);
            Assert.That(p.S.Table.CurrentUse,Is.Null);Assert.That(p.S.Stage.Satisfaction,Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void AdditionalSingleParticipantFailure_PreservesCompletedMemberAndPriorMeal(bool tipAlreadyPaid)
    {
        using(var p=new Pair(ordersA:1,ordersB:2))
        {
            CustomerPartyCompletionTests.CookRound(p);var foods=p.Batch.Orders.ToArray();
            CustomerPartyCompletionTests.Eat(p,foods[0]);var use=p.S.Table.CurrentUse;
            if(tipAlreadyPaid) p.S.TakeWait(.5f)();
            var a=use.Completions[0];string fixedA=Ledger(use).Split('|')[0];
            Set(p.SecondData,"_requiredDish","");CustomerPartyCompletionTests.Eat(p,foods[1]);
            Assert.That(p.Batch.State,Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(Ledger(use).Split('|')[0],Is.EqualTo(fixedA));
            var b=use.Completions[1];Assert.That(b.RemainingOrders,Is.EqualTo(1));
            Assert.That(b.TotalPrice,Is.EqualTo(101));Assert.That(b.TotalTip,Is.EqualTo(50));Assert.That(b.MealsCompleted,Is.EqualTo(1));
            Assert.That(a.FinalCompleted&&a.VisitCounted&&a.CoinFixed&&!a.CoinDropped,Is.True);
            if(!tipAlreadyPaid)p.S.TakeWait(.5f)();
            Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(a.TipPaid,Is.True);NoWork(p);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void PostCommitObserverFailure_DoesNotRepeatMenusLedgerOrEnvironment(bool fullSink)
    {
        int notifications=0;
        using(var p=new Pair(beforeGuide:x=>{
            if(fullSink)while(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage,x.S.Table.FloorType))UserInfo.AddSinkBowlCount(UserInfo.CurrentStage,x.S.Table.FloorType);
            x.S.Table.OnChangeTableStateHandler+=state=>{
                if(state!=ETableState.PartySeatedAwaitingOrders)return;
                notifications++;Invoke(x.S.Manager,"PreparePartyOrders",x.S.Table,x.S.Table.CurrentUse);
                throw new InvalidOperationException("Injected post-commit presentation failure");
            };
        }))
        {
            Assert.That(p.Batch.State,Is.EqualTo(PartyOrderBatchState.Prepared));
            var batch=p.Batch;var ids=batch.Orders.ToArray();var ledger=Ledger(batch.Use);var random=UnityEngine.Random.state;
            for(int i=0;i<3;i++) {Invoke(p.S.Manager,"PreparePartyOrders",p.S.Table,batch.Use);p.S.Table.RefreshPartyOrderButtons();}
            Assert.That(Ledger(batch.Use),Is.EqualTo(ledger));Assert.That(p.Batch.Orders,Is.EqualTo(ids));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(notifications,Is.EqualTo(1));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(fullSink?-8:0));Assert.That(p.S.Queue,Is.Empty);
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.Accepted));Assert.That(p.S.Queue.Count,Is.EqualTo(2));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void OldGatecrasherDelay_DoesNotEndNextSingleUse(bool advanceBeforeReplacement)
    {
        using(var s=new Flow())
        {
            s.GuideAndSeat();var gate=s.Create<GatecrasherCustomer>();
            var delayed=(IEnumerator)Invoke(gate,"DelayedAngerCustomer",s.Table,s.Manager,4f);
            if(advanceBeforeReplacement)Assert.That(delayed.MoveNext(),Is.True);
            s.NextCustomer(true);s.GuideAndSeat();var use=s.Table.CurrentUse;var before=s.Snapshot();
            if(!advanceBeforeReplacement)Assert.That(delayed.MoveNext(),Is.True);
            Assert.That(delayed.MoveNext(),Is.False);
            Assert.That(s.Table.CurrentUse,Is.SameAs(use));Assert.That(s.Snapshot(),Is.EqualTo(before));
        }
    }

    [Test]
    public void CurrentGatecrasherDelay_PreservesExistingSingleDispleasure()
    {
        using(var s=new Flow())
        {
            s.GuideAndSeat();var delayed=(IEnumerator)Invoke(s.Create<GatecrasherCustomer>(),"DelayedAngerCustomer",s.Table,s.Manager,4f);
            Assert.That(delayed.MoveNext(),Is.True);Assert.That(delayed.MoveNext(),Is.False);
            Assert.That(s.Table.CurrentUse,Is.Null);Assert.That(s.Stage.Satisfaction,Is.EqualTo(-5));
        }
    }

    // Service cases below assert approved 3D-B behavior; remaining Boundary_ cases are technical holds.
    [TestCase(1,false)] [TestCase(1,true)] [TestCase(2,false)]
    public void AdditionalUnowned_SettlesOnlyCompletedMealsAndPreservesPaidOrPendingTip(int ordersA,bool tipAlreadyPaid)
    {
        using(var p=new Pair(ordersA:ordersA,ordersB:2))
        {
            CustomerPartyCompletionTests.CookRound(p);var foods=p.Batch.Orders.ToArray();
            CustomerPartyCompletionTests.Eat(p,foods[0]);
            if(tipAlreadyPaid)p.S.TakeWait(.5f)();
            ((Dictionary<string,int>)typeof(UserInfo).GetField("_giveRecipeLevelDic",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null)).Remove(p.SecondFood.Id);
            CustomerPartyCompletionTests.Eat(p,foods[1]);
            Assert.That(p.Batch.Round,Is.EqualTo(2));Assert.That(p.Batch.Orders.Count,Is.EqualTo(ordersA==1?1:2));
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.ServiceAborted));var use=p.S.Table.CurrentUse;
            Assert.That(use.PolicyPending,Is.False);Assert.That(use.ServiceAborted&&use.CompletionStarted,Is.True);
            Assert.That(use.Completions.Select(x=>x.MealsCompleted),Is.EqualTo(new[]{1,1}));
            Assert.That(use.Completions[0].FinalCompleted,Is.EqualTo(ordersA==1));
            if(ordersA==1){if(!tipAlreadyPaid)p.S.TakeWait(.5f)();Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(use.Completions[0].CoinAmount,Is.EqualTo(101));}
            else Assert.That(p.S.Stage.Tip,Is.Zero);
            Assert.That(use.Completions[1].TotalPrice,Is.EqualTo(202));
            Assert.That(use.Completions.Select(x=>x.CompletedPrice),Is.EqualTo(new[]{101,101}));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Stage.FeverGauge,Is.Zero);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void WaitingClock_FirstAllReadyStartsOnce_AndLegacyMemberClocksStayOff(int arrived)
    {
        using(var s=new Flow())
        {
            var b=s.Track(UnityEngine.Object.Instantiate(s.Data));Set(b,"_id","WAIT-B");Set(b,"_wiatTime",1);Set(s.Data,"_wiatTime",2);
            Get<Dictionary<string,CustomerData>>(CustomerDataManager.Instance,"_customerDataDic")[b.Id]=b;
            ((Dictionary<string,SaveCustomerData>)typeof(UserInfo).GetField("_enabledCustomerDic",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null))[b.Id]=new SaveCustomerData(b.Id,"",0);
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,b.Id},out var party),Is.True);
            for(int i=0;i<arrived;i++) {var c=party.Members[i].Customer;c.transform.position=c.TargetPos;Get<Action>(c,"_moveCompleted")();}
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out var began,out var limit),Is.EqualTo(arrived==2));
            if(arrived==2) Assert.That(limit,Is.EqualTo(2));
            Assert.That(party.Members.All(x=>Get<Coroutine>(x.Customer,"_waitingCoroutine")==null),Is.True);
            Assert.That(s.Controller.Count,Is.EqualTo(1));Assert.That(s.Controller.WaitingPersonCount,Is.EqualTo(2));
            Assert.That(s.Controller.WaitCount,Is.EqualTo(arrived==2?1:0));
            Assert.That(s.Stage.WaitingParties.Teams.Single(),Is.EqualTo(new[]{s.Data.Id,b.Id}));
            Assert.That(s.Controller.CancelWaitingParty(party),Is.True);Assert.That(s.Controller.Count,Is.Zero);
            Assert.That(s.Stage.WaitingParties.Teams,Is.Empty);
        }
    }

    [Test]
    public void FoodClock_UsesOrderIdentity_AndLegacyMemberClocksStayOff()
    {
        using(var p=new Pair(ordersA:1,ordersB:2))
        {
            Action noTimers=()=>Assert.That(p.Party.Members.All(x=>Get<Coroutine>(x.Customer,"_orderFoodCoroutine")==null),Is.True);
            Assert.That(p.Batch.Orders.All(o=>o.IsWaitingForDelivery),Is.True);
            noTimers();p.Accept();noTimers();p.Pump();noTimers();p.Pump();p.Pump();noTimers();
            var foods=p.Batch.Orders.ToArray();CustomerPartyCompletionTests.Eat(p,foods[0]);noTimers();
            Assert.That(foods[0].IsWaitingForDelivery,Is.False);Assert.That(foods[1].IsWaitingForDelivery,Is.True);
            CustomerPartyCompletionTests.Eat(p,foods[1]);noTimers();Assert.That(p.Batch.Round,Is.EqualTo(2));
            Assert.That(p.S.Table.CurrentUse.Completions[0].FinalCompleted,Is.True);
        }
    }

    [Test]
    public void FailedPairPreparation_PreservesOtherTableCookingIdentityAndAmounts()
    {
        TableData other=null;object identity=null;int price=0;
        using(var p=new Pair(invalidMenu:true,beforeSpawn:x=>{
            other=CustomerPartyOrderTests.AddSingleOrder(x);x.Pump();identity=other.CurrentFood.Identity;price=other.TotalPrice;
        }))
        {
            Assert.That(p.Batch.State,Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(p.Burner.CookingData.Identity,Is.SameAs(identity));Assert.That(other.TotalPrice,Is.EqualTo(price));
            Assert.That(p.S.Queue,Is.Empty);p.Pump();Assert.That(other.TableState,Is.EqualTo(ETableState.CanServing));
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(1));Assert.That(p.Batch.Orders,Is.Empty);
        }
    }

    [Test]
    public void EnvironmentObserverCannotAcceptBeforePreparationPublication()
    {
        int calls=0;
        using(var p=new Pair(beforeGuide:x=>{
            while(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage,x.S.Table.FloorType))UserInfo.AddSinkBowlCount(UserInfo.CurrentStage,x.S.Table.FloorType);
            x.S.Stage.OnChangeSatisfactionHandler+=()=>{
                calls++;Assert.That(x.Accept(),Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled));
                Invoke(x.S.Manager,"PreparePartyOrders",x.S.Table,x.S.Table.CurrentUse);
                throw new InvalidOperationException("Injected environment view failure");
            };
        }))
        {
            Assert.That(calls,Is.EqualTo(2));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-8));
            Assert.That(p.S.Queue,Is.Empty);Assert.That(p.Batch.State,Is.EqualTo(PartyOrderBatchState.Prepared));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.PartySeatedAwaitingOrders));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void MixedTendencies_EitherMemberCanCauseOneTeamAbort(bool reverse)
    {
        using(var p=new Pair(requireBatch:false,beforeGuide:x=>{
            Set(x.S.Data,"_tendencyType",reverse?CustomerTendencyType.HighlySensitive:CustomerTendencyType.Normal);
            Set(x.SecondData,"_tendencyType",reverse?CustomerTendencyType.Normal:CustomerTendencyType.HighlySensitive);
        }))
        {
            var use=p.S.Table.CurrentUse;
            Assert.That(use.ServiceAborted&&use.CompletionStarted,Is.True);
            Assert.That(use.ServiceAbortReason,Is.EqualTo("CustomerTendency"));
            Assert.That(p.Batch,Is.Null);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero);Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            Assert.That(p.S.Manager.AbortPartyService(use,"Gatecrasher"),Is.False);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [TestCase("prepared")] [TestCase("cooking")] [TestCase("carrying")] [TestCase("eating")]
    public void FurnitureFinalRemoval_StopsUnfinishedWorkAndExitsBothOnce(string point)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            IEnumerator eat=null;TableServiceReservation lease=null;
            var food=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;
            if(point=="cooking"){p.Accept();p.Pump();}
            if(point=="carrying"||point=="eating")
            {
                CustomerPartyCompletionTests.CookRound(p);
                if(point=="carrying"){
                    var w=p.S.CreateWaiter();lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
                    Assert.That(p.S.Manager.PickUpWaiterFood(lease),Is.True);
                } else {
                    p.S.Manager.TryServePartyOrder(food);p.S.Manager.StopAllCoroutines();eat=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",food);
                    Assert.That(eat.MoveNext(),Is.True);Assert.That(eat.MoveNext(),Is.True);
                }
            }
            int cooked=UserInfo.TotalCookCount;p.S.Table.TableType=TableType.Table2;
            var edit=p.S.Manager.BeginFurnitureEdit();p.S.Manager.NotFurnitureTable(p.S.Table);
            Assert.That(use.PolicyPending||use.CompletionStarted,Is.False);
            p.S.Manager.CompleteFurnitureEdit(edit);p.S.Manager.CompleteFurnitureEdit(edit);
            Assert.That(use.PolicyPending,Is.False);Assert.That(use.ServiceAborted&&use.FurnitureRemoved&&use.CompletionStarted,Is.True);
            if(lease!=null)Assert.That(lease.Released,Is.True);
            if(eat!=null){Assert.That(eat.MoveNext(),Is.False);Assert.That(food.CompletionRecorded,Is.False);}
            if(point=="cooking"){p.Pump();p.Pump();Assert.That(p.S.Queue,Is.Empty);Assert.That(p.Burner.CookingData.IsDefault(),Is.True);}
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(cooked));Assert.That(p.S.Stage.Tip,Is.Zero);Assert.That(p.S.Coins.CurrentMoney,Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            Assert.That(use.Completions.All(c=>c.ExitStarted),Is.True);
            p.ArriveExit(1);Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));p.ArriveExit(0);
            Assert.That(p.S.Table.CurrentUse,Is.Null);Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.DontUse));
        }
    }

    [Test]
    public void CosmeticFurnitureRefresh_MovesBothSeatsFoodAndAnchorsWithoutResettingLedger()
    {
        using(var p=new Pair())
        {
            CustomerPartyCompletionTests.CookRound(p);var food=p.Batch.Orders[0];p.S.Manager.TryServePartyOrder(food);p.S.Manager.StopAllCoroutines();
            var eat=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",food);eat.MoveNext();eat.MoveNext();
            var use=p.S.Table.CurrentUse;var ledger=Ledger(use);var renderer=Get<SpriteRenderer>(food.Member.Customer,"_foodRenderer");
            var local=renderer.transform.localPosition;
            Set(p.S.Table,"_leftSeatOffset",new Vector2(.2f,.3f));Set(p.S.Table,"_rightSeatOffset",new Vector2(-.3f,.4f));
            p.S.Table.ChairTrs[0].position+=Vector3.right;p.S.Table.ChairTrs[1].position+=Vector3.left;
            var furniture=Get<FurnitureSystem>(p.S.Manager,"_furnitureSystem");
            var group=Get<Dictionary<ERestaurantFloorType,FurnitureGroup>>(furniture,"_furnitureGroupDic")[p.S.Table.FloorType];
            Set(group,"_tableDataList",new List<TableData>{p.S.Table});
            Invoke(p.S.Manager,"OnChangeTableEvent",p.S.Table.FloorType,FurnitureType.Table1);p.S.TakeWait(.01f)();
            foreach(var m in p.Party.Members)Assert.That(m.Customer.transform.position,Is.EqualTo(p.S.Table.GetSeatPosition(m.SeatIndex)));
            Assert.That(renderer.transform.localPosition,Is.EqualTo(local));Assert.That(renderer.gameObject.activeSelf,Is.True);
            Assert.That(Ledger(use),Is.EqualTo(ledger));Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            var buttons=new[]{p.S.Table.OrderButton,p.S.Table.CompanionOrderButton};
            for(int i=0;i<2;i++)Assert.That(Get<Transform>(Get<WorldToSceenPosition>(buttons[i],"_worldToSceenPos"),"_worldTransform"),Is.SameAs(p.S.Table.ChairTrs[i]));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void Boundary_EmptyPathOrUnreachedExit_NeverClaimsCompletion(bool exit)
    {
        if(!exit)
        {
            using(var s=new Flow())
            {
                Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var team),Is.True);
                foreach(var m in team.Members) { m.Customer.transform.position=m.Customer.TargetPos; Get<Action>(m.Customer,"_moveCompleted")(); }
                Get<Dictionary<string,Muks.PathFinding.AStar.MapData>>(Muks.PathFinding.AStar.AStar.Instance,"_maps").Clear();
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No available maps found for start position"));
                Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.False);
                Assert.That(team.IsActive,Is.False);Assert.That(s.Table.CurrentUse,Is.Null);
                Assert.That(s.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(s.Table.OccupantCount,Is.Zero);
                Assert.That(team.Members.All(m=>!m.Customer.gameObject.activeSelf&&m.Customer.VisitParty==null),Is.True);
                Assert.That(s.Manager.CancelPartyGuide(s.Table),Is.False);
            }
            return;
        }
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var use=p.S.Table.CurrentUse;var member=p.Party.Members[0];
            if(exit)
            {
                CustomerPartyCompletionTests.CookRound(p);foreach(var food in p.Batch.Orders.ToArray())CustomerPartyCompletionTests.Eat(p,food);
                var callback=Get<Action>(member.Customer,"_moveCompleted");member.Customer.transform.position=(Vector3)member.Customer.TargetPos+Vector3.right*100;
                callback();Assert.That(use.Completions[0].Returned,Is.False);Assert.That(p.S.Table.OccupantCount,Is.EqualTo(2));
                Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));Assert.That(use.SettlementCompleted,Is.False);
            }
            Assert.That(p.S.Manager.CancelPartyGuide(p.S.Table),Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
        }
    }

    private static void PrepareWaitingPair(Flow flow, out CustomerVisitParty team)
    {
        Assert.That(flow.Controller.TrySpawnWaitingParty(new[]{flow.Data.Id,flow.Data.Id},out team),Is.True);
        foreach(var member in team.Members)
        {
            member.Customer.transform.position=member.Customer.TargetPos;
            Get<Action>(member.Customer,"_moveCompleted")?.Invoke();
            member.Customer.StopMove();
        }
    }

    private static void SetMapWalkable(bool walkable)
    {
        var maps=Get<Dictionary<string,Muks.PathFinding.AStar.MapData>>(Muks.PathFinding.AStar.AStar.Instance,"_maps");
        foreach(var map in maps.Values) foreach(var node in map.Nodes) { node.IsWall=!walkable; node.IsGround=walkable; }
    }

    private static Muks.PathFinding.PathfindingQueue Paths => Muks.PathFinding.PathfindingQueue.Instance;
    private static System.Collections.Concurrent.ConcurrentQueue<Action> PathQueue =>
        Get<System.Collections.Concurrent.ConcurrentQueue<Action>>(Paths,"_pathfindingQueue");
    private static Queue<Action> DispatchQueue => Get<Queue<Action>>(Muks.PathFinding.MainThreadDispatcher.Instance,"_executionQueue");

    private static void DrainPathQueue()
    {
        while(PathQueue.TryDequeue(out var work)) work();
    }

    private static void ClearPendingPathWork()
    {
        while(PathQueue.TryDequeue(out _)) { }
        DispatchQueue.Clear();
    }

    private static void FailPendingPaths(int count)
    {
        for(int i=0;i<count;i++)
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No walkable node found near start position"));
        DrainPathQueue();
        Assert.That(DispatchQueue.Count,Is.EqualTo(count));
        SetMapWalkable(true);
        while(DispatchQueue.Count>0) DispatchQueue.Dequeue()();
    }

    private static TableUseIdentity CompletePairAndStartExit(Pair p)
    {
        ClearPendingPathWork();
        var use=p.S.Table.CurrentUse;
        CustomerPartyCompletionTests.CookRound(p);
        foreach(var food in p.Batch.Orders.ToArray()) CustomerPartyCompletionTests.Eat(p,food);
        Assert.That(use.CompletionStarted,Is.True);
        Assert.That(use.Completions.All(x=>x.ExitStarted),Is.True);
        return use;
    }

    private static void FinishPartyDelayedEffects(Pair p, TableUseIdentity use, bool tipsPaid)
    {
        if(!tipsPaid)
        {
            var first=p.S.TakeWait(.5f);first();first();
            var second=p.S.TakeWait(.5f);second();second();
        }
        int garbage=use.Completions.Sum(x=>x.GarbageCount);
        for(int i=0;i<garbage;i++)
        {
            var wait=p.S.TakeWait(i<2?.02f:.04f);wait();wait();
        }
    }

    [Test]
    public void EntryPathFailure_CancelsPartyAndLateMemberResultCannotCancelReplacement()
    {
        using(var s=new Flow())
        {
            PrepareWaitingPair(s,out var firstParty);
            while(PathQueue.TryDequeue(out _)) { }
            SetMapWalkable(false);
            Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.True);
            var oldUse=s.Table.CurrentUse;
            Assert.That(oldUse,Is.Not.Null);Assert.That(s.Table.OccupantCount,Is.EqualTo(2));
            Assert.That(PathQueue.Count,Is.EqualTo(2));
            for(int i=0;i<2;i++) UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No walkable node found near start position"));
            DrainPathQueue();
            Assert.That(DispatchQueue.Count,Is.EqualTo(2));
            DispatchQueue.Dequeue()();
            Assert.That(s.Table.CurrentUse,Is.Null);Assert.That(s.Table.TableState,Is.EqualTo(ETableState.Empty));
            Assert.That(s.Table.OccupantCount,Is.Zero);Assert.That(oldUse.Invalidated,Is.True);
            Assert.That(firstParty.IsActive,Is.False);Assert.That(firstParty.Members.All(m=>!m.Customer.gameObject.activeSelf&&m.Customer.VisitParty==null),Is.True);
            Assert.That(oldUse.OrderReservation,Is.Null);Assert.That(oldUse.Orders,Is.Null);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);Assert.That(UserInfo.TotalCookCount,Is.Zero);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id),Is.Zero);Assert.That(s.Stage.Tip,Is.Zero);
            Assert.That(s.Stage.Satisfaction,Is.Zero);Assert.That(s.Coins.CurrentMoney,Is.Zero);

            SetMapWalkable(true);
            PrepareWaitingPair(s,out var nextParty);
            while(PathQueue.TryDequeue(out _)) { }
            Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.True);
            var nextUse=s.Table.CurrentUse;
            Assert.That(nextUse,Is.Not.Null);Assert.That(nextUse,Is.Not.SameAs(oldUse));
            Assert.That(s.Table.CurrentParty,Is.SameAs(nextParty));Assert.That(s.Table.OccupantCount,Is.EqualTo(2));
            DispatchQueue.Dequeue()();
            Assert.That(s.Table.CurrentUse,Is.SameAs(nextUse));Assert.That(nextParty.IsActive,Is.True);
            Assert.That(s.Table.TableState,Is.EqualTo(ETableState.Move));
            Assert.That(PathQueue.Count,Is.EqualTo(2));
            foreach(var member in nextParty.Members)
            {
                member.Customer.transform.position=member.Customer.TargetPos;
                Get<Action>(member.Customer,"_moveCompleted")();
                Assert.That(nextUse.HasArrived(member),Is.True);
                Assert.That(member.Customer.transform.position,Is.EqualTo(s.Table.GetSeatPosition(member.SeatIndex)));
            }
            Assert.That(nextUse.AllSeated,Is.False,"The ordinary seating delay still gates party order preparation.");
            Assert.That(s.Manager.CancelPartyGuide(s.Table),Is.True);
            while(DispatchQueue.Count>0)DispatchQueue.Dequeue()();
        }
    }

    [Test]
    public void EntryPathFailureAfterFirstMemberArrives_CancelsWholePartyWithoutVisitOrReward()
    {
        using(var s=new Flow())
        {
            PrepareWaitingPair(s,out var team);
            while(PathQueue.TryDequeue(out _)) { }
            SetMapWalkable(true);
            Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.True);
            var use=s.Table.CurrentUse;
            Assert.That(PathQueue.Count,Is.EqualTo(2));
            Assert.That(PathQueue.TryDequeue(out var firstPath),Is.True);firstPath();
            var first=team.Members[0];first.Customer.transform.position=first.Customer.TargetPos;
            Get<Action>(first.Customer,"_moveCompleted")();
            Assert.That(use.HasArrived(first),Is.True);Assert.That(use.HasArrived(team.Members[1]),Is.False);
            SetMapWalkable(false);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No walkable node found near start position"));
            Assert.That(PathQueue.TryDequeue(out var secondPath),Is.True);secondPath();
            Assert.That(DispatchQueue.Count,Is.EqualTo(2));
            DispatchQueue.Dequeue()();
            Assert.That(s.Table.CurrentUse,Is.SameAs(use));
            DispatchQueue.Dequeue()();
            Assert.That(s.Table.CurrentUse,Is.Null);Assert.That(team.IsActive,Is.False);
            Assert.That(team.Members.All(m=>!m.Customer.gameObject.activeSelf&&m.Customer.VisitParty==null),Is.True);
            Assert.That(s.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(s.Table.OccupantCount,Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id),Is.Zero);
            Assert.That(UserInfo.TotalCookCount,Is.Zero);Assert.That(s.Stage.Tip,Is.Zero);Assert.That(s.Stage.Satisfaction,Is.Zero);
            Assert.That(s.Coins.CurrentMoney,Is.Zero);
        }
    }

    [Test]
    public void CompletedParty_FirstExitPathFails_SecondExitContinuesAndReleasesOnce()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var use=CompletePairAndStartExit(p);
            var first=use.Completions[0];var second=use.Completions[1];
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No walkable node found near start position"));
            Assert.That(PathQueue.TryDequeue(out var firstPath),Is.True);SetMapWalkable(false);firstPath();
            Assert.That(DispatchQueue.Count,Is.EqualTo(1));
            Assert.That(PathQueue.TryDequeue(out var secondPath),Is.True);SetMapWalkable(true);secondPath();
            Assert.That(DispatchQueue.Count,Is.EqualTo(2));

            DispatchQueue.Dequeue()();
            Assert.That(first.ExitFailureResolved,Is.True);Assert.That(first.Returned,Is.False);
            Assert.That(use.SettlementCompleted,Is.False);Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            Assert.That(second.ExitFailureResolved||second.Returned,Is.False);
            Assert.That(second.Member.Customer.gameObject.activeSelf,Is.True);

            DispatchQueue.Dequeue()();
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            var secondCustomer=second.Member.Customer;var completion=Get<Action>(secondCustomer,"_moveCompleted");
            secondCustomer.transform.position=secondCustomer.TargetPos;completion();
            Assert.That(second.Returned,Is.True);Assert.That(second.ExitFailureResolved,Is.False);
            Assert.That(second.Member.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.ReachedExit));
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(first.Member.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.TechnicalCleanup));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));

            FinishPartyDelayedEffects(p,use,false);
            Assert.That(p.S.Stage.Tip,Is.EqualTo(100));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(use.Completions.Sum(x=>x.GarbageCount)));
            SetMapWalkable(true);ClearPendingPathWork();
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CompletedMealServiceAbort_BothExitPathsFail_PreservesOnlyCompletedMember(bool tipAlreadyPaid)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            ClearPendingPathWork();CustomerPartyCompletionTests.CookRound(p);
            var use=p.S.Table.CurrentUse;var foods=p.Batch.Orders.ToArray();
            CustomerPartyCompletionTests.Eat(p,foods[0]);
            var completed=use.Completions[0];var unfinished=use.Completions[1];
            if(tipAlreadyPaid)p.S.TakeWait(.5f)();
            Assert.That(p.S.Manager.AbortPartyService(use,"FoodWaitExpired"),Is.True);
            Assert.That(use.ServiceAborted&&use.CompletionStarted,Is.True);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.S.Data.Id),Is.EqualTo(1));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id),Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            Assert.That(completed.CompletedPrice,Is.EqualTo(101));Assert.That(completed.CoinDropped,Is.True);
            Assert.That(unfinished.MealsCompleted,Is.Zero);Assert.That(unfinished.CompletedPrice,Is.Zero);
            Assert.That(unfinished.VisitCounted||unfinished.CustomerCounted||unfinished.CoinDropped,Is.False);
            Assert.That(PathQueue.Count,Is.EqualTo(2));

            var callbacks=use.Completions.Select(x=>Get<Action>(x.Member.Customer,"_moveCompleted")).ToArray();
            SetMapWalkable(false);FailPendingPaths(2);
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(use.Completions.All(x=>!x.Returned&&x.ExitFailureResolved),Is.True);
            Assert.That(p.Party.Members.All(x=>x.DepartureResolution==CustomerVisitDepartureResolution.TechnicalCleanup),Is.True);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.S.Data.Id),Is.EqualTo(1));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id),Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            Assert.That(p.S.Stage.FeverGauge,Is.Zero);Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(1));
            foreach(var callback in callbacks)callback?.Invoke();
            if(!tipAlreadyPaid)p.S.TakeWait(.5f)();
            for(int i=0;i<completed.GarbageCount;i++){var wait=p.S.TakeWait(.02f*(i+1));wait();wait();}
            Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(completed.TipPaid,Is.True);
            Assert.That(completed.GarbageDropped,Is.EqualTo(completed.GarbageCount));
            Assert.That(unfinished.GarbageCount,Is.Zero);Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(completed.GarbageCount));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));
            SetMapWalkable(true);ClearPendingPathWork();
        }
    }

    [Test]
    public void FinalFurnitureRemoval_BothExitPathsFail_PreservesDontUseAndNoMealRewards()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            ClearPendingPathWork();var use=p.S.Table.CurrentUse;
            p.S.Table.TableType=TableType.Table2;
            var edit=p.S.Manager.BeginFurnitureEdit();p.S.Manager.NotFurnitureTable(p.S.Table);
            p.S.Manager.CompleteFurnitureEdit(edit);
            Assert.That(use.ServiceAborted&&use.FurnitureRemoved&&use.CompletionStarted,Is.True);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.DontUse));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(PathQueue.Count,Is.EqualTo(2));
            SetMapWalkable(false);FailPendingPaths(2);
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.DontUse));
            Assert.That(use.Completions.All(x=>!x.Returned&&x.ExitFailureResolved),Is.True);
            Assert.That(p.Party.Members.All(x=>x.DepartureResolution==CustomerVisitDepartureResolution.TechnicalCleanup),Is.True);
            Assert.That(p.S.Table.OccupantCount,Is.Zero);Assert.That(p.S.Coins.CurrentMoney,Is.Zero);
            Assert.That(p.S.Stage.Tip,Is.Zero);Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            SetMapWalkable(true);ClearPendingPathWork();
        }
    }

    [Test]
    public void SingleEntryPathFailure_DoesNotEnterPartyAutoCancelPolicy()
    {
        using(var s=new Flow())
        {
            var arrival=s.Guide();var use=s.Table.CurrentUse;
            Assert.That(PathQueue.TryDequeue(out var path),Is.True);
            SetMapWalkable(false);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("No walkable node found near start position"));
            path();
            Assert.That(DispatchQueue.Count,Is.EqualTo(1));DispatchQueue.Dequeue()();
            Assert.That(s.Table.CurrentUse,Is.SameAs(use));Assert.That(s.Table.TableState,Is.EqualTo(ETableState.Move));
            Assert.That(s.Customer.HasPathFailure,Is.True);Assert.That(arrival,Is.Not.Null);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CompletedParty_BothExitPathsFail_TechnicalCleanupPreservesDelayedRewards(bool tipsPaid)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var use=CompletePairAndStartExit(p);
            if(tipsPaid)
            {
                p.S.TakeWait(.5f)();p.S.TakeWait(.5f)();
                Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
            }
            var callbacks=use.Completions.Select(x=>Get<Action>(x.Member.Customer,"_moveCompleted")).ToArray();
            Assert.That(PathQueue.Count,Is.EqualTo(2));
            SetMapWalkable(false);
            FailPendingPaths(2);

            Assert.That(use.SettlementCompleted,Is.True);
            Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(use.Completions.All(x=>!x.Returned&&x.ExitFailureResolved),Is.True);
            Assert.That(use.Completions.All(x=>x.ExitFailureReason=="AStarPathUnavailable"),Is.True);
            Assert.That(p.Party.Members.All(x=>x.DepartureResolution==CustomerVisitDepartureResolution.TechnicalCleanup),Is.True);
            Assert.That(p.Party.Members.All(x=>!x.Customer.gameObject.activeSelf&&x.Customer.VisitParty==null),Is.True);
            Assert.That(use.Completions.Select(x=>x.CompletedPrice),Is.EqualTo(new[]{101,101}));
            Assert.That(use.Completions.Select(x=>x.CoinAmount),Is.EqualTo(new[]{101,101}));
            Assert.That(use.Completions.All(x=>x.VisitCounted&&x.CustomerCounted&&x.CoinDropped&&x.SatisfactionApplied&&x.FeverApplied),Is.True);
            Assert.That(UserInfo.GetVisitedCustomerCount(p.S.Data.Id),Is.EqualTo(1));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id),Is.EqualTo(1));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(4));
            Assert.That(p.S.Stage.FeverGauge,Is.EqualTo(40));
            Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(1));

            foreach(var callback in callbacks) callback?.Invoke();
            FinishPartyDelayedEffects(p,use,tipsPaid);
            Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
            Assert.That(use.Completions.All(x=>x.TipPaid&&x.GarbageDropped==x.GarbageCount),Is.True);
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(use.Completions.Sum(x=>x.GarbageCount)));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(4));
            Assert.That(p.S.Stage.FeverGauge,Is.EqualTo(40));

            p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id,p.SecondData.Id},out var next),Is.True);
            foreach(var member in next.Members)
            {
                member.Customer.transform.position=member.Customer.TargetPos;
                Get<Action>(member.Customer,"_moveCompleted")?.Invoke();
                member.Customer.StopMove();
            }
            ClearPendingPathWork();
            Assert.That(p.S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1),Is.True);
            var nextUse=p.S.Table.CurrentUse;
            Assert.That(nextUse,Is.Not.Null);Assert.That(nextUse,Is.Not.SameAs(use));
            foreach(var callback in callbacks) callback?.Invoke();
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(nextUse));
            Assert.That(next.IsActive,Is.True);
            Assert.That(p.S.Manager.CancelPartyGuide(p.S.Table),Is.True);
            ClearPendingPathWork();SetMapWalkable(true);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CompletedParty_FirstMemberExitsSecondPathFails_NewUseIgnoresOldCallback(bool tipsPaid)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var use=CompletePairAndStartExit(p);
            if(tipsPaid){p.S.TakeWait(.5f)();p.S.TakeWait(.5f)();}
            var failedCustomer=use.Completions[1].Member.Customer;
            var staleCompletion=Get<Action>(failedCustomer,"_moveCompleted");

            p.ArriveExit(0);
            Assert.That(use.Completions[0].Returned,Is.True);
            Assert.That(use.Completions[0].ExitFailureResolved,Is.False);
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            Assert.That(failedCustomer.gameObject.activeSelf,Is.True);
            Assert.That(PathQueue.TryDequeue(out _),Is.True,"Discard the already-completed member's queued path calculation.");
            Assert.That(PathQueue.Count,Is.EqualTo(1));
            SetMapWalkable(false);FailPendingPaths(1);

            Assert.That(use.Completions[0].Returned,Is.True);
            Assert.That(use.Completions[0].Member.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.ReachedExit));
            Assert.That(use.Completions[1].Returned,Is.False);
            Assert.That(use.Completions[1].ExitFailureResolved,Is.True);
            Assert.That(use.Completions[1].Member.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.TechnicalCleanup));
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            FinishPartyDelayedEffects(p,use,tipsPaid);
            Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(use.Completions.Sum(x=>x.GarbageCount)));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.S.Data.Id),Is.EqualTo(1));
            Assert.That(UserInfo.GetVisitedCustomerCount(p.SecondData.Id),Is.EqualTo(1));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));

            p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id,p.SecondData.Id},out var next),Is.True);
            foreach(var member in next.Members)
            {
                member.Customer.transform.position=member.Customer.TargetPos;
                Get<Action>(member.Customer,"_moveCompleted")?.Invoke();
                member.Customer.StopMove();
            }
            ClearPendingPathWork();
            Assert.That(next.Members.Any(x=>ReferenceEquals(x.Customer,failedCustomer)),Is.True);
            Assert.That(p.S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1),Is.True);
            var nextUse=p.S.Table.CurrentUse;
            staleCompletion?.Invoke();
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(nextUse));
            Assert.That(next.IsActive,Is.True);
            Assert.That(p.S.Manager.CancelPartyGuide(p.S.Table),Is.True);
            ClearPendingPathWork();SetMapWalkable(true);
        }
    }

    [Test]
    public void CompletedParty_UnreachedCompletionCallbackIsTechnicalAndDoesNotClaimPhysicalExit()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var use=CompletePairAndStartExit(p);
            ClearPendingPathWork();
            var first=use.Completions[0];
            var callback=Get<Action>(first.Member.Customer,"_moveCompleted");
            first.Member.Customer.transform.position=(Vector3)first.Member.Customer.TargetPos+Vector3.right*100;
            callback();
            Assert.That(first.Member.Customer.HasPathFailure,Is.True);
            Assert.That(first.Returned,Is.False);Assert.That(first.ExitFailureResolved,Is.True);
            Assert.That(first.ExitFailureReason,Is.EqualTo("DestinationNotReached"));
            Assert.That(first.Member.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.TechnicalCleanup));
            Assert.That(use.SettlementCompleted,Is.False);
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            Assert.That(use.Completions[1].Member.Customer.gameObject.activeSelf,Is.True);

            p.ArriveExit(1);
            Assert.That(use.Completions[1].Returned,Is.True);
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            FinishPartyDelayedEffects(p,use,false);
            Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
            ClearPendingPathWork();SetMapWalkable(true);
        }
    }

    [Test]
    public void WaitingParty_BothExitPathsFail_RemovesReservationAndEndsTeamWithoutRewards()
    {
        using(var s=new Flow())
        {
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var party),Is.True);
            foreach(var member in party.Members)
            {
                member.Customer.transform.position=member.Customer.TargetPos;
                Get<Action>(member.Customer,"_moveCompleted")();
            }
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out var started,out var limit),Is.True);
            ClearPendingPathWork();
            s.Controller.TickPartyClocks(started+limit+1);
            Assert.That(party.WaitingDeparture,Is.True);
            Assert.That(s.Controller.Count,Is.Zero);Assert.That(s.Controller.WaitCount,Is.Zero);
            Assert.That(PathQueue.Count,Is.EqualTo(2));
            SetMapWalkable(false);FailPendingPaths(2);

            Assert.That(party.IsActive,Is.False);
            Assert.That(party.Members.All(x=>x.DepartureResolution==CustomerVisitDepartureResolution.TechnicalCleanup),Is.True);
            Assert.That(party.Members.All(x=>!x.Customer.gameObject.activeSelf&&x.Customer.VisitParty==null),Is.True);
            Assert.That(s.Controller.Count,Is.Zero);Assert.That(s.Controller.WaitingPersonCount,Is.Zero);
            Assert.That(s.Controller.WaitCount,Is.Zero);Assert.That(s.Stage.WaitingParties.Teams,Is.Empty);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id),Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);
            Assert.That(s.Stage.Tip,Is.Zero);Assert.That(s.Stage.Satisfaction,Is.Zero);Assert.That(s.Coins.CurrentMoney,Is.Zero);
            SetMapWalkable(true);ClearPendingPathWork();
        }
    }

    [Test]
    public void WaitingParty_OnePhysicalExitAndOnePathFailureEndsOnce_StaleCallbackCannotTouchNextTeam()
    {
        using(var s=new Flow())
        {
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var party),Is.True);
            foreach(var member in party.Members)
            {
                member.Customer.transform.position=member.Customer.TargetPos;
                Get<Action>(member.Customer,"_moveCompleted")();
            }
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out var started,out var limit),Is.True);
            ClearPendingPathWork();
            s.Controller.TickPartyClocks(started+limit+1);
            var physicallyExited=party.Members[0];var failed=party.Members[1];
            var stale=Get<Action>(failed.Customer,"_moveCompleted");
            physicallyExited.Customer.transform.position=physicallyExited.Customer.TargetPos;
            Get<Action>(physicallyExited.Customer,"_moveCompleted")();
            Assert.That(physicallyExited.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.ReachedExit));
            Assert.That(failed.Customer.gameObject.activeSelf,Is.True);
            Assert.That(PathQueue.TryDequeue(out _),Is.True);
            Assert.That(PathQueue.Count,Is.EqualTo(1));
            SetMapWalkable(false);FailPendingPaths(1);

            Assert.That(failed.DepartureResolution,Is.EqualTo(CustomerVisitDepartureResolution.TechnicalCleanup));
            Assert.That(party.IsActive,Is.False);
            Assert.That(s.Controller.Count,Is.Zero);Assert.That(s.Controller.WaitingPersonCount,Is.Zero);
            Assert.That(s.Controller.WaitCount,Is.Zero);Assert.That(s.Stage.WaitingParties.Teams,Is.Empty);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id),Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);
            Assert.That(s.Stage.Tip,Is.Zero);Assert.That(s.Stage.Satisfaction,Is.Zero);Assert.That(s.Coins.CurrentMoney,Is.Zero);

            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var next),Is.True);
            Assert.That(next.Members.Any(x=>ReferenceEquals(x.Customer,failed.Customer)),Is.True);
            stale?.Invoke();
            Assert.That(next.IsActive,Is.True);Assert.That(s.Controller.Count,Is.EqualTo(1));
            Assert.That(s.Controller.WaitingPersonCount,Is.EqualTo(2));
            Assert.That(s.Controller.WaitingParties.Single(),Is.SameAs(next));
            Assert.That(next.Members.All(x=>x.DepartureResolution==CustomerVisitDepartureResolution.None),Is.True);
            Assert.That(s.Controller.CancelWaitingParty(next),Is.True);
            SetMapWalkable(true);ClearPendingPathWork();
        }
    }
}
#endif
