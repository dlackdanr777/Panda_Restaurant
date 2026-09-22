#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffGachaPurchasePlanCalculatorTests
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
            Assert.That(Random.state, Is.EqualTo(_randomState));
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
    public void IndependentSingleAndMultiPlans_PreserveSnapshotAndUseFixedPricesAndRewards()
    {
        StaffAccountSaveData snapshot = Account(55);
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        var singleDraw = new List<GachaStaffData> { rare };
        List<GachaStaffData> multiDraw = Eleven(normal, rare);
        Assert.That(Calculate(snapshot, 110, StaffGachaPurchaseType.Single, singleDraw,
            out var single, out string error), Is.True, error);
        AssertPlan(single, StaffGachaPurchaseType.Single, 110, 10, 1, 55);
        Assert.That(single.AccountResult.Acquisition.Items[0].IsNew, Is.True);
        Assert.That(single.AccountResult.Acquisition.TotalPandaTokens, Is.Zero);

        // Both plans start from the same original balance/account, not from the preceding plan.
        Assert.That(Calculate(snapshot, 110, StaffGachaPurchaseType.Multi, multiDraw,
            out var multi, out error), Is.True, error);
        AssertPlan(multi, StaffGachaPurchaseType.Multi, 110, 100, 11, 110);
        StaffGachaAcquisitionResult acquisition = multi.AccountResult.Acquisition;
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            acquisition.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            acquisition.Items.Select(item => item.PandaTokenReward));
        CollectionAssert.AreEqual(new[] { "STAFF23" }, acquisition.NewStaffIds);
        Assert.That(acquisition.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(acquisition.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(acquisition.TotalPandaTokens, Is.EqualTo(55));

        foreach (StaffGachaPurchasePlan plan in new[] { single, multi })
        {
            Assert.That(plan.AccountResult.UpdatedAccount, Is.Not.SameAs(snapshot));
            Assert.That(plan.AccountResult.UpdatedAccount.Staff, Is.Not.SameAs(snapshot.Staff));
            if (plan.AccountResult.UpdatedAccount.Staff is IList list) Assert.That(list.IsReadOnly, Is.True);
        }
        foreach (PropertyInfo property in typeof(StaffGachaPurchasePlan).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.That(property.CanRead, Is.True, property.Name);
            Assert.That(property.GetSetMethod(true), Is.Null, "Result property is not getter-only: " + property.Name);
        }
        Assert.That(single.AccountResult.UpdatedAccount, Is.Not.SameAs(multi.AccountResult.UpdatedAccount));
        string plansBefore = JsonConvert.SerializeObject(new[] { single, multi });
        singleDraw.Clear();
        multiDraw.Reverse(); // Mutate only the disposable caller lists after input-preservation checks.
        Assert.That(JsonConvert.SerializeObject(new[] { single, multi }), Is.EqualTo(plansBefore));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03" }, snapshot.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4 }, snapshot.Staff.Select(item => item.Level));
        Assert.That(snapshot.PandaTokens, Is.EqualTo(55));
    }

    [Test]
    public void DiamondAndCountBoundaries_EnforceEachPurchasePolicyWithoutClamping()
    {
        StaffAccountSaveData snapshot = Account(55);
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        var single = new[] { rare };
        List<GachaStaffData> multi = Eleven(normal, rare);
        foreach (StaffGachaPurchaseType type in new[] { StaffGachaPurchaseType.Single, StaffGachaPurchaseType.Multi })
        {
            int cost = type == StaffGachaPurchaseType.Single ? 10 : 100;
            int count = type == StaffGachaPurchaseType.Single ? 1 : 11;
            IReadOnlyList<GachaStaffData> drawn = type == StaffGachaPurchaseType.Single ? (IReadOnlyList<GachaStaffData>)single : multi;
            Assert.That(Calculate(snapshot, cost, type, drawn, out var exact, out string error), Is.True, error);
            Assert.That(exact.DiamondsAfter, Is.Zero);
            AssertPlan(exact, type, cost, cost, count, type == StaffGachaPurchaseType.Single ? 55 : 110);
            AssertFailure(snapshot, cost - 1, type, drawn, exact);
            AssertFailure(snapshot, -1, type, drawn, exact);
            AssertFailure(snapshot, cost, type, null, exact);
            Assert.That(Calculate(snapshot, int.MaxValue, type, drawn, out var maximum, out error), Is.True, error);
            Assert.That(maximum.DiamondsBefore, Is.EqualTo(int.MaxValue));
            Assert.That(maximum.DiamondsAfter, Is.EqualTo(int.MaxValue - cost));
            Assert.That(maximum.DiamondCost, Is.EqualTo(cost));
        }
        AssertFailure(snapshot, 110, (StaffGachaPurchaseType)(-1), single);
        AssertFailure(snapshot, 110, (StaffGachaPurchaseType)999, single);
        AssertFailure(snapshot, 110, StaffGachaPurchaseType.Single, multi);
        AssertFailure(snapshot, 110, StaffGachaPurchaseType.Multi, single);
        AssertFailure(snapshot, 110, StaffGachaPurchaseType.Multi, multi.Take(10).ToArray());
    }

    [Test]
    public void InvalidAccountDrawOrTokenOverflow_ClearsEvenPreviouslySuccessfulOutput()
    {
        StaffAccountSaveData snapshot = Account(55);
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        var single = new[] { rare };
        Assert.That(Calculate(snapshot, 110, StaffGachaPurchaseType.Single, single,
            out var previous, out string error), Is.True, error);
        var conflict = new StaffAccountSaveData(1, new[]
        {
            new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF01", 4)
        }, 55);
        foreach (StaffAccountSaveData invalid in new[] { null, conflict, Account(-1) })
            AssertFailure(invalid, 110, StaffGachaPurchaseType.Single, single, previous);
        AssertFailure(snapshot, 110, StaffGachaPurchaseType.Single, new GachaStaffData[] { null }, previous);
        List<GachaStaffData> lateInvalid = Eleven(normal, rare);
        lateInvalid[10] = null;
        AssertFailure(snapshot, 110, StaffGachaPurchaseType.Multi, lateInvalid, previous);
        AssertFailure(Account(long.MaxValue), 110, StaffGachaPurchaseType.Single, new[] { normal }, previous);
        AssertFailure(Account(long.MaxValue), 110, StaffGachaPurchaseType.Multi, Eleven(normal, rare), previous);
        Assert.That(Calculate(Account(long.MaxValue), 110, StaffGachaPurchaseType.Single, single,
            out var zeroReward, out error), Is.True, error);
        AssertPlan(zeroReward, StaffGachaPurchaseType.Single, 110, 10, 1, long.MaxValue);
        Assert.That(zeroReward.AccountResult.Acquisition.TotalPandaTokens, Is.Zero);
        AssertPlan(previous, StaffGachaPurchaseType.Single, 110, 10, 1, 55);
    }

    private static void AssertPlan(StaffGachaPurchasePlan plan, StaffGachaPurchaseType type,
        int before, int cost, int count, long tokens)
    {
        Assert.That(plan.PurchaseType, Is.EqualTo(type));
        Assert.That(plan.ResultCount, Is.EqualTo(count));
        Assert.That(plan.DiamondsBefore, Is.EqualTo(before));
        Assert.That(plan.DiamondCost, Is.EqualTo(cost));
        Assert.That(plan.DiamondsAfter, Is.EqualTo(before - cost));
        Assert.That(plan.AccountResult.Acquisition.Items.Count, Is.EqualTo(count));
        Assert.That(plan.AccountResult.UpdatedAccount.PandaTokens, Is.EqualTo(tokens));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03", "STAFF23" }, plan.AccountResult.UpdatedAccount.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4, 1 }, plan.AccountResult.UpdatedAccount.Staff.Select(item => item.Level));
    }

    private static StaffAccountSaveData Account(long tokens) => new StaffAccountSaveData(1, new[]
    {
        new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4)
    }, tokens);

    private static List<GachaStaffData> Eleven(GachaStaffData normal, GachaStaffData rare) =>
        new[] { rare, rare }.Concat(Enumerable.Repeat(normal, 9)).ToList();

    private static bool Calculate(StaffAccountSaveData snapshot, int diamonds, StaffGachaPurchaseType type,
        IReadOnlyList<GachaStaffData> drawn, out StaffGachaPurchasePlan result, out string error)
    {
        string snapshotBefore = JsonConvert.SerializeObject(snapshot);
        GachaStaffData[] orderBefore = drawn?.ToArray();
        string[] dataBefore = drawn?.Select(Describe).ToArray();
        Random.State randomBefore = Random.state;
        try { return StaffGachaPurchasePlanCalculator.TryCalculate(snapshot, diamonds, type, drawn, out result, out error); }
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

    private static void AssertFailure(StaffAccountSaveData snapshot, int diamonds, StaffGachaPurchaseType type,
        IReadOnlyList<GachaStaffData> drawn, StaffGachaPurchasePlan previous = null)
    {
        StaffGachaPurchasePlan result = previous;
        Assert.That(Calculate(snapshot, diamonds, type, drawn, out result, out string error), Is.False);
        Assert.That(result, Is.Null, "Failure retained a previous or partial purchase plan");
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
