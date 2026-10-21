#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using static CustomerPartyCompletionTests;
using Pair = CustomerPartyOrderTests.Pair;

// Explicit clock/callback advancement proves transactions. Real PlayerLoop videos
// are produced separately by the owned Stage1 preview.
public sealed class CustomerPartyInterruptionTests
{
    private static Dictionary<string,int> Owned => (Dictionary<string,int>)typeof(UserInfo)
        .GetField("_giveRecipeLevelDic",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    private static void Arrive(NormalCustomer c) { c.transform.position=c.TargetPos; Get<Action>(c,"_moveCompleted")(); }
    private static CustomerVisitParty Waiting(Flow s)
    {
        Set(s.Data,"_wiatTime",40);
        var b=s.Track(UnityEngine.Object.Instantiate(s.Data)); Set(b,"_id","LONG-WAIT");Set(b,"_wiatTime",70);
        Get<Dictionary<string,CustomerData>>(CustomerDataManager.Instance,"_customerDataDic")[b.Id]=b;
        ((Dictionary<string,SaveCustomerData>)typeof(UserInfo).GetField("_enabledCustomerDic",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))[b.Id]=new SaveCustomerData(b.Id,"",0);
        Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,b.Id},out var party),Is.True);
        return party;
    }
    private static Pair Timed(int a=1,int b=1,bool invalid=false) => new Pair(ordersA:a,ordersB:b,invalidMenu:invalid,beforeGuide:p=>{
        Set(p.S.Data,"_orderFoodTime",20);Set(p.SecondData,"_orderFoodTime",35);
    });
    private static void Expire(Pair p) => p.S.Controller.TickPartyClocks(p.Batch.Orders[0].PreparedAt+35);
    private static void Garbage(Pair p)
    {
        var waits=Get<GameObject>(p.S,"waits").GetComponentsInChildren<Muks.Tween.TweenWait>(true)
            .Where(w=>w.enabled&&Get<float>(w,"_totalDuration")<.1f).ToArray();
        foreach(var w in waits){var done=Get<Action>(w,"_onCompleted");w.Stop();done();done();}
    }

    [TestCase(false)] [TestCase(true)]
    public void QueueClock_MaxStartsAfterBoth_AndNeverRestartsOnReorder(bool reverse)
    {
        using(var s=new Flow())
        {
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id},out var front),Is.True);
            var party=Waiting(s);int first=reverse?1:0;
            s.Controller.TickPartyClocks(Time.timeAsDouble+1000);
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out _,out _),Is.False);
            Arrive(party.Members[first].Customer);s.Controller.TickPartyClocks(Time.timeAsDouble+1000);
            Assert.That(party.WaitingDeparture,Is.False);
            Arrive(party.Members[1-first].Customer);
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out var start,out var limit),Is.True);Assert.That(limit,Is.EqualTo(70));
            s.Controller.TickPartyClocks(start+40);Assert.That(party.WaitingDeparture,Is.False);
            s.Controller.CancelWaitingParty(front);
            Assert.That(s.Controller.WaitCount,Is.Zero);
            foreach(var m in party.Members){Arrive(m.Customer);Get<Action>(m.Customer,"_moveCompleted")();}
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out var unchanged,out _),Is.True);Assert.That(unchanged,Is.EqualTo(start));
            s.Controller.TickPartyClocks(start);s.Controller.TickPartyClocks(start); // paused scaled time
            s.Controller.TickPartyClocks(start+69.99);Assert.That(s.Controller.Count,Is.EqualTo(1));
            s.Controller.TickPartyClocks(start+70);s.Controller.TickPartyClocks(start+100);
            Assert.That(s.Controller.Count,Is.Zero);Assert.That(party.WaitingDeparture,Is.True);
            Assert.That(s.Stage.WaitingParties.Teams,Is.Empty);Assert.That(party.Members.All(m=>m.Customer.gameObject.activeSelf),Is.True);
            Assert.That(s.Stage.Satisfaction,Is.Zero);Assert.That(s.Stage.Tip,Is.Zero);Assert.That(s.Coins.CurrentMoney,Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);Assert.That(s.Table.DropGarbageArea.Count,Is.Zero);
            Arrive(party.Members[first].Customer);Assert.That(party.IsActive,Is.True);
            Assert.That(party.Members[1-first].Customer.gameObject.activeSelf,Is.True);
            Arrive(party.Members[1-first].Customer);Assert.That(party.IsActive,Is.False);
            Assert.That(s.Stage.GetSinkBowlCount(s.Table.FloorType),Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void QueueGuideAndExpiry_FirstTransitionWins(bool expiryFirst)
    {
        using(var s=new Flow())
        {
            var party=Waiting(s);foreach(var m in party.Members)Arrive(m.Customer);
            s.Controller.TryGetPartyWaitingClock(party,out var start,out _);
            if(expiryFirst)s.Controller.TickPartyClocks(start+70);
            Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.EqualTo(!expiryFirst));
            s.Controller.TickPartyClocks(start+70);
            Assert.That(party.WaitingDeparture,Is.EqualTo(expiryFirst));Assert.That(s.Controller.Count,Is.Zero);
            Assert.That(s.Table.CurrentUse==null,Is.EqualTo(expiryFirst));Assert.That(s.Stage.Satisfaction,Is.Zero);
        }
    }

    [TestCase("scene")] [TestCase("account")]
    public void DetachedWaitingExit_ContextCleanupReturnsSurvivorAndPreservesReusedObject(string boundary)
    {
        using(var s=new Flow())
        {
            var party=Waiting(s);foreach(var m in party.Members)Arrive(m.Customer);
            s.Controller.TryGetPartyWaitingClock(party,out var start,out _);s.Controller.TickPartyClocks(start+70);
            var old=Get<Action>(party.Members[0].Customer,"_moveCompleted");Arrive(party.Members[0].Customer);
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id},out var next),Is.True);
            old();Assert.That(next.IsActive,Is.True);Assert.That(s.Controller.Count,Is.EqualTo(1));
            if(boundary=="scene")Invoke(s.Manager,"InvalidateExecution");else s.InstallAccount();
            s.Controller.TickPartyClocks(Time.timeAsDouble);
            Assert.That(party.Members[1].Customer.gameObject.activeSelf,Is.False);Assert.That(party.IsActive,Is.False);
            Assert.That(s.Stage.Satisfaction,Is.Zero);
        }
    }

    [Test]
    public void UnreachedQueueCallback_CancelsTechnicallyWithoutClockOrTimeoutPenalty()
    {
        using(var s=new Flow())
        {
            var party=Waiting(s);
            var callbacks=party.Members.Select(m=>Get<Action>(m.Customer,"_moveCompleted")).ToArray();
            party.Members[0].Customer.transform.position=(Vector3)party.Members[0].Customer.TargetPos+Vector3.right*100;
            foreach(var callback in callbacks) { callback(); callback(); }
            s.Controller.TickPartyClocks(Time.timeAsDouble+1000);
            Assert.That(s.Controller.TryGetPartyWaitingClock(party,out _,out _),Is.False);Assert.That(s.Controller.WaitCount,Is.Zero);
            Assert.That(s.Controller.Count,Is.Zero);Assert.That(s.Stage.Satisfaction,Is.Zero);
            Assert.That(party.IsActive,Is.False);
            Assert.That(party.Members.All(m=>!m.Customer.gameObject.activeSelf&&m.Customer.VisitMember==null),Is.True);
            Assert.That(s.Stage.WaitingParties.Teams,Is.Empty);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id),Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);
            Assert.That(s.Stage.Tip,Is.Zero);Assert.That(s.Stage.FeverGauge,Is.Zero);Assert.That(s.Coins.CurrentMoney,Is.Zero);
        }
    }

    [TestCase("prepared")] [TestCase("queued")] [TestCase("cooking")] [TestCase("ready")] [TestCase("carrying")]
    public void FoodClock_AllWaitingStagesExpireOnce_AndCancelledStaffCannotReviveFood(string point)
    {
        using(var p=Timed())
        {
            var use=p.S.Table.CurrentUse;var foods=p.Batch.Orders.ToArray();var start=foods[0].PreparedAt;
            Assert.That(use.FoodWaitLimit,Is.EqualTo(35));Assert.That(foods.All(f=>f.WaitClockStarted),Is.True);
            p.S.Controller.TickPartyClocks(start+20);Assert.That(use.ServiceAborted,Is.False);
            if(point!="prepared")p.Accept();
            if(point=="cooking")p.Pump();
            if(point=="ready"||point=="carrying"){p.Pump();p.Pump();p.Pump();}
            TableServiceReservation lease=null;WaiterAction worker=null;
            if(point=="carrying")
            {
                var w=p.S.CreateWaiter();worker=w.action;lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
                Assert.That(p.S.Manager.PickUpWaiterFood(lease),Is.True);
            }
            int cooked=UserInfo.TotalCookCount;long money=UserInfo.Money;
            p.S.Controller.TickPartyClocks(start+34.99);Assert.That(use.ServiceAborted,Is.False);
            Expire(p);Expire(p);worker?.Destructor();lease?.Release();
            Assert.That(use.ServiceAborted&&use.CompletionStarted,Is.True);Assert.That(use.PolicyPending,Is.False);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            Assert.That(foods.All(f=>f.State==PartyFoodState.Cancelled&&!f.IsWaitingForDelivery),Is.True);
            Assert.That(foods.All(f=>f.PreparedAt==start),Is.True);Assert.That(p.S.Queue,Is.Empty);Assert.That(p.Burner.CookingData.IsDefault(),Is.True);
            foreach(var food in foods){food.Cooking.OnCompleted();Invoke(p.S.Manager,"CompletePartyMeal",food);Assert.That(p.S.Manager.TryServePartyOrder(food),Is.False);}
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(cooked));Assert.That(UserInfo.Money,Is.EqualTo(money));
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero);Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);Assert.That(use.Completions.All(r=>!r.FinalCompleted),Is.True);
            p.ArriveExit(0);p.ArriveExit(1);Assert.That(p.S.Table.CurrentUse,Is.Null);Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void DeliveryExpiryRace_DeliveredFoodSurvives_PreDeliveryFoodCannotCommitAfterAbort(bool expiryFirst)
    {
        using(var p=Timed())
        {
            CookRound(p);var food=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;
            if(expiryFirst)Expire(p);
            Assert.That(p.S.Manager.TryServePartyOrder(food),Is.EqualTo(!expiryFirst));p.S.Manager.StopAllCoroutines();
            Expire(p);
            Assert.That(food.Identity.ServingStarted,Is.EqualTo(!expiryFirst));Assert.That(food.IsWaitingForDelivery,Is.False);
            Assert.That(use.CompletionStarted,Is.EqualTo(expiryFirst));
            if(!expiryFirst){Finish(p,food);Assert.That(food.CompletionRecorded,Is.True);Assert.That(use.CompletionStarted,Is.True);}
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [TestCase(1,false)] [TestCase(1,true)] [TestCase(2,false)] [TestCase(2,true)]
    public void CurrentDeliveredMealFinishesOnlyItself_AndOnlyOriginalLastMealGrantsPersonalFinal(int aOrders,bool eatingStarted)
    {
        using(var p=Timed(aOrders,2))
        {
            CookRound(p);var food=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;long money=UserInfo.Money;
            Assert.That(p.S.Manager.TryServePartyOrder(food),Is.True);p.S.Manager.StopAllCoroutines();
            var routine=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",food);routine.MoveNext();
            if(eatingStarted)routine.MoveNext();
            Expire(p);Assert.That(use.CompletionStarted,Is.False);Assert.That(food.State,Is.EqualTo(PartyFoodState.Eating));
            Assert.That(use.GetCompletion(food.Member).CompletedPrice,Is.Zero);
            while(routine.MoveNext()){}
            Assert.That(use.CompletionStarted,Is.True);Assert.That(use.Round,Is.EqualTo(1));
            var a=use.Completions[0];Assert.That(a.CompletedPrice,Is.EqualTo(101));Assert.That(a.FinalCompleted,Is.EqualTo(aOrders==1));
            Assert.That(a.RemainingOrders,Is.EqualTo(aOrders-1));Assert.That(use.Completions[1].CompletedPrice,Is.Zero);
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));Assert.That(UserInfo.Money,Is.EqualTo(money));
            if(aOrders==1){p.S.TakeWait(.5f)();Assert.That(p.S.Stage.Tip,Is.EqualTo(50));}else Assert.That(p.S.Stage.Tip,Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(aOrders==1?1:0));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            Garbage(p);Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(a.GarbageCount));Assert.That(a.GarbageCount,Is.InRange(1,2));
            Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            Invoke(p.S.Manager,"CompletePartyMeal",food);p.S.Manager.BeginPartyDeparture(use);p.S.Manager.AbortPartyService(use,"repeat");
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            p.S.Table.TableFurniture.OnCleanAction();p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            p.ArriveExit(1);p.ArriveExit(0);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void FinalA_PendingOrPaidTipSurvivesAbortAndPoolReturn_FixedAmountDoesNotRevalue(bool paid)
    {
        using(var p=Timed(1,2))
        {
            CookRound(p);var foods=p.Batch.Orders.ToArray();var use=p.S.Table.CurrentUse;Eat(p,foods[0]);
            if(paid)p.S.TakeWait(.5f)();
            Assert.That(GameManager.Instance.FeverRuntimeContext.TryActivate(10,out var token),Is.True);
            Eat(p,foods[1]);Owned.Remove(p.SecondFood.Id);
            Assert.That(p.Batch.Orders.Single().Member,Is.SameAs(foods[1].Member));Assert.That(use.FoodWaitLimit,Is.EqualTo(35));
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.ServiceAborted));
            Assert.That(use.Completions[0].CoinAmount,Is.EqualTo(101));Assert.That(use.Completions[1].CoinAmount,Is.EqualTo(202));
            Assert.That(use.Completions[1].CompletedPrice,Is.EqualTo(101));Assert.That(use.Completions[1].TotalPrice,Is.EqualTo(202));
            p.ArriveExit(0);Assert.That(use.IsCompletionContextCurrent,Is.True);p.ArriveExit(1);
            if(!paid)p.S.TakeWait(.5f)();Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(303));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            GameManager.Instance.FeverRuntimeContext.Deactivate(token);
        }
    }

    [Test]
    public void SingleAdditionalParticipantRetainsOriginalLongLimit_AndOldOrderCannotExpireOrRecordAgain()
    {
        using(var p=Timed(2,1))
        {
            CookRound(p);var old=p.Batch.Orders.ToArray();foreach(var f in old)Eat(p,f);
            var use=p.S.Table.CurrentUse;var next=p.Batch.Orders.Single();Assert.That(next.Member,Is.SameAs(old[0].Member));
            Assert.That(use.FoodWaitLimit,Is.EqualTo(35));Assert.That(old.All(o=>!o.IsWaitingForDelivery),Is.True);
            p.S.Controller.TickPartyClocks(next.PreparedAt+20);Assert.That(use.ServiceAborted,Is.False);
            foreach(var f in old)Invoke(p.S.Manager,"CompletePartyMeal",f);
            Expire(p);Assert.That(use.Completions.Select(r=>r.CompletedPrice),Is.EqualTo(new[]{101,101}));
            Assert.That(use.Completions[0].FinalCompleted,Is.False);Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
        }
    }

    [Test]
    public void SameMenuAcrossRounds_RecordsEachOrderOnce_AndFinalPartnerHasNoFoodClock()
    {
        using(var p=new Pair(same:true,ordersA:3,ordersB:1))
        {
            var use=p.S.Table.CurrentUse;CookRound(p);var first=p.Batch.Orders.ToArray();foreach(var food in first)Eat(p,food);
            CookRound(p);var second=p.Batch.Orders.Single();Eat(p,second);
            Assert.That(second.Food.Id,Is.EqualTo(first[0].Food.Id));Assert.That(second.Identity,Is.Not.SameAs(first[0].Identity));
            Invoke(p.S.Manager,"CompletePartyMeal",second);Invoke(p.S.Manager,"CompletePartyMeal",first[0]);
            p.S.Manager.AbortPartyService(use,"FoodWaitExpired");
            Assert.That(use.Completions.Select(r=>r.CompletedPrice),Is.EqualTo(new[]{202,101}));
            Assert.That(use.Completions.Select(r=>r.MealsCompleted),Is.EqualTo(new[]{2,1}));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(303));
        }
    }

    [Test]
    public void StaffRecoveryAndAcceptanceRetry_KeepOriginalClock()
    {
        using(var p=Timed())
        {
            var food=p.Batch.Orders[0];double start=food.PreparedAt;CookRound(p);
            var w=p.S.CreateWaiter();var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
            Assert.That(p.S.Manager.PickUpWaiterFood(lease),Is.True);lease.Release();w.action.Destructor();
            Assert.That(food.State,Is.EqualTo(PartyFoodState.ReadyForServing));Assert.That(food.PreparedAt,Is.EqualTo(start));
            p.Accept();p.S.Table.RefreshPartyOrderButtons();Assert.That(food.PreparedAt,Is.EqualTo(start));
            p.S.Controller.TickPartyClocks(start+35);Assert.That(p.S.Table.CurrentUse.ServiceAborted,Is.True);
        }
    }

    [TestCase("menu")] [TestCase("scene")] [TestCase("account")]
    public void TechnicalFailureAndExecutionCleanup_DoNotApplyServicePenalty(string kind)
    {
        using(var p=Timed(invalid:kind=="menu"))
        {
            var use=p.S.Table.CurrentUse;
            if(kind=="scene")Invoke(p.S.Manager,"InvalidateExecution");
            if(kind=="account")p.S.InstallAccount();
            p.S.Controller.TickPartyClocks(Time.timeAsDouble+10000);
            Assert.That(use.ServiceAborted,Is.False);Assert.That(p.S.Stage.Satisfaction,Is.Zero);
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero);Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.Zero);
            if(kind=="menu")Assert.That(use.Orders.Orders,Is.Empty);
        }
    }

    [TestCase(0)] [TestCase(1)]
    public void GatecrasherDelayedMemberFailure_ClaimsWholeTeamOnce(int memberIndex)
    {
        using(var p=new Pair())
        {
            var use=p.S.Table.CurrentUse;var gate=p.S.Create<GatecrasherCustomer>();
            var routines=use.Party.Members.Select(m=>(IEnumerator)Invoke(gate,"DelayedAngerForMember",p.S.Table,p.S.Manager,4f,use,m,Get<object>(gate,"_serviceEventIdentity"))).ToArray();
            foreach(var r in routines)r.MoveNext();routines[memberIndex].MoveNext();routines[1-memberIndex].MoveNext();
            Assert.That(use.ServiceAborted,Is.True);Assert.That(use.ServiceAbortReason,Is.EqualTo("Gatecrasher"));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [TestCase("guard")] [TestCase("event-reuse")] [TestCase("table-reuse")]
    public void GatecrasherOldOrDefendedDelay_CannotEndCurrentParty(string reason)
    {
        using(var p=new Pair())
        {
            var use=p.S.Table.CurrentUse;var gate=p.S.Create<GatecrasherCustomer>();
            var r=(IEnumerator)Invoke(gate,"DelayedAngerForMember",p.S.Table,p.S.Manager,4f,use,p.Party.Members[1],Get<object>(gate,"_serviceEventIdentity"));r.MoveNext();
            if(reason=="guard")Set(gate,"_isEndEvent",true);
            if(reason=="event-reuse")Set(gate,"_serviceEventIdentity",new object());
            if(reason=="table-reuse")p.S.Manager.CancelPartyGuide(p.S.Table);
            r.MoveNext();Assert.That(use.ServiceAborted,Is.False);Assert.That(p.S.Stage.Satisfaction,Is.Zero);
        }
    }

    [TestCase("accept",0)] [TestCase("accept",1)] [TestCase("deliver",0)] [TestCase("deliver",1)]
    public void ExistingTendencyCheckpoints_EvaluateTheRelevantMember(string point,int memberIndex)
    {
        using(var p=new Pair())
        {
            if(point=="deliver")CookRound(p);
            Set(p.Party.Members[memberIndex].Customer.NormalCustomerData,"_tendencyType",CustomerTendencyType.HighlySensitive);
            if(point=="accept")Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.ServiceAborted));
            else Assert.That(p.S.Manager.TryServePartyOrder(p.Batch.Orders[memberIndex]),Is.False);
            Assert.That(p.S.Table.CurrentUse.ServiceAborted,Is.True);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [Test]
    public void AbortPreservesOtherTablesKitchenAndAlreadyCompletedCookingCounts()
    {
        TableData other=null;
        using(var p=new Pair(beforeSpawn:x=>other=CustomerPartyOrderTests.AddSingleOrder(x)))
        {
            var identity=other.CurrentFood.Identity;int amount=other.TotalPrice;
            p.Accept();p.Pump();p.S.Manager.AbortPartyService(p.S.Table.CurrentUse,"FoodWaitExpired");
            Assert.That(p.Burner.CookingData.Identity,Is.SameAs(identity));p.Pump();
            Assert.That(other.TableState,Is.EqualTo(ETableState.CanServing));Assert.That(other.TotalPrice,Is.EqualTo(amount));
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(1));Assert.That(p.S.Queue,Is.Empty);
        }
    }

    [Test]
    public void EmptyStateObserverFailure_CannotLeaveReturnedPartyOccupyingTable()
    {
        using(var p=Timed())
        {
            Expire(p);var use=p.S.Table.CurrentUse;
            p.S.Table.OnChangeTableStateHandler+=state=>{if(state==ETableState.Empty)throw new InvalidOperationException("Injected empty display failure");};
            p.ArriveExit(0);p.ArriveExit(1);
            Assert.That(use.SettlementCompleted,Is.True);Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [Test]
    public void BrokenCookingTimerView_CannotInterruptLogicalCancellationAndExit()
    {
        using(var p=Timed())
        {
            p.Accept();p.Pump();
            var timers=Get<UIBurnerTimer[]>(p.S.Kitchen,"_burnerTimers");
            Set(timers[0],"_imageFillAmount",null);
            Expire(p);var use=p.S.Table.CurrentUse;
            Assert.That(use.ServiceAborted&&use.CompletionStarted,Is.True);Assert.That(p.S.Queue,Is.Empty);
            Assert.That(p.Burner.CookingData.IsDefault(),Is.True);Assert.That(UserInfo.TotalCookCount,Is.Zero);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));p.ArriveExit(0);p.ArriveExit(1);Assert.That(p.S.Table.CurrentUse,Is.Null);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void FinalA_UnservedB_PreservesOnlyAFixedCoinsAndNormalTip(bool tipPaid)
    {
        using(var p=Timed())
        {
            CookRound(p);var a=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;Eat(p,a);
            if(tipPaid)p.S.TakeWait(.5f)();
            GameManager.Instance.FeverRuntimeContext.TryActivate(10,out var token);
            Expire(p);p.ArriveExit(0);p.ArriveExit(1);
            if(!tipPaid)p.S.TakeWait(.5f)();
            Assert.That(use.Completions[0].CoinAmount,Is.EqualTo(101));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));
            Assert.That(use.Completions[1].MealsCompleted,Is.Zero);Assert.That(use.Completions[1].CoinDropped,Is.False);
            Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));GameManager.Instance.FeverRuntimeContext.Deactivate(token);
        }
    }

    [Test]
    public void BothDelivered_ClocksStayStoppedDuringMealAndCompanionWait()
    {
        using(var p=Timed())
        {
            CookRound(p);var foods=p.Batch.Orders.ToArray();var use=p.S.Table.CurrentUse;
            foreach(var f in foods)Assert.That(p.S.Manager.TryServePartyOrder(f),Is.True);p.S.Manager.StopAllCoroutines();
            p.S.Controller.TickPartyClocks(foods[0].PreparedAt+1000);Assert.That(use.ServiceAborted,Is.False);
            Finish(p,foods[0]);p.S.Controller.TickPartyClocks(foods[0].PreparedAt+2000);Assert.That(use.ServiceAborted,Is.False);
            Finish(p,foods[1]);Assert.That(use.CompletionStarted,Is.True);Assert.That(use.ServiceAborted,Is.False);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(4));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
        }
    }

    [Test]
    public void ReservationReleaseObserver_CannotAcceptDeliverOrCompleteCancelledFood()
    {
        using(var p=Timed())
        {
            CookRound(p);var food=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;
            var w=p.S.CreateWaiter();var lease=p.S.Manager.ReserveWaiterWork(p.S.Table,w.staff,w.action,true);
            p.S.Manager.PickUpWaiterFood(lease);int callbacks=0;
            p.S.Table.OnServiceChangedHandler+=()=>{
                if(!use.ServiceAborted)return;callbacks++;
                Assert.That(p.S.Manager.TryServePartyOrder(food),Is.False);
                Invoke(p.S.Manager,"CompletePartyMeal",food);Invoke(p.S.Manager,"PreparePartyOrders",p.S.Table,use);
                Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.ServiceAborted));
            };
            Expire(p);Assert.That(callbacks,Is.GreaterThan(0));Assert.That(food.CompletionRecorded,Is.False);
            Assert.That(use.Completions.All(r=>r.MealsCompleted==0&&!r.FinalCompleted),Is.True);Assert.That(use.Round,Is.EqualTo(1));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));w.action.Destructor();
        }
    }

    [TestCase("scene")] [TestCase("account")]
    public void AbortedFinalTip_OldContextCallbackCannotPayAfterContextReplacement(string boundary)
    {
        using(var p=Timed())
        {
            CookRound(p);Eat(p,p.Batch.Orders[0]);var tip=p.S.TakeWait(.5f);Expire(p);
            if(boundary=="scene")Invoke(p.S.Manager,"InvalidateExecution");else p.S.InstallAccount();
            tip();tip();Assert.That(p.S.Stage.Tip,Is.Zero);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101),"Already dropped coins are preserved, never paid twice");
        }
    }
}
#endif
