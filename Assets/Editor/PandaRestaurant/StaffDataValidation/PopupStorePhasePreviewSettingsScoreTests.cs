#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>Checks the editable score proposal and its immutable start boundary without entering Play or writing settings.</summary>
public sealed class PopupStorePhasePreviewSettingsScoreTests
{
    private byte[] _assetBefore, _legacyBefore;
    private bool _legacyExisted, _assetDirty;
    private GachaCollectionPreviewSceneWitness _workspace;
    private PopupStorePhasePreviewBalanceAsset _draft;

    [SetUp]
    public void CaptureInputs()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _assetBefore = File.ReadAllBytes(PopupStorePhasePreviewSettings.BalanceAssetPath);
        _legacyExisted = File.Exists(PopupStorePhasePreviewSettings.SettingsPath);
        _legacyBefore = _legacyExisted ? File.ReadAllBytes(PopupStorePhasePreviewSettings.SettingsPath) : null;
        _assetDirty = EditorUtility.IsDirty(Asset());
        _draft = ScriptableObject.CreateInstance<PopupStorePhasePreviewBalanceAsset>();
    }

    [TearDown]
    public void PreserveInputs()
    {
        if (_draft != null) UnityEngine.Object.DestroyImmediate(_draft);
        CollectionAssert.AreEqual(_assetBefore, File.ReadAllBytes(PopupStorePhasePreviewSettings.BalanceAssetPath));
        Assert.That(EditorUtility.IsDirty(Asset()), Is.EqualTo(_assetDirty));
        Assert.That(File.Exists(PopupStorePhasePreviewSettings.SettingsPath), Is.EqualTo(_legacyExisted));
        if (_legacyExisted) CollectionAssert.AreEqual(_legacyBefore, File.ReadAllBytes(PopupStorePhasePreviewSettings.SettingsPath));
        _workspace.AssertUnchanged("cumulative-score preview settings validation");
    }

    [TestCase(0, 200, 22, 22, 5, 3.2)]
    [TestCase(1, 650, 27, 20, 8, 3.0)]
    [TestCase(2, 1200, 30, 18, 12, 2.8)]
    [TestCase(3, 1900, 34, 16, 16, 2.6)]
    [TestCase(4, 2800, 38, 14, 24, 2.4)]
    [TestCase(5, 3900, 42, 12, 32, 2.2)]
    public void AuthoredInspectorProposalUsesExplicitCumulativeGoalsAndFasterPatience(int index, int target,
        double seconds, double patience, int plushCount, double interval)
    {
        Assert.That(Asset().TryFreeze(Catalog(), out _, out var profile, out _, out var error), Is.True, error);
        Assert.That(profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
        var phase = profile.Phases[index];
        Assert.That(phase.TargetScore, Is.EqualTo(target));
        Assert.That(phase.TargetSuccessfulOrders, Is.Zero, "Count targets are not silently converted to points.");
        Assert.That(phase.TimeLimitSeconds, Is.EqualTo(seconds));
        Assert.That(phase.CustomerPatienceSeconds, Is.EqualTo(patience));
        Assert.That(phase.EntryIntervalSeconds, Is.EqualTo(interval));
        Assert.That(Catalog().TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out _), Is.True);
        CollectionAssert.AreEqual(plushes.Take(plushCount).Select(p => p.Id), phase.AllowedPlushIds);
    }

    [TestCase(3, 100)]
    [TestCase(4, 150)]
    [TestCase(5, 220)]
    public void AuthoredPointPolicyFreezesApprovedPreviewPoints(int materials, int expected)
    {
        Assert.That(Asset().TryFreeze(Catalog(), out _, out _, out var policy, out var error), Is.True, error);
        Assert.That(policy.TryGetPoints(materials, out int actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ExplicitStartReadsInspectorAssetAndDoesNotMigrateOrWriteLegacyTempFile()
    {
        Assert.That(PopupStorePhasePreviewSettings.TryLoadForExplicitStart(Catalog(), out string json,
            out var profile, out var policy, out var error), Is.True, error);
        Assert.That(json, Is.Not.Empty);
        Assert.That(profile.ProfileId, Is.EqualTo(Asset().Settings.profileId));
        Assert.That(profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
        Assert.That(profile.Phases[0].TargetScore, Is.EqualTo(Asset().Settings.stages[0].targetScore));
        Assert.That(policy.FourMaterialPoints, Is.EqualTo(Asset().Settings.points.fourMaterials));
    }

    [Test]
    public void FrozenProfileAndPointPolicyIgnoreLaterInspectorEdits()
    {
        Assert.That(_draft.TryFreeze(Catalog(), out string json, out var profile, out var policy, out var error), Is.True, error);
        _draft.Settings.stages[0].targetScore = 250;
        _draft.Settings.stages[0].onePlushPatienceSeconds = 21;
        _draft.Settings.points.fourMaterials = 160;
        _draft.Settings.profileId = "NEXT_PREVIEW";
        Assert.That(profile.Phases[0].TargetScore, Is.EqualTo(200));
        Assert.That(profile.Phases[0].CustomerPatienceSeconds, Is.EqualTo(22));
        Assert.That(policy.FourMaterialPoints, Is.EqualTo(150));
        Assert.That(PopupStorePhasePreviewSettings.TryParseSnapshot(json, Catalog(), out var resumed,
            out var resumedPolicy, out error), Is.True, error);
        Assert.That(resumed.Phases[0].TargetScore, Is.EqualTo(200));
        Assert.That(resumed.ProfileId, Is.EqualTo(profile.ProfileId));
        Assert.That(resumedPolicy.FourMaterialPoints, Is.EqualTo(150));
    }

    [Test]
    public void SubsequentExplicitFreezeAdoptsValidEditsWithoutChangingEarlierSnapshot()
    {
        Assert.That(_draft.TryFreeze(Catalog(), out _, out var first, out var firstPolicy, out var error), Is.True, error);
        _draft.Settings.stages[0].targetScore = 250;
        _draft.Settings.points.fourMaterials = 160;
        Assert.That(_draft.TryFreeze(Catalog(), out _, out var next, out var nextPolicy, out error), Is.True, error);
        Assert.That(next.Phases[0].TargetScore, Is.EqualTo(250));
        Assert.That(nextPolicy.FourMaterialPoints, Is.EqualTo(160));
        Assert.That(first.Phases[0].TargetScore, Is.EqualTo(200));
        Assert.That(firstPolicy.FourMaterialPoints, Is.EqualTo(150));
    }

    [TestCase(0, 150, 220)]
    [TestCase(-1, 150, 220)]
    [TestCase(100, 100, 220)]
    [TestCase(100, 150, 150)]
    [TestCase(200, 150, 220)]
    public void InvalidPointsRejectWholeStartWithoutCorrectingInspectorValues(int three, int four, int five)
    {
        _draft.Settings.points.threeMaterials = three;
        _draft.Settings.points.fourMaterials = four;
        _draft.Settings.points.fiveMaterials = five;
        RejectDraft();
        Assert.That(_draft.Settings.points.threeMaterials, Is.EqualTo(three));
        Assert.That(_draft.Settings.points.fourMaterials, Is.EqualTo(four));
        Assert.That(_draft.Settings.points.fiveMaterials, Is.EqualTo(five));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(650)]
    public void InvalidOrNonIncreasingCGoalCannotStart(int target)
    {
        _draft.Settings.stages[0].targetScore = target;
        RejectDraft(); Assert.That(_draft.Settings.stages[0].targetScore, Is.EqualTo(target));
    }

    [Test]
    public void MissingCumulativePointsCannotFallBackToOrdinaryPoints()
    {
        _draft.Settings.points = null; RejectDraft();
    }

    [Test]
    public void LegacyFactoryRetainsCountTargetsAndOrdinaryPointPolicy()
    {
        var v0 = PopupStorePhasePreviewSettings.CreateProposalV0();
        Assert.That(v0.TryCreateSnapshot(Catalog(), out var profile, out var policy, out var error), Is.True, error);
        Assert.That(profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.SuccessfulOrders));
        CollectionAssert.AreEqual(new[] { 1, 2, 2, 3, 3, 4 }, profile.Phases.Select(p => p.TargetSuccessfulOrders));
        Assert.That(profile.Phases.All(p => p.TargetScore == 0), Is.True);
        Assert.That(policy, Is.SameAs(PopupStoreScorePolicySnapshot.Ordinary));
        Assert.That(policy.FourMaterialPoints, Is.EqualTo(140));
        Assert.That(policy.FiveMaterialPoints, Is.EqualTo(180));
    }

    [Test]
    public void HistoricalJsonWithoutNewFieldsRemainsCountBasedAndIsNotRewritten()
    {
        string json = LegacyJson();
        Assert.That(PopupStorePhasePreviewSettings.TryParseSnapshot(json, Catalog(), out var profile,
            out var policy, out var error), Is.True, error);
        Assert.That(profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.SuccessfulOrders));
        Assert.That(profile.Phases[0].TargetSuccessfulOrders, Is.EqualTo(1));
        Assert.That(profile.Phases[0].TargetScore, Is.Zero);
        Assert.That(policy, Is.SameAs(PopupStoreScorePolicySnapshot.Ordinary));
        Assert.That(json, Does.Not.Contain("targetScore").And.Not.Contain("goalMetric"));
    }

    [Test]
    public void LegacyCountProfileCannotBeUsedAsActualInspectorStartConfiguration()
    {
        SetDraft(PopupStorePhasePreviewSettings.CreateProposalV0()); RejectDraft();
    }

    [Test]
    public void MissingInspectorSettingsFailsClosed()
    {
        SetDraft(null); RejectDraft();
    }

    [Test]
    public void MissingCatalogDoesNotExposeHalfFrozenProfileOrPolicy()
    {
        Assert.That(_draft.TryFreeze(null, out var json, out var profile, out var policy, out var error), Is.False);
        Assert.That(json, Is.Null); Assert.That(profile, Is.Null); Assert.That(policy, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void UnknownGoalMetricFailsClosedInsteadOfUsingLegacyCounts()
    {
        _draft.Settings.goalMetric = (PopupStorePhaseGoalMetric)999; RejectDraft();
    }

    [Test]
    public void LegacyCountFieldDoesNotReplaceExplicitScoreGoal()
    {
        _draft.Settings.stages[0].target = 999;
        Assert.That(_draft.TryFreeze(Catalog(), out _, out var profile, out _, out var error), Is.True, error);
        Assert.That(profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
        Assert.That(profile.Phases[0].TargetScore, Is.EqualTo(200));
        Assert.That(profile.Phases[0].TargetSuccessfulOrders, Is.EqualTo(999));
    }

    [Test]
    public void CountTargetsCannotRepairMissingScoreGoals()
    {
        foreach (var row in _draft.Settings.stages) { row.target = 4; row.targetScore = 0; }
        RejectDraft();
    }

    [Test]
    public void InvalidProfileCannotLeakValidPointsAsAnExecutableConfiguration()
    {
        _draft.Settings.stages = null; RejectDraft();
    }

    [Test]
    public void V1FactoryIsIndependentOfTheLegacyProposalAndEveryOtherV1Instance()
    {
        var first = PopupStorePhasePreviewSettings.CreateProposalV1();
        first.points.fiveMaterials = 999; first.stages[0].targetScore = 999;
        var second = PopupStorePhasePreviewSettings.CreateProposalV1();
        Assert.That(second.points.fiveMaterials, Is.EqualTo(220));
        Assert.That(second.stages[0].targetScore, Is.EqualTo(200));
        Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().stages[0].target, Is.EqualTo(1));
        Assert.That(PopupStorePhasePreviewSettings.SettingsPath, Is.EqualTo("Temp/PopupStorePhasePreview/settings-v0.json"));
    }

    [Test]
    public void EditableAssetAndItsTypeRemainOutsidePlayerAndResources()
    {
        string path = AssetDatabase.GetAssetPath(Asset());
        Assert.That(path, Is.EqualTo(PopupStorePhasePreviewSettings.BalanceAssetPath));
        Assert.That(path, Does.StartWith("Assets/Editor/").And.Not.Contain("/Resources/"));
        var fields = new SerializedObject(Asset());
        Assert.That(fields.FindProperty("_settings.stages").arraySize, Is.EqualTo(6));
        Assert.That(fields.FindProperty("_settings.points.fourMaterials").intValue, Is.EqualTo(150));
        var playerFiles = CompilationPipeline.GetAssemblies(AssembliesType.Player).SelectMany(a => a.sourceFiles);
        Assert.That(playerFiles.Any(p => p.Replace('\\', '/').EndsWith("/PopupStorePhasePreviewBalanceAsset.cs")), Is.False);
    }

    private void RejectDraft()
    {
        Assert.That(_draft.TryFreeze(Catalog(), out var json, out var profile, out var policy, out var error), Is.False);
        Assert.That(json, Is.Null); Assert.That(profile, Is.Null); Assert.That(policy, Is.Null); Assert.That(error, Is.Not.Empty);
    }
    private void SetDraft(PopupStorePhasePreviewSettings settings)
        => typeof(PopupStorePhasePreviewBalanceAsset).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_draft, settings);
    private static PopupStoreMaterialCatalog Catalog()
        => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>("Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset");
    private static PopupStorePhasePreviewBalanceAsset Asset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<PopupStorePhasePreviewBalanceAsset>(PopupStorePhasePreviewSettings.BalanceAssetPath);
        Assert.That(asset, Is.Not.Null, "Authored Editor-only balance asset must exist."); return asset;
    }
    private static string LegacyJson()
    {
        var value = new StringBuilder("{\"profileId\":\"HISTORICAL_V0\",\"version\":1,\"clearNoticeSeconds\":1,\"countdownStepSeconds\":1,\"stages\":[");
        var stages = PopupStorePhasePreviewSettings.CreateProposalV0().stages;
        for (int i = 0; i < stages.Length; i++)
        {
            if (i > 0) value.Append(',');
            var row = stages[i];
            value.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "{{\"seconds\":{0},\"target\":{1},\"plushCount\":{2},\"onePlushPatienceSeconds\":{3},\"entryIntervalSeconds\":{4}}}",
                row.seconds, row.target, row.plushCount, row.onePlushPatienceSeconds, row.entryIntervalSeconds);
        }
        return value.Append("]}").ToString();
    }
}
#endif
