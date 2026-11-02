using System;
using UnityEngine;

/// <summary>Three alternative amounts, never three cumulative grants. A returned value owns no account state.</summary>
[Serializable]
public struct PopupStoreRewardEntry
{
    [SerializeField] private PopupStoreGradeTier _grade;
    [SerializeField] private int _basicDiamonds;
    [SerializeField] private int _bonusDiamonds;
    [SerializeField] private int _jackpotDiamonds;
    // Integer percentages intentionally cannot represent NaN, Infinity or rounding-dependent totals.
    [SerializeField] private int _basicWeight;
    [SerializeField] private int _bonusWeight;
    [SerializeField] private int _jackpotWeight;

    public PopupStoreGradeTier Grade => _grade;
    public int BasicDiamonds => _basicDiamonds;
    public int BonusDiamonds => _bonusDiamonds;
    public int JackpotDiamonds => _jackpotDiamonds;
    public int BasicWeight => _basicWeight;
    public int BonusWeight => _bonusWeight;
    public int JackpotWeight => _jackpotWeight;

    public PopupStoreRewardEntry(PopupStoreGradeTier grade, int basicDiamonds, int bonusDiamonds,
        int jackpotDiamonds, int basicWeight, int bonusWeight, int jackpotWeight)
    {
        _grade = grade;
        _basicDiamonds = basicDiamonds; _bonusDiamonds = bonusDiamonds; _jackpotDiamonds = jackpotDiamonds;
        _basicWeight = basicWeight; _bonusWeight = bonusWeight; _jackpotWeight = jackpotWeight;
    }
}

/// <summary>Pure R0 data validation and theoretical expectation. No random selection or payment API.</summary>
public static class PopupStoreRewardCalculator
{
    public static bool TryValidate(PopupStoreRewardEntry entry, out string error)
    {
        error = null;
        if (entry.Grade < PopupStoreGradeTier.C || entry.Grade > PopupStoreGradeTier.SSS)
            error = "A reward row must use one of C, B, A, S, SS or SSS.";
        else if (entry.BasicDiamonds < 0 || entry.BonusDiamonds < 0 || entry.JackpotDiamonds < 0)
            error = "Reward amounts must be non-negative.";
        else if (entry.BasicDiamonds > entry.BonusDiamonds || entry.BonusDiamonds > entry.JackpotDiamonds)
            error = "Amounts must satisfy Basic <= Bonus <= Jackpot within each grade.";
        else if (entry.BasicWeight < 0 || entry.BonusWeight < 0 || entry.JackpotWeight < 0)
            error = "Probability weights must be non-negative integers.";
        else if ((long)entry.BasicWeight + entry.BonusWeight + entry.JackpotWeight != 100L)
            error = "Probability weights must sum to exactly 100.";
        return error == null;
    }

    public static bool TryCalculateExpectedDiamonds(PopupStoreRewardEntry entry, out decimal expected, out string error)
    {
        expected = 0m;
        if (!TryValidate(entry, out error)) return false;
        // Promote before multiplication. Even int.MaxValue amounts cannot overflow an integer intermediate.
        expected = ((decimal)entry.BasicDiamonds * entry.BasicWeight
            + (decimal)entry.BonusDiamonds * entry.BonusWeight
            + (decimal)entry.JackpotDiamonds * entry.JackpotWeight) / 100m;
        return true;
    }
}

/// <summary>Inspector-authored test policy, separate from grade results, reward sprites and account inventory.</summary>
[CreateAssetMenu(fileName = "PopupStoreRewardTable", menuName = "Panda Restaurant/Popup Store/Reward Table")]
public sealed class PopupStoreRewardTable : ScriptableObject
{
    [Tooltip("R0 test policy only. Each row contains three alternatives; this asset never selects or pays one.")]
    [SerializeField] private PopupStoreRewardEntry[] _entries = Array.Empty<PopupStoreRewardEntry>();

    public bool TryValidate(out string error)
    {
        error = null;
        if (_entries == null || _entries.Length != 6)
        { error = "The reward table must contain exactly six grades: C through SSS."; return false; }
        int seen = 0;
        foreach (var entry in _entries)
        {
            if (!PopupStoreRewardCalculator.TryValidate(entry, out error)) return false;
            int bit = 1 << ((int)entry.Grade - 1);
            if ((seen & bit) != 0)
            { error = "Duplicate reward grade: " + entry.Grade + "."; return false; }
            seen |= bit;
        }
        // Six unique rows drawn from the six supported grades necessarily cover the complete table.
        return true;
    }

    /// <summary>Pass an existing FinalResult.Grade; this query never calculates or owns a final result.
    /// None returns false with no error on a valid table. Invalid data always returns false with an error.</summary>
    public bool TryGetReward(PopupStoreGradeTier grade, out PopupStoreRewardEntry entry, out string error)
    {
        entry = default;
        if (!TryValidate(out error)) return false;
        if (grade == PopupStoreGradeTier.None) return false;
        if (grade < PopupStoreGradeTier.C || grade > PopupStoreGradeTier.SSS)
        { error = "Unknown reward grade."; return false; }
        foreach (var candidate in _entries)
            if (candidate.Grade == grade) { entry = candidate; return true; }
        error = "The requested grade is missing.";
        return false;
    }

    /// <summary>A detached, ordered value snapshot. Editing it cannot mutate the serialized table.</summary>
    public bool TryGetEntries(out PopupStoreRewardEntry[] entries, out string error)
    {
        entries = Array.Empty<PopupStoreRewardEntry>();
        if (!TryValidate(out error)) return false;
        entries = (PopupStoreRewardEntry[])_entries.Clone();
        Array.Sort(entries, (left, right) => left.Grade.CompareTo(right.Grade));
        return true;
    }
}
