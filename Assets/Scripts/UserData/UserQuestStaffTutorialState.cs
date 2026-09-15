using System;
using System.Globalization;
using Muks.BackEnd;

/// <summary>Four explicit main-quest grants; this evidence is separate from StaffAccount Version 1.</summary>
public static class QuestStaffTutorialPolicy
{
    public const string GrantFieldName = "QuestStaffGrantMask";

    public static bool TryGetMapping(string questId, out string staffId, out int bit)
    {
        switch (questId)
        {
            case "MainReward01": staffId = "STAFF06"; bit = 1; return true;
            case "MainReward04": staffId = "STAFF16"; bit = 2; return true;
            case "MainReward05": staffId = "STAFF01"; bit = 4; return true;
            case "MainReward19": staffId = "STAFF21"; bit = 8; return true;
            default: staffId = null; bit = 0; return false;
        }
    }

    public static bool IsValidGrantMask(int value) => value >= 0 && value <= 15;

    // Shared by the raw SDK validator and the flattened legacy loader. No coercion/defaulting.
    public static bool TryParseGrantMask(string raw, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        foreach (char c in raw) if (c < '0' || c > '9') return false;
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && IsValidGrantMask(value);
    }
}

/// <summary>A detached view of live quest progression, not caller-supplied grant authority.</summary>
public sealed class QuestStaffTutorialMemory
{
    public string CurrentQuestId { get; }
    public string RequiredStaffId { get; }
    public EStage Stage { get; }
    public bool FirstTutorialClear { get; }
    public bool DefinitionMatches { get; }
    public bool PrerequisitesCleared { get; }
    public bool IsDone { get; }
    public bool IsRewardClaimed { get; }
    // null identifies an older row without this field. It alone never authorizes a grant.
    public int? GrantMask { get; }

    public QuestStaffTutorialMemory(string currentQuestId, string requiredStaffId, EStage stage,
        bool firstTutorialClear, bool definitionMatches, bool prerequisitesCleared,
        bool isDone, bool isRewardClaimed, int? grantMask)
    {
        CurrentQuestId = currentQuestId; RequiredStaffId = requiredStaffId; Stage = stage;
        FirstTutorialClear = firstTutorialClear; DefinitionMatches = definitionMatches;
        PrerequisitesCleared = prerequisitesCleared; IsDone = isDone;
        IsRewardClaimed = isRewardClaimed; GrantMask = grantMask;
    }

    public bool IsEligible => Stage == EStage.Stage1 && FirstTutorialClear && DefinitionMatches
        && PrerequisitesCleared && !IsDone && !IsRewardClaimed
        && (!GrantMask.HasValue || QuestStaffTutorialPolicy.IsValidGrantMask(GrantMask.Value))
        && QuestStaffTutorialPolicy.TryGetMapping(CurrentQuestId, out var staff, out var bit)
        && string.Equals(RequiredStaffId, staff, StringComparison.Ordinal)
        && ((GrantMask ?? 0) & bit) == 0;

    public bool TryCreateGrantedMask(out int value, out string error)
    {
        value = 0; error = null;
        if (!IsEligible || !QuestStaffTutorialPolicy.TryGetMapping(CurrentQuestId, out _, out int bit))
        { error = "현재 고용 퀘스트의 무료 직원 획득 조건을 확인할 수 없습니다."; return false; }
        value = (GrantMask ?? 0) | bit;
        return true;
    }

    public bool Matches(QuestStaffTutorialMemory other) => other != null
        && CurrentQuestId == other.CurrentQuestId && RequiredStaffId == other.RequiredStaffId
        && Stage == other.Stage && FirstTutorialClear == other.FirstTutorialClear
        && DefinitionMatches == other.DefinitionMatches && PrerequisitesCleared == other.PrerequisitesCleared
        && IsDone == other.IsDone && IsRewardClaimed == other.IsRewardClaimed && GrantMask == other.GrantMask;
}

public interface IQuestStaffTutorialState
{
    QuestStaffTutorialMemory Read();
    // Synchronous and non-notifying. A false return or exception must leave the state unchanged.
    bool TryCommit(QuestStaffTutorialMemory expected, int grantedMask, out string error);
    void NotifyStaffAdded();
}

internal sealed class UserQuestStaffTutorialState : IQuestStaffTutorialState
{
    public QuestStaffTutorialMemory Read() => UserInfo.ReadQuestStaffTutorialMemory();
    public bool TryCommit(QuestStaffTutorialMemory expected, int grantedMask, out string error)
        => UserInfo.CommitQuestStaffTutorialMemory(expected, grantedMask, out error);
    public void NotifyStaffAdded() => UserInfo.OnGiveStaffEvent();
}

public static partial class UserInfo
{
    public static int? QuestStaffGrantMask { get; private set; }

    internal static QuestStaffTutorialMemory ReadQuestStaffTutorialMemory()
    {
        ChallengeManager manager = ChallengeManager.Instance;
        ChallengeData current = manager.GetCurrentMainChallengeData();
        string id = current?.Id;
        var employment = current as Type05ChallengeData;
        bool definition = QuestStaffTutorialPolicy.TryGetMapping(id, out var staffId, out _)
            && current.Challenges == Challenges.Main && current.Type == ChallengeType.TYPE05
            && employment != null && employment.NeedStaffId == staffId;
        bool prerequisites = definition;
        if (definition)
        {
            int ordinal = int.Parse(id.Substring("MainReward".Length), CultureInfo.InvariantCulture);
            for (int i = 1; i < ordinal; i++)
            {
                var previous = manager.GetCallengeData("MainReward" + i.ToString("D2", CultureInfo.InvariantCulture));
                if (previous == null || previous.Challenges != Challenges.Main || !GetIsClearChallenge(previous))
                { prerequisites = false; break; }
            }
        }
        return new QuestStaffTutorialMemory(id, employment?.NeedStaffId, CurrentStage, IsFirstTutorialClear,
            definition, prerequisites, current != null && GetIsDoneChallenge(current),
            current != null && GetIsClearChallenge(current), QuestStaffGrantMask);
    }

    internal static bool CommitQuestStaffTutorialMemory(QuestStaffTutorialMemory expected, int grantedMask, out string error)
    {
        error = null;
        if (expected == null || !expected.Matches(ReadQuestStaffTutorialMemory())
            || !expected.TryCreateGrantedMask(out int planned, out error) || planned != grantedMask)
        { error = error ?? "직원 지급 전 퀘스트 또는 지급 근거가 변경되었습니다."; return false; }
        // The caller installs the prepared StaffAccount before notifications; no callbacks here.
        QuestStaffGrantMask = grantedMask;
        return true;
    }
}
