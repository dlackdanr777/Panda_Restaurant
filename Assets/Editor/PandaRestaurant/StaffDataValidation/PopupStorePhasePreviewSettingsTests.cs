#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>Validates the requested Editor proposal without starting Play or creating/modifying the local settings file.</summary>
public sealed class PopupStorePhasePreviewSettingsTests
{
    private const string Folder = "Assets/Editor/PandaRestaurant/StaffDataValidation/";
    private GachaCollectionPreviewSceneWitness _workspace;
    private string _catalogBefore;
    private string _settingsBefore;
    private bool _settingsExisted;

    [SetUp]
    public void RecordReadOnlyInputs()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _catalogBefore = EditorJsonUtility.ToJson(Catalog());
        _settingsExisted = File.Exists(PopupStorePhasePreviewSettings.SettingsPath);
        _settingsBefore = _settingsExisted ? File.ReadAllText(PopupStorePhasePreviewSettings.SettingsPath) : null;
    }

    [TearDown]
    public void PreserveEditorCatalogAndLocalSettings()
    {
        _workspace.AssertUnchanged("six-phase preview settings validation");
        Assert.That(EditorJsonUtility.ToJson(Catalog()), Is.EqualTo(_catalogBefore));
        Assert.That(File.Exists(PopupStorePhasePreviewSettings.SettingsPath), Is.EqualTo(_settingsExisted));
        if (_settingsExisted) Assert.That(File.ReadAllText(PopupStorePhasePreviewSettings.SettingsPath), Is.EqualTo(_settingsBefore));
    }

    [TestCase(0, 22, 1, 5, 22, 3.2)]
    [TestCase(1, 27, 2, 8, 21, 3.0)]
    [TestCase(2, 30, 2, 12, 20, 2.8)]
    [TestCase(3, 34, 3, 16, 19, 2.6)]
    [TestCase(4, 38, 3, 24, 18, 2.4)]
    [TestCase(5, 42, 4, 32, 17, 2.2)]
    public void RequestedV0UsesExactNumbersAndCatalogDisplayOrder(int index, double seconds, int target,
        int plushCount, double patience, double interval)
    {
        var snapshot = Snapshot(PopupStorePhasePreviewSettings.CreateProposalV0());
        var phase = snapshot.Phases[index];
        Assert.That(phase.Grade, Is.EqualTo((PopupStoreGradeTier)(index + 1)));
        Assert.That(phase.PhaseNumber, Is.EqualTo(index + 1));
        Assert.That(phase.TimeLimitSeconds, Is.EqualTo(seconds));
        Assert.That(phase.TargetSuccessfulOrders, Is.EqualTo(target));
        Assert.That(phase.CustomerPatienceSeconds, Is.EqualTo(patience));
        Assert.That(phase.EntryIntervalSeconds, Is.EqualTo(interval));
        Assert.That(phase.StageClearNoticeSeconds, Is.EqualTo(1));
        Assert.That(phase.CountdownStepSeconds, Is.EqualTo(1));
        Assert.That(Catalog().TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out _), Is.True);
        CollectionAssert.AreEqual(plushes.Take(plushCount).Select(item => item.Id), phase.AllowedPlushIds);
    }

    [Test]
    public void TotalsKeepFiveCountdownsAndTheSeparateLastNotice()
    {
        var snapshot = Snapshot(PopupStorePhasePreviewSettings.CreateProposalV0());
        Assert.That(snapshot.Phases.Sum(p => p.TimeLimitSeconds), Is.EqualTo(193));
        Assert.That(snapshot.Phases.Take(5).Sum(p => p.StageClearNoticeSeconds + 3 * p.CountdownStepSeconds), Is.EqualTo(20));
        Assert.That(snapshot.Phases.Last().StageClearNoticeSeconds, Is.EqualTo(1));
    }

    [Test]
    public void RunningSnapshotCannotAdoptSubsequentEditorSettingsChanges()
    {
        var editable = PopupStorePhasePreviewSettings.CreateProposalV0();
        var snapshot = Snapshot(editable);
        editable.profileId = "changed"; editable.version = 99;
        editable.stages[0].seconds = 200; editable.stages[1].plushCount = 32;
        editable.clearNoticeSeconds = 50; editable.stages = null;
        Assert.That(snapshot.ProfileId, Is.EqualTo("EDITOR_PHASE_PREVIEW_PROPOSAL_V0"));
        Assert.That(snapshot.Version, Is.EqualTo(1));
        Assert.That(snapshot.Phases[0].TimeLimitSeconds, Is.EqualTo(22));
        Assert.That(snapshot.Phases[1].AllowedPlushIds.Count, Is.EqualTo(8));
        Assert.That(snapshot.Phases[0].StageClearNoticeSeconds, Is.EqualTo(1));
    }

    [Test]
    public void ActiveSubsetDoesNotReduceTheOriginalThirtyEightMaterials()
    {
        Snapshot(PopupStorePhasePreviewSettings.CreateProposalV0());
        Assert.That(Catalog().TryGetMaterials(PopupStoreMaterialKind.Plush, out var plush, out _), Is.True);
        Assert.That(Catalog().TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out _), Is.True);
        Assert.That(Catalog().TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out _), Is.True);
        Assert.That(plush.Length, Is.EqualTo(32)); Assert.That(boxes.Length, Is.EqualTo(3)); Assert.That(ribbons.Length, Is.EqualTo(3));
    }

    [Test]
    public void ExplicitValidEditsRoundTripWithoutChangingOriginalProposal()
    {
        var settings = PopupStorePhasePreviewSettings.CreateProposalV0();
        settings.stages[1].seconds = 28; settings.stages[3].target = 4;
        string json = JsonUtility.ToJson(settings);
        Assert.That(PopupStorePhasePreviewSettings.TryParseSnapshot(json, Catalog(), out var snapshot, out var error), Is.True, error);
        Assert.That(snapshot.Phases[1].TimeLimitSeconds, Is.EqualTo(28));
        Assert.That(snapshot.Phases[3].TargetSuccessfulOrders, Is.EqualTo(4));
        Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().stages[1].seconds, Is.EqualTo(27));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(5)]
    [TestCase(7)]
    public void MissingExtraOrNullPhaseRowsAreNotRepaired(int count)
    {
        var settings = PopupStorePhasePreviewSettings.CreateProposalV0();
        settings.stages = count < 0 ? null : Enumerable.Range(0, count).Select(i => PopupStorePhasePreviewSettings.CreateProposalV0().stages[i % 6]).ToArray();
        Reject(settings);
        Assert.That(settings.stages == null ? -1 : settings.stages.Length, Is.EqualTo(count));
    }

    [TestCase(0)]
    [TestCase(33)]
    [TestCase(6)]
    public void InvalidOrNonFiveCPlushCountIsRejected(int count)
    {
        var settings = PopupStorePhasePreviewSettings.CreateProposalV0(); settings.stages[0].plushCount = count;
        Reject(settings); Assert.That(settings.stages[0].plushCount, Is.EqualTo(count));
    }

    [Test]
    public void UnlockedPoolCannotShrink()
    {
        var settings = PopupStorePhasePreviewSettings.CreateProposalV0(); settings.stages[2].plushCount = 7;
        Reject(settings);
    }

    [Test]
    public void LaterBasePatienceCannotIncreaseContraryToTheCoreContract()
    {
        var settings = PopupStorePhasePreviewSettings.CreateProposalV0(); settings.stages[1].onePlushPatienceSeconds = 23;
        Reject(settings);
    }

    [Test]
    public void InvalidTimingsTargetsNullRowsAndIdentityAreRejected()
    {
        foreach (double bad in new[] { -1d, 0d, double.NaN, double.PositiveInfinity })
        {
            var settings = PopupStorePhasePreviewSettings.CreateProposalV0(); settings.stages[0].seconds = bad; Reject(settings);
            settings = PopupStorePhasePreviewSettings.CreateProposalV0(); settings.stages[0].entryIntervalSeconds = bad; Reject(settings);
        }
        var value = PopupStorePhasePreviewSettings.CreateProposalV0(); value.stages[0] = null; Reject(value);
        value = PopupStorePhasePreviewSettings.CreateProposalV0(); value.stages[0].target = 0; Reject(value);
        value = PopupStorePhasePreviewSettings.CreateProposalV0(); value.profileId = " "; Reject(value);
        value = PopupStorePhasePreviewSettings.CreateProposalV0(); value.version = 0; Reject(value);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{}")]
    [TestCase("not-json")]
    public void InvalidJsonCannotFallBackToAnUnrequestedDefault(string json)
    {
        Assert.That(PopupStorePhasePreviewSettings.TryParseSnapshot(json, Catalog(), out var snapshot, out var error), Is.False);
        Assert.That(snapshot, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void MissingCatalogCannotCreateAProfile()
    {
        Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().TryCreateSnapshot(null, out var snapshot, out var error), Is.False);
        Assert.That(snapshot, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void MenuNamesAreDistinctAndDoNotReplaceTheOrdinaryTestMenu()
    {
        var actual = typeof(PopupStorePhasePreviewMenu).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>())
            .Select(attribute => attribute.menuItem).ToArray();
        CollectionAssert.AreEquivalent(new[] { PopupStorePhasePreviewMenu.StartPath, PopupStorePhasePreviewMenu.ResetPath,
            PopupStorePhasePreviewMenu.StatusPath }, actual);
        Assert.That(actual.Distinct().Count(), Is.EqualTo(3));
        Assert.That(actual.Intersect(new[] { PopupStoreManualTestMenu.StartPath, PopupStoreManualTestMenu.ResetPath,
            PopupStoreManualTestMenu.StatusPath }), Is.Empty);
    }

    [Test]
    public void EditableConfigurationIsTempOnlyAndAllEntryFilesAreExcludedFromPlayerCompilation()
    {
        // Historical testcase identity is retained. "TempOnly" here protects the legacy v0 file;
        // DEV-GAME-02B-4 explicit starts use the separately tested Editor-only Inspector balance asset.
        Assert.That(PopupStorePhasePreviewSettings.SettingsPath.Replace('\\', '/'), Does.StartWith("Temp/"));
        var playerSources = CompilationPipeline.GetAssemblies(AssembliesType.Player)
            .SelectMany(assembly => assembly.sourceFiles).Select(path => path.Replace('\\', '/')).ToArray();
        foreach (string file in new[] { "PopupStorePhasePreviewSettings.cs", "PopupStorePhasePreviewMenu.cs", "PopupStorePhasePreviewReviewBridge.cs" })
        {
            Assert.That(playerSources.Any(path => path.EndsWith("/" + file) || path == Folder + file), Is.False, file);
            Assert.That(File.ReadAllText(Folder + file).TrimStart(), Does.StartWith("#if UNITY_EDITOR"));
        }
    }

    private static PopupStoreMaterialCatalog Catalog()
        => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>("Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset");
    private static PopupStorePhaseProfileSnapshot Snapshot(PopupStorePhasePreviewSettings settings)
    {
        Assert.That(settings.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        return snapshot;
    }
    private static void Reject(PopupStorePhasePreviewSettings settings)
    {
        Assert.That(settings.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.False);
        Assert.That(snapshot, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
    }
}
#endif
