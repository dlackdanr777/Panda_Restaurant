using System.Collections.Generic;

/// <summary>
/// 고정 가격 검증과 기존 직원/토큰 계산을 연결하는 미저장 계산안이다.
/// 실제 다이아 조회/차감, 지급, 저장 또는 거래 완료를 수행하지 않는다.
/// </summary>
public static class StaffGachaPurchasePlanCalculator
{
    public static bool TryCalculate(
        StaffAccountSaveData snapshot, int diamondBalance, StaffGachaPurchaseType purchaseType,
        IReadOnlyList<GachaStaffData> drawnStaff, out StaffGachaPurchasePlan result, out string error)
    {
        result = null;
        error = null;
        int diamondCost, resultCount;
        switch (purchaseType)
        {
            case StaffGachaPurchaseType.Single: diamondCost = 10; resultCount = 1; break;
            case StaffGachaPurchaseType.Multi: diamondCost = 100; resultCount = 11; break;
            default:
                error = "유효하지 않은 직원 뽑기 종류입니다.";
                return false;
        }
        if (diamondBalance < 0)
        {
            error = "다이아 잔액은 음수일 수 없습니다.";
            return false;
        }
        if (drawnStaff == null || drawnStaff.Count != resultCount)
        {
            error = "선택한 뽑기 종류에 필요한 직원 결과 개수가 일치하지 않습니다.";
            return false;
        }
        if (diamondBalance < diamondCost)
        {
            error = "다이아 잔액이 부족합니다.";
            return false;
        }
        if (!StaffGachaAccountApplyCalculator.TryCalculate(snapshot, drawnStaff, out var accountResult, out error))
            return false;

        result = new StaffGachaPurchasePlan(purchaseType, resultCount, diamondBalance,
            diamondCost, diamondBalance - diamondCost, accountResult);
        return true;
    }
}

public enum StaffGachaPurchaseType
{
    Single,
    Multi
}

/// <summary>
/// 전체 성공 때만 생성되는 불변 메모리 계산안. 실제 잔액/보유 갱신이나 완료 표식이 아니다.
/// 다이아는 기존 int 표현이며 공용 저장 Version 1 JSON에 필드를 추가하지 않는다.
/// </summary>
public sealed class StaffGachaPurchasePlan
{
    public StaffGachaPurchaseType PurchaseType { get; }
    public int ResultCount { get; }
    public int DiamondsBefore { get; }
    public int DiamondCost { get; }
    public int DiamondsAfter { get; }
    public StaffGachaAccountApplyResult AccountResult { get; }

    internal StaffGachaPurchasePlan(StaffGachaPurchaseType purchaseType, int resultCount,
        int diamondsBefore, int diamondCost, int diamondsAfter, StaffGachaAccountApplyResult accountResult)
    {
        PurchaseType = purchaseType;
        ResultCount = resultCount;
        DiamondsBefore = diamondsBefore;
        DiamondCost = diamondCost;
        DiamondsAfter = diamondsAfter;
        AccountResult = accountResult;
    }
}
