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

// Same injected SDK/account boundary as the existing runtime purchase/migration tests.
public partial class StaffStageMigrationCollectionTests
{
    private readonly List<Fixture> _questFixtures = new List<Fixture>();
    [TearDown]
    public void ReleaseQuestDisplayWrappers()
    {
        foreach (var fixture in _questFixtures)
        {
            var operations = (List<QuestStaffGrantExecution>)Field(typeof(BackendManager), "_questStaffGrants").GetValue(fixture.Manager);
            if (operations != null) foreach (var operation in operations)
                typeof(QuestStaffGrantExecution).GetMethod("ReleaseDisplayData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(operation, null);
        }
        _questFixtures.Clear();
    }

    [Test]
    public void QuestGrant_FourLiveStagesFixFreePayloadAndConfirmBothValuesOnceBeforeNotifications()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string quest in new[] { "MainReward01", "MainReward04", "MainReward05", "MainReward19" })
        {
            var fixture = CreateQuestStaffFixture(quest, out var state, out var wallet);
            var source = fixture.Manager.StaffRuntime.Snapshot;
            bool itemTutorial = UserInfo.IsMiniGameTutorialClear;
            var actualDoneSet = (HashSet<string>)Field(typeof(UserInfo), "_doneMainChallengeSet", true).GetValue(null);
            string[] actualDoneBefore = actualDoneSet.ToArray();
            QuestStaffTutorialPolicy.TryGetMapping(quest, out var id, out int bit);
            var draws = UsePurchaseSelection(fixture, "STAFF23");
            int completions = 0;
            state.OnNotify = () =>
            {
                Assert.That(state.Mask, Is.EqualTo(bit));
                AssertAccountLevels(fixture, id, 1);
                Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
            };
            fixture.Manager.QuestStaffGrantCompleted += op =>
            { completions++; Assert.That(op.LocalCompleted, Is.True); Assert.That(state.Notifications, Is.EqualTo(1)); };
            fixture.Manager.QuestStaffGrantCompleted += _ => throw new InvalidOperationException("display observer failed");
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out string error), Is.True, error);
            Assert.That(operation.DiamondCost, Is.Zero); Assert.That(draws(), Is.Zero);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1)); Assert.That(operation.LocalCompleted, Is.False);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(source)); Assert.That(state.Mask, Is.Zero);
            Assert.That(state.Notifications, Is.Zero);
            JObject payload = JObject.Parse(fixture.Game.WriteValues[0].GetJson());
            CollectionAssert.AreEquivalent(new[] { "StaffAccount", "QuestStaffGrantMask" }, payload.Properties().Select(p => p.Name));
            Assert.That((int)payload["QuestStaffGrantMask"], Is.EqualTo(bit));
            Assert.That(operation.Acquisition.Items.Single().StaffId, Is.EqualTo(id));
            Assert.That(operation.Acquisition.Items.Single().IsNew, Is.True);
            Assert.That(operation.Acquisition.TotalPandaTokens, Is.Zero);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out _, out _), Is.False);
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            Assert.That(fixture.Stage.Runtime[EStage.Stage1].GiveStaff("STAFF02"), Is.False);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(operation.Request.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
            Assert.That(operation.CompletionCount, Is.EqualTo(1)); Assert.That(completions, Is.EqualTo(1));
            Assert.That(state.Notifications, Is.EqualTo(1)); Assert.That(operation.NotificationError, Is.Not.Null);
            Assert.That(state.Claimed, Is.False, "Do not auto-claim the main quest reward");
            AssertAccountLevels(fixture, "STAFF03", 4); AssertAccountLevels(fixture, id, 1);
            Assert.That(source.Staff.Single().Id, Is.EqualTo("STAFF03")); Assert.That(source.Staff.Single().Level, Is.EqualTo(4));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(wallet.DiamondValue, Is.EqualTo(110)); Assert.That(wallet.GoldValue, Is.EqualTo(1000000));
            Assert.That(wallet.Notifications, Is.Zero); Assert.That(draws(), Is.Zero);
            Assert.That(UserInfo.IsMiniGameTutorialClear, Is.EqualTo(itemTutorial));
            CollectionAssert.AreEquivalent(actualDoneBefore, actualDoneSet,
                "Detached quest callbacks must not mutate real MainReward12 or any other real quest progress");
            Assert.That(fixture.Game.Writes, Is.EqualTo(2), "One grant plus one latest autosave; duplicate response adds nothing");
            var latest = JObject.Parse(fixture.Game.WriteValues[1].GetJson());
            Assert.That((int)latest["QuestStaffGrantMask"], Is.EqualTo(bit));
            Assert.That((string)latest["StaffAccount"], Is.EqualTo((string)payload["StaffAccount"]));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        }
    }

    [Test]
    public void QuestGrant_IneligibleOwnedMissingAndBusySourcesNeverDrawOrSend()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string scenario in new[] { "wrong", "future", "done", "claimed", "granted", "damaged-marker", "owned", "loading", "legacy", "broken", "duplicate-catalog", "mail", "autosave" })
        {
            var fixture = CreateQuestStaffFixture("MainReward01", out var state, out _);
            if (scenario == "wrong") state.Quest = "MainReward12";
            if (scenario == "future") state.Prerequisites = false;
            if (scenario == "done") state.Done = true;
            if (scenario == "claimed") state.Claimed = true;
            if (scenario == "granted") state.Mask = 1;
            if (scenario == "damaged-marker") state.Mask = 16;
            if (scenario == "owned") RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF06\",\"Level\":4}],\"PandaTokens\":55}");
            if (scenario == "loading") fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            if (scenario == "legacy") RestoreGameData(fixture, false);
            if (scenario == "broken") RestoreAccountJson(fixture, "{bad", false);
            if (scenario == "duplicate-catalog") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            GameDataMailSaveLease lease = null;
            if (scenario == "mail") Assert.That(fixture.Manager.TryAcquireMailSaveLease(out lease, out _), Is.True);
            if (scenario == "autosave") fixture.Manager.RequestGameDataAutosave();
            int before = fixture.Game.Writes;
            var draws = UsePurchaseSelection(fixture, "STAFF23");
            Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out var offer, out _), Is.False, scenario);
            Assert.That(offer, Is.Null);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False, scenario);
            Assert.That(fixture.Game.Writes, Is.EqualTo(before)); Assert.That(draws(), Is.Zero); Assert.That(state.Notifications, Is.Zero);
            if (scenario == "owned") { AssertAccountLevels(fixture, "STAFF06", 4); Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55)); }
            if (lease != null) Assert.That(lease.TryCancelBeforeRequest(), Is.True);
            if (scenario == "autosave") fixture.Game.WriteReplies[0](Bro("204", ""));
        }
    }

    [Test]
    public void QuestGrant_UnknownAndFailedLocalConfirmationKeepOwnershipAndNeverPartiallyGrant()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string scenario in new[] { "null", "error", "throw", "missing", "refuse", "commit-exception", "quest-changed", "query", "reauth", "account" })
        {
            var fixture = CreateQuestStaffFixture("MainReward04", out var state, out var wallet);
            var source = fixture.Manager.StaffRuntime.Snapshot;
            if (scenario == "throw") fixture.Game.OnUpdate = (_, __, ___) => throw new InvalidOperationException("after invocation");
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var op, out _), Is.True);
            var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
            if (scenario == "refuse") state.RefuseCommit = true;
            if (scenario == "commit-exception") state.ThrowCommit = true;
            if (scenario == "quest-changed") state.Quest = "MainReward05";
            if (scenario == "query") fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            if (scenario == "reauth" || scenario == "account") Authenticate(fixture, scenario == "reauth" ? fixture.Account : Unique("changed"));
            if (scenario == "missing") coordinator.MarkResponseMissing(op.Identity, "test missing response");
            else if (scenario == "null") fixture.Game.WriteReplies[0](null);
            else if (scenario == "error") fixture.Game.WriteReplies[0](Bro("500", ""));
            else if (scenario != "throw") fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(op.LocalCompleted, Is.False, scenario); Assert.That(state.Mask, Is.Zero);
            Assert.That(state.Notifications, Is.Zero); Assert.That(source.Staff.Count, Is.EqualTo(1));
            Assert.That(wallet.DiamondValue, Is.EqualTo(110));
            CollectionAssert.Contains(new[] { GameDataSaveCoordinatorState.Indeterminate,
                GameDataSaveCoordinatorState.LocalCompletionFailed, GameDataSaveCoordinatorState.InvalidatedAfterSend }, coordinator.State, scenario);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
            fixture.Manager.RequestGameDataAutosave();
            Assert.That(fixture.Game.Writes, Is.EqualTo(1), "No retry or queued write may escape unresolved ownership");
        }
    }

    [Test]
    public void QuestGrant_LatestAutosaveRejectsStaleExplicitEvidenceAndRoundTripDoesNotRegift()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture("MainReward19", out var state, out _);
            state.Mask = null; // A historical missing field is not itself an entitlement; the live eligible quest is required.
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var op, out _), Is.True);
            var pendingAuto = fixture.Manager.RequestGameDataAutosave();
            var stale = new Param(); stale.Add("QuestStaffGrantMask", 0);
            var rejected = fixture.Manager.RequestGameDataSave(stale);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(pendingAuto.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
            Assert.That(rejected.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
            // FIFO does not merge autosaves across the intervening explicit request, even when it is rejected.
            Assert.That(fixture.Game.Writes, Is.EqualTo(3));
            Assert.That(fixture.Game.WriteValues[2].GetJson(), Is.EqualTo(fixture.Game.WriteValues[1].GetJson()),
                "Both ordinary saves contain the same latest committed staff/evidence, not another grant");
            fixture.Game.WriteReplies[2](Bro("204", ""));
            Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
            var saved = JObject.Parse(fixture.Game.WriteValues[2].GetJson());
            var fresh = CreateQuestStaffFixture("MainReward19", out var freshState, out _);
            fresh.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            var row = new JObject { ["owner_inDate"] = S(fresh.Account), ["inDate"] = S(fresh.Row), ["Dia"] = N("110"),
                ["StaffAccount"] = S((string)saved["StaffAccount"]), ["QuestStaffGrantMask"] = N(saved["QuestStaffGrantMask"].ToString()) };
            fresh.Game.GetReplies.Last()(Bro("200", Rows(row)));
            Assert.That(fresh.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.Ready));
            freshState.Mask = fresh.Manager.RestoredGameData.QuestStaffGrantMask;
            Assert.That(freshState.Mask, Is.EqualTo(8)); AssertAccountLevels(fresh, "STAFF21", 1);
            Assert.That(fresh.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
            Assert.That(fresh.Game.Writes, Is.Zero); Assert.That(freshState.Notifications, Is.Zero);
            Assert.That(op.LocalCompleted, Is.True); Assert.That(op.CompletionCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void QuestGrant_ActualMigrationInstallationAllowsCurrentQuestWithoutChangingOriginalRestoreStatus()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreatePurchaseFixture(out _, out _, common: false);
            _questFixtures.Add(fixture);
            var state = new MemoryQuestStaffState { Quest = "MainReward01" };
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            fixture.Manager.LoadAllStageData(true); ReplyRuntimeRows(fixture);
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out _, out _), Is.False);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var op, out string error), Is.True, error);
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(op.LocalCompleted, Is.True); AssertAccountLevels(fixture, "STAFF06", 1);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
            fixture.Game.WriteReplies[2](Bro("204", ""));
        }
    }

    [Test]
    public void QuestGrant_CatalogMutationAfterOfferCannotChangeTheFixedEmployeeOrReachTransport()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture("MainReward01", out var state, out _);
            var registered = fixture.Game.Catalog.Single(s => s.Id == "STAFF06");
            var detached = UnityEngine.Object.Instantiate(registered);
            try
            {
                fixture.Game.Catalog = fixture.Game.Catalog.Where(s => s.Id != "STAFF06").Concat(new[] { detached }).ToArray();
                int reads = 0;
                fixture.Game.CatalogReader = () =>
                {
                    reads++;
                    // The first read authorizes the offer. Only its detached test copy changes after that.
                    if (reads == 2) Field(typeof(BasicData), "_id").SetValue(detached, "STAFF_DIFFERENT");
                    return fixture.Game.Catalog;
                };
                Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out _), Is.False);
                Assert.That(reads, Is.GreaterThanOrEqualTo(2));
                Assert.That(fixture.Game.Writes, Is.Zero); Assert.That(state.Mask, Is.Zero);
                Assert.That(state.Notifications, Is.Zero);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Single().Id, Is.EqualTo("STAFF03"));
                if (operation != null)
                {
                    Assert.That(operation.LocalCompleted, Is.False);
                    Assert.That(typeof(BackendManager).GetMethod("IsCurrentQuestStaffGrant", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(fixture.Manager, new object[] { operation }), Is.False,
                        "A rejected preparation cannot authorize the pending-request UI re-entry path");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(detached); }
        }
    }

    private Fixture CreateQuestStaffFixture(string quest, out MemoryQuestStaffState state, out MemoryStaffWallet wallet)
    {
        var fixture = CreatePurchaseFixture(out _, out wallet);
        RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
        state = new MemoryQuestStaffState { Quest = quest };
        Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
        _questFixtures.Add(fixture);
        return fixture;
    }
    private sealed class MemoryQuestStaffState : IQuestStaffTutorialState
    {
        public string Quest;
        public int? Mask = 0;
        public bool Prerequisites = true, Done, Claimed, RefuseCommit, ThrowCommit;
        public int Notifications;
        public Action OnNotify;
        public QuestStaffTutorialMemory Read()
        {
            bool matches = QuestStaffTutorialPolicy.TryGetMapping(Quest, out var id, out _);
            return new QuestStaffTutorialMemory(Quest, id, EStage.Stage1, true, matches, Prerequisites, Done, Claimed, Mask);
        }
        public bool TryCommit(QuestStaffTutorialMemory expected, int value, out string error)
        {
            error = "stale or refused test state";
            if (ThrowCommit) throw new InvalidOperationException("before commit");
            if (RefuseCommit || !expected.Matches(Read()) || !expected.TryCreateGrantedMask(out int planned, out _) || planned != value) return false;
            Mask = value; error = null; return true;
        }
        public void NotifyStaffAdded() { Notifications++; OnNotify?.Invoke(); }
    }

    [Test]
    public void QuestGrant_ActualQuestEntryAndDisplayRecreationKeepTheSameConfirmedRequest()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateQuestStaffFixture("MainReward01", out var state, out var wallet);
            var singleton = Field(typeof(BackendManager), "_instance", true).GetValue(null);
            bool sdkInitialized = Backend.IsInitialized;
            int draws = 0;
            var diamondWallet = fixture.Manager.StaffPurchaseWallet;
            // A native, explicitly offline owner avoids Unity fake-null MonoBehaviours at the real UI boundary.
            fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
                diamondWallet, _ => { draws++; return null; }, _ => Assert.Fail("No paid record effect"));
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager, state);
            Authenticate(fixture, fixture.Account);
            RestoreAccountJson(fixture, "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}");
            GameObject host = null;
            StaffGachaPurchaseDisplay display = null;
            try
            {
                UIGacha NewView()
                {
                    host = new GameObject("Quest UI lifetime fixture"); host.SetActive(false);
                    var view = host.AddComponent<UIGacha>();
                    var staffHost = new GameObject("Quest staff machine"); staffHost.transform.SetParent(host.transform);
                    var staff = staffHost.AddComponent<UIStaffGacha>();
                    Field(typeof(GachaMachineParent), "_uiGacha").SetValue(staff, view);
                    Field(typeof(UIGacha), "_gachaMachines").SetValue(view, new GachaMachineParent[] { staff });
                    return view;
                }
                void CloseView(UIGacha view)
                {
                    typeof(UIGacha).GetMethod("ClearQuestStaffEntry", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                    UnityEngine.Object.DestroyImmediate(host); host = null;
                }
                var view = NewView();
                Assert.That(UIGacha.EnableEditorEntryUnlockForTesting, Is.True);
                Assert.That(view.PrepareQuestStaffMachine(null), Is.False);
                state.Quest = "MainReward12";
                Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.False, "Editor unlock must not authorize item quest as a staff grant");
                state.Quest = "MainReward04"; state.Prerequisites = false;
                Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.False);
                state.Quest = "MainReward01"; state.Prerequisites = true;
                Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.True);
                Assert.That(fixture.Manager.TryStartQuestStaffGrant(out var operation, out string error), Is.True, error);
                Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.False);
                CloseView(view);
                view = NewView();
                Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.True, "Same pending operation may reopen without accepting another grant");
                display = StaffGachaPurchaseDisplay.ForQuestGrant(null, null, fixture.Manager, "MainReward01", null);
                var read = (Func<object>)Field(typeof(StaffGachaPurchaseDisplay), "_getCompleted").GetValue(display);
                var present = (Func<object, bool>)Field(typeof(StaffGachaPurchaseDisplay), "_canPresent").GetValue(display);
                Assert.That(read(), Is.Null); Assert.That(present(operation), Is.False);
                display.Dispose(); display = null; CloseView(view);
                state.OnNotify = () => state.Done = true;
                fixture.Game.WriteReplies[0](Bro("204", ""));
                fixture.Game.WriteReplies[1](Bro("204", ""));
                Assert.That(operation.LocalCompleted, Is.True);
                for (int reopen = 0; reopen < 2; reopen++)
                {
                    view = NewView();
                    Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.True, "Done ownership may show its saved result, without another offer");
                    display = StaffGachaPurchaseDisplay.ForQuestGrant(null, null, fixture.Manager, "MainReward01", null);
                    read = (Func<object>)Field(typeof(StaffGachaPurchaseDisplay), "_getCompleted").GetValue(display);
                    present = (Func<object, bool>)Field(typeof(StaffGachaPurchaseDisplay), "_canPresent").GetValue(display);
                    var result = (Func<object, StaffGachaAcquisitionResult>)Field(typeof(StaffGachaPurchaseDisplay), "_getAcquisition").GetValue(display);
                    Assert.That(read(), Is.SameAs(operation)); Assert.That(present(operation), Is.True);
                    Assert.That(result(operation), Is.SameAs(operation.Acquisition));
                    Assert.That(display.TryShowCompleted(false, out _), Is.False, "Unavailable card is only a presentation failure");
                    display.Close(); display.Dispose(); display = null; CloseView(view);
                    fixture.Game.WriteReplies[0](Bro("204", ""));
                }
                state.Quest = "MainReward12";
                view = NewView();
                Assert.That(view.PrepareQuestStaffMachine(fixture.Manager), Is.False, "An old completion is not the next quest's entry permission");
                Assert.That(fixture.Game.Writes, Is.EqualTo(2)); Assert.That(draws, Is.Zero);
                Assert.That(state.Notifications, Is.EqualTo(1)); Assert.That(operation.CompletionCount, Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.EqualTo(110)); Assert.That(state.Mask, Is.EqualTo(1));
                Assert.That(Field(typeof(BackendManager), "_instance", true).GetValue(null), Is.SameAs(singleton));
                Assert.That(Backend.IsInitialized, Is.EqualTo(sdkInitialized));
                CloseView(view);
            }
            finally
            {
                display?.Dispose();
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                fixture.Manager.DestroyEditorOfflineOwner();
            }
        }
    }
}
#endif
