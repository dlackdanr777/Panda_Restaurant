#if UNITY_EDITOR
using System;
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

public class StaffGachaRequestSessionTests
{
    private readonly List<GachaStaffData> _wrappers = new List<GachaStaffData>();
    private readonly Dictionary<StaffData, string> _sources = new Dictionary<StaffData, string>();
    private readonly Dictionary<GachaStaffData, string> _wrapperSnapshots = new Dictionary<GachaStaffData, string>();
    private Random.State _randomState;
    private string _gameState;

    [SetUp]
    public void SetUp() { _randomState = Random.state; _gameState = ReadGameState(); }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState));
            Assert.That(ReadGameState(), Is.EqualTo(_gameState), "User or payment state changed");
            foreach (var source in _sources)
                Assert.That(EditorJsonUtility.ToJson(source.Key), Is.EqualTo(source.Value), "Staff asset changed");
            foreach (var wrapper in _wrapperSnapshots)
                Assert.That(Describe(wrapper.Key), Is.EqualTo(wrapper.Value), "Wrapper changed");
        }
        finally
        {
            foreach (GachaStaffData wrapper in _wrappers)
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            _wrappers.Clear(); _sources.Clear(); _wrapperSnapshots.Clear();
        }
    }

    [Test]
    public void SingleRequest_LocksProcessingAndIndeterminateAndCompletesExactlyOnce()
    {
        var session = new StaffGachaRequestSession();
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Idle));
        Assert.That(session.CurrentRequest, Is.Null);
        Assert.That(session.CanStartNewRequest, Is.True);
        GachaStaffData rare = LoadStaff("STAFF23");
        var drawn = new List<GachaStaffData> { rare };
        const string id = " Request A "; // Nonblank IDs are preserved verbatim, not trimmed.
        Assert.That(Start(session, id, Account(55), 110, StaffGachaPurchaseType.Single,
            drawn, out string error), Is.True, error);
        StaffGachaSessionRequest request = session.CurrentRequest;
        Assert.That(request.RequestId, Is.EqualTo(id));
        AssertPlan(request.Plan, StaffGachaPurchaseType.Single, 110, 10, 1, 55);
        Assert.That(request.Plan.AccountResult.Acquisition.TotalPandaTokens, Is.Zero);
        foreach (PropertyInfo property in typeof(StaffGachaSessionRequest).GetProperties())
            Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
        Assert.That(typeof(StaffGachaRequestSession).GetProperty("CurrentRequest").GetSetMethod(false), Is.Null);
        string planBefore = JsonConvert.SerializeObject(request.Plan);
        drawn.Clear();
        Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(planBefore), "Plan aliases caller list");
        AssertBlockedStart(session, StaffGachaRequestState.Processing);
        AssertIgnored(session, id, (StaffGachaResponseKind)999);
        AssertIgnored(session, "Request A", StaffGachaResponseKind.SuccessConfirmed);
        AssertIgnored(session, " request a ", StaffGachaResponseKind.SuccessConfirmed);
        AssertIgnored(session, null, StaffGachaResponseKind.SuccessConfirmed);

        Assert.That(session.TryHandleResponse(id, StaffGachaResponseKind.Indeterminate, out var output), Is.True);
        Assert.That(output, Is.Null);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Indeterminate));
        AssertIgnored(session, id, StaffGachaResponseKind.Indeterminate);
        AssertIgnored(session, id, (StaffGachaResponseKind)999);
        AssertBlockedStart(session, StaffGachaRequestState.Indeterminate);
        Assert.That(session.TryHandleResponse(id, StaffGachaResponseKind.SuccessConfirmed, out output), Is.True);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Succeeded), "Output preceded terminal state");
        Assert.That(output, Is.SameAs(request));
        Assert.That(session.CurrentRequest, Is.SameAs(request));
        Assert.That(session.CanStartNewRequest, Is.True);
        foreach (StaffGachaResponseKind response in new[] { StaffGachaResponseKind.SuccessConfirmed,
                     StaffGachaResponseKind.UnappliedFailureConfirmed, StaffGachaResponseKind.Indeterminate })
            AssertIgnored(session, id, response);
        AssertFailedStart(session, id, Account(55), 110, StaffGachaPurchaseType.Single, new[] { rare });
        Assert.That(Start(session, "after-success", Account(55), 110, StaffGachaPurchaseType.Single,
            new[] { rare }, out error), Is.True, error);
        foreach (StaffGachaResponseKind response in new[] { StaffGachaResponseKind.SuccessConfirmed,
                     StaffGachaResponseKind.UnappliedFailureConfirmed, StaffGachaResponseKind.Indeterminate })
            AssertIgnored(session, id, response);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Processing));
        Assert.That(session.TryHandleResponse("after-success", StaffGachaResponseKind.SuccessConfirmed, out output), Is.True);
        Assert.That(output, Is.SameAs(session.CurrentRequest).And.Not.SameAs(request));
        Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(planBefore));
    }

    [Test]
    public void MultiRequest_UnappliedFailureAndNewRequestIgnoreOldOrConflictingResponses()
    {
        var session = new StaffGachaRequestSession();
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        List<GachaStaffData> drawn = Eleven(normal, rare);
        Assert.That(Start(session, "multi-A", Account(55), 110, StaffGachaPurchaseType.Multi,
            drawn, out string error), Is.True, error);
        StaffGachaSessionRequest first = session.CurrentRequest;
        AssertPlan(first.Plan, StaffGachaPurchaseType.Multi, 110, 100, 11, 110);
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            first.Plan.AccountResult.Acquisition.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            first.Plan.AccountResult.Acquisition.Items.Select(item => item.PandaTokenReward));
        Assert.That(first.Plan.AccountResult.Acquisition.TotalPandaTokens, Is.EqualTo(55));
        string firstBefore = JsonConvert.SerializeObject(first);
        Assert.That(session.TryHandleResponse("multi-A", StaffGachaResponseKind.Indeterminate, out var output), Is.True);
        Assert.That(output, Is.Null);
        Assert.That(session.TryHandleResponse("multi-A", StaffGachaResponseKind.UnappliedFailureConfirmed, out output), Is.True);
        Assert.That(output, Is.Null);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.FailedUnapplied));
        Assert.That(session.CanStartNewRequest, Is.True);
        Assert.That(session.CurrentRequest, Is.SameAs(first));
        AssertIgnored(session, "multi-A", StaffGachaResponseKind.SuccessConfirmed);
        AssertIgnored(session, "multi-A", StaffGachaResponseKind.UnappliedFailureConfirmed);
        AssertFailedStart(session, "multi-A", Account(55), 110, StaffGachaPurchaseType.Multi, drawn);

        Assert.That(Start(session, "multi-B", Account(55), 100, StaffGachaPurchaseType.Multi,
            drawn, out error), Is.True, error);
        StaffGachaSessionRequest second = session.CurrentRequest;
        Assert.That(second, Is.Not.SameAs(first));
        AssertPlan(second.Plan, StaffGachaPurchaseType.Multi, 100, 100, 11, 110);
        foreach (StaffGachaResponseKind response in new[] { StaffGachaResponseKind.Indeterminate,
                     StaffGachaResponseKind.SuccessConfirmed, StaffGachaResponseKind.UnappliedFailureConfirmed })
            AssertIgnored(session, "multi-A", response);
        AssertIgnored(session, "multi-B", (StaffGachaResponseKind)(-1));
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Processing));
        Assert.That(session.TryHandleResponse("multi-B", StaffGachaResponseKind.SuccessConfirmed, out output), Is.True);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Succeeded));
        Assert.That(output, Is.SameAs(second));
        AssertIgnored(session, "multi-B", StaffGachaResponseKind.SuccessConfirmed);
        AssertIgnored(session, "multi-B", StaffGachaResponseKind.UnappliedFailureConfirmed);
        Assert.That(JsonConvert.SerializeObject(first), Is.EqualTo(firstBefore));
    }

    [Test]
    public void FailedStarts_PreserveStateAndDoNotConsumeIdsOrExposePartialPlans()
    {
        var session = new StaffGachaRequestSession();
        GachaStaffData normal = LoadStaff("STAFF01"), rare = LoadStaff("STAFF23");
        var single = new[] { rare };
        foreach (string id in new[] { null, "", " ", "\t\r\n" })
            AssertFailedStart(session, id, Account(55), 110, StaffGachaPurchaseType.Single, single);
        AssertFailedStart(session, "Case", Account(55), 9, StaffGachaPurchaseType.Single, single);
        AssertFailedStart(session, "Case", null, 110, StaffGachaPurchaseType.Single, single);
        AssertFailedStart(session, "Case", new StaffAccountSaveData(1,
            new[] { new StaffAccountStaffRecord("STAFF01", 0) }, 55), 110, StaffGachaPurchaseType.Single, single);
        AssertFailedStart(session, "Case", Account(55), 110, StaffGachaPurchaseType.Single, null);
        AssertFailedStart(session, "Case", Account(55), 110, StaffGachaPurchaseType.Single, new GachaStaffData[] { null });
        AssertFailedStart(session, "Case", Account(55), 110, (StaffGachaPurchaseType)999, single);
        AssertFailedStart(session, "Case", Account(long.MaxValue), 110, StaffGachaPurchaseType.Single, new[] { normal });
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Idle));
        Assert.That(session.CurrentRequest, Is.Null);
        Assert.That(Start(session, "Case", Account(55), 110, StaffGachaPurchaseType.Single,
            single, out string error), Is.True, error);
        StaffGachaSessionRequest previous = session.CurrentRequest;
        Assert.That(session.TryHandleResponse("Case", StaffGachaResponseKind.UnappliedFailureConfirmed, out var output), Is.True);
        Assert.That(output, Is.Null);
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.FailedUnapplied));

        AssertFailedStart(session, "case", null, 110, StaffGachaPurchaseType.Single, single);
        AssertFailedStart(session, "case", Account(long.MaxValue), 110, StaffGachaPurchaseType.Single, new[] { normal });
        Assert.That(session.CurrentRequest, Is.SameAs(previous));
        Assert.That(Start(session, "case", Account(55), 110, StaffGachaPurchaseType.Single,
            single, out error), Is.True, error);
        Assert.That(session.CurrentRequest.RequestId, Is.EqualTo("case"));
        Assert.That(session.CurrentRequest, Is.Not.SameAs(previous));
        AssertIgnored(session, "Case", StaffGachaResponseKind.SuccessConfirmed);
        Assert.That(session.TryHandleResponse("case", StaffGachaResponseKind.SuccessConfirmed, out output), Is.True);
        Assert.That(output, Is.SameAs(session.CurrentRequest));
        Assert.That(session.State, Is.EqualTo(StaffGachaRequestState.Succeeded));
        AssertFailedStart(session, "after-success", Account(55), 9, StaffGachaPurchaseType.Single, single);
    }

    private static void AssertBlockedStart(StaffGachaRequestSession session, StaffGachaRequestState expected)
    {
        StaffGachaSessionRequest before = session.CurrentRequest;
        string planBefore = JsonConvert.SerializeObject(before);
        foreach (string id in new[] { before.RequestId, "unused-blocked-id" })
        {
            Assert.That(session.TryStart(id, Account(55), 110, StaffGachaPurchaseType.Single,
                new ThrowingDrawnStaff(), out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(session.State, Is.EqualTo(expected));
            Assert.That(session.CurrentRequest, Is.SameAs(before));
            Assert.That(session.CanStartNewRequest, Is.False);
            Assert.That(JsonConvert.SerializeObject(before), Is.EqualTo(planBefore));
        }
        AssertIgnored(session, "unused-blocked-id", StaffGachaResponseKind.SuccessConfirmed);
        AssertIgnored(session, "unused-blocked-id", StaffGachaResponseKind.UnappliedFailureConfirmed);
    }

    private static void AssertIgnored(StaffGachaRequestSession session, string id, StaffGachaResponseKind response)
    {
        StaffGachaRequestState state = session.State;
        StaffGachaSessionRequest request = session.CurrentRequest;
        bool canStart = session.CanStartNewRequest;
        string before = JsonConvert.SerializeObject(request);
        Assert.That(session.TryHandleResponse(id, response, out var output), Is.False);
        Assert.That(output, Is.Null);
        Assert.That(session.State, Is.EqualTo(state));
        Assert.That(session.CurrentRequest, Is.SameAs(request));
        Assert.That(session.CanStartNewRequest, Is.EqualTo(canStart));
        Assert.That(JsonConvert.SerializeObject(request), Is.EqualTo(before));
    }

    private static void AssertFailedStart(StaffGachaRequestSession session, string id, StaffAccountSaveData snapshot,
        int diamonds, StaffGachaPurchaseType type, IReadOnlyList<GachaStaffData> drawn)
    {
        StaffGachaRequestState state = session.State;
        StaffGachaSessionRequest request = session.CurrentRequest;
        bool canStart = session.CanStartNewRequest;
        string before = JsonConvert.SerializeObject(request);
        Assert.That(Start(session, id, snapshot, diamonds, type, drawn, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(session.State, Is.EqualTo(state));
        Assert.That(session.CurrentRequest, Is.SameAs(request));
        Assert.That(session.CanStartNewRequest, Is.EqualTo(canStart));
        Assert.That(JsonConvert.SerializeObject(request), Is.EqualTo(before));
    }

    private static bool Start(StaffGachaRequestSession session, string id, StaffAccountSaveData snapshot,
        int diamonds, StaffGachaPurchaseType type, IReadOnlyList<GachaStaffData> drawn, out string error)
    {
        string before = JsonConvert.SerializeObject(snapshot);
        GachaStaffData[] order = drawn?.ToArray();
        string[] data = drawn?.Select(Describe).ToArray();
        Random.State random = Random.state;
        try { return session.TryStart(id, snapshot, diamonds, type, drawn, out error); }
        finally
        {
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(before));
            if (drawn != null)
            {
                CollectionAssert.AreEqual(order, drawn);
                CollectionAssert.AreEqual(data, drawn.Select(Describe));
            }
            Assert.That(Random.state, Is.EqualTo(random));
        }
    }

    private static void AssertPlan(StaffGachaPurchasePlan plan, StaffGachaPurchaseType type,
        int diamonds, int cost, int count, long tokens)
    {
        Assert.That(plan.PurchaseType, Is.EqualTo(type));
        Assert.That(plan.ResultCount, Is.EqualTo(count));
        Assert.That(plan.DiamondsBefore, Is.EqualTo(diamonds));
        Assert.That(plan.DiamondCost, Is.EqualTo(cost));
        Assert.That(plan.DiamondsAfter, Is.EqualTo(diamonds - cost));
        Assert.That(plan.AccountResult.Acquisition.Items.Count, Is.EqualTo(count));
        Assert.That(plan.AccountResult.Acquisition.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(plan.AccountResult.Acquisition.Items.Count(item => item.IsDuplicate), Is.EqualTo(count - 1));
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

    private GachaStaffData LoadStaff(string id)
    {
        StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
        Assert.That(staff, Is.Not.Null, id);
        Assert.That(staff.Id, Is.EqualTo(id));
        if (!_sources.ContainsKey(staff)) _sources.Add(staff, EditorJsonUtility.ToJson(staff));
        GachaStaffData wrapper = GachaStaffData.Create(staff);
        _wrappers.Add(wrapper);
        _wrapperSnapshots.Add(wrapper, Describe(wrapper));
        return wrapper;
    }

    private static string Describe(GachaStaffData data) => data == null ? "null" : JsonConvert.SerializeObject(new
    {
        Instance = data.GetInstanceID(), Wrapper = EditorJsonUtility.ToJson(data),
        Source = data.StaffData == null ? null : EditorJsonUtility.ToJson(data.StaffData)
    });

    private sealed class ThrowingDrawnStaff : IReadOnlyList<GachaStaffData>
    {
        public int Count => throw new InvalidOperationException("Blocked start read Count");
        public GachaStaffData this[int index] => throw new InvalidOperationException("Blocked start read candidate");
        public IEnumerator<GachaStaffData> GetEnumerator() => throw new InvalidOperationException("Blocked start enumerated candidates");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static string ReadGameState()
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
            UserInfo.CurrentStage, UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas
        });
    }
}
#endif
