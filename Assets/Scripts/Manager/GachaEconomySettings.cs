using UnityEngine;

[CreateAssetMenu(menuName = "Panda Restaurant/Gacha economy settings")]
public sealed class GachaEconomySettings : ScriptableObject
{
    [Min(1)] public int ItemTicketPrice = 100;
    [Min(1)] public int StaffTicketPrice = 100;
    [Min(1)] public int TicketBundleQuantity = 1;
    [Min(1)] public int NormalItemPrice = 40;
    [Min(1)] public int RareItemPrice = 100;
    [Min(1)] public int UniqueItemPrice = 250;
    [Min(1)] public int SpecialItemPrice = 600;
    public Sprite ItemTicketSprite;
    public Sprite StaffTicketSprite;
    public int ItemPrice(Rank rank)
    {
        switch (rank)
        {
            case Rank.Normal1: case Rank.Normal2: return NormalItemPrice;
            case Rank.Rare: return RareItemPrice;
            case Rank.Unique: return UniqueItemPrice;
            case Rank.Special: return SpecialItemPrice;
            default: return 0;
        }
    }
}
