#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Random = UnityEngine.Random;

public class StaffAccountLoadPlannerTests
{
    private const string Existing = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"UNKNOWN_ID\",\"Level\":6}],\"PandaTokens\":55}";
    private const string Empty = "{\"Version\":1,\"Staff\":[],\"PandaTokens\":0}";
    private Random.State _randomState;
    private string _userState;

    [SetUp]
    public void SetUp() { _randomState = Random.state; _userState = ReadUserState(); }

    [TearDown]
    public void TearDown()
    {
        Assert.That(Random.state, Is.EqualTo(_randomState), "Load planning consumed randomness");
        Assert.That(ReadUserState(), Is.EqualTo(_userState), "Load planning changed live in-memory game state");
    }

    [Test]
    public void ExistingData_WinsWithoutStageValidationAndInvalidDataNeverFallsBack()
    {
        var scope = new[] { EStage.Stage1, EStage.Stage2 };
        var conflicts = new[]
        {
            Source(EStage.Stage1, Staff("A", 2, "ONE")),
            Source(EStage.Stage2, Staff("A", 4, "TWO"))
        };
        foreach (var sources in new[] { null, conflicts })
        {
            StaffAccountLoadResult result = Plan(StaffAccountQueryStatus.SucceededFieldPresent,
                Existing, scope, sources, new Dictionary<string, int>());
            AssertState(result, StaffAccountLoadStatus.ExistingDataLoaded);
            CollectionAssert.AreEqual(new[] { "STAFF01", "UNKNOWN_ID" }, result.ExistingData.Staff.Select(item => item.Id));
            CollectionAssert.AreEqual(new[] { 2, 6 }, result.ExistingData.Staff.Select(item => item.Level));
            Assert.That(result.ExistingData.PandaTokens, Is.EqualTo(55));
        }
        StaffAccountLoadResult empty = Plan(StaffAccountQueryStatus.SucceededFieldPresent, Empty);
        AssertState(empty, StaffAccountLoadStatus.ExistingDataLoaded);
        Assert.That(empty.ExistingData.Staff, Is.Empty);
        Assert.That(empty.ExistingData.PandaTokens, Is.Zero);

        var validSources = new[] { Source(EStage.Stage1, Staff("A", 2, "RAW")) };
        foreach (string corrupt in new[] { "", "null", "{", "{}" })
            AssertState(Plan(StaffAccountQueryStatus.SucceededFieldPresent, corrupt,
                new[] { EStage.Stage1 }, validSources, Definitions()), StaffAccountLoadStatus.InvalidExistingData);
        AssertState(Plan(StaffAccountQueryStatus.SucceededFieldPresent, Empty.Replace("\"Version\":1", "\"Version\":2"),
            new[] { EStage.Stage1 }, validSources, Definitions()), StaffAccountLoadStatus.UnsupportedVersion);
    }

