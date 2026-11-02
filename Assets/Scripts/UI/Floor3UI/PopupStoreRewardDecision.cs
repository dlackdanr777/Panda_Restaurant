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
    public bool IsDecided => true;
    public bool IsLocalPreview => true;

    internal PopupStoreRewardDecision(string sessionId, PopupStoreGradeTier grade,
        PopupStoreRewardKind kind, int diamonds)
    {
        SessionId = sessionId; Grade = grade; Kind = kind; Diamonds = diamonds;
        DecisionId = Guid.NewGuid().ToString("N");
    }
}

/// <summary>One integer in [0,100). Injected sources make every boundary reproducible in tests.</summary>
public interface IPopupStoreRewardRandomSource
{
    int NextPercent();
}

/// <summary>Private PRNG state per popup. Does not read or advance UnityEngine.Random.
/// Local testing only: this is not a trusted authority for future account payments.</summary>
public sealed class PopupStoreRewardRandomSource : IPopupStoreRewardRandomSource
{
    private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
    public int NextPercent() => _random.Next(0, 100);
}

/// <summary>Pure mapping of a validated row and one supplied integer. Never draws, stores or pays.</summary>
public static class PopupStoreRewardSelector
{
    public static bool TrySelect(PopupStoreRewardEntry entry, int roll,
        out PopupStoreRewardKind kind, out int diamonds, out string error)
    {
        kind = default; diamonds = 0;
        if (!PopupStoreRewardCalculator.TryValidate(entry, out error)) return false;
        if (roll < 0 || roll >= 100) { error = "Reward roll must be in [0,100)."; return false; }
        if (roll < entry.BasicWeight)
        { kind = PopupStoreRewardKind.Basic; diamonds = entry.BasicDiamonds; }
        else if (roll < entry.BasicWeight + entry.BonusWeight)
        { kind = PopupStoreRewardKind.Bonus; diamonds = entry.BonusDiamonds; }
        else
        { kind = PopupStoreRewardKind.Jackpot; diamonds = entry.JackpotDiamonds; }
        return true;
    }
}
