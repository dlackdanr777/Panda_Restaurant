#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

/// <summary>Exercises actual BackendManager entry methods with a detached managed instance and a memory-only SDK boundary.</summary>
public class GameDataEntrySafetyTests
{
    private readonly Dictionary<StaffData, string> _assets = new Dictionary<StaffData, string>();
    private Random.State _random;
    private string _gameState;
    private bool _saveEnabled;
    private GameDataSaveCoordinator[] _blockedCoordinators;

    [SetUp]
    public void SetUp()
    {
        _random = Random.state;
        _gameState = GameState();
        _saveEnabled = (bool)Field(typeof(BackendManager), "_isSaveEnabled", true).GetValue(null);
        _blockedCoordinators = ((HashSet<GameDataSaveCoordinator>)Field(typeof(GameDataSaveCoordinator), "BlockedCoordinators", true)
            .GetValue(null)).ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_random));
            Assert.That(GameState(), Is.EqualTo(_gameState), "Real UserInfo or PaymentInfo changed");
            foreach (var asset in _assets)
                Assert.That(EditorJsonUtility.ToJson(asset.Key), Is.EqualTo(asset.Value), "Staff resource changed");
        }
        finally
        {
            // Isolation of the static flag touched by the actual LogOut/login entry methods, not a runtime recovery API.
            Field(typeof(BackendManager), "_isSaveEnabled", true).SetValue(null, _saveEnabled);
            var blocked = (HashSet<GameDataSaveCoordinator>)Field(typeof(GameDataSaveCoordinator), "BlockedCoordinators", true).GetValue(null);
            blocked.Clear();
            foreach (GameDataSaveCoordinator original in _blockedCoordinators) blocked.Add(original);
            _assets.Clear();
        }
    }

    [Test]
    public void ConfirmedGuestAndGoogleSignup_InitializesOnceThenEntersOnlyAfterRequeryAndRestore()
    {
        foreach (GameDataAuthenticationKind kind in new[] { GameDataAuthenticationKind.Guest, GameDataAuthenticationKind.Google })
        {
            var fixture = Create();
            string account = Unique("signup-" + kind), row = Unique("row");
            var attempt = Authenticate(fixture, kind, "201", account);
            int entries = 0;
            GameDataRestoreResult result = null;
            fixture.Manager.GetAndRestoreGameDataAsync((query, restored) =>
            { result = restored; if (restored.CanContinueLegacy) entries++; });
            Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
            Assert.That(fixture.Transport.Gets[0].Account, Is.EqualTo(account));
            fixture.Transport.Gets[0].Reply(Bro("200", Rows()));
            Assert.That(fixture.Transport.Inserts.Count, Is.EqualTo(1));
            Assert.That(fixture.Transport.InitialCalls, Is.EqualTo(1));
            Assert.That(fixture.Transport.RestoreCalls, Is.Zero);
            Assert.That(entries, Is.Zero);
            Assert.That(fixture.Manager.CanSaveLegacyGameData, Is.False);
            Assert.That(fixture.Manager.RestoredGameData, Is.Null);
            JObject initial = JObject.Parse(fixture.Transport.Inserts[0].Values.GetJson());
            Assert.That(initial.Count, Is.EqualTo(64));
            Assert.That((int)initial["Dia"], Is.Zero);
            Assert.That((long)initial["Money"], Is.Zero);
            Assert.That((int)initial["SkinToken"], Is.Zero);
            Assert.That((int)initial["TotalUseGachaMachineCount"], Is.Zero);
            Assert.That(initial[GameDataRestoreContext.StaffAccountFieldName], Is.Null);
            Assert.That((string)initial["LastAccessTime"], Is.EqualTo(FakeBackend.FixedTime.ToString()));
            foreach (JProperty property in initial.Properties())
            {
                if (property.Value.Type == JTokenType.Integer) Assert.That((long)property.Value, Is.Zero, property.Name);
                if (property.Value.Type == JTokenType.Boolean) Assert.That((bool)property.Value, Is.False, property.Name);
                if (property.Value is JArray list) Assert.That(list, Is.Empty, property.Name);
            }
            // Return the actual captured initial values in SDK AttributeValue form, not a Dia-only stand-in.
            JObject initializedRow = Row(account, row, false, 0);
            foreach (JProperty property in initial.Properties())
            {
                switch (property.Value.Type)
                {
                    case JTokenType.Integer:
                        initializedRow[property.Name] = new JObject { ["N"] = property.Value.ToString(Formatting.None) };
                        break;
                    case JTokenType.Boolean:
                        initializedRow[property.Name] = new JObject { ["BOOL"] = (bool)property.Value };
                        break;
                    case JTokenType.String:
                        initializedRow[property.Name] = new JObject { ["S"] = (string)property.Value };
                        break;
                    case JTokenType.Array:
                        initializedRow[property.Name] = new JObject { ["L"] = new JArray() }; // Initial lists were verified empty above.
                        break;
                    default: Assert.Fail("Unexpected initial field type: " + property.Name); break;
                }
            }
            fixture.Transport.Restoration = bro =>
            {
                // Both FlattenRows and this parser are memory-only; do not call UserInfo.TryLoadGameData or ServerTime.
                var parsed = new LoadUserData(bro.FlattenRows());
                Assert.That(parsed.IsValid, Is.True);
                Assert.That(parsed.Dia, Is.Zero);
                Assert.That(parsed.Money, Is.Zero);
                Assert.That(parsed.SkinToken, Is.Zero);
                Assert.That(parsed.Score, Is.Zero);
                Assert.That(parsed.TotalUseGachaMachineCount, Is.Zero);
                Assert.That(parsed.IsFirstTutorialClear, Is.False);
                Assert.That(parsed.UserId, Is.Empty);
                Assert.That(parsed.FirstAccessTime, Is.Empty);
                Assert.That(parsed.LastAttendanceTime, Is.Empty);
                Assert.That(parsed.LastAccessTime, Is.EqualTo(FakeBackend.FixedTime.ToString()));
                Assert.That(parsed.GiveRecipeLevelDic, Is.Empty);
                Assert.That(parsed.GiveGachaItemCountDic, Is.Empty);
                Assert.That(parsed.GiveStaffSkinSet, Is.Empty);
                Assert.That(parsed.TimeDataDic, Is.Empty);
                return parsed.IsValid;
            };
            Assert.That(fixture.Transport.LastInitialValues.GetJson(), Is.EqualTo(fixture.Transport.InitialJson));
            fixture.Transport.Gets[0].Reply(Bro("200", Rows()));
            Assert.That(fixture.Manager.NotifyFederationLoginSuccess(attempt, Bro("201", "")), Is.False);
            Assert.That(fixture.Transport.Inserts.Count, Is.EqualTo(1));
            fixture.Transport.Inserts[0].Reply(Bro("201", ""));
            Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(2));
            Assert.That(entries, Is.Zero, "Insert success alone is not restored entry readiness");
            fixture.Transport.Inserts[0].Reply(Bro("201", ""));
            fixture.Transport.Gets[1].Reply(Bro("200", Rows(initializedRow)));
            fixture.Transport.Gets[1].Reply(Bro("200", Rows(initializedRow)));
            Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(entries, Is.EqualTo(1));
            Assert.That(fixture.Transport.RestoreCalls, Is.EqualTo(1));
            Assert.That(fixture.Transport.Inserts.Count, Is.EqualTo(1));
            Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(2));
            Assert.That(fixture.Manager.RestoredGameData, Is.Null, "First generation must not invent StaffAccount");
            Assert.That(fixture.Manager.CanSaveLegacyGameData, Is.True);
            Assert.That(fixture.Manager.IsLoaded, Is.True, "Successful legacy restoration must preserve existing loading compatibility");
        }
    }

    [Test]
    public void UnprovenOrUnknownCreation_NeverFallsBackToRepeatedInsertOrEntryAcrossAuthenticationChanges()
    {
        foreach (var scenario in new[]
        {
            new { Kind = GameDataAuthenticationKind.Guest, Status = "200", AlreadyLoggedIn = false, Policy = true },
            new { Kind = GameDataAuthenticationKind.Other, Status = "201", AlreadyLoggedIn = false, Policy = true },
            new { Kind = GameDataAuthenticationKind.Google, Status = "201", AlreadyLoggedIn = true, Policy = true },
            new { Kind = GameDataAuthenticationKind.Guest, Status = "201", AlreadyLoggedIn = false, Policy = false }
        })
        {
            var fixture = Create();
            Authenticate(fixture, scenario.Kind, scenario.Status, Unique("unproven"), scenario.AlreadyLoggedIn);
            if (!scenario.Policy) Field(typeof(BackendManager), "_gameDataInitializationPolicy").SetValue(fixture.Manager, null);
            int entries = 0;
            fixture.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) entries++; });
            fixture.Transport.Gets[0].Reply(Bro("200", Rows()));
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.RowMissing));
            Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
            Assert.That(entries, Is.Zero);
            AssertAllLegacyWritesBlocked(fixture, Unique("missing-row"));
        }
        foreach (Func<string, string> invalidResponse in new Func<string, string>[]
        {
            account => "{broken",
            account => Rows(Row(account, "one", false), Row(account, "two", false)),
            account => "{\"rows\":[],\"firstKey\":{\"next\":\"page\"}}"
        })
        {
            var fixture = Create();
            string account = Unique("invalid-new");
            Authenticate(fixture, GameDataAuthenticationKind.Guest, "201", account);
            int entries = 0;
            fixture.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) entries++; });
            fixture.Transport.Gets[0].Reply(Bro("200", invalidResponse(account)));
            Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
            Assert.That(fixture.Transport.InitialCalls, Is.Zero);
            Assert.That(entries, Is.Zero);
        }
        foreach (BackendReturnObject response in new[] { null, Bro("400", "", "HttpRequestException"), Bro("500", "", "InternalServerError") })
        {
            var fixture = Create();
            string account = Unique("unknown-insert"), row = Unique("row");
            Authenticate(fixture, GameDataAuthenticationKind.Google, "201", account);
            int entries = 0;
            fixture.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) entries++; });
            fixture.Transport.Gets[0].Reply(Bro("200", Rows()));
            fixture.Transport.Inserts[0].Reply(response);
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.InitialCreationIndeterminate));
            fixture.Transport.Inserts[0].Reply(Bro("201", ""));
            Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
            fixture.Manager.LogOut();
            Authenticate(fixture, GameDataAuthenticationKind.Google, "201", account);
            fixture.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) entries++; });
            fixture.Transport.Gets[1].Reply(Bro("200", Rows(Row(account, row, false))));
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.InitialCreationIndeterminate));
            Assert.That(entries, Is.Zero);
            Assert.That(fixture.Transport.RestoreCalls, Is.Zero);
            Assert.That(fixture.Transport.Inserts.Count, Is.EqualTo(1));
            AssertAllLegacyWritesBlocked(fixture, row);
        }

        var stale = Create();
        string staleAccount = Unique("stale-insert"), staleRow = Unique("row");
        Authenticate(stale, GameDataAuthenticationKind.Guest, "201", staleAccount);
        int staleEntries = 0;
        stale.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) staleEntries++; });
        stale.Transport.Gets[0].Reply(Bro("200", Rows()));
        stale.Manager.LogOut();
        Authenticate(stale, GameDataAuthenticationKind.Guest, "200", staleAccount);
        stale.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) staleEntries++; });
        stale.Transport.Inserts[0].Reply(Bro("201", ""));
        stale.Transport.Gets[1].Reply(Bro("200", Rows(Row(staleAccount, staleRow, false))));
        Assert.That(stale.Transport.Inserts.Count, Is.EqualTo(1));
        Assert.That(stale.Transport.Gets.Count, Is.EqualTo(2));
        Assert.That(staleEntries, Is.Zero);

        var authentication = Create();
        var oldAttempt = authentication.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Google);
        Assert.That(authentication.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Google), Is.Null,
            "Do not overlap a second SDK authentication with an outstanding first authentication");
        authentication.Manager.LogOut();
        Assert.That(authentication.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Google), Is.Null);
        Assert.That(authentication.Manager.ObserveGameDataAuthenticationResponse(oldAttempt), Is.False,
            "An old authentication response may clear its in-flight marker but cannot authorize the new session");
        var currentAttempt = authentication.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Google);
        Assert.That(currentAttempt, Is.Not.Null);
        authentication.Transport.NativeLoggedIn = authentication.Transport.LoggedIn = true;
        authentication.Transport.AccountInDate = Unique("current-auth");
        Assert.That(authentication.Manager.NotifyFederationLoginSuccess(oldAttempt, Bro("201", "")), Is.False);
        Assert.That(authentication.Manager.NotifyFederationLoginSuccess(currentAttempt, Bro("200", "")), Is.True);
        var blank = Create();
        var blankAttempt = blank.Manager.BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
        blank.Transport.NativeLoggedIn = blank.Transport.LoggedIn = true;
        blank.Transport.AccountInDate = "";
        Assert.That(blank.Manager.NotifyFederationLoginSuccess(blankAttempt, Bro("201", "")), Is.False);
        Assert.That(blank.Transport.Inserts.Count, Is.Zero);
    }

    [Test]
    public void LegacyAccountWithoutCommonStaff_RestoresAndUpdatesKnownRowWithoutGetInsertOrRetryFallback()
    {
        var fixture = Create();
        string account = Unique("legacy"), row = Unique("row");
        Authenticate(fixture, GameDataAuthenticationKind.Guest, "200", account);
        int entries = Restore(fixture, account, row, false);
        Assert.That(entries, Is.EqualTo(1));
        Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
        Assert.That(fixture.Manager.CanSaveLegacyGameData, Is.True);
        Assert.That(fixture.Manager.RestoredGameData, Is.Null);
        var values = Values();
        string original = values.GetJson();
        int success = 0, failure = 0;
        fixture.Manager.SaveGameDataAsync("GameData", values, _ => success++, _ => failure++);
        fixture.Manager.UpdateGameDataAsync("GameData", row, values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.SaveGameData("GameData", values), Is.False,
            "A queued legacy bool request is not confirmed success");
        Assert.That(fixture.Manager.UpdateGameData("GameData", row, values), Is.False);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
        Assert.That(success, Is.Zero);
        for (int i = 0; i < 4; i++)
        {
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(i + 1), "Only the current request may have reached SDK");
            var update = fixture.Transport.Updates[i];
            Assert.That(update.Target.AccountInDate, Is.EqualTo(account));
            Assert.That(update.Target.RowInDate, Is.EqualTo(row));
            Assert.That(update.Values.GetJson(), Is.EqualTo(original));
            Assert.That(update.Values, Is.Not.SameAs(values));
            update.Reply(Bro("204", ""));
            update.Reply(Bro("204", ""));
        }
        Assert.That(values.GetJson(), Is.EqualTo(original));
        Assert.That(success, Is.EqualTo(2));
        Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        Assert.That(fixture.Transport.SyncUpdateCalls, Is.Zero, "The old synchronous SDK path must not be used");
        fixture.Manager.UpdateGameDataAsync("GameData", "wrong-row", values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.UpdateGameData("GameData", "wrong-row", values), Is.False);
        fixture.Manager.InsertGameDataAsync("GameData", values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.InsertGameData("GameData", values), Is.False);
        Assert.That(failure, Is.EqualTo(2));
        fixture.Manager.SaveGameDataAsync("GameData", values, _ => success++, _ => failure++);
        fixture.Transport.Updates[4].Reply(Bro("400", "", "HttpRequestException"));
        fixture.Transport.Updates[4].Reply(Bro("400", "", "HttpRequestException"));
        Assert.That(failure, Is.EqualTo(3));
        Assert.That(success, Is.EqualTo(2));
        Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(5));
        // A later definitive response for this same submitted request is not a resend or a new write.
        fixture.Transport.Updates[4].Reply(Bro("204", ""));
        Assert.That(success, Is.EqualTo(3));
        Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        fixture.Transport.Updates[4].Reply(Bro("204", ""));
        fixture.Transport.Updates[4].Reply(Bro("400", "", "late-after-confirmation"));
        Assert.That(success, Is.EqualTo(3));
        Assert.That(failure, Is.EqualTo(3));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(5), "No automatic or captured popup retry may resubmit");
        Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
        Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
    }

    [Test]
    public void FailedPartialRestoreAndOldCallbacks_BlockAllLegacyWritersAndDownstreamEntry()
    {
        var failed = Create();
        string failedAccount = Unique("partial-restore"), failedRow = Unique("row");
        Authenticate(failed, GameDataAuthenticationKind.Guest, "200", failedAccount);
        int partialFixtureState = 0;
        failed.Transport.Restoration = _ => { partialFixtureState++; throw new InvalidOperationException("fake partial restore"); };
        Assert.That(Restore(failed, failedAccount, failedRow, false), Is.Zero);
        Assert.That(partialFixtureState, Is.EqualTo(1), "The test does not claim rollback of a partially applied legacy restore");
        Assert.That(failed.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.RestoreFailed));
        AssertAllLegacyWritesBlocked(failed, failedRow);
        Field(typeof(BackendManager), "_isLoaded").SetValue(failed.Manager, true); // Mimics the unrelated table-success flag only.
        AssertAllLegacyWritesBlocked(failed, failedRow);
        Assert.That(failed.Transport.Updates.Count, Is.Zero);
        Assert.That(failed.Transport.Inserts.Count, Is.Zero);

        var query = Create();
        string account = Unique("query-generation"), row = Unique("row");
        Authenticate(query, GameDataAuthenticationKind.Guest, "200", account);
        int entries = 0, writeSuccess = 0;
        Action<GameDataRestoreQuery, GameDataRestoreResult> enter = (token, result) => { if (result.CanContinueLegacy) entries++; };
        query.Manager.GetAndRestoreGameDataAsync(enter);
        query.Manager.GetAndRestoreGameDataAsync(enter);
        query.Transport.Gets[0].Reply(Bro("200", Rows(Row(account, row, false))));
        Assert.That(entries, Is.Zero);
        Assert.That(query.Transport.RestoreCalls, Is.Zero);
        query.Transport.Gets[1].Reply(Bro("200", Rows(Row(account, row, false))));
        Assert.That(entries, Is.EqualTo(1));
        query.Manager.SaveGameDataAsync("GameData", Values(), _ => writeSuccess++);
        query.Manager.GetAndRestoreGameDataAsync(enter);
        query.Transport.Updates[0].Reply(Bro("204", ""));
        Assert.That(writeSuccess, Is.Zero, "An old save success must not start Stage/payment follow-up after a new query");
        query.Transport.Gets[2].Reply(Bro("400", "", "HttpRequestException"));
        Assert.That(query.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.QueryFailed));
        AssertAllLegacyWritesBlocked(query, row);

        foreach (bool differentAccount in new[] { false, true })
        {
            var fixture = Create();
            string firstAccount = Unique("session"), firstRow = Unique("row");
            Authenticate(fixture, GameDataAuthenticationKind.Google, "200", firstAccount);
            Assert.That(Restore(fixture, firstAccount, firstRow, false), Is.EqualTo(1));
            int callbacks = 0;
            fixture.Manager.SaveGameDataAsync("GameData", Values(), _ => callbacks++);
            fixture.Manager.GetAndRestoreGameDataAsync((token, result) => { if (result.CanContinueLegacy) callbacks++; });
            fixture.Manager.LogOut();
            AssertAllLegacyWritesBlocked(fixture, firstRow);
            string nextAccount = differentAccount ? Unique("different") : firstAccount;
            Authenticate(fixture, GameDataAuthenticationKind.Google, "200", nextAccount);
            fixture.Manager.GetAndRestoreGameDataAsync((token, result) => { if (result.CanContinueLegacy) callbacks++; });
            fixture.Transport.Updates[0].Reply(Bro("204", ""));
            fixture.Transport.Gets[1].Reply(Bro("200", Rows(Row(firstAccount, firstRow, false))));
            Assert.That(callbacks, Is.Zero);
            fixture.Transport.Gets[2].Reply(Bro("200", Rows(Row(nextAccount, firstRow, false))));
            Assert.That(callbacks, Is.EqualTo(1));
            fixture.Transport.Updates[0].Reply(Bro("400", "", "HttpRequestException"));
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExistingCoordinatorLocks_BlockActualLegacyEntryAfterRequeryAndSameAccountRelogin()
    {
        foreach (bool localFailure in new[] { false, true })
        {
            var fixture = Create();
            string account = Unique("coordinator-lock"), row = Unique("row");
            Authenticate(fixture, GameDataAuthenticationKind.Guest, "200", account);
            Assert.That(Restore(fixture, account, row, true), Is.EqualTo(1));
            var target = fixture.Manager.RestoredGameData.Target;
            var mockWrite = new FakeSingleUpdate();
            var policy = FakePolicy();
            var updater = new GameDataSingleUpdate(() => new GameDataSaveReadiness(true, true, true, true,
                account, target, initializationPolicy: policy), mockWrite);
            var coordinator = new GameDataSaveCoordinator(target, updater, Values);
            var identity = new GameDataSaveIdentity("blocked-save", target);
            Assert.That(coordinator.TryStartPurchase(identity, Values, _ =>
            { if (localFailure) throw new InvalidOperationException("fake local apply failure"); }, out string error), Is.True, error);
            mockWrite.Reply(identity, localFailure ? new GameDataRawResponse(true, "204", "", "")
                : new GameDataRawResponse(false, "400", "HttpRequestException", "mock unknown"));
            var expected = localFailure ? GameDataSaveCoordinatorState.LocalCompletionFailed : GameDataSaveCoordinatorState.Indeterminate;
            Assert.That(coordinator.State, Is.EqualTo(expected));
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(target), Is.True);
            AssertAllLegacyWritesBlocked(fixture, row);
            Assert.That(fixture.Manager.CreateGameDataSingleUpdate().CanSend(identity, out _), Is.False);
            Assert.That(Restore(fixture, account, row, true), Is.EqualTo(1));
            fixture.Manager.LogOut();
            Authenticate(fixture, GameDataAuthenticationKind.Guest, "200", account);
            Assert.That(Restore(fixture, account, row, true), Is.EqualTo(1));
            Assert.That(coordinator.State, Is.EqualTo(expected));
            Assert.That(coordinator.CanStartPurchase, Is.False);
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(target), Is.True);
            AssertAllLegacyWritesBlocked(fixture, row);
            Assert.That(fixture.Manager.CreateGameDataSingleUpdate().CanSend(identity, out _), Is.False);
            Assert.That(mockWrite.Count, Is.EqualTo(1));
            Assert.That(fixture.Transport.Updates.Count, Is.Zero);
            Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
        }
    }

    [Test]
    public void SharedAutosaveFlow_MergesLatestIntentAndPreservesPartialUpdatesAndPerBatchCompletion()
    {
        var fixture = RestoredFixture(false, out string account, out string row);
        int memoryDiamonds = 110;
        var events = new List<string>();
        fixture.Transport.LatestFactory = () =>
        {
            events.Add("factory:" + memoryDiamonds);
            var latest = new Param(); latest.Add("Dia", memoryDiamonds); return latest;
        };
        var first = fixture.Manager.RequestGameDataAutosave(_ =>
        {
            Assert.That(memoryDiamonds, Is.EqualTo(135), "Old transmitted values must not roll back current memory");
            events.Add("first-confirmed"); memoryDiamonds = 136;
        });
        var mergedA = fixture.Manager.RequestGameDataAutosave(_ => events.Add("merged-A"));
        var mergedB = fixture.Manager.RequestGameDataAutosave(_ => events.Add("merged-B"));
        var partial = new Param(); partial.Add("UserId", "saved-nickname");
        string partialBefore = partial.GetJson();
        var nickname = fixture.Manager.RequestGameDataSave(partial, row, _ =>
        { events.Add("nickname-confirmed"); memoryDiamonds = 150; });
        ((System.Collections.SortedList)partial)["UserId"] = "caller-changed-after-acceptance";
        var tail = fixture.Manager.RequestGameDataAutosave(_ => events.Add("tail-confirmed"));
        Assert.That(first.Accepted, Is.True);
        Assert.That(mergedA.Accepted && mergedB.Accepted && nickname.Accepted && tail.Accepted, Is.True);
        Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
        Assert.That(mergedA.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That(fixture.Transport.LatestCalls, Is.EqualTo(1));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
        CollectionAssert.AreEqual(new[] { "factory:110" }, events);
        memoryDiamonds = 135; // Memory fixture only, never UserInfo.

        var rawSuccess = Bro("204", "", "", "original callback message");
        fixture.Transport.Updates[0].Reply(rawSuccess);
        Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(first.Receipt.RawResponse.Message, Is.EqualTo("original callback message"));
        Assert.That(mergedA.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
        Assert.That(nickname.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That((int)JObject.Parse(fixture.Transport.Updates[1].Values.GetJson())["Dia"], Is.EqualTo(136));
        CollectionAssert.AreEqual(new[] { "factory:110", "first-confirmed", "factory:136" }, events);
        fixture.Transport.Updates[0].Reply(Bro("500", "", "late-old-error"));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(2));
        fixture.Transport.Updates[1].Reply(Bro("204", ""));
        Assert.That(mergedA.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(mergedB.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(fixture.Transport.Updates[2].Values.GetJson(), Is.EqualTo(partialBefore));
        Assert.That(fixture.Transport.Updates[2].Values, Is.Not.SameAs(partial));
        Assert.That(fixture.Transport.LatestCalls, Is.EqualTo(2), "A partial-field request is not a full autosave");
        fixture.Transport.Updates[2].Reply(Bro("204", ""));
        Assert.That((int)JObject.Parse(fixture.Transport.Updates[3].Values.GetJson())["Dia"], Is.EqualTo(150));
        fixture.Transport.Updates[3].Reply(Bro("204", ""));
        foreach (var write in fixture.Transport.Updates.ToArray()) write.Reply(Bro("204", ""));
        CollectionAssert.AreEqual(new[] { "factory:110", "first-confirmed", "factory:136", "merged-A", "merged-B",
            "nickname-confirmed", "factory:150", "tail-confirmed" }, events);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(4));
        Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        Assert.That(fixture.Transport.LatestCalls, Is.EqualTo(3));
        Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
        Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
        Assert.That(fixture.Transport.SyncUpdateCalls, Is.Zero);
        Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
        foreach (var write in fixture.Transport.Updates)
        {
            Assert.That(write.Target.AccountInDate, Is.EqualTo(account));
            Assert.That(write.Target.RowInDate, Is.EqualTo(row));
            Assert.That(JObject.Parse(write.Values.GetJson())[GameDataRestoreContext.StaffAccountFieldName], Is.Null);
        }

        var guarded = RestoredFixture(false, out _, out string guardedRow);
        int guardedSuccess = 0, guardedFailure = 0, independentCompletion = 0;
        guarded.Manager.RequestGameDataSave(Values(), guardedRow);
        var gameplaySave = guarded.Manager.RequestGameDataAutosave(_ => guardedSuccess++, _ => guardedFailure++,
            requireGameplay: true);
        var independentValues = new Param(); independentValues.Add("UserId", "independent-after-guarded-save");
        var independent = guarded.Manager.RequestGameDataSave(independentValues, guardedRow, _ => independentCompletion++);
        Assert.That(gameplaySave.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That(independent.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        guarded.Transport.GameplaySaveAllowed = false; // Fake tutorial condition changes while its request is queued.
        guarded.Transport.Updates[0].Reply(Bro("204", ""));
        Assert.That(gameplaySave.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
        Assert.That(gameplaySave.Receipt.SendStarted, Is.False);
        Assert.That(guardedSuccess, Is.Zero);
        Assert.That(guardedFailure, Is.EqualTo(1));
        Assert.That(guarded.Transport.LatestCalls, Is.Zero, "Recheck gameplay permission before building queued latest data");
        Assert.That(guarded.Transport.Updates.Count, Is.EqualTo(2),
            "A rejected autosave must not strand a previously accepted independent partial update behind it");
        Assert.That(independent.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
        Assert.That(independentCompletion, Is.Zero);
        Assert.That(guarded.Transport.Updates[1].Values.GetJson(), Is.EqualTo(independentValues.GetJson()));
        guarded.Transport.Updates[1].Reply(Bro("204", ""));
        Assert.That(independent.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(independentCompletion, Is.EqualTo(1));
        Assert.That(guarded.Transport.Updates.Count, Is.EqualTo(2), "No additional request should be needed to pump the accepted update");
        Assert.That(guarded.Transport.MaxInFlight, Is.EqualTo(1));
    }

    [TestCase(true, TestName = "SharedAutosaveFlow_SameFieldPatchKeepsAThenPThenBFinalValue")]
    [TestCase(false, TestName = "SharedAutosaveFlow_DifferentFieldPatchPreservesLatestValuesAndOmittedFields")]
    public void SharedAutosaveFlow_UpdateV2MemoryRowPreservesOrderAndFinalValues(bool sameField)
    {
        var fixture = RestoredFixture(true, out _, out string row);
        const string originalCommon = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}";
        fixture.Transport.StoredRow = new JObject
        {
            ["Dia"] = 90, ["UserId"] = "old-name",
            [GameDataRestoreContext.StaffAccountFieldName] = originalCommon
        };
        int memoryDiamonds = 110;
        string memoryUserId = "old-name";
        fixture.Transport.LatestFactory = () =>
        {
            // Same source contract as GetSaveUserData: current Dia/UserId, no StaffAccount field.
            var latest = new Param();
            latest.Add("Dia", memoryDiamonds); latest.Add("UserId", memoryUserId);
            return latest;
        };
        var callbacks = new List<string>();
        var leader = fixture.Manager.RequestGameDataAutosave(_ => callbacks.Add("S"));
        var autoA = fixture.Manager.RequestGameDataAutosave(_ => callbacks.Add("A"));
        var partial = new Param();
        if (sameField)
        {
            memoryDiamonds = 125;
            partial.Add("Dia", 125);
        }
        else
        {
            // FirstLoadingScene likewise changes UserInfo.SetUserId before requesting a save.
            // This is an explicit compatibility-API patch test, not a new runtime partial writer.
            memoryUserId = "new-name";
            partial.Add("UserId", "new-name");
        }
        string partialInput = partial.GetJson();
        fixture.Manager.UpdateGameDataAsync("GameData", row, partial, _ => callbacks.Add("P"));
        memoryDiamonds = 150; // Later progress is authoritative for the pending latest-value autosaves.
        var autoB = fixture.Manager.RequestGameDataAutosave(_ => callbacks.Add("B"));

        Assert.That(leader.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
        Assert.That(autoA.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That(autoB.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That(autoA.Identity.RequestId, Is.Not.EqualTo(autoB.Identity.RequestId), "B must not merge across P into A");
        Assert.That(callbacks, Is.Empty);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
        Assert.That(fixture.Transport.AppliedUpdates, Is.Zero);
        Assert.That((int)fixture.Transport.StoredRow["Dia"], Is.EqualTo(90));

        // Independent expected values from the caller's explicit patch and later current-memory value.
        // Wrong A+B coalescing would leave Dia=125 after P in the same-field case, not the intended 150.
        var expectedDiamonds = sameField ? new[] { 110, 150, 125, 150 } : new[] { 110, 150, 150, 150 };
        var expectedNames = sameField ? new[] { "old-name", "old-name", "old-name", "old-name" }
            : new[] { "old-name", "new-name", "new-name", "new-name" };
        var expectedCallbacks = new[] { "S", "A", "P", "B" };
        for (int i = 0; i < 4; i++)
        {
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(i + 1));
            CollectionAssert.AreEqual(expectedCallbacks.Take(i), callbacks, "Only this request's own success may notify it");
            var sent = JObject.Parse(fixture.Transport.Updates[i].Values.GetJson());
            if (i == 2)
            {
                Assert.That(sent.Count, Is.EqualTo(1), "P remains a partial update, never a generated whole snapshot");
                Assert.That(fixture.Transport.Updates[i].Values.GetJson(), Is.EqualTo(partialInput));
            }
            else
            {
                Assert.That((int)sent["Dia"], Is.EqualTo(i == 0 ? 110 : 150));
                Assert.That((string)sent["UserId"], Is.EqualTo(expectedNames[i]));
                Assert.That(sent.Property(GameDataRestoreContext.StaffAccountFieldName), Is.Null);
            }
            fixture.Transport.Updates[i].Reply(Bro("204", ""));
            Assert.That(fixture.Transport.AppliedUpdates, Is.EqualTo(i + 1));
            Assert.That((int)fixture.Transport.StoredRow["Dia"], Is.EqualTo(expectedDiamonds[i]));
            Assert.That((string)fixture.Transport.StoredRow["UserId"], Is.EqualTo(expectedNames[i]));
            Assert.That((string)fixture.Transport.StoredRow[GameDataRestoreContext.StaffAccountFieldName], Is.EqualTo(originalCommon));
            CollectionAssert.AreEqual(expectedCallbacks.Take(i + 1), callbacks);
            Assert.That(memoryDiamonds, Is.EqualTo(150), "Success must not roll back the source to an old transmitted snapshot");
            Assert.That(memoryUserId, Is.EqualTo(sameField ? "old-name" : "new-name"));
        }
        var expectedFinalRow = new JObject
        {
            ["Dia"] = 150, ["UserId"] = sameField ? "old-name" : "new-name",
            [GameDataRestoreContext.StaffAccountFieldName] = originalCommon
        };
        Assert.That(JToken.DeepEquals(fixture.Transport.StoredRow, expectedFinalRow), Is.True);
        foreach (var write in fixture.Transport.Updates.ToArray()) write.Reply(Bro("204", ""));
        Assert.That(JToken.DeepEquals(fixture.Transport.StoredRow, expectedFinalRow), Is.True);
        CollectionAssert.AreEqual(expectedCallbacks, callbacks);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(4));
        Assert.That(fixture.Transport.AppliedUpdates, Is.EqualTo(4));
        Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        Assert.That(fixture.Transport.LatestCalls, Is.EqualTo(3));
        Assert.That(fixture.Transport.SyncUpdateCalls, Is.Zero);
        Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
        Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
        Assert.That(partial.GetJson(), Is.EqualTo(partialInput));
        Assert.That(autoA.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(autoB.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
    }

    [Test]
    public void SaveSession_BeforeSendInvalidationNeverRunsOldFactoriesOrRevalidatesOldUpdater()
    {
        var fixture = RestoredFixture(true, out string account, out string row);
        var oldUpdater = fixture.Manager.CreateGameDataSingleUpdate();
        var target = fixture.Manager.RestoredGameData.Target;
        int oldFactories = 0, completed = 0;
        var oldCoordinator = new GameDataSaveCoordinator(target, oldUpdater, () =>
        { oldFactories++; return Values(); });
        fixture.Manager.GetAndRestoreGameDataAsync((_, __) => { });
        Assert.That(oldCoordinator.RequestAutosave(_ => completed++).Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
        fixture.Transport.Gets.Last().Reply(Bro("200", Rows(Row(account, row, true))));
        Assert.That(oldUpdater.CanSend(new GameDataSaveIdentity("old-generation", target), out _), Is.False);
        Assert.That(oldCoordinator.RequestAutosave(_ => completed++).Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
        Assert.That(oldFactories, Is.Zero);
        Assert.That(completed, Is.Zero);
        Assert.That(fixture.Transport.Updates.Count, Is.Zero);

        fixture.Transport.LatestFactory = () =>
        {
            fixture.Manager.InvalidateGameDataRestore();
            return Values();
        };
        var invalidatedDuringFactory = fixture.Manager.RequestGameDataAutosave(_ => completed++);
        Assert.That(invalidatedDuringFactory.Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
        Assert.That(invalidatedDuringFactory.Receipt == null || !invalidatedDuringFactory.Receipt.SendStarted, Is.True);
        Assert.That(fixture.Transport.LatestCalls, Is.EqualTo(1));
        Assert.That(fixture.Transport.Updates.Count, Is.Zero);
        Assert.That(completed, Is.Zero);
        Assert.That(fixture.Manager.RestoredGameData, Is.Null);
        // Even an identical account/row restoration cannot revive this already invalidated old updater.
        Assert.That(Restore(fixture, account, row, true), Is.EqualTo(1));
        Assert.That(oldCoordinator.RequestAutosave(_ => completed++).Status, Is.EqualTo(GameDataSaveRequestStatus.RejectedBeforeSend));
        Assert.That(oldFactories, Is.Zero);
    }

    [Test]
    public void SaveSession_AfterSendRequeryOrReloginKeepsOwnershipAndRejectsOldApplicationAndPumping()
    {
        foreach (string transition in new[] { "requery", "same-account", "different-account" })
        {
            var fixture = RestoredFixture(true, out string account, out string row);
            int completions = 0, queuedCompletions = 0, outsiderFactories = 0;
            var first = fixture.Manager.RequestGameDataSave(Values(), row, _ => completions++);
            var queued = fixture.Manager.RequestGameDataAutosave(_ => queuedCompletions++);
            var owner = fixture.Manager.CurrentGameDataSaveCoordinator;
            var identity = owner.CurrentIdentity;
            string frozen = owner.CurrentPayload.Json;
            var outsider = new GameDataSaveCoordinator(identity.Target, fixture.Manager.CreateGameDataSingleUpdate(), () =>
            { outsiderFactories++; return Values(); });
            Assert.That(outsider.RequestAutosave(_ => completions++).Accepted, Is.False,
                "A separate coordinator may not bypass another owner's in-flight request");
            Assert.That(outsiderFactories, Is.Zero);
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(identity.Target), Is.True);

            string nextAccount = account;
            if (transition != "requery")
            {
                fixture.Manager.LogOut();
                if (transition == "different-account") nextAccount = Unique("next-account");
                Authenticate(fixture, GameDataAuthenticationKind.Google, "200", nextAccount);
            }
            Assert.That(Restore(fixture, nextAccount, row, true), Is.EqualTo(1));
            Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.InvalidatedAfterSend));
            Assert.That(owner.CurrentIdentity, Is.SameAs(identity));
            Assert.That(owner.CurrentPayload.Json, Is.EqualTo(frozen));
            fixture.Transport.Updates[0].Reply(Bro("204", "", "", "late old success"));
            fixture.Transport.Updates[0].Reply(Bro("204", ""));
            Assert.That(completions, Is.Zero);
            Assert.That(queuedCompletions, Is.Zero);
            Assert.That(queued.Status, Is.Not.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
            Assert.That(fixture.Transport.LatestCalls, Is.Zero, "An old queued factory must not read the new session");
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
            Assert.That(owner.State, Is.EqualTo(GameDataSaveCoordinatorState.InvalidatedAfterSend));
            Assert.That(owner.LastReceipt.RawResponse.Message, Is.EqualTo("late old success"));
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(identity.Target), Is.True);
            Assert.That(outsider.RequestAutosave().Accepted, Is.False);
            if (transition != "different-account")
            {
                Assert.That(fixture.Manager.RequestGameDataAutosave().Accepted, Is.False);
                Assert.That(fixture.Transport.LatestCalls, Is.Zero);
            }
            Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        }

        foreach (bool explicitAlwaysTrue in new[] { false, true })
        {
            var externalFixture = RestoredFixture(true, out string account, out string row);
            var target = externalFixture.Manager.RestoredGameData.Target;
            var updater = externalFixture.Manager.CreateGameDataSingleUpdate();
            int applied = 0, latestCalls = 0;
            var external = new GameDataSaveCoordinator(target, updater, () =>
            { latestCalls++; return Values(); },
                isSessionCurrent: explicitAlwaysTrue ? (Func<bool>)(() => true) : null);
            var request = external.EnqueueSave(Values, _ => applied++);
            external.RequestAutosave(_ => applied++);
            var identity = external.CurrentIdentity;
            string frozen = external.CurrentPayload.Json;
            Assert.That(externalFixture.Transport.Updates.Count, Is.EqualTo(1),
                "The public updater and independently constructed coordinator use the same fake SDK boundary");
            Assert.That(request.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That(Restore(externalFixture, account, row, true), Is.EqualTo(1));
            Assert.That(updater.CanSend(identity, out _), Is.False,
                "A restored replacement row must not revive an updater bound to the previous query");
            Assert.That(externalFixture.Manager.RequestGameDataAutosave().Accepted, Is.False,
                "The external in-flight owner still blocks newly created manager save sessions");
            externalFixture.Transport.Updates[0].Reply(Bro("204", "", "", "external old success"));
            externalFixture.Transport.Updates[0].Reply(Bro("204", ""));
            Assert.That(request.Status, Is.EqualTo(GameDataSaveRequestStatus.InvalidatedAfterSend));
            Assert.That(external.State, Is.EqualTo(GameDataSaveCoordinatorState.InvalidatedAfterSend));
            Assert.That(external.CurrentIdentity, Is.SameAs(identity));
            Assert.That(external.CurrentPayload.Json, Is.EqualTo(frozen));
            Assert.That(external.LastReceipt.RawResponse.Message, Is.EqualTo("external old success"));
            Assert.That(applied, Is.Zero, "An omitted or always-true additional predicate cannot override frozen updater evidence");
            Assert.That(latestCalls, Is.Zero);
            Assert.That(externalFixture.Transport.LatestCalls, Is.Zero);
            Assert.That(externalFixture.Transport.Updates.Count, Is.EqualTo(1));
            Assert.That(externalFixture.Transport.MaxInFlight, Is.EqualTo(1));
            Assert.That(GameDataSaveCoordinator.IsTargetBlocked(target), Is.True);
        }
    }

    [Test]
    public void SharedSaveFlow_UnknownNullExceptionAndNoResponseBlockAllFollowingLegacyAndNewOwners()
    {
        foreach (string failure in new[] { "network", "null", "after-send-exception", "no-response" })
        {
            var fixture = RestoredFixture(false, out string account, out string row);
            int completed = 0, failures = 0;
            if (failure == "after-send-exception")
                fixture.Transport.OnUpdate = _ => throw new InvalidOperationException("fake after SDK boundary entry");
            var first = fixture.Manager.RequestGameDataSave(Values(), row, _ => completed++, _ => failures++);
            var queued = fixture.Manager.RequestGameDataAutosave(_ => completed++);
            var owner = fixture.Manager.CurrentGameDataSaveCoordinator;
            if (failure == "network") fixture.Transport.Updates[0].Reply(Bro("400", "", "HttpRequestException", "fake connection loss"));
            if (failure == "null") fixture.Transport.Updates[0].Reply(null);
            if (failure == "no-response")
            {
                Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
                Assert.That(completed, Is.Zero);
                Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
                Assert.That(owner.MarkResponseMissing(new GameDataSaveIdentity("wrong", owner.CurrentIdentity.Target), "timeout"), Is.False);
                Assert.That(owner.MarkResponseMissing(owner.CurrentIdentity, "test caller observed no response"), Is.True);
            }
            Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.Indeterminate));
            Assert.That(first.Receipt.SendStarted, Is.True);
            Assert.That(owner.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
            Assert.That(completed, Is.Zero);
            Assert.That(failures, Is.EqualTo(1), "Unknown outcomes are reported once, not retried or declared unapplied");
            Assert.That(fixture.Manager.RequestGameDataAutosave().Accepted, Is.False);
            AssertAllLegacyWritesBlocked(fixture, row);
            var outsider = new GameDataSaveCoordinator(owner.CurrentIdentity.Target,
                fixture.Manager.CreateGameDataSingleUpdate(), () => throw new InvalidOperationException("Blocked factory"));
            Assert.That(outsider.RequestAutosave().Accepted, Is.False);
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
            Assert.That(fixture.Transport.LatestCalls, Is.Zero);
            Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
            Assert.That(fixture.Transport.Inserts.Count, Is.Zero);
            Assert.That(fixture.Transport.Gets.Count, Is.EqualTo(1));
            Assert.That(queued.Status, Is.Not.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        }
    }

    [Test]
    public void SharedSaveFlow_ReentrantCompletionImmediateResponseAndLocalFailureKeepOrdering()
    {
        var fixture = RestoredFixture(false, out _, out string row);
        var order = new List<string>();
        bool firstCallbackFinished = false;
        fixture.Transport.OnUpdate = call => { call.Reply(Bro("204", "")); call.Reply(Bro("204", "")); };
        fixture.Transport.LatestFactory = () =>
        {
            Assert.That(firstCallbackFinished, Is.True, "A reentrant autosave must wait for the entire previous callback");
            order.Add("latest"); return Values();
        };
        GameDataSaveRequest next = null;
        var first = fixture.Manager.RequestGameDataSave(Values(), row, _ =>
        {
            order.Add("first-begin");
            next = fixture.Manager.RequestGameDataAutosave(__ => order.Add("second-confirmed"));
            Assert.That(next.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
            firstCallbackFinished = true;
            order.Add("first-end");
        });
        Assert.That(first.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        Assert.That(next.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
        CollectionAssert.AreEqual(new[] { "first-begin", "first-end", "latest", "second-confirmed" }, order);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(2));
        Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        Assert.That(fixture.Manager.SaveGameData("GameData", Values()), Is.True,
            "A legacy bool may be true only when this immediate fake already supplied confirmed success");
        Assert.That(fixture.Transport.SyncUpdateCalls, Is.Zero);

        var failed = RestoredFixture(false, out _, out string failedRow);
        int callbacks = 0;
        var localFailure = failed.Manager.RequestGameDataSave(Values(), failedRow, _ =>
        {
            callbacks++;
            failed.Manager.RequestGameDataAutosave();
            throw new InvalidOperationException("fake local completion exception");
        });
        Assert.DoesNotThrow(() => failed.Transport.Updates[0].Reply(Bro("204", "")));
        failed.Transport.Updates[0].Reply(Bro("204", ""));
        Assert.That(localFailure.Status, Is.EqualTo(GameDataSaveRequestStatus.LocalCompletionFailed));
        Assert.That(localFailure.Receipt.Disposition, Is.EqualTo(GameDataSaveDisposition.SuccessConfirmed),
            "Server response success and local completion failure remain distinct evidence");
        Assert.That(failed.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.LocalCompletionFailed));
        Assert.That(callbacks, Is.EqualTo(1));
        Assert.That(failed.Transport.LatestCalls, Is.Zero);
        Assert.That(failed.Transport.Updates.Count, Is.EqualTo(1));
        Assert.That(failed.Manager.RequestGameDataAutosave().Accepted, Is.False);
        AssertAllLegacyWritesBlocked(failed, failedRow);
    }

    [Test]
    public void SharedSaveFlow_NormalStaffAccountIsPreservedAndAsyncLegacyBoolNeverClaimsQueuedSuccess()
    {
        var fixture = RestoredFixture(true, out _, out string row);
        string common = StaffAccountSaveConverter.TrySerialize(fixture.Manager.RestoredGameData.StaffAccount,
            out string serialized, out string error) ? serialized : throw new InvalidOperationException(error);
        fixture.Transport.CurrentValues = Values();
        Assert.That(fixture.Manager.SaveGameData("GameData", Values()), Is.False);
        Assert.That(fixture.Manager.UpdateGameData("GameData", row, Values()), Is.False);
        int autoCompletions = 0;
        var queued = fixture.Manager.RequestGameDataAutosave(_ => autoCompletions++);
        Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
        Assert.That(autoCompletions, Is.Zero);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(1));
        for (int i = 0; i < 3; i++) fixture.Transport.Updates[i].Reply(Bro("204", ""));
        Assert.That(autoCompletions, Is.EqualTo(1));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(3));
        Assert.That(fixture.Transport.MaxInFlight, Is.EqualTo(1));
        Assert.That(fixture.Transport.SyncUpdateCalls, Is.Zero);
        Assert.That(fixture.Manager.RestoredGameData.StaffAccount.PandaTokens, Is.EqualTo(55));
        Assert.That(StaffAccountSaveConverter.TrySerialize(fixture.Manager.RestoredGameData.StaffAccount,
            out string after, out _), Is.True);
        Assert.That(after, Is.EqualTo(common));
        foreach (var write in fixture.Transport.Updates)
        {
            JToken savedCommon = JObject.Parse(write.Values.GetJson())[GameDataRestoreContext.StaffAccountFieldName];
            Assert.That(savedCommon == null || (savedCommon.Type == JTokenType.String && (string)savedCommon == common), Is.True,
                "An ordinary save may omit common staff, or preserve it, but may not replace it with null/default data");
        }
        // GameManager pause/quit bindings are inspected separately; this tests their asynchronous completion contract only.
    }

    private Fixture RestoredFixture(bool common, out string account, out string row)
    {
        var fixture = Create();
        account = Unique("integrated-account"); row = Unique("integrated-row");
        Authenticate(fixture, GameDataAuthenticationKind.Guest, "200", account);
        Assert.That(Restore(fixture, account, row, common), Is.EqualTo(1));
        return fixture;
    }

    private Fixture Create()
    {
        var fake = new FakeBackend { Catalog = Catalog() };
        // Never AddComponent/Awake/Instance: those would initialize real SDK and register runtime event handlers.
        var manager = (BackendManager)FormatterServices.GetUninitializedObject(typeof(BackendManager));
        Field(typeof(BackendManager), "_gameDataTransport").SetValue(manager, fake);
        Field(typeof(BackendManager), "_gameDataInitializationPolicy").SetValue(manager, FakePolicy());
        return new Fixture { Manager = manager, Transport = fake };
    }

    private static GameDataAuthenticationAttempt Authenticate(Fixture fixture, GameDataAuthenticationKind kind,
        string status, string account, bool alreadyLoggedIn = false)
    {
        fixture.Transport.LoggedIn = fixture.Transport.NativeLoggedIn = alreadyLoggedIn;
        fixture.Transport.AccountInDate = alreadyLoggedIn ? account : null;
        var attempt = fixture.Manager.BeginGameDataAuthentication(kind);
        fixture.Transport.NativeLoggedIn = fixture.Transport.LoggedIn = true;
        fixture.Transport.AccountInDate = account;
        // SDK public auth response is stripped; AccountInDate represents the SDK-populated authenticated identity.
        Assert.That(fixture.Manager.NotifyFederationLoginSuccess(attempt, Bro(status, "")), Is.True);
        return attempt;
    }

    private static int Restore(Fixture fixture, string account, string row, bool common)
    {
        int entries = 0;
        fixture.Manager.GetAndRestoreGameDataAsync((query, result) => { if (result.CanContinueLegacy) entries++; });
        fixture.Transport.Gets[fixture.Transport.Gets.Count - 1].Reply(Bro("200", Rows(Row(account, row, common))));
        return entries;
    }

    private static void AssertAllLegacyWritesBlocked(Fixture fixture, string row)
    {
        int updates = fixture.Transport.Updates.Count, inserts = fixture.Transport.Inserts.Count;
        int success = 0, failure = 0;
        var values = Values();
        string before = values.GetJson();
        fixture.Manager.SaveGameDataAsync("GameData", values, _ => success++, _ => failure++);
        fixture.Manager.UpdateGameDataAsync("GameData", row, values, _ => success++, _ => failure++);
        fixture.Manager.InsertGameDataAsync("GameData", values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.SaveGameData("GameData", values), Is.False);
        Assert.That(fixture.Manager.UpdateGameData("GameData", row, values), Is.False);
        Assert.That(fixture.Manager.InsertGameData("GameData", values), Is.False);
        Assert.That(success, Is.Zero);
        Assert.That(failure, Is.EqualTo(3));
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(updates));
        Assert.That(fixture.Transport.Inserts.Count, Is.EqualTo(inserts));
        Assert.That(values.GetJson(), Is.EqualTo(before));
    }

    private IReadOnlyList<StaffData> Catalog()
    {
        var values = new List<StaffData>();
        foreach (string id in new[] { "STAFF01", "STAFF03" })
        {
            StaffData asset = Resources.Load<StaffData>("StaffData/" + id);
            Assert.That(asset, Is.Not.Null, id);
            if (!_assets.ContainsKey(asset)) _assets.Add(asset, EditorJsonUtility.ToJson(asset));
            values.Add(asset);
        }
        return values;
    }
    private static string Unique(string prefix) => prefix + ":" + Guid.NewGuid().ToString("N");
    private static FieldInfo Field(Type type, string name, bool isStatic = false) =>
        type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
    private static Param Values() { var result = new Param(); result.Add("Dia", 10); return result; }
    private static JObject Row(string account, string row, bool common, int diamonds = 110)
    {
        var result = new JObject
        {
            ["owner_inDate"] = new JObject { ["S"] = account }, ["inDate"] = new JObject { ["S"] = row },
            ["Dia"] = new JObject { ["N"] = diamonds.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };
        if (common)
            result[GameDataRestoreContext.StaffAccountFieldName] = new JObject
            { ["S"] = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}" };
        return result;
    }
    private static string Rows(params JObject[] rows) => new JObject
    { ["rows"] = new JArray(rows.Select(row => row.DeepClone())), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
    private static BackendReturnObject Bro(string status, string raw, string error = "", string message = "")
    {
        var result = new BackendReturnObject();
        foreach (var entry in new Dictionary<string, string>
        { { "StatusCode", status }, { "ReturnValue", raw }, { "ErrorCode", error }, { "Message", message } })
        {
            PropertyInfo property = typeof(BackendReturnObject).GetProperty(entry.Key,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, entry.Key);
            property.SetValue(result, Convert.ChangeType(entry.Value, property.PropertyType));
        }
        return result;
    }
    private static GameDataSdkInitializationPolicy FakePolicy()
    {
        bool initialized = false;
        var settings = new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true);
        Func<GameDataSdkRetrySettings> read = () => settings;
        Func<bool> state = () => initialized;
        Func<GameDataRawResponse> initialize = () => { initialized = true; return new GameDataRawResponse(true, "200", "", ""); };
        var args = new object[] { read, state, initialize, null };
        typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return (GameDataSdkInitializationPolicy)args[3];
    }
    private sealed class Fixture { public BackendManager Manager; public FakeBackend Transport; }
    private sealed class FakeBackend : IGameDataBackendTransport
    {
        public static readonly DateTime FixedTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        public bool LoggedIn { get; set; }
        public bool NativeLoggedIn { get; set; }
        public bool GameplaySaveAllowed { get; set; } = true;
        public string AccountInDate { get; set; }
        public IReadOnlyList<StaffData> Catalog;
        public Func<BackendReturnObject, bool> Restoration;
        public readonly List<GetCall> Gets = new List<GetCall>();
        public readonly List<WriteCall> Inserts = new List<WriteCall>();
        public readonly List<WriteCall> Updates = new List<WriteCall>();
        public int RestoreCalls, InitialCalls;
        public int LatestCalls, InFlight, MaxInFlight, SyncUpdateCalls;
        public Param CurrentValues = Values();
        public Func<Param> LatestFactory;
        public Action<WriteCall> OnUpdate;
        public JObject StoredRow; // Optional memory-only UpdateV2 model; never reads/writes an actual account.
        public int AppliedUpdates;
        public Param LastInitialValues;
        public string InitialJson;
        public void Get(string accountInDate, Action<BackendReturnObject> callback) => Gets.Add(new GetCall { Account = accountInDate, Reply = callback });
        public void Insert(Param values, Action<BackendReturnObject> callback) => Inserts.Add(new WriteCall { Values = values, Reply = callback });
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback)
        {
            InFlight++;
            MaxInFlight = Math.Max(MaxInFlight, InFlight);
            bool responseObserved = false;
            bool valuesApplied = false;
            var call = new WriteCall { Target = target, Values = values };
            call.Reply = response =>
            {
                if (!responseObserved) { responseObserved = true; InFlight--; }
                if (!valuesApplied && StoredRow != null && response != null && response.IsSuccess())
                {
                    // UpdateV2 replaces supplied fields and preserves fields omitted from Param.
                    // A duplicate response is not another SDK send and must not reapply old values.
                    foreach (JProperty field in JObject.Parse(values.GetJson()).Properties())
                        StoredRow[field.Name] = field.Value.DeepClone();
                    valuesApplied = true;
                    AppliedUpdates++;
                }
                callback(response); // Deliberately forward duplicates to exercise the real once-only guard.
            };
            Updates.Add(call);
            OnUpdate?.Invoke(call);
        }
        public BackendReturnObject Update(GameDataSaveTarget target, Param values)
        { SyncUpdateCalls++; throw new InvalidOperationException("A separate synchronous SDK path is forbidden"); }
        public Param LatestValues() { LatestCalls++; return LatestFactory == null ? CurrentValues : LatestFactory(); }
        public IReadOnlyList<StaffData> ReadCatalog() => Catalog;
        public bool Restore(BackendReturnObject response) { RestoreCalls++; return Restoration == null || Restoration(response); }
        public Param InitialValues()
        {
            InitialCalls++;
            LastInitialValues = LoadUserData.CreateInitialGameData(FixedTime);
            InitialJson = LastInitialValues.GetJson();
            return LastInitialValues;
        }
        public sealed class GetCall { public string Account; public Action<BackendReturnObject> Reply; }
        public sealed class WriteCall { public GameDataSaveTarget Target; public Param Values; public Action<BackendReturnObject> Reply; }
    }
    private sealed class FakeSingleUpdate : IGameDataUpdateTransport
    {
        public int Count;
        public Action<GameDataSaveIdentity, GameDataRawResponse> Reply;
        public void UpdateOnce(GameDataSaveIdentity identity, Param values, Action<GameDataSaveIdentity, GameDataRawResponse> response)
        { Count++; Reply = response; }
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
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount,
            UserInfo.CurrentStage, UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas
        });
    }
}
#endif
