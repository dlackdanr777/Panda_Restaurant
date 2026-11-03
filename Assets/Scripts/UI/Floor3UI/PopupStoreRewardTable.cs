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
    // Integer units are interpreted by the table's explicit schema, never inferred from their sum.
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

/// <summary>Zero is the explicit legacy serialization default for R0 assets that predate schema fields.</summary>
public enum PopupStoreRewardSchema { LegacyR0Percent = 0, R1Permille = 1 }

/// <summary>Pure data validation and theoretical expectation. No random selection or payment API.</summary>
public static class PopupStoreRewardCalculator
{
    public static bool TryValidate(PopupStoreRewardEntry entry, out string error)
        => TryValidate(entry, 100, out error); // Explicit historical R0 API.

    public static bool TryValidate(PopupStoreRewardEntry entry, int denominator, out string error)
    {
        error = null;
        if (denominator != 100 && denominator != 1000)
            error = "Reward units must explicitly use the supported denominator 100 or 1000.";
        else if (entry.Grade < PopupStoreGradeTier.C || entry.Grade > PopupStoreGradeTier.SSS)
            error = "A reward row must use one of C, B, A, S, SS or SSS.";
        else if (entry.BasicDiamonds < 0 || entry.BonusDiamonds < 0 || entry.JackpotDiamonds < 0)
            error = "Reward amounts must be non-negative.";
        else if (entry.BasicDiamonds > entry.BonusDiamonds || entry.BonusDiamonds > entry.JackpotDiamonds)
            error = "Amounts must satisfy Basic <= Bonus <= Jackpot within each grade.";
        else if (entry.BasicWeight < 0 || entry.BonusWeight < 0 || entry.JackpotWeight < 0)
            error = "Probability weights must be non-negative integers.";
        else if ((long)entry.BasicWeight + entry.BonusWeight + entry.JackpotWeight != denominator)
            error = "Probability weights must sum to exactly " + denominator + ".";
        return error == null;
    }

    public static bool TryCalculateExpectedDiamonds(PopupStoreRewardEntry entry, out decimal expected, out string error)
        => TryCalculateExpectedDiamonds(entry, 100, out expected, out error); // Explicit historical R0 API.

    public static bool TryCalculateExpectedDiamonds(PopupStoreRewardEntry entry, int denominator,
        out decimal expected, out string error)
    {
        expected = 0m;
        if (!TryValidate(entry, denominator, out error)) return false;
        // Promote before multiplication. Even int.MaxValue amounts cannot overflow an integer intermediate.
        expected = ((decimal)entry.BasicDiamonds * entry.BasicWeight
            + (decimal)entry.BonusDiamonds * entry.BonusWeight
            + (decimal)entry.JackpotDiamonds * entry.JackpotWeight) / denominator;
        return true;
    }
}

/// <summary>Inspector-authored test policy, separate from grade results, reward sprites and account inventory.</summary>
[CreateAssetMenu(fileName = "PopupStoreRewardTable", menuName = "Panda Restaurant/Popup Store/Reward Table")]
public sealed class PopupStoreRewardTable : ScriptableObject
{
    public const string LegacyPolicyId = "PopupStoreDiamond-R0";
    public const string R1PolicyId = "PopupStoreDiamond-R1-20261103";

    [Tooltip("LegacyR0Percent = 100 units; R1Permille = 1000 units. Missing historical fields mean R0, not auto-detection.")]
    [SerializeField] private PopupStoreRewardSchema _schema = PopupStoreRewardSchema.LegacyR0Percent;
    [Tooltip("R1 must explicitly identify the approved policy. Empty is permitted only for historical R0 assets.")]
    [SerializeField] private string _policyId = string.Empty;
    [Tooltip("Three alternatives per grade. This asset never selects or pays a reward.")]
    [SerializeField] private PopupStoreRewardEntry[] _entries = Array.Empty<PopupStoreRewardEntry>();

    public PopupStoreRewardSchema Schema => _schema;
    public string PolicyId => _schema == PopupStoreRewardSchema.LegacyR0Percent && string.IsNullOrEmpty(_policyId)
        ? LegacyPolicyId : _policyId;
    public int Denominator => _schema == PopupStoreRewardSchema.LegacyR0Percent ? 100
        : _schema == PopupStoreRewardSchema.R1Permille ? 1000 : 0;

