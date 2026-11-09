#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;

public sealed class CustomerUnlockRegressionTests
{
    private static readonly MethodInfo UnlockCheck = typeof(CustomerDataManager).GetMethod(
        "CheckEnableCustomer", BindingFlags.Static | BindingFlags.NonPublic);

    private static DateTime Initialize(Flow flow)
    {
        DateTime now = CustomerEncounterTests.PrepareMemoryAccountRestore(flow);
        flow.Swap(typeof(UserInfo), "_score", 0);
        flow.Swap(typeof(UserInfo), "_money", 100L);
        flow.Swap(typeof(UserInfo), "_staffCostCommitDepth", 0);
        flow.Swap(typeof(UserInfo), "<EconomyInventoryReservation>k__BackingField", null);
        flow.Swap(typeof(UserInfo), "_giveRecipeLevelDic", new Dictionary<string, int>());
        flow.Swap(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int>());
        flow.Swap(typeof(UserInfo), "_giveGachaItemLevelDic", new Dictionary<string, int>());
        flow.Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData>());
        flow.Swap(typeof(UserInfo), "_notificationMessageSet", new HashSet<string>());
        flow.Swap(typeof(UserInfo), "_clearNotificationMessageSet", new HashSet<string>());

        // Init clears its catalog caches; never let it clear the user's original collections.
        foreach (var field in typeof(CustomerDataManager).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsGenericType)
                flow.Swap(typeof(CustomerDataManager), field.Name, Activator.CreateInstance(field.FieldType));
            else if (field.Name != "_instance")
                flow.Swap(typeof(CustomerDataManager), field.Name, null);
        }
        Set(GameManager.Instance, "_addFurnitureScore", 0);
        Set(GameManager.Instance, "_addKitchenUtensilScore", 0);
        Set(GameManager.Instance, "_addGiveGachaItemScore", 0);
        // Each existing CSV ends with a blank-ID row; keep its diagnostic explicit, not ignored.
        for (int catalog = 0; catalog < 3; catalog++)
            LogAssert.Expect(LogType.Error, "Id값이 이상합니다: ");
        typeof(CustomerDataManager).GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        foreach (var data in CustomerDataManager.Instance.GetCustomerDataList()) flow.Track(data);
        return now;
    }

    private static NormalCustomerData Candidate(Flow flow, string id, int score = 0, string dish = "", string item = "")
    {
        var data = flow.Track(UnityEngine.Object.Instantiate(flow.Data));
        Set(data, "_id", id);
        Set(data, "_name", id);
        Set(data, "_minScore", score);
        Set(data, "_requiredDish", dish);
        Set(data, "_requiredItem", item);
        Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic").Add(id, data);
        return data;
    }

    private static void SelectCandidates(Flow flow, params CustomerData[] candidates)
    {
        flow.Swap(typeof(CustomerDataManager), "_customerDataList", candidates.ToList());
        flow.Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData>());
    }

    private static int SubscriptionCount(Type type, object owner, string eventName)
    {
        var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        var handlers = (Delegate)field.GetValue(owner);
        return handlers == null ? 0 : handlers.GetInvocationList().Count(handler => handler.Method == UnlockCheck);
    }

    [TestCase(1L)]
    [TestCase(-1L)]
    public void CoinOnly_DoesNotCheckEvenAnEligibleLockedCustomer(long amount)
    {
        using (var flow = new Flow())
        {
            Initialize(flow);
            var customer = Candidate(flow, "UNLOCK-COIN");
            SelectCandidates(flow, customer);
            int subscriptions = SubscriptionCount(typeof(UserInfo), null, "OnChangeMoneyHandler");
            int checkCalls = 0;
            var moneyEvent = typeof(UserInfo).GetField("OnChangeMoneyHandler", BindingFlags.Static | BindingFlags.NonPublic);
            var handlers = (Action)moneyEvent.GetValue(null);
            Action observed = null;
            if (handlers != null)
                foreach (Action handler in handlers.GetInvocationList())
                {
                    Action callback = handler;
                    if (handler.Method == UnlockCheck)
                        callback = () => { checkCalls++; handler(); };
                    observed += callback;
                }
            moneyEvent.SetValue(null, observed);
            int enabledEvents = 0;
            UserInfo.OnEnabledCustomerHandler += () => enabledEvents++;
            long before = UserInfo.Money;
            UserInfo.AddMoney(amount);
            Assert.That(UserInfo.Money, Is.EqualTo(before + amount));
            Assert.That(checkCalls, Is.Zero, "Count the actual unlock callback dispatched by one coin change.");
            Assert.That(enabledEvents, Is.Zero, "An unnecessary full scan would unlock this eligible sentinel.");
            Assert.That(UserInfo.GetCustomerEnableState(customer), Is.False);
            Assert.That(subscriptions, Is.Zero);
        }
    }

    [Test]
    public void Initialization_ChecksAllAndOnlyEligibleCatalogCustomersWithoutMoney()
    {
        using (var flow = new Flow())
        {
            Initialize(flow);
            var customers = CustomerDataManager.Instance.GetCustomerDataList();
            Assert.That(customers, Is.Not.Empty);
            foreach (var customer in customers)
            {
                var state = UserInfo.GetCustomerVisitState(customer);
                Assert.That(UserInfo.GetCustomerEnableState(customer),
                    Is.EqualTo(state.IsScoreValid && state.IsGiveRecipe && state.IsGiveItem), customer.Id);
            }
            Assert.That(SubscriptionCount(typeof(UserInfo), null, "OnGiveRecipeHandler"), Is.EqualTo(1));
            Assert.That(SubscriptionCount(typeof(UserInfo), null, "OnGiveGachaItemHandler"), Is.EqualTo(1));
            Assert.That(SubscriptionCount(typeof(GameManager), GameManager.Instance, "OnChangeScoreHandler"), Is.EqualTo(1));
        }
    }

    [Test]
    public void ScoreRise_UnlocksAtThresholdOnceAndDoesNotSkipOrDuplicateCustomers()
    {
        using (var flow = new Flow())
        {
            Initialize(flow);
            var first = Candidate(flow, "UNLOCK-SCORE-A", score: 10);
            var second = Candidate(flow, "UNLOCK-SCORE-B", score: 10);
            var blocked = Candidate(flow, "UNLOCK-SCORE-BLOCKED", score: 10, dish: flow.Food.Id);
            SelectCandidates(flow, first, first, second, blocked);
            int enabledEvents = 0;
            UserInfo.OnEnabledCustomerHandler += () => enabledEvents++;
            UserInfo.AddScore(9);
            Assert.That(enabledEvents, Is.Zero);
            UserInfo.AddScore(1);
            Assert.That(UserInfo.GetCustomerEnableState(first), Is.True);
            Assert.That(UserInfo.GetCustomerEnableState(second), Is.True);
            Assert.That(UserInfo.GetCustomerEnableState(blocked), Is.False);
            Assert.That(enabledEvents, Is.EqualTo(2));
            UserInfo.AddScore(1);
            Assert.That(enabledEvents, Is.EqualTo(2));
        }
    }

    [Test]
    public void RecipeAcquisition_UnlocksOnceAndKeepsMissingItemLocked()
    {
        using (var flow = new Flow())
        {
            Initialize(flow);
            var eligible = Candidate(flow, "UNLOCK-RECIPE", dish: flow.Food.Id);
            var blocked = Candidate(flow, "UNLOCK-RECIPE-BLOCKED", dish: flow.Food.Id, item: "UNLOCK-ITEM");
            SelectCandidates(flow, eligible, blocked);
            int enabledEvents = 0;
            UserInfo.OnEnabledCustomerHandler += () => enabledEvents++;
            UserInfo.GiveRecipe(flow.Food);
            Assert.That(UserInfo.GetCustomerEnableState(eligible), Is.True);
            Assert.That(UserInfo.GetCustomerEnableState(blocked), Is.False);
            UserInfo.GiveRecipe(flow.Food.Id);
            Assert.That(enabledEvents, Is.EqualTo(1));
        }
    }

    [Test]
    public void ItemAcquisition_UnlocksOnceAndKeepsMissingRecipeLocked()
    {
        using (var flow = new Flow())
        {
            Initialize(flow);
            var item = new GachaItemData("UNLOCK-ITEM", "Unlock item", "", 0, 0, 1, UpgradeType.UPGRADE01, 0, 0, 1, null);
            flow.Swap(typeof(ItemManager), "_instance", flow.Create<ItemManager>());
            flow.Swap(typeof(ItemManager), "_gachaItemDataDic", new Dictionary<string, GachaItemData> { [item.Id] = item });
            var eligible = Candidate(flow, "UNLOCK-ITEM-CUSTOMER", item: item.Id);
            var blocked = Candidate(flow, "UNLOCK-ITEM-BLOCKED", dish: flow.Food.Id, item: item.Id);
            SelectCandidates(flow, eligible, blocked);
            int enabledEvents = 0;
            UserInfo.OnEnabledCustomerHandler += () => enabledEvents++;
            Assert.That(UserInfo.GiveGachaItem(item), Is.True);
            Assert.That(UserInfo.GetCustomerEnableState(eligible), Is.True);
            Assert.That(UserInfo.GetCustomerEnableState(blocked), Is.False);
            Assert.That(UserInfo.GiveGachaItem(item.Id), Is.True);
            Assert.That(enabledEvents, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AccountRestore_UsesRemainingEventsAndPreservesExistingVisitsAndSkin(bool existingAccount)
    {
        using (var flow = new Flow())
        {
            DateTime now = Initialize(flow);
            var eligible = Candidate(flow, "UNLOCK-RESTORE", score: existingAccount ? 10 : 0,
                dish: existingAccount ? flow.Food.Id : "", item: existingAccount ? "UNLOCK-ITEM" : "");
            var blocked = Candidate(flow, "UNLOCK-RESTORE-BLOCKED", score: 11);
            SelectCandidates(flow, eligible, blocked);
            JObject payload = JObject.Parse(LoadUserData.CreateInitialGameData(now).GetJson());
            payload["LastAccessTime"] = now.AddHours(9).ToString();
            payload["LastAttendanceTime"] = now.AddHours(9).ToString();
            if (existingAccount)
            {
                payload["Score"] = 10;
                payload["GiveRecipeList"] = new JArray(new JObject { ["Id"] = flow.Food.Id, ["Level"] = 1 });
                payload["GiveGachaItemCountList"] = new JArray(new JObject { ["Id"] = "UNLOCK-ITEM", ["Count"] = 1 });
                payload["GiveGachaItemLevelList"] = new JArray(new JObject { ["Id"] = "UNLOCK-ITEM", ["Level"] = 1 });
                payload["EnabledCustomerDataList"] = JArray.FromObject(new[] { new SaveCustomerData(flow.Data.Id, "SAVED-SKIN", 25) });
            }
            int enabledEvents = 0;
            UserInfo.OnEnabledCustomerHandler += () => enabledEvents++;
            for (int restore = 0; restore < 2; restore++)
            {
                enabledEvents = 0;
                Assert.That(UserInfo.TryLoadGameData(CustomerEncounterTests.EncounterMemoryResponse(payload)), Is.True);
                Assert.That(UserInfo.GetCustomerEnableState(eligible), Is.True);
                Assert.That(UserInfo.GetCustomerEnableState(blocked), Is.False);
                Assert.That(enabledEvents, Is.EqualTo(1), "Recipe and item restore events must not double unlock.");
                if (existingAccount)
                {
                    var records = (Dictionary<string, SaveCustomerData>)typeof(UserInfo).GetField(
                        "_enabledCustomerDic", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    var saved = records[flow.Data.Id];
                    Assert.That(saved.VisitCount, Is.EqualTo(25));
                    Assert.That(saved.SkinId, Is.EqualTo("SAVED-SKIN"));
                }
            }
        }
    }
}
#endif
