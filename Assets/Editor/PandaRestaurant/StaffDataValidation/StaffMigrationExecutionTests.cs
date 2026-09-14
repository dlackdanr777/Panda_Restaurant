#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// Actual automatic Backend/Stage/runtime/coordinator calls; only the SDK and wallet boundary are detached memory fakes.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void MigrationExecution_AutomaticCompletionWritesOnlyStaffAndInstallsOnceBeforeLatestAutosave()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet, common: false);
            var originalRestore = fixture.Manager.GameDataRestoreResult;
            fixture.Game.LatestFactory = () =>
            {
                var values = new Param(); values.Add("Dia", wallet.DiamondValue); values.Add("Money", wallet.GoldValue);
                values.Add("UserId", "current-memory-name"); return values;
            };
            int completions = 0;
            bool callbackSawInstalled = false, callbackBeforeAutosave = false;
            fixture.Manager.StaffMigrationCompleted += completed =>
            {
                completions++;
                callbackSawInstalled = completed.IsCompleted && fixture.Manager.StaffRuntime.Mode == StaffAccountRuntimeMode.Common;
                callbackBeforeAutosave = fixture.Game.Writes == 1 && fixture.Game.LatestCalls == 0;
            };
            fixture.Manager.LoadAllStageData(true);
            Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3, Staff("STAFF23", 1, "THIRD_SKIN")));
            Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2, Staff("STAFF01", 2, "SECOND_SKIN"), Staff("STAFF03", 4)));
            Assert.That(fixture.Game.Writes, Is.Zero);
            Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 2, "FIRST_SKIN")));
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            Assert.That(execution, Is.Not.Null);
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Sending));
            Assert.That(execution.IsCompleted, Is.False); Assert.That(execution.CompletionCount, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            AssertMigrationPayload(fixture, 0, execution);
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(wallet.GoldValue, Is.EqualTo(1000000)); Assert.That(wallet.DiamondValue, Is.EqualTo(110));
            var collection = fixture.Manager.StageMigrationCollection;
            var candidate = collection.Result.MigrationCandidate;
            string raw = string.Join("\n", collection.Stages.Select(stage => stage.RawJson));
            string sources = MigrationStageValues(fixture);
            for (int repeat = 0; repeat < 3; repeat++) fixture.Manager.TryStartStaffMigration();
            Assert.That(fixture.Manager.CurrentStaffMigrationExecution, Is.SameAs(execution));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            var queued = fixture.Manager.RequestGameDataAutosave();
            Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(fixture.Game.LatestCalls, Is.Zero);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Completed));
            Assert.That(execution.CompletionCount, Is.EqualTo(1)); Assert.That(completions, Is.EqualTo(1));
            Assert.That(callbackSawInstalled, Is.True); Assert.That(callbackBeforeAutosave, Is.True);
            Assert.That(fixture.Manager.IsStaffMigrationProtected, Is.False);
            AssertAccountLevels(fixture, "STAFF01", 2); AssertAccountLevels(fixture, "STAFF03", 4); AssertAccountLevels(fixture, "STAFF23", 1);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
            Assert.That(MigrationStageValues(fixture), Is.EqualTo(sources), "Installation must not rewrite Stage skins, placements or saved staff records");
            Assert.That(string.Join("\n", collection.Stages.Select(stage => stage.RawJson)), Is.EqualTo(raw));
            Assert.That(collection.Result.MigrationCandidate, Is.SameAs(candidate));
            Assert.That(collection.Result.Status, Is.EqualTo(StaffAccountLoadStatus.UnsavedMigrationCandidate));
            Assert.That(fixture.Manager.GameDataRestoreResult, Is.SameAs(originalRestore));
            Assert.That(originalRestore.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(fixture.Manager.RestoredGameData, Is.Null, "Write success is separate provenance, not a fabricated read response");
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            AssertMigrationAccountJson((string)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["StaffAccount"], 2);

            Assert.That(fixture.Stage.Runtime[EStage.Stage2].UpgradeStaff(scope.Staff("STAFF01")), Is.True);
            Assert.That(wallet.CostCommits, Is.EqualTo(1)); Assert.That(wallet.Notifications, Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.LastNotificationError, Is.Null);
            wallet.DiamondValue = 150; // Unrelated current memory change, not a migration reward.
            var afterGrowth = fixture.Manager.RequestGameDataAutosave();
            fixture.Manager.StaffRuntime.Refresh();
            fixture.Game.WriteReplies[0](Bro("204", ""));
            AssertAccountLevels(fixture, "STAFF01", 3);
            Assert.That(completions, Is.EqualTo(1)); Assert.That(execution.CompletionCount, Is.EqualTo(1));
            Assert.That(candidate.Staff.Single(staff => staff.Id == "STAFF01").Level, Is.EqualTo(2));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(afterGrowth.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That(fixture.Game.Writes, Is.EqualTo(3));
            JObject latest = JObject.Parse(fixture.Game.WriteValues[2].GetJson());
            Assert.That((int)latest["Dia"], Is.EqualTo(150)); Assert.That((long)latest["Money"], Is.EqualTo(wallet.GoldValue));
            Assert.That((string)latest["UserId"], Is.EqualTo("current-memory-name"));
            AssertMigrationAccountJson((string)latest["StaffAccount"], 3);
            fixture.Game.WriteReplies[2](Bro("204", ""));
            Assert.That(afterGrowth.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
            Assert.That(fixture.Game.Inserts, Is.Zero);
        }
    }

    [Test]
    public void MigrationExecution_PrecedingSaveAndMailReservationKeepOneIntentAndRevalidateBeforeItsTurn()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string mode in new[] { "save", "mail", "save-changed", "mail-changed" })
        {
            var fixture = CreateAccountRuntimeFixture(out _, common: false);
            GameDataMailSaveLease mail = null;
            bool useMail = mode.StartsWith("mail", StringComparison.Ordinal);
            if (useMail)
            {
                Assert.That(fixture.Manager.TryAcquireMailSaveLease(out mail, out var error), Is.True, error);
                Assert.That(mail.TryMarkRequestStarted(), Is.True);
            }
            else fixture.Manager.RequestGameDataAutosave();
            int precedingWrites = fixture.Game.Writes;
            fixture.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(fixture);
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Waiting), mode);
            Assert.That(fixture.Manager.IsStaffMigrationProtected, Is.False, "A waiting intent must not steal a preceding owner's protection");
            Assert.That(fixture.Game.Writes, Is.EqualTo(precedingWrites));
            fixture.Manager.TryStartStaffMigration(); fixture.Manager.TryStartStaffMigration();
            Assert.That(fixture.Manager.CurrentStaffMigrationExecution, Is.SameAs(execution));
            var after = fixture.Manager.RequestGameDataAutosave();
            Assert.That(after.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            bool changed = mode.EndsWith("changed", StringComparison.Ordinal);
            if (changed) Assert.That(fixture.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02")), Is.True);
            if (useMail)
            {
                Assert.That(mail.IsCurrent, Is.True);
                Assert.That(mail.TryCompleteKnownOutcome(), Is.True);
            }
            else fixture.Game.WriteReplies[0](Bro("204", ""));
            if (changed)
            {
                Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.RejectedBeforeSend), mode);
                Assert.That(execution.CompletionCount, Is.Zero);
                Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].IsGiveStaff("STAFF02"), Is.True, "A failed validation must not undo the actual earlier grant");
                Assert.That(fixture.Game.Writes, Is.EqualTo(precedingWrites + 1), "The later ordinary save must continue without a migration transmission");
                Assert.That(JObject.Parse(fixture.Game.WriteValues.Last().GetJson())["StaffAccount"], Is.Null);
                Assert.That(after.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            }
            else
            {
                Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Sending), mode);
                Assert.That(fixture.Game.Writes, Is.EqualTo(precedingWrites + 1));
                AssertMigrationPayload(fixture, precedingWrites, execution);
                Assert.That(after.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
                Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False, "Mail cannot acquire the same target while migration owns it");
                fixture.Game.WriteReplies[precedingWrites](Bro("204", ""));
                Assert.That(execution.CompletionCount, Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(precedingWrites + 2));
                Assert.That(after.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
                AssertMigrationAccountJson((string)JObject.Parse(fixture.Game.WriteValues.Last().GetJson())["StaffAccount"], 2);
            }
        }
    }

    [Test]
    public void MigrationExecution_ProtectedManagedMutatorsAndExportedCopiesCannotAlterTheCandidate()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out var wallet, common: false);
            fixture.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(fixture);
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            Assert.That(fixture.Manager.IsStaffMigrationProtected, Is.True);
            Assert.That(fixture.Manager.StaffRuntime.CanMutate, Is.False);
            var stage = fixture.Stage.Runtime[EStage.Stage1];
            var before = stage.CaptureStaffRuntimeSnapshot();
            string source = MigrationStageValues(fixture);
            int notifications = 0;
            stage.OnGiveStaffHandler += () => notifications++;
            stage.OnUpgradeStaffHandler += () => notifications++;
            stage.OnChangeStaffSkinHandler += () => notifications++;
            Assert.That(stage.GiveStaff(scope.Staff("STAFF02")), Is.False);
            Assert.That(stage.UpgradeStaff(scope.Staff("STAFF01")), Is.False);
            Assert.That(stage.TrySetStaffSkin(scope.Staff("STAFF01"), (StaffSkinData)null), Is.False);
            Assert.That(stage.LoadData(stage.SaveData()), Is.False);
            Assert.That(wallet.CostCommits, Is.Zero); Assert.That(wallet.Notifications, Is.Zero); Assert.That(notifications, Is.Zero);
            Assert.That(before.HasSameState(stage.CaptureStaffRuntimeSnapshot()), Is.True);
            var exported = stage.SaveData().GiveStaffList.Single();
            exported.LevelUp(); exported.SetSkinId("EXPORTED_COPY_ONLY");
            // The original parsed input is another potential alias; actual LoadData must have detached it too.
            fixture.Stage.Applied[EStage.Stage1].GiveStaffList.Single().LevelUp();
            fixture.Stage.Applied[EStage.Stage1].GiveStaffList.Single().SetSkinId("INPUT_COPY_ONLY");
            Assert.That(MigrationStageValues(fixture), Is.EqualTo(source));
            Assert.That(before.HasSameState(stage.CaptureStaffRuntimeSnapshot()), Is.True);
            Assert.That(execution.Preparation.Candidate.Staff.Single(staff => staff.Id == "STAFF01").Level, Is.EqualTo(2));
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            fixture.Game.WriteReplies.Single()(Bro("204", ""));
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Completed));
            AssertAccountLevels(fixture, "STAFF01", 2);
            Assert.That(stage.GiveStaff(scope.Staff("STAFF02")), Is.True, "Only this migration's mutation fence is released after installation");
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
        }
    }

    [Test]
    public void MigrationExecution_InvalidRawOrIncompleteApplicationsNeverTransmitAndDoNotBlockOrdinaryLegacySave()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string mode in new[] { "level-six", "conflict", "pending", "apply-false", "source-mismatch", "catalog",
            "before-protection", "under-protection", "before-transport" })
        {
            var fixture = CreateAccountRuntimeFixture(out _, common: false);
            if (mode == "apply-false") fixture.Stage.RuntimeApply = (stage, data) => stage != EStage.Stage3 && fixture.Stage.Runtime[stage].LoadData(data);
            if (mode == "source-mismatch") fixture.Stage.RuntimeApply = (stage, data) =>
            { if (stage == EStage.Stage2) data.GiveStaffList[0].LevelUp(); return fixture.Stage.Runtime[stage].LoadData(data); };
            fixture.Manager.LoadAllStageData(true);
            int changedAtBoundary = 0;
            bool? publicGrantDuringProtection = null;
            if (mode == "before-protection" || mode == "under-protection" || mode == "before-transport")
                fixture.Game.CatalogReader = () =>
                {
                    var operation = fixture.Manager.CurrentStaffMigrationExecution;
                    bool protectedNow = fixture.Manager.IsStaffMigrationProtected;
                    var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
                    bool atBoundary = operation != null && (mode == "before-protection" ? !protectedNow
                        : protectedNow && coordinator.State == (mode == "under-protection"
                            ? GameDataSaveCoordinatorState.Preparing : GameDataSaveCoordinatorState.Sending));
                    if (changedAtBoundary == 0 && atBoundary)
                    {
                        changedAtBoundary++;
                        if (mode == "before-protection") fixture.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02"));
                        else
                        {
                            publicGrantDuringProtection = fixture.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02"));
                            var sourceRecords = (Dictionary<string, SaveStaffData>)Field(typeof(StageInfo), "_giveStaffDic")
                                .GetValue(fixture.Stage.Runtime[EStage.Stage1]);
                            sourceRecords["STAFF01"].LevelUp(); // Model an unsupported alias/reflection mutation beyond the normal guarded API.
                        }
                    }
                    return fixture.Game.Catalog;
                };
            if (mode == "catalog") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, mode == "level-six" ? Staff("STAFF02", 6) : Staff("STAFF01", 2)));
            Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2, mode == "conflict" ? Staff("STAFF01", 4) : Staff("STAFF03", 4)));
            if (mode != "pending") Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3, Staff("STAFF23", 1)));
            fixture.Manager.TryStartStaffMigration(); fixture.Manager.TryStartStaffMigration();
            Assert.That(fixture.Game.Writes, Is.Zero, mode);
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(fixture.Manager.IsStaffMigrationProtected, Is.False);
            Assert.That(fixture.Manager.CanSaveLegacyGameData, Is.True);
            if (mode == "before-protection" || mode == "under-protection" || mode == "before-transport")
            {
                Assert.That(changedAtBoundary, Is.EqualTo(1), mode);
                if (mode != "before-protection") Assert.That(publicGrantDuringProtection, Is.False, mode);
                Assert.That(fixture.Manager.CurrentStaffMigrationExecution.Status, Is.EqualTo(StaffMigrationExecutionStatus.RejectedBeforeSend));
                Assert.That(fixture.Manager.CurrentStaffMigrationExecution.Request.Receipt.SendStarted, Is.False);
            }
            if (mode == "level-six")
            {
                Assert.That(fixture.Manager.StageMigrationCollection.Stages[0].Records.Single().Level, Is.EqualTo(6));
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].GetStaffLevel("STAFF02"), Is.EqualTo(5));
                Assert.That(fixture.Stage.A2Saves, Is.EqualTo(1), "The existing A2 save callback is not a migration request");
            }
            var ordinary = fixture.Manager.RequestGameDataAutosave();
            Assert.That(ordinary.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending), mode);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(JObject.Parse(fixture.Game.WriteValues.Single().GetJson())["StaffAccount"], Is.Null);
        }
    }

    [Test]
    public void MigrationExecution_ChangedGenerationOrRoundPreservesOldSendEvidenceAndCannotInstallElsewhere()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string change in new[] { "query", "same-account", "different-account", "round" })
        {
            var fixture = CreateAccountRuntimeFixture(out _, common: false);
            fixture.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(fixture);
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            string frozen = execution.StaffAccountJson;
            var originalCoordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
            if (change == "round") fixture.Manager.LoadAllStageData(true);
            else
            {
                if (change != "query")
                {
                    fixture.Manager.LogOut();
                    Authenticate(fixture, change == "same-account" ? fixture.Account : Unique("migration-next-account"));
                }
                RestoreGameData(fixture, false);
            }
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(execution.IsCompleted, Is.False); Assert.That(execution.CompletionCount, Is.Zero);
            Assert.That(execution.Status == StaffMigrationExecutionStatus.InvalidatedAfterSend || execution.Status == StaffMigrationExecutionStatus.LocalCompletionFailed, Is.True, change);
            Assert.That(execution.StaffAccountJson, Is.EqualTo(frozen));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.Null, change);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(originalCoordinator.LastReceipt, Is.Not.Null);
            Assert.That(originalCoordinator.LastReceipt.RawResponse.IsSuccess, Is.True, "Retain the actual late server response without treating it as current installation authority");
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(execution.Identity.Target), Is.True);
            if (change != "different-account")
            {
                var ordinary = fixture.Manager.RequestGameDataAutosave();
                Assert.That(ordinary.Accepted, Is.False, "Requery or same-account login must not create another writer around an unresolved old send");
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void MigrationExecution_UnknownResponsesKeepTheRequestAndNeverAutomaticallyRetryOrUnlock()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string failure in new[] { "null", "error", "throw", "missing" })
        {
            var fixture = CreateAccountRuntimeFixture(out _, common: false);
            if (failure == "throw") fixture.Game.OnUpdate = (_, __, ___) => throw new InvalidOperationException("Detached transport threw after send started");
            fixture.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(fixture);
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
            if (failure == "null") fixture.Game.WriteReplies.Single()(null);
            if (failure == "error") fixture.Game.WriteReplies.Single()(Bro("400", "{\"error\":\"unverified\"}"));
            if (failure == "missing") Assert.That(coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Detached missing migration response"), Is.True);
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Indeterminate), failure);
            Assert.That(execution.CompletionCount, Is.Zero); Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            Assert.That(fixture.Manager.StaffRuntime.CanMutate, Is.False);
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            fixture.Manager.TryStartStaffMigration(); fixture.Manager.TryStartStaffMigration();
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(execution.Identity.Target), Is.True);
            if (failure == "missing")
            {
                fixture.Game.WriteReplies.Single()(Bro("204", ""));
                Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Completed));
                Assert.That(execution.CompletionCount, Is.EqualTo(1));
                AssertAccountLevels(fixture, "STAFF23", 1);
                fixture.Game.WriteReplies.Single()(Bro("204", ""));
                Assert.That(execution.CompletionCount, Is.EqualTo(1)); Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void MigrationExecution_UnexpectedInternalSourceOrCatalogChangeAfterSendRetainsConfirmedButUninstalledOutcome()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string mismatch in new[] { "raw-reference", "stage-owner", "catalog" })
        {
            var fixture = CreateAccountRuntimeFixture(out _, common: false);
            fixture.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(fixture);
            var execution = fixture.Manager.CurrentStaffMigrationExecution;
            string preparation = DescribePreparation(execution.Preparation);
            if (mismatch == "raw-reference")
            {
                var privateOwned = (Dictionary<string, SaveStaffData>)Field(typeof(StageInfo), "_giveStaffDic")
                    .GetValue(fixture.Stage.Runtime[EStage.Stage1]);
                privateOwned["STAFF01"].LevelUp(); // Deliberate unsupported corruption, not a public migration mutation path.
            }
            if (mismatch == "stage-owner") fixture.Stage.Runtime[EStage.Stage1] = new StageInfo();
            if (mismatch == "catalog") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            var waiting = fixture.Manager.RequestGameDataAutosave();
            fixture.Game.WriteReplies.Single()(Bro("204", ""));
            Assert.That(execution.Status, Is.EqualTo(StaffMigrationExecutionStatus.LocalCompletionFailed), mismatch);
            Assert.That(execution.IsCompleted, Is.False); Assert.That(execution.CompletionCount, Is.Zero);
            Assert.That(execution.Request.Receipt.RawResponse.IsSuccess, Is.True);
            Assert.That(execution.Request.Status, Is.EqualTo(GameDataSaveRequestStatus.LocalCompletionFailed));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(DescribePreparation(execution.Preparation), Is.EqualTo(preparation));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1)); Assert.That(fixture.Game.LatestCalls, Is.Zero);
            Assert.That(waiting.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(execution.Identity.Target), Is.True);
            if (mismatch == "raw-reference") Assert.That(fixture.Stage.Runtime[EStage.Stage1].GetStaffLevel("STAFF01"), Is.EqualTo(3), "Do not overwrite or silently repair unexpected memory changes");
            fixture.Game.WriteReplies.Single()(Bro("204", ""));
            fixture.Manager.TryStartStaffMigration();
            Assert.That(fixture.Game.Writes, Is.EqualTo(1)); Assert.That(execution.CompletionCount, Is.Zero);
        }
    }

    [Test]
    public void MigrationExecution_ExistingCommonAndOrdinaryLegacyWritesCannotUseTheNarrowMigrationPermission()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var common = CreateAccountRuntimeFixture(out _);
            common.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(common);
            Assert.That(common.Manager.TryStartStaffMigration(), Is.False);
            Assert.That(common.Game.Writes, Is.Zero);
            Assert.That(common.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            var legacy = CreateAccountRuntimeFixture(out _, common: false);
            var values = new Param(); values.Add("StaffAccount", CommonAccountJson());
            string original = values.GetJson();
            var rejected = legacy.Manager.RequestGameDataSave(values);
            Assert.That(rejected.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
            Assert.That(legacy.Game.Writes, Is.Zero); Assert.That(values.GetJson(), Is.EqualTo(original));
            Assert.That(legacy.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            legacy.Manager.LoadAllStageData(true); ReplyMigrationRuntimeRows(legacy);
            var migration = legacy.Manager.CurrentStaffMigrationExecution;
            Assert.That(legacy.Game.Writes, Is.EqualTo(1));
            var queuedForgery = legacy.Manager.RequestGameDataSave(values);
            legacy.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(migration.Status, Is.EqualTo(StaffMigrationExecutionStatus.Completed));
            Assert.That(queuedForgery.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
            Assert.That(legacy.Game.Writes, Is.EqualTo(1), "The migration permit never authorizes an unrelated caller's different StaffAccount JSON");
            Assert.That(legacy.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
        }
    }

    private void ReplyMigrationRuntimeRows(Fixture fixture)
    {
        Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, Staff("STAFF01", 2, "FIRST_SKIN")));
        Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2, Staff("STAFF01", 2, "SECOND_SKIN"), Staff("STAFF03", 4)));
        Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3, Staff("STAFF23", 1, "THIRD_SKIN")));
    }
    private static string MigrationStageValues(Fixture fixture) => JsonConvert.SerializeObject(fixture.Stage.Runtime
        .OrderBy(pair => pair.Key).Select(pair => new { Stage = pair.Key, Values = pair.Value.SaveData().GetParam().GetJson() }));
    private static void AssertMigrationPayload(Fixture fixture, int index, StaffMigrationExecution execution)
    {
        JObject fields = JObject.Parse(fixture.Game.WriteValues[index].GetJson());
        CollectionAssert.AreEqual(new[] { "StaffAccount" }, fields.Properties().Select(field => field.Name));
        Assert.That((string)fields["StaffAccount"], Is.EqualTo(execution.StaffAccountJson));
        Assert.That(fixture.Game.WriteTargets[index].AccountInDate, Is.EqualTo(fixture.Account));
        Assert.That(fixture.Game.WriteTargets[index].RowInDate, Is.EqualTo(fixture.Row));
        AssertMigrationAccountJson((string)fields["StaffAccount"], 2);
    }
    private static void AssertMigrationAccountJson(string json, int staff01Level)
    {
        var read = StaffAccountSaveConverter.Read(json);
        Assert.That(read.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03", "STAFF23" }, read.Data.Staff.Select(staff => staff.Id));
        CollectionAssert.AreEqual(new[] { staff01Level, 4, 1 }, read.Data.Staff.Select(staff => staff.Level));
        Assert.That(read.Data.Version, Is.EqualTo(1)); Assert.That(read.Data.PandaTokens, Is.Zero);
    }
}
#endif
