using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>R0 data validation and deterministic expectation only. Never draws, awards or saves a reward.</summary>
public sealed class PopupStoreRewardTableTests
{
    private const string AssetPath = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(PopupStoreGradeTier.C, 1, 2, 10, "1.23")]
    [TestCase(PopupStoreGradeTier.B, 2, 3, 12, "2.24")]
    [TestCase(PopupStoreGradeTier.A, 3, 4, 15, "3.26")]
    [TestCase(PopupStoreGradeTier.S, 4, 6, 20, "4.44")]
    [TestCase(PopupStoreGradeTier.SS, 5, 8, 25, "5.62")]
    [TestCase(PopupStoreGradeTier.SSS, 6, 10, 30, "6.80")]
    public void AuthoredR0EntryContainsExactAmountsWeightsAndDecimalExpectation(PopupStoreGradeTier grade,
        int basic, int bonus, int jackpot, string expectation)
    {
        var table = Authored();
        Assert.That(table.TryValidate(out string error), Is.True, error);
        Assert.That(table.TryGetReward(grade, out var entry, out error), Is.True, error);
        Assert.That(entry.Grade, Is.EqualTo(grade));
        Assert.That(entry.BasicDiamonds, Is.EqualTo(basic)); Assert.That(entry.BonusDiamonds, Is.EqualTo(bonus));
        Assert.That(entry.JackpotDiamonds, Is.EqualTo(jackpot));
        Assert.That(entry.BasicWeight, Is.EqualTo(85)); Assert.That(entry.BonusWeight, Is.EqualTo(14)); Assert.That(entry.JackpotWeight, Is.EqualTo(1));
        Assert.That((long)entry.BasicWeight + entry.BonusWeight + entry.JackpotWeight, Is.EqualTo(100));
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out decimal actual, out error), Is.True, error);
        Assert.That(actual, Is.EqualTo(decimal.Parse(expectation, CultureInfo.InvariantCulture)));
        Assert.That(actual, Is.Not.EqualTo(basic + bonus + jackpot), "These are alternatives, not cumulative awards.");
    }

    [Test]
    public void NoneHasNoRewardAndNoErrorWhenTheWholeAuthoredTableIsValid()
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.None, out _, out string error), Is.False);
        Assert.That(error, Is.Null);
    }

    [TestCase(-1)]
    [TestCase(7)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void UndefinedLookupGradesFailExplicitlyInsteadOfFallingBackToAnotherEntry(int grade)
    {
        Assert.That(Authored().TryGetReward((PopupStoreGradeTier)grade, out _, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(7)]
    [TestCase(int.MaxValue)]
    public void PureEntryValidationRejectsNoneAndUndefinedGrades(int grade)
        => AssertInvalid(new PopupStoreRewardEntry((PopupStoreGradeTier)grade, 1, 2, 10, 85, 14, 1));

    [TestCase(0, -1)]
    [TestCase(1, -1)]
    [TestCase(2, -1)]
    [TestCase(0, int.MinValue)]
    [TestCase(1, int.MinValue)]
    [TestCase(2, int.MinValue)]
    public void NegativeAmountsCannotValidateOrProduceAnExpectation(int position, int value)
    {
        int[] amounts = { 1, 2, 10 }; amounts[position] = value;
        AssertInvalid(new PopupStoreRewardEntry(PopupStoreGradeTier.C, amounts[0], amounts[1], amounts[2], 85, 14, 1));
    }

    [TestCase(2, 1, 10)]
    [TestCase(1, 11, 10)]
    public void AmountsMustBeNondecreasingAcrossBasicBonusAndJackpot(int basic, int bonus, int jackpot)
        => AssertInvalid(new PopupStoreRewardEntry(PopupStoreGradeTier.C, basic, bonus, jackpot, 85, 14, 1));

    [TestCase(-1, 100, 1)]
    [TestCase(85, -1, 16)]
    [TestCase(85, 16, -1)]
    public void ATotalOfOneHundredDoesNotExcuseANegativeWeight(int basic, int bonus, int jackpot)
    {
        Assert.That((long)basic + bonus + jackpot, Is.EqualTo(100));
        AssertInvalid(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, basic, bonus, jackpot));
    }

    [TestCase(0, 0, 0)]
    [TestCase(85, 14, 0)]
    [TestCase(85, 14, 2)]
    [TestCase(int.MaxValue, int.MaxValue, 102)]
    public void IncorrectWeightTotalsIncludingAnIntWraparoundToOneHundredAreRejected(int basic, int bonus, int jackpot)
    {
        Assert.That((long)basic + bonus + jackpot, Is.Not.EqualTo(100));
        AssertInvalid(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, basic, bonus, jackpot));
    }

    [TestCase(0, 0, 0, 85, 14, 1, "0")]
    [TestCase(1, 2, 10, 100, 0, 0, "1")]
    [TestCase(1, 2, 10, 0, 100, 0, "2")]
    [TestCase(1, 2, 10, 0, 0, 100, "10")]
    [TestCase(2, 10, 100, 50, 30, 20, "24")]
    [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, 85, 14, 1, "2147483647")]
    public void ValidEditedRowsUseTheirActualValuesIncludingZeroEqualAndMaximumAmounts(int basic, int bonus, int jackpot,
        int basicWeight, int bonusWeight, int jackpotWeight, string expectation)
    {
        var entry = new PopupStoreRewardEntry(PopupStoreGradeTier.A, basic, bonus, jackpot, basicWeight, bonusWeight, jackpotWeight);
        Assert.That(PopupStoreRewardCalculator.TryValidate(entry, out string error), Is.True, error);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out decimal expected, out error), Is.True, error);
        Assert.That(expected, Is.EqualTo(decimal.Parse(expectation, CultureInfo.InvariantCulture)));
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void EveryGradeIsRequiredBeforeAnyPartOfTheTableCanBeRead(PopupStoreGradeTier missing)
    {
        using (var scope = new TableScope(Rows().Where(row => row.Grade != missing).ToArray()))
            AssertInvalidTable(scope.Table);
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void DuplicateGradesCannotSilentlyReplaceOrShadowAnotherGrade(PopupStoreGradeTier duplicate)
    {
        var rows = Rows();
        int index = Array.FindIndex(rows, row => row.Grade == duplicate);
        rows[(index + 1) % rows.Length] = rows[index];
        using (var scope = new TableScope(rows)) AssertInvalidTable(scope.Table);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(7)]
    public void NullEmptyOrOversizedTablesCannotExposePartialRewards(int length)
    {
        PopupStoreRewardEntry[] rows = length < 0 ? null : length == 0 ? Array.Empty<PopupStoreRewardEntry>() : Rows().Concat(new[] { Rows()[0] }).ToArray();
        using (var scope = new TableScope(rows)) AssertInvalidTable(scope.Table);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(7)]
    public void NoneOrUndefinedGradeInAnOtherwiseFullTableInvalidatesTheWholeTable(int grade)
    {
        var rows = Rows(); rows[5] = new PopupStoreRewardEntry((PopupStoreGradeTier)grade, 6, 10, 30, 85, 14, 1);
        using (var scope = new TableScope(rows)) AssertInvalidTable(scope.Table);
    }

    [Test]
    public void InvalidUnrequestedRowAlsoBlocksAValidGradeAndNoneLookup()
    {
        var rows = Rows(); rows[5] = new PopupStoreRewardEntry(PopupStoreGradeTier.SSS, 6, 10, 30, 85, 14, 0);
        using (var scope = new TableScope(rows))
        {
            Assert.That(scope.Table.TryGetReward(PopupStoreGradeTier.C, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(scope.Table.TryGetReward(PopupStoreGradeTier.None, out _, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty, "None is error-free only after the table validates.");
        }
    }

    [Test]
    public void EditingAValidMemoryTableRecalculatesTheExpectationWithoutR0Fallback()
    {
        using (var scope = new TableScope(Rows()))
        {
            Assert.That(scope.Table.TryGetReward(PopupStoreGradeTier.SSS, out var old, out _), Is.True);
            var replacement = Rows(); replacement[5] = new PopupStoreRewardEntry(PopupStoreGradeTier.SSS, 7, 12, 50, 90, 9, 1);
            scope.Set(replacement);
            Assert.That(scope.Table.TryValidate(out string error), Is.True, error);
            Assert.That(scope.Table.TryGetReward(PopupStoreGradeTier.SSS, out var current, out error), Is.True, error);
            Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(current, out decimal expected, out error), Is.True, error);
            Assert.That(expected, Is.EqualTo(7.88m));
            Assert.That(old.BasicDiamonds, Is.EqualTo(6)); Assert.That(current.BasicDiamonds, Is.EqualTo(7));
            Assert.That(Authored().TryGetReward(PopupStoreGradeTier.SSS, out var authored, out _), Is.True);
            Assert.That(authored.BasicDiamonds, Is.EqualTo(6), "Editing a memory table cannot modify the R0 asset.");
        }
    }

    [Test]
    public void ExportSortsADefensiveCopyWithoutReorderingOrExposingTheInspectorArray()
    {
        var reversed = Rows().Reverse().ToArray();
        using (var scope = new TableScope(reversed))
        {
            Assert.That(scope.Table.TryGetEntries(out var first, out string error), Is.True, error);
            Assert.That(scope.Table.TryGetEntries(out var second, out error), Is.True, error);
            Assert.That(first, Is.Not.SameAs(second)); Assert.That(first, Is.Not.SameAs(reversed));
            CollectionAssert.AreEqual(Rows().Select(row => row.Grade).ToArray(), first.Select(row => row.Grade).ToArray());
            CollectionAssert.AreEqual(Rows().Reverse().Select(row => row.Grade).ToArray(), ((PopupStoreRewardEntry[])Read(scope.Table, "_entries")).Select(row => row.Grade).ToArray());
            first[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 99, 99, 99, 100, 0, 0);
            Assert.That(scope.Table.TryGetReward(PopupStoreGradeTier.C, out var current, out _), Is.True);
            Assert.That(current.BasicDiamonds, Is.EqualTo(1)); Assert.That(second[0].BasicDiamonds, Is.EqualTo(1));
        }
    }

    [Test]
    public void PreviouslyReturnedRowsStayUnchangedAfterTheTableIsEdited()
    {
        using (var scope = new TableScope(Rows()))
        {
            Assert.That(scope.Table.TryGetEntries(out var retained, out _), Is.True);
            var changed = Rows(); changed[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 3, 3, 3, 34, 33, 33); scope.Set(changed);
            Assert.That(scope.Table.TryGetEntries(out var newer, out _), Is.True);
            Assert.That(retained[0].BasicDiamonds, Is.EqualTo(1)); Assert.That(newer[0].BasicDiamonds, Is.EqualTo(3));
            Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(retained[0], out decimal previous, out _), Is.True);
            Assert.That(previous, Is.EqualTo(1.23m));
        }
    }

    [Test]
    public void FailedCalculationReturnsNoUsableExpectationAndFailedExportNoPartialTable()
    {
        var invalid = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, 100, 100, 100);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(invalid, out decimal expected, out string error), Is.False);
        Assert.That(expected, Is.Zero); Assert.That(error, Is.Not.Null.And.Not.Empty);
        using (var scope = new TableScope(new[] { invalid }))
        {
            Assert.That(scope.Table.TryGetEntries(out var entries, out error), Is.False);
            Assert.That(entries == null || entries.Length == 0, Is.True);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void RepeatedValidAndInvalidQueriesAreDeterministicWithoutRandomDrawsOrAccountChanges()
    {
        var account = new AccountSnapshot();
        var random = UnityEngine.Random.state;
        var table = Authored(); string before = EditorJsonUtility.ToJson(table);
        Assert.That(table.TryGetEntries(out var entries, out _), Is.True);
        for (int repeat = 0; repeat < 32; repeat++)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                Assert.That(table.TryGetReward(entries[i].Grade, out var actual, out string error), Is.True, error);
                Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(actual, out decimal expected, out error), Is.True, error);
                Assert.That(expected, Is.EqualTo(new[] { 1.23m, 2.24m, 3.26m, 4.44m, 5.62m, 6.80m }[i]));
            }
            Assert.That(table.TryGetReward(PopupStoreGradeTier.None, out _, out _), Is.False);
            Assert.That(table.TryGetReward((PopupStoreGradeTier)99, out _, out _), Is.False);
            AssertInvalid(default(PopupStoreRewardEntry));
        }
        Assert.That(EditorJsonUtility.ToJson(table), Is.EqualTo(before));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); account.AssertUnchanged();
    }

    [Test]
    public void SerializedWeightsAreIntegersSoNaNInfinityAndFractionalProbabilitiesCannotEnterTheSchema()
    {
        foreach (string field in new[] { "_basicWeight", "_bonusWeight", "_jackpotWeight" })
        {
            var info = Field(typeof(PopupStoreRewardEntry), field);
            Assert.That(info.FieldType, Is.EqualTo(typeof(int)));
            Assert.That(info.IsDefined(typeof(SerializeField), true), Is.True);
        }
        foreach (var property in typeof(PopupStoreRewardEntry).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            Assert.That(property.GetSetMethod(false), Is.Null, property.Name + " is read-only to callers.");
    }

    [TestCase("ko-KR")]
    [TestCase("en-US")]
    [TestCase("de-DE")]
    public void EditorDiagnosticsShowAllSixCurrentR0RowsWithCultureIndependentAmountsAndExpectations(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            string report = PopupStoreRewardTableEditor.BuildReport(Authored(), out bool valid);
            Assert.That(valid, Is.True); Assert.That(report, Does.Contain("R0 보상표 VALID"));
            string[] lines = report.Split('\n').Where(line => line.Contains(": 기본 ")).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "C: 기본 1D(85%) / 추가 2D(14%) / 대박 10D(1%) | 합계 100% | 기대값 1.23D",
                "B: 기본 2D(85%) / 추가 3D(14%) / 대박 12D(1%) | 합계 100% | 기대값 2.24D",
                "A: 기본 3D(85%) / 추가 4D(14%) / 대박 15D(1%) | 합계 100% | 기대값 3.26D",
                "S: 기본 4D(85%) / 추가 6D(14%) / 대박 20D(1%) | 합계 100% | 기대값 4.44D",
                "SS: 기본 5D(85%) / 추가 8D(14%) / 대박 25D(1%) | 합계 100% | 기대값 5.62D",
                "SSS: 기본 6D(85%) / 추가 10D(14%) / 대박 30D(1%) | 합계 100% | 기대값 6.80D"
            }, lines);
            if (culture == "ko-KR") Debug.Log("POPUP_STORE_REWARD_TABLE_DIAGNOSTICS\n" + report);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void EditorDiagnosticsRebuildAfterValidInvalidAndCorrectedMemoryEditsWithoutStaleStatus()
    {
        using (var scope = new TableScope(Rows()))
        {
            string original = PopupStoreRewardTableEditor.BuildReport(scope.Table, out bool valid);
            Assert.That(valid, Is.True);
            var edited = Rows(); edited[5] = new PopupStoreRewardEntry(PopupStoreGradeTier.SSS, 7, 12, 50, 90, 9, 1); scope.Set(edited);
            string changed = PopupStoreRewardTableEditor.BuildReport(scope.Table, out valid);
            Assert.That(valid, Is.True);
            Assert.That(changed, Does.Contain("SSS: 기본 7D(90%) / 추가 12D(9%) / 대박 50D(1%) | 합계 100% | 기대값 7.88D"));
            Assert.That(changed, Does.Not.Contain("6.80D"));
            edited[5] = new PopupStoreRewardEntry(PopupStoreGradeTier.SSS, 7, 12, 50, 90, 9, 0); scope.Set(edited);
            string invalid = PopupStoreRewardTableEditor.BuildReport(scope.Table, out valid);
            Assert.That(valid, Is.False); Assert.That(invalid, Does.Contain("INVALID")); Assert.That(invalid, Does.Not.Contain("기대값 7.88D"));
            scope.Set(Rows());
            string repaired = PopupStoreRewardTableEditor.BuildReport(scope.Table, out valid);
            Assert.That(valid, Is.True); Assert.That(repaired, Is.EqualTo(original));
        }
    }

    [Test]
    public void EditorDiagnosticsReportAMissingTableWithoutInventingAnR0Fallback()
    {
        string report = PopupStoreRewardTableEditor.BuildReport(null, out bool valid);
        Assert.That(valid, Is.False); Assert.That(report, Is.Not.Null.And.Not.Empty);
        Assert.That(report, Does.Contain("없음")); Assert.That(report, Does.Not.Contain("기대값 1.23D"));
    }

    [TestCase(PopupStoreGradeTier.None)]
    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.S)]
    public void ReadingByTheActualFinalGradePreservesItsReceiptsSpritesAndExistingReturnToFloor3(PopupStoreGradeTier tier)
    {
        using (var host = new ResultHost())
        {
            host.Call("Start", 90d); if (tier != PopupStoreGradeTier.None) host.Call("SellUntil", tier); host.Call("End"); host.Call("ShowResult");
            var grade = host.Get<PopupStoreGradeSession>("Grade"); var final = grade.FinalResult;
            var result = host.Get<PopupStoreResultPanel>("Result"); var packing = host.Get<PopupStorePacking>("Packing");
            var score = host.Get<PopupStoreScore>("Score"); var box = host.Get<GameObject>("Root").GetComponent<PopupStoreRewardBox>();
            var committed = packing.CommittedReceipts.ToArray(); var queued = ((Queue<PopupStorePackingReceipt>)Read(packing, "_receipts")).ToArray();
            var processed = score.ProcessedReceiptIds.ToArray(); Sprite sprite = box.DisplayedSprite;
            var account = new AccountSnapshot(); var random = UnityEngine.Random.state;
            for (int i = 0; i < 3; i++)
            {
                bool found = Authored().TryGetReward(final.Grade, out var entry, out string error);
                Assert.That(found, Is.EqualTo(tier != PopupStoreGradeTier.None)); Assert.That(error, Is.Null);
                if (found) Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out _, out error), Is.True, error);
            }
            Assert.That(grade.FinalResult, Is.SameAs(final)); Assert.That(result.ShownResult, Is.SameAs(final)); Assert.That(grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(box.DisplayedSprite, Is.SameAs(sprite)); Assert.That(score.TotalScore, Is.EqualTo(final.Score));
            Assert.That(score.SuccessfulOrderCount, Is.EqualTo(final.SuccessfulOrders));
            CollectionAssert.AreEqual(committed, packing.CommittedReceipts.ToArray()); CollectionAssert.AreEqual(queued, ((Queue<PopupStorePackingReceipt>)Read(packing, "_receipts")).ToArray());
            CollectionAssert.AreEquivalent(processed, score.ProcessedReceiptIds.ToArray());
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); account.AssertUnchanged();
            Assert.That(result.TryConfirm(), Is.True); Assert.That(host.Get<GameObject>("Root").activeSelf, Is.False);
            host.Call("DisableObservers");
            Assert.That(host.Get<MobileUINavigation>("Navigation").FirstView, Is.SameAs(host.Get<UIFloor3>("Floor")));
            Assert.That(host.Get<MobileUINavigation>("Navigation").Count, Is.EqualTo(1)); Assert.That(result.ConfirmationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void DataInspectionLeavesAuthoredPrefabSpritesSceneUndoSelectionAndManagersUntouched()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture(); var account = new AccountSnapshot();
        var table = Authored(); bool dirty = EditorUtility.IsDirty(table); string serialized = EditorJsonUtility.ToJson(table);
        var paths = new List<string> { PrefabPath, PrefabPath + ".meta", AssetPath, AssetPath + ".meta" };
        foreach (var row in Rows())
        {
            string path = "Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + row.Grade + "_APPROVED.png";
            paths.Add(path); paths.Add(path + ".meta");
        }
        var hashes = paths.ToDictionary(path => path, Hash);
        using (var scope = new TableScope(Rows().Reverse().ToArray()))
        {
            Assert.That(scope.Table.TryGetEntries(out var entries, out string error), Is.True, error);
            foreach (var entry in entries) Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out _, out error), Is.True, error);
        }
        Assert.That(table.TryValidate(out string validationError), Is.True, validationError);
        Assert.That(EditorUtility.IsDirty(table), Is.EqualTo(dirty)); Assert.That(EditorJsonUtility.ToJson(table), Is.EqualTo(serialized));
        foreach (var pair in hashes) Assert.That(Hash(pair.Key), Is.EqualTo(pair.Value), pair.Key);
        account.AssertUnchanged(); witness.AssertUnchanged("popup-store reward table data inspection");
    }

    private static PopupStoreRewardTable Authored()
    {
        var table = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(AssetPath);
        Assert.That(table, Is.Not.Null, "The R0 data must remain an authored asset.");
        Assert.That(AssetDatabase.GUIDToAssetPath(AssetDatabase.AssetPathToGUID(AssetPath)), Is.EqualTo(AssetPath));
        return table;
    }
    private static PopupStoreRewardEntry[] Rows() => new[]
    {
        new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, 85, 14, 1),
        new PopupStoreRewardEntry(PopupStoreGradeTier.B, 2, 3, 12, 85, 14, 1),
        new PopupStoreRewardEntry(PopupStoreGradeTier.A, 3, 4, 15, 85, 14, 1),
        new PopupStoreRewardEntry(PopupStoreGradeTier.S, 4, 6, 20, 85, 14, 1),
        new PopupStoreRewardEntry(PopupStoreGradeTier.SS, 5, 8, 25, 85, 14, 1),
        new PopupStoreRewardEntry(PopupStoreGradeTier.SSS, 6, 10, 30, 85, 14, 1)
    };
    private static void AssertInvalid(PopupStoreRewardEntry entry)
    {
        Assert.That(PopupStoreRewardCalculator.TryValidate(entry, out string error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out decimal expected, out error), Is.False);
        Assert.That(expected, Is.Zero); Assert.That(error, Is.Not.Null.And.Not.Empty);
    }
    private static void AssertInvalidTable(PopupStoreRewardTable table)
    {
        Assert.That(table.TryValidate(out string error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
        foreach (var grade in Rows().Select(row => row.Grade))
        {
            Assert.That(table.TryGetReward(grade, out _, out error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
        Assert.That(table.TryGetEntries(out var entries, out error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(entries == null || entries.Length == 0, Is.True);
    }
    private sealed class TableScope : IDisposable
    {
        internal readonly PopupStoreRewardTable Table;
        internal TableScope(PopupStoreRewardEntry[] entries) { Table = ScriptableObject.CreateInstance<PopupStoreRewardTable>(); Set(entries); }
        internal void Set(PopupStoreRewardEntry[] entries) => Field(typeof(PopupStoreRewardTable), "_entries").SetValue(Table, entries);
        public void Dispose() { if (Table != null) Object.DestroyImmediate(Table); }
    }
    // Only existing test-host helpers are reached by reflection; finalization and navigation remain production calls.
    private sealed class ResultHost : IDisposable
    {
        private readonly object _inner;
        internal ResultHost()
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
        }
        internal T Get<T>(string name) => (T)Read(_inner, name);
        internal object Call(string name, params object[] args)
        {
            try { return _inner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(_inner, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        public void Dispose() => ((IDisposable)_inner).Dispose();
    }
    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = Field(typeof(UserInfo), "_score").GetValue(null), _manager = Field(typeof(GameManager), "_instance").GetValue(null);
        private readonly BackendManager _backend = BackendManager.ExistingInstance;
        private readonly bool _sdk = BackEnd.Backend.IsInitialized;
        private readonly float _timeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits)); Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(Field(typeof(UserInfo), "_score").GetValue(null), Is.EqualTo(_score)); Assert.That(Field(typeof(GameManager), "_instance").GetValue(null), Is.SameAs(_manager));
            Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
}
