#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

// Actual initial serializer, legacy parser and raw restoration context; no SDK/UserInfo apply or writes.
public sealed class TutorialRewardSchemaTests
{
    private const string Account = "tutorial-schema-memory-account";
    private const string Field = GameDataRestoreContext.FirstTutorialStartRewardGrantedFieldName;
    private Random.State _random;
    private string _gameState;

    [SetUp]
    public void SetUp() { _random = Random.state; _gameState = GameState(); }

    [TearDown]
    public void TearDown()
    {
        Assert.That(Random.state, Is.EqualTo(_random));
        Assert.That(GameState(), Is.EqualTo(_gameState), "Schema reads must not grant money/staff or record a purchase");
    }

    [Test]
    public void InitialData_ExplicitFalseMarkerDoesNotGrantAnyRewardOrChangeStaffJsonVersion()
    {
        JObject values = InitialValues();
        Assert.That(values[Field].Type, Is.EqualTo(JTokenType.Boolean));
        Assert.That((bool)values[Field], Is.False);
        Assert.That((long)values["Money"], Is.Zero);
        Assert.That((int)values["Dia"], Is.Zero);
        Assert.That((bool)values["IsFirstTutorialClear"], Is.False);
        Assert.That(values.Property(GameDataRestoreContext.StaffAccountFieldName), Is.Null,
            "Initial data must not invent a common staff account or perform migration");
        var loaded = Parse(values);
        Assert.That(loaded.IsValid, Is.True);
        Assert.That(loaded.FirstTutorialStartRewardGranted, Is.EqualTo(false));
        Assert.That(loaded.Money, Is.Zero);
        Assert.That(loaded.IsFirstTutorialClear, Is.False);
        Assert.That(StaffAccountSaveConverter.TrySerialize(new StaffAccountSaveData(1,
            new StaffAccountStaffRecord[0], 0), out string commonJson, out string error), Is.True, error);
        JObject common = JObject.Parse(commonJson);
        Assert.That((int)common["Version"], Is.EqualTo(1));
        Assert.That(common.Property(Field), Is.Null, "Reward evidence is a GameData field, not a StaffAccount format change");
    }

    [Test]
    public void MissingAndBooleanMarkers_KeepNullableLegacyEvidenceAndExistingRestoreContracts()
    {
        IReadOnlyList<StaffData> catalog = Resources.LoadAll<StaffData>("StaffData");
        foreach (bool common in new[] { false, true })
        foreach (bool? granted in new bool?[] { null, false, true })
        {
            JObject values = InitialValues();
            if (granted.HasValue) values[Field] = granted.Value;
            else values.Remove(Field);
            values["Money"] = 8123L;
            values["IsFirstTutorialClear"] = granted == null; // Missing on an old completed account remains unknown.
            if (common) values[GameDataRestoreContext.StaffAccountFieldName] = "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}";
            var loaded = Parse(values);
            Assert.That(loaded.IsValid, Is.True);
            Assert.That(loaded.FirstTutorialStartRewardGranted, Is.EqualTo(granted));
            Assert.That(loaded.Money, Is.EqualTo(8123L));
            string raw = Response(values);
            string original = raw;
            var context = new GameDataRestoreContext(() => Account);
            int restorations = 0;
            var result = context.TryRestore(context.BeginQuery(), true, raw, () => catalog,
                () => { restorations++; return Parse(values).IsValid; });
            Assert.That(result.Status, Is.EqualTo(common ? GameDataRestoreStatus.Ready : GameDataRestoreStatus.MigrationRequired), result.Reason);
            Assert.That(result.CanContinueLegacy, Is.True);
            Assert.That(restorations, Is.EqualTo(1));
            Assert.That(context.LegacyQuery, Is.Not.Null);
            Assert.That(context.LegacyTarget, Is.Not.Null);
            if (common) Assert.That(result.Evidence.RawJson, Is.EqualTo(original));
            else Assert.That(result.Evidence, Is.Null);
            Assert.That(raw, Is.EqualTo(original));
            Assert.That(Parse(values).FirstTutorialStartRewardGranted, Is.EqualTo(granted), "Repeated reads cannot turn unknown into false");
        }
    }

