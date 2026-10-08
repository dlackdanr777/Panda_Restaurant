using System;
using Muks.BackEnd;

internal sealed class UserAttendanceState : IAttendanceState
{
    public AttendanceMemory Read() => UserInfo.ReadAttendanceMemory();
    public bool TryCommit(AttendanceClaim claim, out string error) => UserInfo.CommitAttendanceMemory(claim, out error);
    public void Notify(MoneyType type) => UserInfo.NotifyAttendanceReward(type);
}

public static partial class UserInfo
{
    internal static AttendanceMemory ReadAttendanceMemory() => new AttendanceMemory(_lastAttendanceTime, _totalAttendanceDays,
        _dia, _money, _totalAddMoney, _dailyAddMoney, _weeklyAddMoney);
    internal static bool CommitAttendanceMemory(AttendanceClaim claim, out string error)
    {
        error = null;
        var state = ReadAttendanceMemory();
        if (!claim.IsCurrent || !claim.Before.SameAttendance(state) || !state.CanAdd(claim.Type, claim.Amount))
        { error = "출석 확정 상태를 확인할 수 없습니다."; return false; }
        _staffCostCommitDepth++;
        try
        {
            if (claim.Type == MoneyType.Gold) AddMoney(claim.Amount); else AddDia(claim.Amount);
            _lastAttendanceTime = claim.Time; _totalAttendanceDays = claim.Day;
            return true;
        }
        finally { _staffCostCommitDepth--; }
    }
    internal static void NotifyAttendanceReward(MoneyType type)
    {
        void Notify(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[Attendance] Listener: " + ex.GetType().Name); }
        }
        if (type == MoneyType.Gold) { Notify(DataBindMoney); Notify(OnChangeMoneyHandler); }
        else { Notify(DataBindDia); Notify(OnChangeDiaHandler); }
        Notify(OnUpdateAttendanceDataHandler);
    }
}
