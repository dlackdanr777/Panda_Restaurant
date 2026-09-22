#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GachaEconomyTests
{
    private readonly List<Object> _created = new List<Object>();
    private GachaEconomySettings _settings;
    private List<GachaData> _catalog;
    private UnityEngine.Random.State _random;
    [SetUp] public void SetUp()
    {
        _random = UnityEngine.Random.state;
        _settings = ScriptableObject.CreateInstance<GachaEconomySettings>(); _created.Add(_settings);
        _catalog = new List<GachaData>();
        foreach (Rank rank in new[] { Rank.Normal2, Rank.Rare, Rank.Unique, Rank.Special })
        {
            var source = Resources.LoadAll<StaffData>("StaffData").First(s => s.Rank == rank);
            var wrapper = GachaStaffData.Create(source); _created.Add(wrapper); _catalog.Add(wrapper);
        }
        _catalog.Add(Item("ITEM-A", Rank.Normal2)); _catalog.Add(Item("ITEM-S", Rank.Special));
        _catalog.Add(Item("RECIPE-A", Rank.Rare, UpgradeType.None));
    }
    [TearDown] public void TearDown()
    {
        foreach (Object item in _created) if (item != null) Object.DestroyImmediate(item);
        _created.Clear();
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random), "Deterministic economy tests changed gameplay RNG");
    }
    private GachaItemData Item(string id, Rank rank, UpgradeType type = UpgradeType.UPGRADE01)
    {
        var item = ScriptableObject.CreateInstance<GachaItemData>(); _created.Add(item);
        typeof(BasicData).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, id);
        typeof(BasicData).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, id);
        typeof(GachaData).GetField("_rank", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, rank);
        typeof(GachaItemData).GetField("_upgradeType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, type);
        return item;
    }
    private GachaEconomySnapshot Initial(long tokens = 1000, int itemCounter = 0, int staffCounter = 0,
        int itemTickets = 0, int staffTickets = 0, IEnumerable<string> owned = null,
        Dictionary<string,int> counts = null, Dictionary<string,int> levels = null, IEnumerable<string> history = null)
        => new GachaEconomySnapshot("offline:test", new StaffAccountSaveData(2,
            (owned ?? Array.Empty<string>()).Select(x => new StaffAccountStaffRecord(x,1)).ToArray(), tokens,
            new GachaEconomySaveData(itemTickets,staffTickets,itemCounter,staffCounter,history, exchange: SavedTestOffers())), 1000, counts,levels);
    // These tests begin with previously persisted offers; dedicated catalog tests cover first creation.
    private GachaExchangeCatalogSaveData SavedTestOffers()
        => new GachaExchangeCatalogSaveData(
            new GachaExchangeDisplaySaveData(1, _catalog.OfType<GachaStaffData>().Where(x => GachaEconomyService.CanPurchaseStaff(x.Rank))
                .OrderByDescending(x => x.Rank).ThenBy(x => x.Id, StringComparer.Ordinal)
                .Select(x => new GachaExchangeSlotSaveData("staff:" + x.Id, GachaEconomyService.StaffPrice(x.Rank), 1))),
            new GachaExchangeDisplaySaveData(1, _catalog.OfType<GachaItemData>().OrderByDescending(x => x.Rank).ThenBy(x => x.Id, StringComparer.Ordinal)
                .Select(x => new GachaExchangeSlotSaveData("item:" + x.Id, _settings.ItemPrice(x.Rank), 1))));
    private GachaEconomyService Service(GachaEconomyMemoryStore store, Func<GachaMachineKind,IReadOnlyList<GachaData>,bool,GachaData> draw = null)
        => new GachaEconomyService(store,_catalog,_settings,draw ?? ((m,p,g) => p[0]));
    private GachaStaffData Staff(Rank rank) => _catalog.OfType<GachaStaffData>().First(x => x.Rank == rank);

    [TestCase(Rank.Normal2,40)] [TestCase(Rank.Rare,100)]
    public void StaffExchange_UsesOfficialRankPrice_OnceOnly_NoRefund(Rank rank,int cost)
    {
        var store = new GachaEconomyMemoryStore(Initial()); var service = Service(store); var staff = Staff(rank);
        Assert.That(service.TryExchange("staff:"+staff.Id,1,null,out string error),Is.True,error);
        Assert.That(service.Snapshot.Account.PandaTokens,Is.EqualTo(1000-cost));
        Assert.That(service.LastTransaction.Results.Single().IsNew,Is.True);
        Assert.That(service.LastTransaction.Results.Single().PandaTokenReward,Is.Zero);
        Assert.That(service.TryExchange("staff:"+staff.Id,1,null,out error),Is.False);
        Assert.That(store.CommitCount,Is.EqualTo(1));
    }
    [TestCase(Rank.Unique)] [TestCase(Rank.Special)]
    public void ForbiddenStaff_DirectPolicyCallRejectedWithoutAnySave(Rank rank)
    {
        var store = new GachaEconomyMemoryStore(Initial()); var service = Service(store);
        Assert.That(GachaEconomyService.CanPurchaseStaff(rank),Is.False);
        Assert.That(service.TryExchange("staff:"+Staff(rank).Id,1,null,out _),Is.False);
        Assert.That(store.SaveAttempts,Is.Zero); Assert.That(service.Snapshot.Account.PandaTokens,Is.EqualTo(1000));
    }
    [TestCase(0)] [TestCase(-1)] [TestCase(int.MaxValue)]
    public void InvalidQuantityAndProduct_NoMutation(int quantity)
    {
        var store = new GachaEconomyMemoryStore(Initial());var service=Service(store);
        Assert.That(service.TryExchange("ticket:staff",quantity,null,out _),Is.False);
        Assert.That(service.TryExchange("missing",1,null,out _),Is.False); Assert.That(store.CommitCount,Is.Zero);
    }
    [Test] public void InsufficientTokens_AndZeroConfiguredPrice_AreRejected()
    {
        var store=new GachaEconomyMemoryStore(Initial(0));var service=Service(store);
        Assert.That(service.TryExchange("ticket:staff",1,null,out _),Is.False);
        _settings.StaffTicketPrice=0;service=Service(new GachaEconomyMemoryStore(Initial()));
        Assert.That(service.TryExchange("ticket:staff",1,null,out _),Is.False);
    }
    [TestCase("tokens")] [TestCase("tickets")] [TestCase("items")]
    public void ArithmeticOverflow_RejectsTheEntireTransactionBeforeSave(string resource)
    {
        var staff = Staff(Rank.Normal2);
        var item = _catalog.OfType<GachaItemData>().First();
        var initial = resource == "tokens" ? Initial(long.MaxValue, owned: new[] { staff.Id })
            : resource == "tickets" ? Initial(staffTickets: int.MaxValue)
            : Initial(counts: new Dictionary<string,int> { { item.Id, int.MaxValue } }, levels: new Dictionary<string,int> { { item.Id, 1 } });
        var store = new GachaEconomyMemoryStore(initial); var service = Service(store);
        bool accepted = resource == "tokens"
            ? service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, null, out _)
            : service.TryExchange(resource == "tickets" ? "ticket:staff" : "item:" + item.Id, 1, null, out _);
        Assert.That(accepted, Is.False);
        Assert.That(store.SaveAttempts, Is.Zero);
        Assert.That(initial.HasSameState(service.Snapshot), Is.True, "Overflow must not debit or pollute acquisition history");
    }
    [Test] public void TicketPurchaseAndUse_AreSeparate_MachineSpecific_NoDiamondsOrBonus()
    {
        var store=new GachaEconomyMemoryStore(Initial());var service=Service(store);
        Assert.That(service.TryExchange("ticket:staff",10,null,out string error),Is.True,error);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffTickets,Is.EqualTo(10));
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.Zero);
        Assert.That(service.TryDraw(GachaMachineKind.Item,GachaPaymentKind.TicketSingle,null,out _),Is.False);
        for(int i=0;i<10;i++) Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.TicketSingle,null,out error),Is.True,error);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffTickets,Is.Zero);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.EqualTo(10));
        Assert.That(service.Snapshot.Diamonds,Is.EqualTo(1000)); Assert.That(service.Snapshot.TotalDrawCount,Is.EqualTo(10));
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.TicketSingle,null,out _),Is.False);
        Assert.That(service.Snapshot.Diamonds,Is.EqualTo(1000));
    }
    [Test] public void StaffBatch_IsNewIsFrozenSequentially_AndDuplicateRewardRetained()
    {
        var store=new GachaEconomyMemoryStore(Initial());var service=Service(store);
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.DiamondsEleven,null,out string error),Is.True,error);
        var tx=service.LastTransaction;
        Assert.That(tx.Results.Select(x=>x.IsNew),Is.EqualTo(new[]{true}.Concat(Enumerable.Repeat(false,10))));
        Assert.That(tx.Results.Sum(x=>x.PandaTokenReward),Is.EqualTo(50));
        Assert.That(tx.After.Diamonds,Is.EqualTo(900));
        Assert.That(tx.Results[0].IsNew,Is.True,"Reopening must reuse the immutable result after ownership commit");
    }
    [Test] public void ItemBatch_NewDuplicateNew_UsesActualInventoryAndNoTokenRefund()
    {
        var store=new GachaEconomyMemoryStore(Initial()); int index=0;
        var a=_catalog.OfType<GachaItemData>().First(x=>x.Id=="ITEM-A"); var b=_catalog.OfType<GachaItemData>().First(x=>x.Id=="RECIPE-A");
        var service=Service(store,(m,p,g)=>index++==2?b:a);
        Assert.That(service.TryDraw(GachaMachineKind.Item,GachaPaymentKind.DiamondsEleven,null,out _),Is.True);
        Assert.That(service.LastTransaction.Results.Take(3).Select(x=>x.IsNew),Is.EqualTo(new[]{true,false,true}));
        Assert.That(service.LastTransaction.Results[2].Kind,Is.EqualTo(GachaAcquisitionKind.Recipe));
        Assert.That(service.Snapshot.ItemCounts[a.Id],Is.EqualTo(10)); Assert.That(service.Snapshot.ItemCounts[b.Id],Is.EqualTo(1));
        Assert.That(service.Snapshot.RecipeLevels.Count,Is.Zero,"Recipe gacha unlock still uses original required-item inventory");
        Assert.That(service.Snapshot.Account.PandaTokens,Is.EqualTo(1000));
    }
    [Test] public void RecipeExchange_UnlocksOnce_RejectsBulkAndRepeat_WithoutChangingGachaDuplicates()
    {
        var store = new GachaEconomyMemoryStore(Initial()); var service = Service(store);
        const string id = "RECIPE-A";
        Assert.That(service.Products.Single(product => product.Id == "item:" + id).IsRepeatable, Is.False);
        Assert.That(service.TryExchange("item:" + id, 2, null, out _), Is.False);
        Assert.That(store.SaveAttempts, Is.Zero);
        Assert.That(service.TryExchange("item:" + id, 1, null, out string error), Is.True, error);
        Assert.That(service.LastTransaction.Results.Single().Kind, Is.EqualTo(GachaAcquisitionKind.Recipe));
        Assert.That(service.LastTransaction.Results.Single().IsNew, Is.True);
        Assert.That(service.Snapshot.ItemCounts[id], Is.EqualTo(1));
        Assert.That(service.Snapshot.ItemLevels[id], Is.EqualTo(1));
        Assert.That(service.Snapshot.RecipeLevels, Is.Empty, "Unlocks retain the existing required-item path");
        Assert.That(service.Snapshot.Account.PandaTokens, Is.EqualTo(900));
        Assert.That(service.TryExchange("item:" + id, 1, null, out _), Is.False);
        Assert.That(store.CommitCount, Is.EqualTo(1));
        var recipe = _catalog.Single(data => data.Id == id);
        var drawService = Service(store, (machine, pool, guaranteed) => recipe);
        Assert.That(drawService.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out error), Is.True, error);
        Assert.That(drawService.LastTransaction.Results.Single().IsNew, Is.False);
        Assert.That(drawService.Snapshot.ItemCounts[id], Is.EqualTo(2), "Existing random-draw duplicate materials are preserved");
        Assert.That(drawService.Snapshot.Account.PandaTokens, Is.EqualTo(900));
    }
    [TestCase("zero-count")] [TestCase("level")] [TestCase("history")]
    public void RecipeExchange_PreviousUnlockEvidenceRejectsRepurchase(string evidence)
    {
        const string id = "RECIPE-A";
        var before = Initial(counts: evidence == "zero-count" ? new Dictionary<string,int>{{id,0}} : null,
            levels: evidence == "level" ? new Dictionary<string,int>{{id,1}} : null,
            history: evidence == "history" ? new[]{GachaEconomySaveData.Key(GachaAcquisitionKind.Recipe,id)} : null);
        var store = new GachaEconomyMemoryStore(before); var service = Service(store);
        Assert.That(service.CanExchange("item:" + id, 1, out string error), Is.False);
        Assert.That(error, Does.Contain("이미 해금"));
        Assert.That(service.TryExchange("item:" + id, 1, null, out _), Is.False);
        Assert.That(store.SaveAttempts, Is.Zero); Assert.That(store.Capture().HasSameState(before), Is.True);
    }
    [Test] public void ConsumedItemAndOldAccountHistory_AreNotNew_AndKindsRemainIndependent()
    {
        var item=_catalog.OfType<GachaItemData>().First();
        var store=new GachaEconomyMemoryStore(Initial(counts:new Dictionary<string,int>{{item.Id,0}},levels:new Dictionary<string,int>{{item.Id,2}}));
        var service=Service(store);
        Assert.That(service.TryExchange("item:"+item.Id,1,null,out _),Is.True);
        Assert.That(service.LastTransaction.Results[0].IsNew,Is.False);
        Assert.That(service.Snapshot.ItemLevels[item.Id],Is.EqualTo(2));
        var history=new[]{GachaEconomySaveData.Key(GachaAcquisitionKind.Staff,item.Id)};
        service=Service(new GachaEconomyMemoryStore(Initial(history:history)));
        Assert.That(service.TryExchange("item:"+item.Id,1,null,out _),Is.True);Assert.That(service.LastTransaction.Results[0].IsNew,Is.True);
    }
    [TestCase(0,10,-1)] [TestCase(89,99,-1)] [TestCase(90,0,9)] [TestCase(98,8,1)] [TestCase(99,9,0)]
    public void ElevenDrawBoundary_GuaranteesExactHundredthAndKeepsRemainder(int start,int after,int guaranteedIndex)
    {
        var store=new GachaEconomyMemoryStore(Initial(staffCounter:start)); var service=Service(store);
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.DiamondsEleven,null,out string error),Is.True,error);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.EqualTo(after));
        Assert.That(service.LastTransaction.Results.Count(r=>r.IsGuaranteedSpecial),Is.EqualTo(guaranteedIndex < 0 ? 0 : 1));
        if (guaranteedIndex >= 0)
        {
            Assert.That(service.LastTransaction.Results[guaranteedIndex].Rank,Is.EqualTo(Rank.Special));
            Assert.That(service.LastTransaction.Results[guaranteedIndex].CounterBefore,Is.EqualTo(99));
            Assert.That(service.LastTransaction.Results[guaranteedIndex].CounterAfter,Is.Zero);
        }
        Assert.That(service.LastTransaction.Results.Last().IsBonus, Is.True);
        Assert.That(service.LastTransaction.Results.Last().CounterBefore, Is.EqualTo(after));
        Assert.That(service.LastTransaction.Results.Last().CounterAfter, Is.EqualTo(after));
        Assert.That(service.Snapshot.Account.GachaEconomy.ItemCounter,Is.Zero);
    }
    [Test] public void NinetyEightToNinetyNineToHundred_NaturalSpecialDoesNotReset_TicketsCount()
    {
        var service=Service(new GachaEconomyMemoryStore(Initial(staffCounter:98,staffTickets:2)),(m,p,g)=>p.Last(x=>x.Rank==Rank.Special));
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.TicketSingle,null,out _),Is.True);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.EqualTo(99));Assert.That(service.LastTransaction.Results[0].IsGuaranteedSpecial,Is.False);
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.TicketSingle,null,out _),Is.True);
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.Zero);Assert.That(service.LastTransaction.Results[0].IsGuaranteedSpecial,Is.True);
    }
    [Test] public void ItemMachineHasItsOwnGuarantee_StaffCounterDoesNotChange()
    {
        var service=Service(new GachaEconomyMemoryStore(Initial(itemCounter:99,staffCounter:37)));
        Assert.That(service.TryDraw(GachaMachineKind.Item,GachaPaymentKind.DiamondsSingle,null,out _),Is.True);
        Assert.That(service.LastTransaction.Results[0].Rank,Is.EqualTo(Rank.Special));
        Assert.That(service.Snapshot.Account.GachaEconomy.ItemCounter,Is.Zero);Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.EqualTo(37));
    }
    [Test] public void MissingGuaranteedCandidate_RejectsBeforeDrawSaveAndPayment()
    {
        _catalog.RemoveAll(x=>x is GachaStaffData && x.Rank==Rank.Special);
        var store=new GachaEconomyMemoryStore(Initial(staffCounter:99));int calls=0;var service=Service(store,(m,p,g)=>{calls++;return p[0];});
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.DiamondsSingle,null,out _),Is.False);
        Assert.That(calls,Is.Zero);Assert.That(store.SaveAttempts,Is.Zero);Assert.That(service.Snapshot.Diamonds,Is.EqualTo(1000));
    }
    [Test] public void FailedSaveRetry_ReusesSameIdResultsAndCounter_NoHistoryPollution()
    {
        var store=new GachaEconomyMemoryStore(Initial(staffCounter:99)){FailNextSave=true};int calls=0;
        var service=Service(store,(m,p,g)=>{calls++;return p[0];});
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.DiamondsSingle,null,out _),Is.False);
        var tx=service.LastTransaction;Assert.That(tx.Status,Is.EqualTo(GachaTransactionStatus.Rejected));
        Assert.That(service.Snapshot.Account.Staff.Count,Is.Zero);Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.EqualTo(99));
        Assert.That(service.Snapshot.Account.GachaEconomy.Acquired.Count,Is.Zero);
        Assert.That(service.RetryRejected(null,out string error),Is.True,error);
        Assert.That(service.LastTransaction,Is.SameAs(tx));Assert.That(calls,Is.EqualTo(1));Assert.That(store.CommitCount,Is.EqualTo(1));
        Assert.That(service.Snapshot.Account.GachaEconomy.StaffCounter,Is.Zero);
    }
    [Test] public void PendingAndUnknownSave_BlockEveryMachineAndExchange_OriginalLateSuccessCommitsOnce()
    {
        var store=new GachaEconomyMemoryStore(Initial()){DeferNextSave=true};var service=Service(store);int events=0;
        service.Committed+=_=>events++;
        Assert.That(service.TryDraw(GachaMachineKind.Staff,GachaPaymentKind.DiamondsSingle,null,out _),Is.True);
        Assert.That(service.TryExchange("ticket:item",1,null,out _),Is.False);
        Assert.That(service.TryDraw(GachaMachineKind.Item,GachaPaymentKind.DiamondsSingle,null,out _),Is.False);
        store.CompletePending(GachaStoreResult.Indeterminate);Assert.That(service.IsBusy,Is.True);
        Assert.That(service.RetryRejected(null,out _),Is.False);Assert.That(store.SaveAttempts,Is.EqualTo(1));
        store.CompletePending(GachaStoreResult.Confirmed);store.CompletePending(GachaStoreResult.Confirmed);
        Assert.That(events,Is.EqualTo(1));Assert.That(store.CommitCount,Is.EqualTo(1));Assert.That(service.Snapshot.Diamonds,Is.EqualTo(990));
    }
    [Test] public void ExchangeAndTicketState_RoundTripReceiptAndNewFlags()
    {
        var store=new GachaEconomyMemoryStore(Initial(itemCounter:98,staffCounter:99));var service=Service(store);
        Assert.That(service.TryExchange("ticket:item",1,null,out _),Is.True);
        Assert.That(service.TryDraw(GachaMachineKind.Item,GachaPaymentKind.TicketSingle,null,out _),Is.True);
        var restored=GachaEconomyMemoryStore.Restore(store.SavedJson);
        Assert.That(restored.HasSameState(service.Snapshot),Is.True);
        var receipt=JObject.Parse(restored.Account.GachaEconomy.LastTransactionJson);
        Assert.That((bool)receipt["Results"][0]["IsNew"],Is.True);
        Assert.That(restored.Account.GachaEconomy.ItemCounter,Is.EqualTo(99));Assert.That(restored.Account.GachaEconomy.StaffCounter,Is.EqualTo(99));
    }
    [Test] public void LegacyV1_DefaultsNewFieldsToZero_AndV2RejectsMalformedCounters()
    {
        var old=StaffAccountSaveConverter.Read("{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":3}],\"PandaTokens\":55}");
        Assert.That(old.Status,Is.EqualTo(StaffAccountSaveReadStatus.Success));Assert.That(old.Data.Staff.Single().Level,Is.EqualTo(3));
        Assert.That(old.Data.GachaEconomy.ItemCounter,Is.Zero);Assert.That(old.Data.GachaEconomy.StaffTickets,Is.Zero);
        Assert.That(StaffAccountSaveConverter.TrySerialize(Initial().Account,out string json,out _),Is.True);
        var root=JObject.Parse(json);root["GachaEconomy"]["StaffCounter"]=100;
        Assert.That(StaffAccountSaveConverter.Read(root.ToString()).Status,Is.EqualTo(StaffAccountSaveReadStatus.InvalidData));
    }
    [Test] public void EconomyConservation_FullCollectionTicketRebateIncludingGuaranteeBelowCost()
    {
        // Exact 100-draw expectation: 99 ordinary independent draws + one guaranteed Special.
        double ordinary=0.60*5+0.20*10+0.15*20+0.05*50;
        double perDraw=(99*ordinary+50)/100;
        Assert.That(perDraw,Is.EqualTo(10.895).Within(0.000001));
        Assert.That(perDraw/_settings.StaffTicketPrice,Is.LessThan(1));
        Assert.That(GachaEconomyService.StaffPrice(Rank.Normal2),Is.GreaterThan(GachaEconomyService.DuplicateReward(Rank.Normal2)));
        Assert.That(GachaEconomyService.StaffPrice(Rank.Rare),Is.GreaterThan(GachaEconomyService.DuplicateReward(Rank.Rare)));
    }
}
#endif