    public bool TryValidate(out string error)
    {
        error = null;
        if (Denominator == 0)
        { error = "Unknown reward schema."; return false; }
        string expectedPolicy = _schema == PopupStoreRewardSchema.LegacyR0Percent ? LegacyPolicyId : R1PolicyId;
        if (!string.Equals(PolicyId, expectedPolicy, StringComparison.Ordinal))
        { error = "The reward policy identifier does not match its explicit schema."; return false; }
        if (_entries == null || _entries.Length != 6)
        { error = "The reward table must contain exactly six grades: C through SSS."; return false; }
        int seen = 0;
        foreach (var entry in _entries)
        {
            if (!PopupStoreRewardCalculator.TryValidate(entry, Denominator, out error)) return false;
            int bit = 1 << ((int)entry.Grade - 1);
            if ((seen & bit) != 0)
            { error = "Duplicate reward grade: " + entry.Grade + "."; return false; }
            seen |= bit;
            if (_schema == PopupStoreRewardSchema.R1Permille && !MatchesApprovedR1(entry))
            { error = "The R1 row differs from the approved 2026-11-03 policy: " + entry.Grade + "."; return false; }
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

    /// <summary>One detached policy for one approved business. Later asset edits cannot change it.</summary>
    public bool TryCreateSnapshot(out PopupStoreRewardPolicySnapshot snapshot, out string error)
    {
        snapshot = null;
        if (!TryGetEntries(out var entries, out error)) return false;
        snapshot = new PopupStoreRewardPolicySnapshot(PolicyId, Schema, Denominator, entries);
        return true;
    }

    private static bool MatchesApprovedR1(PopupStoreRewardEntry entry)
    {
        if (entry.BasicWeight != 900 || entry.BonusWeight != 97 || entry.JackpotWeight != 3) return false;
        // Validation only: never rewrite Inspector values or silently supply a substitute row.
        switch (entry.Grade)
        {
            case PopupStoreGradeTier.C: return Amounts(entry, 1, 3, 5);
            case PopupStoreGradeTier.B: return Amounts(entry, 3, 8, 15);
            case PopupStoreGradeTier.A: return Amounts(entry, 5, 15, 30);
            case PopupStoreGradeTier.S: return Amounts(entry, 10, 20, 100);
            case PopupStoreGradeTier.SS: return Amounts(entry, 20, 30, 200);
            case PopupStoreGradeTier.SSS: return Amounts(entry, 30, 50, 300);
            default: return false;
        }
    }
    private static bool Amounts(PopupStoreRewardEntry entry, int basic, int bonus, int jackpot)
        => entry.BasicDiamonds == basic && entry.BonusDiamonds == bonus && entry.JackpotDiamonds == jackpot;
}

/// <summary>Read-only, validated policy values. No Unity asset reference, mutable array exposure or account authority.</summary>
public sealed class PopupStoreRewardPolicySnapshot
{
    private readonly PopupStoreRewardEntry[] _entries;
    public string PolicyId { get; }
    public PopupStoreRewardSchema Schema { get; }
    public int Denominator { get; }

    internal PopupStoreRewardPolicySnapshot(string policyId, PopupStoreRewardSchema schema, int denominator,
        PopupStoreRewardEntry[] entries)
    {
        PolicyId = policyId; Schema = schema; Denominator = denominator;
        _entries = (PopupStoreRewardEntry[])entries.Clone();
    }

    public bool TryGetReward(PopupStoreGradeTier grade, out PopupStoreRewardEntry entry, out string error)
    {
        entry = default; error = null;
        if (grade == PopupStoreGradeTier.None) return false;
        if (grade < PopupStoreGradeTier.C || grade > PopupStoreGradeTier.SSS)
        { error = "Unknown reward grade."; return false; }
        foreach (var candidate in _entries)
            if (candidate.Grade == grade) { entry = candidate; return true; }
        error = "The requested grade is missing from the policy snapshot.";
        return false;
    }

    public bool TryGetEntries(out PopupStoreRewardEntry[] entries, out string error)
    {
        entries = (PopupStoreRewardEntry[])_entries.Clone(); error = null; return true;
    }
}
