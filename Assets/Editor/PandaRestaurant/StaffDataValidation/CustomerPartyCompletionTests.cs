#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Pair = CustomerPartyOrderTests.Pair;

public sealed class CustomerPartyCompletionTests
{
    internal static void CookRound(Pair p, bool waiter = false)
    {
        if (waiter)
        {
            var w = p.S.CreateWaiter();
            Invoke(w.action, "OrderAction", p.S.Table); p.S.Arrive(w.staff);
            for (int i=0;i<2;i++)
            {
                var wait=Get<Muks.Tween.TweenWait>(w.action,"_tweenData");
                var done=Get<Action>(wait,"_onCompleted"); wait.Stop(); done();
            }
            w.action.Destructor();
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Accepted));
        }
        else Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.Accepted));
        int count = p.Batch.Orders.Count;
        for (int i = 0; i <= count; i++) p.Pump();
        Assert.That(p.Batch.Orders.All(o => o.State == PartyFoodState.ReadyForServing), Is.True);
    }

    internal static void Eat(Pair p, PartyFoodOrder food)
    {
        Assert.That(p.S.Manager.TryServePartyOrder(food), Is.True);
        p.S.Manager.StopAllCoroutines();
        Finish(p, food);
    }
    internal static void Finish(Pair p, PartyFoodOrder food)
    {
        var eat = (IEnumerator)Invoke(p.S.Manager, "EatPartyOrder", food);
        Assert.That(eat.MoveNext(), Is.True); Assert.That(eat.MoveNext(), Is.True);
        Assert.That(food.Identity.EatingAnimationStarted, Is.True);
        Assert.That(eat.MoveNext(), Is.False);
    }
    private static void DistinctCoins(Pair p)
    {
        var b = p.S.Create<DropCoinArea>(); Set(b, "_dropArea", p.S.Create<Transform>());
        Set(b, "_maxCoinCount", 10); Set(b, "_data", p.S.Stage.GetTableData(p.S.Table.FloorType,p.S.Table.TableType).CoinAreaDatas[1]);
        Set(p.S.Table, "_dropCoinAreas", new[] {p.S.Coins, b});
    }
    private static void Garbage(Pair p, TableUseIdentity use)
    {
        int n = use.Completions.Sum(r => r.GarbageCount);
        for (int i=0;i<2;i++) p.S.TakeWait(.02f)();
        for (int i=2;i<n;i++) p.S.TakeWait(.04f)();
    }
    private static void Tips(Pair p) { p.S.TakeWait(.5f)(); p.S.TakeWait(.5f)(); }

    [TestCase(false)] [TestCase(true)]
    public void DuplicateAndReversedDropCallbacks_PreserveBothPersonalResults(bool reverse)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p); var use=p.S.Table.CurrentUse;
            foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var waits=Get<GameObject>(p.S,"waits").GetComponentsInChildren<Muks.Tween.TweenWait>(true)
                .Where(w=>w.enabled && Get<float>(w,"_totalDuration")<.1f).ToArray();
            var callbacks=waits.Select(w=>Get<Action>(w,"_onCompleted")).ToArray(); foreach(var w in waits) w.Stop();
            if(reverse) Array.Reverse(callbacks);
            foreach(var callback in callbacks) {callback();callback();}
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(use.Completions.Sum(r=>r.GarbageCount)));
            var firstTip=p.S.TakeWait(.5f);firstTip();firstTip();var secondTip=p.S.TakeWait(.5f);secondTip();secondTip();
            Assert.That(p.S.Stage.Tip,Is.EqualTo(100)); Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            foreach(var food in p.Batch.Orders) Invoke(p.S.Manager,"CompletePartyMeal",food);
            p.S.Manager.BeginPartyDeparture(use); Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
        }
    }

    [TestCase("scene")] [TestCase("stage")] [TestCase("cancel")] [TestCase("account")]
    public void OldCompletionCallbacks_AfterInvalidation_CannotDropOrPay(string boundary)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p); foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var waits=Get<GameObject>(p.S,"waits").GetComponentsInChildren<Muks.Tween.TweenWait>(true).Where(w=>w.enabled).ToArray();
            var callbacks=waits.Select(w=>Get<Action>(w,"_onCompleted")).Where(a=>a!=null).ToArray(); foreach(var w in waits) w.Stop();
            if(boundary=="scene") Invoke(p.S.Manager,"InvalidateExecution");
            else if(boundary=="stage") p.S.Swap(typeof(UserInfo),"_currentStage",EStage.Stage2);
            else if(boundary=="account") p.S.InstallAccount();
            else p.S.Manager.CancelPartyGuide(p.S.Table);
            foreach(var callback in callbacks) callback();
            Assert.That(p.S.Stage.Tip,Is.Zero); Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);
            Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202),"Already dropped money must not be clawed back or paid twice");
        }
    }

    [Test]
    public void NormalFirstReturn_ReusedCustomerCannotInvalidateMovingPartnerOrCapturedTip()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p); foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var use=p.S.Table.CurrentUse; var first=p.Party.Members[0].Customer; var second=p.Party.Members[1].Customer;
            var old=Get<Action>(first,"_moveCompleted"); p.ArriveExit(0);
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id},out var next),Is.True);
            Assert.That(next.Members[0].Customer,Is.SameAs(first));
            old(); Assert.That(second.gameObject.activeSelf,Is.True); Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            p.ArriveExit(1); Tips(p);
            Assert.That(next.IsActive,Is.True); Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(4));
            Assert.That(p.S.Stage.FeverGauge,Is.EqualTo(40));
        }
    }

    [Test]
    public void RewardAndSinkObserversThrow_OtherResultsAndBothExitRequestsStillComplete()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            UserInfo.OnVisitedCustomerHandler+=()=>throw new InvalidOperationException("owned visit view");
            UserInfo.OnAddCustomerCountHandler+=()=>throw new InvalidOperationException("owned count view");
            p.S.Stage.OnChangeSinkBowlHandler+=()=>throw new InvalidOperationException("owned bowl view");
            CookRound(p); foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var use=p.S.Table.CurrentUse;
            Assert.That(use.Completions.All(r=>r.ExitStarted&&r.CoinDropped),Is.True);
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2)); Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(202));
            Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            p.S.Table.TableFurniture.OnCleanAction(); Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            p.ArriveExit(0);p.ArriveExit(1);Tips(p);Assert.That(p.S.Stage.Tip,Is.EqualTo(100));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualCleanAfterReload_MarkedPartySkipsBowl_LegacyAddsOne(bool marked)
    {
        using(var p=new Pair())
        {
            var floor=p.S.Table.FloorType; var saved=p.S.Stage.GetTableData(floor,p.S.Table.TableType);
            saved.SetNeedCleaning(true); if(marked) p.S.Stage.TryProcessCleaningBowl(floor,p.S.Table.TableType);
            var wire=Newtonsoft.Json.Linq.JObject.Parse(p.S.Stage.SaveData().GetParam().GetJson());
            if(!marked) ((Newtonsoft.Json.Linq.JObject)wire["SaveTableDataList"][0][0]).Remove("CleaningBowlProcessed");
            var dto=new ServerStageData();
            var parsed=(List<List<SaveTableData>>)Invoke(dto,"ConvertJsonToSaveTableList",LitJson.JsonMapper.ToObject(wire["SaveTableDataList"].ToString()));
            var loaded=parsed[0][0];
            Get<SaveTableData[,]>(p.S.Stage,"_saveTableDatas")[0,0]=loaded;
            p.S.Manager.CancelPartyGuide(p.S.Table);p.S.Table.TableState=ETableState.NeedCleaning;p.S.Table.TableFurniture.SetTableData(p.S.Table,loaded);
            p.S.Table.TableFurniture.OnCleanAction();p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(p.S.Stage.GetSinkBowlCount(floor),Is.EqualTo(1));
        }
    }
    [TestCase(1,1,false)] [TestCase(2,1,true)] [TestCase(1,2,true)] [TestCase(2,2,false)] [TestCase(4,3,true)]
    public void Rounds_PersonalCompletion_AndJointDeparture_PreserveMemberCounts(int a, int b, bool waiter)
    {
        using (var p = new Pair(ordersA:a, ordersB:b))
        {
            DistinctCoins(p); var use = p.S.Table.CurrentUse; long moneyBefore=UserInfo.Money;
            for (int round=1; round<=Math.Max(a,b); round++)
            {
                var batch=p.Batch; Assert.That(batch.Round,Is.EqualTo(round));
                Assert.That(batch.Orders.Count,Is.EqualTo((a>=round?1:0)+(b>=round?1:0)));
                var random=UnityEngine.Random.state; p.S.Table.RefreshPartyOrderButtons();
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                CookRound(p,waiter); var foods=batch.Orders.ToArray();
                for(int i=0;i<foods.Length;i++)
                {
                    Eat(p,foods[i]);
                    if(i<foods.Length-1) Assert.That(p.Batch,Is.SameAs(batch));
                }
                if(round<Math.Max(a,b))
                {
                    Assert.That(use.CompletionStarted,Is.False);
                    Assert.That(p.S.Table.DropCoinAreas.Sum(c=>c.CurrentMoney),Is.Zero);
                    Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);
                    Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.Zero);
                    Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.PartySeatedAwaitingOrders));
                    Assert.That(p.Batch,Is.Not.SameAs(batch));
                    Invoke(p.S.Manager,"CompletePartyMeal",foods[0]);
                    Assert.That(p.Batch.Round,Is.EqualTo(round+1));
                    Assert.That(p.S.Manager.TryServePartyOrder(foods[0]),Is.False);
                }
            }
            Assert.That(use.Completions.Select(r=>r.MealsCompleted),Is.EqualTo(new[]{a,b}));
            Assert.That(use.Completions.All(r=>r.FinalCompleted&&r.CoinFixed&&r.CoinDropped),Is.True);
            Assert.That(UserInfo.Money,Is.EqualTo(moneyBefore),"Drop must not credit currency before collection");
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(a+b));
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(2*(a+b)));
            Assert.That(p.S.Stage.FeverGauge,Is.EqualTo(40));
            Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(2));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(1));
            Tips(p); Garbage(p,use);
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.InRange(2,4));
            Assert.That(p.S.Stage.Tip,Is.EqualTo(50*(a+b)));
            Assert.That(p.S.Table.DropCoinAreas.Select(c=>c.CurrentMoney),Is.EqualTo(new long[]{101*a,101*b}));
            p.S.Manager.BeginPartyDeparture(use); Assert.That(p.S.Table.DropGarbageArea.Count,Is.EqualTo(use.Completions.Sum(r=>r.GarbageCount)));
            var second=p.Party.Members[1].Customer;
            p.ArriveExit(0); Assert.That(second.gameObject.activeSelf,Is.True);
            Assert.That(p.S.Table.CurrentUse,Is.SameAs(use));
            p.ArriveExit(1); Assert.That(p.S.Table.CurrentUse,Is.Null);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CleaningAndExit_EitherOrder_OneDish_AndNextSingleUse(bool cleanFirst)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p); var foods=p.Batch.Orders.ToArray(); foreach(var f in foods) Eat(p,f);
            var cycle=p.S.Table.CleaningCycle;
            if(cleanFirst) p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1),Is.False);
            p.ArriveExit(0); p.ArriveExit(1);
            if(!cleanFirst) Invoke(p.S.Table.TableFurniture,"OnTouchEvent");
            p.S.Table.TableFurniture.OnCleanAction(cycle);
            Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(1));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));
            Assert.That(p.S.Stage.GetTableData(p.S.Table.FloorType,p.S.Table.TableType).CleaningBowlProcessed,Is.False);
            p.S.GuideAndSeat(); Assert.That(p.S.Table.CurrentUse.IsSingle,Is.True);
            p.S.Table.TableState=ETableState.NeedCleaning;
            p.S.Table.TableFurniture.OnCleanAction(cycle);
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType),Is.EqualTo(2));
        }
    }

    [Test]
    public void FullSink_PreservesExistingCap_AndUnprocessedCleaningAddsOnceLater()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var floor=p.S.Table.FloorType;
            while(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage,floor)) UserInfo.AddSinkBowlCount(UserInfo.CurrentStage,floor);
            int max=UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,floor);
            CookRound(p); foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var saved=p.S.Stage.GetTableData(floor,p.S.Table.TableType);
            Assert.That(saved.NeedCleaning,Is.True); Assert.That(saved.CleaningBowlProcessed,Is.False);
            p.S.Table.TableFurniture.OnCleanAction(); Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            UserInfo.SubSinkBowlCount(UserInfo.CurrentStage,floor); p.S.Table.TableFurniture.OnCleanAction();
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty)); Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,floor),Is.EqualTo(max));
            p.S.Table.TableFurniture.OnCleanAction(); Assert.That(UserInfo.GetSinkBowlCount(UserInfo.CurrentStage,floor),Is.EqualTo(max));
        }
    }

    [Test]
    public void PersonalCompletion_PrecedesDeparture_AndLaterMultiplierCannotChangeFixedCoin()
    {
        using(var p=new Pair(ordersA:1,ordersB:2))
        {
            DistinctCoins(p); var use=p.S.Table.CurrentUse; CookRound(p);
            var a=p.Batch.Orders[0]; var b=p.Batch.Orders[1];
            var bonus=new StaffSkillSourceToken(1,1);
            GameManager.Instance.StaffSkillEffectRegistry.RegisterOrUpdate(StaffSkillEffectType.RestaurantTipPayoutPercent,bonus,25f,"party completion test");
            Eat(p,a);
            var result=use.GetCompletion(a.Member);
            Assert.That(result.FinalCompleted,Is.True); Assert.That(result.CoinAmount,Is.EqualTo(101));
            Assert.That(result.CoinDropped,Is.False); Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            p.S.TakeWait(.5f)(); Assert.That(p.S.Stage.Tip,Is.EqualTo(62));
            GameManager.Instance.StaffSkillEffectRegistry.RemoveAllForSource(bonus);
            Assert.That(GameManager.Instance.FeverRuntimeContext.TryActivate(10f,out var feverToken),Is.True);
            Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero); Assert.That(p.S.Table.TableState,Is.Not.EqualTo(ETableState.NeedCleaning));
            Eat(p,b); Assert.That(p.Batch.Orders.Single().Member,Is.SameAs(b.Member));
            Assert.That(p.S.Table.OrderButton.gameObject.activeSelf,Is.False); Assert.That(p.S.Table.CompanionOrderButton.gameObject.activeSelf,Is.True);
            CookRound(p); Eat(p,p.Batch.Orders.Single()); p.S.TakeWait(.5f)();
            Assert.That(result.CoinAmount,Is.EqualTo(101)); Assert.That(p.S.Coins.CurrentMoney,Is.EqualTo(101));
            Assert.That(p.S.Stage.Tip,Is.EqualTo(162));
            Assert.That(use.GetCompletion(b.Member).CoinAmount,Is.EqualTo(404));
            Assert.That(p.S.Table.DropCoinAreas[1].CurrentMoney,Is.EqualTo(404));
            GameManager.Instance.FeverRuntimeContext.Deactivate(feverToken);
        }
    }

    [Test]
    public void AdditionalUnownedRecipe_IsHeldWithoutInitialRejectionOrNewSettlement()
    {
        using(var p=new Pair())
        {
            CookRound(p); foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var use=p.S.Table.CurrentUse;
            var owned=(Dictionary<string,int>)typeof(UserInfo).GetField("_giveRecipeLevelDic",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
            owned.Clear();
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.UnsupportedPolicy));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.PartyPolicyPending));
            Assert.That(use.CompletionStarted,Is.False); Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero); Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.UnsupportedPolicy));
            Assert.That(p.S.Manager.CancelPartyGuide(p.S.Table),Is.True);
        }
    }
    [Test]
    public void BothCoinAreas_UseExistingCollectionTween_AndOnlyThenCreditMoney()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            DistinctCoins(p);long before=UserInfo.Money;CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            Assert.That(UserInfo.Money,Is.EqualTo(before));
            foreach(var area in p.S.Table.DropCoinAreas)
            {
                Set(area,"_coinEndTr",p.S.Create<Transform>());
                var coins=Get<List<PointerDownSpriteRenderer>>(area,"_coinList").ToArray();area.GiveCoin();
                foreach(var coin in coins)
                {
                    var tween=coin.GetComponent<Muks.Tween.TweenTransformMove>();
                    var callbacks=Get<Dictionary<int,Action>>(tween,"_onCompletedDic").Values.ToArray();
                    foreach(var callback in callbacks) callback();
                }
                area.GiveCoin();Assert.That(area.CurrentMoney,Is.Zero);
            }
            Assert.That(UserInfo.Money-before,Is.EqualTo(202));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void CleaningBindingChange_CannotModifyAnotherStageOrReplacementRecord(bool swapStage)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            if(swapStage)
            {
                p.S.Table.OnChangeTableStateHandler+=state=>{if(state==ETableState.NeedCleaning)p.S.Swap(typeof(UserInfo),"_currentStage",EStage.Stage2);};
                CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
                Assert.That(UserInfo.GetSinkBowlCount(EStage.Stage2,p.S.Table.FloorType),Is.Zero);
                Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.Zero);
            }
            else
            {
                CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
                var replacement=new SaveTableData(p.S.Table.FloorType,p.S.Table.TableType);Get<SaveTableData[,]>(p.S.Stage,"_saveTableDatas")[0,0]=replacement;
                p.S.Table.TableFurniture.OnCleanAction();Assert.That(replacement.NeedCleaning,Is.False);
                Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            }
        }
    }
    private static (StaffCleaner staff,CleanerAction action) Cleaner(Pair p)
    {
        var s=p.S;var group=Get<Dictionary<ERestaurantFloorType,FurnitureGroup>>(Get<FurnitureSystem>(s.Manager,"_furnitureSystem"),"_furnitureGroupDic")[s.Table.FloorType];
        Set(group,"_cleanerWaitTr",s.Create<Transform>());Set(group,"_tableDataList",new List<TableData>{s.Table});
        Get<Dictionary<StaffGroupType,float>>(GameManager.Instance,"_addGachaItemStaffSpeedMulDic")[StaffGroupType.Cleaner]=0;
        var staff=s.Track(UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/Cleaner.prefab"))).GetComponent<StaffCleaner>();
        var data=s.Track(ScriptableObject.CreateInstance<CleanerData>());
        Set(data,"_id","OWNED-CLEANER");Set(data,"_speed",6f);Set(data,"_cleanerLevelData",new[]{new CleanerLevelData()});Set(data,"_sprite",s.NewSprite());Set(data,"_idleSprites",new[]{s.NewSprite()});
        Get<Dictionary<string,SaveStaffData>>(s.Stage,"_giveStaffDic")[data.Id]=new SaveStaffData(data.Id,1);
        Get<Dictionary<ERestaurantFloorType,Dictionary<EquipStaffType,StaffData>>>(s.Stage,"_equipStaffTypeDic")[s.Table.FloorType][EquipStaffType.Cleaner]=data;
        s.Swap(typeof(StaffDataManager),"_instance",s.Create<StaffDataManager>());
        staff.Init(EquipStaffType.Cleaner,s.Manager,Get<KitchenSystem>(s.Manager,"_kitchenSystem"),s.Controller,Get<FeverSystem>(s.Manager,"_feverSystem"));staff.SetStaffData(data,s.Table.FloorType);
        return(staff,(CleanerAction)Get<IStaffAction>(staff,"_staffAction"));
    }
    private static Action CleanerWait(CleanerAction action)
    {var wait=Get<Muks.Tween.TweenWait>(action,"_tweenData");var callback=Get<Action>(wait,"_onCompleted");wait.Stop();return callback;}
    [TestCase(false)] [TestCase(true)]
    public void AlreadyProcessedDish_FullSink_CanCleanThroughManualOrActualCleaner(bool cleaner)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            while(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage,p.S.Table.FloorType)) UserInfo.AddSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType);
            int bowls=p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType);
            if(cleaner){var w=Cleaner(p);Set(w.action,"_time",2f);w.action.PerformAction(w.staff);p.S.Arrive(w.staff);CleanerWait(w.action)();}
            else Invoke(p.S.Table.TableFurniture,"OnTouchEvent");
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.Empty));Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(bowls));
            Assert.That(p.S.Table.CurrentUse,Is.Not.Null,"Cleaning cannot release moving occupants");
        }
    }
    [TestCase("arrival")] [TestCase("work")] [TestCase("reset")] [TestCase("scene")]
    public void CleanerOldCallback_CannotChangeReplacementStaffOrCleaningCycle(string at)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            var w=Cleaner(p);Invoke(w.action,"CleanTableAction",w.staff,p.S.Table);Action callback=Get<Action>(w.staff,"_moveCompleted");
            if(at!="arrival"){p.S.Arrive(w.staff);callback=CleanerWait(w.action);}
            if(at=="reset"){callback();callback=CleanerWait(w.action);p.S.Table.TableState=ETableState.NeedCleaning;}
            if(at=="scene") Invoke(w.staff,"OnChangeSceneEvent");else w.staff.SetStaffData(null,p.S.Table.FloorType);
            Set(w.staff,"_state",EStaffState.Action);callback();callback();
            Assert.That(Get<EStaffState>(w.staff,"_state"),Is.EqualTo(EStaffState.Action));
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
        }
    }
    [Test]
    public void GarbageCleanup_ReentrantStatsAndNotificationException_CountOnlyOnce()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);Garbage(p,p.S.Table.CurrentUse);
            var area=p.S.Table.DropGarbageArea;int count=area.Count;int before=UserInfo.TotalCleanCount;
            UserInfo.OnAddCleanCountHandler+=()=>{area.CleanGarbage();throw new InvalidOperationException("owned cleanup view");};
            area.CleanGarbage();area.CleanGarbage();Assert.That(area.Count,Is.Zero);Assert.That(UserInfo.TotalCleanCount-before,Is.EqualTo(1),"Existing cleanup counts one area action, not each sprite");
        }
    }
    [TestCase("normal")] [TestCase("tutorial")] [TestCase("active")]
    public void PersonalSatisfactionAndFever_UseOwnSkinAndExistingExceptions(string mode)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            var skin=p.S.Track(ScriptableObject.CreateInstance<CustomerSkinData>());Set(skin,"_upgradeType",SkinCustomerUpgradeType.Type4);Set(skin,"_upgradeValue",50f);
            Invoke(p.Party.Members[0].Customer,"ApplySkinEffect",skin);Set(skin,"_upgradeType",SkinCustomerUpgradeType.Type5);Set(skin,"_upgradeValue",100f);Invoke(p.Party.Members[1].Customer,"ApplySkinEffect",skin);
            CookRound(p);Eat(p,p.Batch.Orders[0]);Assert.That(p.S.Stage.Satisfaction,Is.Zero);Assert.That(p.S.Stage.FeverGauge,Is.Zero);
            UserInfo.IsTutorialStart=mode=="tutorial";
            if(mode=="active"){var context=GameManager.Instance.FeverRuntimeContext;context.TryActivate(10,out var token);var system=Get<FeverSystem>(p.S.Manager,"_feverSystem");Set(system,"_feverRuntimeContext",context);Set(system,"_activeFeverToken",token);}
            Eat(p,p.Batch.Orders[1]);p.S.Manager.BeginPartyDeparture(p.S.Table.CurrentUse);
            Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(5));Assert.That(p.S.Stage.FeverGauge,Is.EqualTo(mode=="normal"?60:0));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void UnsupportedFailure_PreservesPersonalTip_AndCookingCallbackCannotOverwriteHold(bool duringCooking)
    {
        using(var p=new Pair(ordersA:1,ordersB:2))
        {
            if(duringCooking){Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.Accepted));p.S.Table.TableType=TableType.Table2;UserInfo.OnAddCookCountHandler+=()=>p.S.Manager.NotFurnitureTable(p.S.Table);p.Pump();p.Pump();}
            else{CookRound(p);Eat(p,p.Batch.Orders[0]);p.S.Manager.AngerExitCustomer(p.S.Table);p.S.TakeWait(.5f)();Assert.That(p.S.Stage.Tip,Is.EqualTo(50));Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(1));Assert.That(p.S.Manager.TryServePartyOrder(p.Batch.Orders[1]),Is.False);}
            Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.PartyPolicyPending));Assert.That(p.S.Manager.HasPendingOrder(p.S.Table),Is.False);Assert.That(p.S.Table.CurrentUse.CompletionStarted,Is.False);
            Assert.That(p.S.Coins.CurrentMoney,Is.Zero);Assert.That(p.S.Table.DropGarbageArea.Count,Is.Zero);
        }
    }
    [Test]
    public void FurnitureRemovedBeforeOrders_HoldsWithoutNullOrLatePreparation()
    {
        using(var s=new Flow())
        {
            Assert.That(s.Controller.TrySpawnWaitingParty(new[]{s.Data.Id,s.Data.Id},out var party),Is.True);
            foreach(var member in party.Members) Get<Action>(member.Customer,"_moveCompleted")();Assert.That(s.Manager.OnCustomerGuideEvent(s.Table.FloorType),Is.True);s.Table.TableType=TableType.Table2;s.Manager.NotFurnitureTable(s.Table);
            foreach(var member in party.Members){member.Customer.transform.position=member.Customer.TargetPos;Get<Action>(member.Customer,"_moveCompleted")();s.TakeWait(1.5f)();}
            Assert.That(s.Table.TableState,Is.EqualTo(ETableState.PartyPolicyPending));Assert.That(s.Table.PartyOrders,Is.Null);Assert.That(party.IsActive,Is.True);
        }
    }
    [Test]
    public void FullSinkDuringAdditionalPreparation_AppliesExistingMinusFourOnlyToOrderingMember()
    {
        using(var p=new Pair(ordersA:1,ordersB:2))
        {
            UserInfo.AddSatisfaction(UserInfo.CurrentStage,100);CookRound(p);Eat(p,p.Batch.Orders[0]);
            while(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage,p.S.Table.FloorType)) UserInfo.AddSinkBowlCount(UserInfo.CurrentStage,p.S.Table.FloorType);
            Eat(p,p.Batch.Orders[1]);Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(ConstValue.MAX_SATISFACTION-4));Assert.That(p.Batch.Orders.Count,Is.EqualTo(1));p.S.Table.RefreshPartyOrderButtons();Assert.That(p.S.Stage.Satisfaction,Is.EqualTo(ConstValue.MAX_SATISFACTION-4));
        }
    }
    [Test]
    public void CleanedDepartingTable_DoesNotHideAnotherEmptyTable()
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);p.S.Table.TableFurniture.OnCleanAction();
            var group=Get<Dictionary<ERestaurantFloorType,FurnitureGroup>>(Get<FurnitureSystem>(p.S.Manager,"_furnitureSystem"),"_furnitureGroupDic")[p.S.Table.FloorType];
            var other=p.S.Create<TableData>();other.TableType=TableType.Table2;other.TableState=ETableState.Empty;Get<Dictionary<TableType,TableData>>(group,"_tableDataDic")[other.TableType]=other;
            Assert.That(group.GetTableType(ETableState.Empty),Is.SameAs(other));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void ConsecutiveVisits_SingleOrPairThenPair_ProduceOneBowlPerCleaningCycle(bool firstPair)
    {
        using(var p=new Pair(ordersA:1,ordersB:1))
        {
            if(firstPair){CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);Garbage(p,p.S.Table.CurrentUse);Tips(p);p.ArriveExit(0);p.ArriveExit(1);}
            else{p.S.Manager.CancelPartyGuide(p.S.Table);p.S.GuideAndSeat();p.S.OrderAndCook(false);p.S.Eat();p.S.OrderAndCook(false);p.S.Eat();p.S.Settle();Get<Action>(p.S.Customer,"_moveCompleted")();}
            p.S.Table.TableFurniture.OnCleanAction();Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(1));
            var oldCycle=p.S.Table.CleaningCycle;
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id,p.SecondData.Id},out p.Party),Is.True);
            foreach(var member in p.Party.Members) Get<Action>(member.Customer,"_moveCompleted")();Assert.That(p.S.Manager.OnCustomerGuideEvent(p.S.Table.FloorType),Is.True);
            foreach(var member in p.Party.Members){member.Customer.transform.position=member.Customer.TargetPos;Get<Action>(member.Customer,"_moveCompleted")();p.S.TakeWait(1.5f)();}
            CookRound(p);foreach(var food in p.Batch.Orders.ToArray()) Eat(p,food);
            Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(2));p.S.Table.TableFurniture.OnCleanAction(oldCycle);Assert.That(p.S.Table.TableState,Is.EqualTo(ETableState.NeedCleaning));
            p.S.Table.TableFurniture.OnCleanAction();Assert.That(p.S.Stage.GetSinkBowlCount(p.S.Table.FloorType),Is.EqualTo(2));
        }
    }
}
#endif
