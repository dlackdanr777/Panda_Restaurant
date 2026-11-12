using System;

public enum PopupStoreRewardKind { Basic, Bonus, Jackpot }

/// <summary>One immutable local preview, not an entitlement, payment receipt or persistent reward record.</summary>
public sealed class PopupStoreRewardDecision
{
    public string SessionId { get; }
    public PopupStoreGradeTier Grade { get; }
    public PopupStoreRewardKind Kind { get; }
    public int Diamonds { get; }
    public string DecisionId { get; }
    public string PolicyId { get; }
    public bool IsDecided => true;
    public bool IsLocalPreview => true;

    internal PopupStoreRewardDecision(string sessionId, PopupStoreGradeTier grade,
        PopupStoreRewardKind kind, int diamonds)
        : this(sessionId, grade, kind, diamonds, PopupStoreRewardTable.LegacyPolicyId) { }

    internal PopupStoreRewardDecision(string sessionId, PopupStoreGradeTier grade,
        PopupStoreRewardKind kind, int diamonds, string policyId)
    {
        SessionId = sessionId; Grade = grade; Kind = kind; Diamonds = diamonds;
        PolicyId = policyId;
        DecisionId = Guid.NewGuid().ToString("N");
    }
}

/// <summary>One integer in [0, exclusiveUpperBound). The validated policy supplies its explicit unit.</summary>
public interface IPopupStoreRewardRandomSource
{
    int NextInt(int exclusiveUpperBound);
}

/// <summary>Private PRNG state per popup. Does not read or advance UnityEngine.Random.
/// Local testing only: this is not a trusted authority for future account payments.</summary>
public sealed class PopupStoreRewardRandomSource : IPopupStoreRewardRandomSource
{
    private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
    public int NextInt(int exclusiveUpperBound)
    {
        if (exclusiveUpperBound != 100 && exclusiveUpperBound != 1000)
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound), "The reward policy must use 100 or 1000 units.");
        return _random.Next(0, exclusiveUpperBound);
    }
}

/// <summary>Pure mapping of a validated row and one supplied integer. Never draws, stores or pays.</summary>
public static class PopupStoreRewardSelector
{
    public static bool TrySelect(PopupStoreRewardEntry entry, int roll,
        out PopupStoreRewardKind kind, out int diamonds, out string error)
        => TrySelect(entry, roll, 100, out kind, out diamonds, out error); // Explicit historical R0 API.

    public static bool TrySelect(PopupStoreRewardEntry entry, int roll, int denominator,
        out PopupStoreRewardKind kind, out int diamonds, out string error)
    {
        kind = default; diamonds = 0;
        if (!PopupStoreRewardCalculator.TryValidate(entry, denominator, out error)) return false;
        if (roll < 0 || roll >= denominator) { error = "Reward roll must be in [0," + denominator + ")."; return false; }
        if (roll < entry.BasicWeight)
        { kind = PopupStoreRewardKind.Basic; diamonds = entry.BasicDiamonds; }
        else if (roll < entry.BasicWeight + entry.BonusWeight)
        { kind = PopupStoreRewardKind.Bonus; diamonds = entry.BonusDiamonds; }
        else
        { kind = PopupStoreRewardKind.Jackpot; diamonds = entry.JackpotDiamonds; }
        return true;
    }
}
