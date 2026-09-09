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
        Assert.That(fixture.Manager.SaveGameData("GameData", values), Is.True);
        Assert.That(fixture.Manager.UpdateGameData("GameData", row, values), Is.True);
        Assert.That(fixture.Transport.Updates.Count, Is.EqualTo(4));
        foreach (var update in fixture.Transport.Updates)
        {
            Assert.That(update.Target.AccountInDate, Is.EqualTo(account));
            Assert.That(update.Target.RowInDate, Is.EqualTo(row));
            Assert.That(update.Values.GetJson(), Is.EqualTo(original));
            Assert.That(update.Values, Is.Not.SameAs(values));
        }
        Assert.That(values.GetJson(), Is.EqualTo(original));
        fixture.Transport.Updates[0].Reply(Bro("204", ""));
        fixture.Transport.Updates[0].Reply(Bro("204", ""));
        fixture.Transport.Updates[1].Reply(Bro("204", ""));
        Assert.That(success, Is.EqualTo(2));
        fixture.Manager.UpdateGameDataAsync("GameData", "wrong-row", values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.UpdateGameData("GameData", "wrong-row", values), Is.False);
        fixture.Manager.InsertGameDataAsync("GameData", values, _ => success++, _ => failure++);
        Assert.That(fixture.Manager.InsertGameData("GameData", values), Is.False);
        Assert.That(failure, Is.EqualTo(2));
        fixture.Manager.SaveGameDataAsync("GameData", values, _ => success++, _ => failure++);
        fixture.Transport.Updates[4].Reply(Bro("400", "", "HttpRequestException"));
        fixture.Transport.Updates[4].Reply(Bro("400", "", "HttpRequestException"));
        fixture.Transport.Updates[4].Reply(Bro("204", ""));
        Assert.That(failure, Is.EqualTo(3));
        Assert.That(success, Is.EqualTo(2));
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
        public string AccountInDate { get; set; }
        public IReadOnlyList<StaffData> Catalog;
        public Func<BackendReturnObject, bool> Restoration;
        public readonly List<GetCall> Gets = new List<GetCall>();
        public readonly List<WriteCall> Inserts = new List<WriteCall>();
        public readonly List<WriteCall> Updates = new List<WriteCall>();
        public int RestoreCalls, InitialCalls;
        public Param LastInitialValues;
        public string InitialJson;
        public void Get(string accountInDate, Action<BackendReturnObject> callback) => Gets.Add(new GetCall { Account = accountInDate, Reply = callback });
        public void Insert(Param values, Action<BackendReturnObject> callback) => Inserts.Add(new WriteCall { Values = values, Reply = callback });
        public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback) =>
            Updates.Add(new WriteCall { Target = target, Values = values, Reply = callback });
        public BackendReturnObject Update(GameDataSaveTarget target, Param values)
        { Updates.Add(new WriteCall { Target = target, Values = values }); return Bro("204", ""); }
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
