#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Pair = CustomerPartyOrderTests.Pair;

// Explicit production callbacks test state and identity; the Stage1 capture tests elapsed movement separately.
public sealed class CustomerPartyFurnitureTests
{
    internal static void Slot(Pair p)
    {
        var s=p.S;s.Table.TableType=TableType.Table2;
        var group=Get<Dictionary<ERestaurantFloorType,FurnitureGroup>>(Get<FurnitureSystem>(s.Manager,"_furnitureSystem"),"_furnitureGroupDic")[s.Table.FloorType];
        Set(group,"_tableManager",s.Manager);Set(group,"_tableDataList",new List<TableData>{s.Table});
        Set(group,"_tableDataDic",new Dictionary<TableType,TableData>{{TableType.Table2,s.Table}});
        s.Table.TableFurniture.SetTableData(s.Table,s.Stage.GetTableData(s.Table.FloorType,TableType.Table2));
    }
    private static object Remove(Pair p)
    { var edit=p.S.Manager.BeginFurnitureEdit();p.S.Manager.NotFurnitureTable(p.S.Table);return edit; }
    private static void Close(Pair p,object edit)
    {p.S.Manager.CompleteFurnitureEdit(edit);p.S.Manager.CompleteFurnitureEdit(edit);}
    private static void Wait(WaiterAction w)
    {var t=Get<Muks.Tween.TweenWait>(w,"_tweenData");var callback=Get<Action>(t,"_onCompleted");t.Stop();callback();}
    private static string Ledger(TableUseIdentity u)=>string.Join("|",u.Completions.Select(c=>$"{c.RemainingOrders},{c.TotalPrice},{c.CompletedPrice},{c.CoinAmount},{c.TipPaid},{c.VisitCounted}"));
    private static FurnitureData Equip(Pair p,int index=0)
    {
        CustomerPartyIntegrationTests.LoadFurnitureCatalog(p.S);
        var art=FurnitureDataManager.Instance.GetFurnitureDataList(FurnitureType.Table2)[index];
        Get<FurnitureData[,]>(p.S.Stage,"_equipFurnitureDatas")[0,(int)FurnitureType.Table2]=art;return art;
    }

    [TestCase("unchanged")] [TestCase("cancel")] [TestCase("scene")] [TestCase("account")]
    public void NoFinalFurnitureRemovalOnUnchangedCancelledOrInvalidatedSession(string kind)
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            var use=p.S.Table.CurrentUse;var edit=p.S.Manager.BeginFurnitureEdit();
            if(kind!="unchanged")p.S.Manager.NotFurnitureTable(p.S.Table);
            if(kind=="cancel")p.S.Manager.CancelFurnitureEdit(edit);
            if(kind=="scene")Invoke(p.S.Manager,"InvalidateExecution");
            if(kind=="account")p.S.InstallAccount();
            Close(p,edit);Assert.That(p.S.Stage.Satisfaction,Is.Zero);Assert.That(use.FurnitureRemoved,Is.False);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void UnownedUnacceptedMenu_RemovalAlwaysPenalizesTwo_KeepNeverRerolls(bool remove)
    {
        using(var p=new Pair(firstOwned:false,beforeGuide:Slot))
        {
            var use=p.S.Table.CurrentUse;var batch=p.Batch;var ids=batch.Orders.Select(o=>o.Identity).ToArray();
            var clock=batch.Orders.Select(o=>o.PreparedAt).ToArray();var ledger=Ledger(use);var random=UnityEngine.Random.state;
            for(int i=0;i<3;i++)
            {
                Get<FurnitureData[,]>(p.S.Stage,"_equipFurnitureDatas")[0,(int)FurnitureType.Table2]=null;
                var token=Remove(p);if(!remove)Equip(p);Close(p,token);
                if(remove)break;
                Assert.That(p.Batch,Is.SameAs(batch));Assert.That(batch.Orders.Select(o=>o.Identity),Is.EqualTo(ids));
                Assert.That(batch.Orders.Select(o=>o.PreparedAt),Is.EqualTo(clock));Assert.That(Ledger(use),Is.EqualTo(ledger));
            }
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(remove?-10:0));
            Assert.That(use.CompletionStarted,Is.EqualTo(remove));Assert.That(p.S.Coins.CurrentMoney,Is.Zero);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
    }

