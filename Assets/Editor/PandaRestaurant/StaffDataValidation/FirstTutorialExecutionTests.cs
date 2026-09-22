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

// Extends the existing detached-runtime fixture and its account/resource/RNG immutability checks.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void FirstTutorial_NewCommonRewardAndPlacementConfirmBeforeOnePresenterCanStart()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            CompleteTutorialStageReads(fixture);
            var original = fixture.Manager.StaffRuntime.Snapshot;
            Assert.That(fixture.Game.Writes, Is.Zero, "Ordinary restoration is not a tutorial intent");

            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out var status, out var error), Is.True, error);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Waiting));
            Assert.That(operation.IsPrepared, Is.False);
            Assert.That(operation.TryClaimPresentation(new object()), Is.False);
            Assert.That(state.Money, Is.Zero);
            Assert.That(state.Granted, Is.False);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(original));
            Assert.That(stageWrites.Updates.Count, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            var payload = JObject.Parse(fixture.Game.WriteValues[0].GetJson());
            Assert.That((long)payload["Money"], Is.EqualTo(5000));
            Assert.That((bool)payload["FirstTutorialStartRewardGranted"], Is.True);
            Assert.That(payload["IsFirstTutorialClear"], Is.Null);
            Assert.That(JObject.Parse((string)payload["StaffAccount"])["Staff"].Count(), Is.EqualTo(1));

            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var repeated, out _, out _), Is.True);
            Assert.That(repeated, Is.SameAs(operation));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(operation.CoreCommitted, Is.True);
            Assert.That(operation.IsPrepared, Is.False, "GameData success is not Stage placement success");
            Assert.That(state.Money, Is.EqualTo(5000));
            Assert.That(state.Granted, Is.True);
            Assert.That(state.Clear, Is.False);
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(1));
            Assert.That(stageWrites.Updates[0].Target.AccountInDate, Is.EqualTo(fixture.Account));
            Assert.That(stageWrites.Updates[0].Target.RowInDate, Is.EqualTo(fixture.Row + "-Stage1"));
            Assert.That(JObject.Parse(stageWrites.Updates[0].Values.GetJson()).Properties().Select(p => p.Name),
                Is.EqualTo(new[] { "EquipStaffDataDic" }));
            Assert.That(state.Stage1.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Marketer), Is.Null);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(1), "Repeated GameData success cannot resend Stage placement");

            stageWrites.Updates[0].Reply(Bro("204", ""));
            Assert.That(operation.IsPrepared, Is.True);
            Assert.That(operation.IsCompleted, Is.False);
            Assert.That(state.Stage1.GetEquipStaff(ERestaurantFloorType.Floor1, EquipStaffType.Marketer).Id, Is.EqualTo("STAFF11"));
            AssertAccountLevels(fixture, "STAFF11", 1);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            var first = new object(); var second = new object();
            Assert.That(operation.TryClaimPresentation(first), Is.True);
            Assert.That(operation.HasStartedPresentation, Is.False);
            Assert.That(operation.TryMarkPresentationStarted(second), Is.False);
            Assert.That(operation.TryMarkPresentationStarted(first), Is.True);
            Assert.That(operation.HasStartedPresentation, Is.True);
            Assert.That(operation.TryClaimPresentation(first), Is.True);
            Assert.That(operation.TryClaimPresentation(second), Is.False);
            operation.ReleasePresentation(second);
            Assert.That(operation.TryClaimPresentation(second), Is.False);
            operation.ReleasePresentation(first);
            Assert.That(operation.TryClaimPresentation(second), Is.True);
            Assert.That(operation.HasStartedPresentation, Is.True, "Recreated display must offer explicit completion, not replay gameplay steps");
            stageWrites.Updates[0].Reply(Bro("204", ""));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(state.Commits, Is.EqualTo(1));
            Assert.That(state.Notifications, Is.EqualTo(1));
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(1));
            DrainTutorialAutosave(fixture);
        }
    }

    [Test]
    public void FirstTutorial_ExistingStaffLevelAndTokensArePreservedWithoutLegacyGoldInference()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (bool? priorReward in new bool?[] { true, null })
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites,
                TutorialAccountJson(3, 77), priorReward, 1234);
            CompleteTutorialStageReads(fixture, alreadyPlaced: true);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out _, out var error), Is.True, error);
            Assert.That(operation.Gold, Is.Zero, "Missing evidence is not an unclaimed new-account reward");
            Assert.That(operation.StaffAdded, Is.False);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(operation.IsPrepared, Is.False);
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(1), "Even matching placement needs this preparation's Stage confirmation");
            stageWrites.Updates[0].Reply(Bro("204", ""));
            Assert.That(operation.IsPrepared, Is.True);
            Assert.That(state.Money, Is.EqualTo(1234));
            Assert.That(state.Clear, Is.False);
            AssertAccountLevels(fixture, "STAFF11", 3);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(77));
            DrainTutorialAutosave(fixture);
        }
    }

    [Test]
    public void FirstTutorial_WaitsForAllApplicationsAndProtectionWithoutSendingOrGranting()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var none, out var status, out _), Is.False);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Waiting));
            Assert.That(none, Is.Null);
            fixture.Manager.LoadAllStageData(true);
            Reply(fixture, EStage.Stage3, TutorialStageResponse(fixture, EStage.Stage3));
            Reply(fixture, EStage.Stage2, TutorialStageResponse(fixture, EStage.Stage2));
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out none, out status, out _), Is.False);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Waiting));
            Assert.That(none, Is.Null);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Reply(fixture, EStage.Stage1, TutorialStageResponse(fixture, EStage.Stage1));
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out var mail, out var mailError), Is.True, mailError);
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out none, out status, out _), Is.False);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Waiting));
            Assert.That(state.Commits, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(mail.TryCancelBeforeRequest(), Is.True);
            fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out none, out status, out _), Is.False);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Blocked));
            Assert.That(none, Is.Null);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(stageWrites.Updates, Is.Empty);
            Assert.That(state.Money, Is.Zero);
        }
    }

    [Test]
    public void FirstTutorial_NormalAndSkipOnlyCompleteAfterTheirOwnConfirmedSave()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (bool skipped in new[] { false, true })
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            var start = PrepareTutorialConfirmed(fixture, stageWrites);
            var presenter = new object();
            Assert.That(fixture.Manager.TryCompleteFirstTutorial(start, skipped, out _, out _), Is.False,
                "Completion cannot bypass the explicit presentation owner");
            Assert.That(start.TryClaimPresentation(presenter), Is.True);
            int ordinarySave = fixture.Game.Writes;
            fixture.Manager.RequestGameDataAutosave(requireGameplay: false);
            Assert.That(fixture.Manager.TryCompleteFirstTutorial(start, skipped, out var waiting, out var waitingError), Is.False);
            Assert.That(waiting, Is.Null);
            Assert.That(waitingError, Is.Null, "Ordinary save contention is an unaccepted wait, not a terminal tutorial error");
            Assert.That(state.Clear, Is.False);
            fixture.Game.WriteReplies[ordinarySave](Bro("204", ""));
            int before = fixture.Game.Writes;
            Assert.That(fixture.Manager.TryCompleteFirstTutorial(start, skipped, out var completion, out var error), Is.True, error);
            Assert.That(completion.Skipped, Is.EqualTo(skipped));
            Assert.That(completion.Gold, Is.Zero);
            Assert.That(state.Clear, Is.False);
            Assert.That(completion.IsCompleted, Is.False);
            Assert.That((bool)JObject.Parse(fixture.Game.WriteValues[before].GetJson())["IsFirstTutorialClear"], Is.True);
            Assert.That(fixture.Manager.TryCompleteFirstTutorial(start, !skipped, out var repeated, out _), Is.True);
            Assert.That(repeated, Is.SameAs(completion));
            Assert.That(fixture.Game.Writes, Is.EqualTo(before + 1));
            fixture.Game.WriteReplies[before](Bro("204", ""));
            Assert.That(state.Clear, Is.True);
            Assert.That(completion.IsCompleted, Is.True);
            Assert.That(completion.CompletionCount, Is.EqualTo(1));
            Assert.That(state.Money, Is.EqualTo(5000));
            Assert.That(state.Commits, Is.EqualTo(2));
            fixture.Game.WriteReplies[before](Bro("204", ""));
            Assert.That(completion.CompletionCount, Is.EqualTo(1));
            Assert.That(state.Commits, Is.EqualTo(2));
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var none, out var status, out _), Is.True);
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.AlreadyCompleted));
            Assert.That(none, Is.Null);
            DrainTutorialAutosave(fixture);
        }
    }

    [Test]
    public void FirstTutorial_UnknownSaveRefusedCommitAndStageFailureNeverPretendPreparedOrResend()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string failure in new[] { "missing-response", "local-refusal", "stage-failure" })
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            CompleteTutorialStageReads(fixture);
            var before = fixture.Manager.StaffRuntime.Snapshot;
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out _, out _), Is.True);
            if (failure == "missing-response")
            {
                var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
                Assert.That(coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Mock tutorial response missing"), Is.True);
            }
            else
            {
                state.RefuseCommit = failure == "local-refusal";
                fixture.Game.WriteReplies[0](Bro("204", ""));
                if (failure == "stage-failure") stageWrites.Updates[0].Reply(Bro("500", ""));
            }
            Assert.That(operation.IsPrepared, Is.False);
            Assert.That(operation.IsCompleted, Is.False);
            Assert.That(operation.IsFailed, Is.True);
            int sends = fixture.Game.Writes;
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var same, out var status, out _), Is.False);
            Assert.That(same, Is.SameAs(operation));
            Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Blocked));
            Assert.That(fixture.Game.Writes, Is.EqualTo(sends));
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            Assert.That(state.Clear, Is.False);
            if (failure == "stage-failure")
            {
                Assert.That(state.Money, Is.EqualTo(5000), "Confirmed reward is retained, not repeated after placement failure");
                Assert.That(state.Commits, Is.EqualTo(1));
                Assert.That(stageWrites.Updates.Count, Is.EqualTo(1));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(stageWrites.Updates.Count, Is.EqualTo(1), "Unknown Stage outcome cannot restart placement via repeated GameData response");
                stageWrites.Updates[0].Reply(Bro("204", ""));
                Assert.That(operation.IsPrepared, Is.False, "A consumed ambiguous placement response is not a fresh save");
            }
            else
            {
                Assert.That(state.Money, Is.Zero);
                Assert.That(state.Commits, Is.Zero);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(before));
                Assert.That(stageWrites.Updates, Is.Empty);
            }
        }
    }

    [Test]
    public void FirstTutorial_AuthenticationAndQueryChangesRejectLateRewardAndPlacementResponses()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (bool afterReward in new[] { false, true })
        foreach (string change in new[] { "query", "same-account", "other-account" })
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            CompleteTutorialStageReads(fixture);
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out _, out _), Is.True);
            if (afterReward) fixture.Game.WriteReplies[0](Bro("204", ""));
            int committed = state.Commits;
            long money = state.Money;
            if (change == "query") fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            else Authenticate(fixture, change == "same-account" ? fixture.Account : Unique("tutorial-other-account"));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            if (afterReward) stageWrites.Updates[0].Reply(Bro("204", ""));
            Assert.That(operation.IsCurrent, Is.False);
            Assert.That(operation.IsPrepared, Is.False);
            Assert.That(operation.TryClaimPresentation(new object()), Is.False);
            Assert.That(state.Money, Is.EqualTo(money));
            Assert.That(state.Commits, Is.EqualTo(committed));
            Assert.That(state.Clear, Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
        }
    }

    [Test]
    public void FirstTutorial_RestoredClaimedRewardDoesNotRepeatGoldOrResetCurrentStaff()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            PrepareTutorialConfirmed(fixture, stageWrites);
            var saved = fixture.Manager.StaffRuntime.Snapshot;
            Assert.That(StaffAccountSaveConverter.TrySerialize(saved, out var accountJson, out var error), Is.True, error);
            int before = fixture.Game.Writes;
            RestoreTutorialAccount(fixture, accountJson, state.Granted, false);
            CompleteTutorialStageReads(fixture, alreadyPlaced: true);
            Assert.That(fixture.Game.Writes, Is.EqualTo(before), "Restoration alone does not issue the tutorial grant");
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var resumed, out _, out error), Is.True, error);
            Assert.That(resumed.Gold, Is.Zero);
            Assert.That(resumed.StaffAdded, Is.False);
            fixture.Game.WriteReplies[before](Bro("204", ""));
            Assert.That(resumed.IsPrepared, Is.False);
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(2));
            stageWrites.Updates[1].Reply(Bro("204", ""));
            Assert.That(resumed.IsPrepared, Is.True);
            Assert.That(state.Money, Is.EqualTo(5000));
            AssertAccountLevels(fixture, "STAFF11", 1);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(stageWrites.Updates.Count, Is.EqualTo(2), "Each explicit preparation checks its Stage once, without repeating rewards");
            DrainTutorialAutosave(fixture);
        }
    }

    [Test]
    public void FirstTutorial_ProtectedAutosaveUsesPostCommitLatestIncomeAndObserversSeeWholeReward()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            CompleteTutorialStageReads(fixture);
            bool observerWholeState = false;
            bool observerPurchaseAccepted = false;
            bool observerMailAccepted = false;
            state.OnNotify = () =>
            {
                observerWholeState = state.Money == 5025 && state.Granted == true
                    && fixture.Manager.StaffRuntime.Snapshot.Staff.Any(s => s.Id == "STAFF11" && s.Level == 1);
                observerPurchaseAccepted = fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out _, out _);
                observerMailAccepted = fixture.Manager.TryAcquireMailSaveLease(out _, out _);
            };
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out _, out _), Is.True);
            state.AddIncome(25);
            fixture.Manager.RequestGameDataAutosave(requireGameplay: false);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That((long)JObject.Parse(fixture.Game.WriteValues[0].GetJson())["Money"], Is.EqualTo(5000), "Accepted payload is fixed");
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(observerWholeState, Is.True);
            Assert.That(observerPurchaseAccepted, Is.False);
            Assert.That(observerMailAccepted, Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1), "Autosave remains deferred until placement finishes");
            stageWrites.Updates[0].Reply(Bro("204", ""));
            Assert.That(operation.IsPrepared, Is.True);
            Assert.That(fixture.Game.Writes, Is.EqualTo(2), "Deferred requests coalesce and generate current values after protection");
            var latest = JObject.Parse(fixture.Game.WriteValues[1].GetJson());
            foreach (string name in new[] { "Money", "TotalAddMoney", "DailyAddMoney", "WeeklyAddMoney" })
                Assert.That((long)latest[name], Is.EqualTo(5025), name);
            Assert.That((bool)latest["FirstTutorialStartRewardGranted"], Is.True);
            Assert.That(JObject.Parse((string)latest["StaffAccount"])["Staff"].Count(), Is.EqualTo(1));
            DrainTutorialAutosave(fixture);
        }
    }

    [Test]
    public void FirstTutorial_ConfirmedSignupCreatesEachAbsentStageOnceThenReadsAndAppliesActualResponses()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateTutorialBootstrapFixture(out var state, out var initialGame, out var initialStages);
            BeginTutorialBootstrap(fixture, initialGame, "201", confirmInitial: true);
            Assert.That(initialGame.Inserts.Count, Is.EqualTo(1));
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            fixture.Manager.LoadAllStageData(true);
            Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(3));
            foreach (var stage in new[] { EStage.Stage3, EStage.Stage2, EStage.Stage1 })
            {
                var firstRead = fixture.Stage.Gets.Last(call => call.Stage == stage);
                firstRead.Reply(Bro("200", Rows()));
                var initial = initialStages.Inserts.Last();
                Assert.That(initial.Stage, Is.EqualTo(stage));
                JObject values = JObject.Parse(initial.Values.GetJson());
                Assert.That(values["GiveStaffList"], Is.Empty, "Initial rows use existing empty Stage defaults, not reward injection");
                Assert.That(JToken.DeepEquals(values, JObject.Parse(new StageInfo().SaveData().GetParam().GetJson())), Is.True);
                int inserts = initialStages.Inserts.Count, reads = fixture.Stage.Gets.Count;
                firstRead.Reply(Bro("200", Rows()));
                Assert.That(initialStages.Inserts.Count, Is.EqualTo(inserts), "An outstanding initial Insert cannot repeat");
                initial.Reply(Bro("200", ""));
                Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(reads + 1), "Confirmed Insert must requery the same Stage");
                initial.Reply(Bro("200", ""));
                Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(reads + 1), "Repeated Insert success must not repeat requery");
                var actualRead = fixture.Stage.Gets.Last();
                Assert.That(actualRead.Stage, Is.EqualTo(stage));
                Assert.That(actualRead.Account, Is.EqualTo(fixture.Account));
                Assert.That(actualRead.IsCurrent(), Is.True);
                var response = TutorialStageResponse(fixture, stage);
                actualRead.Reply(response);
                actualRead.Reply(response);
            }
            Assert.That(initialStages.Inserts.Count, Is.EqualTo(3));
            Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(6));
            Assert.That(fixture.Stage.ApplyCalls, Is.EqualTo(3));
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
            Assert.That(fixture.Manager.StageMigrationCollection.Applications.All(a => a.Status == StaffStageApplicationStatus.Succeeded), Is.True);
            Assert.That(fixture.Manager.StageMigrationCollection.CompletionCount, Is.EqualTo(1));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1), "Only existing automatic empty-Stage migration is started");
            Assert.That(fixture.Manager.CurrentStaffMigrationExecution, Is.Not.Null);
            Assert.That(fixture.Manager.CurrentFirstTutorial, Is.Null, "Stage bootstrap itself must not grant the tutorial reward");
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Common));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff, Is.Empty);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
            Assert.That(state.Commits, Is.Zero);
            Assert.That(state.Money, Is.Zero);
            Assert.That(state.Granted, Is.False);
            Assert.That(initialStages.Updates, Is.Zero);
            Assert.That(fixture.Manager.FirstTutorialInitialStageError, Is.Null);
        }
    }

    [Test]
    public void FirstTutorial_StageBootstrapRejectsLegacyOrUnconfirmedCreationAndMalformedEmptyPages()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string scenario in new[] { "existing-200", "signup-with-existing-row", "initial-unconfirmed", "marker-absent", "marker-granted", "already-clear", "query-failed", "null-response", "null-rows", "continuation" })
        {
            var fixture = CreateTutorialBootstrapFixture(out var state, out var initialGame, out var initialStages);
            if (scenario == "existing-200") RestoreGameData(fixture, common: false);
            else if (scenario == "signup-with-existing-row") BeginTutorialBootstrap(fixture, initialGame, "201", existingRow: true);
            else BeginTutorialBootstrap(fixture, initialGame, "201", confirmInitial: scenario != "initial-unconfirmed");
            if (scenario == "marker-absent") state.Granted = null;
            if (scenario == "marker-granted") state.Granted = true;
            if (scenario == "already-clear") state.Clear = true;
            fixture.Manager.LoadAllStageData(true);
            if (scenario == "initial-unconfirmed")
            {
                Assert.That(fixture.Stage.Gets, Is.Empty, "GameData creation awaiting confirmation cannot start a Stage round");
                Assert.That(initialGame.Inserts.Count, Is.EqualTo(1));
            }
            else
            {
                BackendReturnObject response = scenario == "null-response" ? null
                    : scenario == "query-failed" ? Bro("500", Rows())
                    : scenario == "null-rows" ? Bro("200", "{\"rows\":null,\"firstKey\":null}")
                    : scenario == "continuation" ? Bro("200", "{\"rows\":[],\"firstKey\":{\"next\":\"page\"}}")
                    : Bro("200", Rows());
                Reply(fixture, EStage.Stage1, response);
                Assert.That(fixture.Manager.StageMigrationCollection.Stages.Single(s => s.Stage == EStage.Stage1).HasVerifiedRawRecords, Is.False);
            }
            Assert.That(initialStages.Inserts, Is.Empty, scenario);
            Assert.That(initialStages.Updates, Is.Zero);
            Assert.That(fixture.Stage.ApplyCalls, Is.Zero);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(state.Commits, Is.Zero);
            if (scenario == "existing-200" || scenario == "signup-with-existing-row") Assert.That(initialGame.Inserts, Is.Empty);
        }
    }

    [Test]
    public void FirstTutorial_UnknownInitialStageOutcomeCannotResendOrApplyAfterANewRound()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string failure in new[] { "no-response", "error", "throw-after-entry", "old-round-success" })
        {
            var fixture = CreateTutorialBootstrapFixture(out var state, out var initialGame, out var initialStages);
            BeginTutorialBootstrap(fixture, initialGame, "201", confirmInitial: true);
            initialStages.ThrowAfterEntry = failure == "throw-after-entry";
            fixture.Manager.LoadAllStageData(true);
            var oldRead = fixture.Stage.Gets.Single(call => call.Stage == EStage.Stage1);
            oldRead.Reply(Bro("200", Rows()));
            Assert.That(initialStages.Inserts.Count, Is.EqualTo(1));
            var initial = initialStages.Inserts[0];
            if (failure == "error") initial.Reply(Bro("500", ""));
            oldRead.Reply(Bro("200", Rows()));
            Assert.That(initialStages.Inserts.Count, Is.EqualTo(1));
            Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(3), "Unknown initial outcome must not begin an assumed-success requery");
            Assert.That(fixture.Stage.ApplyCalls, Is.Zero);
            fixture.Manager.LoadAllStageData(true); // Explicit read retry creates a new round; it is not Insert authority.
            if (failure == "old-round-success") initial.Reply(Bro("200", ""));
            Reply(fixture, EStage.Stage1, Bro("200", Rows()));
            Assert.That(initialStages.Inserts.Count, Is.EqualTo(1), "The per-account/Stage initial-send fence survives rounds");
            Assert.That(fixture.Stage.Gets.Count, Is.EqualTo(6));
            Assert.That(fixture.Stage.ApplyCalls, Is.Zero);
            Assert.That(fixture.Manager.FirstTutorialInitialStageError, Is.Not.Null.And.Not.Empty);
            Assert.That(fixture.Manager.CurrentStaffMigrationExecution, Is.Null);
            Assert.That(fixture.Manager.CurrentFirstTutorial, Is.Null);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(initialStages.Updates, Is.Zero);
            Assert.That(state.Commits, Is.Zero);
            Assert.That(state.Money, Is.Zero);
            string originalAccount = fixture.Account;
            Authenticate(fixture, Unique("different-tutorial-account"));
            RestoreGameData(fixture, common: true);
            Assert.That(fixture.Manager.FirstTutorialInitialStageError, Is.Null, "One account's unresolved bootstrap must not poison another account");
            Authenticate(fixture, originalAccount);
            RestoreGameData(fixture, common: false);
            Assert.That(fixture.Manager.FirstTutorialInitialStageError, Is.Not.Null.And.Not.Empty, "Returning to the original account must not erase its unresolved initial send");
        }
    }

    [Test]
    public void FirstTutorial_OrdinaryStageWriteWaitsOrBlocksAcrossReauthenticationAndDefersStalePayloads()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string outcome in new[] { "success", "null", "error", "throw-after-entry" })
        {
            var fixture = CreateTutorialFixture(out var state, out var stageWrites);
            CompleteTutorialStageReads(fixture);
            Action<BackendReturnObject> pending = null;
            int sends = 0, replies = 0;
            Action<Action<BackendReturnObject>> send = callback =>
            {
                sends++; pending = callback;
                if (outcome == "throw-after-entry") throw new InvalidOperationException("mock transport after entry");
            };
            var observer = typeof(BackendManager).GetMethod("ObserveOrdinaryStageWrite", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(Action<Action<BackendReturnObject>>), typeof(Action<BackendReturnObject>) }, null);
            Assert.That(observer, Is.Not.Null);
            Action observe = () => observer.Invoke(fixture.Manager,
                new object[] { "Stage1Data", send, new Action<BackendReturnObject>(_ => replies++) });
            if (outcome == "throw-after-entry") Assert.Throws<TargetInvocationException>(() => observe());
            else observe();
            Assert.That(sends, Is.EqualTo(1));
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var blocked, out var status, out _), Is.False);
            Assert.That(blocked, Is.Null);
            Assert.That(status, Is.EqualTo(outcome == "throw-after-entry"
                ? FirstTutorialPreparationStatus.Blocked : FirstTutorialPreparationStatus.Waiting));
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(state.Commits, Is.Zero);

            if (outcome != "success")
            {
                if (outcome != "throw-after-entry") pending(outcome == "null" ? null : Bro("500", ""));
                Assert.That(fixture.Manager.TryPrepareFirstTutorial(out blocked, out status, out _), Is.False);
                Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Blocked));
                Authenticate(fixture, fixture.Account); // Same account, new authentication and restoration generation.
                RestoreTutorialAccount(fixture, "{\"Version\":1,\"Staff\":[],\"PandaTokens\":55}", false, false);
                CompleteTutorialStageReads(fixture);
                Assert.That(fixture.Manager.TryPrepareFirstTutorial(out blocked, out status, out _), Is.False);
                Assert.That(blocked, Is.Null);
                Assert.That(status, Is.EqualTo(FirstTutorialPreparationStatus.Blocked), "A new read cannot erase an unknown earlier Stage write");
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(sends, Is.EqualTo(1));
            }

            pending(Bro("204", "")); // Only this same send's confirmed response releases its account fence.
            var oldPayload = state.Stage1.SaveData().GetParam();
            var bind = typeof(BackendManager).GetMethod("BindFirstTutorialStageSaveGuard", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldGuard = (Func<bool>)bind.Invoke(fixture.Manager, new object[] { "Stage1Data", oldPayload, new Func<bool>(() => true) });
            Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out status, out var error), Is.True, error);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(oldGuard(), Is.False, "An accepted tutorial operation must defer an ordinary Stage save");
            Assert.That(Field(typeof(BackendManager), "_deferredTutorialStageSave").GetValue(fixture.Manager),
                Is.SameAs(((GameDataRestoreContext)Field(typeof(BackendManager), "_gameDataRestoreContext").GetValue(fixture.Manager)).LegacyQuery));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            stageWrites.Updates.Single().Reply(Bro("204", ""));
            DrainTutorialAutosave(fixture);
            Assert.That(operation.IsPrepared, Is.True);
            Assert.That(oldGuard(), Is.False, "The old placement payload must not replay after protection is released");
            var freshGuard = (Func<bool>)bind.Invoke(fixture.Manager,
                new object[] { "Stage1Data", state.Stage1.SaveData().GetParam(), new Func<bool>(() => true) });
            Assert.That(freshGuard(), Is.True, "The deferred intent can use a newly generated current placement payload");
            Assert.That(state.Commits, Is.EqualTo(1));
            Assert.That(sends, Is.EqualTo(1));
            Assert.That(replies, Is.EqualTo(outcome == "null" || outcome == "error" ? 2 : 1));
        }
    }

    private Fixture CreateTutorialBootstrapFixture(out TutorialMemoryState state,
        out TutorialBootstrapGame initialGame, out TutorialBootstrapStages initialStages)
    {
        var fixture = CreateAccountRuntimeFixture(out _, common: false);
        state = new TutorialMemoryState(fixture.Stage.Runtime[EStage.Stage1], false, 0);
        initialGame = new TutorialBootstrapGame(fixture.Game);
        initialStages = new TutorialBootstrapStages();
        Field(typeof(BackendManager), "_firstTutorialState").SetValue(fixture.Manager, state);
        Field(typeof(BackendManager), "_firstTutorialStageTransport").SetValue(fixture.Manager, initialStages);
        Field(typeof(BackendManager), "_gameDataTransport").SetValue(fixture.Manager, initialGame);
        return fixture;
    }

    private void BeginTutorialBootstrap(Fixture fixture, TutorialBootstrapGame initialGame, string loginStatus,
        bool confirmInitial = false, bool existingRow = false)
    {
        fixture.Game.LoggedIn = fixture.Game.NativeLoggedIn = false;
        var authentication = fixture.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
        fixture.Account = fixture.Game.AccountInDate = Unique("tutorial-bootstrap-account");
        fixture.Game.LoggedIn = fixture.Game.NativeLoggedIn = true;
        Assert.That(fixture.Manager.CompleteGameDataAuthentication(authentication, Bro(loginStatus, "")), Is.True);
        fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row), ["Dia"] = N("0"),
            [GameDataRestoreContext.FirstTutorialStartRewardGrantedFieldName] = Attribute(new JValue(false)) };
        fixture.Game.GetReplies.Last()(Bro("200", existingRow ? Rows(row) : Rows()));
        if (confirmInitial)
        {
            Assert.That(initialGame.Inserts.Count, Is.EqualTo(1));
            initialGame.Inserts[0].Reply(Bro("200", ""));
            fixture.Game.GetReplies.Last()(Bro("200", Rows(row)));
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
        }
    }

    private Fixture CreateTutorialFixture(out TutorialMemoryState state, out TutorialStageWrites stageWrites,
        string accountJson = "{\"Version\":1,\"Staff\":[],\"PandaTokens\":55}", bool? granted = false, long money = 0)
    {
        var fixture = CreateAccountRuntimeFixture(out _);
        state = new TutorialMemoryState(fixture.Stage.Runtime[EStage.Stage1], granted, money);
        stageWrites = new TutorialStageWrites();
        Field(typeof(BackendManager), "_firstTutorialState").SetValue(fixture.Manager, state);
        Field(typeof(BackendManager), "_firstTutorialStageTransport").SetValue(fixture.Manager, stageWrites);
        var captured = state;
        fixture.Game.LatestFactory = () => captured.Latest();
        RestoreTutorialAccount(fixture, accountJson, granted, false);
        return fixture;
    }

    private void RestoreTutorialAccount(Fixture fixture, string accountJson, bool? granted, bool clear)
    {
        fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row),
            ["Dia"] = N("110"), ["StaffAccount"] = S(accountJson), ["IsFirstTutorialClear"] = Attribute(new JValue(clear)) };
        if (granted.HasValue) row["FirstTutorialStartRewardGranted"] = Attribute(new JValue(granted.Value));
        fixture.Game.GetReplies.Last()(Bro("200", Rows(row)));
        Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Common));
    }

    private static string TutorialAccountJson(int level, long tokens) => "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF11\",\"Level\":"
        + level + "}],\"PandaTokens\":" + tokens + "}";

    private BackendReturnObject TutorialStageResponse(Fixture fixture, EStage stage, bool alreadyPlaced = false)
    {
        var source = new StageInfo().SaveData();
        if (alreadyPlaced && stage == EStage.Stage1)
            source.EquipStaffDataDic["Floor1"]["Marketer"] = "STAFF11";
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row + "-" + stage) };
        foreach (var field in JObject.Parse(source.GetParam().GetJson()).Properties()) row[field.Name] = Attribute(field.Value);
        return Bro("200", Rows(row));
    }

    private void CompleteTutorialStageReads(Fixture fixture, bool alreadyPlaced = false)
    {
        fixture.Manager.LoadAllStageData(true);
        foreach (var stage in new[] { EStage.Stage3, EStage.Stage2, EStage.Stage1 })
            Reply(fixture, stage, TutorialStageResponse(fixture, stage, alreadyPlaced));
        Assert.That(fixture.Manager.StageMigrationCollection.Applications.All(a => a.Status == StaffStageApplicationStatus.Succeeded), Is.True);
    }

    private FirstTutorialExecution PrepareTutorialConfirmed(Fixture fixture, TutorialStageWrites stageWrites)
    {
        CompleteTutorialStageReads(fixture);
        int before = fixture.Game.Writes;
        Assert.That(fixture.Manager.TryPrepareFirstTutorial(out var operation, out _, out var error), Is.True, error);
        fixture.Game.WriteReplies[before](Bro("204", ""));
        stageWrites.Updates.Last().Reply(Bro("204", ""));
        Assert.That(operation.IsPrepared, Is.True);
        DrainTutorialAutosave(fixture);
        return operation;
    }

    private void DrainTutorialAutosave(Fixture fixture)
    {
        // The tutorial core intentionally schedules one latest-value save after Stage protection releases.
        var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
        if (coordinator.State == GameDataSaveCoordinatorState.Sending)
            fixture.Game.WriteReplies.Last()(Bro("204", ""));
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
    }

    private sealed class TutorialMemoryState : IFirstTutorialState
    {
        public StageInfo Stage1 { get; }
        public bool Clear;
        public bool? Granted;
        public long Money, Total, Daily, Weekly;
        public int Commits, Notifications;
        public bool RefuseCommit;
        public Action OnNotify;
        public TutorialMemoryState(StageInfo stage, bool? granted, long money)
        { Stage1 = stage; Granted = granted; Money = Total = Daily = Weekly = money; }
        public FirstTutorialMemory Read() => new FirstTutorialMemory(Clear, Granted, Money, Total, Daily, Weekly);
        public bool TryCommit(FirstTutorialExecution operation, out string error)
        {
            error = null;
            if (RefuseCommit || !operation.IsCurrent || operation.Before.Clear != Clear
                || operation.Before.RewardGranted != Granted || !Read().CanAdd(operation.Gold))
            { error = "Detached tutorial state refused the complete commit"; return false; }
            bool completing = (bool)typeof(FirstTutorialExecution).GetProperty("IsCompletion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(operation);
            if (completing) Clear = true;
            else { AddIncome(operation.Gold); Granted = true; }
            Commits++;
            return true;
        }
        public void AddIncome(long value)
        { checked { Money += value; Total += value; Daily += value; Weekly += value; } }
        public void NotifyReward(bool staffAdded, long gold) { Notifications++; OnNotify?.Invoke(); }
        public Param Latest()
        {
            var values = new Param(); values.Add("Dia", 110); values.Add("Money", Money); values.Add("TotalAddMoney", Total);
            values.Add("DailyAddMoney", Daily); values.Add("WeeklyAddMoney", Weekly); values.Add("IsFirstTutorialClear", Clear);
            if (Granted.HasValue) values.Add("FirstTutorialStartRewardGranted", Granted.Value);
            return values;
        }
    }

    private sealed class TutorialBootstrapGame : IGameDataBackendTransport
    {
        private readonly FakeGame _game;
        public readonly List<InsertCall> Inserts = new List<InsertCall>();
        public TutorialBootstrapGame(FakeGame game) { _game = game; }
        public bool LoggedIn => _game.LoggedIn;
        public bool NativeLoggedIn => _game.NativeLoggedIn;
        public string AccountInDate => _game.AccountInDate;
        public bool GameplaySaveAllowed => _game.GameplaySaveAllowed;
        public IReadOnlyList<StaffData> ReadCatalog() => _game.ReadCatalog();
        public bool Restore(BackendReturnObject response) => _game.Restore(response);
        public Param LatestValues() => _game.LatestValues();
        public Param InitialValues() => LoadUserData.CreateInitialGameData(new DateTime(2026, 9, 14));
        public void Get(string account, Action<BackendReturnObject> response) => _game.Get(account, response);
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> response) => _game.Update(target, values, response);
        public void Insert(Param values, Action<BackendReturnObject> response) => Inserts.Add(new InsertCall { Values = values, Reply = response });
        public sealed class InsertCall { public Param Values; public Action<BackendReturnObject> Reply; }
    }

    private sealed class TutorialBootstrapStages : IFirstTutorialStageTransport
    {
        public readonly List<InsertCall> Inserts = new List<InsertCall>();
        public bool ThrowAfterEntry;
        public int Updates;
        public void InsertInitial(EStage stage, Param values, Action<BackendReturnObject> response)
        {
            Inserts.Add(new InsertCall { Stage = stage, Values = values, Reply = response });
            if (ThrowAfterEntry) throw new InvalidOperationException("Mock exception after entering initial Stage transport");
        }
        public void UpdatePlacement(GameDataSaveTarget target, Param values, Action<BackendReturnObject> response)
        { Updates++; throw new InvalidOperationException("Bootstrap must not start a tutorial placement write"); }
        public sealed class InsertCall { public EStage Stage; public Param Values; public Action<BackendReturnObject> Reply; }
    }

    private sealed class TutorialStageWrites : IFirstTutorialStageTransport
    {
        public readonly List<UpdateCall> Updates = new List<UpdateCall>();
        public int Inserts;
        public void InsertInitial(EStage stage, Param values, Action<BackendReturnObject> response)
        { Inserts++; Assert.Fail("Restored Common accounts must not create Stage rows"); }
        public void UpdatePlacement(GameDataSaveTarget target, Param values, Action<BackendReturnObject> response) =>
            Updates.Add(new UpdateCall { Target = target, Values = values, Reply = response });
        public sealed class UpdateCall { public GameDataSaveTarget Target; public Param Values; public Action<BackendReturnObject> Reply; }
    }
}
#endif
