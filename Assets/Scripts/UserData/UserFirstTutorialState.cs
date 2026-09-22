using System;
using Muks.BackEnd;

internal sealed class UserFirstTutorialState : IFirstTutorialState
{
    public FirstTutorialMemory Read() => UserInfo.ReadFirstTutorialMemory();
    public StageInfo Stage1 => UserInfo.FirstTutorialStage1;
    public bool TryCommit(FirstTutorialExecution operation, out string error) => UserInfo.CommitFirstTutorialMemory(operation, out error);
    public void NotifyReward(bool staffAdded, long gold) => UserInfo.NotifyFirstTutorialReward(staffAdded, gold);
}

public static partial class UserInfo
{
    internal static StageInfo FirstTutorialStage1 => _stageInfos[(int)EStage.Stage1];
    internal static FirstTutorialMemory ReadFirstTutorialMemory() => new FirstTutorialMemory(IsFirstTutorialClear,
        FirstTutorialStartRewardGranted, _money, _totalAddMoney, _dailyAddMoney, _weeklyAddMoney);
    internal static bool CommitFirstTutorialMemory(FirstTutorialExecution operation, out string error)
    {
        error = null;
        var before = operation.Before;
        var current = ReadFirstTutorialMemory();
        if (current.Clear != before.Clear || current.RewardGranted != before.RewardGranted || !current.CanAdd(operation.Gold))
        { error = "튜토리얼 보상 확정 상태가 변경되었습니다."; return false; }
        // No callbacks between the existing currency arithmetic, reward evidence, and caller's staff assignment.
        _staffCostCommitDepth++;
        try
        {
            if (operation.Gold != 0) AddMoney(operation.Gold);
            if (operation.IsCompletion) IsFirstTutorialClear = true;
            else FirstTutorialStartRewardGranted = true;
            return true;
        }
        finally { _staffCostCommitDepth--; }
    }
    internal static void NotifyFirstTutorialReward(bool added, long gold)
    {
        if (gold != 0) { DataBindMoney(); OnChangeMoneyHandler?.Invoke(); }
        if (added) OnGiveStaffEvent();
    }
}