    [Test]
    public void ConfirmedAbsence_ProducesOnlyUnsavedCandidatesOrOriginalMigrationDiagnostics()
    {
        var scope = new[] { EStage.Stage2, EStage.Stage1 };
        var sources = new[]
        {
            Source(EStage.Stage1, Staff("A", 2, "SKIN_ONE"), Staff("B", 1, "SKIN_B")),
            Source(EStage.Stage2, Staff("A", 2, "SKIN_TWO")),
            new StaffStageOwnershipSnapshot(EStage.Stage3, false, false, null)
        };
        StaffAccountLoadResult candidate = Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            scope, sources, Definitions());
        AssertState(candidate, StaffAccountLoadStatus.UnsavedMigrationCandidate);
        Assert.That(candidate.MigrationCandidate.Version, Is.EqualTo(StaffAccountSaveConverter.CurrentVersion));
        CollectionAssert.AreEqual(new[] { "A", "B" }, candidate.MigrationCandidate.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 1 }, candidate.MigrationCandidate.Staff.Select(item => item.Level));
        Assert.That(candidate.MigrationCandidate.PandaTokens, Is.Zero);
        Assert.That(typeof(StaffAccountStaffRecord).GetProperty("SkinId"), Is.Null);
        StaffAccountLoadResult empty = Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            new[] { EStage.Stage1 }, new[] { Source(EStage.Stage1) }, new Dictionary<string, int>());
        AssertState(empty, StaffAccountLoadStatus.UnsavedMigrationCandidate);
        Assert.That(empty.MigrationCandidate.Staff, Is.Empty);
        Assert.That(empty.MigrationCandidate.PandaTokens, Is.Zero);

        StaffAccountLoadResult conflict = Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            new[] { EStage.Stage1, EStage.Stage2 }, new[]
            {
                Source(EStage.Stage1, Staff("A", 2, "ONE"), Staff("B", 1, "UNRELATED")),
                Source(EStage.Stage2, Staff("A", 4, "TWO"))
            }, Definitions());
        StaffAccountMigrationIssue issue = AssertBlocked(conflict, StaffAccountMigrationIssueCode.LevelConflict);
        CollectionAssert.AreEqual(new[] { EStage.Stage1, EStage.Stage2 }, issue.Records.Select(record => record.Stage));
        CollectionAssert.AreEqual(new[] { 0, 0 }, issue.Records.Select(record => record.RecordIndex));
        CollectionAssert.AreEqual(new[] { "A", "A" }, issue.Records.Select(record => record.StaffId));
        CollectionAssert.AreEqual(new int?[] { 2, 4 }, issue.Records.Select(record => record.Level));
        CollectionAssert.AreEqual(new[] { "ONE", "TWO" }, issue.Records.Select(record => record.SkinId));
        AssertReadOnly(conflict.MigrationIssues);
        AssertReadOnly(issue.Records);

        AssertBlocked(Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            new[] { EStage.Stage1 }, new StaffStageOwnershipSnapshot[0], Definitions()),
            StaffAccountMigrationIssueCode.MissingSource);
        AssertBlocked(Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            null, sources, Definitions()), StaffAccountMigrationIssueCode.InvalidScope);
        AssertBlocked(Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
            new[] { EStage.Stage1 }, sources, null), StaffAccountMigrationIssueCode.MissingStaffDefinitions);
        foreach (bool ready in new[] { false, true })
        {
            SaveStaffData original = Staff("STAFF02", 6, "UNMODIFIED_SKIN");
            var source = new StaffStageOwnershipSnapshot(EStage.Stage1, ready, !ready, new[] { original });
            StaffAccountMigrationIssue raw = AssertBlocked(Plan(StaffAccountQueryStatus.SucceededFieldAbsent,
                null, new[] { EStage.Stage1 }, new[] { source }, Definitions()), ready
                ? StaffAccountMigrationIssueCode.UnverifiedRawSource : StaffAccountMigrationIssueCode.SourceNotReady);
            Assert.That(raw.Records[0].Stage, Is.EqualTo(EStage.Stage1));
            Assert.That(raw.Records[0].RecordIndex, Is.Zero);
            Assert.That(raw.Records[0].StaffId, Is.EqualTo("STAFF02"));
            Assert.That(raw.Records[0].Level, Is.EqualTo(6));
            Assert.That(raw.Records[0].SkinId, Is.EqualTo("UNMODIFIED_SKIN"));
            Assert.That(original.Level, Is.EqualTo(6));
            Assert.That(original.SkinId, Is.EqualTo("UNMODIFIED_SKIN"));
        }
    }

    [Test]
    public void QueryStatesAndContradictions_NeverReadStageInputsOrCreateCandidates()
    {
        // Any Count/index/enumeration access throws: unrelated query paths must not inspect Stage data.
        var untouched = new ThrowingStageSources();
        StaffAccountLoadResult WithoutMigration(StaffAccountQueryStatus query, string json) =>
            StaffAccountLoadPlanner.Plan(query, json, new[] { EStage.Stage1 }, untouched, Definitions());
        foreach (string json in new[] { null, Existing, "{corrupt" })
        {
            AssertState(WithoutMigration(StaffAccountQueryStatus.NotCompleted, json), StaffAccountLoadStatus.QueryPending);
            AssertState(WithoutMigration(StaffAccountQueryStatus.Failed, json), StaffAccountLoadStatus.QueryFailed);
            AssertState(WithoutMigration((StaffAccountQueryStatus)999, json), StaffAccountLoadStatus.InvalidQueryInput);
        }
        AssertState(WithoutMigration(StaffAccountQueryStatus.SucceededFieldPresent, null), StaffAccountLoadStatus.InvalidExistingData);
        foreach (string json in new[] { "", " ", "null", Existing })
            AssertState(WithoutMigration(StaffAccountQueryStatus.SucceededFieldAbsent, json), StaffAccountLoadStatus.InvalidQueryInput);
        AssertState(WithoutMigration(StaffAccountQueryStatus.SucceededFieldPresent, Existing), StaffAccountLoadStatus.ExistingDataLoaded);
        AssertState(WithoutMigration(StaffAccountQueryStatus.SucceededFieldPresent, "{corrupt"), StaffAccountLoadStatus.InvalidExistingData);
        AssertState(WithoutMigration(StaffAccountQueryStatus.SucceededFieldPresent,
            Empty.Replace("\"Version\":1", "\"Version\":2")), StaffAccountLoadStatus.UnsupportedVersion);
    }

    private static StaffAccountLoadResult Plan(StaffAccountQueryStatus query, string json,
        IReadOnlyCollection<EStage> required = null, IReadOnlyList<StaffStageOwnershipSnapshot> sources = null,
        IReadOnlyDictionary<string, int> definitions = null)
    {
        string before = JsonConvert.SerializeObject(new { json, required, sources, definitions });
        try { return StaffAccountLoadPlanner.Plan(query, json, required, sources, definitions); }
        finally
        {
            Assert.That(JsonConvert.SerializeObject(new { json, required, sources, definitions }), Is.EqualTo(before),
                "Planning changed input JSON, Stage records, levels, skins or validation evidence");
        }
    }

    private static void AssertState(StaffAccountLoadResult result, StaffAccountLoadStatus status)
    {
        Assert.That(result.Status, Is.EqualTo(status), result.Error);
        Assert.That(result.ExistingData != null, Is.EqualTo(status == StaffAccountLoadStatus.ExistingDataLoaded));
        Assert.That(result.MigrationCandidate != null, Is.EqualTo(status == StaffAccountLoadStatus.UnsavedMigrationCandidate));
        if (status != StaffAccountLoadStatus.MigrationBlocked) Assert.That(result.MigrationIssues, Is.Not.Null.And.Empty);
    }

    private static StaffAccountMigrationIssue AssertBlocked(StaffAccountLoadResult result, StaffAccountMigrationIssueCode code)
    {
        AssertState(result, StaffAccountLoadStatus.MigrationBlocked);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
        Assert.That(result.MigrationIssues.Any(issue => issue.Code == code), Is.True, code.ToString());
        StaffAccountMigrationIssue issue = result.MigrationIssues.First(item => item.Code == code);
        Assert.That(issue.Message, Is.Not.Null.And.Not.Empty);
        return issue;
    }

    private static StaffStageOwnershipSnapshot Source(EStage stage, params SaveStaffData[] records) =>
        new StaffStageOwnershipSnapshot(stage, true, true, records);

    private static SaveStaffData Staff(string id, int level, string skin)
    {
        var staff = new SaveStaffData(id, level);
        staff.SetSkinId(skin);
        return staff;
    }

    private static Dictionary<string, int> Definitions() => new Dictionary<string, int>(StringComparer.Ordinal)
        { { "A", 5 }, { "B", 5 }, { "STAFF02", 5 }, { "CATALOG_ONLY", 5 } };

    private static void AssertReadOnly(object value)
    {
        if (value is IList list) Assert.That(list.IsReadOnly, Is.True);
    }

    private sealed class ThrowingStageSources : IReadOnlyList<StaffStageOwnershipSnapshot>
    {
        public int Count => throw new InvalidOperationException("Stage Count was read");
        public StaffStageOwnershipSnapshot this[int index] => throw new InvalidOperationException("Stage index was read");
        public IEnumerator<StaffStageOwnershipSnapshot> GetEnumerator() => throw new InvalidOperationException("Stage sources were enumerated");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static string ReadUserState()
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
            UserInfo.CurrentStage, UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState
        });
    }
}
#endif
