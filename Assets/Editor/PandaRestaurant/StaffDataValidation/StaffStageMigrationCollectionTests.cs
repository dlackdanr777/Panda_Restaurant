#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Actual load/parse/A2 entry methods, with memory-only SDK boundaries and a detached legacy application sink.</summary>
public class StaffStageMigrationCollectionTests
{
    private readonly Dictionary<StaffData, string> _assets = new Dictionary<StaffData, string>();
    private readonly Dictionary<BackendReturnObject, string> _responses = new Dictionary<BackendReturnObject, string>();
    private Random.State _random;
    private string _gameState;
    private bool _saveEnabled;
    private GameDataSaveCoordinator[] _originalOwners;

    [SetUp]
    public void SetUp()
    {
        _random = Random.state;
        _gameState = GameState();
        _saveEnabled = (bool)Field(typeof(BackendManager), "_isSaveEnabled", true).GetValue(null);
        _originalOwners = Owners().ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_random));
            Assert.That(GameState(), Is.EqualTo(_gameState), "Actual UserInfo/PaymentInfo changed");
            foreach (var asset in _assets) Assert.That(EditorJsonUtility.ToJson(asset.Key), Is.EqualTo(asset.Value));
            foreach (var response in _responses) Assert.That(Describe(response.Key), Is.EqualTo(response.Value), "Raw SDK response changed");
        }
        finally
        {
            Field(typeof(BackendManager), "_isSaveEnabled", true).SetValue(null, _saveEnabled);
            Owners().Clear(); foreach (var owner in _originalOwners) Owners().Add(owner);
            _assets.Clear(); _responses.Clear();
        }
    }

    [Test]
    public void AllStages_ReverseAndDuplicateResponsesCreateOneUnsavedPlanFromUnmodifiedRawRecords()
    {
        foreach (bool asynchronous in new[] { true, false })
        {
            var fixture = Create();
            var raw = new Dictionary<EStage, BackendReturnObject>
            {
                [EStage.Stage1] = StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 2, "SKIN_ONE")),
                [EStage.Stage2] = StageResponse(fixture, EStage.Stage2, Staff("STAFF01", 2, "SKIN_TWO"), Staff("STAFF03", 4)),
                [EStage.Stage3] = StageResponse(fixture, EStage.Stage3)
            };
            fixture.Stage.SyncResponse = stage => raw[stage];
            fixture.Manager.LoadAllStageData(asynchronous);
            var collection = fixture.Manager.StageMigrationCollection;
            CollectionAssert.AreEqual(new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 }, collection.RequiredStages);
            Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(3));
            foreach (var get in fixture.Stage.Gets) Assert.That(get.Account, Is.EqualTo(fixture.Account));
            if (asynchronous)
            {
                Assert.That(collection.CompletedStageCount, Is.Zero);
                Assert.That(collection.Result, Is.Null);
                Assert.That(fixture.Manager.RequestGameDataAutosave().Accepted, Is.True);
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                fixture.Game.WriteReplies.Single()(Bro("204", ""));
                Assert.That(fixture.Manager.StageMigrationCollection, Is.SameAs(collection));
                Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Collecting),
                    "Creating the first normal save coordinator must not invalidate an unrelated Stage collection round");
                foreach (EStage stage in new[] { EStage.Stage3, EStage.Stage2, EStage.Stage1 })
                {
                    fixture.Stage.Gets.Single(get => get.Stage == stage).Reply(raw[stage]);
                    if (stage != EStage.Stage1) Assert.That(collection.PlanCount, Is.Zero);
                }
                foreach (var get in fixture.Stage.Gets) get.Reply(raw[get.Stage]);
            }
            Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.UnsavedMigrationCandidate), collection.Error);
            Assert.That(collection.CompletedStageCount, Is.EqualTo(3));
            Assert.That(collection.CompletionCount, Is.EqualTo(1));
            Assert.That(collection.PlanCount, Is.EqualTo(1));
            Assert.That(collection.Result.Status, Is.EqualTo(StaffAccountLoadStatus.UnsavedMigrationCandidate));
            Assert.That(collection.Result.ExistingData, Is.Null);
            CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03" }, collection.Result.MigrationCandidate.Staff.Select(staff => staff.Id));
            CollectionAssert.AreEqual(new[] { 2, 4 }, collection.Result.MigrationCandidate.Staff.Select(staff => staff.Level));
            Assert.That(collection.Result.MigrationCandidate.PandaTokens, Is.Zero);
            Assert.That(collection.Stages[0].Records[0].SkinId, Is.EqualTo("SKIN_ONE"));
            Assert.That(collection.Stages[1].Records[0].SkinId, Is.EqualTo("SKIN_TWO"));
            Assert.That(collection.Stages[2].HasVerifiedRawRecords, Is.True);
            Assert.That(collection.Stages[2].Records, Is.Empty);
            Assert.That(fixture.Stage.Applied.Count, Is.EqualTo(3));
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.EqualTo(asynchronous ? 1 : 0), "No writes beyond the explicitly requested general autosave");
            Assert.That(fixture.Game.Inserts, Is.Zero);
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            fixture.Stage.Applied[EStage.Stage1].GiveStaffList[0].LevelUp();
            fixture.Stage.Applied[EStage.Stage1].GiveStaffList[0].SetSkinId("DETACHED_LEGACY_CHANGED");
            Assert.That(collection.Stages[0].Records[0].Level, Is.EqualTo(2));
            Assert.That(collection.Stages[0].Records[0].SkinId, Is.EqualTo("SKIN_ONE"));
            Assert.That(collection.Result.MigrationCandidate.Staff[0].Level, Is.EqualTo(2));
        }
    }

    [Test]
    public void RawEnvelopeAndStaffFieldFailures_AreNotEmptyOwnershipAndDoNotBlockValidGeneralGameDataSave()
    {
        var cases = new[]
        {
            "empty", "missing-row", "missing-field", "null-field", "wrong-list-type", "broken", "failed-query",
            "paged", "wrong-owner", "multiple-rows"
        };
        foreach (string scenario in cases)
        {
            var fixture = Create();
            JObject row = StageRow(fixture, EStage.Stage1);
            string raw;
            string status = "200";
            var expected = StaffStageRawStatus.VerifiedRawRecords;
            switch (scenario)
            {
                case "missing-row": raw = Rows(); expected = StaffStageRawStatus.MissingRow; break;
                case "missing-field": row.Remove("GiveStaffList"); raw = Rows(row); expected = StaffStageRawStatus.MissingStaffField; break;
                case "null-field": row["GiveStaffList"] = JValue.CreateNull(); raw = Rows(row); expected = StaffStageRawStatus.InvalidStaffRecords; break;
                case "wrong-list-type": row["GiveStaffList"] = S("[]"); raw = Rows(row); expected = StaffStageRawStatus.InvalidStaffRecords; break;
                case "broken": raw = "{broken"; expected = StaffStageRawStatus.InvalidResponse; break;
                case "failed-query": raw = Rows(row); status = "400"; expected = StaffStageRawStatus.QueryFailed; break;
                case "paged": raw = new JObject { ["rows"] = new JArray(row), ["firstKey"] = new JObject() }.ToString(Formatting.None); expected = StaffStageRawStatus.PaginationIncomplete; break;
                case "wrong-owner": row["owner_inDate"] = S("another-account"); raw = Rows(row); expected = StaffStageRawStatus.AccountMismatch; break;
                case "multiple-rows": raw = Rows(row, row); expected = StaffStageRawStatus.AmbiguousRows; break;
                default: raw = Rows(row); break;
            }
            fixture.Manager.LoadAllStageData(true);
            Reply(fixture, EStage.Stage1, Bro(status, raw));
            Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2));
            Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3));
            var collection = fixture.Manager.StageMigrationCollection;
            Assert.That(collection.Stages[0].Status, Is.EqualTo(expected), scenario);
            Assert.That(collection.Stages[0].RawJson, Is.EqualTo(raw));
            Assert.That(collection.CompletedStageCount, Is.EqualTo(3));
            Assert.That(collection.CompletionCount, Is.EqualTo(1));
            if (scenario == "empty")
            {
                Assert.That(collection.Result.MigrationCandidate.Staff, Is.Empty);
                Assert.That(collection.Result.MigrationCandidate.PandaTokens, Is.Zero);
                Assert.That(collection.PlanCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Blocked), scenario);
                Assert.That(collection.Result, Is.Null);
                Assert.That(collection.PlanCount, Is.Zero);
                Assert.That(collection.Stages[0].HasVerifiedRawRecords, Is.False);
            }
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Game.Inserts, Is.Zero);
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
            Assert.That(fixture.Manager.CanSaveLegacyGameData, Is.True, "A migration diagnostic does not invalidate restored general GameData");
            Assert.That(fixture.Manager.RequestGameDataAutosave().Accepted, Is.True);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            fixture.Game.WriteReplies.Single()(Bro("204", ""));
        }
    }

    [Test]
    public void CatalogAndRawStaffValidation_PreserveBadTokensAndNeverRepairOrSilentlyDropRecords()
    {
        foreach (string scenario in new[] { "unavailable", "duplicate-catalog", "unknown-id", "level-zero", "level-too-large",
            "string-level", "malformed-level", "null-id", "absent-skin", "null-skin" })
        {
            var fixture = Create();
            if (scenario == "unavailable") fixture.Game.Catalog = null;
            if (scenario == "duplicate-catalog") fixture.Game.Catalog = new[] { fixture.Game.Catalog[0], fixture.Game.Catalog[0] };
            JObject row = StageRow(fixture, EStage.Stage1, Staff("STAFF01", 2, "UNCHANGED_SKIN"));
            JObject record = (JObject)row["GiveStaffList"]["L"][0]["M"];
            var expected = StaffStageRawStatus.InvalidStaffCatalog;
            if (scenario == "unknown-id") { record["Id"] = S("STAFF_NOT_REGISTERED"); expected = StaffStageRawStatus.UnregisteredStaff; }
            if (scenario == "level-zero") { record["Level"] = N("0"); expected = StaffStageRawStatus.InvalidStaffLevel; }
            if (scenario == "level-too-large") { record["Level"] = N("6"); expected = StaffStageRawStatus.InvalidStaffLevel; }
            if (scenario == "string-level") { record["Level"] = S("2"); expected = StaffStageRawStatus.InvalidStaffRecords; }
            if (scenario == "malformed-level") { record["Level"] = N("oops"); expected = StaffStageRawStatus.InvalidStaffRecords; }
            if (scenario == "null-id") { record["Id"] = JValue.CreateNull(); expected = StaffStageRawStatus.InvalidStaffRecords; }
            if (scenario == "absent-skin") { record.Remove("SkinId"); expected = StaffStageRawStatus.VerifiedRawRecords; }
            if (scenario == "null-skin") { record["SkinId"] = JValue.CreateNull(); expected = StaffStageRawStatus.InvalidStaffRecords; }
            string originalLevel = record["Level"].ToString(Formatting.None);
            string originalId = record["Id"].ToString(Formatting.None);
            fixture.Manager.LoadAllStageData(true);
            Reply(fixture, EStage.Stage1, Bro("200", Rows(row)));
            Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2));
            Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3));
            var collection = fixture.Manager.StageMigrationCollection;
            Assert.That(collection.Stages[0].Status, Is.EqualTo(expected), scenario);
            Assert.That(collection.Stages[0].Records.Count, Is.EqualTo(1));
            Assert.That(collection.Stages[0].Records[0].LevelRawJson, Is.EqualTo(originalLevel));
            Assert.That(collection.Stages[0].Records[0].IdRawJson, Is.EqualTo(originalId));
            var original = collection.Stages[0].Records[0];
            Assert.That(original.SkinIdPresent, Is.EqualTo(scenario != "absent-skin"));
            Assert.That(original.SkinId, Is.EqualTo(scenario == "absent-skin" || scenario == "null-skin" ? null : "UNCHANGED_SKIN"));
            if (scenario == "null-skin") Assert.That(original.SkinIdRawJson, Is.EqualTo("null"));
            if (scenario == "absent-skin")
            {
                Assert.That(original.SkinIdRawJson, Is.Null);
                Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.UnsavedMigrationCandidate));
                Assert.That(collection.PlanCount, Is.EqualTo(1));
                Assert.That(collection.Result.MigrationCandidate.Staff.Single().Id, Is.EqualTo("STAFF01"));
            }
            else
            {
                Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Blocked));
                Assert.That(collection.Result, Is.Null);
                Assert.That(collection.PlanCount, Is.Zero);
            }
            if (scenario == "malformed-level")
            {
                Assert.That(original.Level, Is.Null, "Do not turn the original malformed level into the legacy parser's fallback");
                Assert.That(fixture.Stage.Applied.ContainsKey(EStage.Stage1), Is.True,
                    "The actual legacy parser's fallback path must be demonstrated, not inferred from raw validation");
                Assert.That(fixture.Stage.Applied[EStage.Stage1].GiveStaffList[0].Level, Is.EqualTo(1));
            }
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
        }
    }

    [Test]
    public void RawLevelSixAndValidCrossStageConflicts_RemainDiagnosticsBeforeLegacyA2()
    {
        var legacy = Create();
        legacy.Manager.LoadAllStageData(true);
        var rawSix = StageResponse(legacy, EStage.Stage1, Staff("STAFF02", 6, "LEGACY_SKIN"));
        Reply(legacy, EStage.Stage1, rawSix);
        Reply(legacy, EStage.Stage2, StageResponse(legacy, EStage.Stage2));
        Reply(legacy, EStage.Stage3, StageResponse(legacy, EStage.Stage3));
        var blocked = legacy.Manager.StageMigrationCollection;
        Assert.That(blocked.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Blocked));
        Assert.That(blocked.Result, Is.Null);
        Assert.That(blocked.Stages[0].Status, Is.EqualTo(StaffStageRawStatus.InvalidStaffLevel));
        Assert.That(blocked.Stages[0].Records[0].Level, Is.EqualTo(6));
        Assert.That(blocked.Stages[0].Records[0].LevelRawJson, Is.EqualTo("{\"N\":\"6\"}"));
        Assert.That(blocked.Stages[0].Records[0].SkinId, Is.EqualTo("LEGACY_SKIN"));
        Assert.That(legacy.Stage.Applied[EStage.Stage1].GiveStaffList[0].Level, Is.EqualTo(5), "Only detached legacy data follows existing A2");
        Assert.That(legacy.Stage.Applied[EStage.Stage1].GiveStaffList[0].SkinId, Is.EqualTo("LEGACY_SKIN"));
        Assert.That(legacy.Stage.A2Saves, Is.EqualTo(1), "Existing A2 save callback only; not a migration save");
        Assert.That(legacy.Game.Writes, Is.Zero);
        Assert.That(blocked.PlanCount, Is.Zero);
        Reply(legacy, EStage.Stage1, rawSix);
        Assert.That(legacy.Stage.A2Saves, Is.EqualTo(1));

        var conflict = Create();
        conflict.Manager.LoadAllStageData(true);
        Reply(conflict, EStage.Stage1, StageResponse(conflict, EStage.Stage1, Staff("STAFF01", 2, "SKIN_2")));
        Reply(conflict, EStage.Stage2, StageResponse(conflict, EStage.Stage2, Staff("STAFF01", 4, "SKIN_4")));
        Reply(conflict, EStage.Stage3, StageResponse(conflict, EStage.Stage3));
        var plan = conflict.Manager.StageMigrationCollection;
        Assert.That(plan.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Blocked));
        Assert.That(plan.Result, Is.Null);
        Assert.That(plan.PlanCount, Is.EqualTo(1));
        var issue = plan.MigrationIssues.Single(item => item.Code == StaffAccountMigrationIssueCode.LevelConflict);
        CollectionAssert.AreEqual(new[] { EStage.Stage1, EStage.Stage2 }, issue.Records.Select(record => record.Stage));
        CollectionAssert.AreEqual(new int?[] { 2, 4 }, issue.Records.Select(record => record.Level));
        CollectionAssert.AreEqual(new[] { "SKIN_2", "SKIN_4" }, issue.Records.Select(record => record.SkinId));
        Assert.That(conflict.Game.Writes, Is.Zero);
        Assert.That(conflict.Stage.A2Saves, Is.Zero);
        // These are synthetic memory cases, not evidence of a real account's level conflict.
    }

    [Test]
    public void ExistingCommonStaff_BypassesMigrationPlanningButPreservesLegacyStageLoading()
    {
        var fixture = Create(true);
        var evidence = fixture.Manager.RestoredGameData;
        string original = JsonConvert.SerializeObject(evidence.StaffAccount);
        int catalogCalls = fixture.Game.CatalogCalls;
        fixture.Manager.LoadAllStageData(true);
        foreach (EStage stage in new[] { EStage.Stage3, EStage.Stage1, EStage.Stage2 })
            Reply(fixture, stage, StageResponse(fixture, stage, Staff("STAFF01", 2)));
        var collection = fixture.Manager.StageMigrationCollection;
        Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.NotRequired));
        Assert.That(collection.CompletedStageCount, Is.EqualTo(3));
        Assert.That(collection.PlanCount, Is.Zero);
        Assert.That(collection.Result, Is.Null);
        Assert.That(collection.Stages.All(stage => stage.Status == StaffStageRawStatus.LegacyOnly), Is.True);
        Assert.That(fixture.Stage.Applied.Count, Is.EqualTo(3));
        Assert.That(fixture.Game.CatalogCalls, Is.EqualTo(catalogCalls));
        Assert.That(fixture.Manager.RestoredGameData, Is.SameAs(evidence));
        Assert.That(JsonConvert.SerializeObject(evidence.StaffAccount), Is.EqualTo(original));
        Assert.That(evidence.StaffAccount.PandaTokens, Is.EqualTo(55));
        Assert.That(fixture.Game.Writes, Is.Zero);
    }

    [Test]
    public void NewCollectionRounds_NeverReuseOldStagesOrMixSingleStageReloads()
    {
        var fixture = Create();
        fixture.Manager.LoadAllStageData(true);
        var first = fixture.Manager.StageMigrationCollection;
        var oldGets = fixture.Stage.Gets.ToArray();
        Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 2)));
        fixture.Manager.LoadStageData(EStage.Stage2, true);
        var single = fixture.Manager.StageMigrationCollection;
        Assert.That(single.Round, Is.Not.EqualTo(first.Round));
        Assert.That(first.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Invalidated));
        oldGets[1].Reply(StageResponse(fixture, EStage.Stage2, Staff("STAFF03", 4)));
        oldGets[2].Reply(StageResponse(fixture, EStage.Stage3));
        Assert.That(single.CompletedStageCount, Is.Zero);
        var singleGet = fixture.Stage.Gets.Last();
        singleGet.Reply(StageResponse(fixture, EStage.Stage2, Staff("STAFF03", 4)));
        Assert.That(single.CompletedStageCount, Is.EqualTo(1));
        Assert.That(single.Stages[0].Status, Is.EqualTo(StaffStageRawStatus.Pending));
        Assert.That(single.Result, Is.Null);
        Assert.That(single.PlanCount, Is.Zero);
        fixture.Manager.LoadAllStageData(true);
        var latest = fixture.Manager.StageMigrationCollection;
        singleGet.Reply(StageResponse(fixture, EStage.Stage2, Staff("STAFF01", 4)));
        Assert.That(latest.CompletedStageCount, Is.Zero);
        foreach (var get in fixture.Stage.Gets.Skip(4).ToArray()) get.Reply(StageResponse(fixture, get.Stage));
        Assert.That(latest.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.UnsavedMigrationCandidate));
        Assert.That(latest.Result.MigrationCandidate.Staff, Is.Empty, "No old stage ownership may leak into a new round");
        Assert.That(first.Result, Is.Null);
        Assert.That(single.Result, Is.Null);
        Assert.That(fixture.Game.Writes, Is.Zero);

        // Execute the actual popup retry target without opening a popup or calling an SDK.
        MethodInfo retry = typeof(BackendManager).GetMethod("RetryStageCollection", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(retry, Is.Not.Null);
        var beforeRetry = fixture.Stage.Gets.ToArray();
        retry.Invoke(fixture.Manager, new object[] { latest.Query, latest.Round });
        var retried = fixture.Manager.StageMigrationCollection;
        Assert.That(retried.Round, Is.Not.EqualTo(latest.Round));
        Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(beforeRetry.Length + 3));
        retry.Invoke(fixture.Manager, new object[] { latest.Query, latest.Round });
        Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(beforeRetry.Length + 3), "Duplicate old retry closure must not start another round");
        foreach (var get in beforeRetry) get.Reply?.Invoke(StageResponse(fixture, get.Stage));
        Assert.That(retried.CompletedStageCount, Is.Zero);
        foreach (var get in fixture.Stage.Gets.Skip(beforeRetry.Length).ToArray()) get.Reply(StageResponse(fixture, get.Stage));
        Assert.That(retried.Result.MigrationCandidate.Staff, Is.Empty);
        RestoreGameData(fixture, false);
        retry.Invoke(fixture.Manager, new object[] { retried.Query, retried.Round });
        Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(beforeRetry.Length + 3), "A stale GameData query cannot trigger a delayed popup retry");
    }

    [Test]
    public void GameDataRequeryAndAuthenticationChanges_RejectEveryOldStageCallbackAndCandidate()
    {
        foreach (string transition in new[] { "requery", "same-account", "different-account" })
        {
            var fixture = Create();
            fixture.Manager.LoadAllStageData(true);
            var old = fixture.Manager.StageMigrationCollection;
            var gets = fixture.Stage.Gets.ToArray();
            var oldStage2 = StageResponse(fixture, EStage.Stage2, Staff("STAFF02", 6));
            var oldStage3 = StageResponse(fixture, EStage.Stage3);
            gets[0].Reply(StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 2)));
            int appliedBefore = fixture.Stage.ApplyCalls;
            if (transition != "requery")
            {
                fixture.Manager.LogOut();
                Authenticate(fixture, transition == "different-account" ? Unique("other-account") : fixture.Account);
            }
            RestoreGameData(fixture, false);
            Assert.That(old.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Invalidated));
            gets[1].Reply(oldStage2);
            gets[2].Reply(oldStage3);
            Assert.That(fixture.Stage.ApplyCalls, Is.EqualTo(appliedBefore));
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
            Assert.That(old.Result, Is.Null);
            Assert.That(old.PlanCount, Is.Zero);
            fixture.Manager.LoadAllStageData(true);
            var current = fixture.Manager.StageMigrationCollection;
            Assert.That(current.Query, Is.Not.SameAs(old.Query));
            foreach (var get in fixture.Stage.Gets.Skip(3).ToArray()) get.Reply(StageResponse(fixture, get.Stage));
            Assert.That(current.Result.MigrationCandidate.Staff, Is.Empty);
            Assert.That(current.PlanCount, Is.EqualTo(1));
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void LegacyApplyCheckpoints_BlockStaleParsedDataAndA2SaveWithoutChangingRawEvidence()
    {
        foreach (string checkpoint in new[] { "after-flatten", "before-A2-save", "legacy-rejected" })
        {
            var fixture = Create();
            fixture.Stage.Checkpoint = count =>
            {
                if (checkpoint == "after-flatten" && count == 2) fixture.Manager.InvalidateGameDataRestore();
            };
            fixture.Stage.MemoryApply = data =>
            {
                if (checkpoint == "before-A2-save") fixture.Manager.InvalidateGameDataRestore();
                return checkpoint != "legacy-rejected";
            };
            fixture.Manager.LoadAllStageData(true);
            var collection = fixture.Manager.StageMigrationCollection;
            var response = StageResponse(fixture, EStage.Stage1, Staff("STAFF02", 6, "ORIGINAL_SKIN"));
            Reply(fixture, EStage.Stage1, response);
            Assert.That(collection.Stages[0].Records[0].Level, Is.EqualTo(6));
            Assert.That(collection.Stages[0].Records[0].SkinId, Is.EqualTo("ORIGINAL_SKIN"));
            Assert.That(collection.Result, Is.Null);
            Assert.That(fixture.Stage.A2Saves, Is.Zero, checkpoint);
            Assert.That(fixture.Stage.MemoryCalls, Is.EqualTo(checkpoint == "after-flatten" ? 0 : 1));
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Game.Inserts, Is.Zero);
            if (checkpoint != "legacy-rejected")
                Assert.That(collection.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.Invalidated));
        }
    }

    [Test]
    public void LegacyApiBoundary_RechecksCurrentSessionBeforeSendAndBeforeResponseFollowUp()
    {
        var fixture = Create();
        fixture.Manager.LoadAllStageData(true);
        var collection = fixture.Manager.StageMigrationCollection;
        var method = typeof(BackendManager).GetMethod("ProcessBackendAPI", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        int sends = 0, success = 0, failures = 0;
        Action<BackendReturnObject> response = null;
        Action<Action<BackendReturnObject>> fakeSdk = callback => { sends++; response = callback; };
        Action<BackendReturnObject> followed = _ => success++;
        Action<BackendState> failed = _ => failures++;
        Func<bool> current = () => collection.RefreshValidity();
        method.Invoke(fixture.Manager, new object[] { "Stage test read", fakeSdk, followed, failed, 3, false, current });
        Assert.That(sends, Is.EqualTo(1));
        Assert.That(success, Is.Zero);
        fixture.Manager.InvalidateGameDataRestore();
        response(Bro("200", Rows()));
        response(Bro("400", ""));
        Assert.That(success, Is.Zero, "Old success must not start downstream Stage/A2 save work");
        Assert.That(failures, Is.Zero, "Old error must not reach HandleError, retry or failure popup handling");
        Assert.That(sends, Is.EqualTo(1));
        method.Invoke(fixture.Manager, new object[] { "Stage delayed retry", fakeSdk, followed, failed, 3, false, current });
        Assert.That(sends, Is.EqualTo(1));

        // Even if the first entry check passes, the send boundary must check again.
        int checks = 0;
        Func<bool> invalidatedBeforeSend = () => ++checks == 1;
        method.Invoke(fixture.Manager, new object[] { "Stage pre-send", fakeSdk, followed, failed, 3, false, invalidatedBeforeSend });
        Assert.That(checks, Is.EqualTo(2));
        Assert.That(sends, Is.EqualTo(1));
        Assert.That(fixture.Game.Writes, Is.Zero);
        Assert.That(fixture.Stage.A2Saves, Is.Zero);
    }

    private Fixture Create(bool common = false)
    {
        var game = new FakeGame { Catalog = Catalog() };
        var stage = new FakeStage();
        var manager = (BackendManager)FormatterServices.GetUninitializedObject(typeof(BackendManager));
        Field(typeof(BackendManager), "_gameDataTransport").SetValue(manager, game);
        Field(typeof(BackendManager), "_stageDataTransport").SetValue(manager, stage);
        Field(typeof(BackendManager), "_gameDataInitializationPolicy").SetValue(manager, Policy());
        var fixture = new Fixture { Manager = manager, Game = game, Stage = stage, Row = Unique("game-row") };
        Authenticate(fixture, Unique("stage-test-account"));
        RestoreGameData(fixture, common);
        return fixture;
    }
    private void Authenticate(Fixture fixture, string account)
    {
        fixture.Game.LoggedIn = fixture.Game.NativeLoggedIn = false;
        var attempt = fixture.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
        fixture.Game.LoggedIn = fixture.Game.NativeLoggedIn = true;
        fixture.Account = fixture.Game.AccountInDate = account;
        Assert.That(fixture.Manager.NotifyFederationLoginSuccess(attempt, Bro("200", "")), Is.True);
    }
    private void RestoreGameData(Fixture fixture, bool common)
    {
        int restored = 0;
        fixture.Manager.GetAndRestoreGameDataAsync((_, result) => { if (result.CanContinueLegacy) restored++; });
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row), ["Dia"] = N("110") };
        if (common) row["StaffAccount"] = S("{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
        fixture.Game.GetReplies.Last()(Bro("200", Rows(row)));
        Assert.That(restored, Is.EqualTo(1));
    }
    private IReadOnlyList<StaffData> Catalog()
    {
        var catalog = Resources.LoadAll<StaffData>("StaffData");
        foreach (string id in new[] { "STAFF01", "STAFF02", "STAFF03" }) Assert.That(catalog.Count(data => data != null && data.Id == id), Is.EqualTo(1));
        foreach (var data in catalog) if (data != null && !_assets.ContainsKey(data)) _assets.Add(data, EditorJsonUtility.ToJson(data));
        return catalog;
    }
    private BackendReturnObject StageResponse(Fixture fixture, EStage stage, params SaveStaffData[] staff) => Bro("200", Rows(StageRow(fixture, stage, staff)));
    private static JObject StageRow(Fixture fixture, EStage stage, params SaveStaffData[] staff)
    {
        var original = new ServerStageData { GiveStaffList = staff.ToList() };
        original.EquipStaffDataDic.Add("Floor1", new Dictionary<string, string> { ["Waiter"] = "STAFF01" });
        JObject plain = JObject.Parse(original.GetParam().GetJson());
        var result = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row + "-" + stage) };
        foreach (var field in plain.Properties()) result[field.Name] = Attribute(field.Value);
        return result;
    }
    private static JToken Attribute(JToken value)
    {
        if (value is JObject obj) return new JObject { ["M"] = new JObject(obj.Properties().Select(field => new JProperty(field.Name, Attribute(field.Value)))) };
        if (value is JArray list) return new JObject { ["L"] = new JArray(list.Select(Attribute)) };
        if (value.Type == JTokenType.String) return S((string)value);
        if (value.Type == JTokenType.Boolean) return new JObject { ["BOOL"] = (bool)value };
        if (value.Type == JTokenType.Null) return new JObject { ["NULL"] = true };
        return N(value.ToString(Formatting.None));
    }
    private static SaveStaffData Staff(string id, int level, string skin = "") { var staff = new SaveStaffData(id, level); staff.SetSkinId(skin); return staff; }
    private static JObject S(string value) => new JObject { ["S"] = value };
    private static JObject N(string value) => new JObject { ["N"] = value };
    private static string Rows(params JObject[] rows) => new JObject
    { ["rows"] = new JArray(rows.Select(row => row.DeepClone())), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
    private static void Reply(Fixture fixture, EStage stage, BackendReturnObject response) => fixture.Stage.Gets.Last(get => get.Stage == stage).Reply(response);
    private BackendReturnObject Bro(string status, string raw)
    {
        var bro = new BackendReturnObject();
        foreach (var pair in new Dictionary<string, string> { ["StatusCode"] = status, ["ReturnValue"] = raw, ["ErrorCode"] = "", ["Message"] = "" })
        {
            var property = typeof(BackendReturnObject).GetProperty(pair.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            property.SetValue(bro, Convert.ChangeType(pair.Value, property.PropertyType, CultureInfo.InvariantCulture));
        }
        _responses.Add(bro, Describe(bro));
        return bro;
    }
    private static string Describe(BackendReturnObject bro) => JsonConvert.SerializeObject(new
    { Raw = bro.GetReturnValue(), Status = bro.GetStatusCode(), Error = bro.GetErrorCode(), Message = bro.GetMessage() });
    private static string Unique(string prefix) => prefix + ":" + Guid.NewGuid().ToString("N");
    private static FieldInfo Field(Type type, string name, bool isStatic = false) => type.GetField(name,
        BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
    private static HashSet<GameDataSaveCoordinator> Owners() => (HashSet<GameDataSaveCoordinator>)Field(typeof(GameDataSaveCoordinator), "BlockedCoordinators", true).GetValue(null);
    private static GameDataSdkInitializationPolicy Policy()
    {
        bool initialized = false;
        var settings = new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true);
        Func<GameDataSdkRetrySettings> read = () => settings;
        Func<bool> isInitialized = () => initialized;
        Func<GameDataRawResponse> initialize = () => { initialized = true; return new GameDataRawResponse(true, "200", "", ""); };
        var args = new object[] { read, isInitialized, initialize, null };
        typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return (GameDataSdkInitializationPolicy)args[3];
    }
    private sealed class Fixture { public BackendManager Manager; public FakeGame Game; public FakeStage Stage; public string Account, Row; }
    private sealed class FakeGame : IGameDataBackendTransport
    {
        public bool LoggedIn { get; set; }
        public bool NativeLoggedIn { get; set; }
        public string AccountInDate { get; set; }
        public bool GameplaySaveAllowed => true;
        public IReadOnlyList<StaffData> Catalog;
        public int CatalogCalls, Writes, Inserts;
        public readonly List<Action<BackendReturnObject>> GetReplies = new List<Action<BackendReturnObject>>();
        public readonly List<Action<BackendReturnObject>> WriteReplies = new List<Action<BackendReturnObject>>();
        public void Get(string account, Action<BackendReturnObject> callback) => GetReplies.Add(callback);
        public void Insert(Param values, Action<BackendReturnObject> callback) { Inserts++; Assert.Fail("Stage collection must not insert GameData"); }
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback) { Writes++; WriteReplies.Add(callback); }
        public Param LatestValues() { var values = new Param(); values.Add("Dia", 110); return values; }
        public IReadOnlyList<StaffData> ReadCatalog() { CatalogCalls++; return Catalog; }
        public bool Restore(BackendReturnObject response) => true;
        public Param InitialValues() => throw new InvalidOperationException("No initial account creation in Stage collection tests");
    }
    private sealed class FakeStage : IStageDataLoadTransport
    {
        public readonly List<GetCall> Gets = new List<GetCall>();
        public readonly Dictionary<EStage, ServerStageData> Applied = new Dictionary<EStage, ServerStageData>();
        public Func<EStage, BackendReturnObject> SyncResponse;
        public Func<ServerStageData, bool> MemoryApply;
        public Action<int> Checkpoint;
        public int ApplyCalls, MemoryCalls, A2Saves;
        public void Get(EStage stage, string account, Func<bool> isCurrent, Action<BackendReturnObject> callback) =>
            Gets.Add(new GetCall { Stage = stage, Account = account, IsCurrent = isCurrent, Reply = callback });
        public BackendReturnObject Get(EStage stage, string account, Func<bool> isCurrent)
        {
            Gets.Add(new GetCall { Stage = stage, Account = account, IsCurrent = isCurrent });
            return isCurrent() ? SyncResponse(stage) : null;
        }
        public void Apply(EStage stage, BackendReturnObject response, Func<bool> isCurrent, bool asynchronous)
        {
            ApplyCalls++;
            int checks = 0;
            UserInfo.TryApplyStageDataResponse(stage, response, () =>
            {
                Checkpoint?.Invoke(++checks);
                return isCurrent();
            }, data =>
            {
                MemoryCalls++;
                Applied[stage] = data;
                return MemoryApply == null || MemoryApply(data);
            }, () => A2Saves++);
        }
        public sealed class GetCall { public EStage Stage; public string Account; public Func<bool> IsCurrent; public Action<BackendReturnObject> Reply; }
    }
    private static string GameState()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var stages = (StageInfo[])typeof(UserInfo).GetField("_stageInfos", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var stageState = stages?.Select(stage => stage == null ? null : new
        {
            Owned = typeof(StageInfo).GetField("_giveStaffDic", flags).GetValue(stage),
            Equipped = ((Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>)
                typeof(StageInfo).GetField("_equipStaffTypeDic", flags).GetValue(stage))
                .Select(floor => new { Floor = floor.Key, Staff = floor.Value.Select(slot => new
                { Slot = slot.Key, Id = slot.Value == null ? null : slot.Value.Id }).ToArray() }).ToArray()
        }).ToArray();
        return JsonConvert.SerializeObject(new
        {
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount, UserInfo.CurrentStage,
            UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas
        });
    }
}
#endif
