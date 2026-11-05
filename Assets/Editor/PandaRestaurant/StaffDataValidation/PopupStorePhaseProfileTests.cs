using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>TEST_ONLY configuration validation. These values are state-model inputs, not approved operating balance.</summary>
public sealed class PopupStorePhaseProfileTests
{
    [Test]
    public void SixExplicitPhasesProduceAValidatedSnapshotWithExistingGradeEnum()
    {
        var profile = PopupStorePhaseTestProfiles.CreateDefinitionProfile();
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        Assert.That(snapshot.ProfileId, Is.EqualTo("TEST_ONLY_phase_profile"));
        Assert.That(snapshot.Version, Is.EqualTo(1));
        CollectionAssert.AreEqual(new[] { PopupStoreGradeTier.C, PopupStoreGradeTier.B, PopupStoreGradeTier.A,
            PopupStoreGradeTier.S, PopupStoreGradeTier.SS, PopupStoreGradeTier.SSS }, snapshot.Phases.Select(p => p.Grade));
        for (int i = 0; i < 6; i++)
        {
            var definition = profile.Phases[i];
            var phase = snapshot.Phases[i];
            Assert.That(phase.PhaseNumber, Is.EqualTo(i + 1));
            Assert.That(phase.TimeLimitSeconds, Is.EqualTo(definition.TimeLimitSeconds));
            Assert.That(phase.TargetSuccessfulOrders, Is.EqualTo(definition.TargetSuccessfulOrders));
            Assert.That(phase.CustomerPatienceSeconds, Is.EqualTo(definition.CustomerPatienceSeconds));
            Assert.That(phase.EntryIntervalSeconds, Is.EqualTo(definition.EntryIntervalSeconds));
            Assert.That(phase.StageClearNoticeSeconds, Is.EqualTo(definition.StageClearNoticeSeconds));
            Assert.That(phase.CountdownStepSeconds, Is.EqualTo(definition.CountdownStepSeconds));
            CollectionAssert.AreEqual(definition.AllowedPlushIds, phase.AllowedPlushIds);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \t")]
    public void BlankProfileIdentityIsRejected(string id)
    {
        var profile = Profile(); profile.ProfileId = id;
        Reject(profile, "ProfileId");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveVersionIsRejected(int value)
    {
        var profile = Profile(); profile.Version = value;
        Reject(profile, "Version");
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(7)]
    public void PhaseCountMustBeExactlySix(int count)
    {
        var profile = Profile();
        profile.Phases = Enumerable.Range(0, count).Select(i => Profile().Phases[i % 6]).ToArray();
        Reject(profile, "exactly six");
    }

    [Test]
    public void NullPhaseArrayAndNullDefinitionAreRejectedWithoutPartialSnapshot()
    {
        var profile = Profile(); profile.Phases = null;
        Reject(profile, "exactly six");
        profile = Profile(); profile.Phases[3] = null;
        Reject(profile, "Phase 4: definition");
    }

    [TestCase("None")]
    [TestCase("duplicate")]
    [TestCase("swapped")]
    [TestCase("undefined")]
    public void GradeOrderCannotBeMissingDuplicatedOrReversed(string defect)
    {
        var profile = Profile();
        if (defect == "None") profile.Phases[0].Grade = PopupStoreGradeTier.None;
        if (defect == "duplicate") profile.Phases[2].Grade = PopupStoreGradeTier.B;
        if (defect == "swapped") { profile.Phases[0].Grade = PopupStoreGradeTier.B; profile.Phases[1].Grade = PopupStoreGradeTier.C; }
        if (defect == "undefined") profile.Phases[5].Grade = (PopupStoreGradeTier)77;
        Reject(profile, "Grade");
    }

    [TestCase(0)]
    [TestCase(7)]
    [TestCase(1)]
    public void PhaseNumbersMustMatchTheirOneBasedPositions(int number)
    {
        var profile = Profile(); profile.Phases[1].PhaseNumber = number;
        Reject(profile, "PhaseNumber");
    }

    [TestCase("TimeLimitSeconds")]
    [TestCase("CustomerPatienceSeconds")]
    [TestCase("EntryIntervalSeconds")]
    [TestCase("CountdownStepSeconds")]
    public void PositiveTimeFieldsRejectZeroNegativeAndNonfiniteValues(string field)
    {
        foreach (double invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var profile = Profile();
            typeof(PopupStorePhaseDefinition).GetProperty(field).SetValue(profile.Phases[0], invalid);
            Reject(profile, field);
            Assert.That(double.IsNaN(invalid) ? double.IsNaN((double)typeof(PopupStorePhaseDefinition).GetProperty(field).GetValue(profile.Phases[0]))
                : Equals(invalid, typeof(PopupStorePhaseDefinition).GetProperty(field).GetValue(profile.Phases[0])), Is.True,
                "Invalid input must not be silently corrected.");
        }
    }

    [Test]
    public void ClearNoticeAllowsZeroButRejectsNegativeOrNonfiniteDurations()
    {
        var zero = Profile(); zero.Phases[0].StageClearNoticeSeconds = 0;
        Assert.That(zero.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        Assert.That(snapshot.Phases[0].StageClearNoticeSeconds, Is.Zero);
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var profile = Profile(); profile.Phases[0].StageClearNoticeSeconds = invalid;
            Reject(profile, "StageClearNoticeSeconds");
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveTargetOrderCountIsRejected(int count)
    {
        var profile = Profile(); profile.Phases[0].TargetSuccessfulOrders = count;
        Reject(profile, "TargetSuccessfulOrders");
    }

    [TestCase(4)]
    [TestCase(6)]
    public void FirstCPhaseRequiresExactlyFivePlushIds(int count)
    {
        var profile = Profile(); profile.Phases[0].AllowedPlushIds = Ids(count);
        Reject(profile, "exactly five");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("STAFF33")]
    [TestCase("staff01")]
    [TestCase("BOX_BLUE")]
    [TestCase("RIBBON_PINK")]
    public void AllowedIdsMustBeRegisteredOrdinalPlushIds(string id)
    {
        var profile = Profile(); profile.Phases[0].AllowedPlushIds[0] = id;
        Reject(profile, "unregistered or non-Plush");
    }

    [Test]
    public void NullEmptyAndDuplicateAllowedSetsAreRejected()
    {
        var profile = Profile(); profile.Phases[0].AllowedPlushIds = null;
        Reject(profile, "AllowedPlushIds");
        profile = Profile(); profile.Phases[1].AllowedPlushIds = Array.Empty<string>();
        Reject(profile, "AllowedPlushIds");
        profile = Profile(); profile.Phases[0].AllowedPlushIds[4] = "STAFF01";
        Reject(profile, "duplicate ID");
    }

    [TestCase("shrink")]
    [TestCase("replace")]
    public void LaterPhasesCannotLoseAnyPreviouslyUnlockedId(string defect)
    {
        var profile = Profile();
        profile.Phases[1].AllowedPlushIds = defect == "shrink" ? Ids(4)
            : new[] { "STAFF01", "STAFF02", "STAFF03", "STAFF04", "STAFF06", "STAFF07" };
        Reject(profile, "every previously unlocked");
    }

    [Test]
    public void EqualSetsAndEqualPatienceAreAllowedWithoutInventingUnlockOrDifficultyRules()
    {
        var profile = Profile();
        foreach (var phase in profile.Phases)
        { phase.AllowedPlushIds = Ids(5); phase.CustomerPatienceSeconds = 30; }
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        Assert.That(snapshot.Phases.All(p => p.AllowedPlushIds.Count == 5 && p.CustomerPatienceSeconds == 30), Is.True);
    }

    [Test]
    public void LaterPatienceMayDecreaseButCannotIncrease()
    {
        var valid = Profile();
        Assert.That(valid.TryCreateSnapshot(Catalog(), out _, out var error), Is.True, error);
        valid.Phases[1].CustomerPatienceSeconds = valid.Phases[0].CustomerPatienceSeconds + .01;
        Reject(valid, "cannot increase");
    }

    [Test]
    public void SnapshotPreservesExplicitIdOrderAndCanEventuallyUnlockAllThirtyTwo()
    {
        var profile = Profile();
        profile.Phases[0].AllowedPlushIds = Ids(5).Reverse().ToArray();
        profile.Phases[5].AllowedPlushIds = Ids(32).Reverse().ToArray();
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        CollectionAssert.AreEqual(new[] { "STAFF05", "STAFF04", "STAFF03", "STAFF02", "STAFF01" }, snapshot.Phases[0].AllowedPlushIds);
        CollectionAssert.AreEqual(profile.Phases[5].AllowedPlushIds, snapshot.Phases[5].AllowedPlushIds);
        Assert.That(snapshot.Phases[5].AllowedPlushIds.Count, Is.EqualTo(32));
        Assert.That(snapshot.Phases[0].IsPlushAllowed("STAFF06"), Is.False);
        Assert.That(snapshot.Phases[5].IsPlushAllowed("STAFF32"), Is.True);
        Assert.That(snapshot.Phases[5].IsPlushAllowed("staff32"), Is.False);
        Assert.That(snapshot.Phases[5].IsPlushAllowed("BOX_BLUE"), Is.False);
        Assert.That(snapshot.Phases[5].IsPlushAllowed(null), Is.False);
    }

    [Test]
    public void AllOriginalSettingsAndArraysCanChangeWithoutMutatingTheRunningSnapshot()
    {
        var profile = Profile();
        var originalArray = profile.Phases;
        var originalPhase = originalArray[0];
        var originalIds = originalPhase.AllowedPlushIds;
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.True, error);
        profile.ProfileId = "changed"; profile.Version = 999; profile.Phases = null;
        originalPhase.PhaseNumber = 5; originalPhase.Grade = PopupStoreGradeTier.SS;
        originalPhase.TimeLimitSeconds = 999; originalPhase.TargetSuccessfulOrders = 999;
        originalPhase.CustomerPatienceSeconds = 999; originalPhase.EntryIntervalSeconds = 999;
        originalPhase.StageClearNoticeSeconds = 999; originalPhase.CountdownStepSeconds = 999;
        originalIds[0] = "STAFF32"; originalPhase.AllowedPlushIds = null; originalArray[0] = null;
        Assert.That(snapshot.ProfileId, Is.EqualTo("TEST_ONLY_phase_profile"));
        Assert.That(snapshot.Version, Is.EqualTo(1));
        Assert.That(snapshot.Phases.Count, Is.EqualTo(6));
        var first = snapshot.Phases[0];
        Assert.That(first.PhaseNumber, Is.EqualTo(1)); Assert.That(first.Grade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(first.TimeLimitSeconds, Is.EqualTo(10)); Assert.That(first.TargetSuccessfulOrders, Is.EqualTo(2));
        Assert.That(first.CustomerPatienceSeconds, Is.EqualTo(30)); Assert.That(first.EntryIntervalSeconds, Is.EqualTo(1));
        Assert.That(first.StageClearNoticeSeconds, Is.EqualTo(.5)); Assert.That(first.CountdownStepSeconds, Is.EqualTo(1));
        CollectionAssert.AreEqual(Ids(5), first.AllowedPlushIds);
        Assert.That(first.IsPlushAllowed("STAFF01"), Is.True);
        Assert.That(first.IsPlushAllowed("STAFF32"), Is.False);
    }

    [Test]
    public void ReturnedCollectionsAndPropertiesHaveNoExternalMutationSurface()
    {
        var snapshot = PopupStorePhaseTestProfiles.CreateSnapshot();
        var phases = snapshot.Phases as IList<PopupStorePhaseSnapshot>;
        var ids = snapshot.Phases[0].AllowedPlushIds as IList<string>;
        Assert.That(phases.IsReadOnly, Is.True); Assert.That(ids.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => phases[0] = snapshot.Phases[1]);
        Assert.Throws<NotSupportedException>(() => phases.Clear());
        Assert.Throws<NotSupportedException>(() => ids[0] = "STAFF32");
        Assert.Throws<NotSupportedException>(() => ids.Add("STAFF32"));
        foreach (var type in new[] { typeof(PopupStorePhaseProfileSnapshot), typeof(PopupStorePhaseSnapshot) })
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(property.GetSetMethod(true), Is.Null, type.Name + "." + property.Name);
    }

    [Test]
    public void CompleteOriginalCatalogIsRequiredAndIsNeverReducedToTheActiveSet()
    {
        var catalog = Catalog();
        string before = EditorJsonUtility.ToJson(catalog);
        Assert.That(Profile().TryCreateSnapshot(null, out var missing, out var error), Is.False);
        Assert.That(missing, Is.Null); Assert.That(error, Does.Contain("38-material"));
        var incomplete = Object.Instantiate(catalog);
        try
        {
            Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out _), Is.True);
            typeof(PopupStoreMaterialCatalog).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(incomplete, plushes.Take(5).ToArray());
            Assert.That(Profile().TryCreateSnapshot(incomplete, out var invalid, out error), Is.False);
            Assert.That(invalid, Is.Null); Assert.That(error, Does.Contain("complete catalog"));
        }
        finally { Object.DestroyImmediate(incomplete); }
        Assert.That(Profile().TryCreateSnapshot(catalog, out var snapshot, out error), Is.True, error);
        Assert.That(snapshot.Phases[0].AllowedPlushIds.Count, Is.EqualTo(5));
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var allPlush, out _), Is.True);
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out _), Is.True);
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out _), Is.True);
        Assert.That(allPlush.Length, Is.EqualTo(32)); Assert.That(boxes.Length, Is.EqualTo(3)); Assert.That(ribbons.Length, Is.EqualTo(3));
        Assert.That(EditorJsonUtility.ToJson(catalog), Is.EqualTo(before));
    }

    [Test]
    public void BlankMutableDefinitionsHaveNoHiddenOperatingDefaults()
    {
        var profile = new PopupStorePhaseProfile();
        Assert.That(profile.ProfileId, Is.Null); Assert.That(profile.Version, Is.Zero); Assert.That(profile.Phases, Is.Null);
        Reject(profile, "ProfileId");
        var phase = new PopupStorePhaseDefinition();
        Assert.That(phase.TimeLimitSeconds, Is.Zero); Assert.That(phase.TargetSuccessfulOrders, Is.Zero);
        Assert.That(phase.AllowedPlushIds, Is.Null); Assert.That(phase.CustomerPatienceSeconds, Is.Zero);
        Assert.That(phase.EntryIntervalSeconds, Is.Zero); Assert.That(phase.CountdownStepSeconds, Is.Zero);
        Assert.That(typeof(ScriptableObject).IsAssignableFrom(typeof(PopupStorePhaseProfile)), Is.False);
        Assert.That(typeof(Component).IsAssignableFrom(typeof(PopupStorePhaseDefinition)), Is.False);
    }

    private static PopupStorePhaseProfile Profile() => PopupStorePhaseTestProfiles.CreateDefinitionProfile();
    private static PopupStoreMaterialCatalog Catalog() => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(PopupStorePhaseTestProfiles.CatalogPath);
    private static string[] Ids(int count) => Enumerable.Range(1, count).Select(i => "STAFF" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    private static void Reject(PopupStorePhaseProfile profile, string expectedReason)
    {
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out var error), Is.False);
        Assert.That(snapshot, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(error, Does.Contain(expectedReason));
    }
}

