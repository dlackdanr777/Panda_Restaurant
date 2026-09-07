using System;
using System.Collections.Generic;

public static class StaffGachaRandomSelector
{
    private const int ProbabilityScale = 100;

    private enum GradeGroup
    {
        Normal,
        Rare,
        Unique,
        Special,
    }

    private readonly struct GradeProbability
    {
        public GradeProbability(GradeGroup group, int weight, string displayName)
        {
            Group = group;
            Weight = weight;
            DisplayName = displayName;
        }

        public GradeGroup Group { get; }
        public int Weight { get; }
        public string DisplayName { get; }
    }

    // 직원 가챠 등급 확률의 단일 설정 지점: 노멀 60%, 레어 20%, 유니크 15%, 스페셜 5%.
    private static readonly GradeProbability[] GradeProbabilities =
    {
        new GradeProbability(GradeGroup.Normal, 60, "노멀"),
        new GradeProbability(GradeGroup.Rare, 20, "레어"),
        new GradeProbability(GradeGroup.Unique, 15, "유니크"),
        new GradeProbability(GradeGroup.Special, 5, "스페셜"),
    };

    public static float TotalGradeProbability => (float)GetTotalGradeWeight() / ProbabilityScale;

    public static GachaStaffData Select(IReadOnlyList<GachaData> candidates)
    {
        return Select(
            candidates,
            maxExclusive => UnityEngine.Random.Range(0, maxExclusive),
            count => UnityEngine.Random.Range(0, count));
    }

    /// <summary>
    /// 승인된 등급 확률로 등급을 먼저 고른 뒤 해당 등급의 유효한 직원을 균등 선택한다.
    /// 명시적인 선택값은 실제 선택 로직의 결정적 검증에도 사용한다.
    /// </summary>
    public static GachaStaffData Select(
        IReadOnlyList<GachaData> candidates,
        int gradeRoll,
        int staffIndex)
    {
        return Select(candidates, _ => gradeRoll, _ => staffIndex);
    }

    private static GachaStaffData Select(
        IReadOnlyList<GachaData> candidates,
        Func<int, int> gradeRollSelector,
        Func<int, int> staffIndexSelector)
    {
        if (candidates == null || candidates.Count == 0)
        {
            DebugLog.LogError("직원 가챠 후보 목록이 비어있습니다.");
            return null;
        }

        int totalGradeWeight = GetTotalGradeWeight();
        if (totalGradeWeight != ProbabilityScale)
        {
            DebugLog.LogError($"직원 가챠 등급 확률 합계가 100%가 아닙니다: {totalGradeWeight}%");
            return null;
        }

        Dictionary<GradeGroup, List<GachaStaffData>> candidateGroups = CreateCandidateGroups();
        HashSet<string> addedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!(candidates[i] is GachaStaffData candidate)
                || !IsValidStaffData(candidate.StaffData)
                || !TryGetGradeGroup(candidate.StaffData.Rank, out GradeGroup group))
            {
                continue;
            }

            if (!addedIds.Add(candidate.StaffData.Id))
                continue;

            candidateGroups[group].Add(candidate);
        }

        for (int i = 0; i < GradeProbabilities.Length; i++)
        {
            GradeProbability probability = GradeProbabilities[i];
            if (candidateGroups[probability.Group].Count != 0)
                continue;

            DebugLog.LogError(
                $"직원 가챠 후보에 {probability.DisplayName} 등급 직원이 없습니다. 확률을 재분배하지 않고 추첨을 중단합니다.");
            return null;
        }

        int gradeRoll = gradeRollSelector(totalGradeWeight);
        if (gradeRoll < 0 || gradeRoll >= totalGradeWeight)
        {
            DebugLog.LogError(
                $"직원 가챠 등급 난수 값은 0 이상 {totalGradeWeight} 미만이어야 합니다.");
            return null;
        }

        GradeGroup selectedGroup = SelectGradeGroup(gradeRoll);
        List<GachaStaffData> selectedGradeCandidates = candidateGroups[selectedGroup];
        int selectedIndex = staffIndexSelector(selectedGradeCandidates.Count);
        if (selectedIndex < 0 || selectedIndex >= selectedGradeCandidates.Count)
        {
            DebugLog.LogError(
                $"직원 가챠 등급 내 선택 인덱스가 범위를 벗어났습니다: {selectedIndex}/{selectedGradeCandidates.Count}");
            return null;
        }

        return selectedGradeCandidates[selectedIndex];
    }

    internal static bool IsValidStaffData(StaffData data)
    {
        return data != null
               && !string.IsNullOrWhiteSpace(data.Id)
               && !string.IsNullOrWhiteSpace(data.Name)
               && TryGetGradeGroup(data.Rank, out _)
               && (data.ThumbnailSprite != null || data.Sprite != null);
    }

    private static Dictionary<GradeGroup, List<GachaStaffData>> CreateCandidateGroups()
    {
        Dictionary<GradeGroup, List<GachaStaffData>> groups =
            new Dictionary<GradeGroup, List<GachaStaffData>>(GradeProbabilities.Length);

        for (int i = 0; i < GradeProbabilities.Length; i++)
        {
            groups.Add(GradeProbabilities[i].Group, new List<GachaStaffData>());
        }

        return groups;
    }

    private static GradeGroup SelectGradeGroup(int gradeRoll)
    {
        int cumulativeWeight = 0;
        for (int i = 0; i < GradeProbabilities.Length; i++)
        {
            cumulativeWeight += GradeProbabilities[i].Weight;
            if (gradeRoll < cumulativeWeight || i == GradeProbabilities.Length - 1)
                return GradeProbabilities[i].Group;
        }

        throw new InvalidOperationException("직원 가챠 등급을 선택할 수 없습니다.");
    }

    private static int GetTotalGradeWeight()
    {
        int totalWeight = 0;
        for (int i = 0; i < GradeProbabilities.Length; i++)
        {
            totalWeight += GradeProbabilities[i].Weight;
        }

        return totalWeight;
    }

    private static bool TryGetGradeGroup(Rank rank, out GradeGroup group)
    {
        switch (rank)
        {
            case Rank.Normal1:
            case Rank.Normal2:
                group = GradeGroup.Normal;
                return true;
            case Rank.Rare:
                group = GradeGroup.Rare;
                return true;
            case Rank.Unique:
                group = GradeGroup.Unique;
                return true;
            case Rank.Special:
                group = GradeGroup.Special;
                return true;
            default:
                group = default;
                return false;
        }
    }

}
