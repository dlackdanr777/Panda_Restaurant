#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Random = UnityEngine.Random;

public class StaffAccountSaveConverterTests
{
    private Random.State _randomState;

    [SetUp]
    public void SetUp() => _randomState = Random.state;

    [TearDown]
    public void TearDown() => Assert.That(Random.state, Is.EqualTo(_randomState));

    [Test]
    public void RoundTrip_PreservesRecordsIntegerBoundsAndCopiedInputsWithoutRegistryRules()
    {
        Assert.That(StaffAccountSaveConverter.CurrentVersion, Is.EqualTo(1));
        var records = new List<StaffAccountStaffRecord>
        {
            new StaffAccountStaffRecord("STAFF01", 2),
            new StaffAccountStaffRecord("UNKNOWN_ID", 6)
        };
        string recordsBefore = JsonConvert.SerializeObject(records);
        var data = new StaffAccountSaveData(1, records, 55);
        const string expected = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"UNKNOWN_ID\",\"Level\":6}],\"PandaTokens\":55}";
        AssertRoundTrip(data, expected);
        Assert.That(JsonConvert.SerializeObject(records), Is.EqualTo(recordsBefore));

        records[0] = new StaffAccountStaffRecord("REPLACED", 9);
        records.Clear();
        Assert.That(data.Staff.Count, Is.EqualTo(2), "The save model retained the caller's mutable list");
        AssertRoundTrip(data, expected);

        // Only storage-type bounds apply: no catalog lookup, official level cap or ID case folding.
        var bounds = new StaffAccountSaveData(1, new[]
        {
            new StaffAccountStaffRecord("STAFF01", 1),
            new StaffAccountStaffRecord("staff01", int.MaxValue)
        }, long.MaxValue);
        AssertRoundTrip(bounds,
            "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":1},{\"Id\":\"staff01\",\"Level\":2147483647}],\"PandaTokens\":9223372036854775807}");
        AssertRoundTrip(new StaffAccountSaveData(1,
                new[] { new StaffAccountStaffRecord("2026-09-08T00:00:00Z", 1) }, 0),
            "{\"Version\":1,\"Staff\":[{\"Id\":\"2026-09-08T00:00:00Z\",\"Level\":1}],\"PandaTokens\":0}");
    }

    [Test]
    public void EmptyAccount_IsValidButAbsentAndIncompletePayloadsRemainDistinct()
    {
        var empty = new StaffAccountSaveData(1, new StaffAccountStaffRecord[0], 0);
        AssertRoundTrip(empty, "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}");
        AssertReadFailure(null, StaffAccountSaveReadStatus.Missing);
        foreach (string json in new[]
        {
            "", " \t\r\n", "null", "{}", "{\"Staff\":[],\"PandaTokens\":0}",
            "{\"Version\":1,\"PandaTokens\":0}", "{\"Version\":1,\"Staff\":[]}",
            "{\"Version\":1,\"Staff\":[{}],\"PandaTokens\":0}",
            "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\"}],\"PandaTokens\":0}",
            "{\"Version\":1,\"Staff\":[{\"Level\":1}],\"PandaTokens\":0}"
        }) AssertReadFailure(json, StaffAccountSaveReadStatus.InvalidData);
    }

