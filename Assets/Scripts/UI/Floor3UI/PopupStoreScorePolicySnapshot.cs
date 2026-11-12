/// <summary>Immutable local points for complete authenticated 3/4/5-material receipts.</summary>
public sealed class PopupStoreScorePolicySnapshot
{
    public int ThreeMaterialPoints { get; }
    public int FourMaterialPoints { get; }
    public int FiveMaterialPoints { get; }

    public static PopupStoreScorePolicySnapshot Ordinary { get; }
        = new PopupStoreScorePolicySnapshot(100, 140, 180);

    private PopupStoreScorePolicySnapshot(int three, int four, int five)
    { ThreeMaterialPoints = three; FourMaterialPoints = four; FiveMaterialPoints = five; }

    public static bool TryCreate(int three, int four, int five,
        out PopupStoreScorePolicySnapshot snapshot, out string error)
    {
        snapshot = null; error = null;
        if (three <= 0 || four <= three || five <= four)
        { error = "3/4/5-material points must be positive and strictly increasing."; return false; }
        snapshot = new PopupStoreScorePolicySnapshot(three, four, five);
        return true;
    }

    public bool TryGetPoints(int materialCount, out int points)
    {
        points = materialCount == 3 ? ThreeMaterialPoints : materialCount == 4 ? FourMaterialPoints
            : materialCount == 5 ? FiveMaterialPoints : 0;
        return points > 0;
    }
}
