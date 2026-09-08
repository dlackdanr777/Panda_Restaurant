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

public class StaffGachaAcquisitionCalculatorTests
{
    private readonly List<Object> _createdObjects = new List<Object>();
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
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }
            _createdObjects.Clear();
        }
    }

    [Test]
    public void EmptyOwnedSnapshot_SingleUnownedStaffIsNewWithoutTokens()
    {
        var owned = new List<string>();
        GachaStaffData staff = CreateCandidate("A", Rank.Normal1);
        var drawn = new List<GachaStaffData> { staff };

        Assert.That(Calculate(owned, drawn, out var result, out var error), Is.True, error);
        Assert.That(result.Items.Count, Is.EqualTo(1));
        Assert.That(result.Items[0].StaffId, Is.EqualTo("A"));
        Assert.That(result.Items[0].Rank, Is.EqualTo(Rank.Normal1));
        Assert.That(result.Items[0].IsNew, Is.True);
        Assert.That(result.Items[0].IsDuplicate, Is.False);
        Assert.That(result.Items[0].PandaTokenReward, Is.Zero);
        CollectionAssert.AreEqual(new[] { "A" }, result.NewStaffIds);
        Assert.That(result.TotalPandaTokens, Is.Zero);
        AssertReadOnly(result.Items);
        AssertReadOnly(result.NewStaffIds);

        // Change only disposable fixture inputs after checking calculator immutability.
        owned.Add("AFTER");
        drawn.Clear();
        SetStaffField(staff.StaffData, "_id", "AFTER");
        Assert.That(result.Items[0].StaffId, Is.EqualTo("A"));
        CollectionAssert.AreEqual(new[] { "A" }, result.NewStaffIds);
        Assert.That(result.Items[0].GetType().GetProperties()
            .All(property => property.GetSetMethod() == null), Is.True);
    }

    [Test]
    public void ElevenResults_RecognizeSameBatchDuplicatesAndAward180Tokens()
    {
        GachaStaffData a = CreateCandidate("A", Rank.Normal1);
        GachaStaffData b = CreateCandidate("B", Rank.Normal2);
        GachaStaffData c = CreateCandidate("C", Rank.Rare);
        GachaStaffData d = CreateCandidate("D", Rank.Unique);
        GachaStaffData e = CreateCandidate("E", Rank.Special);
        var owned = new List<string> { "A", "C", "D", "E" };
        var drawn = new List<GachaStaffData> { a, b, b, c, d, e, a, c, d, e, b };

        Assert.That(Calculate(owned, drawn, out var result, out var error), Is.True, error);
        CollectionAssert.AreEqual(new[] { "A", "B", "B", "C", "D", "E", "A", "C", "D", "E", "B" },
            result.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 5, 0, 5, 10, 20, 50, 5, 10, 20, 50, 5 },
            result.Items.Select(item => item.PandaTokenReward));
        CollectionAssert.AreEqual(new[] { "B" }, result.NewStaffIds);
        Assert.That(result.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(result.Items[1].IsNew, Is.True);
        Assert.That(result.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(result.Items.All(item => item.IsNew != item.IsDuplicate), Is.True);
        Assert.That(result.TotalPandaTokens, Is.EqualTo(180));
    }

    [Test]
    public void InvalidInputs_FailWithoutPartialResultOrInputMutation()
    {
        GachaStaffData valid = CreateCandidate("VALID", Rank.Normal1);
        var emptyOwned = new List<string>();
        var single = new List<GachaStaffData> { valid };
        AssertFailure(null, single, "null owned snapshot");
        AssertFailure(emptyOwned, null, "null results");
        foreach (int count in new[] { 0, 2, 10, 12 })
            AssertFailure(emptyOwned, Enumerable.Repeat(valid, count).ToList(), "count " + count);

        foreach (string invalidId in new[] { null, "", " ", " A", "A ", "A B", "A\tB", "A\nB", "A\u00a0B" })
        {
            AssertFailure(new List<string> { invalidId }, single, "invalid owned ID");
            AssertFailure(emptyOwned, new[] { CreateCandidate(invalidId, Rank.Normal1) }, "invalid drawn ID");
        }
        foreach (Rank invalidRank in new[] { (Rank)(-1), Rank.Length, (Rank)999 })
            AssertFailure(emptyOwned, new[] { CreateCandidate("BAD_RANK", invalidRank) }, "invalid rank");

        GachaStaffData missingBacking = Track(ScriptableObject.CreateInstance<GachaStaffData>());
        GachaStaffData destroyedWrapper = CreateCandidate("DESTROYED_WRAPPER", Rank.Normal1);
        Object.DestroyImmediate(destroyedWrapper);
        GachaStaffData destroyedBacking = CreateCandidate("DESTROYED_BACKING", Rank.Normal1);
        Object.DestroyImmediate(destroyedBacking.StaffData);
        foreach (GachaStaffData invalid in new[] { null, missingBacking, destroyedWrapper, destroyedBacking })
            AssertFailure(emptyOwned, new[] { invalid }, "null or destroyed data");

        GachaStaffData idMismatch = CreateCandidate("WRAPPER_ID", Rank.Normal1);
        SetStaffField(idMismatch.StaffData, "_id", "BACKING_ID");
        GachaStaffData rankMismatch = CreateCandidate("RANK_MISMATCH", Rank.Normal1);
        SetStaffRank(rankMismatch.StaffData, Rank.Rare);
        foreach (GachaStaffData invalid in new[] { idMismatch, rankMismatch })
            AssertFailure(emptyOwned, new[] { invalid }, "wrapper/backing mismatch");

        foreach (Rank conflictingRank in new[] { Rank.Normal2, Rank.Rare, Rank.Unique, Rank.Special })
        {
            var lateConflict = Enumerable.Repeat(valid, 10).ToList();
            lateConflict.Add(CreateCandidate("VALID", conflictingRank));
            AssertFailure(emptyOwned, lateConflict, "same ID with conflicting rank");
        }
        var lateInvalid = Enumerable.Repeat(valid, 10).ToList();
        lateInvalid.Add(null);
        AssertFailure(emptyOwned, lateInvalid, "late invalid item must not leak a partial result");

        // Explicit empty ownership is valid; duplicated owned IDs also remain accepted.
        Assert.That(Calculate(emptyOwned, single, out var newResult, out var newError), Is.True, newError);
        Assert.That(newResult.Items[0].IsNew, Is.True);
        var repeatedOwned = new List<string> { "VALID", "VALID" };
        Assert.That(Calculate(repeatedOwned, single, out var duplicateResult, out var duplicateError), Is.True, duplicateError);
        Assert.That(duplicateResult.Items[0].IsDuplicate, Is.True);
        Assert.That(duplicateResult.TotalPandaTokens, Is.EqualTo(5));
    }

    private static bool Calculate(
        IReadOnlyCollection<string> owned, IReadOnlyList<GachaStaffData> drawn,
        out StaffGachaAcquisitionResult result, out string error)
    {
        string[] ownedBefore = owned?.ToArray();
        GachaStaffData[] drawnBefore = drawn?.ToArray();
        string[] dataBefore = drawn?.Select(DescribeCandidate).ToArray();
        Random.State randomBefore = Random.state;
        try
        {
            return StaffGachaAcquisitionCalculator.TryCalculate(owned, drawn, out result, out error);
        }
        finally
        {
            if (owned != null)
                CollectionAssert.AreEqual(ownedBefore, owned, "Owned input changed");
            if (drawn != null)
            {
                CollectionAssert.AreEqual(drawnBefore, drawn, "Draw order changed");
                CollectionAssert.AreEqual(dataBefore, drawn.Select(DescribeCandidate), "Staff fixture changed");
            }
            Assert.That(Random.state, Is.EqualTo(randomBefore), "Calculator consumed randomness");
        }
    }

    private static void AssertFailure(
        IReadOnlyCollection<string> owned, IReadOnlyList<GachaStaffData> drawn, string context)
    {
        Assert.That(Calculate(owned, drawn, out var result, out var error), Is.False, context);
        Assert.That(result, Is.Null, context);
        Assert.That(error, Is.Not.Null.And.Not.Empty, context);
    }

    private GachaStaffData CreateCandidate(string id, Rank rank)
    {
        WaiterData staff = Track(ScriptableObject.CreateInstance<WaiterData>());
        SetStaffField(staff, "_id", id);
        SetStaffField(staff, "_name", string.Empty);
        SetStaffRank(staff, rank);
        return Track(GachaStaffData.Create(staff));
    }

    private static void SetStaffField(StaffData staff, string field, string value)
    {
        var serialized = new SerializedObject(staff);
        serialized.FindProperty(field).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStaffRank(StaffData staff, Rank rank)
    {
        var serialized = new SerializedObject(staff);
        serialized.FindProperty("_rank").intValue = (int)rank;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string DescribeCandidate(GachaStaffData data)
    {
        if (ReferenceEquals(data, null)) return "null";
        if (data == null) return "destroyed wrapper";
        StaffData staff = data.StaffData;
        return JsonConvert.SerializeObject(new
        {
            data.Id, data.Rank,
            StaffId = staff == null ? null : staff.Id,
            StaffRank = staff == null ? (Rank?)null : staff.Rank,
            StaffName = staff == null ? null : staff.Name
        });
    }

    private static void AssertReadOnly(object collection)
    {
        if (collection is IList list)
        {
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list[0] = list[0]);
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

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }
}
#endif
