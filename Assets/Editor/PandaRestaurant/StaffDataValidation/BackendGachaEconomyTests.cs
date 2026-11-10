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
    public void EconomyBackend_StaffSkinPurchaseAndDuplicateRewardUseOnlyPandaTokens()
    {
        using (var scope = new EconomyUserInfoScope())
        using (var staffScope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var backendField = Field(typeof(BackendManager), "_instance", true);
            var skinManagerField = Field(typeof(SkinDataManager), "_instance", true);
            var skinDictionaryField = Field(typeof(SkinDataManager), "_staffSkinDataDic", true);
            object oldBackend = backendField.GetValue(null), oldManager = skinManagerField.GetValue(null);
            object oldDictionary = skinDictionaryField.GetValue(null);
            var stagesField = Field(typeof(UserInfo), "_stageInfos", true);
            var stageField = Field(typeof(UserInfo), "_currentStage", true);
            object oldStages = stagesField.GetValue(null), oldStage = stageField.GetValue(null);
            var root = new GameObject("Inactive isolated token skin manager");
            root.SetActive(false);
            var skin = ScriptableObject.CreateInstance<StaffSkinData>();
            try
            {
                fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
                    UserInfo.StaffPurchaseWallet, _ => null, _ => { });
                Authenticate(fixture, fixture.Account);
                RestoreAccountJson(fixture, CommonAccountJson());
                backendField.SetValue(null, fixture.Manager);
                skinManagerField.SetValue(null, root.AddComponent<SkinDataManager>());
                typeof(BasicData).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(skin, "SKIN_STAFF_TOKEN_TEST");
                Field(typeof(SkinData), "_equipId").SetValue(skin, "STAFF01");
                Field(typeof(SkinData), "_buyPrice").SetValue(skin, 40);
                Field(typeof(SkinData), "_duplicationToken").SetValue(skin, 5);
                skinDictionaryField.SetValue(null, new Dictionary<string, StaffSkinData> { [skin.Id] = skin });
                stagesField.SetValue(null, new[] { fixture.Stage.Runtime[EStage.Stage1],
                    fixture.Stage.Runtime[EStage.Stage2], fixture.Stage.Runtime[EStage.Stage3] });
                stageField.SetValue(null, EStage.Stage1);
                fixture.Game.LatestFactory = () =>
                {
                    var values = new Param();
                    values.Add("GiveStaffSkinList", new List<string>((HashSet<string>)
                        Field(typeof(UserInfo), "_giveStaffSkinSet", true).GetValue(null)));
                    return values;
                };
                int changes = 0, grants = 0;
                bool ownedAtNotification = false, reentrantPurchase = false;
                UserInfo.OnChangePandaTokensHandler += () =>
                {
                    changes++;
                    ownedAtNotification = UserInfo.IsGiveStaffSkin(skin.Id);
                    reentrantPurchase |= UserInfo.TryPurchaseStaffSkin(skin, out _);
                };
                UserInfo.OnGiveStaffSkinHandler += () => grants++;
                Assert.That(UserInfo.PandaTokens, Is.EqualTo(55));
                Assert.That(UserInfo.TryPurchaseStaffSkin(skin, out string error), Is.True, error);
                Assert.That(UserInfo.PandaTokens, Is.EqualTo(15));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(ownedAtNotification, Is.True);
                Assert.That(reentrantPurchase, Is.False, "Listener reentry cannot buy twice.");
                Assert.That(grants, Is.EqualTo(1));
                Assert.That(UserInfo.TryPurchaseStaffSkin(skin, out _), Is.False);
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                var saved = JObject.Parse(fixture.Game.WriteValues[0].GetJson());
                Assert.That(saved["SkinToken"], Is.Null);
                Assert.That((string)saved["GiveStaffSkinList"][0], Is.EqualTo(skin.Id));
                Assert.That(StaffAccountSaveConverter.Read((string)saved["StaffAccount"]).Data.PandaTokens, Is.EqualTo(15));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                UserInfo.GiveStaffSkin(skin);
                Assert.That(UserInfo.PandaTokens, Is.EqualTo(20));
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(grants, Is.EqualTo(1), "Duplicate rewards do not grant the skin twice.");
                Assert.That(UserInfo.TryAddPandaTokens(long.MaxValue, out error), Is.False);
                Assert.That(UserInfo.PandaTokens, Is.EqualTo(20));
            }
            finally
            {
                fixture.Manager.InvalidateGameDataRestore();
                fixture.Manager.DestroyEditorOfflineOwner();
                backendField.SetValue(null, oldBackend);
                skinManagerField.SetValue(null, oldManager);
                skinDictionaryField.SetValue(null, oldDictionary);
                stagesField.SetValue(null, oldStages);
                stageField.SetValue(null, oldStage);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(skin);
            }
        }
    }

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
                Assert.That(fixture.Manager.StaffRuntime.TryChangePandaTokens(5, out _), Is.False,
                    "Skin rewards/purchases cannot alter a reserved gacha account.");
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
    [TestCase(GachaPaymentKind.TicketEleven)]
    public void EconomyBackend_FreeQuestOfferBlocksStaffDrawBeforeRngAndPayment(GachaPaymentKind payment)
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                new[] { new StaffAccountStaffRecord("STAFF03", 4) }, 55, new GachaEconomySaveData(staffTickets: 10));
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

    [Test]
    public void EconomyBackend_Stage1FairyAppearsOnlyAfterProductionCommitAndNeverDuplicates()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            using (var host = new EnhancementFairyStage1Host())
            {
                var service = CreateEconomyService(fixture, out var cleanup, productionItems: host.Catalog);
                var handler = (Action<GachaEconomyTransaction>)Delegate.CreateDelegate(typeof(Action<GachaEconomyTransaction>),
                    fixture.Manager, typeof(BackendManager).GetMethod("OnEconomyCommitted", BindingFlags.Instance | BindingFlags.NonPublic));
                service.Committed += handler;
                try
                {
                    host.Move(ERestaurantFloorType.Floor3);
                    host.Move(RestaurantType.Kitchen);
                    host.Advance(1f / 30f); // Observe the final Kitchen camera transform.
                    Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                    var tx = service.LastTransaction;
                    Assert.That(EnhancementFairyCatalog.IsEligible(tx.Results[0].Data), Is.True);
                    Assert.That(host.Habitat.ActiveCount, Is.Zero, "Sending must not grant a fairy");
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
                    fixture.Game.WriteReplies[0](Bro("500", ""));
                    Assert.That(tx.Status, Is.EqualTo(GachaTransactionStatus.Indeterminate));
                    Assert.That(host.Habitat.ActiveCount, Is.Zero, "An unknown/failed response is not a confirmed grant");
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    Assert.That(tx.IsCompleted, Is.True);
                    Assert.That(host.Habitat.ActiveCount, Is.EqualTo(1));
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1), "A confirmed birth can start on the visible Kitchen wall");
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1), "Repeated success callback must not start another birth");
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1), "Repeated success callback does not enqueue twice");
                    host.Move(RestaurantType.Hall);
                    host.Advance(1f / 30f);
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
                    host.RecordBirth();
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero, "Landing consumes the confirmed birth once");
                    host.Advance(.25f);
                    Assert.That(host.VisibleBodyCount(), Is.GreaterThan(0));
                    host.Capture("stage1-first-acquisition.png");
                    var identity = host.Identity();
                    if (fixture.Game.WriteReplies.Count > 1) fixture.Game.WriteReplies[1](Bro("204", ""));
                    int nextWrite = fixture.Game.Writes;
                    Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out error), Is.True, error);
                    Assert.That(service.LastTransaction.Results[0].Id, Is.EqualTo(tx.Results[0].Id));
                    Assert.That(service.LastTransaction.Results[0].IsNew, Is.False);
                    fixture.Game.WriteReplies[nextWrite](Bro("204", ""));
                    Assert.That(service.LastTransaction.IsCompleted, Is.True);
                    CollectionAssert.AreEqual(identity, host.Identity());
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
                    host.Advance(.5f);
                    host.Capture("stage1-duplicate-acquisition.png");
                    if (fixture.Game.WriteReplies.Count > nextWrite + 1) fixture.Game.WriteReplies[nextWrite + 1](Bro("204", ""));
                }
                finally
                { service.Committed -= handler; fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
            }
        }
    }

    [Test]
    public void EconomyBackend_CancelledSessionDoesNotGrantStage1FairyFromLateSuccess()
    {
        using (var scope = new EconomyUserInfoScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            using (var host = new EnhancementFairyStage1Host())
            {
                var service = CreateEconomyService(fixture, out var cleanup, productionItems: host.Catalog);
                var handler = (Action<GachaEconomyTransaction>)Delegate.CreateDelegate(typeof(Action<GachaEconomyTransaction>),
                    fixture.Manager, typeof(BackendManager).GetMethod("OnEconomyCommitted", BindingFlags.Instance | BindingFlags.NonPublic));
                service.Committed += handler;
                try
                {
                    host.Move(ERestaurantFloorType.Floor3);
                    Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                    fixture.Manager.InvalidateGameDataRestore();
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                    Assert.That(service.LastTransaction.IsCompleted, Is.False);
                    Assert.That(host.Habitat.ActiveCount, Is.Zero);
                    Assert.That(host.Habitat.OwnedTypeCount, Is.Zero);
                    Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero);
                    Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
                }
                finally { service.Committed -= handler; fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
            }
        }
    }

    private GachaEconomyService CreateEconomyService(Fixture fixture, out Action cleanup, Action onDraw = null,
        IReadOnlyList<GachaItemData> productionItems = null)
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
        var catalog = new List<GachaData>(wrappers);
        if (productionItems == null) catalog.Add(item);
        else catalog.AddRange(productionItems);
        var type = typeof(BackendManager).Assembly.GetType("Muks.BackEnd.BackendGachaEconomyStore", true);
        var restore = (GameDataRestoreContext)Field(typeof(BackendManager), "_gameDataRestoreContext").GetValue(fixture.Manager);
        var store = (IGachaEconomyStore)Activator.CreateInstance(type, fixture.Manager, restore.LegacyQuery, restore.LegacyTarget);
        Field(typeof(BackendManager), "_gachaEconomyStore").SetValue(fixture.Manager, store);
        cleanup = () => { foreach (var wrapper in wrappers) Object.DestroyImmediate(wrapper); Object.DestroyImmediate(item); Object.DestroyImmediate(config); };
        return new GachaEconomyService(store, catalog, config, (machine, pool, guarantee) =>
        {
            onDraw?.Invoke();
            return productionItems != null && machine == GachaMachineKind.Item
                ? pool.First(EnhancementFairyCatalog.IsEligible) : pool[0];
        });
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
            Replace("_giveStaffSkinSet", new HashSet<string>());
            foreach(string name in new[]{"OnChangeDiaHandler","OnChangePandaTokensHandler","OnGiveStaffSkinHandler","OnGiveGachaItemHandler","OnGiveRecipeHandler","OnGiveStaffHandler","OnUseGachaMachineHandler","OnAddNotificationHandler"})
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
