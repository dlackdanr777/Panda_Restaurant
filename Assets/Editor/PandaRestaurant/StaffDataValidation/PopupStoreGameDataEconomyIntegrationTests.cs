#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public partial class StaffStageMigrationCollectionTests
{
    // The SDK transport/clock/journal and presentation are detached. The account adapter,
    // shared save coordinator, UserInfo wallet and named economy commit methods are real.
    // These are not live ads/payments, multi-device server tests or complete minigame plays.
    [Test]
    public void PopupEconomy_A_TicketStartThenActualAttendancePreservesBothRecords()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                StartPopup(fixture, bridge);
                Assert.That(bridge.CurrentRecord.UsedCount, Is.EqualTo(1));
                ActualAttendance(fixture, 5);
                Assert.That(UserInfo.Dia, Is.EqualTo(115));
                Assert.That(UserInfo.TotalAttendanceDays, Is.EqualTo(1));
                Assert.That(bridge.CurrentRecord.State, Is.EqualTo(PopupStoreGameDataState.Started));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 115);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_B_ActualAttendanceThenPopupClaimAddsDeltaOnce()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out _);
                ActualAttendance(fixture, 5);
                ClaimPopup(fixture, bridge);
                Assert.That(UserInfo.Dia, Is.EqualTo(116));
                Assert.That(UserInfo.TotalAttendanceDays, Is.EqualTo(1));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 116);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_C_PopupClaimThenActualItemGachaKeepsClaimAndCurrentWallet()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            Action cleanup = () => { };
            try
            {
                var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out _);
                var service = CreateEconomyService(fixture, out cleanup);
                fixture.Game.LatestFactory = PopupEconomyValues;
                ClaimPopup(fixture, bridge);
                string claimed = bridge.CurrentRecord.ToJson();
                Assert.That(service.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                Assert.That(UserInfo.Dia, Is.EqualTo(111), "The existing gacha also waits for its acknowledgement.");
                AckPopupWrites(fixture);
                Assert.That(service.LastTransaction.IsCompleted, Is.True);
                Assert.That(UserInfo.Dia, Is.EqualTo(101));
                Assert.That(UserInfo.GetGiveGachaItemCountDic()["OFFLINE-ECONOMY-ITEM"], Is.EqualTo(1));
                Assert.That(bridge.CurrentRecord.ToJson(), Is.EqualTo(claimed));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 101);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    [Test]
    public void PopupEconomy_D_ActualStaffGachaThenPopupStartDoesNotRestoreOldDia()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var service = CreateEconomyService(fixture, out var cleanup);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                Assert.That(service.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle, null, out string error), Is.True, error);
                AckPopupWrites(fixture);
                Assert.That(UserInfo.Dia, Is.EqualTo(100));
                Assert.That(service.LastTransaction.IsCompleted, Is.True);
                int staffCounter = fixture.Manager.StaffRuntime.Snapshot.GachaEconomy.StaffCounter;
                StartPopup(fixture, bridge);
                Assert.That(UserInfo.Dia, Is.EqualTo(100));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.GachaEconomy.StaffCounter, Is.EqualTo(staffCounter));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 100);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); cleanup(); }
        }
    }

    [Test]
    public void PopupEconomy_E_ActualFeverRewardThenPopupStartPreservesEarnedDia()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                scope.Fever(5);
                Assert.That(UserInfo.Dia, Is.EqualTo(115));
                StartPopup(fixture, bridge);
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 115);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_F_ActualAdRewardThenPopupStartPreservesDailyAdCounter()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                scope.Ad(fixture.Manager);
                AckPopupWrites(fixture);
                Assert.That(UserInfo.Dia, Is.EqualTo(113));
                Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(1));
                StartPopup(fixture, bridge);
                Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(1));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 113);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_G_MailOperationWithActualDiaAndStaffGrantThenPopupStart()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out var staffWallet);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                var environment = new MemoryMailEnvironment(fixture, staffWallet);
                environment.ApplyOverride = reward =>
                {
                    if (reward.Id != "Dia")
                        return fixture.Stage.Runtime[EStage.Stage1].GiveStaff(fixture.Game.Catalog.Single(x => x.Id == reward.Id))
                            ? MailRewardApplyResult.Applied(reward.Count) : MailRewardApplyResult.Rejected("Detached staff grant failed");
                    UserInfo.AddDia(reward.Count);
                    return MailRewardApplyResult.Applied(reward.Count);
                };
                var transport = new MemoryMailTransport();
                int grants = 0;
                var operation = new MailReceiveOperation(environment, transport, entry =>
                { grants++; fixture.Manager.RequestGameDataAutosave(); });
                var target = new MailReceiveTarget(PostType.Admin, "TEST_ONLY-popup-mail",
                    new[] { new MailRewardItem("Dia", 5), new MailRewardItem("STAFF02", 1) }, fixture.Manager.CurrentMailReceiveQuery);
                Assert.That(operation.TryStart(new[] { target }, out string error), Is.True, error);
                var response = new MailReceiveResponse(true, new JObject { ["postItems"] = new JArray(
                    MailRewardJson("Dia", 5), MailRewardJson("STAFF02", 1)) }.ToString());
                transport.Calls.Single().Reply(response); transport.Calls.Single().Reply(response);
                Assert.That(grants, Is.EqualTo(1)); Assert.That(operation.PublicCallCount, Is.EqualTo(1));
                Assert.That(UserInfo.Dia, Is.EqualTo(115));
                AssertAccountLevels(fixture, "STAFF02", 1);
                AckPopupWrites(fixture);
                StartPopup(fixture, bridge);
                Assert.That(UserInfo.Dia, Is.EqualTo(115)); AssertAccountLevels(fixture, "STAFF02", 1);
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 115);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_H_ActualGoldShopSpendThenPopupStartKeepsCostAndGoods()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                var shop = scope.Root.AddComponent<PopupEconomyShop>(); shop.Owner = fixture.Manager;
                shop.OnSlotButtonClicked(MoneyType.Gold, 100, 5);
                Assert.That(shop.Completed, Is.EqualTo(1));
                Assert.That(UserInfo.Dia, Is.EqualTo(105)); Assert.That(UserInfo.Money, Is.EqualTo(200));
                AckPopupWrites(fixture); StartPopup(fixture, bridge);
                Assert.That(UserInfo.Money, Is.EqualTo(200));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 105);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_I_PendingPopupClaimCoalescesAutosaveAndKeepsRealAdFeverIncome()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out _);
                bridge.ClaimRecovery(_ => { });
                Assert.That(fixture.Game.Writes, Is.EqualTo(1)); Assert.That(UserInfo.Dia, Is.EqualTo(110));
                Assert.That(UserInfo.TrySpendDia(1, out _), Is.False);
                scope.Fever(5); scope.Ad(fixture.Manager);
                Assert.That(UserInfo.Dia, Is.EqualTo(118));
                Assert.That(fixture.Manager.RequestGameDataAutosave().Accepted, Is.True);
                Assert.That(fixture.Game.Writes, Is.EqualTo(1), "Queued ordinary saves cannot overtake the reward write.");
                Assert.That((int)JObject.Parse(fixture.Game.WriteValues[0].GetJson())["Dia"], Is.EqualTo(111));
                AckPopupWrites(fixture);
                Assert.That(UserInfo.Dia, Is.EqualTo(119));
                Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(1));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 119);
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(119));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_J_AccountInvalidationKeepsUnknownJournalAndIgnoresLateClaim()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out var journal);
            try
            {
                bridge.ClaimRecovery(_ => { });
                fixture.Game.WriteReplies[0](Bro("500", ""));
                Assert.That(journal.Intents.Count, Is.EqualTo(1));
                fixture.Manager.InvalidateGameDataRestore();
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(110));
                Assert.That(bridge.CurrentOperation.Committed, Is.False);
                Assert.That(((StaffPurchaseDiamondWallet)UserInfo.StaffPurchaseWallet).HasReservation, Is.False);
                Assert.That(journal.Intents.Count, Is.EqualTo(1), "Session cleanup must not delete unresolved durable evidence.");
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_K_RestoredConfirmedClaimReconcilesJournalWithoutAddingDiaAgain()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out var journal);
            try
            {
                bridge.ClaimRecovery(_ => { });
                var intent = journal.Intents.Values.Single();
                fixture.Manager.InvalidateGameDataRestore();
                // A new original-row response is the evidence. The detached transport's legacy
                // restore sink is represented by this exact saved Dia assignment, never AddDia.
                Field(typeof(UserInfo), "_dia", true).SetValue(null, (int)JObject.Parse(intent.PayloadJson)["Dia"]);
                RestorePopupRecord(fixture, intent.Candidate);
                var restarted = fixture.Manager.CreatePopupStoreAccountBridge(PopupStoreGameDataTestData.Phase().Profile, PopupStoreGameDataTestData.Policy());
                Assert.That(restarted, Is.Not.Null); Assert.That(restarted.IsReady, Is.True, restarted.Status);
                Assert.That(restarted.CurrentRecord.State, Is.EqualTo(PopupStoreGameDataState.Claimed));
                Assert.That(journal.Intents, Is.Empty);
                int writes = fixture.Game.Writes;
                restarted.ClaimRecovery(result => Assert.That(result.State, Is.EqualTo(PopupStoreAccountClaimState.Confirmed)));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(111)); Assert.That(fixture.Game.Writes, Is.EqualTo(writes));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_L_RepeatStartAndDuplicateCallbackNeverConsumeTwoTickets()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, null, out _);
                var request = StartPopup(fixture, bridge);
                int writes = fixture.Game.Writes;
                fixture.Game.WriteReplies[0](Bro("204", ""));
                bridge.TryStartBusiness(request, "TEST_ONLY-another-local", result =>
                    Assert.That(result.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.Started)));
                bridge.TryStartBusiness(new PopupStoreBusinessStartRequest(fixture.Account, "TEST_ONLY-another-start",
                    PopupStoreBusinessMode.Rewarded, PopupStoreGameDataAccountBridge.AccessVersion), "TEST_ONLY-next-local",
                    result => Assert.That(result.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend)));
                Assert.That(bridge.CurrentRecord.UsedCount, Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(writes)); Assert.That(UserInfo.Dia, Is.EqualTo(110));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PopupEconomy_M_MalformedRowAndUnresolvedJournalBlockOrdinaryWrites(bool corruptJournal)
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var journal = new PopupEconomyJournal { FailRead = corruptJournal };
                var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
                Assert.That(source.TryClaim("TEST_ONLY-unresolved", out var candidate, out _), Is.True);
                var values = new Param(); values.Add(PopupStoreGameDataRecord.FieldName, candidate.ToJson()); values.Add("Dia", 111);
                journal.TryWrite(new PopupStoreAccountIntent(new GameDataSaveTarget(fixture.Account, fixture.Row),
                    candidate.LastRequestId, source, candidate, values.GetJson()), out _);
                ConfigurePopupFixture(fixture, journal, new PopupEconomyClock());
                RestorePopupRaw(fixture, "{malformed-row");
                fixture.Game.LatestFactory = () => { var p = new Param(); p.Add("Dia", 110); return p; };
                var bridge = fixture.Manager.CreatePopupStoreAccountBridge(PopupStoreGameDataTestData.Phase().Profile, PopupStoreGameDataTestData.Policy());
                Assert.That(bridge, Is.Not.Null); Assert.That(bridge.IsReady, Is.False);
                var request = fixture.Manager.RequestGameDataAutosave();
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(request.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
                Assert.That(journal.Intents.Count, Is.EqualTo(1)); Assert.That(UserInfo.Dia, Is.EqualTo(110));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_N_ArbitraryDirectRequestCannotOverwriteConfirmedRewardWithStaleDia()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out _);
                ClaimPopup(fixture, bridge); int writes = fixture.Game.Writes;
                var values = new Param(); values.Add("Dia", 110);
                Assert.That(GameDataSavePayload.TryCapture(values, out var payload, out _), Is.True);
                GameDataSaveReceipt response = null;
                fixture.Manager.CreateGameDataSingleUpdate().Send(new GameDataSaveIdentity("ordinary-arbitrary-not-save-prefix",
                    new GameDataSaveTarget(fixture.Account, fixture.Row)), payload, reply => response = reply);
                Assert.That(response, Is.Not.Null); Assert.That(response.SendStarted, Is.False);
                Assert.That(response.Disposition, Is.EqualTo(GameDataSaveDisposition.RejectedBeforeSend));
                Assert.That(fixture.Game.Writes, Is.EqualTo(writes)); Assert.That(UserInfo.Dia, Is.EqualTo(111));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_O_TenSecondClockTimeoutRejectsStartAndIgnoresLateClock()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var clock = new PopupEconomyManualClock(); var bridge = PopupBridge(fixture, null, out _, clock);
                int responses = 0; PopupStoreBusinessStartResponse response = null;
                var request = new PopupStoreBusinessStartRequest(fixture.Account, "TEST_ONLY-delayed-clock", PopupStoreBusinessMode.Rewarded,
                    PopupStoreGameDataAccountBridge.AccessVersion);
                bridge.TryStartBusiness(request, "TEST_ONLY-clock-business", value => { response = value; responses++; });
                Assert.That(response, Is.Null); bridge.ObserveElapsed(9.9); Assert.That(response, Is.Null);
                bridge.ObserveElapsed(.2);
                Assert.That(response?.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.RejectedBeforeSend));
                clock.Success(PopupStoreGameDataTestData.Utc);
                Assert.That(responses, Is.EqualTo(1)); Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(bridge.CurrentRecord.UsedCount, Is.Zero); Assert.That(UserInfo.Dia, Is.EqualTo(110));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_P_TenSecondWriteTimeoutNeverResendsAndLateAckAddsExactlyOneDelta()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var bridge = PopupBridge(fixture, PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0), out var journal);
                PopupStoreAccountClaimResult response = null;
                bridge.ClaimRecovery(value => response = value);
                bridge.ObserveElapsed(10);
                Assert.That(response?.State, Is.EqualTo(PopupStoreAccountClaimState.Pending));
                Assert.That(UserInfo.Dia, Is.EqualTo(110)); Assert.That(journal.Intents.Count, Is.EqualTo(1));
                bridge.ClaimRecovery(value => Assert.That(value.State, Is.EqualTo(PopupStoreAccountClaimState.Pending)));
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                fixture.Game.WriteReplies[0](Bro("204", "")); AckPopupWrites(fixture);
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(UserInfo.Dia, Is.EqualTo(111)); Assert.That(journal.Intents, Is.Empty);
                Assert.That(bridge.CurrentRecord.State, Is.EqualTo(PopupStoreGameDataState.Claimed));
                AssertSavedPopupAndBalance(fixture, bridge.CurrentRecord, 111);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_Q_NextNoonUsesFreshAuthorityClockAndFailureDoesNotInventQuota()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                var finalized = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
                Assert.That(finalized.TryClaim("TEST_ONLY-earlier-claim", out var claimed, out _), Is.True);
                var clock = new PopupEconomyManualClock(); var bridge = PopupBridge(fixture, claimed, out _, clock);
                var session = (PopupStoreRewardAccountSession)Field(typeof(PopupStoreGameDataAccountBridge), "_account").GetValue(bridge);
                PopupStoreBusinessAccessSnapshot result = null;
                bridge.QueryAccess(session, value => result = value); clock.Success(PopupStoreGameDataTestData.Utc);
                Assert.That(result.Success, Is.True); Assert.That(result.Remaining, Is.Zero);
                result = null; bridge.QueryAccess(session, value => result = value); Assert.That(result, Is.Null);
                clock.Failed("TEST_ONLY missing authority clock");
                Assert.That(result.Success, Is.False); Assert.That(fixture.Game.Writes, Is.Zero);
                result = null; bridge.QueryAccess(session, value => result = value);
                clock.Success(PopupStoreGameDataTestData.Utc.AddDays(1));
                Assert.That(result.Success, Is.True); Assert.That(result.Remaining, Is.EqualTo(1));
                Assert.That(bridge.CurrentRecord.Matches(claimed), Is.True, "A quota read never rewrites prior earned reward history.");
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    [Test]
    public void PopupEconomy_R_FirstTutorialGameplayGateDoesNotConstructAccountPlayBridge()
    {
        using (var scope = new PopupEconomyUserScope())
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            try
            {
                ConfigurePopupFixture(fixture, new PopupEconomyJournal(), new PopupEconomyClock());
                Field(typeof(BackendManager), "_gameDataTransport").SetValue(fixture.Manager, new PopupEconomyTutorialGate(fixture.Game));
                Assert.That(fixture.Manager.CreatePopupStoreAccountBridge(PopupStoreGameDataTestData.Phase().Profile,
                    PopupStoreGameDataTestData.Policy()), Is.Null);
                Assert.That(fixture.Game.Writes, Is.Zero); Assert.That(UserInfo.Dia, Is.EqualTo(110));
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }

    private PopupStoreGameDataAccountBridge PopupBridge(Fixture fixture, PopupStoreGameDataRecord record,
        out PopupEconomyJournal journal, IPopupStoreAccountClock clock = null)
    {
        journal = new PopupEconomyJournal();
        ConfigurePopupFixture(fixture, journal, clock ?? new PopupEconomyClock());
        if (record != null) RestorePopupRecord(fixture, record);
        fixture.Game.LatestFactory = PopupEconomyValues;
        var bridge = fixture.Manager.CreatePopupStoreAccountBridge(PopupStoreGameDataTestData.Phase().Profile, PopupStoreGameDataTestData.Policy());
        Assert.That(bridge, Is.Not.Null); Assert.That(bridge.IsReady, Is.True, bridge.Status);
        return bridge;
    }

    private void ConfigurePopupFixture(Fixture fixture, PopupEconomyJournal journal, IPopupStoreAccountClock clock)
    {
        Assert.That(Field(typeof(BackendManager), "_gameDataTransport").GetValue(fixture.Manager), Is.SameAs(fixture.Game));
        Assert.That(Field(typeof(BackendManager), "_stageDataTransport").GetValue(fixture.Manager), Is.SameAs(fixture.Stage));
        // This legacy fixture uses FormatterServices and explicitly injected SDK-free transports.
        // Mark only that detached instance as offline; never weaken the production admission gate.
        Field(typeof(BackendManager), "_editorOfflineOwner").SetValue(fixture.Manager, true);
        fixture.Manager.StaffPurchaseWallet = UserInfo.StaffPurchaseWallet;
        fixture.Manager.ConfigureEditorPopupStoreAccount(clock, journal, new UserPopupStoreAccountMemory());
    }

    private void RestorePopupRecord(Fixture fixture, PopupStoreGameDataRecord record)
        => RestorePopupRaw(fixture, record.ToJson());
    private void RestorePopupRaw(Fixture fixture, string popupJson)
    {
        bool restored = false;
        fixture.Manager.GetAndRestoreGameDataAsync((_, result) => restored = result.CanContinueLegacy);
        var row = new JObject { ["owner_inDate"] = S(fixture.Account), ["inDate"] = S(fixture.Row),
            ["Dia"] = N(UserInfo.Dia.ToString()), ["StaffAccount"] = S(CommonAccountJson()),
            [PopupStoreGameDataRecord.FieldName] = S(popupJson) };
        fixture.Game.GetReplies.Last()(Bro("200", Rows(row)));
        Assert.That(restored, Is.True);
    }

    private PopupStoreBusinessStartRequest StartPopup(Fixture fixture, PopupStoreGameDataAccountBridge bridge)
    {
        var request = new PopupStoreBusinessStartRequest(fixture.Account, "TEST_ONLY-start-" + Guid.NewGuid().ToString("N"),
            PopupStoreBusinessMode.Rewarded, PopupStoreGameDataAccountBridge.AccessVersion);
        PopupStoreBusinessStartResponse response = null;
        bridge.TryStartBusiness(request, "TEST_ONLY-business-" + Guid.NewGuid().ToString("N"), value => response = value);
        Assert.That(response, Is.Null, "No start authority before acknowledgement.");
        AckPopupWrites(fixture);
        Assert.That(response?.Kind, Is.EqualTo(PopupStoreBusinessStartResponseKind.Started), response?.Error);
        return request;
    }

    private void ClaimPopup(Fixture fixture, PopupStoreGameDataAccountBridge bridge)
    {
        int before = UserInfo.Dia, amount = bridge.CurrentRecord.Diamonds, write = fixture.Game.Writes;
        PopupStoreAccountClaimResult response = null;
        bridge.ClaimRecovery(value => response = value);
        Assert.That(UserInfo.Dia, Is.EqualTo(before)); Assert.That(response, Is.Null);
        AckPopupWrites(fixture);
        Assert.That(response?.State, Is.EqualTo(PopupStoreAccountClaimState.Confirmed), response?.Message);
        fixture.Game.WriteReplies[write](Bro("204", ""));
        bridge.ClaimRecovery(value => Assert.That(value.State, Is.EqualTo(PopupStoreAccountClaimState.Confirmed)));
        Assert.That(UserInfo.Dia, Is.EqualTo(before + amount));
        Assert.That(bridge.CurrentRecord.State, Is.EqualTo(PopupStoreGameDataState.Claimed));
    }

    private void ActualAttendance(Fixture fixture, int amount)
    {
        var state = (IAttendanceState)Activator.CreateInstance(typeof(UserInfo).Assembly.GetType("UserAttendanceState", true));
        Field(typeof(BackendManager), "_attendanceState").SetValue(fixture.Manager, new PopupEconomyAttendance(state));
        Field(typeof(BackendManager), "_attendanceNowKst").SetValue(fixture.Manager, (Func<DateTime>)(() => PopupStoreGameDataTestData.Utc.AddHours(9)));
        Field(typeof(BackendManager), "_attendanceReward").SetValue(fixture.Manager,
            (Func<int, AttendanceData>)(_ => new AttendanceData(MoneyType.Dia, amount)));
        int before = UserInfo.Dia;
        Assert.That(fixture.Manager.TryClaimAttendance(false, out var claim, out string error), Is.True, error);
        Assert.That(UserInfo.Dia, Is.EqualTo(before)); AckPopupWrites(fixture);
        Assert.That(claim.Committed, Is.True); Assert.That(UserInfo.Dia, Is.EqualTo(before + amount));
    }

    private void AckPopupWrites(Fixture fixture)
    {
        int safety = 0;
        while (fixture.Manager.CurrentGameDataSaveCoordinator != null
            && fixture.Manager.CurrentGameDataSaveCoordinator.State != GameDataSaveCoordinatorState.Idle)
        {
            Assert.That(++safety, Is.LessThan(12), "A rejected/indeterminate save must not be hidden by an unbounded test loop.");
            Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Sending));
            fixture.Game.WriteReplies.Last()(Bro("204", ""));
        }
    }

    private static Param PopupEconomyValues()
    {
        var values = new Param(); values.Add("Dia", UserInfo.Dia); values.Add("Money", UserInfo.Money);
        values.Add("LastAttendanceTime", UserInfo.LastAttendanceTime ?? "");
        values.Add("TotalAttendanceDays", UserInfo.TotalAttendanceDays);
        values.Add("TotalAddMoney", UserInfo.TotalAddMoney); values.Add("DailyAddMoney", UserInfo.DailyAddMoney);
        values.Add("WeeklyAddMoney", UserInfo.WeeklyAddMoney); values.Add("DailyAdDiaRewardCount", UserInfo.DailyAdDiaRewardCount);
        string json = (string)Field(typeof(UserInfo), "_popupStoreAccountJson", true).GetValue(null);
        if (json != null) values.Add(PopupStoreGameDataRecord.FieldName, json);
        return values;
    }

    private static void AssertSavedPopupAndBalance(Fixture fixture, PopupStoreGameDataRecord record, int diamonds)
    {
        var saved = JObject.Parse(fixture.Game.WriteValues.Last().GetJson());
        Assert.That((int)saved["Dia"], Is.EqualTo(diamonds));
        Assert.That((string)saved[PopupStoreGameDataRecord.FieldName], Is.EqualTo(record.ToJson()));
        Assert.That(fixture.Game.Inserts, Is.Zero);
        Assert.That(fixture.Game.WriteTargets.All(target => target.AccountInDate == fixture.Account && target.RowInDate == fixture.Row), Is.True);
    }

    private sealed class PopupEconomyClock : IPopupStoreAccountClock
    { public void Query(Action<DateTime> success, Action<string> failed) => success(PopupStoreGameDataTestData.Utc); }
    private sealed class PopupEconomyManualClock : IPopupStoreAccountClock
    {
        internal Action<DateTime> Success; internal Action<string> Failed;
        public void Query(Action<DateTime> success, Action<string> failed) { Success = success; Failed = failed; }
    }
    private sealed class PopupEconomyTutorialGate : IGameDataBackendTransport
    {
        private readonly FakeGame _inner;
        internal PopupEconomyTutorialGate(FakeGame inner) { _inner = inner; }
        public bool LoggedIn => _inner.LoggedIn;
        public bool NativeLoggedIn => _inner.NativeLoggedIn;
        public string AccountInDate => _inner.AccountInDate;
        public bool GameplaySaveAllowed => false;
        public void Get(string account, Action<BackendReturnObject> reply) => _inner.Get(account, reply);
        public void Insert(Param values, Action<BackendReturnObject> reply) => _inner.Insert(values, reply);
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> reply) => _inner.Update(target, values, reply);
        public Param LatestValues() => _inner.LatestValues();
        public Param InitialValues() => _inner.InitialValues();
        public IReadOnlyList<StaffData> ReadCatalog() => _inner.ReadCatalog();
        public bool Restore(BackendReturnObject response) => _inner.Restore(response);
    }
    private sealed class PopupEconomyJournal : IPopupStoreAccountJournalStore
    {
        internal readonly Dictionary<string, PopupStoreAccountIntent> Intents = new Dictionary<string, PopupStoreAccountIntent>();
        internal bool FailRead;
        private static string Key(GameDataSaveTarget target) => target.AccountInDate.Length + ":" + target.AccountInDate + ":" + target.RowInDate;
        public bool TryRead(GameDataSaveTarget target, out PopupStoreAccountIntent intent, out string error)
        { error = FailRead ? "TEST_ONLY unreadable journal" : null; Intents.TryGetValue(Key(target), out intent); return !FailRead; }
        public bool TryWrite(PopupStoreAccountIntent intent, out string error)
        { error = null; Intents[Key(intent.Target)] = PopupStoreAccountIntent.Read(intent.ToJson()); return true; }
        public bool TryClear(GameDataSaveTarget target, string requestId, out string error)
        {
            error = null; string key = Key(target);
            if (Intents.TryGetValue(key, out var intent) && intent.RequestId != requestId) { error = "Wrong request"; return false; }
            Intents.Remove(key); return true;
        }
    }
    private sealed class PopupEconomyAttendance : IAttendanceState
    {
        private readonly IAttendanceState _actual;
        internal PopupEconomyAttendance(IAttendanceState actual) { _actual = actual; }
        public AttendanceMemory Read() => _actual.Read();
        public bool TryCommit(AttendanceClaim claim, out string error) => _actual.TryCommit(claim, out error);
        public void Notify(MoneyType type) { } // No live DataBind/UI while testing the actual account commit.
    }
    public sealed class PopupEconomyShop : UIPayment
    {
        internal BackendManager Owner; internal int Completed;
        protected override void ShowPurchaseRejected(string error) { }
        protected override void CompleteGoldPurchasePresentationAndSave(MoneyType type, int value, int price)
        { Completed++; Owner.RequestGameDataAutosave(); }
    }
    public sealed class PopupEconomyAd : UIPaymentAdSlot
    {
        internal BackendManager Owner; internal int Completed;
        protected override void CompleteDiamondReward(int amount) { Completed++; Owner.RequestGameDataAutosave(); }
    }
    private sealed class PopupEconomyUserScope : IDisposable
    {
        private readonly EconomyUserInfoScope _wallet = new EconomyUserInfoScope();
        private readonly Dictionary<FieldInfo, object> _saved = new Dictionary<FieldInfo, object>();
        internal readonly GameObject Root;
        internal PopupEconomyUserScope()
        {
            Assert.That(Backend.IsInitialized || Backend.IsLogin, Is.False, "Never exercise an actual authenticated SDK in these tests.");
            Root = new GameObject("TEST_ONLY detached popup economy") { hideFlags = HideFlags.HideAndDontSave }; Root.SetActive(false);
            Replace("_lastAttendanceTime", ""); Replace("_totalAttendanceDays", 0);
            Replace("_money", 100L); Replace("_totalAddMoney", 100L); Replace("_dailyAddMoney", 100L); Replace("_weeklyAddMoney", 100L);
            Replace("_dailyAdDiaRewardCount", 0); Replace("_popupStoreAccountJson", null);
        }
        private void Replace(string name, object value)
        { var field = Field(typeof(UserInfo), name, true); _saved[field] = field.GetValue(null); field.SetValue(null, value); }
        internal void Fever(int amount)
        {
            var fever = Root.AddComponent<MiniGameFever>();
            Field(typeof(MiniGameFever), "_feverRewardConfig").SetValue(fever, new FeverRewardConfig(new[] { 1 }, new[] { amount }, MoneyType.Dia));
            Field(typeof(MiniGameFever), "_touchCount").SetValue(fever, 1);
            typeof(MiniGameFever).GetMethod("Reward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fever, null);
        }
        internal void Ad(BackendManager owner)
        {
            var watch = Root.AddComponent<WatchAdButton>();
            var slot = Root.AddComponent<PopupEconomyAd>(); slot.Owner = owner;
            Field(typeof(UIPaymentAdSlot), "_watchAdButton").SetValue(slot, watch);
            Field(typeof(UIPaymentAdSlot), "_valueText").SetValue(slot, Text("Reward"));
            Field(typeof(UIPaymentAdSlot), "_countText").SetValue(slot, Text("Count"));
            slot.Init(null, MoneyType.Dia);
            ((Action)Field(typeof(WatchAdButton), "OnAdButtonClicked").GetValue(watch))?.Invoke();
            var rewarded = (Action)Field(typeof(WatchAdButton), "OnAdRewarded").GetValue(watch);
            rewarded?.Invoke(); rewarded?.Invoke();
            Assert.That(slot.Completed, Is.EqualTo(1), "The actual ad reward callback must remain one-shot.");
        }
        private TextMeshProUGUI Text(string name)
        {
            var child = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(Root.transform, false); return child.AddComponent<TextMeshProUGUI>();
        }
        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            foreach (var pair in _saved) pair.Key.SetValue(null, pair.Value);
            _wallet.Dispose();
        }
    }

    [Test]
    public void PopupEconomy_RewardLeasePreservesPositiveIncomeAndBlocksConsumptionUntilCommit()
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out string error), Is.True, error);
        Assert.That(wallet.Wallet.TryAddReward(5, out error), Is.True, error);
        Assert.That(wallet.Wallet.TryAddReward(3, out error), Is.True, error);
        Assert.That(wallet.Wallet.TrySpend(10, out _), Is.False);
        Assert.That(wallet.Wallet.TryReserve(new object(), 10, 108, out _), Is.False);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 15, out error), Is.True, error);
        Assert.That(wallet.Balance, Is.EqualTo(123));
        Assert.That(wallet.Notifications, Is.EqualTo(2), "Confirmed reward remains unpublished until its record is installed.");
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 15, out _), Is.False);
        Assert.That(wallet.Wallet.TryAddReward(2, out error), Is.True, error);
        Assert.That(wallet.Wallet.ValidateReservation(owner, out error), Is.True, error);
        wallet.Wallet.NotifyCommittedReward();
        wallet.Wallet.NotifyCommittedReward();
        Assert.That(wallet.Notifications, Is.EqualTo(4));
        wallet.Wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.Wallet.TrySpend(10, out error), Is.True, error);
        Assert.That(wallet.Balance, Is.EqualTo(115));
    }

    [TestCase("owner")]
    [TestCase("generation")]
    [TestCase("external-assignment")]
    public void PopupEconomy_RewardLeaseRejectsWrongOwnerAndAccountReplacement(string mutation)
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out _), Is.True);
        object candidate = owner;
        if (mutation == "owner") candidate = new object();
        if (mutation == "generation") wallet.Generation++;
        if (mutation == "external-assignment") wallet.Balance = 101;
        int before = wallet.Balance;
        Assert.That(wallet.Wallet.TryCommitReservedReward(candidate, 3, out _), Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(before));
        Assert.That(wallet.Notifications, Is.Zero);
        Assert.That(wallet.Wallet.HasReservation, Is.True);
        wallet.Wallet.ReleaseBeforeSend(new object());
        Assert.That(wallet.Wallet.HasReservation, Is.True);
        wallet.Wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.Wallet.HasReservation, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void PopupEconomy_RewardLeaseDoesNotMakeZeroOrNegativePurchaseCostValid(int cost)
    {
        var wallet = new PopupWalletCase(100);
        Assert.That(wallet.Wallet.TryReserve(new object(), cost, 100, out _), Is.False);
        Assert.That(wallet.Wallet.HasReservation, Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(100));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PopupEconomy_CostAndRewardReservationsCannotAuthorizeEachOther(bool reward)
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(reward ? wallet.Wallet.TryReserveReward(owner, 100, out _)
            : wallet.Wallet.TryReserve(owner, 10, 100, out _), Is.True);
        Assert.That(reward ? wallet.Wallet.TryCommitReservedCost(owner, out _)
            : wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(100));
        Assert.That(wallet.Wallet.ValidateReservation(owner, out _), Is.True);
        Assert.That(reward ? wallet.Wallet.TryCommitReservedReward(owner, 3, out _)
            : wallet.Wallet.TryCommitReservedCost(owner, out _), Is.True);
        Assert.That(wallet.Balance, Is.EqualTo(reward ? 103 : 90));
        wallet.Wallet.ReleaseBeforeSend(owner);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public void PopupEconomy_InvalidRewardAmountDoesNotMutateOrReleaseProtection(int amount)
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out _), Is.True);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, amount, out _), Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(100));
        Assert.That(wallet.Wallet.HasReservation, Is.True);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.True);
        Assert.That(wallet.Balance, Is.EqualTo(103));
        wallet.Wallet.ReleaseBeforeSend(owner);
    }

    [Test]
    public void PopupEconomy_RewardLeaseAllowsZeroBalanceButNotStaleExpectedBalance()
    {
        var wallet = new PopupWalletCase(0);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 1, out _), Is.False);
        Assert.That(wallet.Wallet.TryReserveReward(owner, 0, out _), Is.True);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 1, out _), Is.True);
        wallet.Wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.Balance, Is.EqualTo(1));
    }

    [Test]
    public void PopupEconomy_RewardObserverFailureCannotConvertConfirmedDeltaToRetry()
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out _), Is.True);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.True);
        wallet.Observe = () => throw new InvalidOperationException("detached UI failure");
        Assert.DoesNotThrow(wallet.Wallet.NotifyCommittedReward);
        Assert.That(wallet.Wallet.LastNotificationError, Is.EqualTo("detached UI failure"));
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.False);
        wallet.Wallet.NotifyCommittedReward();
        Assert.That(wallet.Notifications, Is.EqualTo(1));
        Assert.That(wallet.Balance, Is.EqualTo(103));
        wallet.Wallet.ReleaseBeforeSend(owner);
    }

    [Test]
    public void PopupEconomy_ReleasedRewardLeaseStillFencesConsumptionDuringItsNotification()
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out _), Is.True);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.True);
        wallet.Wallet.ReleaseBeforeSend(owner);
        bool spent = true, reserved = true;
        wallet.Observe = () =>
        {
            spent = wallet.Wallet.TrySpend(1, out _);
            reserved = wallet.Wallet.TryReserveReward(new object(), wallet.Balance, out _);
        };
        wallet.Wallet.NotifyCommittedReward();
        Assert.That(spent, Is.False);
        Assert.That(reserved, Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(103));
    }

    [Test]
    public void PopupEconomy_RewardCancelledBeforeSendLeavesIncomeAndNoPopupDelta()
    {
        var wallet = new PopupWalletCase(100);
        var owner = new object();
        Assert.That(wallet.Wallet.TryReserveReward(owner, 100, out _), Is.True);
        Assert.That(wallet.Wallet.TryAddReward(5, out _), Is.True);
        wallet.Wallet.ReleaseBeforeSend(owner);
        Assert.That(wallet.Wallet.TryCommitReservedReward(owner, 3, out _), Is.False);
        Assert.That(wallet.Balance, Is.EqualTo(105));
        Assert.That(wallet.Wallet.TrySpend(5, out _), Is.True);
        Assert.That(wallet.Balance, Is.EqualTo(100));
    }

    private sealed class PopupWalletCase
    {
        internal int Balance, Notifications;
        internal long Generation;
        internal Action Observe;
        internal readonly StaffPurchaseDiamondWallet Wallet;
        internal PopupWalletCase(int balance)
        {
            Balance = balance;
            Wallet = new StaffPurchaseDiamondWallet(() => Balance, value => Balance = value,
                () => { Notifications++; Observe?.Invoke(); }, () => Generation);
        }
    }
}
#endif