    [Test]
    public void InvalidPayloadsAndModels_RejectTypesDuplicatesOverflowAndPartialResults()
    {
        const string valid = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2}],\"PandaTokens\":55}";
        var invalidJson = new List<string>
        {
            "[]", "true", "1", "\"account\"", "{", valid + "{}", valid + " garbage",
            valid.Replace("\"Staff\":[", "\"Extra\":0,\"Staff\":["),
            valid.Replace("\"Level\":2", "\"Extra\":0,\"Level\":2"),
            valid.Replace("\"Version\":1", "\"Version\":1,\"Version\":1"),
            valid.Replace("\"Id\":\"STAFF01\"", "\"Id\":\"STAFF01\",\"Id\":\"STAFF01\""),
            valid.Replace("\"Level\":2", "\"Level\":2,\"Level\":2"),
            valid.Replace("\"PandaTokens\":55", "\"PandaTokens\":55,\"PandaTokens\":55"),
            valid.Replace("{\"Id\":\"STAFF01\",\"Level\":2}", "null"),
            valid.Replace("{\"Id\":\"STAFF01\",\"Level\":2}", "true"),
            valid.Replace("{\"Id\":\"STAFF01\",\"Level\":2}", "{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"STAFF01\",\"Level\":3}")
        };
        foreach (string value in new[] { "null", "true", "\"1\"", "1.0", "2147483648", "[]", "{}" })
            invalidJson.Add(valid.Replace("\"Version\":1", "\"Version\":" + value));
        foreach (string value in new[] { "null", "true", "0", "-1", "\"2\"", "2.0", "2147483648", "[]", "{}" })
            invalidJson.Add(valid.Replace("\"Level\":2", "\"Level\":" + value));
        foreach (string value in new[] { "null", "false", "-1", "\"55\"", "55.0", "9223372036854775808", "[]", "{}" })
            invalidJson.Add(valid.Replace("\"PandaTokens\":55", "\"PandaTokens\":" + value));
        foreach (string value in new[] { "null", "true", "1", "\"\"", "\" A\"", "\"A B\"", "\"A\\tB\"", "\"A\\u00a0B\"", "[]", "{}" })
            invalidJson.Add(valid.Replace("\"Id\":\"STAFF01\"", "\"Id\":" + value));
        foreach (string value in new[] { "null", "true", "1", "\"[]\"", "{}" })
            invalidJson.Add(valid.Replace("[{\"Id\":\"STAFF01\",\"Level\":2}]", value));
        foreach (string json in invalidJson)
            AssertReadFailure(json, StaffAccountSaveReadStatus.InvalidData);
        foreach (int version in new[] { -1, 0, 2, int.MaxValue })
            AssertReadFailure(valid.Replace("\"Version\":1", "\"Version\":" + version),
                StaffAccountSaveReadStatus.UnsupportedVersion);

        var invalidModels = new List<StaffAccountSaveData>
        {
            null, new StaffAccountSaveData(2, new StaffAccountStaffRecord[0], 0),
            new StaffAccountSaveData(1, null, 0),
            new StaffAccountSaveData(1, new StaffAccountStaffRecord[] { null }, 0),
            new StaffAccountSaveData(1, new StaffAccountStaffRecord[0], -1),
            new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord("A", 1), new StaffAccountStaffRecord("A", 2) }, 0)
        };
        foreach (string id in new[] { null, "", " ", "A B", "A\tB", "A\u00a0B" })
            invalidModels.Add(new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord(id, 1) }, 0));
        foreach (int level in new[] { 0, -1 })
            invalidModels.Add(new StaffAccountSaveData(1, new[] { new StaffAccountStaffRecord("A", level) }, 0));
        foreach (StaffAccountSaveData data in invalidModels)
        {
            string before = JsonConvert.SerializeObject(data);
            Assert.That(StaffAccountSaveConverter.TrySerialize(data, out string json, out string error), Is.False);
            Assert.That(json, Is.Null, "Invalid input leaked a partial JSON payload");
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(JsonConvert.SerializeObject(data), Is.EqualTo(before));
        }
    }

    private static void AssertRoundTrip(StaffAccountSaveData data, string expectedJson)
    {
        string modelBefore = JsonConvert.SerializeObject(data);
        var fixture = JObject.Parse(expectedJson);
        JToken fixtureBefore = fixture.DeepClone();
        string inputJson = fixture.ToString(Formatting.None);
        Assert.That(StaffAccountSaveConverter.TrySerialize(data, out string json, out string error), Is.True, error);
        Assert.That(JToken.DeepEquals(JObject.Parse(json), fixture), Is.True);
        Assert.That(LitJson.JsonMapper.ToObject(json)["PandaTokens"].ToString(),
            Is.EqualTo(data.PandaTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "Existing JSON reader lost integer precision");
        StaffAccountSaveReadResult read = StaffAccountSaveConverter.Read(json);
        Assert.That(read.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success), read.Error);
        Assert.That(read.Data, Is.Not.Null);
        Assert.That(read.Data.Version, Is.EqualTo(data.Version));
        Assert.That(read.Data.PandaTokens, Is.EqualTo(data.PandaTokens));
        CollectionAssert.AreEqual(data.Staff.Select(item => item.Id), read.Data.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(data.Staff.Select(item => item.Level), read.Data.Staff.Select(item => item.Level));
        Assert.That(StaffAccountSaveConverter.TrySerialize(read.Data, out string again, out error), Is.True, error);
        Assert.That(again, Is.EqualTo(json), "Read/write changed the canonical serialization");
        Assert.That(JsonConvert.SerializeObject(data), Is.EqualTo(modelBefore));
        Assert.That(inputJson, Is.EqualTo(fixtureBefore.ToString(Formatting.None)));
        Assert.That(JToken.DeepEquals(fixture, fixtureBefore), Is.True);
    }

    private static void AssertReadFailure(string json, StaffAccountSaveReadStatus status)
    {
        string before = json;
        StaffAccountSaveReadResult result = StaffAccountSaveConverter.Read(json);
        Assert.That(result.Status, Is.EqualTo(status), "Input: " + (json ?? "<absent>"));
        Assert.That(result.Data, Is.Null, "Rejected input leaked partial account data");
        if (status != StaffAccountSaveReadStatus.Missing)
            Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Is.EqualTo(before));
    }
}
#endif
