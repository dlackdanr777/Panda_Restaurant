#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Reuse the actual Backend entry boundary, detached StageInfo fixture and original-state assertions.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void AccountRuntime_CommonRestoreOverridesOldStageGrowthWithoutRewritingOriginalSnapshots()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet);
            var initial = fixture.Manager.StaffRuntime.Snapshot;
            string original = JsonConvert.SerializeObject(initial);
            Assert.That(fixture.Manager.StaffRuntime.Mode.ToString(), Is.EqualTo("Common"));
            fixture.Manager.LoadAllStageData(true);
            Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3, Staff("STAFF01", 4, "THIRD_SKIN")));
            Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2));
            Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 1, "FIRST_SKIN")));
            AssertAccountLevels(fixture, "STAFF01", 2);
            AssertAccountLevels(fixture, "STAFF03", 4);
            foreach (var stage in fixture.Stage.Runtime.Values)
                CollectionAssert.AreEquivalent(new[] { "STAFF01", "STAFF03" }, stage.GetGiveStaffDataList().Select(staff => staff.Id));
            fixture.Stage.Runtime[EStage.Stage1].GetGiveStaffDataList().Clear();
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Count, Is.EqualTo(2), "A returned list is not the common ownership store");
            Assert.That(fixture.Stage.Runtime[EStage.Stage1].SaveData().GiveStaffList.Single().Level, Is.EqualTo(1));
            Assert.That(fixture.Stage.Runtime[EStage.Stage3].SaveData().GiveStaffList.Single().SkinId, Is.EqualTo("THIRD_SKIN"));
            var lateLegacy = fixture.Stage.Runtime[EStage.Stage1].SaveData();
            lateLegacy.GiveStaffList = new List<SaveStaffData> { Staff("STAFF01", 5) };
            Assert.That(fixture.Stage.Runtime[EStage.Stage1].LoadData(lateLegacy), Is.True);
            AssertAccountLevels(fixture, "STAFF01", 2);
            Assert.That(JsonConvert.SerializeObject(initial), Is.EqualTo(original));
            Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(original));
            Assert.That(fixture.Manager.PrepareStaffMigration().Status, Is.EqualTo(StaffMigrationPreparationStatus.NotRequired));
            Assert.That(wallet.CostCommits, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void AccountRuntime_PaidGrowthCommitsOnceAndEveryStageReadsTheCommonLevel()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet);
            var stage = fixture.Stage.Runtime[EStage.Stage1];
            int upgraded = 0;
            stage.OnUpgradeStaffHandler += () => upgraded++;
            var before = fixture.Manager.StaffRuntime.Snapshot;
            Assert.That(scope.Staff("STAFF01").GetUpgradeMoneyData(2).MoneyType, Is.EqualTo(MoneyType.Gold));
            Assert.That(scope.Staff("STAFF01").GetUpgradeMoneyData(2).Price, Is.EqualTo(90000));
            int[] levelsObservedAtCostNotification = null;
            long goldObservedAtCostNotification = -1;
            wallet.AfterCostNotification = _ =>
            {
                levelsObservedAtCostNotification = fixture.Stage.Runtime.Values.Select(value => value.GetStaffLevel("STAFF01")).ToArray();
                goldObservedAtCostNotification = wallet.Gold;
            };
            Assert.That(stage.UpgradeStaff(scope.Staff("STAFF01")), Is.True);
            AssertAccountLevels(fixture, "STAFF01", 3);
            Assert.That(wallet.Gold, Is.EqualTo(910000));
            Assert.That(wallet.Diamonds, Is.EqualTo(110));
            Assert.That(wallet.CostCommits, Is.EqualTo(1));
            Assert.That(wallet.Notifications, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 3, 3, 3 }, levelsObservedAtCostNotification);
            Assert.That(goldObservedAtCostNotification, Is.EqualTo(910000));
            Assert.That(fixture.Manager.StaffRuntime.LastNotificationError, Is.Null);
            Assert.That(upgraded, Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(before.Staff.Single(staff => staff.Id == "STAFF01").Level, Is.EqualTo(2));
            Assert.That(fixture.Game.Writes, Is.Zero, "Growth does not start a new persistence system");

            var diaFixture = CreateAccountRuntimeFixture(out var diaWallet);
            RestoreAccountJson(diaFixture, CommonAccountJson(level: 4));
            Assert.That(diaFixture.Stage.Runtime[EStage.Stage2].UpgradeStaff(scope.Staff("STAFF01")), Is.True);
            AssertAccountLevels(diaFixture, "STAFF01", 5);
            Assert.That(diaWallet.Gold, Is.EqualTo(1000000));
            Assert.That(diaWallet.Diamonds, Is.EqualTo(100));
            Assert.That(diaWallet.CostCommits, Is.EqualTo(1));
            Assert.That(diaWallet.Notifications, Is.EqualTo(1));
            Assert.That(diaFixture.Manager.StaffRuntime.LastNotificationError, Is.Null);
            Assert.That(diaFixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
        }
    }

    [Test]
    public void AccountRuntime_RejectionsAndReentrantGrowthNeverPartiallyChargeOrNotifyTwice()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string reason in new[] { "poor", "max", "wallet-refused", "invalidated", "invalidated-during-read", "indeterminate", "unowned", "null" })
            {
                var fixture = CreateAccountRuntimeFixture(out var wallet);
                if (reason == "poor") wallet.GoldValue = 89999;
                if (reason == "max") RestoreAccountJson(fixture, CommonAccountJson(level: 5));
                if (reason == "wallet-refused") wallet.RefuseCost = true;
                if (reason == "indeterminate")
                {
                    fixture.Manager.RequestGameDataAutosave();
                    var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
                    Assert.That(coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Memory-only missing response"), Is.True);
                    Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
                    Assert.That(fixture.Manager.StaffRuntime.CanMutate, Is.False);
                }
                var before = fixture.Manager.StaffRuntime.Snapshot;
                string original = JsonConvert.SerializeObject(before);
                long gold = wallet.Gold; int dia = wallet.Diamonds;
                if (reason == "invalidated") fixture.Manager.InvalidateGameDataRestore();
                if (reason == "invalidated-during-read") wallet.OnReadScore = () => fixture.Manager.InvalidateGameDataRestore();
                int notifications = 0;
                fixture.Stage.Runtime[EStage.Stage1].OnUpgradeStaffHandler += () => notifications++;
                StaffData staff = reason == "null" ? null : scope.Staff(reason == "unowned" ? "STAFF02" : "STAFF01");
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].UpgradeStaff(staff), Is.False, reason);
                Assert.That(wallet.Gold, Is.EqualTo(gold), reason);
                Assert.That(wallet.Diamonds, Is.EqualTo(dia), reason);
                Assert.That(wallet.CostCommits, Is.Zero, reason);
                Assert.That(wallet.Notifications, Is.Zero, reason);
                Assert.That(notifications, Is.Zero, reason);
                Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(original), reason);
                if (!reason.StartsWith("invalidated", StringComparison.Ordinal))
                    Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(original), reason);
                Assert.That(fixture.Game.Writes, Is.EqualTo(reason == "indeterminate" ? 1 : 0));
            }
            var reentrant = CreateAccountRuntimeFixture(out var bank);
            int rejected = 0, upgraded = 0;
            var first = reentrant.Stage.Runtime[EStage.Stage1];
            bank.BeforeCostCommit = _ =>
            {
                if (!reentrant.Stage.Runtime[EStage.Stage2].UpgradeStaff(scope.Staff("STAFF01"))) rejected++;
            };
            bank.AfterCostNotification = _ =>
            {
                if (!first.UpgradeStaff(scope.Staff("STAFF01"))) rejected++;
            };
            first.OnUpgradeStaffHandler += () =>
            {
                upgraded++;
                if (!first.UpgradeStaff(scope.Staff("STAFF01"))) rejected++;
            };
            Assert.That(first.UpgradeStaff(scope.Staff("STAFF01")), Is.True);
            Assert.That(rejected, Is.EqualTo(3));
            Assert.That(bank.CostCommits, Is.EqualTo(1));
            Assert.That(bank.Notifications, Is.EqualTo(1));
            Assert.That(upgraded, Is.EqualTo(1));
            Assert.That(bank.Gold, Is.EqualTo(910000));
            Assert.That(reentrant.Manager.StaffRuntime.LastNotificationError, Is.Null);
            AssertAccountLevels(reentrant, "STAFF01", 3);
        }
    }

    [Test]
    public void AccountRuntime_CommonOwnershipAllowsStageLocalPlacementAndSkinWithoutPropagation()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet);
            fixture.Manager.LoadAllStageData(true);
            foreach (EStage stage in new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 })
                Reply(fixture, stage, StageResponse(fixture, stage));
            foreach (var stage in fixture.Stage.Runtime.Values)
                stage.SetNullEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Waiter);
            var first = fixture.Stage.Runtime[EStage.Stage1];
            Assert.That(first.SaveData().GiveStaffList, Is.Empty);
            first.SetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Waiter, scope.Staff("STAFF01"));
            Assert.That(first.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Waiter), Is.SameAs(scope.Staff("STAFF01")));
            foreach (var stage in fixture.Stage.Runtime.Where(entry => entry.Key != EStage.Stage1).Select(entry => entry.Value))
                Assert.That(stage.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Waiter), Is.Null);
            LogAssert.Expect(LogType.Error, "해당 스탭과 관련 없는 장착 슬롯입니다: (스탭 타입 Waiter)(장착 슬롯Chef)");
            first.SetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Chef, scope.Staff("STAFF01"));
            Assert.That(first.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Chef), Is.Null);
            var skin = ScriptableObject.CreateInstance<StaffSkinData>();
            try
            {
                typeof(BasicData).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, "MEMORY_STAFF01_SKIN");
                typeof(SkinData).GetField("_equipId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, "STAFF01");
                Assert.That(first.TrySetStaffSkin(scope.Staff("STAFF01"), skin), Is.True);
                Assert.That(first.SaveData().GiveStaffList.Single(staff => staff.Id == "STAFF01").SkinId, Is.EqualTo("MEMORY_STAFF01_SKIN"));
                foreach (var stage in fixture.Stage.Runtime.Where(entry => entry.Key != EStage.Stage1).Select(entry => entry.Value))
                    Assert.That(stage.SaveData().GiveStaffList, Is.Empty);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
                AssertAccountLevels(fixture, "STAFF01", 2);
            }
            finally { UnityEngine.Object.DestroyImmediate(skin); }
            Assert.That(wallet.CostCommits, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void AccountRuntime_OrdinaryGiveAndDuplicateUseCommonOwnershipWithoutPandaRewards()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet);
            int additions = 0;
            var first = fixture.Stage.Runtime[EStage.Stage1];
            first.OnGiveStaffHandler += () => additions++;
            Assert.That(first.GiveStaff(scope.Staff("STAFF02")), Is.True);
            AssertAccountLevels(fixture, "STAFF02", 1);
            var snapshot = fixture.Manager.StaffRuntime.Snapshot;
            Assert.That(first.GiveStaff("STAFF02"), Is.True);
            Assert.That(fixture.Stage.Runtime[EStage.Stage2].GiveStaff(scope.Staff("STAFF02")), Is.True);
            Assert.That(additions, Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Count(staff => staff.Id == "STAFF02"), Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(snapshot)));
            AssertAccountLevels(fixture, "STAFF01", 2);
            Assert.That(wallet.CostCommits, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void AccountRuntime_AutosaveUsesLatestCommonJsonAndOldResponsesNeverRollbackMemory()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet);
            fixture.Game.LatestFactory = () =>
            {
                var values = new Param(); values.Add("Dia", wallet.Diamonds); values.Add("Money", wallet.Gold); return values;
            };
            int completed = 0;
            var first = fixture.Manager.RequestGameDataAutosave(_ => completed++);
            Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(fixture.Game.LatestCalls, Is.EqualTo(1));
            string oldPayload = fixture.Game.WriteValues[0].GetJson();
            AssertSavedAccount(fixture.Game.WriteValues[0], 2, false, 55);
            Assert.That(fixture.Stage.Runtime[EStage.Stage2].UpgradeStaff(scope.Staff("STAFF01")), Is.True);
            Assert.That(fixture.Stage.Runtime[EStage.Stage3].GiveStaff(scope.Staff("STAFF02")), Is.True);
            var latestSnapshot = fixture.Manager.StaffRuntime.Snapshot;
            string latestJson = JsonConvert.SerializeObject(latestSnapshot);
            var second = fixture.Manager.RequestGameDataAutosave(_ => completed++);
            Assert.That(second.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            Assert.That(fixture.Game.LatestCalls, Is.EqualTo(2));
            AssertSavedAccount(fixture.Game.WriteValues[1], 3, true, 55);
            Assert.That((long)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["Money"], Is.EqualTo(910000));
            Assert.That(fixture.Game.WriteValues[0].GetJson(), Is.EqualTo(oldPayload));
            Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(latestJson));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(completed, Is.EqualTo(2));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(latestJson));
            foreach (var target in fixture.Game.WriteTargets)
            { Assert.That(target.AccountInDate, Is.EqualTo(fixture.Account)); Assert.That(target.RowInDate, Is.EqualTo(fixture.Row)); }
            // A copied old common snapshot is not allowed to overwrite the current mutable runtime.
            var stale = new Param(); stale.Add("StaffAccount", (string)JObject.Parse(oldPayload)["StaffAccount"]);
            var rejected = fixture.Manager.RequestGameDataSave(stale);
            Assert.That(rejected.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
        }
    }

    [Test]
    public void AccountRuntime_EmptyCommonLegacyFallbackAndInvalidGenerationsRemainDistinct()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var empty = CreateAccountRuntimeFixture(out _);
            RestoreAccountJson(empty, "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}");
            Assert.That(empty.Manager.StaffRuntime.Mode.ToString(), Is.EqualTo("Common"));
            Assert.That(empty.Manager.StaffRuntime.Snapshot.Staff, Is.Empty);
            Assert.That(empty.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
            Assert.That(empty.Stage.Runtime[EStage.Stage1].IsGiveStaff("STAFF01"), Is.False);
            var legacy = CreateAccountRuntimeFixture(out _, common: false);
            legacy.Manager.LoadAllStageData(true);
            ReplyRuntimeRows(legacy);
            Assert.That(legacy.Manager.StaffRuntime.Mode.ToString(), Is.EqualTo("Legacy"));
            Assert.That(legacy.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(legacy.Stage.Runtime[EStage.Stage1].GetStaffLevel("STAFF01"), Is.EqualTo(2));
            Assert.That(legacy.Stage.Runtime[EStage.Stage2].IsGiveStaff("STAFF01"), Is.False);
            AssertPreparation(legacy.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.Valid);
            Assert.That(legacy.Game.Writes, Is.EqualTo(1), "All required Stage applications now automatically send one migration request");
            Assert.That(legacy.Manager.CurrentStaffMigrationExecution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Sending));
            Assert.That(legacy.Manager.CurrentStaffMigrationExecution.CompletionCount, Is.Zero,
                "Legacy authority remains until the actual original request reports a confirmed success");
            CollectionAssert.AreEqual(new[] { "StaffAccount" }, JObject.Parse(legacy.Game.WriteValues[0].GetJson()).Properties().Select(field => field.Name));
            foreach (string invalid in new[] { "{broken", "{\"Version\":77,\"Staff\":[],\"PandaTokens\":0}" })
            {
                var fixture = CreateAccountRuntimeFixture(out var wallet);
                var original = fixture.Manager.StaffRuntime.Snapshot;
                RestoreAccountJson(fixture, invalid, expectedContinue: false);
                Assert.That(fixture.Manager.StaffRuntime.Mode.ToString(), Is.EqualTo("Unavailable"));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.Null);
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02")), Is.False);
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].UpgradeStaff(scope.Staff("STAFF01")), Is.False);
                Assert.That(original.PandaTokens, Is.EqualTo(55));
                Assert.That(wallet.CostCommits, Is.Zero);
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
            var staleFixture = CreateAccountRuntimeFixture(out var staleWallet);
            var prior = staleFixture.Manager.StaffRuntime.Snapshot;
            string oldAccount = staleFixture.Account;
            staleFixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            var oldReply = staleFixture.Game.GetReplies.Last();
            staleFixture.Manager.LogOut();
            Authenticate(staleFixture, Unique("new-common-account"));
            var oldRow = new JObject { ["owner_inDate"] = S(oldAccount), ["inDate"] = S(staleFixture.Row), ["Dia"] = N("110"), ["StaffAccount"] = S(CommonAccountJson()) };
            oldReply(Bro("200", Rows(oldRow)));
            Assert.That(staleFixture.Manager.StaffRuntime.Mode.ToString(), Is.EqualTo("Unavailable"));
            Assert.That(staleFixture.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(staleFixture.Stage.Runtime[EStage.Stage1].UpgradeStaff(scope.Staff("STAFF01")), Is.False);
            Assert.That(staleWallet.CostCommits, Is.Zero);
            Assert.That(prior.PandaTokens, Is.EqualTo(55));
        }
    }

    private Fixture CreateAccountRuntimeFixture(out MemoryStaffWallet wallet, bool common = true)
    {
        var fixture = Create(common);
        wallet = new MemoryStaffWallet();
        fixture.Stage.UseRuntimeStages = true;
        foreach (EStage stage in new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 })
            fixture.Stage.Runtime.Add(stage, new StageInfo(() => fixture.Manager.StaffRuntime, wallet));
        return fixture;
    }
    private void RestoreAccountJson(Fixture fixture, string json, bool expectedContinue = true)
    {
        bool continued = false;
        fixture.Manager.GetAndRestoreGameDataAsync((_, result) => continued = result.CanContinueLegacy);
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row), ["Dia"] = N("110"), ["StaffAccount"] = S(json) };
        fixture.Game.GetReplies.Last()(Bro("200", Rows(row)));
        Assert.That(continued, Is.EqualTo(expectedContinue));
    }
    private static string CommonAccountJson(int level = 2) => "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":" + level
        + "},{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}";
    private static void AssertAccountLevels(Fixture fixture, string id, int expected)
    {
        foreach (var stage in fixture.Stage.Runtime.Values)
        { Assert.That(stage.IsGiveStaff(id), Is.True, id); Assert.That(stage.GetStaffLevel(id), Is.EqualTo(expected), id); }
        Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Single(staff => staff.Id == id).Level, Is.EqualTo(expected));
    }
    private static void AssertSavedAccount(Param values, int staff01Level, bool hasStaff02, long tokens)
    {
        JObject payload = JObject.Parse(values.GetJson());
        Assert.That(payload["StaffAccount"].Type, Is.EqualTo(JTokenType.String));
        JObject account = JObject.Parse((string)payload["StaffAccount"]);
        Assert.That((int)account["Version"], Is.EqualTo(1));
        Assert.That((long)account["PandaTokens"], Is.EqualTo(tokens));
        var staff = ((JArray)account["Staff"]).ToDictionary(item => (string)item["Id"], item => (int)item["Level"]);
        Assert.That(staff["STAFF01"], Is.EqualTo(staff01Level)); Assert.That(staff["STAFF03"], Is.EqualTo(4));
        Assert.That(staff.ContainsKey("STAFF02"), Is.EqualTo(hasStaff02));
        Assert.That(staff.Count, Is.EqualTo(hasStaff02 ? 3 : 2));
        if (hasStaff02) Assert.That(staff["STAFF02"], Is.EqualTo(1));
    }
    private sealed class MemoryStaffWallet : IStaffUpgradeWallet
    {
        public long GoldValue = 1000000;
        public int DiamondValue = 110;
        public int ScoreValue = int.MaxValue;
        public int CostCommits, Notifications;
        public bool RefuseCost;
        public Action OnReadScore;
        public Action<UpgradeMoneyData> BeforeCostCommit, AfterCostNotification;
        public int Score { get { var read = OnReadScore; OnReadScore = null; read?.Invoke(); return ScoreValue; } }
        public long Gold => GoldValue;
        public int Diamonds => DiamondValue;
        public bool TryApplyCost(UpgradeMoneyData cost, long expectedGold, int expectedDiamonds)
        {
            BeforeCostCommit?.Invoke(cost);
            if (RefuseCost || expectedGold != GoldValue || expectedDiamonds != DiamondValue || cost == null || cost.Price < 0) return false;
            if (cost.MoneyType == MoneyType.Gold && GoldValue >= cost.Price) GoldValue -= cost.Price;
            else if (cost.MoneyType == MoneyType.Dia && DiamondValue >= cost.Price) DiamondValue -= cost.Price;
            else return false;
            CostCommits++; return true;
        }
        public void NotifyCost(UpgradeMoneyData cost) { Notifications++; AfterCostNotification?.Invoke(cost); }
    }
}
#endif