    [Test]
    public void MalformedPresentMarker_BlocksParsingAndRawRestoreBeforeLegacyApplyOrCatalogAccess()
    {
        foreach (JToken invalid in new JToken[]
        {
            JValue.CreateNull(), new JValue("false"), new JValue("true"), new JValue(0), new JValue(1),
            new JValue(0.0), new JArray(), new JObject(), new JObject { ["BOOL"] = false }
        })
        {
            JObject values = InitialValues();
            values[Field] = invalid.DeepClone();
            string original = values.ToString(Formatting.None);
            var loaded = Parse(values);
            Assert.That(loaded.IsValid, Is.False, "Flattened marker: " + invalid.ToString(Formatting.None));
            Assert.That(loaded.FirstTutorialStartRewardGranted, Is.Null);
            Assert.That(values.ToString(Formatting.None), Is.EqualTo(original));
        }

        foreach (bool common in new[] { false, true })
        foreach (JToken invalid in new JToken[]
        {
            JValue.CreateNull(), new JValue(false), new JObject(), new JArray(),
            new JObject { ["BOOL"] = JValue.CreateNull() }, new JObject { ["BOOL"] = "false" },
            new JObject { ["BOOL"] = 0 }, new JObject { ["BOOL"] = new JArray() },
            new JObject { ["S"] = "true" }, new JObject { ["N"] = "0" },
            new JObject { ["BOOL"] = true, ["S"] = "true" }
        })
        {
            JObject values = InitialValues();
            if (common) values[GameDataRestoreContext.StaffAccountFieldName] = "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}";
            JObject response = JObject.Parse(Response(values));
            response["rows"][0][Field] = invalid.DeepClone();
            string raw = response.ToString(Formatting.None);
            var context = new GameDataRestoreContext(() => Account);
            int restorations = 0, catalogReads = 0;
            var result = context.TryRestore(context.BeginQuery(), true, raw,
                () => { catalogReads++; return Array.Empty<StaffData>(); },
                () => { restorations++; return true; });
            Assert.That(result.Status, Is.EqualTo(GameDataRestoreStatus.InvalidResponse), raw);
            Assert.That(result.CanContinueLegacy, Is.False);
            Assert.That(result.Evidence, Is.Null);
            Assert.That(context.LegacyQuery, Is.Null);
            Assert.That(context.LegacyTarget, Is.Null);
            Assert.That(restorations, Is.Zero, "Damaged grant evidence must never reach UserInfo apply");
            Assert.That(catalogReads, Is.Zero, "Marker is validated in the raw envelope before the StaffAccount branch");
            Assert.That(response.ToString(Formatting.None), Is.EqualTo(raw));
        }
    }

