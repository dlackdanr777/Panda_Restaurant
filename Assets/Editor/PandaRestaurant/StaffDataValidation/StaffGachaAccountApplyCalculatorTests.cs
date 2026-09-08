#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffGachaAccountApplyCalculatorTests
{
    private readonly List<GachaStaffData> _wrappers = new List<GachaStaffData>();
    private readonly Dictionary<StaffData, string> _sourceSnapshots = new Dictionary<StaffData, string>();
    private readonly Dictionary<GachaStaffData, string> _wrapperSnapshots = new Dictionary<GachaStaffData, string>();
    private Random.State _randomState;

    [SetUp]
    public void SetUp() => _randomState = Random.state;

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState), "Random state changed");
            foreach (var entry in _sourceSnapshots)
                Assert.That(EditorJsonUtility.ToJson(entry.Key), Is.EqualTo(entry.Value), "Registered staff changed");
            foreach (var entry in _wrapperSnapshots)
                Assert.That(Describe(entry.Key), Is.EqualTo(entry.Value), "Draw wrapper changed");
        }
        finally
        {
            foreach (GachaStaffData wrapper in _wrappers)
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            _wrappers.Clear();
            _sourceSnapshots.Clear();
            _wrapperSnapshots.Clear();
        }
    }

    [Test]
    public void SingleNewStaff_PreservesOwnedLevelsAndBalanceAndAddsOnlyLevelOne()
    {
        StaffAccountSaveData snapshot = Account(55);
        var drawn = new List<GachaStaffData> { LoadStaff("STAFF23") };
        Assert.That(Calculate(snapshot, drawn, out var result, out string error), Is.True, error);
        Assert.That(result.UpdatedAccount, Is.Not.SameAs(snapshot));
        Assert.That(result.UpdatedAccount.Staff, Is.Not.SameAs(snapshot.Staff));
        AssertAccount(result.UpdatedAccount, 55);
        Assert.That(result.Acquisition.Items.Count, Is.EqualTo(1));
        Assert.That(result.Acquisition.Items[0].StaffId, Is.EqualTo("STAFF23"));
        Assert.That(result.Acquisition.Items[0].Rank, Is.EqualTo(Rank.Rare));
        Assert.That(result.Acquisition.Items[0].IsNew, Is.True);
        Assert.That(result.Acquisition.Items[0].PandaTokenReward, Is.Zero);
        CollectionAssert.AreEqual(new[] { "STAFF23" }, result.Acquisition.NewStaffIds);
        Assert.That(result.Acquisition.TotalPandaTokens, Is.Zero);
    }

    [Test]
    public void ElevenResults_PreserveOrderAward55AndRoundTripUpdatedBalance110()
    {
        StaffAccountSaveData snapshot = Account(55);
        List<GachaStaffData> drawn = Eleven(LoadStaff("STAFF01"), LoadStaff("STAFF23"));
        Assert.That(Calculate(snapshot, drawn, out var result, out string error), Is.True, error);
        AssertAccount(result.UpdatedAccount, 110);
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            result.Acquisition.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { Rank.Rare, Rank.Rare }.Concat(Enumerable.Repeat(Rank.Normal2, 9)),
            result.Acquisition.Items.Select(item => item.Rank));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            result.Acquisition.Items.Select(item => item.PandaTokenReward));
        CollectionAssert.AreEqual(new[] { "STAFF23" }, result.Acquisition.NewStaffIds);
        Assert.That(result.Acquisition.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(result.Acquisition.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(result.Acquisition.TotalPandaTokens, Is.EqualTo(55));
        string resultBefore = JsonConvert.SerializeObject(result);
        Assert.That(StaffAccountSaveConverter.TrySerialize(result.UpdatedAccount,
            out string json, out error), Is.True, error);
        StaffAccountSaveReadResult restored = StaffAccountSaveConverter.Read(json);
        Assert.That(restored.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success), restored.Error);
        AssertAccount(restored.Data, 110);
        Assert.That(StaffAccountSaveConverter.TrySerialize(restored.Data,
            out string again, out error), Is.True, error);
        Assert.That(again, Is.EqualTo(json));
        Assert.That(JsonConvert.SerializeObject(result), Is.EqualTo(resultBefore));
    }

    [Test]
    public void InvalidSnapshotsDrawsAndOverflow_ExposeNoPartialAccountOrAcquisition()
    {
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        StaffAccountSaveData snapshot = Account(55);
        StaffAccountSaveReadResult missing = StaffAccountSaveConverter.Read(null);
        StaffAccountSaveReadResult invalid = StaffAccountSaveConverter.Read("{}");
        Assert.That(missing.Status, Is.EqualTo(StaffAccountSaveReadStatus.Missing));
        Assert.That(invalid.Status, Is.EqualTo(StaffAccountSaveReadStatus.InvalidData));
        foreach (StaffAccountSaveData unavailable in new[] { null, missing.Data, invalid.Data })
            AssertFailure(unavailable, new[] { rare });
        var conflicting = new StaffAccountSaveData(1, new[]
        {
            new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF01", 4)
        }, 55);
        AssertFailure(conflicting, new[] { rare });
        AssertFailure(snapshot, null);
        AssertFailure(snapshot, new GachaStaffData[0]);
        AssertFailure(snapshot, new[] { normal, rare });
        AssertFailure(snapshot, new GachaStaffData[] { null });
        List<GachaStaffData> lateInvalid = Eleven(normal, rare);
        lateInvalid[10] = null;
        AssertFailure(snapshot, lateInvalid);

        AssertFailure(Account(long.MaxValue), new[] { normal }); // MaxValue + 5.
        // A pending new STAFF23 must not escape through either result property when the batch overflows.
        AssertFailure(Account(long.MaxValue), Eleven(normal, rare));
        Assert.That(Calculate(Account(long.MaxValue - 5), new[] { normal },
            out var exact, out string error), Is.True, error);
        Assert.That(exact.UpdatedAccount.PandaTokens, Is.EqualTo(long.MaxValue));
        Assert.That(exact.Acquisition.TotalPandaTokens, Is.EqualTo(5));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03" }, exact.UpdatedAccount.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4 }, exact.UpdatedAccount.Staff.Select(item => item.Level));
        Assert.That(Calculate(Account(long.MaxValue), new[] { rare },
            out var zeroReward, out error), Is.True, error);
        AssertAccount(zeroReward.UpdatedAccount, long.MaxValue);
        Assert.That(zeroReward.Acquisition.TotalPandaTokens, Is.Zero);
    }

    private static StaffAccountSaveData Account(long tokens) => new StaffAccountSaveData(1, new[]
    {
        new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4)
    }, tokens);

    private static List<GachaStaffData> Eleven(GachaStaffData normal, GachaStaffData rare) =>
        new[] { rare, rare }.Concat(Enumerable.Repeat(normal, 9)).ToList();

    private static void AssertAccount(StaffAccountSaveData account, long tokens)
    {
        Assert.That(account, Is.Not.Null);
        Assert.That(account.Version, Is.EqualTo(StaffAccountSaveConverter.CurrentVersion));
        Assert.That(account.PandaTokens, Is.EqualTo(tokens));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03", "STAFF23" }, account.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4, 1 }, account.Staff.Select(item => item.Level));
    }

    private static bool Calculate(StaffAccountSaveData snapshot, IReadOnlyList<GachaStaffData> drawn,
        out StaffGachaAccountApplyResult result, out string error)
    {
        string snapshotBefore = JsonConvert.SerializeObject(snapshot);
        GachaStaffData[] orderBefore = drawn?.ToArray();
        string[] dataBefore = drawn?.Select(Describe).ToArray();
        Random.State randomBefore = Random.state;
        try { return StaffGachaAccountApplyCalculator.TryCalculate(snapshot, drawn, out result, out error); }
        finally
        {
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(snapshotBefore));
            if (drawn != null)
            {
                CollectionAssert.AreEqual(orderBefore, drawn, "Draw input order changed");
                CollectionAssert.AreEqual(dataBefore, drawn.Select(Describe), "Draw input data changed");
            }
            Assert.That(Random.state, Is.EqualTo(randomBefore));
        }
    }

    private static void AssertFailure(StaffAccountSaveData snapshot, IReadOnlyList<GachaStaffData> drawn)
    {
        Assert.That(Calculate(snapshot, drawn, out var result, out string error), Is.False);
        Assert.That(result, Is.Null, "Failure exposed partial acquisition/account data");
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    private GachaStaffData LoadStaff(string id)
    {
        StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
        Assert.That(staff, Is.Not.Null, "Missing registered staff " + id);
        Assert.That(staff.Id, Is.EqualTo(id));
        if (!_sourceSnapshots.ContainsKey(staff)) _sourceSnapshots.Add(staff, EditorJsonUtility.ToJson(staff));
        GachaStaffData wrapper = GachaStaffData.Create(staff);
        _wrappers.Add(wrapper);
        _wrapperSnapshots.Add(wrapper, Describe(wrapper));
        return wrapper;
    }

    private static string Describe(GachaStaffData data) => data == null ? "null" : JsonConvert.SerializeObject(new
    {
        Instance = data.GetInstanceID(), Wrapper = EditorJsonUtility.ToJson(data),
        SourceInstance = data.StaffData == null ? 0 : data.StaffData.GetInstanceID(),
        Source = data.StaffData == null ? null : EditorJsonUtility.ToJson(data.StaffData)
    });
}
#endif