/// <summary>TEST_ONLY synthetic profile for pure-model tests. Not a balance proposal and never a Resources asset.</summary>
internal static class PopupStorePhaseTestProfiles
{
    internal const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";
    internal static PopupStorePhaseProfile CreateDefinitionProfile()
    {
        var phases = new PopupStorePhaseDefinition[6];
        for (int i = 0; i < phases.Length; i++)
            phases[i] = new PopupStorePhaseDefinition
            {
                PhaseNumber = i + 1,
                Grade = (PopupStoreGradeTier)(i + (int)PopupStoreGradeTier.C),
                TimeLimitSeconds = 10 + i,
                TargetSuccessfulOrders = 2 + i,
                AllowedPlushIds = Enumerable.Range(1, 5 + i).Select(n => "STAFF" + n.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
                CustomerPatienceSeconds = 30 - i,
                EntryIntervalSeconds = 1,
                StageClearNoticeSeconds = .5,
                CountdownStepSeconds = 1
            };
        return new PopupStorePhaseProfile { ProfileId = "TEST_ONLY_phase_profile", Version = 1, Phases = phases };
    }

    internal static PopupStorePhaseProfileSnapshot CreateSnapshot()
    {
        var definition = CreateDefinitionProfile();
        Assert.That(definition.TryCreateSnapshot(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath),
            out var snapshot, out var error), Is.True, error);
        return snapshot;
    }
}
