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
using static CustomerTableUseRegressionTests;

public class StageWaitingSaveGuardTests
{
    [TestCase(false)] [TestCase(true)]
    public void ActiveAndUnvisitedStage_SaveSuccessAndReload_PreserveNewOrLegacyOriginal(bool legacy)
    {
        using (var f = new MemorySession())
        {
            var original = legacy ? (JToken)new JArray("A", "A", "B") : new JValue("{\"version\":1,\"teams\":[[\"A\"],[\"B\",\"A\"]]}");
            f.SetQueue(EStage.Stage2, original, legacy); f.LoadAll();
            Assert.That(f.Stages[1].WaitingParties.NeedsValidation, Is.True);
            Assert.That(f.Stages[1].WaitingParties.IsProtected, Is.False);
            int people = Resources.FindObjectsOfTypeAll<NormalCustomer>().Length;
            Set(f.Stages[0], "_tip", 43); Set(f.Stages[1], "_score", 81);
            UserInfo.SetWaitingParties(EStage.Stage1, new[] { new[] { "A", "B" } });
            var active = UserInfo.SaveStageDataAsync(EStage.Stage1);
            var inactive = UserInfo.SaveStageDataAsync(EStage.Stage2);
            Assert.That(active.Status, Is.EqualTo(StageSaveStatus.Pending));
            Assert.That(inactive.WaitingQueuePreserved, Is.True);
            f.Ack(1); f.Ack(0); // Independent Stage replies may arrive in reverse order.
            Assert.That(active.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(inactive.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(JToken.DeepEquals(f.Queue(EStage.Stage2, legacy), original), Is.True);
            Assert.That(legacy ? f.Rows[EStage.Stage2][WaitingPartySaveData.FieldName] == null : true, Is.True);
            var a = f.Reload(EStage.Stage1); var b = f.Reload(EStage.Stage2);
            Assert.That(a.Tip, Is.EqualTo(43)); Assert.That(a.WaitingParties.Teams.Single(), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(b.Score, Is.EqualTo(81)); Assert.That(b.WaitingParties.Teams.SelectMany(t => t), Is.EqualTo(new[] { "A", legacy ? "A" : "B", legacy ? "B" : "A" }));
            Assert.That(Resources.FindObjectsOfTypeAll<NormalCustomer>().Length, Is.EqualTo(people));
            Assert.That(Backend.IsInitialized || Backend.IsLogin, Is.False);
        }
    }

    [Test]
    public void NewEmpty_IsAuthoritativeAfterActualWriteAckAndSdkReload()
    {
        using (var f = new MemorySession())
        {
            f.Rows[EStage.Stage2]["WaitingCustomerIdList"] = new JArray("OLD", "OLD"); f.LoadAll();
            Set(f.Stages[1], "_tip", 71); var request = UserInfo.SaveStageData(EStage.Stage2);
            Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            var loaded = f.Reload(EStage.Stage2); Assert.That(loaded.Tip, Is.EqualTo(71));
            Assert.That(loaded.WaitingParties.Teams, Is.Empty); Assert.That(loaded.WaitingParties.IsLegacy, Is.False);
        }
    }

    [TestCase("malformed")] [TestCase("future")] [TestCase("object")] [TestCase("array")]
    [TestCase("unknown")] [TestCase("limit")] [TestCase("members")]
    public void ProtectedQueue_SameStageCurrentFieldsAndUntouchedFieldsSurviveAckAndReload(string kind)
    {
        using (var f = new MemorySession())
        {
            JToken original = kind == "malformed" ? (JToken)new JValue("{broken") : kind == "future" ? new JValue(" { \"version\":9, \"teams\":[], \"future\":{\"x\":[1,2]} } ")
                : kind == "object" ? (JToken)new JObject { ["opaque"] = new JArray(1, 2) } : kind == "array" ? new JArray("opaque", 3)
                : new JValue(new JObject { ["version"] = 1, ["teams"] = kind == "unknown" ? new JArray(new JArray("UNKNOWN"))
                    : kind == "members" ? new JArray(new JArray("A", "B", "C")) : new JArray(Enumerable.Range(0, 31).Select(_ => new JArray("A"))) }.ToString(Formatting.None));
            f.SetQueue(EStage.Stage2, original); f.LoadAll();
            var stage = f.Stages[1];
            if (kind == "unknown" || kind == "limit")
            {
                Assert.That(stage.WaitingParties.ValidateForRestore(30, id => id == "A" || id == "B", out string reason), Is.False);
                stage.ProtectWaitingParties(reason);
            }
            Assert.That(stage.WaitingParties.IsProtected, Is.True);
            Assert.That(stage.Tip, Is.EqualTo(17)); Assert.That(stage.Score, Is.EqualTo(23));
            Set(stage, "_tip", 26);
            Get<SaveTableData[,]>(stage, "_saveTableDatas")[0, 0].SetNeedCleaning(false);
            Get<CoinAreaData[,]>(stage, "_coinAreaDatas")[0, 0].SetMoney(67);
            var waiting = stage.WaitingParties;
            var request = UserInfo.SaveStageDataAsync(EStage.Stage2);
            Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Pending));
            Assert.That(JObject.Parse(f.Writes.Single().Json).ContainsKey(WaitingPartySaveData.FieldName), Is.False);
            Assert.That(JObject.Parse(f.Writes.Single().Json).ContainsKey("WaitingCustomerIdList"), Is.False);
            f.Ack(0); Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(stage.WaitingParties, Is.SameAs(waiting), "ACK cannot validate or replace the protected queue");
            Assert.That(JToken.DeepEquals(f.Queue(EStage.Stage2), original), Is.True);
            var loaded = f.Reload(EStage.Stage2);
            Assert.That(loaded.Tip, Is.EqualTo(26)); Assert.That(loaded.Score, Is.EqualTo(23));
            Assert.That(loaded.SaveData().SaveTableDataList[0][0].NeedCleaning, Is.False);
            Assert.That(loaded.SaveData().CoinAreaDataList[0][0].Money, Is.EqualTo(67));
            Assert.That(loaded.WaitingParties.Teams.Count, Is.EqualTo(waiting.Teams.Count));
        }
    }

    [Test]
    public void CatalogNotReady_IsPendingNotUnknown_AndOtherFieldsCanSaveWithoutCustomers()
    {
        using (var f = new MemorySession())
        {
            f.SetQueue(EStage.Stage3, new JValue("{\"version\":1,\"teams\":[[\"NOT_YET_LOADED\"]]}")); f.LoadAll();
            var waiting = f.Stages[2].WaitingParties;
            Assert.That(waiting.ValidateForRestore(null, null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("WaitingValidationNotReady")); Assert.That(waiting.IsProtected, Is.False);
            Set(f.Stages[2], "_score", 99);
            Assert.That(UserInfo.SaveStageData(EStage.Stage3).Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(f.Reload(EStage.Stage3).Score, Is.EqualTo(99));
            Assert.That(waiting.NeedsValidation, Is.True);
        }
    }

    [TestCase("missing")] [TestCase("multiple")] [TestCase("wrongOwner")] [TestCase("replaced")]
    public void MissingOrChangedOriginalRow_HoldsUpdateAndNeverInserts(string mode)
    {
        using (var f = new MemorySession())
        {
            f.SetQueue(EStage.Stage2, new JValue("{broken")); f.LoadAll(); f.ReadMode = mode;
            var request = UserInfo.SaveStageData(EStage.Stage2);
            Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Held)); Assert.That(f.Writes, Is.Empty);
            Assert.That(request.Reason, Does.Contain("existing Stage row"));
        }
    }

    [Test]
    public void SameStageOverlaps_AreSerialized_AndOldDuplicateAckCannotClearNewChanges()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); Set(f.Stages[0], "_tip", 31); var a = UserInfo.SaveStageDataAsync(EStage.Stage1);
            Set(f.Stages[0], "_tip", 42); var b = UserInfo.SaveStageDataAsync(EStage.Stage1);
            Assert.That(f.Writes.Count, Is.EqualTo(1)); f.Ack(0);
            Assert.That(f.Writes.Count, Is.EqualTo(2)); Assert.That((int)JObject.Parse(f.Writes[1].Json)["Tip"], Is.EqualTo(42));
            Set(f.Stages[0], "_tip", 53); f.Ack(1); f.Writes[0].Reply(Response("204", ""));
            Assert.That(f.Stages[0].Tip, Is.EqualTo(53)); Assert.That(a.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(b.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(42));
            UserInfo.SaveStageData(EStage.Stage1); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(53));
        }
    }

    [Test]
    public void FailureAndRetry_UsesLatestNormalChangesAndKeepsOpaqueQueue()
    {
        using (var f = new MemorySession())
        {
            var original = new JValue("{\"version\":99,\"unknown\":[1,2]}"); f.SetQueue(EStage.Stage2, original); f.LoadAll();
            Set(f.Stages[1], "_tip", 31); var request = UserInfo.SaveStageDataAsync(EStage.Stage2);
            f.Writes[0].Reply(Response("500", "")); Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Failed));
            Assert.That((int)f.Rows[EStage.Stage2]["Tip"], Is.EqualTo(17));
            Set(f.Stages[1], "_tip", 64); var retry = request.Retry(); f.Ack(1);
            Assert.That(retry.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage2).Tip, Is.EqualTo(64));
            Assert.That(JToken.DeepEquals(f.Queue(EStage.Stage2), original), Is.True);
        }
    }

    [Test]
    public void AccountChange_LateGetAndAckAndRetryCannotUseAnotherAccountsStage()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); var sent = UserInfo.SaveStageDataAsync(EStage.Stage1);
            f.DelayReads = true; var read = UserInfo.SaveStageDataAsync(EStage.Stage2); var oldRead = f.Reads.Last();
            f.Authenticate("B"); f.DelayReads = false; f.LoadAll(); Set(f.Stages[1], "_tip", 88);
            oldRead(); f.Writes[0].Reply(Response("204", ""));
            Assert.That(sent.Status, Is.EqualTo(StageSaveStatus.Superseded)); Assert.That(read.Status, Is.EqualTo(StageSaveStatus.Superseded));
            Assert.That(sent.Retry().Status, Is.EqualTo(StageSaveStatus.Held));
            Assert.That(f.Writes.Count, Is.EqualTo(1)); Assert.That(f.Stages[1].Tip, Is.EqualTo(88));
        }
    }

    [Test]
    public void ReverseLoadRoundAndLocalChangeWhileLoading_CannotOverwriteLatestRuntime()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); f.DelayReads = true; f.Owner.LoadStageData(EStage.Stage2, true); var older = f.Reads.Last();
            f.Rows[EStage.Stage2]["Tip"] = 91; f.Owner.LoadStageData(EStage.Stage2, true); f.Reads.Last()(); older();
            Assert.That(f.Stages[1].Tip, Is.EqualTo(91));
            f.Owner.LoadStageData(EStage.Stage2, true); var beforeMutation = f.Reads.Last(); Set(f.Stages[1], "_tip", 106); beforeMutation();
            Assert.That(f.Stages[1].Tip, Is.EqualTo(106));
        }
    }

    [Test]
    public void LaterProductionRestoreAndGuide_SaveNewQueueInsteadOfOldOriginal()
    {
        using (var flow = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(flow.Customer);
            using (var f = new MemorySession(GetStaticStages()))
            {
                f.SetQueue(EStage.Stage1, new JValue("{\"version\":1,\"teams\":[[\"" + flow.Data.Id + "\",\"" + flow.Data.Id + "\"]]}"));
                f.LoadAll(); Invoke(flow.Controller, "LoadWaitingCustomers");
                Assert.That(flow.Controller.Count, Is.EqualTo(1));
                Assert.That(flow.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
                Assert.That(flow.Controller.TrySpawnWaitingParty(new[] { flow.Data.Id }, out _), Is.True);
                Assert.That(UserInfo.SaveStageData(EStage.Stage1).Status, Is.EqualTo(StageSaveStatus.Succeeded));
                var loaded = f.Reload(EStage.Stage1); Assert.That(loaded.WaitingParties.Teams.Single().Count, Is.EqualTo(1));
                Assert.That(loaded.WaitingParties.Teams.Single().Single(), Is.EqualTo(flow.Data.Id));
            }
        }
    }

    [Test]
    public void CompletionMayQueueAnotherSave_WithoutStartingItTwice()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); StageSaveRequest second = null;
            var first = UserInfo.SaveStageDataAsync(EStage.Stage1, completed: _ =>
            { Set(f.Stages[0], "_tip", 82); second = UserInfo.SaveStageDataAsync(EStage.Stage1); });
            f.Ack(0); Assert.That(f.Writes.Count, Is.EqualTo(2)); f.Ack(1);
            Assert.That(first.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(second.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(82));
        }
    }

    [Test]
    public void SameAccountNewSession_WaitsForOldPhysicalWriteBeforeSendingNewValues()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); Set(f.Stages[0], "_tip", 31); var old = UserInfo.SaveStageDataAsync(EStage.Stage1);
            f.Authenticate("A"); f.LoadAll(); Set(f.Stages[0], "_tip", 89);
            var current = UserInfo.SaveStageDataAsync(EStage.Stage1);
            Assert.That(f.Writes.Count, Is.EqualTo(1)); Assert.That(current.Status, Is.EqualTo(StageSaveStatus.Pending));
            f.Ack(0); Assert.That(old.Status, Is.EqualTo(StageSaveStatus.Superseded));
            Assert.That(f.Stages[0].Tip, Is.EqualTo(89)); Assert.That(f.Writes.Count, Is.EqualTo(2)); f.Ack(1);
            Assert.That(current.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(89));
        }
    }

    [Test]
    public void SaveReadFailure_DoesNotReloadOverUnsavedChanges_AndRetryUsesLatest()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); Set(f.Stages[1], "_tip", 56); f.FailSaveRead = true;
            var request = UserInfo.SaveStageDataAsync(EStage.Stage2);
            Assert.That(request.Status, Is.EqualTo(StageSaveStatus.Failed)); Assert.That(f.Stages[1].Tip, Is.EqualTo(56));
            Assert.That(f.Writes, Is.Empty); f.FailSaveRead = false; Set(f.Stages[1], "_tip", 78);
            var retry = request.Retry(); f.Ack(0);
            Assert.That(retry.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage2).Tip, Is.EqualTo(78));
        }
    }

    [TestCase("badNumber")] [TestCase("null")] [TestCase("multipleTypes")]
    public void InvalidWaitingSdkWrapper_DoesNotReachFlatten_AndRemainsByteForByteOnServer(string kind)
    {
        using (var f = new MemorySession())
        {
            var wrapper = kind == "badNumber" ? (JToken)new JObject { ["N"] = "not-a-number" }
                : kind == "null" ? JValue.CreateNull() : new JObject { ["S"] = "{bad", ["N"] = "7" };
            f.RawQueueAttributes[EStage.Stage2] = wrapper; f.LoadAll();
            Assert.That(f.Stages[1].WaitingParties.IsProtected, Is.True);
            Assert.That(f.Stages[1].Tip, Is.EqualTo(17)); string original = wrapper.ToString(Formatting.None);
            Set(f.Stages[1], "_tip", 95); var save = UserInfo.SaveStageDataAsync(EStage.Stage2); f.Ack(0);
            Assert.That(save.Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(f.RawQueueAttributes[EStage.Stage2].ToString(Formatting.None), Is.EqualTo(original));
            var reload = f.Reload(EStage.Stage2); Assert.That(reload.Tip, Is.EqualTo(95)); Assert.That(reload.WaitingParties.IsProtected, Is.True);
        }
    }

    [TestCase("Tip")] [TestCase("GiveFurnitureList")] [TestCase("SaveTableDataList")]
    public void InvalidNonWaitingFields_HoldOnlyTheirStage_WithoutSavingDefaults(string field)
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); f.SetQueue(EStage.Stage2, new JValue("{broken")); f.Rows[EStage.Stage2][field] = "bad-type";
            Assert.That(f.Apply(EStage.Stage2, f.Read(EStage.Stage2, f.AccountInDate), () => true, false), Is.False);
            Assert.That(UserInfo.SaveStageData(EStage.Stage2).Status, Is.EqualTo(StageSaveStatus.Held));
            Assert.That(f.Writes, Is.Empty); Set(f.Stages[0], "_tip", 102);
            Assert.That(UserInfo.SaveStageData(EStage.Stage1).Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(102)); Assert.That((string)f.Rows[EStage.Stage2][field], Is.EqualTo("bad-type"));
        }
    }

    [Test]
    public void RuntimeApplyException_RemovesPreviousBindingSoPartialStateCannotSave()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); f.SetQueue(EStage.Stage2, new JValue("{broken"));
            f.Rows[EStage.Stage2]["GiveStaffList"] = new JArray(new JObject { ["Id"] = "STAFF01", ["Level"] = 1 }, new JObject { ["Id"] = "STAFF01", ["Level"] = 1 });
            Assert.Throws<TargetInvocationException>(() => f.Apply(EStage.Stage2, f.Read(EStage.Stage2, f.AccountInDate), () => true, false));
            Assert.That(UserInfo.SaveStageData(EStage.Stage2).Status, Is.EqualTo(StageSaveStatus.Held)); Assert.That(f.Writes, Is.Empty);
        }
    }

    [Test]
    public void DelayedRetryFactoryFailure_CompletesReceiptAndReleasesQueuedSave()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); f.RetryFactoryFailure = true;
            var first = UserInfo.SaveStageDataAsync(EStage.Stage1); Set(f.Stages[0], "_tip", 76);
            var second = UserInfo.SaveStageDataAsync(EStage.Stage1);
            f.RetryFactoryFailure = false; f.Writes[0].Reply(Response("500", ""));
            Assert.That(first.Status, Is.EqualTo(StageSaveStatus.Failed)); Assert.That(f.Writes.Count, Is.EqualTo(2)); f.Ack(1);
            Assert.That(second.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(76));
        }
    }

    [Test]
    public void ExpiredReadHead_WithValidQueuedSuccessor_DoesNotStrandFollowingSaves()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); bool firstCurrent = true; f.DelayReads = true;
            var first = UserInfo.SaveStageDataAsync(EStage.Stage1, () => firstCurrent); var oldRead = f.Reads.Last();
            var second = UserInfo.SaveStageDataAsync(EStage.Stage1);
            firstCurrent = false; f.DelayReads = false; Set(f.Stages[0], "_tip", 92);
            var third = UserInfo.SaveStageDataAsync(EStage.Stage1); oldRead();
            Assert.That(first.Status, Is.EqualTo(StageSaveStatus.Superseded)); Assert.That(f.Writes.Count, Is.EqualTo(1));
            f.Ack(0); Assert.That(second.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Writes.Count, Is.EqualTo(2)); f.Ack(1);
            Assert.That(third.Status, Is.EqualTo(StageSaveStatus.Succeeded)); Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(92));
        }
    }

    [Test]
    public void ActualFurnitureLoadEvent_CannotSavePartiallyAppliedStageThroughOldBinding()
    {
        using (var flow = new Flow())
        {
            var furniture = flow.Track(ScriptableObject.CreateInstance<FurnitureData>());
            Set(furniture, "_id", "SAVE-GUARD-FURNITURE"); Set(furniture, "_type", (FurnitureType)0);
            flow.Swap(typeof(FurnitureDataManager), "_instance", flow.Create<FurnitureDataManager>());
            flow.Swap(typeof(FurnitureDataManager), "_furnitureDataDic", new Dictionary<string, FurnitureData> { [furniture.Id] = furniture });
            using (var f = new MemorySession(GetStaticStages()))
            {
                f.LoadAll(); f.SetQueue(EStage.Stage2, new JValue("{broken"));
                f.Rows[EStage.Stage2]["GiveFurnitureList"] = new JArray(furniture.Id);
                f.Rows[EStage.Stage2]["EquipFurnitureList"][0][0] = furniture.Id;
                StageSaveRequest duringLoad = null;
                f.Stages[1].OnChangeFurnitureHandler += (_, __) => duringLoad = UserInfo.SaveStageDataAsync(EStage.Stage2);
                Assert.That(f.Apply(EStage.Stage2, f.Read(EStage.Stage2, f.AccountInDate), () => true, false), Is.True);
                Assert.That(duringLoad, Is.Not.Null); Assert.That(duringLoad.Status, Is.EqualTo(StageSaveStatus.Held)); Assert.That(f.Writes, Is.Empty);
                Set(f.Stages[1], "_tip", 87); Assert.That(UserInfo.SaveStageData(EStage.Stage2).Status, Is.EqualTo(StageSaveStatus.Succeeded));
                var loaded = f.Reload(EStage.Stage2); Assert.That(loaded.Tip, Is.EqualTo(87));
                Assert.That(loaded.SaveData().GiveFurnitureList, Does.Contain(furniture.Id)); Assert.That(loaded.WaitingParties.IsProtected, Is.True);
            }
        }
    }

    [Test]
    public void RawCollectionRejection_CannotKeepAnOldBindingAndOverwriteNewProtectedData()
    {
        using (var f = new MemorySession())
        {
            f.LoadAll(); f.SetQueue(EStage.Stage2, new JValue("{new-opaque"));
            f.Rows[EStage.Stage2]["GiveStaffList"] = "bad-staff-shape";
            f.Owner.LoadStageData(EStage.Stage2, false);
            Assert.That(UserInfo.SaveStageData(EStage.Stage2).Status, Is.EqualTo(StageSaveStatus.Held)); Assert.That(f.Writes, Is.Empty);
            Assert.That((string)f.Queue(EStage.Stage2), Is.EqualTo("{new-opaque"));
            Set(f.Stages[0], "_tip", 104); Assert.That(UserInfo.SaveStageData(EStage.Stage1).Status, Is.EqualTo(StageSaveStatus.Succeeded));
            Assert.That(f.Reload(EStage.Stage1).Tip, Is.EqualTo(104));
        }
    }

    private static StageInfo[] GetStaticStages() => (StageInfo[])Static(typeof(UserInfo), "_stageInfos").GetValue(null);
    private static FieldInfo Static(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    [TestCase(false)] [TestCase(true)]
    public void PartyCleaningMarker_ActualSaveRetryAndReload_PreservesSinkAndProtectedQueue(bool legacy)
    {
        using (var f = new MemorySession())
        {
            var opaque=new JValue("{broken waiting original"); f.SetQueue(EStage.Stage2,opaque);
            var originalLegacy=f.Rows[EStage.Stage2]["WaitingCustomerIdList"].DeepClone();
            if(legacy) ((JObject)f.Rows[EStage.Stage2]["SaveTableDataList"][0][0]).Remove("CleaningBowlProcessed");
            f.LoadAll(); var stage=f.Stages[1]; var floor=ERestaurantFloorType.Floor1;
            var saved=stage.GetTableData(floor,TableType.Table1);
            Assert.That(saved.CleaningBowlProcessed,Is.False);
            Assert.That(stage.TryProcessCleaningBowl(floor,TableType.Table1,saved),Is.True);
            Assert.That(saved.NeedCleaning&&saved.CleaningBowlProcessed,Is.True);
            var request=UserInfo.SaveStageDataAsync(EStage.Stage2);
            f.Writes[0].Reply(Response("500","")); Assert.That(request.Status,Is.EqualTo(StageSaveStatus.Failed));
            Assert.That(stage.GetSinkBowlCount(floor),Is.EqualTo(1)); Assert.That(saved.CleaningBowlProcessed,Is.True);
            Set(stage,"_tip",91); var retry=request.Retry(); f.Ack(1);
            Assert.That(retry.Status,Is.EqualTo(StageSaveStatus.Succeeded));
            var loaded=f.Reload(EStage.Stage2); var table=loaded.GetTableData(floor,TableType.Table1);
            Assert.That(table.CleaningBowlProcessed&&table.NeedCleaning,Is.True);
            Assert.That(loaded.TryProcessCleaningBowl(floor,TableType.Table1,table),Is.True);
            Assert.That(loaded.GetSinkBowlCount(floor),Is.EqualTo(1));
            Assert.That(loaded.Tip,Is.EqualTo(91));
            Assert.That(JToken.DeepEquals(f.Queue(EStage.Stage2),opaque),Is.True);
            Assert.That(JToken.DeepEquals(f.Rows[EStage.Stage2]["WaitingCustomerIdList"],originalLegacy),Is.True);
        }
    }

    [Test]
    public void PartyCleaningBowl_ObserverSnapshotIsConsistent_AndExceptionCannotRepeatCommit()
    {
        using(var f=new MemorySession())
        {
            f.LoadAll(); var stage=f.Stages[0]; var floor=ERestaurantFloorType.Floor1;
            JObject snapshot=null;
            stage.OnChangeSinkBowlHandler+=()=> {snapshot=JObject.Parse(stage.SaveData().GetParam().GetJson()); throw new InvalidOperationException("owned bowl observer");};
            Assert.Throws<InvalidOperationException>(()=>stage.TryProcessCleaningBowl(floor,TableType.Table1));
            Assert.That((bool)snapshot["SaveTableDataList"][0][0]["NeedCleaning"],Is.True);
            Assert.That((bool)snapshot["SaveTableDataList"][0][0]["CleaningBowlProcessed"],Is.True);
            Assert.That((int)snapshot["SaveKitchenDataList"][0]["SinkBowlCount"],Is.EqualTo(1));
            Assert.That(stage.TryProcessCleaningBowl(floor,TableType.Table1),Is.True);
            Assert.That(stage.GetSinkBowlCount(floor),Is.EqualTo(1));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void MalformedCleaningMarker_HoldsStageWithoutOverwritingOriginal(bool protectedQueue)
    {
        using(var f=new MemorySession())
        {
            if(protectedQueue) f.SetQueue(EStage.Stage2,new JValue("{broken"));
            f.Rows[EStage.Stage2]["SaveTableDataList"][0][0]["CleaningBowlProcessed"]="invalid";
            f.LoadAll();
            Assert.That(UserInfo.SaveStageData(EStage.Stage2).Status,Is.EqualTo(StageSaveStatus.Held));
            Assert.That(f.Writes,Is.Empty);
            Assert.That((string)f.Rows[EStage.Stage2]["SaveTableDataList"][0][0]["CleaningBowlProcessed"],Is.EqualTo("invalid"));
        }
    }
    private static BackendReturnObject Response(string code, string json) => StaffGachaOfflineSession.Response(code, json);
    private static JObject Attribute(JToken value)
    {
        if (value is JObject obj) return new JObject { ["M"] = new JObject(obj.Properties().Select(p => new JProperty(p.Name, Attribute(p.Value)))) };
        if (value is JArray array) return new JObject { ["L"] = new JArray(array.Select(Attribute)) };
        if (value.Type == JTokenType.String) return new JObject { ["S"] = value };
        if (value.Type == JTokenType.Boolean) return new JObject { ["BOOL"] = value };
        if (value.Type == JTokenType.Null) return new JObject { ["NULL"] = true };
        return new JObject { ["N"] = value.ToString(Formatting.None) };
    }

    private sealed class MemorySession : IStageDataLoadTransport, IStageDataWriteTransport, IGameDataBackendTransport, IDisposable
    {
        internal sealed class Write { internal string Account, Json; internal EStage Stage; internal Action<BackendReturnObject> Reply; }
        internal readonly Dictionary<EStage, JObject> Rows = new Dictionary<EStage, JObject>();
        internal readonly Dictionary<EStage, JToken> RawQueueAttributes = new Dictionary<EStage, JToken>();
        internal readonly List<Write> Writes = new List<Write>();
        internal readonly List<Action> Reads = new List<Action>();
        internal readonly StageInfo[] Stages;
        internal readonly BackendManager Owner;
        private readonly object oldStages, oldOwner;
        internal bool DelayReads;
        internal bool FailSaveRead, RetryFactoryFailure;
        internal string ReadMode;
        public bool LoggedIn { get; private set; }
        public bool NativeLoggedIn => LoggedIn;
        public string AccountInDate { get; private set; }
        public bool GameplaySaveAllowed => true;
        internal MemorySession(StageInfo[] stages = null)
        {
            oldStages = Static(typeof(UserInfo), "_stageInfos").GetValue(null); oldOwner = Static(typeof(BackendManager), "_instance").GetValue(null);
            Stages = stages ?? new[] { new StageInfo(), new StageInfo(), new StageInfo() };
            Static(typeof(UserInfo), "_stageInfos").SetValue(null, Stages);
            for (int i = 0; i < 3; i++)
            {
                var data = new StageInfo().SaveData(); data.Tip = 17; data.Score = 23;
                data.SaveTableDataList[0][0].SetNeedCleaning(true); data.CoinAreaDataList[0][0].SetMoney(41);
                Rows[(EStage)i] = JObject.Parse(data.GetParam().GetJson());
            }
            Owner = BackendManager.CreateEditorOfflineOwner(this, this, UserInfo.StaffPurchaseWallet, _ => throw new Exception("No draws"), _ => { });
            Static(typeof(BackendManager), "_instance").SetValue(null, Owner);
            Authenticate("A");
        }
        internal void Authenticate(string account)
        {
            LoggedIn = false; var auth = Owner.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
            // Match real SDK inDate strings; Json.NET must not normalize these as dates.
            AccountInDate = account == "A" ? "2026-10-14T00:00:00.001Z" : "2026-10-14T00:00:00.002Z"; LoggedIn = true;
            Assert.That(Owner.NotifyFederationLoginSuccess(auth, Response("200", "")), Is.True);
            Owner.GetAndRestoreGameDataAsync((_, result) => Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.Ready), result.Reason));
        }
        internal void SetQueue(EStage stage, JToken value, bool legacy = false)
        { if (legacy) Rows[stage].Remove(WaitingPartySaveData.FieldName); Rows[stage][legacy ? "WaitingCustomerIdList" : WaitingPartySaveData.FieldName] = value.DeepClone(); }
        internal JToken Queue(EStage stage, bool legacy = false) => Rows[stage][legacy ? "WaitingCustomerIdList" : WaitingPartySaveData.FieldName];
        internal void LoadAll() => Owner.LoadAllStageData(false);
        internal StageInfo Reload(EStage stage)
        { var result = new StageInfo(); Assert.That(UserInfo.TryApplyStageDataResponse(stage, Read(stage, AccountInDate), () => true, result.LoadData, null), Is.True); return result; }
        internal void Ack(int index)
        {
            var write = Writes[index]; Assert.That(write.Account, Is.EqualTo(AccountInDate));
            foreach (var value in JObject.Parse(write.Json).Properties()) Rows[write.Stage][value.Name] = value.Value.DeepClone();
            write.Reply(Response("204", ""));
        }
        internal BackendReturnObject Read(EStage stage, string account)
        {
            var row = new JObject { ["owner_inDate"] = Attribute(new JValue(ReadMode == "wrongOwner" ? "other" : account)),
                ["inDate"] = Attribute(new JValue(ReadMode == "replaced" ? "2026-10-14T01:01:00.123Z" : "2026-10-14T01:00:0" + (int)stage + ".123Z")) };
            foreach (var field in Rows[stage].Properties()) row[field.Name] = Attribute(field.Value);
            if (RawQueueAttributes.TryGetValue(stage, out var raw)) row[WaitingPartySaveData.FieldName] = raw.DeepClone();
            var rows = ReadMode == "missing" ? new JArray() : ReadMode == "multiple" ? new JArray(row, row.DeepClone()) : new JArray(row);
            return Response("200", new JObject { ["rows"] = rows, ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None));
        }
        public void Get(EStage stage, string account, Func<bool> current, Action<BackendReturnObject> reply)
        { var response = Read(stage, account); if (DelayReads) Reads.Add(() => reply(response)); else reply(response); }
        public BackendReturnObject Get(EStage stage, string account, Func<bool> current) => Read(stage, account);
        public bool Apply(EStage stage, BackendReturnObject response, Func<bool> current, bool asynchronous) =>
            (bool)typeof(UserInfo).GetMethod("ApplyLoadedStageData", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { stage, response, current, asynchronous });
        public StaffStageRuntimeSnapshot ReadStaff(EStage stage) => Stages[(int)stage].CaptureStaffRuntimeSnapshot();
        public void ReadForSave(EStage stage, string account, Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply)
        { if (FailSaveRead) reply(Response("500", "")); else if (asynchronous) Get(stage, account, current, reply); else reply(Get(stage, account, current)); }
        public void Update(EStage stage, string account, string row, Func<Param> latest, Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply)
        {
            Assert.That(current(), Is.True);
            if (RetryFactoryFailure)
            {
                Action<BackendReturnObject> retry = _ => Attempt(() => throw new InvalidOperationException("Later retry guard"),
                    (values, received) => Assert.Fail("Must not transmit a rejected retry"), reply, () => reply(null));
                Attempt(latest, (values, received) => Writes.Add(new Write { Account = account, Stage = stage, Json = values.GetJson(), Reply = received }), retry, () => reply(null));
            }
            else { Writes.Add(new Write { Account = account, Stage = stage, Json = latest().GetJson(), Reply = reply }); if (!asynchronous) Ack(Writes.Count - 1); }
        }
        private static void Attempt(Func<Param> latest, Action<Param, Action<BackendReturnObject>> send, Action<BackendReturnObject> received, Action failed)
            => typeof(BackendManager).GetMethod("AttemptStageWrite", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { latest, send, received, failed });
        public IReadOnlyList<StaffData> ReadCatalog() => Resources.LoadAll<StaffData>("StaffData");
        public bool Restore(BackendReturnObject response) => true;
        public Param InitialValues() => throw new Exception("No account Insert");
        public Param LatestValues() => new Param();
        public void Insert(Param values, Action<BackendReturnObject> reply) => throw new Exception("No account Insert");
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> reply) => throw new Exception("No GameData writes");
        public void Get(string account, Action<BackendReturnObject> reply)
        { reply(Response("200", new JObject { ["rows"] = new JArray(new JObject { ["owner_inDate"] = Attribute(new JValue(account)), ["inDate"] = Attribute(new JValue("game-row-" + account)), ["Dia"] = Attribute(new JValue(110)), ["StaffAccount"] = Attribute(new JValue("{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}")) }), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None))); }
        public void Dispose()
        {
            Static(typeof(BackendManager), "_instance").SetValue(null, oldOwner);
            Static(typeof(UserInfo), "_stageInfos").SetValue(null, oldStages);
            Owner?.DestroyEditorOfflineOwner();
        }
    }
}
#endif
