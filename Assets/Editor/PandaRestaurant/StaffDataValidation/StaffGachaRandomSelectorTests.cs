#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffGachaRandomSelectorTests
{
    private readonly List<Object> _createdObjects = new List<Object>();
    private Texture2D _texture;
    private Sprite _sprite;

    [SetUp]
    public void SetUp()
    {
        _texture = Track(new Texture2D(2, 2));
        _sprite = Track(Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.zero));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void GradeProbabilities_TotalOneHundredPercent()
    {
        Assert.That(StaffGachaRandomSelector.TotalGradeProbability, Is.EqualTo(1f).Within(0.000001f));
    }

    [TestCase(0, "NORMAL")]
    [TestCase(59, "NORMAL")]
    [TestCase(60, "RARE")]
    [TestCase(79, "RARE")]
    [TestCase(80, "UNIQUE")]
    [TestCase(94, "UNIQUE")]
    [TestCase(95, "SPECIAL")]
    [TestCase(99, "SPECIAL")]
    public void GradeSelection_UsesApprovedBoundaries(int gradeRoll, string expectedId)
    {
        List<GachaData> candidates = CreateOnePerGradeCandidates();

        GachaStaffData selected = StaffGachaRandomSelector.Select(candidates, gradeRoll, 0);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected.Id, Is.EqualTo(expectedId));
    }

    [Test]
    public void Normal1AndNormal2_ShareOneUniformPool()
    {
        List<GachaData> candidates = new List<GachaData>
        {
            CreateCandidate("NORMAL_1", Rank.Normal1),
            CreateCandidate("NORMAL_2_A", Rank.Normal2),
            CreateCandidate("NORMAL_2_B", Rank.Normal2),
            CreateCandidate("RARE", Rank.Rare),
            CreateCandidate("UNIQUE", Rank.Unique),
            CreateCandidate("SPECIAL", Rank.Special),
        };

        Assert.That(StaffGachaRandomSelector.Select(candidates, 10, 0).Id, Is.EqualTo("NORMAL_1"));
        Assert.That(StaffGachaRandomSelector.Select(candidates, 10, 1).Id, Is.EqualTo("NORMAL_2_A"));
        Assert.That(StaffGachaRandomSelector.Select(candidates, 10, 2).Id, Is.EqualTo("NORMAL_2_B"));
    }

    [Test]
    public void GradeSelection_DoesNotDependOnCandidateCounts()
    {
        List<GachaData> candidates = new List<GachaData>
        {
            CreateCandidate("NORMAL_A", Rank.Normal1),
            CreateCandidate("NORMAL_B", Rank.Normal2),
            CreateCandidate("NORMAL_C", Rank.Normal2),
            CreateCandidate("RARE_A", Rank.Rare),
            CreateCandidate("RARE_B", Rank.Rare),
            CreateCandidate("UNIQUE_A", Rank.Unique),
            CreateCandidate("UNIQUE_B", Rank.Unique),
            CreateCandidate("UNIQUE_C", Rank.Unique),
            CreateCandidate("UNIQUE_D", Rank.Unique),
            CreateCandidate("SPECIAL", Rank.Special),
        };

        Assert.That(StaffGachaRandomSelector.Select(candidates, 59, 2).Id, Is.EqualTo("NORMAL_C"));
        Assert.That(StaffGachaRandomSelector.Select(candidates, 79, 1).Id, Is.EqualTo("RARE_B"));
        Assert.That(StaffGachaRandomSelector.Select(candidates, 94, 3).Id, Is.EqualTo("UNIQUE_D"));
        Assert.That(StaffGachaRandomSelector.Select(candidates, 99, 0).Id, Is.EqualTo("SPECIAL"));
    }

    [Test]
    public void DuplicateIds_DoNotIncreaseSelectionSlots()
    {
        List<GachaData> candidates = new List<GachaData>
        {
            CreateCandidate("NORMAL_A", Rank.Normal1),
            CreateCandidate("NORMAL_A", Rank.Normal2),
            CreateCandidate("NORMAL_B", Rank.Normal2),
            CreateCandidate("RARE", Rank.Rare),
            CreateCandidate("UNIQUE", Rank.Unique),
            CreateCandidate("SPECIAL", Rank.Special),
        };

        GachaStaffData selected = StaffGachaRandomSelector.Select(candidates, 10, 1);

        Assert.That(selected.Id, Is.EqualTo("NORMAL_B"));
    }

    [Test]
    public void InvalidCandidates_AreIgnoredWithoutChangingValidPools()
    {
        List<GachaData> candidates = CreateOnePerGradeCandidates();
        candidates.Insert(0, null);
        candidates.Add(CreateCandidate("INVALID_RANK", Rank.Length));
        candidates.Add(CreateCandidate("INVALID_NAME", Rank.Rare, string.Empty));
        candidates.Add(CreateCandidate("INVALID_SPRITE", Rank.Rare, "INVALID_SPRITE", false));

        GachaStaffData selected = StaffGachaRandomSelector.Select(candidates, 70, 0);

        Assert.That(selected.Id, Is.EqualTo("RARE"));
    }

    [Test]
    public void Selection_DoesNotMutateCandidateListAndAllowsRepeatResult()
    {
        List<GachaData> candidates = CreateOnePerGradeCandidates();
        GachaData[] originalOrder = candidates.ToArray();

        GachaStaffData first = StaffGachaRandomSelector.Select(candidates, 10, 0);
        GachaStaffData second = StaffGachaRandomSelector.Select(candidates, 10, 0);

        CollectionAssert.AreEqual(originalOrder, candidates);
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Selection_DoesNotChangeUserDataOrRaiseMutationEvents()
    {
        int originalDia = UserInfo.Dia;
        long originalMoney = UserInfo.Money;
        int originalSkinToken = UserInfo.SkinToken;
        int originalGachaCount = UserInfo.TotalUseGachaMachineCount;
        int mutationEventCount = 0;
        Action mutationHandler = () => mutationEventCount++;

        UserInfo.OnChangeDiaHandler += mutationHandler;
        UserInfo.OnGiveStaffHandler += mutationHandler;
        UserInfo.OnChangeSkinTokenHandler += mutationHandler;
        UserInfo.OnUseGachaMachineHandler += mutationHandler;

        try
        {
            GachaStaffData selected =
                StaffGachaRandomSelector.Select(CreateOnePerGradeCandidates(), 10, 0);

            Assert.That(selected, Is.Not.Null);
            Assert.That(UserInfo.Dia, Is.EqualTo(originalDia));
            Assert.That(UserInfo.Money, Is.EqualTo(originalMoney));
            Assert.That(UserInfo.SkinToken, Is.EqualTo(originalSkinToken));
            Assert.That(UserInfo.TotalUseGachaMachineCount, Is.EqualTo(originalGachaCount));
            Assert.That(mutationEventCount, Is.Zero);
        }
        finally
        {
            UserInfo.OnChangeDiaHandler -= mutationHandler;
            UserInfo.OnGiveStaffHandler -= mutationHandler;
            UserInfo.OnChangeSkinTokenHandler -= mutationHandler;
            UserInfo.OnUseGachaMachineHandler -= mutationHandler;
        }
    }

    [Test]
    public void ManagerEntryPoint_UsesSelectorWithCurrentResourceCandidates()
    {
        StaffData[] resourceStaff = Resources.LoadAll<StaffData>("StaffData");
        List<GachaStaffData> currentStaff = new List<GachaStaffData>(resourceStaff.Length);
        for (int i = 0; i < resourceStaff.Length; i++)
            currentStaff.Add(Track(GachaStaffData.Create(resourceStaff[i])));

        List<GachaData> candidates = currentStaff.Cast<GachaData>().ToList();
        Assert.That(candidates, Is.Not.Empty);
        Assert.That(
            currentStaff.Select(data => data.Id).Distinct(StringComparer.Ordinal).Count(),
            Is.EqualTo(currentStaff.Count));

        Random.State originalRandomState = Random.state;

        try
        {
            Random.InitState(20260907);
            GachaStaffData expected = StaffGachaRandomSelector.Select(candidates);
            Random.InitState(20260907);
            GachaData actual = StaffDataManager.GetRandomGachaStaffData(candidates);

            Assert.That(expected, Is.Not.Null);
            Assert.That(actual, Is.SameAs(expected));
        }
        finally
        {
            Random.state = originalRandomState;
        }
    }

    [Test]
    public void EmptyCandidates_FailClearly()
    {
        LogAssert.Expect(LogType.Error, "직원 가챠 후보 목록이 비어있습니다.");

        Assert.That(StaffGachaRandomSelector.Select(new List<GachaData>(), 0, 0), Is.Null);
    }

    [Test]
    public void NullCandidates_FailClearly()
    {
        LogAssert.Expect(LogType.Error, "직원 가챠 후보 목록이 비어있습니다.");

        Assert.That(
            StaffGachaRandomSelector.Select((IReadOnlyList<GachaData>)null, 0, 0),
            Is.Null);
    }

    [TestCase(Rank.Normal1, "노멀")]
    [TestCase(Rank.Rare, "레어")]
    [TestCase(Rank.Unique, "유니크")]
    [TestCase(Rank.Special, "스페셜")]
    public void MissingGrade_FailsBeforeSelectionWithoutRedistribution(Rank missingRank, string displayName)
    {
        List<GachaData> candidates = new List<GachaData>();
        if (missingRank != Rank.Normal1)
            candidates.Add(CreateCandidate("NORMAL", Rank.Normal2));
        if (missingRank != Rank.Rare)
            candidates.Add(CreateCandidate("RARE", Rank.Rare));
        if (missingRank != Rank.Unique)
            candidates.Add(CreateCandidate("UNIQUE", Rank.Unique));
        if (missingRank != Rank.Special)
            candidates.Add(CreateCandidate("SPECIAL", Rank.Special));

        LogAssert.Expect(
            LogType.Error,
            $"직원 가챠 후보에 {displayName} 등급 직원이 없습니다. 확률을 재분배하지 않고 추첨을 중단합니다.");

        Assert.That(StaffGachaRandomSelector.Select(candidates, 0, 0), Is.Null);
    }

    [Test]
    public void MissingGrade_PublicSelectionFailsWithoutConsumingRandomState()
    {
        List<GachaData> candidates = new List<GachaData>
        {
            CreateCandidate("NORMAL", Rank.Normal2),
            CreateCandidate("RARE", Rank.Rare),
            CreateCandidate("UNIQUE", Rank.Unique),
        };
        Random.State originalState = Random.state;

        try
        {
            Random.InitState(123456);
            int expectedNextValue = Random.Range(0, 100);
            Random.InitState(123456);
            LogAssert.Expect(
                LogType.Error,
                "직원 가챠 후보에 스페셜 등급 직원이 없습니다. 확률을 재분배하지 않고 추첨을 중단합니다.");

            Assert.That(StaffGachaRandomSelector.Select(candidates), Is.Null);
            Assert.That(Random.Range(0, 100), Is.EqualTo(expectedNextValue));
        }
        finally
        {
            Random.state = originalState;
        }
    }

    [TestCase(-1)]
    [TestCase(100)]
    public void InvalidGradeRoll_FailsClearly(int gradeRoll)
    {
        LogAssert.Expect(LogType.Error, "직원 가챠 등급 난수 값은 0 이상 100 미만이어야 합니다.");

        Assert.That(StaffGachaRandomSelector.Select(CreateOnePerGradeCandidates(), gradeRoll, 0), Is.Null);
    }

    private List<GachaData> CreateOnePerGradeCandidates()
    {
        return new List<GachaData>
        {
            CreateCandidate("NORMAL", Rank.Normal1),
            CreateCandidate("RARE", Rank.Rare),
            CreateCandidate("UNIQUE", Rank.Unique),
            CreateCandidate("SPECIAL", Rank.Special),
        };
    }

    private GachaStaffData CreateCandidate(
        string id,
        Rank rank,
        string displayName = null,
        bool includeSprite = true)
    {
        WaiterData staffData = Track(ScriptableObject.CreateInstance<WaiterData>());
        SerializedObject serializedData = new SerializedObject(staffData);
        serializedData.FindProperty("_id").stringValue = id;
        serializedData.FindProperty("_name").stringValue = displayName ?? id;
        serializedData.FindProperty("_rank").enumValueIndex = (int)rank;
        serializedData.FindProperty("_thumbnailSprite").objectReferenceValue = includeSprite ? _sprite : null;
        serializedData.ApplyModifiedPropertiesWithoutUndo();

        return Track(GachaStaffData.Create(staffData));
    }

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }
}
#endif
