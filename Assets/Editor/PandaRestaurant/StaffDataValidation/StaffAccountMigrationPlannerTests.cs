#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Random = UnityEngine.Random;

public class StaffAccountMigrationPlannerTests
{
    private Random.State _randomState;
    private string _userState;
    private int _mutationEvents;
    private Action _mutationHandler;
    private Action<ERestaurantFloorType, EquipStaffType> _equipHandler;

    [SetUp]
    public void SetUp()
    {
        _randomState = Random.state;
        _userState = ReadUserState();
        _mutationEvents = 0;
        _mutationHandler = () => _mutationEvents++;
        _equipHandler = (floor, type) => _mutationEvents++;
        UserInfo.OnChangeDiaHandler += _mutationHandler;
        UserInfo.OnChangeMoneyHandler += _mutationHandler;
        UserInfo.OnChangeSkinTokenHandler += _mutationHandler;
        UserInfo.OnGiveStaffHandler += _mutationHandler;
        UserInfo.OnUpgradeStaffHandler += _mutationHandler;
        UserInfo.OnUseGachaMachineHandler += _mutationHandler;
        UserInfo.OnChangeStaffHandler += _equipHandler;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState), "Unity Random state changed");
            Assert.That(ReadUserState(), Is.EqualTo(_userState), "Actual user state changed");
            Assert.That(_mutationEvents, Is.Zero, "User mutation event was raised");
        }
        finally
        {
            UserInfo.OnChangeDiaHandler -= _mutationHandler;
            UserInfo.OnChangeMoneyHandler -= _mutationHandler;
            UserInfo.OnChangeSkinTokenHandler -= _mutationHandler;
            UserInfo.OnGiveStaffHandler -= _mutationHandler;
            UserInfo.OnUpgradeStaffHandler -= _mutationHandler;
            UserInfo.OnUseGachaMachineHandler -= _mutationHandler;
            UserInfo.OnChangeStaffHandler -= _equipHandler;
        }
    }

    [Test]
    public void NormalSources_MergeEqualLevelsInExplicitScopeWithoutChoosingSkins()
    {
        var required = new List<EStage> { EStage.Stage2, EStage.Stage1 };
        SaveStaffData firstA = Staff("A", 2, "SKIN_STAGE2");
        var sources = new List<StaffStageOwnershipSnapshot>
        {
            Source(EStage.Stage1, Staff("A", 2, "SKIN_STAGE1"), Staff("B", 1)),
            new StaffStageOwnershipSnapshot(EStage.Stage3, false, false, null),
            Source(EStage.Stage2, firstA, Staff("C", 3))
        };
        var definitions = Definitions();
        definitions.Add("CATALOG_ONLY", 5);

        StaffAccountMigrationPlan plan = Calculate(required, sources, definitions);
        Assert.That(plan.CanMigrate, Is.True);
        Assert.That(plan.Issues, Is.Empty);
        CollectionAssert.AreEqual(new[] { "A", "C", "B" }, plan.Candidates.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, plan.Candidates.Select(item => item.Level));
        Assert.That(typeof(StaffAccountMigrationCandidate).GetProperty("SkinId"), Is.Null);
        AssertReadOnly(plan.Candidates);
        AssertReadOnly(plan.Issues);

        // Mutate only disposable fixture inputs after the planner's immutability check.
        firstA.LevelUp();
        firstA.SetSkinId("AFTER");
        required.Clear();
        sources.Clear();
        definitions.Clear();
        CollectionAssert.AreEqual(new[] { "A", "C", "B" }, plan.Candidates.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, plan.Candidates.Select(item => item.Level));
    }

    [Test]
    public void ConflictingLevels_ReportEveryOriginalOccurrenceAndNoPartialCandidates()
    {
        SaveStaffData firstA = Staff("A", 2, "SKIN_1");
        SaveStaffData repeatedA = Staff("A", 2, "SKIN_1_REPEAT");
        SaveStaffData conflictingA = Staff("A", 4, "SKIN_2");
        var sources = new[]
        {
            Source(EStage.Stage1, firstA, Staff("B", 1), repeatedA),
            Source(EStage.Stage2, conflictingA)
        };
        StaffAccountMigrationPlan plan = Calculate(
            new[] { EStage.Stage1, EStage.Stage2 }, sources, Definitions());

        Assert.That(plan.CanMigrate, Is.False);
        Assert.That(plan.Candidates, Is.Null, "Valid B must not leak a partial migration");
        Assert.That(plan.Issues.Count, Is.EqualTo(1));
        StaffAccountMigrationIssue issue = plan.Issues[0];
        Assert.That(issue.Code, Is.EqualTo(StaffAccountMigrationIssueCode.LevelConflict));
        Assert.That(issue.Message, Is.Not.Null.And.Not.Empty);
        CollectionAssert.AreEqual(new[] { EStage.Stage1, EStage.Stage1, EStage.Stage2 },
            issue.Records.Select(record => record.Stage));
        CollectionAssert.AreEqual(new[] { 0, 2, 0 }, issue.Records.Select(record => record.RecordIndex));
        CollectionAssert.AreEqual(new[] { "A", "A", "A" }, issue.Records.Select(record => record.StaffId));
        CollectionAssert.AreEqual(new int?[] { 2, 2, 4 }, issue.Records.Select(record => record.Level));
        CollectionAssert.AreEqual(new[] { "SKIN_1", "SKIN_1_REPEAT", "SKIN_2" },
            issue.Records.Select(record => record.SkinId));
        AssertReadOnly(issue.Records);
        firstA.LevelUp();
        conflictingA.SetSkinId("AFTER");
        CollectionAssert.AreEqual(new int?[] { 2, 2, 4 }, issue.Records.Select(record => record.Level));
        Assert.That(issue.Records[2].SkinId, Is.EqualTo("SKIN_2"));
    }

    [Test]
    public void SourceValidation_RejectsIncompleteOrInvalidRawDataWithoutRepairingIt()
    {
        var required = new[] { EStage.Stage1 };
        var validSource = new[] { Source(EStage.Stage1, Staff("A", 2)) };
        var definitions = Definitions();
        Fail(null, validSource, definitions, StaffAccountMigrationIssueCode.InvalidScope);
        Fail(new EStage[0], validSource, definitions, StaffAccountMigrationIssueCode.InvalidScope);
        foreach (EStage invalidStage in new[] { (EStage)(-1), EStage.Length, (EStage)999 })
            Fail(new[] { invalidStage }, validSource, definitions, StaffAccountMigrationIssueCode.InvalidScope);
        Fail(required, null, definitions, StaffAccountMigrationIssueCode.MissingSources);
        Fail(required, validSource, null, StaffAccountMigrationIssueCode.MissingStaffDefinitions);
        Fail(required, new StaffStageOwnershipSnapshot[0], definitions, StaffAccountMigrationIssueCode.MissingSource);
        Fail(required, new[] { Source(EStage.Stage2) }, definitions, StaffAccountMigrationIssueCode.MissingSource);
        Fail(required, new[] { validSource[0], validSource[0] }, definitions, StaffAccountMigrationIssueCode.DuplicateSource);
        Fail(required, new[] { validSource[0], null }, definitions, StaffAccountMigrationIssueCode.InvalidSource);
        Fail(required, new[] { new StaffStageOwnershipSnapshot(EStage.Stage1, false, true, new SaveStaffData[0]) },
            definitions, StaffAccountMigrationIssueCode.SourceNotReady);
        Fail(required, new[] { new StaffStageOwnershipSnapshot(EStage.Stage1, true, false, new SaveStaffData[0]) },
            definitions, StaffAccountMigrationIssueCode.UnverifiedRawSource);
        Fail(required, new[] { new StaffStageOwnershipSnapshot(EStage.Stage1, true, true, null) }, definitions);
        StaffAccountMigrationPlan nullRecord = Fail(required, new[] { Source(EStage.Stage1, (SaveStaffData)null) },
            definitions, StaffAccountMigrationIssueCode.InvalidStaffRecord);
        var rawNull = nullRecord.Issues.First(issue => issue.Code == StaffAccountMigrationIssueCode.InvalidStaffRecord).Records[0];
        Assert.That(rawNull.Stage, Is.EqualTo(EStage.Stage1));
        Assert.That(rawNull.RecordIndex, Is.Zero);
        Assert.That(rawNull.StaffId, Is.Null);
        Assert.That(rawNull.Level, Is.Null);

        foreach (string invalidId in new[] { null, "", " ", " A", "A ", "A B", "A\tB", "A\u00a0B" })
            Fail(required, new[] { Source(EStage.Stage1, Staff(invalidId, 1)) }, definitions,
                StaffAccountMigrationIssueCode.InvalidStaffId);
        Fail(required, new[] { Source(EStage.Stage1, Staff("UNKNOWN", 1)) }, definitions,
            StaffAccountMigrationIssueCode.UnknownStaff);
        var caseInsensitiveDefinitions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "A", 5 } };
        Fail(required, new[] { Source(EStage.Stage1, Staff("a", 1)) }, caseInsensitiveDefinitions,
            StaffAccountMigrationIssueCode.UnknownStaff);
        foreach (int invalidLevel in new[] { 0, -1, 6, int.MaxValue })
            Fail(required, new[] { Source(EStage.Stage1, Staff("A", invalidLevel)) }, definitions,
                StaffAccountMigrationIssueCode.InvalidLevel);
        Fail(required, new[] { Source(EStage.Stage1, Staff("LIMITED", 3)) }, definitions,
            StaffAccountMigrationIssueCode.InvalidLevel);
        foreach (int invalidMaximum in new[] { 0, -1, StaffData.OfficialMaxLevel + 1 })
            Fail(required, validSource, new Dictionary<string, int> { { "A", invalidMaximum } },
                StaffAccountMigrationIssueCode.InvalidStaffDefinition);
        SaveStaffData legacyStaff02 = Staff("STAFF02", 6, "ORIGINAL_SKIN");
        StaffAccountMigrationPlan legacyPlan = Fail(required, new[] { Source(EStage.Stage1, legacyStaff02) }, definitions,
            StaffAccountMigrationIssueCode.InvalidLevel);
        Assert.That(legacyStaff02.Level, Is.EqualTo(6), "Planner must not apply A2 or clamp");
        Assert.That(legacyStaff02.SkinId, Is.EqualTo("ORIGINAL_SKIN"));
        StaffMigrationSourceRecord legacyEvidence = legacyPlan.Issues
            .First(issue => issue.Code == StaffAccountMigrationIssueCode.InvalidLevel).Records[0];
        Assert.That(legacyEvidence.Stage, Is.EqualTo(EStage.Stage1));
        Assert.That(legacyEvidence.StaffId, Is.EqualTo("STAFF02"));
        Assert.That(legacyEvidence.Level, Is.EqualTo(6));
        Assert.That(legacyEvidence.SkinId, Is.EqualTo("ORIGINAL_SKIN"));

        // Ready + verified + explicitly empty is distinct from missing/unready sources.
        StaffAccountMigrationPlan empty = Calculate(required, new[] { Source(EStage.Stage1) }, definitions);
        Assert.That(empty.CanMigrate, Is.True);
        Assert.That(empty.Candidates, Is.Not.Null.And.Empty);
        Assert.That(empty.Issues, Is.Empty);
        AssertReadOnly(empty.Candidates);
        StaffAccountMigrationPlan oneStage = Calculate(new[] { EStage.Stage1, EStage.Stage1 }, validSource, definitions);
        Assert.That(oneStage.CanMigrate, Is.True, "Unspecified Stage2/Stage3 are not required");
        Assert.That(oneStage.Candidates.Count, Is.EqualTo(1));
        StaffAccountMigrationPlan levelBoundary = Calculate(required,
            new[] { Source(EStage.Stage1, Staff("LIMITED", 2)) }, definitions);
        Assert.That(levelBoundary.CanMigrate, Is.True);
        Assert.That(levelBoundary.Candidates[0].Level, Is.EqualTo(2));
    }

    private static StaffAccountMigrationPlan Calculate(
        IReadOnlyCollection<EStage> required, IReadOnlyList<StaffStageOwnershipSnapshot> sources,
        IReadOnlyDictionary<string, int> definitions)
    {
        string before = JsonConvert.SerializeObject(new { required, sources, definitions });
        Random.State randomBefore = Random.state;
        try
        {
            return StaffAccountMigrationPlanner.Calculate(required, sources, definitions);
        }
        finally
        {
            Assert.That(JsonConvert.SerializeObject(new { required, sources, definitions }), Is.EqualTo(before),
                "Planner changed raw inputs, level, skin or definitions");
            Assert.That(Random.state, Is.EqualTo(randomBefore), "Planner consumed randomness");
        }
    }

    private static StaffAccountMigrationPlan Fail(
        IReadOnlyCollection<EStage> required, IReadOnlyList<StaffStageOwnershipSnapshot> sources,
        IReadOnlyDictionary<string, int> definitions, StaffAccountMigrationIssueCode? expected = null)
    {
        StaffAccountMigrationPlan plan = Calculate(required, sources, definitions);
        Assert.That(plan.CanMigrate, Is.False);
        Assert.That(plan.Candidates, Is.Null);
        Assert.That(plan.Issues, Is.Not.Empty);
        Assert.That(plan.Issues.All(issue => !string.IsNullOrWhiteSpace(issue.Message)), Is.True);
        if (expected.HasValue)
            Assert.That(plan.Issues.Any(issue => issue.Code == expected.Value), Is.True, expected.Value.ToString());
        return plan;
    }

    private static StaffStageOwnershipSnapshot Source(EStage stage, params SaveStaffData[] records)
    {
        return new StaffStageOwnershipSnapshot(stage, true, true, records);
    }

    private static SaveStaffData Staff(string id, int level, string skinId = "")
    {
        var staff = new SaveStaffData(id, level);
        staff.SetSkinId(skinId);
        return staff;
    }

    private static Dictionary<string, int> Definitions()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "A", 5 }, { "B", 5 }, { "C", 5 }, { "STAFF02", 5 }, { "LIMITED", 2 }
        };
    }

    private static void AssertReadOnly(object collection)
    {
        if (collection is IList list)
        {
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list.Add(null));
        }
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
                .Select(floor => new
                {
                    Floor = floor.Key,
                    Staff = floor.Value.Select(slot => new
                    {
                        Slot = slot.Key,
                        Id = slot.Value == null ? null : slot.Value.Id
                    }).ToArray()
                }).ToArray()
        }).ToArray();
        return JsonConvert.SerializeObject(new
        {
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken,
            UserInfo.TotalUseGachaMachineCount, UserInfo.CurrentStage,
            UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart,
            Stages = stageState
        });
    }
}
#endif
