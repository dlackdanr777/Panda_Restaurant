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
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Raw response and legacy restoration are memory fakes. No SDK initialization, UserInfo load or network call.</summary>
public class GameDataRestoreContextTests
{
    private const string AccountId = "test-account";
    private const string RowId = "test-GameData-row";
    private readonly Dictionary<StaffData, string> _sourceSnapshots = new Dictionary<StaffData, string>();
    private Random.State _randomState;
    private string _gameState;

    [SetUp]
    public void SetUp() { _randomState = Random.state; _gameState = ReadGameState(); }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState));
            Assert.That(ReadGameState(), Is.EqualTo(_gameState), "Actual user or payment state changed");
            foreach (var source in _sourceSnapshots)
                Assert.That(EditorJsonUtility.ToJson(source.Key), Is.EqualTo(source.Value), "Registered staff asset changed");
        }
        finally { _sourceSnapshots.Clear(); }
    }

    [Test]
    public void ValidSingleRow_PreservesSameRowValuesAndPublishesEvidenceOnlyAfterRestoration()
    {
        var context = new GameDataRestoreContext(() => AccountId);
        List<StaffData> catalog = Catalog();
        StaffData[] catalogBefore = catalog.ToArray();
        var policy = FakeAppliedSdkPolicy();
        var fakeTransport = new FakeTransport();
        var updater = new GameDataSingleUpdate(() => new GameDataSaveReadiness(true, true,
            context.Evidence != null, true, AccountId, context.Evidence?.Target, initializationPolicy: policy), fakeTransport);
        var saveIdentity = new GameDataSaveIdentity("readiness-only", new GameDataSaveTarget(AccountId, RowId));
        Assert.That(context.Evidence, Is.Null);
        Assert.That(context.Result.Status, Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(updater.CanSend(saveIdentity, out _), Is.False);
        var query = context.BeginQuery();
        Assert.That(context.Result.Status, Is.EqualTo(GameDataRestoreStatus.QueryPending));
        Assert.That(context.IsCurrent(query), Is.True);
        int restored = 0;
        string raw = Response(Row());
        string rawBefore = raw;
        var result = context.TryRestore(query, true, raw, () => catalog, () =>
        {
            restored++;
            Assert.That(context.Result.Status, Is.EqualTo(GameDataRestoreStatus.Restoring));
            Assert.That(context.Evidence, Is.Null, "Evidence must not be published before legacy restoration succeeds");
            Assert.That(updater.CanSend(saveIdentity, out _), Is.False);
            var nested = context.TryRestore(query, true, raw,
                () => throw new InvalidOperationException("Reentrant catalog must not be read"),
                () => throw new InvalidOperationException("Reentrant restoration must not run"));
            Assert.That(nested.Status, Is.EqualTo(GameDataRestoreStatus.Invalidated));
            Assert.That(context.Result.Status, Is.EqualTo(GameDataRestoreStatus.Restoring));
            return true;
        });
        Assert.That(restored, Is.EqualTo(1));
        Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.Ready), result.Reason);
        Assert.That(result.CanContinueLegacy, Is.True);
        Assert.That(result, Is.SameAs(context.Result));
        Assert.That(result.Evidence, Is.SameAs(context.Evidence));
        Assert.That(result.Evidence.Query, Is.SameAs(query));
        Assert.That(result.Evidence.Target.AccountInDate, Is.EqualTo(AccountId));
        Assert.That(result.Evidence.Target.RowInDate, Is.EqualTo(RowId));
        Assert.That(result.Evidence.Diamonds, Is.EqualTo(110));
        Assert.That(result.Evidence.StaffAccount.PandaTokens, Is.EqualTo(55));
        Assert.That(result.Evidence.RawJson, Is.EqualTo(rawBefore));
        Assert.That(updater.CanSend(saveIdentity, out string readyError), Is.True, readyError);
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03" }, result.Evidence.StaffAccount.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4 }, result.Evidence.StaffAccount.Staff.Select(item => item.Level));
        Assert.That(raw, Is.EqualTo(rawBefore));
        CollectionAssert.AreEqual(catalogBefore, catalog);

        var next = context.BeginQuery();
        Assert.That(context.Evidence, Is.Null, "A new query must invalidate old proof");
        Assert.That(updater.CanSend(saveIdentity, out _), Is.False);
        var empty = context.TryRestore(next, true,
            Response(Row(0, Serialize(new StaffAccountSaveData(1, new StaffAccountStaffRecord[0], 0)))),
            () => catalog, () => true);
        Assert.That(empty.Status, Is.EqualTo(GameDataRestoreStatus.Ready), empty.Reason);
        Assert.That(empty.Evidence.Diamonds, Is.Zero);
        Assert.That(empty.Evidence.StaffAccount.Staff, Is.Empty);
        Assert.That(empty.Evidence.StaffAccount.PandaTokens, Is.Zero);
        Assert.That(result.Evidence.StaffAccount.PandaTokens, Is.EqualTo(55), "Published snapshots must remain immutable");
        Assert.That(updater.CanSend(saveIdentity, out readyError), Is.True, readyError);
        Assert.That(fakeTransport.Count, Is.Zero, "Readiness inspection does not send a request");
    }

    [Test]
    public void MissingAndMalformedData_DistinguishesAbsentRowsMigrationAndInvalidCommonData()
    {
        var catalog = Catalog();
        AssertBlocked(Response(), GameDataRestoreStatus.RowMissing, catalog);
        AssertBlocked(Response(Row()), GameDataRestoreStatus.QueryFailed, catalog, querySucceeded: false);
        foreach (string malformed in new[] { null, "", "{broken", "{}", "{\"rows\":null}", "{\"rows\":{}}" })
            AssertBlocked(malformed, GameDataRestoreStatus.InvalidResponse, catalog);

        JObject absent = Row();
        absent.Remove(GameDataRestoreContext.StaffAccountFieldName);
        int legacyCalls = 0, catalogCalls = 0;
        var context = new GameDataRestoreContext(() => AccountId);
        var migration = context.TryRestore(context.BeginQuery(), true, Response(absent), () =>
        { catalogCalls++; return catalog; }, () =>
        {
            legacyCalls++;
            Assert.That(context.Evidence, Is.Null);
            return true;
        });
        Assert.That(migration.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
        Assert.That(migration.CanContinueLegacy, Is.True, "Missing common data alone must not block legacy game entry");
        Assert.That(migration.Evidence, Is.Null);
        Assert.That(context.Evidence, Is.Null);
        Assert.That(catalogCalls, Is.Zero);
        Assert.That(legacyCalls, Is.EqualTo(1));

        foreach (JToken malformed in new JToken[]
        {
            JValue.CreateNull(), new JObject { ["S"] = JValue.CreateNull() },
            new JObject { ["S"] = "" }, new JObject { ["S"] = "{broken" },
            new JObject { ["N"] = "1" }, new JValue(Serialize(Account())),
            new JObject { ["S"] = "{\"Version\":1,\"Staff\":[]}" },
            new JObject { ["S"] = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":0}],\"PandaTokens\":55}" }
        })
        {
            JObject row = Row();
            row[GameDataRestoreContext.StaffAccountFieldName] = malformed;
            AssertBlocked(Response(row), GameDataRestoreStatus.InvalidStaffData, catalog);
        }
        JObject unsupported = Row(commonJson: "{\"Version\":2,\"Staff\":[],\"PandaTokens\":55}");
        AssertBlocked(Response(unsupported), GameDataRestoreStatus.UnsupportedStaffVersion, catalog);
    }

    [Test]
    public void TargetDiamondsAndCatalogValidation_RejectsAmbiguousIncompleteOrUnverifiableInputs()
    {
        var catalog = Catalog();
        AssertBlocked(Response(Row(), Row()), GameDataRestoreStatus.AmbiguousRows, catalog);
        foreach (JToken firstKey in new JToken[] { new JObject(), new JArray(), new JValue("next-page") })
        {
            var response = JObject.Parse(Response(Row()));
            response["firstKey"] = firstKey;
            AssertBlocked(response.ToString(Formatting.None), GameDataRestoreStatus.PaginationIncomplete, catalog);
            // An empty page with a continuation cannot prove that the entire account has no row.
            response["rows"] = new JArray();
            AssertBlocked(response.ToString(Formatting.None), GameDataRestoreStatus.PaginationIncomplete, catalog);
        }
        JObject wrongOwner = Row();
        wrongOwner["owner_inDate"] = S("other-account");
        AssertBlocked(Response(wrongOwner), GameDataRestoreStatus.AccountMismatch, catalog);
        foreach (string field in new[] { "owner_inDate", "inDate" })
        {
            JObject missing = Row();
            missing.Remove(field);
            AssertBlocked(Response(missing), GameDataRestoreStatus.InvalidResponse, catalog);
            foreach (JToken invalid in new JToken[] { S(" "), new JObject { ["N"] = "1" }, new JValue("unwrapped") })
            {
                JObject row = Row();
                row[field] = invalid;
                AssertBlocked(Response(row), GameDataRestoreStatus.InvalidResponse, catalog);
            }
        }
        JObject missingDiamonds = Row();
        missingDiamonds.Remove("Dia");
        AssertBlocked(Response(missingDiamonds), GameDataRestoreStatus.InvalidDiamonds, catalog);
        foreach (JToken invalid in new JToken[]
        {
            JValue.CreateNull(), S("110"), new JValue(110), new JObject { ["N"] = 110 },
            new JObject { ["N"] = "-1" }, new JObject { ["N"] = "2147483648" },
            new JObject { ["N"] = "1.5" }, new JObject { ["N"] = "1e2" }, new JObject { ["N"] = " 110" }
        })
        {
            JObject row = Row();
            row["Dia"] = invalid;
            AssertBlocked(Response(row), GameDataRestoreStatus.InvalidDiamonds, catalog);
        }

        AssertBlocked(Response(Row()), GameDataRestoreStatus.CatalogUnavailable, null);
        AssertBlocked(Response(Row()), GameDataRestoreStatus.CatalogUnavailable, new StaffData[0]);
        AssertBlocked(Response(Row()), GameDataRestoreStatus.InvalidStaffCatalog, new[] { catalog[0], catalog[0] });
        AssertBlocked(Response(Row()), GameDataRestoreStatus.InvalidStaffCatalog, new[] { catalog[0], null });
        AssertBlocked(Response(Row()), GameDataRestoreStatus.InvalidStaffCatalog, catalog, catalogThrows: true);
        AssertBlocked(Response(Row(commonJson: Serialize(new StaffAccountSaveData(1,
            new[] { new StaffAccountStaffRecord("STAFF_UNREGISTERED_TEST", 1) }, 55)))),
            GameDataRestoreStatus.UnregisteredStaff, catalog);
        StaffData staff = catalog[0];
        int maximum = Math.Min(staff.MaxLevel, StaffData.OfficialMaxLevel);
        Assert.That(maximum, Is.GreaterThanOrEqualTo(2), "Registered fixture does not support the required existing level");
        string excessive = Serialize(new StaffAccountSaveData(1,
            new[] { new StaffAccountStaffRecord(staff.Id, maximum + 1) }, 55));
        AssertBlocked(Response(Row(commonJson: excessive)), GameDataRestoreStatus.InvalidStaffLevel, catalog);
        var boundary = new GameDataRestoreContext(() => AccountId);
        var atMaximum = boundary.TryRestore(boundary.BeginQuery(), true, Response(Row(int.MaxValue,
            Serialize(new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord(staff.Id, maximum) }, 55)))),
            () => catalog, () => true);
        Assert.That(atMaximum.Status, Is.EqualTo(GameDataRestoreStatus.Ready), atMaximum.Reason);
        Assert.That(atMaximum.Evidence.Diamonds, Is.EqualTo(int.MaxValue));
        Assert.That(atMaximum.Evidence.StaffAccount.Staff[0].Level, Is.EqualTo(maximum));
    }

    [Test]
    public void QueryLifetimeAndRestorationFailure_NeverAppliesStaleResponsesOrPublishesPartialProof()
    {
        string currentAccount = AccountId;
        var catalog = Catalog();
        var context = new GameDataRestoreContext(() => currentAccount);
        var oldQuery = context.BeginQuery();
        var newQuery = context.BeginQuery();
        var pending = context.Result;
        int restorations = 0;
        Func<bool> restore = () => { restorations++; return true; };
        var stale = context.TryRestore(oldQuery, true, Response(Row()), () => catalog, restore);
        Assert.That(stale.Status, Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(context.Result, Is.SameAs(pending));
        Assert.That(restorations, Is.Zero);
        Assert.That(context.TryRestore(newQuery, true, Response(Row()), () => catalog, restore).Status,
            Is.EqualTo(GameDataRestoreStatus.Ready));
        Assert.That(restorations, Is.EqualTo(1));
        oldQuery = context.BeginQuery();
        currentAccount = "other-account";
        Assert.That(context.TryRestore(oldQuery, true, Response(Row()), () => catalog, restore).Status,
            Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(context.Evidence, Is.Null);
        Assert.That(restorations, Is.EqualTo(1));

        currentAccount = AccountId;
        var beforeRelogin = context.BeginQuery();
        context.InvalidateAccountSession();
        var afterRelogin = context.BeginQuery();
        pending = context.Result;
        Assert.That(context.TryRestore(beforeRelogin, true, Response(Row()), () => catalog, restore).Status,
            Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(context.Result, Is.SameAs(pending));
        Assert.That(restorations, Is.EqualTo(1));
        Assert.That(context.TryRestore(afterRelogin, true, Response(Row()), () => catalog, restore).Status,
            Is.EqualTo(GameDataRestoreStatus.Ready));

        var failed = context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog, () => false);
        Assert.That(failed.Status, Is.EqualTo(GameDataRestoreStatus.RestoreFailed));
        Assert.That(failed.Evidence, Is.Null);
        Assert.That(failed.CanContinueLegacy, Is.False);
        var thrown = context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog,
            () => throw new InvalidOperationException("fake restoration failed"));
        Assert.That(thrown.Status, Is.EqualTo(GameDataRestoreStatus.RestoreFailed));
        Assert.That(thrown.Reason, Is.Not.Null.And.Not.Empty);
        Assert.That(context.Evidence, Is.Null);
        var accountChangedDuringRestore = context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog,
            () => { currentAccount = "changed-during-restore"; return true; });
        Assert.That(accountChangedDuringRestore.Status, Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(context.Evidence, Is.Null);

        currentAccount = AccountId;
        GameDataRestoreQuery startedDuringRestore = null;
        var replaced = context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog,
            () => { startedDuringRestore = context.BeginQuery(); return true; });
        Assert.That(replaced.Status, Is.EqualTo(GameDataRestoreStatus.Invalidated));
        Assert.That(context.Result.Status, Is.EqualTo(GameDataRestoreStatus.QueryPending));
        Assert.That(context.IsCurrent(startedDuringRestore), Is.True);
        Assert.That(context.Evidence, Is.Null);
    }

    [Test]
    public void RequeryAndSameAccountRelogin_DoNotReleaseExistingUncertainOrLocalFailureSaveLocks()
    {
        var catalog = Catalog();
        var context = new GameDataRestoreContext(() => AccountId);
        Assert.That(context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog, () => true).Status,
            Is.EqualTo(GameDataRestoreStatus.Ready));
        var originalEvidence = context.Evidence;
        var target = originalEvidence.Target;
        var policy = FakeAppliedSdkPolicy();
        foreach (bool localApplyThrows in new[] { false, true })
        {
            var transport = new FakeTransport();
            var updater = new GameDataSingleUpdate(() => new GameDataSaveReadiness(true, true,
                context.Evidence != null, true, AccountId, context.Evidence?.Target, initializationPolicy: policy), transport);
            var coordinator = new GameDataSaveCoordinator(target, updater, Values);
            var identity = new GameDataSaveIdentity(localApplyThrows ? "local-failure" : "unknown-save", target);
            Assert.That(coordinator.TryStartPurchase(identity, Values, _ =>
            {
                if (localApplyThrows) throw new InvalidOperationException("fake local completion failure");
            }, out string error), Is.True, error);
            transport.Respond(identity, localApplyThrows
                ? new GameDataRawResponse(true, "204", "", "")
                : new GameDataRawResponse(false, "400", "HttpRequestException", "unknown write result"));
            GameDataSaveCoordinatorState expected = localApplyThrows
                ? GameDataSaveCoordinatorState.LocalCompletionFailed : GameDataSaveCoordinatorState.Indeterminate;
            Assert.That(coordinator.State, Is.EqualTo(expected));
            var receiptBefore = coordinator.LastReceipt;
            string payloadBefore = coordinator.CurrentPayload.Json;

            var refreshed = context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog, () => true);
            Assert.That(refreshed.Status, Is.EqualTo(GameDataRestoreStatus.Ready));
            context.InvalidateAccountSession();
            Assert.That(context.Evidence, Is.Null);
            Assert.That(context.TryRestore(context.BeginQuery(), true, Response(Row()), () => catalog, () => true).Status,
                Is.EqualTo(GameDataRestoreStatus.Ready));
            Assert.That(coordinator.State, Is.EqualTo(expected));
            Assert.That(coordinator.CanStartPurchase, Is.False);
            Assert.That(coordinator.CurrentIdentity, Is.SameAs(identity));
            Assert.That(coordinator.CurrentPayload.Json, Is.EqualTo(payloadBefore));
            Assert.That(coordinator.LastReceipt, Is.SameAs(receiptBefore));
            Assert.That(coordinator.MarkAutosaveNeeded(), Is.False);
            Assert.That(coordinator.TryStartPurchase(new GameDataSaveIdentity("blocked-new", target),
                () => throw new InvalidOperationException("A blocked new factory must not run"), _ => { }, out error), Is.False);
            Assert.That(transport.Count, Is.EqualTo(1));
        }
        Assert.That(originalEvidence.Diamonds, Is.EqualTo(110));
        Assert.That(originalEvidence.StaffAccount.PandaTokens, Is.EqualTo(55));
    }

    private void AssertBlocked(string raw, GameDataRestoreStatus expected, IReadOnlyList<StaffData> catalog,
        bool querySucceeded = true, bool catalogThrows = false)
    {
        var context = new GameDataRestoreContext(() => AccountId);
        int restorations = 0;
        StaffData[] before = catalog?.ToArray();
        string originalRaw = raw;
        var result = context.TryRestore(context.BeginQuery(), querySucceeded, raw, () =>
        {
            if (catalogThrows) throw new InvalidOperationException("catalog getter failed");
            return catalog;
        }, () => { restorations++; return true; });
        Assert.That(result.Status, Is.EqualTo(expected), result.Reason);
        Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Evidence, Is.Null);
        Assert.That(context.Evidence, Is.Null);
        Assert.That(result.CanContinueLegacy, Is.False);
        Assert.That(restorations, Is.Zero, "Rejected raw data must not reach legacy application");
        Assert.That(raw, Is.EqualTo(originalRaw));
        if (before != null) CollectionAssert.AreEqual(before, catalog);
    }

    private List<StaffData> Catalog()
    {
        // Same raw registration source as the production adapter, not the sprite-filtered gacha list.
        var result = Resources.LoadAll<StaffData>("StaffData").ToList();
        foreach (string id in new[] { "STAFF01", "STAFF03" })
        {
            Assert.That(result.Count(data => data != null && data.Id == id), Is.EqualTo(1), id);
        }
        foreach (StaffData data in result)
        {
            Assert.That(data, Is.Not.Null);
            if (!_sourceSnapshots.ContainsKey(data)) _sourceSnapshots.Add(data, EditorJsonUtility.ToJson(data));
        }
        return result;
    }

    private static JObject S(string value) => new JObject { ["S"] = value };
    private static JObject Row(int diamonds = 110, string commonJson = null) => new JObject
    {
        ["owner_inDate"] = S(AccountId), ["inDate"] = S(RowId),
        ["Dia"] = new JObject { ["N"] = diamonds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        [GameDataRestoreContext.StaffAccountFieldName] = S(commonJson ?? Serialize(Account()))
    };
    private static string Response(params JObject[] rows) => new JObject
    { ["rows"] = new JArray(rows.Select(row => row.DeepClone())), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
    private static StaffAccountSaveData Account() => new StaffAccountSaveData(1, new[]
    { new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4) }, 55);
    private static string Serialize(StaffAccountSaveData data)
    {
        Assert.That(StaffAccountSaveConverter.TrySerialize(data, out string json, out string error), Is.True, error);
        return json;
    }
    private static Param Values()
    {
        var result = new Param();
        result.Add("Dia", 110);
        result.Add(GameDataRestoreContext.StaffAccountFieldName, Serialize(Account()));
        return result;
    }

    private static GameDataSdkInitializationPolicy FakeAppliedSdkPolicy()
    {
        bool initialized = false;
        var settings = new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true);
        Func<GameDataSdkRetrySettings> read = () => settings;
        Func<bool> state = () => initialized;
        Func<GameDataRawResponse> initialize = () =>
        { initialized = true; return new GameDataRawResponse(true, "200", "", "fake initialization only"); };
        var arguments = new object[] { read, state, initialize, null };
        typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, arguments);
        var result = (GameDataSdkInitializationPolicy)arguments[3];
        Assert.That(result.IsSupported, Is.True, result.BlockReason);
        return result;
    }

    private sealed class FakeTransport : IGameDataUpdateTransport
    {
        public int Count;
        public Action<GameDataSaveIdentity, GameDataRawResponse> Respond;
        public void UpdateOnce(GameDataSaveIdentity identity, Param values, Action<GameDataSaveIdentity, GameDataRawResponse> response)
        { Count++; Respond = response; }
    }

    private static string ReadGameState()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var stages = (StageInfo[])typeof(UserInfo)
            .GetField("_stageInfos", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
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
