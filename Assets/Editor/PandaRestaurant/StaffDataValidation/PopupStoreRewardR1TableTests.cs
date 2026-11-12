using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Deterministic enumeration of supplied inputs, not a random-frequency experiment or payment test.</summary>
public sealed class PopupStoreRewardR1TableTests
{
    private const string R1Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset";
    private const string R0Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(PopupStoreGradeTier.C, 1, 3, 5, "1.206")]
    [TestCase(PopupStoreGradeTier.B, 3, 8, 15, "3.521")]
    [TestCase(PopupStoreGradeTier.A, 5, 15, 30, "6.045")]
    [TestCase(PopupStoreGradeTier.S, 10, 20, 100, "11.240")]
    [TestCase(PopupStoreGradeTier.SS, 20, 30, 200, "21.510")]
    [TestCase(PopupStoreGradeTier.SSS, 30, 50, 300, "32.750")]
    public void ApprovedR1RowsUseExactAmountsCommonWeightsAndDecimalExpectation(PopupStoreGradeTier grade,
        int basic, int bonus, int jackpot, string expectation)
    {
        var table = Authored();
        Assert.That(table.TryValidate(out string error), Is.True, error);
        Assert.That(table.Schema, Is.EqualTo(PopupStoreRewardSchema.R1Permille));
        Assert.That(table.PolicyId, Is.EqualTo("PopupStoreDiamond-R1-20261103"));
        Assert.That(table.Denominator, Is.EqualTo(1000));
        Assert.That(table.TryGetReward(grade, out var row, out error), Is.True, error);
        CollectionAssert.AreEqual(new[] { basic, bonus, jackpot }, Amounts(row));
        CollectionAssert.AreEqual(new[] { 900, 97, 3 }, Weights(row));
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(row, table.Denominator,
            out decimal expected, out error), Is.True, error);
        Assert.That(expected, Is.EqualTo(decimal.Parse(expectation, CultureInfo.InvariantCulture)));
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void EveryOneOfTheThousandInputsSelectsExactly900Basic97BonusAnd3Jackpot(PopupStoreGradeTier grade)
    {
        Assert.That(Authored().TryGetReward(grade, out var row, out _), Is.True);
        var counts = new int[3];
        var unityState = UnityEngine.Random.state;
        for (int roll = 0; roll < 1000; roll++)
        {
            Assert.That(PopupStoreRewardSelector.TrySelect(row, roll, 1000, out var kind, out int diamonds,
                out string error), Is.True, error);
            int expectedKind = roll < 900 ? 0 : roll < 997 ? 1 : 2;
            Assert.That((int)kind, Is.EqualTo(expectedKind), "roll=" + roll);
            Assert.That(diamonds, Is.EqualTo(Amounts(row)[expectedKind]), "roll=" + roll);
            counts[(int)kind]++;
        }
        CollectionAssert.AreEqual(new[] { 900, 97, 3 }, counts);
        Assert.That(UnityEngine.Random.state, Is.EqualTo(unityState));
        Debug.Log("POPUP_STORE_R1_EXHAUSTIVE grade=" + grade + " suppliedInputs=1000 basic=900 bonus=97 jackpot=3 randomDraws=0");
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void BothEdgesOfEachR1IntervalSelectTheirOwnAmountWithoutASecondDraw(PopupStoreGradeTier grade)
    {
        Assert.That(Authored().TryGetReward(grade, out var row, out _), Is.True);
        int[] rolls = { 0, 899, 900, 996, 997, 999 };
        for (int i = 0; i < rolls.Length; i++)
        {
            Assert.That(PopupStoreRewardSelector.TrySelect(row, rolls[i], 1000, out var kind, out int diamonds,
                out string error), Is.True, error);
            Assert.That((int)kind, Is.EqualTo(i / 2));
            Assert.That(diamonds, Is.EqualTo(Amounts(row)[i / 2]));
        }
    }

    [TestCase(-1)]
    [TestCase(1000)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void OutOfRangeRollsAreRejectedWithoutClampModuloOrAnAmount(int roll)
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.SSS, out var row, out _), Is.True);
        Assert.That(PopupStoreRewardSelector.TrySelect(row, roll, 1000, out _, out int diamonds,
            out string error), Is.False);
        Assert.That(diamonds, Is.Zero); Assert.That(error, Does.Contain("[0,1000)"));
    }

    [TestCase(-1, 998, 3)]
    [TestCase(900, -1, 101)]
    [TestCase(900, 101, -1)]
    [TestCase(900, 97, 2)]
    [TestCase(900, 97, 4)]
    [TestCase(int.MaxValue, int.MaxValue, 1002)]
    public void NegativeWrongTotalAndOverflowWeightsAreRejectedBeforeSelection(int basic, int bonus, int jackpot)
    {
        var row = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 3, 5, basic, bonus, jackpot);
        AssertInvalidRow(row);
        using (var scope = new TableScope())
        { var rows = scope.Rows(); rows[0] = row; scope.SetRows(rows); AssertInvalidTable(scope.Table); }
    }

    [TestCase(-1, 3, 5)]
    [TestCase(1, -1, 5)]
    [TestCase(1, 3, -1)]
    [TestCase(4, 3, 5)]
    [TestCase(1, 6, 5)]
    public void NegativeAndReversedAmountsCannotProduceASelectedReward(int basic, int bonus, int jackpot)
        => AssertInvalidRow(new PopupStoreRewardEntry(PopupStoreGradeTier.C, basic, bonus, jackpot, 900, 97, 3));

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(int.MaxValue)]
    public void UnknownSerializedSchemaCannotInferItsUnitFromWeightTotals(int schema)
    {
        using (var scope = new TableScope())
        { Write(scope.Table, "_schema", (PopupStoreRewardSchema)schema); AssertInvalidTable(scope.Table); Assert.That(scope.Table.Denominator, Is.Zero); }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("PopupStoreDiamond-R0")]
    [TestCase("PopupStoreDiamond-R1-20261104")]
    [TestCase("PopupStoreDiamond-R1-20261103 ")]
    public void R1RequiresItsExactApprovedPolicyIdentifier(string policy)
    {
        using (var scope = new TableScope())
        { Write(scope.Table, "_policyId", policy); AssertInvalidTable(scope.Table); }
    }

    [TestCase(0)]
    [TestCase(99)]
    [TestCase(999)]
    [TestCase(1001)]
    [TestCase(int.MaxValue)]
    public void UnsupportedExplicitUnitsAreRejectedByCalculatorSelectorAndPrng(int unit)
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.C, out var row, out _), Is.True);
        Assert.That(PopupStoreRewardCalculator.TryValidate(row, unit, out _), Is.False);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(row, unit, out decimal expected, out _), Is.False);
        Assert.That(expected, Is.Zero);
        Assert.That(PopupStoreRewardSelector.TrySelect(row, 0, unit, out _, out int diamonds, out _), Is.False);
        Assert.That(diamonds, Is.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PopupStoreRewardRandomSource().NextInt(unit));
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void MissingAndDuplicateGradesEachInvalidateTheWholeR1Table(PopupStoreGradeTier grade)
    {
        using (var scope = new TableScope())
        {
            var original = scope.Rows();
            scope.SetRows(original.Where(row => row.Grade != grade).ToArray()); AssertInvalidTable(scope.Table);
            var duplicate = original.ToArray(); int index = (int)grade - 1;
            duplicate[(index + 1) % 6] = duplicate[index];
            scope.SetRows(duplicate); AssertInvalidTable(scope.Table);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(7)]
    public void NoneAndUnknownSerializedGradesCannotReplaceAR1Row(int grade)
    {
        using (var scope = new TableScope())
        {
            var rows = scope.Rows(); rows[0] = new PopupStoreRewardEntry((PopupStoreGradeTier)grade, 1, 3, 5, 900, 97, 3);
            scope.SetRows(rows); AssertInvalidTable(scope.Table);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ArithmeticallyValidButUnapprovedR1ChangesDoNotSilentlyNormalizeOrFallBack(bool changeWeights)
    {
        using (var scope = new TableScope())
        {
            var rows = scope.Rows(); rows[0] = changeWeights
                ? new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 3, 5, 899, 98, 3)
                : new PopupStoreRewardEntry(PopupStoreGradeTier.C, 2, 4, 6, 900, 97, 3);
            Assert.That(PopupStoreRewardCalculator.TryValidate(rows[0], 1000, out _), Is.True);
            scope.SetRows(rows); AssertInvalidTable(scope.Table);
        }
    }

    [Test]
    public void R0AssetAndMetaRemainByteExactAndMissingFieldsExplicitlyMeanLegacy100()
    {
        Assert.That(Hash(R0Path), Is.EqualTo("CA57EE7E7D3BB478A1839D4E4A67DF2B6E2866F9837DB1148D45F582169BF1B0"));
        Assert.That(Hash(R0Path + ".meta"), Is.EqualTo("CAF0C7ADDAA4F99E21ACA350DDFC34C17AC2EAA1BC097F15F44178DE76A516F3"));
        string yaml = File.ReadAllText(R0Path);
        Assert.That(yaml, Does.Not.Contain("_schema").And.Not.Contain("_policyId"));
        var table = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R0Path);
        Assert.That(table.Schema, Is.EqualTo(PopupStoreRewardSchema.LegacyR0Percent));
        Assert.That(table.PolicyId, Is.EqualTo(PopupStoreRewardTable.LegacyPolicyId));
        Assert.That(table.Denominator, Is.EqualTo(100));
        Assert.That(table.TryCreateSnapshot(out var snapshot, out string error), Is.True, error);
        Assert.That(snapshot.Denominator, Is.EqualTo(100));
        Assert.That(snapshot.TryGetReward(PopupStoreGradeTier.C, out var row, out error), Is.True, error);
        Assert.That(PopupStoreRewardSelector.TrySelect(row, 99, snapshot.Denominator, out var kind, out int amount, out error), Is.True, error);
        Assert.That(kind, Is.EqualTo(PopupStoreRewardKind.Jackpot)); Assert.That(amount, Is.EqualTo(10));
    }

    [Test]
    public void HistoricalHundredUnitOverloadsNeverGuessThatAnR1RowMeansOneThousand()
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.C, out var row, out _), Is.True);
        Assert.That(PopupStoreRewardCalculator.TryValidate(row, out _), Is.False);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(row, out decimal expected, out _), Is.False);
        Assert.That(expected, Is.Zero);
        Assert.That(PopupStoreRewardSelector.TrySelect(row, 99, out _, out int diamonds, out _), Is.False);
        Assert.That(diamonds, Is.Zero);
    }

    [Test]
    public void R1SchemaWithR0WeightsAndLegacySchemaWithR1WeightsAreBothRejected()
    {
        using (var scope = new TableScope())
        {
            var rows = scope.Rows(); var r0 = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R0Path);
            Assert.That(r0.TryGetEntries(out var old, out _), Is.True); scope.SetRows(old); AssertInvalidTable(scope.Table);
            scope.SetRows(rows); Write(scope.Table, "_schema", PopupStoreRewardSchema.LegacyR0Percent);
            Write(scope.Table, "_policyId", PopupStoreRewardTable.LegacyPolicyId); AssertInvalidTable(scope.Table);
        }
    }

    [Test]
    public void ImmutablePolicySnapshotSurvivesSourceEditsDestructionAndExportedArrayEdits()
    {
        PopupStoreRewardPolicySnapshot snapshot;
        using (var scope = new TableScope())
        {
            Assert.That(scope.Table.TryCreateSnapshot(out snapshot, out string error), Is.True, error);
            Assert.That(snapshot.TryGetEntries(out var exported, out _), Is.True);
            exported[0] = default;
            scope.SetRows(Array.Empty<PopupStoreRewardEntry>()); Write(scope.Table, "_policyId", "changed");
            AssertInvalidTable(scope.Table);
        }
        Assert.That(snapshot.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId));
        Assert.That(snapshot.Schema, Is.EqualTo(PopupStoreRewardSchema.R1Permille));
        Assert.That(snapshot.Denominator, Is.EqualTo(1000));
        Assert.That(snapshot.TryGetEntries(out var first, out _), Is.True);
        Assert.That(snapshot.TryGetEntries(out var second, out _), Is.True);
        Assert.That(first, Is.Not.SameAs(second)); Assert.That(first, Has.Length.EqualTo(6));
        Assert.That(snapshot.TryGetReward(PopupStoreGradeTier.C, out var row, out _), Is.True);
        CollectionAssert.AreEqual(new[] { 1, 3, 5 }, Amounts(row));
        Assert.That(typeof(PopupStoreRewardPolicySnapshot).IsSealed, Is.True);
        foreach (var property in typeof(PopupStoreRewardPolicySnapshot).GetProperties())
            Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
    }

    [Test]
    public void SnapshotNoneAndUnknownQueriesDoNotCreateARewardOrAlterValidRows()
    {
        Assert.That(Authored().TryCreateSnapshot(out var snapshot, out _), Is.True);
        Assert.That(snapshot.TryGetReward(PopupStoreGradeTier.None, out _, out string error), Is.False);
        Assert.That(error, Is.Null);
        Assert.That(snapshot.TryGetReward((PopupStoreGradeTier)99, out _, out error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(snapshot.TryGetReward(PopupStoreGradeTier.SSS, out var row, out error), Is.True, error);
        Assert.That(row.JackpotDiamonds, Is.EqualTo(300));
    }

    [TestCase(100)]
    [TestCase(1000)]
    public void ActualPrngUsesTheRequestedExclusiveUpperBoundOnceWithoutChangingUnityRandom(int denominator)
    {
        var unityState = UnityEngine.Random.state;
        var recorder = new RecordingRandom(); var source = new PopupStoreRewardRandomSource();
        Write(source, "_random", recorder);
        Assert.That(source.NextInt(denominator), Is.EqualTo(denominator - 1));
        Assert.That(recorder.Calls, Is.EqualTo(1)); Assert.That(recorder.Minimum, Is.Zero);
        Assert.That(recorder.Maximum, Is.EqualTo(denominator));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(unityState));
    }

    [Test]
    public void AuthoredAndNewlyInstantiatedPopupUseR1WithoutAnR0Fallback()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); Assert.That(prefab, Is.Not.Null);
        var session = prefab.GetComponent<PopupStoreRewardSession>();
        Assert.That(Read(session, "_table"), Is.SameAs(Authored()));
        GameObject clone = null;
        try
        {
            clone = Object.Instantiate(prefab); Assert.That(clone.activeSelf, Is.False);
            Assert.That(Read(clone.GetComponent<PopupStoreRewardSession>(), "_table"), Is.SameAs(Authored()));
            Assert.That(clone.GetComponent<PopupStoreRewardSession>().Result, Is.Null);
        }
        finally { if (clone != null) Object.DestroyImmediate(clone); }
        Assert.That(AssetDatabase.AssetPathToGUID(R1Path), Is.EqualTo("44cd6378d68845c183a7efa349de7d71"));
    }

    private static void AssertInvalidRow(PopupStoreRewardEntry row)
    {
        Assert.That(PopupStoreRewardCalculator.TryValidate(row, 1000, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(row, 1000, out decimal expected, out _), Is.False);
        Assert.That(expected, Is.Zero);
        Assert.That(PopupStoreRewardSelector.TrySelect(row, 0, 1000, out _, out int diamonds, out _), Is.False);
        Assert.That(diamonds, Is.Zero);
    }
    private static void AssertInvalidTable(PopupStoreRewardTable table)
    {
        Assert.That(table.TryValidate(out string error), Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(table.TryGetEntries(out var rows, out _), Is.False); Assert.That(rows, Is.Empty);
        Assert.That(table.TryGetReward(PopupStoreGradeTier.C, out _, out _), Is.False);
        Assert.That(table.TryCreateSnapshot(out var snapshot, out _), Is.False); Assert.That(snapshot, Is.Null);
    }
    private static int[] Amounts(PopupStoreRewardEntry entry) => new[] { entry.BasicDiamonds, entry.BonusDiamonds, entry.JackpotDiamonds };
    private static int[] Weights(PopupStoreRewardEntry entry) => new[] { entry.BasicWeight, entry.BonusWeight, entry.JackpotWeight };
    private static PopupStoreRewardTable Authored() => AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R1Path);
    private static object Read(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static string Hash(string path)
    { using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }

    private sealed class TableScope : IDisposable
    {
        internal readonly PopupStoreRewardTable Table = Object.Instantiate(Authored());
        internal PopupStoreRewardEntry[] Rows()
        { Assert.That(Table.TryGetEntries(out var rows, out string error), Is.True, error); return rows; }
        internal void SetRows(PopupStoreRewardEntry[] entries) => Write(Table, "_entries", entries);
        public void Dispose() { if (Table != null) Object.DestroyImmediate(Table); }
    }
    private sealed class RecordingRandom : System.Random
    {
        internal int Calls, Minimum, Maximum;
        public override int Next(int minValue, int maxValue)
        { Calls++; Minimum = minValue; Maximum = maxValue; return maxValue - 1; }
    }
}
