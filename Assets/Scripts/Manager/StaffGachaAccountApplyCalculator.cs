using System;
using System.Collections.Generic;

/// <summary>
/// 검증된 공용 스냅샷과 이미 정해진 직원 결과로 새 스냅샷만 계산한다.
/// 추첨, 실제 지급/재화 변경, 계정 조회, 이전 및 저장에는 연결하지 않는다.
/// </summary>
public static class StaffGachaAccountApplyCalculator
{
    public static bool TryCalculate(
        StaffAccountSaveData snapshot,
        IReadOnlyList<GachaStaffData> drawnStaff,
        out StaffGachaAccountApplyResult result,
        out string error)
    {
        // 토큰 합산을 포함한 전체 성공 전에는 보상 결과도 공용 데이터도 노출하지 않는다.
        result = null;
        if (!StaffAccountSaveConverter.Validate(snapshot, out error))
            return false;

        var ownedIds = new List<string>(snapshot.Staff.Count);
        foreach (StaffAccountStaffRecord record in snapshot.Staff)
            ownedIds.Add(record.Id);
        if (!StaffGachaAcquisitionCalculator.TryCalculate(ownedIds, drawnStaff,
                out StaffGachaAcquisitionResult acquisition, out error))
            return false;

        long pandaTokens;
        try
        {
            pandaTokens = checked(snapshot.PandaTokens + acquisition.TotalPandaTokens);
        }
        catch (OverflowException)
        {
            error = "판다토큰 잔액과 중복 보상의 합계가 long 범위를 초과합니다. 전체 반영하지 않습니다.";
            return false;
        }

        var updatedStaff = new List<StaffAccountStaffRecord>();
        foreach (StaffAccountStaffRecord record in snapshot.Staff)
            updatedStaff.Add(new StaffAccountStaffRecord(record.Id, record.Level));
        foreach (string staffId in acquisition.NewStaffIds)
        {
            // 기존 StageInfo.GiveStaff의 신규 초기 레벨만 따른다. 지급 메서드를 호출하지 않는다.
            updatedStaff.Add(new StaffAccountStaffRecord(staffId, 1));
        }

        var updatedAccount = new StaffAccountSaveData(snapshot.Version, updatedStaff, pandaTokens);
        result = new StaffGachaAccountApplyResult(acquisition, updatedAccount);
        return true;
    }
}

/// <summary>동일 입력에서 함께 계산된 불변 결과. 실패 시 이 객체 자체가 반환되지 않는다.</summary>
public sealed class StaffGachaAccountApplyResult
{
    public StaffGachaAcquisitionResult Acquisition { get; }
    public StaffAccountSaveData UpdatedAccount { get; }

    internal StaffGachaAccountApplyResult(StaffGachaAcquisitionResult acquisition, StaffAccountSaveData updatedAccount)
    {
        Acquisition = acquisition;
        UpdatedAccount = updatedAccount;
    }
}
