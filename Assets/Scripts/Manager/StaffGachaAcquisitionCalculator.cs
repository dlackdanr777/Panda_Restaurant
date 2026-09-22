using System;
using System.Collections.Generic;

/// <summary>
/// 이미 추첨된 직원의 신규/중복 획득과 지급 예정 판다토큰만 계산한다.
/// 계정 전체 보유 ID 스냅샷은 호출자가 명시적으로 제공해야 한다.
/// 추첨, 실제 보유/재화 변경, 저장은 수행하지 않는다.
/// </summary>
public static class StaffGachaAcquisitionCalculator
{
    private const int SingleResultCount = 1;
    private const int MultiResultCount = 11;

    public static bool TryCalculate(
        IReadOnlyCollection<string> accountOwnedStaffIds,
        IReadOnlyList<GachaStaffData> drawnStaff,
        out StaffGachaAcquisitionResult result,
        out string error)
    {
        // 실패한 계산에서는 지급에 사용할 수 있는 부분 결과를 노출하지 않는다.
        result = null;
        error = null;

        if (accountOwnedStaffIds == null)
        {
            error = "계정 전체 보유 직원 ID 스냅샷이 필요합니다. 빈 목록은 명시적으로 전달해야 합니다.";
            return false;
        }

        if (drawnStaff == null ||
            (drawnStaff.Count != SingleResultCount && drawnStaff.Count != MultiResultCount))
        {
            error = "직원 추첨 결과는 1개 또는 11개여야 합니다.";
            return false;
        }

        var calculatedOwnedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string staffId in accountOwnedStaffIds)
        {
            if (!IsValidStaffId(staffId))
            {
                error = "보유 스냅샷에 유효하지 않은 직원 ID가 있습니다.";
                return false;
            }

            calculatedOwnedIds.Add(staffId);
        }

        // 모든 입력을 먼저 검증하고 값만 복사한다. 원본 직원 데이터는 결과에 보관하지 않는다.
        var staffIds = new string[drawnStaff.Count];
        var ranks = new Rank[drawnStaff.Count];
        var duplicateRewards = new int[drawnStaff.Count];
        var rankByStaffId = new Dictionary<string, Rank>(StringComparer.Ordinal);
        for (int i = 0; i < drawnStaff.Count; i++)
        {
            GachaStaffData staff = drawnStaff[i];
            if (staff == null || staff.StaffData == null || !IsValidStaffId(staff.Id) ||
                !TryGetDuplicatePandaTokens(staff.Rank, out int duplicateReward))
            {
                error = $"추첨 결과 {i + 1}의 직원 ID 또는 등급이 유효하지 않습니다.";
                return false;
            }

            if (!string.Equals(staff.Id, staff.StaffData.Id, StringComparison.Ordinal) ||
                staff.Rank != staff.StaffData.Rank)
            {
                error = $"추첨 결과 {i + 1}의 직원 원본과 ID 또는 등급이 일치하지 않습니다.";
                return false;
            }

            if (rankByStaffId.TryGetValue(staff.Id, out Rank previousRank) && previousRank != staff.Rank)
            {
                error = $"동일 직원 ID의 등급이 충돌합니다: {staff.Id}";
                return false;
            }

            rankByStaffId[staff.Id] = staff.Rank;
            staffIds[i] = staff.Id;
            ranks[i] = staff.Rank;
            duplicateRewards[i] = duplicateReward;
        }

        var items = new List<StaffGachaAcquisitionItem>(staffIds.Length);
        var newStaffIds = new List<string>();
        int totalPandaTokens = 0;
        for (int i = 0; i < staffIds.Length; i++)
        {
            // 계산용 복사본에만 추가하므로 다회 내 첫 획득 이후는 중복으로 처리된다.
            bool isNew = calculatedOwnedIds.Add(staffIds[i]);
            int pandaTokenReward = isNew ? 0 : duplicateRewards[i];
            if (isNew)
                newStaffIds.Add(staffIds[i]);

            items.Add(new StaffGachaAcquisitionItem(staffIds[i], ranks[i], isNew, pandaTokenReward));
            totalPandaTokens += pandaTokenReward;
        }

        result = new StaffGachaAcquisitionResult(items, newStaffIds, totalPandaTokens);
        return true;
    }

    private static bool IsValidStaffId(string staffId)
    {
        if (string.IsNullOrEmpty(staffId))
            return false;

        // ID를 임의로 Trim/대소문자 변환하지 않는다. 리소스 등록 여부 조회도 하지 않는다.
        foreach (char character in staffId)
        {
            if (char.IsWhiteSpace(character))
                return false;
        }

        return true;
    }

    private static bool TryGetDuplicatePandaTokens(Rank rank, out int amount)
    {
        // 판다토큰 지급 예정량의 단일 정책 지점. 기존 SkinToken과는 별개이다.
        switch (rank)
        {
            case Rank.Normal1:
            case Rank.Normal2:
                amount = 5;
                return true;
            case Rank.Rare:
                amount = 10;
                return true;
            case Rank.Unique:
                amount = 20;
                return true;
            case Rank.Special:
                amount = 50;
                return true;
            default:
                amount = 0;
                return false;
        }
    }
}

/// <summary>한 항목의 지급 예정 결과. 실제 보유 데이터나 재화 잔액이 아니다.</summary>
public sealed class StaffGachaAcquisitionItem
{
    public string StaffId { get; }
    public Rank Rank { get; }
    public bool IsNew { get; }
    public bool IsDuplicate => !IsNew;
    public int PandaTokenReward { get; }

    internal StaffGachaAcquisitionItem(string staffId, Rank rank, bool isNew, int pandaTokenReward)
    {
        StaffId = staffId;
        Rank = rank;
        IsNew = isNew;
        PandaTokenReward = pandaTokenReward;
    }
}

/// <summary>검증된 전체 결과만 반환한다. 목록과 항목은 외부에서 변경할 수 없다.</summary>
public sealed class StaffGachaAcquisitionResult
{
    public IReadOnlyList<StaffGachaAcquisitionItem> Items { get; }
    public IReadOnlyList<string> NewStaffIds { get; }
    public int TotalPandaTokens { get; }

    internal StaffGachaAcquisitionResult(
        List<StaffGachaAcquisitionItem> items, List<string> newStaffIds, int totalPandaTokens)
    {
        Items = Array.AsReadOnly(items.ToArray());
        NewStaffIds = Array.AsReadOnly(newStaffIds.ToArray());
        TotalPandaTokens = totalPandaTokens;
    }
}