    [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(true,1)]
    public void LatestCompletedMeals_FixedCoinPendingTipAndPartialFoodSurvive(bool paid,int completedB)
    {
        using(var p=new Pair(ordersA:1,ordersB:2,beforeGuide:Slot))
        {
            var use=p.S.Table.CurrentUse;CustomerPartyCompletionTests.CookRound(p);var foods=p.Batch.Orders.ToArray();
            var edit=Remove(p);CustomerPartyCompletionTests.Eat(p,foods[0]);
            if(completedB==1)CustomerPartyCompletionTests.Eat(p,foods[1]);
            else {p.S.Manager.TryServePartyOrder(foods[1]);p.S.Manager.StopAllCoroutines();}
            if(paid)p.S.TakeWait(.5f)();
            int fixedA=use.Completions[0].CoinAmount;Close(p,edit);
            Assert.That(use.Completions[0].CoinAmount,Is.EqualTo(fixedA));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101*(1+completedB)));
            Assert.That(use.Completions[1].FinalCompleted,Is.False);Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            p.ArriveExit(0);Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));p.ArriveExit(1);
            if(!paid){var tip=p.S.TakeWait(.5f);tip();tip();}
            Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.DontUse));
        }
    }

    [Test]
    public void FoodTimeoutWhileEditingIsNotUndone_FinalRemovalStopsEatingWithoutSecondPenalty()
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            CustomerPartyCompletionTests.CookRound(p);var food=p.Batch.Orders[0];var use=p.S.Table.CurrentUse;
            p.S.Manager.TryServePartyOrder(food);p.S.Manager.StopAllCoroutines();var eating=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",food);eating.MoveNext();eating.MoveNext();
            var edit=Remove(p);p.S.Manager.TickPartyFoodClocks(use,100000);
            Assert.That(use.ServiceAborted,Is.True);Assert.That(use.CompletionStarted,Is.False);
            Close(p,edit);Assert.That(eating.MoveNext(),Is.False);Assert.That(use.CompletionStarted,Is.True);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(p.S.Coins.CurrentMoney,Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CompletedDepartureCannotBeRepenalizedByLateShopClose(bool abort)
    {
        using(var p=new Pair(ordersA:1,ordersB:1,beforeGuide:Slot))
        {
            var edit=Remove(p);var use=p.S.Table.CurrentUse;
            if(abort)p.S.Manager.AbortPartyService(use,"FoodWaitExpired");
            else{CustomerPartyCompletionTests.CookRound(p);foreach(var f in p.Batch.Orders.ToArray())CustomerPartyCompletionTests.Eat(p,f);}
            float sat=p.S.Stage.Satisfaction;long coins=p.S.Coins.CurrentMoney;
            Close(p,edit);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(sat));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(coins));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CarryingRetargetPreservesLeaseAndUnchangedTargetPreservesDeliveryTimer(bool change)
    {
        using(var p=new Pair())
        {
            CustomerPartyCompletionTests.CookRound(p);var w=p.S.CreateWaiter();Invoke(w.action,"ServingAction",p.S.Table);p.S.Arrive(w.staff);Wait(w.action);
            var lease=Get<TableServiceReservation>(w.action,"_lease");var food=lease.Order.PartyOrder;var oldArrival=Get<Action>(w.staff,"_moveCompleted");
            p.S.Arrive(w.staff);var oldWait=Get<Muks.Tween.TweenWait>(w.action,"_tweenData");var oldCallback=Get<Action>(oldWait,"_onCompleted");
            float due=Get<float>(w.action,"_deliveryDue");
            if(change)p.S.Table.ChairTrs[food.SeatIndex].position+=Vector3.right;
            w.action.RefreshPartySeat(lease.Use);
            Assert.That(Get<TableServiceReservation>(w.action,"_lease"),Is.SameAs(lease));Assert.That(food.State,Is.EqualTo(PartyFoodState.Carrying));
            Assert.That(Get<float>(w.action,"_deliveryDue"),Is.EqualTo(due));
            if(change){oldArrival();oldCallback();Assert.That(food.Identity.ServingStarted,Is.False);p.S.Arrive(w.staff);}
            else Assert.That(Get<Muks.Tween.TweenWait>(w.action,"_tweenData"),Is.SameAs(oldWait));
            Wait(w.action);Wait(w.action);Assert.That(food.Identity.ServingStarted,Is.True);Assert.That(lease.Released,Is.True);
        }
    }

    [Test]
    public void DifferentFurnitureAndOffsets_MoveBothFoodAndButtonsWithoutLedgerReset()
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            var use=p.S.Table.CurrentUse;var edit=Remove(p);var ledger=Ledger(use);var ids=p.Batch.Orders.Select(f=>f.Identity).ToArray();
            Equip(p,1);Set(p.S.Table,"_leftSeatOffset",new Vector2(.2f,.4f));Set(p.S.Table,"_rightSeatOffset",new Vector2(-.3f,.7f));
            p.S.Table.ChairTrs[0].position+=new Vector3(.3f,.2f);p.S.Table.ChairTrs[1].position+=new Vector3(-.4f,.8f);
            Close(p,edit);foreach(var m in p.Party.Members)Assert.That(m.Customer.transform.position,Is.EqualTo(p.S.Table.GetSeatPosition(m.SeatIndex)));
            Assert.That(Ledger(use),Is.EqualTo(ledger));Assert.That(p.Batch.Orders.Select(f=>f.Identity),Is.EqualTo(ids));
            Assert.That(p.S.Stage.Satisfaction,Is.Zero);
        }
    }

    [Test]
    public void UnequippedSlotSaveParseRestoreAndSharedOddCoinCollectionPreserveEveryUnit()
    {
        using(var p=new Pair(ordersA:1,ordersB:1,beforeGuide:Slot))
        {
            // Both seats deliberately share the original persistent area's binding.
            var area=p.S.Coins;var originalData=p.S.Stage.GetTableData(p.S.Table.FloorType,TableType.Table1).CoinAreaDatas[0];
            Set(area,"_data",originalData);area.DropCoin(Vector3.zero,202,true);
            CustomerPartyCompletionTests.CookRound(p);CustomerPartyCompletionTests.Eat(p,p.Batch.Orders[0]);Close(p,Remove(p));
            Assert.That(area.CurrentMoney,Is.EqualTo(303));Assert.That(Get<CoinAreaData>(area,"_data"),Is.SameAs(originalData));
            var wire=Newtonsoft.Json.Linq.JObject.Parse(p.S.Stage.SaveData().GetParam().GetJson());
            var parsed=(List<List<SaveTableData>>)Invoke(new ServerStageData(),"ConvertJsonToSaveTableList",LitJson.JsonMapper.ToObject(wire["SaveTableDataList"].ToString()));
            var loaded=parsed[0][0].CoinAreaDatas[0];Assert.That(loaded.Money,Is.EqualTo(303));
            area.Init(loaded,p.S.Table.FloorType);Set(area,"_coinEndTr",p.S.Create<Transform>());long before=UserInfo.Money;
            var coins=Get<List<PointerDownSpriteRenderer>>(area,"_coinList").ToArray();area.GiveCoin();
            Assert.That(UserInfo.Money,Is.EqualTo(before));
            foreach(var coin in coins)
            {
                var callbacks=Get<Dictionary<int,Action>>(coin.GetComponent<Muks.Tween.TweenTransformMove>(),"_onCompletedDic").Values.ToArray();
                foreach(var callback in callbacks){callback();callback();}
            }
            Assert.That(UserInfo.Money-before,Is.EqualTo(303));Assert.That(loaded.Money,Is.Zero);Assert.That(loaded.CoinCount,Is.Zero);
        }
    }

    [Test]
    public void DisplayCapDoesNotLeakUncollectableCoinImages()
    {
        using(var p=new Pair())
        {
            var area=p.S.Coins;Set(area,"_maxCoinCount",2);
            area.DropCoin(Vector3.zero,101,true);area.DropCoin(Vector3.zero,101,true);area.DropCoin(Vector3.zero,101,true);
            Assert.That(area.Count,Is.EqualTo(2));Assert.That(area.CurrentMoney,Is.EqualTo(303));
            Set(area,"_coinEndTr",p.S.Create<Transform>());var coins=Get<List<PointerDownSpriteRenderer>>(area,"_coinList").ToArray();long before=UserInfo.Money;area.GiveCoin();
            foreach(var coin in coins)foreach(var done in Get<Dictionary<int,Action>>(coin.GetComponent<Muks.Tween.TweenTransformMove>(),"_onCompletedDic").Values.ToArray())done();
            Assert.That(UserInfo.Money-before,Is.EqualTo(303));Assert.That(area.Count,Is.Zero);Assert.That(coins.All(c=>!c.gameObject.activeSelf),Is.True);
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void InFlightGuideRetargetOrRemovalNeverCompletesUndeliveredMember(bool remove)
    {
        using(var s=new Flow())
        {
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var party),Is.True);
            foreach(var m in party.Members){m.Customer.transform.position=m.Customer.TargetPos;Get<Action>(m.Customer,"_moveCompleted")();}
            Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.True);var use=s.Table.CurrentUse;
            var a=party.Members[0];a.Customer.transform.position=a.Customer.TargetPos;Get<Action>(a.Customer,"_moveCompleted")();
            var b=party.Members[1];var prior=b.Customer.transform.position;
            var token=s.Manager.BeginFurnitureEdit();
            if(remove){s.Table.TableType=TableType.Table2;s.Manager.NotFurnitureTable(s.Table);}
            else{Invoke(s.Manager,"TrackFurnitureEdit",s.Table);s.Table.ChairTrs[1].position+=Vector3.right;}
            s.Manager.CompleteFurnitureEdit(token);
            Assert.That(b.Customer.transform.position,Is.EqualTo(prior));Assert.That(use.HasArrived(b),Is.False);
            if(remove){Assert.That(use.CompletionStarted,Is.True);Assert.That(use.Completions.All(c=>c.ExitStarted),Is.True);Assert.That(s.Stage.Satisfaction,Is.EqualTo(-10));}
            else{Assert.That(b.Customer.TargetPos.x,Is.EqualTo(s.Table.GetSeatPosition(b.SeatIndex).x));Assert.That(use.CompletionStarted,Is.False);}
        }
    }

    [TestCase(1)] [TestCase(2)]
    public void LateConsumedCloseCannotTerminateNextUse(int memberCount)
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            var token=Remove(p);Close(p,token);p.ArriveExit(0);p.ArriveExit(1);p.S.Table.TableState=ETableState.Empty;
            Assert.That(p.S.Controller.TrySpawnWaitingParty(Enumerable.Repeat(p.S.Data.Id,memberCount).ToArray(),out var next),Is.True);
            foreach(var m in next.Members){m.Customer.transform.position=m.Customer.TargetPos;Get<Action>(m.Customer,"_moveCompleted")();}
            Assert.That(p.S.Manager.OnCustomerGuideEvent(p.S.Table.FloorType),Is.True);var use=p.S.Table.CurrentUse;
            Close(p,token);Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));Assert.That(use.CompletionStarted||use.FurnitureRemoved,Is.False);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [Test]
    public void NewFurnitureEventInSameShopTracksReplacementUse()
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            var token=Remove(p);p.S.Manager.CancelPartyGuide(p.S.Table);p.S.Table.TableState=ETableState.Empty;
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id,p.S.Data.Id},out var next),Is.True);
            foreach(var m in next.Members){m.Customer.transform.position=m.Customer.TargetPos;Get<Action>(m.Customer,"_moveCompleted")();}
            Assert.That(p.S.Manager.OnCustomerGuideEvent(p.S.Table.FloorType),Is.True);var use=p.S.Table.CurrentUse;
            p.S.Manager.NotFurnitureTable(p.S.Table);Close(p,token);Assert.That(use.FurnitureRemoved&&use.CompletionStarted,Is.True);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(-10));
        }
    }

    [TestCase("stage")] [TestCase("account")] [TestCase("destroy")] [TestCase("reuse")]
    public void LateCoinCollectionReturnsOwnedImageAndPreservesSameAccountPayment(string boundary)
    {
        using(var p=new Pair())
        {
            var area=p.S.Coins;area.DropCoin(Vector3.zero,101,true);Set(area,"_coinEndTr",p.S.Create<Transform>());
            var coin=Get<List<PointerDownSpriteRenderer>>(area,"_coinList")[0];area.GiveCoin();
            var done=Get<Dictionary<int,Action>>(coin.GetComponent<Muks.Tween.TweenTransformMove>(),"_onCompletedDic").Values.ToArray();
            if(boundary=="account")p.S.InstallAccount();
            else if(boundary=="stage")p.S.Swap(typeof(UserInfo),"_currentStage",EStage.Stage2);
            else if(boundary=="destroy")UnityEngine.Object.DestroyImmediate(area.gameObject);
            else
            {
                ObjectPoolManager.Instance.DespawnCoin(coin);
                var pool=(Queue<PointerDownSpriteRenderer>)typeof(ObjectPoolManager).GetField("_coinPool",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
                var others=pool.Where(c=>c!=coin).ToArray();pool.Clear();pool.Enqueue(coin);foreach(var c in others)pool.Enqueue(c);
                area.DropCoin(Vector3.zero,47,true);Assert.That(Get<List<PointerDownSpriteRenderer>>(area,"_coinList")[0],Is.SameAs(coin));
            }
            long money=UserInfo.Money;foreach(var callback in done){callback();callback();}
            Assert.That(UserInfo.Money-money,Is.EqualTo(boundary=="account"?0:101));
            Assert.That(coin.gameObject.activeSelf,Is.EqualTo(boundary=="reuse"));
            if(boundary=="reuse"){Assert.That(area.CurrentMoney,Is.EqualTo(47));Assert.That(area.Count,Is.EqualTo(1));}
        }
    }

    [Test]
    public void RemovedSlotFullSinkKeepsOneDeferredCleaningCycleAfterReequip()
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            CustomerPartyCompletionTests.CookRound(p);p.S.Manager.TryServePartyOrder(p.Batch.Orders[0]);p.S.Manager.StopAllCoroutines();
            var eating=(IEnumerator)Invoke(p.S.Manager,"EatPartyOrder",p.Batch.Orders[0]);eating.MoveNext();eating.MoveNext();
            var floor=p.S.Table.FloorType;while(UserInfo.GetBowlAddEnabled(EStage.Stage1,floor))UserInfo.AddSinkBowlCount(EStage.Stage1,floor);
            int max=p.S.Stage.GetSinkBowlCount(floor);Close(p,Remove(p));p.ArriveExit(0);p.ArriveExit(1);
            var saved=p.S.Stage.GetTableData(floor,TableType.Table2);Assert.That(saved.NeedCleaning&&!saved.CleaningBowlProcessed,Is.True);
            p.S.Table.TableFurniture.OnCleanAction();Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.DontUse));
            Equip(p);p.S.Table.TableState=p.S.Table.TableFurniture.AvailableTableState;
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));p.S.Table.TableFurniture.OnCleanAction();Assert.That(saved.NeedCleaning,Is.True);
            UserInfo.SubSinkBowlCount(EStage.Stage1,floor);p.S.Table.TableFurniture.OnCleanAction();p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(p.S.Stage.GetSinkBowlCount(floor),Is.EqualTo(max));
        }
    }

    [Test]
    public void FixedPersonalAmountCannotBeRecalculatedByLaterFeverAtRemoval()
    {
        using(var p=new Pair(ordersA:1,ordersB:2,beforeGuide:Slot))
        {
            CustomerPartyCompletionTests.CookRound(p);CustomerPartyCompletionTests.Eat(p,p.Batch.Orders[0]);var use=p.S.Table.CurrentUse;
            int fixedAmount=use.Completions[0].CoinAmount;Assert.That(GameManager.Instance.FeverRuntimeContext.TryActivate(10f,out var token),Is.True);
            Close(p,Remove(p));Assert.That(use.Completions[0].CoinAmount,Is.EqualTo(fixedAmount));Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(fixedAmount));
            GameManager.Instance.FeverRuntimeContext.Deactivate(token);
        }
    }

    [Test]
    public void ShopDisableCancelsEditingWithoutUserRemovalPenalty()
    {
        using(var p=new Pair(beforeGuide:Slot))
        {
            var shop=p.S.Create<UIRestaurantAdmin>();var edit=Remove(p);Set(shop,"_partyTableManager",p.S.Manager);Set(shop,"_partyFurnitureEdit",edit);
            Invoke(shop,"OnDisable");Close(p,edit);Assert.That(p.S.Stage.Satisfaction,Is.Zero);Assert.That(p.S.Table.CurrentUse.FurnitureRemoved,Is.False);
        }
    }

}
#endif
