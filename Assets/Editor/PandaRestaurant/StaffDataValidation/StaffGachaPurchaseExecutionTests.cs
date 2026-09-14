#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

// The production purchase/selector/session/coordinator/runtime path, with only SDK and wallet storage detached.
public partial class StaffStageMigrationCollectionTests
{
    private readonly List<Fixture> _purchaseFixtures = new List<Fixture>();

    [TearDown]
    public void ReleaseDetachedPurchaseDisplayData()
    {
        // Production Backend.OnDestroy performs this owner-lifetime cleanup. The detached manager has no scene lifetime.
        foreach (Fixture fixture in _purchaseFixtures)
        {
            var operation = fixture.Manager.CurrentStaffPurchaseExecution;
            if (operation != null) typeof(StaffGachaPurchaseExecution).GetMethod("ReleaseOwnedDisplayData",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(operation, null);
        }
        _purchaseFixtures.Clear();
    }

    [Test]
    public void PurchaseExecution_SingleAndElevenFixOnePayloadAndCommitCoreBeforeOneCompletion()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (var kind in new[] { StaffGachaPurchaseType.Single, StaffGachaPurchaseType.Multi })
        foreach (bool throwAfterCore in new[] { false, true })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet);
            string[] expectedIds = PurchaseIds(kind);
            int wrappersBefore = Resources.FindObjectsOfTypeAll<GachaStaffData>().Length;
            Func<int> draws = UsePurchaseSelection(fixture, expectedIds);
            StaffAccountSaveData initial = fixture.Manager.StaffRuntime.Snapshot;
            string original = JsonConvert.SerializeObject(initial);
            int effects = 0, callbacks = 0, trailingCallbacks = 0, reentrantStarts = 0;
            bool effectsSawCore = false, callbacksSawCore = true;
            fixture.Manager.StaffPurchaseCommittedEffects = execution =>
            {
                effects++;
                effectsSawCore = execution.IsCompleted && wallet.DiamondValue == execution.Plan.DiamondsAfter
                    && fixture.Manager.StaffRuntime.Snapshot.PandaTokens == execution.Plan.AccountResult.UpdatedAccount.PandaTokens
                    && fixture.Stage.Runtime.Values.All(stage => stage.GetStaffLevel("STAFF23") == 1);
                if (fixture.Manager.TryStartStaffPurchase(kind, out _, out _)) reentrantStarts++;
                if (throwAfterCore) throw new InvalidOperationException("Detached post-commit record/effect failure");
            };
            fixture.Manager.StaffPurchaseCompleted += execution =>
            {
                callbacks++;
                // Production intentionally isolates observer exceptions; assert this recorded observation
                // outside the callback so an NUnit failure cannot be swallowed as a display failure.
                callbacksSawCore &= execution.IsCompleted && wallet.DiamondValue == execution.Plan.DiamondsAfter
                    && ReferenceEquals(fixture.Manager.StaffRuntime.Snapshot, execution.Plan.AccountResult.UpdatedAccount);
                if (throwAfterCore) throw new InvalidOperationException("Detached post-commit display failure");
            };
            fixture.Manager.StaffPurchaseCompleted += _ => trailingCallbacks++;
            Assert.That(fixture.Manager.TryStartStaffPurchase(kind, out var purchase, out var error), Is.True, error);
            Assert.That(purchase, Is.SameAs(fixture.Manager.CurrentStaffPurchaseExecution));
            Assert.That(purchase.IsCompleted, Is.False);
            Assert.That(purchase.SessionState, Is.EqualTo(StaffGachaRequestState.Processing));
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(wrappersBefore + expectedIds.Distinct().Count()),
                "Validation must allocate no native wrappers; only unique selected result wrappers remain owned");
            Assert.That(draws(), Is.EqualTo(expectedIds.Length));
            Assert.That(wallet.DiamondValue, Is.EqualTo(110));
            Assert.That(diamonds.HasReservation, Is.True);
            Assert.That(JsonConvert.SerializeObject(fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(original));
            Assert.That(effects, Is.Zero); Assert.That(callbacks, Is.Zero); Assert.That(wallet.Notifications, Is.Zero);
            AssertPurchasePayload(fixture, 0, purchase, kind == StaffGachaPurchaseType.Single ? 100 : 10,
                kind == StaffGachaPurchaseType.Single ? 55 : 110);
            AssertPurchaseItems(purchase, expectedIds, kind == StaffGachaPurchaseType.Single ? new[] { 0 } : PurchaseRewards());
            string frozen = JsonConvert.SerializeObject(purchase.Plan);
            var queued = fixture.Manager.RequestGameDataAutosave();
            Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(fixture.Game.LatestCalls, Is.Zero);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(purchase.IsCompleted, Is.True);
            Assert.That(purchase.SessionState, Is.EqualTo(StaffGachaRequestState.Succeeded));
            Assert.That(purchase.Status.ToString(), Is.EqualTo("Completed"));
            Assert.That(effectsSawCore, Is.True); Assert.That(effects, Is.EqualTo(1)); Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(callbacksSawCore, Is.True);
            Assert.That(trailingCallbacks, Is.EqualTo(1), "An observer failure cannot suppress independent later completion observers");
            if (throwAfterCore) Assert.That(purchase.NotificationError, Is.Not.Empty);
            Assert.That(reentrantStarts, Is.Zero, "Notifications cannot start another request before the current owner releases protection");
            Assert.That(wallet.Notifications, Is.EqualTo(1));
            Assert.That(diamonds.HasReservation, Is.False);
            Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.SameAs(purchase));
            AssertAccountLevels(fixture, "STAFF01", 2); AssertAccountLevels(fixture, "STAFF03", 4); AssertAccountLevels(fixture, "STAFF23", 1);
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That((int)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["Dia"], Is.EqualTo(purchase.Plan.DiamondsAfter));
            AssertPurchaseStoredAccount((string)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["StaffAccount"],
                purchase.Plan.AccountResult.UpdatedAccount.PandaTokens);
            fixture.Game.WriteReplies[0](Bro("204", ""));
            fixture.Game.WriteReplies[0](Bro("400", "{\"message\":\"late conflicting response\"}"));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(effects, Is.EqualTo(1)); Assert.That(callbacks, Is.EqualTo(1)); Assert.That(wallet.Notifications, Is.EqualTo(1));
            Assert.That(trailingCallbacks, Is.EqualTo(1), "A post-commit failure must not turn a duplicate response into another completion");
            Assert.That(draws(), Is.EqualTo(expectedIds.Length));
            Assert.That(JsonConvert.SerializeObject(purchase.Plan), Is.EqualTo(frozen));
            Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(wrappersBefore + expectedIds.Distinct().Count()),
                "Response-time validation and autosave must not leak discarded catalog wrappers");
            Assert.That(JsonConvert.SerializeObject(initial), Is.EqualTo(original));
            Assert.That(fixture.Game.Inserts, Is.Zero);
        }
    }

    [Test]
    public void PurchaseExecution_ReservedCostPreservesLaterRewardsAndBlocksEveryOtherSpendUntilRelease()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (bool requestAutosaveManually in new[] { false, true })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, PurchaseIds(StaffGachaPurchaseType.Multi));
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Multi, out var purchase, out var error), Is.True, error);
            string originalPayload = fixture.Game.WriteValues[0].GetJson();
            Assert.That(diamonds.TryAddReward(5, out error), Is.True, error);
            Assert.That(wallet.DiamondValue, Is.EqualTo(115));
            Assert.That(diamonds.TrySpend(1, out _), Is.False, "A competing consumer must not grant its product when cost reservation refuses it");
            Assert.That(wallet.DiamondValue, Is.EqualTo(115));
            Assert.That(fixture.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02")), Is.False);
            Assert.That(fixture.Stage.Runtime[EStage.Stage2].UpgradeStaff(scope.Staff("STAFF01")), Is.False);
            Assert.That(fixture.Stage.Runtime[EStage.Stage1].TrySetStaffSkin(scope.Staff("STAFF01"), (StaffSkinData)null), Is.False);
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False, "Mail must stop before consuming any post");
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out _, out _), Is.False);
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Multi, out _, out _), Is.False);
            var staleDiamondValues = new Param(); staleDiamondValues.Add("Dia", 115);
            var staleDiamondSave = fixture.Manager.RequestGameDataSave(staleDiamondValues);
            var waiting = requestAutosaveManually ? fixture.Manager.RequestGameDataAutosave() : null;
            Assert.That(fixture.Game.LatestCalls, Is.Zero, "No latest-value factory runs during the protected purchase");
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(purchase.IsCompleted, Is.True);
            Assert.That(wallet.DiamondValue, Is.EqualTo(15));
            Assert.That(purchase.Plan.DiamondsAfter, Is.EqualTo(10), "The fixed transmitted plan must not silently become a new computation");
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(110));
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            if (waiting != null) Assert.That(waiting.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That(fixture.Game.LatestCalls, Is.EqualTo(1), "Purchase completion itself must retain the +5 reward for a latest-value save even without any earlier autosave request");
            Assert.That((int)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["Dia"], Is.EqualTo(15));
            AssertPurchaseStoredAccount((string)JObject.Parse(fixture.Game.WriteValues[1].GetJson())["StaffAccount"], 110);
            Assert.That(fixture.Game.WriteValues[0].GetJson(), Is.EqualTo(originalPayload));
            Assert.That(draws(), Is.EqualTo(11));
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(staleDiamondSave.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend),
                "A pre-confirmation explicit Dia-only payload must not overwrite the latest reserved-cost balance");
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            Assert.That(diamonds.TrySpend(5, out error), Is.True, error);
            Assert.That(wallet.DiamondValue, Is.EqualTo(10));
            Assert.That(fixture.Stage.Runtime[EStage.Stage3].GiveStaff(scope.Staff("STAFF02")), Is.True);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(110), "Ordinary duplicate/grant never adds gacha token rewards");
        }
    }

    [Test]
    public void PurchaseExecution_PreflightRejectsWithoutDrawingSendingOrLeavingAQueuedPurchase()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string reason in new[] { "legacy", "loading", "damaged", "poor", "negative", "kind", "ordinary-save", "unknown-save", "mail",
            "migration", "catalog-null", "catalog-duplicate", "catalog-null-entry" })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet, common: reason != "legacy" && reason != "migration");
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            if (reason == "loading") fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
            if (reason == "damaged") RestoreAccountJson(fixture, "{broken", expectedContinue: false);
            if (reason == "poor") wallet.DiamondValue = 9;
            if (reason == "negative") wallet.DiamondValue = -1;
            if (reason == "ordinary-save" || reason == "unknown-save")
            {
                fixture.Manager.RequestGameDataAutosave();
                if (reason == "unknown-save")
                {
                    var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
                    Assert.That(coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Detached missing preceding save"), Is.True);
                }
                else Assert.That(fixture.Stage.Runtime[EStage.Stage1].UpgradeStaff(scope.Staff("STAFF01")), Is.True,
                    "An ordinary save still permits normal growth; only purchases are rejected instead of queued");
            }
            if (reason == "mail") Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.True);
            if (reason == "migration")
            {
                fixture.Manager.LoadAllStageData(true); ReplyRuntimeRows(fixture);
                Assert.That(fixture.Manager.CurrentStaffMigrationExecution.IsCompleted, Is.False);
            }
            if (reason == "catalog-null") fixture.Game.Catalog = null;
            if (reason == "catalog-duplicate") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            if (reason == "catalog-null-entry") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new StaffData[] { null }).ToArray();
            int writes = fixture.Game.Writes, balance = wallet.DiamondValue;
            var current = fixture.Manager.StaffRuntime.Snapshot;
            var kind = reason == "kind" ? (StaffGachaPurchaseType)999 : StaffGachaPurchaseType.Single;
            Assert.That(fixture.Manager.TryStartStaffPurchase(kind, out var purchase, out var error), Is.False, reason);
            Assert.That(purchase, Is.Null, reason);
            Assert.That(error, Is.Not.Empty, reason);
            Assert.That(draws(), Is.Zero, reason);
            Assert.That(fixture.Game.Writes, Is.EqualTo(writes), reason);
            Assert.That(wallet.DiamondValue, Is.EqualTo(balance), reason);
            Assert.That(diamonds.HasReservation, Is.False, reason);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(current), reason);
        }
    }

    [Test]
    public void PurchaseExecution_ConfirmedMigrationCommonIsEligibleWithoutFabricatingRestoreEvidence()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreatePurchaseFixture(out _, out var wallet, common: false);
            fixture.Manager.LoadAllStageData(true); ReplyRuntimeRows(fixture);
            var original = fixture.Manager.GameDataRestoreResult;
            Assert.That(original.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Common));
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.Zero);
            UsePurchaseSelection(fixture, "STAFF23");
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out var purchase, out var error), Is.True, error);
            Assert.That(fixture.Game.Writes, Is.EqualTo(2));
            AssertPurchasePayload(fixture, 1, purchase, 100, 0);
            fixture.Game.WriteReplies[1](Bro("204", ""));
            Assert.That(purchase.IsCompleted, Is.True); Assert.That(wallet.DiamondValue, Is.EqualTo(100));
            fixture.Manager.StaffRuntime.Refresh();
            AssertAccountLevels(fixture, "STAFF23", 1);
            Assert.That(fixture.Manager.GameDataRestoreResult, Is.SameAs(original));
            Assert.That(original.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(fixture.Manager.RestoredGameData, Is.Null);
        }
    }

    [Test]
    public void PurchaseExecution_UnknownResponsesAndPostSendExceptionsRetainFixedRequestWithoutRetryOrRefund()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string response in new[] { "null", "error", "throw", "missing" })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            if (response == "throw") fixture.Game.OnUpdate = (_, __, ___) => throw new InvalidOperationException("Detached post-send exception");
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out var purchase, out _), Is.True);
            string frozen = JsonConvert.SerializeObject(purchase.Plan);
            var coordinator = fixture.Manager.CurrentGameDataSaveCoordinator;
            if (response == "null") fixture.Game.WriteReplies[0](null);
            if (response == "error") fixture.Game.WriteReplies[0](Bro("400", "{\"message\":\"outcome unknown\"}"));
            if (response == "missing") Assert.That(coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Detached missing purchase response"), Is.True);
            Assert.That(purchase.Status.ToString(), Is.EqualTo("Indeterminate"), response);
            Assert.That(purchase.SessionState, Is.Not.EqualTo(StaffGachaRequestState.FailedUnapplied));
            Assert.That(purchase.IsCompleted, Is.False);
            Assert.That(wallet.DiamondValue, Is.EqualTo(110)); Assert.That(wallet.Notifications, Is.Zero);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Any(staff => staff.Id == "STAFF23"), Is.False);
            Assert.That(diamonds.HasReservation, Is.True);
            Assert.That(diamonds.TrySpend(1, out _), Is.False);
            Assert.That(fixture.Manager.TryAcquireMailSaveLease(out _, out _), Is.False);
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Multi, out _, out _), Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1)); Assert.That(draws(), Is.EqualTo(1));
            Assert.That(JsonConvert.SerializeObject(purchase.Plan), Is.EqualTo(frozen));
            if (response == "missing")
            {
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(purchase.IsCompleted, Is.True);
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100)); Assert.That(wallet.Notifications, Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void PurchaseExecution_ChangedGenerationAndLocalCommitFailureNeverApplyToAnotherState()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string change in new[] { "query", "same-account", "different-account", "raw-diamond", "raw-account", "catalog", "reservation-replaced" })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            int effects = 0;
            fixture.Manager.StaffPurchaseCommittedEffects = _ => effects++;
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out var purchase, out var error), Is.True, error);
            var owner = fixture.Manager.CurrentGameDataSaveCoordinator;
            var candidate = purchase.Plan.AccountResult.UpdatedAccount;
            string frozen = JsonConvert.SerializeObject(candidate);
            if (change == "query") RestoreAccountJson(fixture, CommonAccountJson());
            if (change == "same-account" || change == "different-account")
            {
                fixture.Manager.LogOut();
                Authenticate(fixture, change == "same-account" ? fixture.Account : Unique("purchase-new-account"));
                RestoreGameData(fixture, true);
            }
            if (change == "raw-diamond") wallet.DiamondValue = 111; // Unsupported external assignment, not an accepted reward.
            if (change == "raw-account") Field(typeof(StaffAccountRuntime), "_current").SetValue(fixture.Manager.StaffRuntime,
                new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord("STAFF01", 4) }, 55));
            if (change == "catalog") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
            if (change == "reservation-replaced")
            {
                // Deliberate misuse of the public seam: the same owner cannot confirm with a different reservation contract.
                diamonds.ReleaseBeforeSend(purchase);
                Assert.That(diamonds.TryReserve(purchase, 1, 110, out error), Is.True, error);
            }
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(purchase.IsCompleted, Is.False, change);
            Assert.That(purchase.Status.ToString() == "InvalidatedAfterSend" || purchase.Status.ToString() == "LocalCompletionFailed", Is.True, change);
            Assert.That(owner.LastReceipt.RawResponse.IsSuccess, Is.True, "Retain real response success separately from local completion");
            Assert.That(wallet.DiamondValue, Is.EqualTo(change == "raw-diamond" ? 111 : 110));
            Assert.That(wallet.Notifications, Is.Zero); Assert.That(effects, Is.Zero);
            Assert.That(diamonds.HasReservation, Is.True, change);
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(purchase.Identity.Target), Is.True);
            Assert.That(fixture.Manager.StaffRuntime.Snapshot.Staff.Any(staff => staff.Id == "STAFF23"), Is.False);
            Assert.That(JsonConvert.SerializeObject(candidate), Is.EqualTo(frozen));
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(effects, Is.Zero); Assert.That(draws(), Is.EqualTo(1)); Assert.That(fixture.Game.Writes, Is.EqualTo(1));
        }
    }

    [Test]
    public void PurchaseExecution_RejectedPreparationReleasesOnlyItsProtectionAndCannotAuthorizeOrdinaryPayloads()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        foreach (string invalid in new[] { "null-draw", "throw-draw", "catalog-changed", "token-overflow" })
        {
            var fixture = CreatePurchaseFixture(out var diamonds, out var wallet);
            int draws = 0;
            int wrappersBefore = Resources.FindObjectsOfTypeAll<GachaStaffData>().Length;
            if (invalid == "token-overflow") RestoreAccountJson(fixture, CommonAccountJson().Replace("\"PandaTokens\":55", "\"PandaTokens\":" + long.MaxValue));
            Func<int> actual = UsePurchaseSelection(fixture, invalid == "token-overflow" ? "STAFF01" : "STAFF23");
            var select = fixture.Manager.StaffPurchaseDraw;
            fixture.Manager.StaffPurchaseDraw = candidates =>
            {
                draws++;
                if (invalid == "null-draw") return null;
                if (invalid == "throw-draw") throw new InvalidOperationException("Detached selector failure");
                var selected = select(candidates);
                if (invalid == "catalog-changed") fixture.Game.Catalog = fixture.Game.Catalog.Concat(new[] { fixture.Game.Catalog[0] }).ToArray();
                return selected;
            };
            Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Single, out var purchase, out var error), Is.False, invalid);
            if (purchase != null)
            {
                Assert.That(purchase.IsCompleted, Is.False, invalid);
                Assert.That(purchase.Status, Is.EqualTo(StaffGachaPurchaseExecutionStatus.RejectedBeforeSend));
                Assert.That(purchase.Request.Receipt.SendStarted, Is.False);
            }
            Assert.That(error, Is.Not.Empty); Assert.That(draws, Is.EqualTo(1));
            Assert.That(fixture.Game.Writes, Is.Zero); Assert.That(wallet.DiamondValue, Is.EqualTo(110));
            Assert.That(wallet.Notifications, Is.Zero); Assert.That(diamonds.HasReservation, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(wrappersBefore),
                "Rejected preparation must release even wrappers selected before the later failure");
            if (invalid == "catalog-changed") fixture.Game.Catalog = Catalog();
            Assert.That(diamonds.TrySpend(1, out error), Is.True, error);
            var forgery = new Param(); forgery.Add("Dia", 0); forgery.Add("StaffAccount", "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}");
            var rejected = fixture.Manager.RequestGameDataSave(forgery);
            Assert.That(rejected.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void PurchaseExecution_RealSelectionConsumesRandomOnlyOnceWhilePurePlanAndRePresentationDoNot()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var before = Random.state;
            try
            {
                var fixture = CreatePurchaseFixture(out _, out var wallet);
                int draws = 0, effects = 0;
                fixture.Manager.StaffPurchaseDraw = candidates => { draws++; return StaffGachaRandomSelector.Select(candidates); };
                fixture.Manager.StaffPurchaseCommittedEffects = _ => effects++;
                Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Multi, out var purchase, out var error), Is.True, error);
                Assert.That(draws, Is.EqualTo(11));
                Assert.That(Random.state, Is.Not.EqualTo(before), "The actual staff selector consumes random during the accepted draw, unlike the pure calculators");
                var afterDraw = Random.state;
                var pendingDisplay = new StaffGachaPurchaseDisplay(null, null, fixture.Manager);
                pendingDisplay.Close(); pendingDisplay.Dispose();
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.SameAs(purchase));
                Assert.That(purchase.IsCompleted, Is.False);
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                string plan = JsonConvert.SerializeObject(purchase.Plan);
                Assert.That(StaffGachaPurchasePlanCalculator.TryCalculate(fixture.Manager.StaffRuntime.Snapshot, 110,
                    StaffGachaPurchaseType.Multi, purchase.DrawnStaff, out _, out error), Is.True, error);
                Assert.That(Random.state, Is.EqualTo(afterDraw));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(purchase.IsCompleted, Is.True); Assert.That(wallet.DiamondValue, Is.EqualTo(10));
                Assert.That(fixture.Manager.CanPresentStaffPurchase(purchase), Is.True);
                // No display object is required to own the request; a new observer only reads the retained completion.
                for (int reopen = 0; reopen < 3; reopen++)
                {
                    Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.SameAs(purchase));
                    Assert.That(fixture.Manager.CanPresentStaffPurchase(purchase), Is.True);
                    Assert.That(purchase.DrawnStaff.Count, Is.EqualTo(11));
                    var display = new StaffGachaPurchaseDisplay(null, null, fixture.Manager);
                    Assert.That(display.TryShowCompleted(false, out _), Is.False, "Missing machine/card is a display failure, not a failed purchase");
                    display.Close(); display.Dispose();
                }
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(JsonConvert.SerializeObject(purchase.Plan), Is.EqualTo(plan));
                Assert.That(draws, Is.EqualTo(11)); Assert.That(effects, Is.EqualTo(1));
                Assert.That(Random.state, Is.EqualTo(afterDraw), "Success/duplicate response/re-observation must not draw again or consume decoration randomness");
                Assert.That(fixture.Game.Writes, Is.EqualTo(2), "One purchase plus the latest post-confirmation autosave; re-observation adds no requests");
                fixture.Game.WriteReplies[1](Bro("204", ""));
            }
            finally { Random.state = before; }
        }
    }

    private Fixture CreatePurchaseFixture(out StaffPurchaseDiamondWallet diamonds, out MemoryStaffWallet wallet, bool common = true)
    {
        var fixture = CreateAccountRuntimeFixture(out wallet, common);
        _purchaseFixtures.Add(fixture);
        var storage = wallet;
        diamonds = new StaffPurchaseDiamondWallet(() => storage.DiamondValue, value => storage.DiamondValue = value,
            () => storage.Notifications++);
        fixture.Manager.StaffPurchaseWallet = diamonds;
        fixture.Manager.StaffPurchaseCommittedEffects = _ => { };
        fixture.Game.LatestFactory = () =>
        {
            var values = new Param(); values.Add("Dia", storage.DiamondValue); values.Add("Money", storage.GoldValue); return values;
        };
        return fixture;
    }

    private static string[] PurchaseIds(StaffGachaPurchaseType kind) => kind == StaffGachaPurchaseType.Single
        ? new[] { "STAFF23" } : new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)).ToArray();
    private static int[] PurchaseRewards() => new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)).ToArray();
    private static Func<int> UsePurchaseSelection(Fixture fixture, params string[] ids)
    {
        int selectedCount = 0;
        fixture.Manager.StaffPurchaseDraw = candidates =>
        {
            Assert.That(selectedCount, Is.LessThan(ids.Length), "No hidden reroll is permitted");
            string id = ids[selectedCount++];
            var desired = candidates.OfType<GachaStaffData>().Single(staff => staff.Id == id);
            Rank rank = desired.StaffData.Rank;
            int roll = rank == Rank.Normal1 || rank == Rank.Normal2 ? 0 : rank == Rank.Rare ? 60 : rank == Rank.Unique ? 80 : 95;
            var peers = candidates.OfType<GachaStaffData>().Where(staff => rank == Rank.Normal1 || rank == Rank.Normal2
                ? staff.StaffData.Rank == Rank.Normal1 || staff.StaffData.Rank == Rank.Normal2 : staff.StaffData.Rank == rank).ToList();
            int index = peers.FindIndex(staff => staff.Id == id);
            GachaStaffData actual = StaffGachaRandomSelector.Select(candidates, roll, index);
            Assert.That(actual.Id, Is.EqualTo(id));
            return actual;
        };
        return () => selectedCount;
    }
    private static void AssertPurchaseItems(StaffGachaPurchaseExecution purchase, string[] ids, int[] rewards)
    {
        var items = purchase.Plan.AccountResult.Acquisition.Items;
        CollectionAssert.AreEqual(ids, purchase.DrawnStaff.Select(staff => staff.Id));
        CollectionAssert.AreEqual(ids, items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(rewards, items.Select(item => item.PandaTokenReward));
        Assert.That(items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(items.Count(item => item.IsDuplicate), Is.EqualTo(ids.Length - 1));
        CollectionAssert.AreEqual(new[] { "STAFF23" }, purchase.Plan.AccountResult.Acquisition.NewStaffIds);
    }
    private static void AssertPurchasePayload(Fixture fixture, int index, StaffGachaPurchaseExecution purchase, int dia, long tokens)
    {
        JObject fields = JObject.Parse(fixture.Game.WriteValues[index].GetJson());
        CollectionAssert.AreEquivalent(new[] { "Dia", "StaffAccount" }, fields.Properties().Select(field => field.Name));
        Assert.That((int)fields["Dia"], Is.EqualTo(dia));
        Assert.That(purchase.Plan.DiamondsAfter, Is.EqualTo(dia));
        Assert.That(fixture.Game.WriteTargets[index].AccountInDate, Is.EqualTo(fixture.Account));
        Assert.That(fixture.Game.WriteTargets[index].RowInDate, Is.EqualTo(fixture.Row));
        AssertPurchaseStoredAccount((string)fields["StaffAccount"], tokens);
    }
    private static void AssertPurchaseStoredAccount(string json, long tokens)
    {
        var read = StaffAccountSaveConverter.Read(json);
        Assert.That(read.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success), read.Error);
        Assert.That(read.Data.Version, Is.EqualTo(1)); Assert.That(read.Data.PandaTokens, Is.EqualTo(tokens));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03", "STAFF23" }, read.Data.Staff.Select(staff => staff.Id));
        CollectionAssert.AreEqual(new[] { 2, 4, 1 }, read.Data.Staff.Select(staff => staff.Level));
    }
}
#endif
