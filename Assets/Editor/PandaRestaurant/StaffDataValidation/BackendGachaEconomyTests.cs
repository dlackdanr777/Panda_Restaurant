#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void EconomyBackend_OneAtomicPayload_AcknowledgementAndDuplicateCallback_UsesRealRuntime()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var service = CreateEconomyService(fixture, out var cleanup);
            try
            {
                Assert.That(service.TryEnsureExchangeCatalog(null, out string setupError), Is.True, setupError);
                fixture.Game.WriteReplies[0](Bro("204", ""));
                if (fixture.Game.WriteReplies.Count > 1) fixture.Game.WriteReplies[1](Bro("204", ""));
                int purchaseWrite = fixture.Game.Writes;
                int commits = 0; service.Committed += _ => commits++;
                var before = service.Snapshot;
                Assert.That(service.TryExchange("item:OFFLINE-ECONOMY-ITEM", 1, null, out string error), Is.True, error);
                Assert.That(fixture.Game.Writes, Is.EqualTo(purchaseWrite + 1));
                Assert.That(service.Snapshot.HasSameState(before), Is.True, "No debit or grant before acknowledgement");
                Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out _), Is.False);
                Assert.That(UserInfo.GiveGachaItem((GachaItemData)service.Products.First(p => p.Category == GachaExchangeCategory.Items).Data), Is.False,
                    "Ordinary inventory mutations must respect the same reservation");
                var payload = JObject.Parse(fixture.Game.WriteValues[purchaseWrite].GetJson());
                Assert.That(payload["Dia"], Is.Null, "Token exchange must not overwrite independently changed diamonds");
                var account = StaffAccountSaveConverter.Read((string)payload["StaffAccount"]).Data;
                Assert.That(account.PandaTokens, Is.EqualTo(15));
                Assert.That(account.GachaEconomy.LastTransactionId, Is.EqualTo(service.LastTransaction.Id));
                Assert.That((int)payload["GiveGachaItemCountList"][0]["Count"], Is.EqualTo(1));
                Assert.That((int)payload["GiveGachaItemLevelList"][0]["Level"], Is.EqualTo(1));
                fixture.Game.WriteReplies[purchaseWrite](Bro("204", ""));
                fixture.Game.WriteReplies[purchaseWrite](Bro("204", ""));
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(service.LastTransaction.IsCompleted, Is.True);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(15));
                Assert.That(UserInfo.GetGiveGachaItemCountDic()["OFFLINE-ECONOMY-ITEM"], Is.EqualTo(1));
                Assert.That(UserInfo.Dia, Is.EqualTo(110));
                Assert.That(service.LastTransaction.Results[0].IsNew, Is.True);
                Assert.That(service.IsBusy, Is.True, "Queued current-state save must still keep exchange unavailable");
                if (fixture.Game.WriteReplies.Count > purchaseWrite + 1) fixture.Game.WriteReplies[purchaseWrite + 1](Bro("204", ""));
                Assert.That(service.IsBusy, Is.False, "Polling must observe readiness after asynchronous coordinator completion");
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    [Test]
    public void EconomyBackend_UnknownResponse_DoesNotRerollOrDebitTwice_OriginalSuccessSettles()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var service = CreateEconomyService(fixture, out var cleanup);
            try
            {
                Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                var tx = service.LastTransaction;
                fixture.Game.WriteReplies[0](Bro("500", ""));
                Assert.That(tx.Status, Is.EqualTo(GachaTransactionStatus.Indeterminate));
                Assert.That(service.RetryRejected(null, out _), Is.False);
                Assert.That(service.TryExchange("ticket:item", 1, null, out _), Is.False);
                Assert.That(UserInfo.Dia, Is.EqualTo(110));
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(tx.IsCompleted, Is.True);
                Assert.That(UserInfo.Dia, Is.EqualTo(100));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.GachaEconomy.StaffCounter, Is.EqualTo(1));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(100));
                if (fixture.Game.WriteReplies.Count > 1) fixture.Game.WriteReplies[1](Bro("204", ""));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    [Test]
    public void EconomyBackend_AccountInvalidation_DoesNotApplyOldCallbackOrRetainGlobalInventoryFence()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var service = CreateEconomyService(fixture, out var cleanup);
            try
            {
                Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                fixture.Manager.InvalidateGameDataRestore();
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(110));
                Assert.That(service.Snapshot, Is.Null);
                Assert.That(service.LastTransaction.IsCompleted, Is.False);
                Assert.That(typeof(UserInfo).GetProperty("EconomyInventoryReservation", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                Assert.That(fixture.Manager.IsGachaEconomyProtected, Is.False);
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator, Is.Null);
            }
            finally { cleanup(); }
        }
    }

    [TestCase(GachaPaymentKind.DiamondsSingle)]
    [TestCase(GachaPaymentKind.DiamondsEleven)]
    [TestCase(GachaPaymentKind.TicketSingle)]
    public void EconomyBackend_FreeQuestOfferBlocksStaffDrawBeforeRngAndPayment(GachaPaymentKind payment)
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                new[] { new StaffAccountStaffRecord("STAFF03", 4) }, 55, new GachaEconomySaveData(staffTickets: 1));
            Assert.That(StaffAccountSaveConverter.TrySerialize(account, out string json, out _), Is.True);
            RestoreAccountJson(fixture, json);
            int draws = 0;
            var service = CreateEconomyService(fixture, out var cleanup, () => draws++);
            var state = new MemoryQuestStaffState { Quest = "MainReward05" };
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            try
            {
                var before = service.Snapshot;
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out string offerError), Is.True, offerError);
                Assert.That(service.CanDraw(GachaMachineKind.Staff, payment, out string error), Is.False);
                Assert.That(error, Does.Contain("무료 직원"));
                Assert.That(service.TryDraw(GachaMachineKind.Staff, payment, null, out _), Is.False);
                Assert.That(draws, Is.Zero); Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(service.Snapshot.HasSameState(before), Is.True);
                Assert.That(service.CanDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, out _), Is.True);
                state.Done = true;
                Assert.That(service.CanDraw(GachaMachineKind.Staff, payment, out error), Is.True, error);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    [Test]
    public void EconomyBackend_QuestAdmissionRecheckedAfterSelectionBeforeSaveReservation()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
            var state = new MemoryQuestStaffState();
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            int draws = 0;
            var service = CreateEconomyService(fixture, out var cleanup, () => { draws++; state.Quest = "MainReward05"; });
            try
            {
                var before = service.Snapshot;
                Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, null, out string error), Is.False);
                Assert.That(error, Does.Contain("무료 직원"));
                Assert.That(draws, Is.EqualTo(1)); Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(service.LastTransaction.Status, Is.EqualTo(GachaTransactionStatus.Rejected));
                Assert.That(service.Snapshot.HasSameState(before), Is.True);
                Assert.That(service.RetryRejected(null, out _), Is.False);
                Assert.That(draws, Is.EqualTo(1));
                Assert.That(typeof(UserInfo).GetProperty("EconomyInventoryReservation", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    private GachaEconomyService CreateEconomyService(Fixture fixture, out Action cleanup, Action onDraw = null)
    {
        if (Field(typeof(BackendManager), "_questStaffTutorialState").GetValue(fixture.Manager) == null)
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, new MemoryQuestStaffState());
        fixture.Manager.StaffPurchaseWallet = UserInfo.StaffPurchaseWallet;
        fixture.Game.LatestFactory = () => { var values = new Param(); values.Add("Dia", UserInfo.Dia); return values; };
        var config = ScriptableObject.CreateInstance<GachaEconomySettings>();
        var item = ScriptableObject.CreateInstance<GachaItemData>();
        typeof(BasicData).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, "OFFLINE-ECONOMY-ITEM");
        typeof(BasicData).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, "Offline economy item");
        typeof(GachaData).GetField("_rank", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, Rank.Normal2);
        var wrappers = fixture.Game.Catalog.Select(GachaStaffData.Create).ToArray();
        var catalog = new List<GachaData>(wrappers) { item };
        var type = typeof(BackendManager).Assembly.GetType("Muks.BackEnd.BackendGachaEconomyStore", true);
        var restore = (GameDataRestoreContext)Field(typeof(BackendManager), "_gameDataRestoreContext").GetValue(fixture.Manager);
        var store = (IGachaEconomyStore)Activator.CreateInstance(type, fixture.Manager, restore.LegacyQuery, restore.LegacyTarget);
        Field(typeof(BackendManager), "_gachaEconomyStore").SetValue(fixture.Manager, store);
        cleanup = () => { foreach (var wrapper in wrappers) Object.DestroyImmediate(wrapper); Object.DestroyImmediate(item); Object.DestroyImmediate(config); };
        return new GachaEconomyService(store, catalog, config, (machine, pool, guarantee) => { onDraw?.Invoke(); return pool[0]; });
    }
    private sealed class EconomyUserInfoScope : IDisposable
    {
        private readonly Dictionary<FieldInfo,object> _original = new Dictionary<FieldInfo,object>();
        private readonly Dictionary<FieldInfo,object> _walletOriginal = new Dictionary<FieldInfo,object>();
        private readonly object _wallet = typeof(UserInfo).GetField("DiamondWallet",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        public EconomyUserInfoScope()
        {
            foreach(var field in _wallet.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic))
                if (!field.IsInitOnly) _walletOriginal[field]=field.GetValue(_wallet);
            foreach (string name in new[]{"_giveGachaItemCountDic","_giveGachaItemLevelDic","_giveRecipeLevelDic"}) Replace(name,new Dictionary<string,int>());
            Replace("_dia",110); Replace("_totalUseGachaMachineCount",0);
            Replace("_staffCostCommitDepth",1); // Suppress live DataBind notifications for this isolated wallet scope.
            Replace("_notificationMessageSet",new HashSet<string>()); Replace("_clearNotificationMessageSet",new HashSet<string>());
            foreach(string name in new[]{"OnChangeDiaHandler","OnGiveGachaItemHandler","OnGiveRecipeHandler","OnGiveStaffHandler","OnUseGachaMachineHandler","OnAddNotificationHandler"})
            {
                var field=typeof(UserInfo).GetField(name,BindingFlags.Static|BindingFlags.NonPublic);
                if(field!=null){_original[field]=field.GetValue(null);field.SetValue(null,null);}
            }
        }
        private void Replace(string name,object value)
        { var field=typeof(UserInfo).GetField(name,BindingFlags.Static|BindingFlags.NonPublic);_original[field]=field.GetValue(null);field.SetValue(null,value); }
        public void Dispose(){ foreach(var pair in _original) pair.Key.SetValue(null,pair.Value);
            foreach(var pair in _walletOriginal) pair.Key.SetValue(_wallet,pair.Value); }
    }
}
#endif