    [Test]
    public void ConfirmedInitialCreation_RequiresTheSameNewAuthenticationAndConfirmedInsertNotJustAbsentRows()
    {
        GameDataSdkInitializationPolicy policy = AppliedMemoryPolicy();
        string emptyRows = "{\"rows\":[],\"firstKey\":null}";
        foreach (string loginStatus in new[] { "200", "201" })
        {
            string account = Account;
            var context = new GameDataRestoreContext(() => account);
            var authentication = context.BeginAuthentication(GameDataAuthenticationKind.Guest, false);
            Assert.That(context.CompleteAuthentication(authentication,
                new GameDataRawResponse(true, loginStatus, "", ""), account), Is.True);
            var query = context.BeginQuery();
            Assert.That(context.HasConfirmedInitialCreation(query), Is.False);
            Assert.That(context.TryRestore(query, true, emptyRows, null, () => true).Status,
                Is.EqualTo(GameDataRestoreStatus.RowMissing));
            Assert.That(context.HasConfirmedInitialCreation(query), Is.False, "Absent GameData alone is not new-account proof");
            bool accepted = context.TryBeginInitialCreation(query, policy, out _);
            Assert.That(accepted, Is.EqualTo(loginStatus == "201"));
            Assert.That(context.HasConfirmedInitialCreation(query), Is.False, "An in-flight initial Insert is not confirmed");
            if (!accepted) continue;
            Assert.That(context.CompleteInitialCreation(query, new GameDataRawResponse(true, "200", "", "")).Status,
                Is.EqualTo(GameDataRestoreStatus.InitialCreationConfirmedAwaitingRestore));
            Assert.That(context.HasConfirmedInitialCreation(query), Is.True);

            var restoredQuery = context.BeginQuery();
            Assert.That(context.HasConfirmedInitialCreation(query), Is.False, "Old query cannot authorize Stage creation");
            Assert.That(context.TryRestore(restoredQuery, true, Response(InitialValues()), null, () => true).Status,
                Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(context.HasConfirmedInitialCreation(restoredQuery), Is.True, "Required requery keeps same-authentication bootstrap proof");
            foreach (string nextStatus in new[] { "200", "201" })
            {
                var nextAuthentication = context.BeginAuthentication(GameDataAuthenticationKind.Guest, false);
                Assert.That(context.CompleteAuthentication(nextAuthentication,
                    new GameDataRawResponse(true, nextStatus, "", ""), account), Is.True);
                var newQuery = context.BeginQuery();
                Assert.That(context.HasConfirmedInitialCreation(restoredQuery), Is.False);
                Assert.That(context.HasConfirmedInitialCreation(newQuery), Is.False,
                    "Even another 201 cannot borrow the prior authentication's confirmed Insert");
            }
            account = Account + "-other";
            var otherAuthentication = context.BeginAuthentication(GameDataAuthenticationKind.Guest, false);
            Assert.That(context.CompleteAuthentication(otherAuthentication,
                new GameDataRawResponse(true, "201", "", ""), account), Is.True);
            Assert.That(context.HasConfirmedInitialCreation(context.BeginQuery()), Is.False,
                "A new account cannot borrow another account's creation record");
        }

        var unknown = new GameDataRestoreContext(() => Account);
        var signup = unknown.BeginAuthentication(GameDataAuthenticationKind.Guest, false);
        Assert.That(unknown.CompleteAuthentication(signup, new GameDataRawResponse(true, "201", "", ""), Account), Is.True);
        var unknownQuery = unknown.BeginQuery();
        unknown.TryRestore(unknownQuery, true, emptyRows, null, () => true);
        Assert.That(unknown.TryBeginInitialCreation(unknownQuery, policy, out _), Is.True);
        Assert.That(unknown.CompleteInitialCreation(unknownQuery, null).Status,
            Is.EqualTo(GameDataRestoreStatus.InitialCreationIndeterminate));
        Assert.That(unknown.HasConfirmedInitialCreation(unknownQuery), Is.False);
        Assert.That(unknown.HasConfirmedInitialCreation(unknown.BeginQuery()), Is.False,
            "Requery must not convert an unknown Insert into bootstrap permission");
    }

    private static GameDataSdkInitializationPolicy AppliedMemoryPolicy()
    {
        // The existing SDK-policy probe pattern, with memory delegates only; no Backend.Initialize.
        bool initialized = false;
        var settings = new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true);
        Func<GameDataSdkRetrySettings> read = () => settings;
        Func<bool> state = () => initialized;
        Func<GameDataRawResponse> initialize = () =>
        { initialized = true; return new GameDataRawResponse(true, "200", "", "schema test only"); };
        var arguments = new object[] { read, state, initialize, null };
        typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, arguments);
        var policy = (GameDataSdkInitializationPolicy)arguments[3];
        Assert.That(policy.IsSupported, Is.True, policy.BlockReason);
        return policy;
    }

    private static JObject InitialValues() => JObject.Parse(LoadUserData.CreateInitialGameData(
        new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc)).GetJson());
    private static LoadUserData Parse(JObject values) => new LoadUserData(JsonMapper.ToObject(
        new JArray(values.DeepClone()).ToString(Formatting.None)));
    private static string Response(JObject values)
    {
        var row = new JObject
        {
            ["owner_inDate"] = new JObject { ["S"] = Account },
            ["inDate"] = new JObject { ["S"] = "tutorial-schema-memory-row" },
            ["Dia"] = new JObject { ["N"] = "0" }
        };
        // Only the restore context's raw fields; the legacy callback separately exercises the full parser.
        if (values.Property(Field) != null) row[Field] = new JObject { ["BOOL"] = values[Field].DeepClone() };
        if (values.Property(GameDataRestoreContext.StaffAccountFieldName) != null)
            row[GameDataRestoreContext.StaffAccountFieldName] = new JObject { ["S"] = values[GameDataRestoreContext.StaffAccountFieldName].DeepClone() };
        return new JObject { ["rows"] = new JArray(row), ["firstKey"] = JValue.CreateNull() }.ToString(Formatting.None);
    }
    private static string GameState() => JsonConvert.SerializeObject(new
    {
        UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount,
        Payments = PaymentInfo.PaymentDatas, GachaPayments = PaymentInfo.GachaPaymentDatas
    });
}
#endif
