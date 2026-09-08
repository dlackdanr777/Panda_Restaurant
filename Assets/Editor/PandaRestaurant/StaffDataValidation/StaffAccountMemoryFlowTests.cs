#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffAccountMemoryFlowTests
{
    private readonly List<GachaStaffData> _wrappers = new List<GachaStaffData>();
    private readonly Dictionary<StaffData, string> _originalStaff = new Dictionary<StaffData, string>();
    private readonly Dictionary<GachaStaffData, string> _originalWrappers = new Dictionary<GachaStaffData, string>();
    private Random.State _randomState;

    [SetUp]
    public void SetUp() => _randomState = Random.state;

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState), "Memory flow consumed randomness");
            foreach (var original in _originalStaff)
                Assert.That(EditorJsonUtility.ToJson(original.Key), Is.EqualTo(original.Value), "Staff resource changed");
            foreach (var original in _originalWrappers)
                Assert.That(Describe(original.Key), Is.EqualTo(original.Value), "Draw wrapper changed");
        }
        finally
        {
            foreach (GachaStaffData wrapper in _wrappers)
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            _wrappers.Clear();
            _originalStaff.Clear();
            _originalWrappers.Clear();
        }
    }

    [Test]
    public void ExistingAccount_LoadApplyElevenSerializeAndReloadPreservesValuesAndInputs()
    {
        const string initialJson = "{\"Version\":1,\"Staff\":[{\"Id\":\"STAFF01\",\"Level\":2},{\"Id\":\"STAFF03\",\"Level\":4}],\"PandaTokens\":55}";
        StaffAccountLoadResult loaded = StaffAccountLoadPlanner.Plan(
            StaffAccountQueryStatus.SucceededFieldPresent, initialJson);
        Assert.That(loaded.Status, Is.EqualTo(StaffAccountLoadStatus.ExistingDataLoaded), loaded.Error);
        Assert.That(loaded.MigrationCandidate, Is.Null);
        Assert.That(loaded.MigrationIssues, Is.Empty);
        AssertAccount(loaded.ExistingData, new[] { "STAFF01", "STAFF03" }, new[] { 2, 4 }, 55);
        string loadedBefore = JsonConvert.SerializeObject(loaded);

        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        Assert.That(normal.Rank, Is.EqualTo(Rank.Normal2));
        Assert.That(rare.Rank, Is.EqualTo(Rank.Rare));
        var drawn = new[] { rare, rare }.Concat(Enumerable.Repeat(normal, 9)).ToList();
        GachaStaffData[] drawnBefore = drawn.ToArray();
        string[] drawValuesBefore = drawn.Select(Describe).ToArray();

        Assert.That(StaffGachaAccountApplyCalculator.TryCalculate(loaded.ExistingData, drawn,
            out StaffGachaAccountApplyResult applied, out string error), Is.True, error);
        Assert.That(applied.UpdatedAccount, Is.Not.SameAs(loaded.ExistingData));
        AssertAccount(applied.UpdatedAccount, new[] { "STAFF01", "STAFF03", "STAFF23" }, new[] { 2, 4, 1 }, 110);
        StaffGachaAcquisitionResult acquisition = applied.Acquisition;
        Assert.That(acquisition.Items.Count, Is.EqualTo(11));
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            acquisition.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { true }.Concat(Enumerable.Repeat(false, 10)),
            acquisition.Items.Select(item => item.IsNew));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            acquisition.Items.Select(item => item.PandaTokenReward));
        CollectionAssert.AreEqual(new[] { "STAFF23" }, acquisition.NewStaffIds);
        Assert.That(acquisition.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(acquisition.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(acquisition.TotalPandaTokens, Is.EqualTo(55));
        string appliedBefore = JsonConvert.SerializeObject(applied);

        Assert.That(StaffAccountSaveConverter.TrySerialize(applied.UpdatedAccount,
            out string updatedJson, out error), Is.True, error);
        // This second present-field input is a memory round trip, not a server save or retry.
        StaffAccountLoadResult reloaded = StaffAccountLoadPlanner.Plan(
            StaffAccountQueryStatus.SucceededFieldPresent, updatedJson);
        Assert.That(reloaded.Status, Is.EqualTo(StaffAccountLoadStatus.ExistingDataLoaded), reloaded.Error);
        Assert.That(reloaded.MigrationCandidate, Is.Null);
        Assert.That(reloaded.MigrationIssues, Is.Empty);
        AssertAccount(reloaded.ExistingData, new[] { "STAFF01", "STAFF03", "STAFF23" }, new[] { 2, 4, 1 }, 110);
        Assert.That(StaffAccountSaveConverter.TrySerialize(reloaded.ExistingData,
            out string roundTripJson, out error), Is.True, error);
        Assert.That(roundTripJson, Is.EqualTo(updatedJson));

        Assert.That(JsonConvert.SerializeObject(loaded), Is.EqualTo(loadedBefore), "Initial account changed");
        Assert.That(JsonConvert.SerializeObject(applied), Is.EqualTo(appliedBefore), "Calculated output changed");
        CollectionAssert.AreEqual(drawnBefore, drawn, "Draw order changed");
        CollectionAssert.AreEqual(drawValuesBefore, drawn.Select(Describe), "Draw values changed");
        Assert.That(StaffAccountSaveConverter.TrySerialize(loaded.ExistingData,
            out string originalJson, out error), Is.True, error);
        Assert.That(originalJson, Is.EqualTo(initialJson));
    }

    [Test]
    public void ConfirmedAbsence_MigrateSerializeReadPreservesUnsavedCandidateAndRawStages()
    {
        // Same construction as the existing migration tests: explicit scope, raw ownership and limits.
        var required = new[] { EStage.Stage2, EStage.Stage1 };
        SaveStaffData first = Staff("STAFF01", 2, "SKIN_STAGE1");
        SaveStaffData repeated = Staff("STAFF01", 2, "SKIN_STAGE2");
        var sources = new[]
        {
            new StaffStageOwnershipSnapshot(EStage.Stage1, true, true,
                new[] { first, Staff("STAFF03", 4, "SKIN_03") }),
            new StaffStageOwnershipSnapshot(EStage.Stage3, false, false, null),
            new StaffStageOwnershipSnapshot(EStage.Stage2, true, true,
                new[] { repeated, Staff("STAFF23", 1, "SKIN_23") })
        };
        var definitions = new Dictionary<string, int>
        {
            { "STAFF01", 5 }, { "STAFF03", 5 }, { "STAFF23", 5 }, { "CATALOG_ONLY", 5 }
        };
        string inputsBefore = JsonConvert.SerializeObject(new { required, sources, definitions });

        StaffAccountLoadResult planned = StaffAccountLoadPlanner.Plan(
            StaffAccountQueryStatus.SucceededFieldAbsent, null, required, sources, definitions);
        Assert.That(planned.Status, Is.EqualTo(StaffAccountLoadStatus.UnsavedMigrationCandidate), planned.Error);
        Assert.That(planned.ExistingData, Is.Null);
        Assert.That(planned.MigrationIssues, Is.Empty);
        AssertAccount(planned.MigrationCandidate, new[] { "STAFF01", "STAFF23", "STAFF03" }, new[] { 2, 1, 4 }, 0);
        Assert.That(planned.MigrationCandidate.Staff.Count(item => item.Id == "STAFF01"), Is.EqualTo(1));
        string plannedBefore = JsonConvert.SerializeObject(planned);

        Assert.That(StaffAccountSaveConverter.TrySerialize(planned.MigrationCandidate,
            out string candidateJson, out string error), Is.True, error);
        StaffAccountSaveReadResult decoded = StaffAccountSaveConverter.Read(candidateJson);
        Assert.That(decoded.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success), decoded.Error);
        AssertAccount(decoded.Data, new[] { "STAFF01", "STAFF23", "STAFF03" }, new[] { 2, 1, 4 }, 0);
        Assert.That(StaffAccountSaveConverter.TrySerialize(decoded.Data,
            out string roundTripJson, out error), Is.True, error);
        Assert.That(roundTripJson, Is.EqualTo(candidateJson));

        // Format success does not promote the original plan to persisted, migrated or gameplay-ready.
        Assert.That(planned.Status, Is.EqualTo(StaffAccountLoadStatus.UnsavedMigrationCandidate));
        Assert.That(planned.ExistingData, Is.Null);
        Assert.That(JsonConvert.SerializeObject(planned), Is.EqualTo(plannedBefore));
        Assert.That(JsonConvert.SerializeObject(new { required, sources, definitions }), Is.EqualTo(inputsBefore));
        Assert.That(first.Level, Is.EqualTo(2));
        Assert.That(repeated.Level, Is.EqualTo(2));
        Assert.That(first.SkinId, Is.EqualTo("SKIN_STAGE1"));
        Assert.That(repeated.SkinId, Is.EqualTo("SKIN_STAGE2"));
        Assert.That(typeof(StaffAccountStaffRecord).GetProperty("SkinId"), Is.Null);
    }

    private static void AssertAccount(StaffAccountSaveData account, string[] ids, int[] levels, long tokens)
    {
        Assert.That(account, Is.Not.Null);
        Assert.That(account.Version, Is.EqualTo(StaffAccountSaveConverter.CurrentVersion));
        CollectionAssert.AreEqual(ids, account.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(levels, account.Staff.Select(item => item.Level));
        Assert.That(account.PandaTokens, Is.EqualTo(tokens));
    }

    private static SaveStaffData Staff(string id, int level, string skin)
    {
        var staff = new SaveStaffData(id, level);
        staff.SetSkinId(skin);
        return staff;
    }

    private GachaStaffData LoadStaff(string id)
    {
        StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
        Assert.That(staff, Is.Not.Null, "Missing registered staff " + id);
        Assert.That(staff.Id, Is.EqualTo(id));
        _originalStaff.Add(staff, EditorJsonUtility.ToJson(staff));
        GachaStaffData wrapper = GachaStaffData.Create(staff);
        _wrappers.Add(wrapper);
        _originalWrappers.Add(wrapper, Describe(wrapper));
        return wrapper;
    }

    private static string Describe(GachaStaffData data) => JsonConvert.SerializeObject(new
    {
        Instance = data.GetInstanceID(), Fields = EditorJsonUtility.ToJson(data), Source = data.StaffData.GetInstanceID()
    });
}
#endif
