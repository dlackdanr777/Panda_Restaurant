#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static CustomerTableUseRegressionTests;
using Pair = CustomerPartyOrderTests.Pair;

public sealed class CustomerPartyIntegrationTests
{
    // This is the existing UI UnityEvent chain, not an OS pointer/rendering test.
    // Shop completion is advanced explicitly; real-time UI evidence uses the owned Stage1 preview.
    [TestCase("prepared",false)] [TestCase("cooking",false)] [TestCase("carrying",false)]
    [TestCase("eating",false)] [TestCase("personal-final",false)]
    [TestCase("prepared",true)] [TestCase("cooking",true)] [TestCase("carrying",true)]
    [TestCase("eating",true)] [TestCase("personal-final",true)]
    public void ExistingUnequipButton_FinalKeepOrRemovalPreservesCompletedMoney(string point, bool remove)
    {
        using(var p=new Pair(ordersA:1,ordersB:1,beforeGuide:pair=>
        {
            var s=pair.S; s.Table.TableType=TableType.Table2;
            var group=Group(s); Set(group,"_tableManager",s.Manager);
            Set(group,"_tableDataDic",new Dictionary<TableType,TableData>{{TableType.Table2,s.Table}});
            Set(group,"_tableDataList",new List<TableData>{s.Table});
            s.Table.TableFurniture.SetTableData(s.Table,s.Stage.GetTableData(s.Table.FloorType,TableType.Table2));
        }))
        {
            var s=p.S; var use=s.Table.CurrentUse; var food=p.Batch.Orders[0];
            IEnumerator eating=null; TableServiceReservation lease=null;
            if(point=="cooking") { p.Accept(); p.Pump(); }
            if(point=="carrying"||point=="eating"||point=="personal-final")
            {
                CustomerPartyCompletionTests.CookRound(p);
                if(point=="carrying")
                { var worker=s.CreateWaiter();lease=s.Manager.ReserveWaiterWork(s.Table,worker.staff,worker.action,true);Assert.That(s.Manager.PickUpWaiterFood(lease),Is.True); }
                if(point=="eating")
                { s.Manager.TryServePartyOrder(food);s.Manager.StopAllCoroutines();eating=(IEnumerator)Invoke(s.Manager,"EatPartyOrder",food);eating.MoveNext();eating.MoveNext(); }
                if(point=="personal-final") { CustomerPartyCompletionTests.Eat(p,food);s.TakeWait(.5f)(); }
            }
            LoadFurnitureCatalog(s);
            var art=FurnitureDataManager.Instance.GetFurnitureDataList(FurnitureType.Table2).First(d=>d.Id.StartsWith("TABLE01"));
            Get<FurnitureData[,]>(s.Stage,"_equipFurnitureDatas")[0,(int)FurnitureType.Table2]=art;
            Get<List<string>>(s.Stage,"_giveFurnitureList").Add(art.Id);
            var relay=(Action<ERestaurantFloorType,FurnitureType>)Delegate.CreateDelegate(typeof(Action<ERestaurantFloorType,FurnitureType>),
                typeof(UserInfo).GetMethod("OnChangeFurnitureEvent",BindingFlags.Static|BindingFlags.NonPublic));
            s.Stage.OnChangeFurnitureHandler+=relay;
            var enabled=(Action<ERestaurantFloorType,FurnitureType>)Delegate.CreateDelegate(typeof(Action<ERestaurantFloorType,FurnitureType>),Group(s),
                typeof(FurnitureGroup).GetMethod("CheckTableEnabled",BindingFlags.Instance|BindingFlags.NonPublic));
            s.Swap(typeof(UserInfo),"OnChangeFurnitureHandler",enabled);
            var shop=s.Create<UIFurniture>(); Set(shop,"_currentType",FurnitureType.Table2);
            var preview=s.Create<UIFurniturePreview>(); Set(preview,"_currentData",art);
            foreach(string name in new[]{"_buyButton","_notEnoughMoneyButton","_equipButton"})
            { var view=s.Create<UIButtonAndText>();Set(view,"_button",s.Create<Button>());Set(preview,name,view); }
            var usingView=s.Create<UI3LayerButton>();var button=s.Create<Button>();Set(usingView,"_button",button);Set(preview,"_usingButton",usingView);
            var equip=(Action<ERestaurantFloorType,FurnitureData>)Delegate.CreateDelegate(typeof(Action<ERestaurantFloorType,FurnitureData>),shop,
                typeof(UIFurniture).GetMethod("OnEquipButtonClicked",BindingFlags.Instance|BindingFlags.NonPublic));
            var buy=(Action<FurnitureData>)Delegate.CreateDelegate(typeof(Action<FurnitureData>),shop,
                typeof(UIFurniture).GetMethod("OnBuyButtonClicked",BindingFlags.Instance|BindingFlags.NonPublic));
            preview.Init(equip,buy);
            int visits=UserInfo.TotalCumulativeCustomerCount,tip=s.Stage.Tip;
            var token=s.Manager.BeginFurnitureEdit(); var batch=p.Batch;
            var identities=batch.Orders.Select(f=>f.Identity).ToArray();
            var times=batch.Orders.Select(f=>f.PreparedAt).ToArray();
            button.onClick.Invoke();
            Assert.That(UserInfo.GetEquipFurniture(EStage.Stage1,ERestaurantFloorType.Floor1,FurnitureType.Table2),Is.Null);
            Assert.That(use.PolicyPending||use.CompletionStarted,Is.False);
            Assert.That(s.Table.CurrentUse,Is.SameAs(use));Assert.That(s.Table.OccupantCount,Is.EqualTo(2));
            if(lease!=null)Assert.That(lease.IsCurrent,Is.True);
            if(!remove) equip(ERestaurantFloorType.Floor1,art);
            s.Manager.CompleteFurnitureEdit(token);s.Manager.CompleteFurnitureEdit(token);
            Assert.That(s.Stage.Tip,Is.EqualTo(tip));Assert.That(UserInfo.TotalCumulativeCustomerCount,Is.EqualTo(visits));
            Assert.That(use.PolicyPending,Is.False);
            if(!remove)
            {
                Assert.That(use.CompletionStarted||use.ServiceAborted,Is.False);
                Assert.That(p.Batch,Is.SameAs(batch));Assert.That(batch.Orders.Select(f=>f.Identity),Is.EqualTo(identities));
                Assert.That(batch.Orders.Select(f=>f.PreparedAt),Is.EqualTo(times));
                if(lease!=null) Assert.That(lease.IsCurrent,Is.True);
                foreach(var m in p.Party.Members) Assert.That(m.Customer.transform.position,Is.EqualTo(s.Table.GetSeatPosition(m.SeatIndex)));
                Assert.That(s.Coins.CurrentMoney,Is.Zero);Assert.That(s.Stage.Satisfaction,Is.Zero);
            }
            else
            {
                Assert.That(use.FurnitureRemoved&&use.ServiceAborted&&use.CompletionStarted,Is.True);
                Assert.That(s.Stage.Satisfaction,Is.EqualTo(-10));Assert.That(use.Completions.All(c=>c.ExitStarted),Is.True);
                Assert.That(s.Coins.CurrentMoney,Is.EqualTo(point=="personal-final"?101:0));
                if(lease!=null)Assert.That(lease.Released,Is.True);
                if(eating!=null)Assert.That(eating.MoveNext(),Is.False);
                p.ArriveExit(0);Assert.That(s.Table.CurrentUse,Is.SameAs(use));p.ArriveExit(1);
                Assert.That(s.Table.CurrentUse,Is.Null);Assert.That(s.Table.TableState,Is.EqualTo(ETableState.DontUse));
                equip(ERestaurantFloorType.Floor1,art);
                Assert.That(s.Table.TableState,Is.EqualTo(point=="eating"||point=="personal-final"?ETableState.NeedCleaning:ETableState.Empty));
                s.Table.TableFurniture.OnCleanAction();s.Table.TableFurniture.OnCleanAction();
                Assert.That(s.Coins.CurrentMoney,Is.EqualTo(point=="personal-final"?101:0));
                Assert.That(s.Stage.Satisfaction,Is.EqualTo(-10));
            }

        }
    }
    private static FurnitureGroup Group(Flow s) => Get<Dictionary<ERestaurantFloorType,FurnitureGroup>>(Get<FurnitureSystem>(s.Manager,"_furnitureSystem"),"_furnitureGroupDic")[s.Table.FloorType];
    internal static void LoadFurnitureCatalog(Flow s)
    {
        var type=typeof(FurnitureDataManager);
        foreach(var field in type.GetFields(BindingFlags.NonPublic|BindingFlags.Static))
        {
            if(field.IsLiteral||field.IsInitOnly||field.Name=="_instance")continue;
            var old=field.GetValue(null);object replacement=old;
            if(old is Array array) replacement=Array.CreateInstance(field.FieldType.GetElementType(),array.Length);
            else if(old!=null && !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) replacement=Activator.CreateInstance(field.FieldType);
            s.Swap(type,field.Name,replacement);
        }
        s.Swap(type,"_instance",s.Create<FurnitureDataManager>());
        foreach(var name in new[]{"TableData","CounterData","RackData","FrameData","FlowerData","AccData","WallpaperData"})
            foreach(var row in Resources.Load<TextAsset>("FurnitureData/CSVData/"+name).text.Split('\n').Skip(1))
                if(string.IsNullOrWhiteSpace(row.Split(',')[0])) UnityEngine.TestTools.LogAssert.Expect(LogType.Error,"Id값이 이상합니다: ");
        type.GetMethod("InitData",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    }
}
#endif
