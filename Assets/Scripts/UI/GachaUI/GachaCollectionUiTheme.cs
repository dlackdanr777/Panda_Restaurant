using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Panda Restaurant/Gacha Collection UI Theme")]
public sealed class GachaCollectionUiTheme : ScriptableObject
{
    public TMP_FontAsset Font;
    public Sprite PandaToken;
    public Sprite Ticket;
    public Sprite SlotFrame;
    public Sprite ButtonPlate;
    public Sprite WoodFrame;
    public Sprite ExchangeClose;
    public Sprite ExchangeBuy;
    public Sprite ExchangeTitle;
    public Sprite LeftChain;
    public Sprite RightChain;
    public Sprite ExchangeRefresh;
    public Sprite NormalProductFrame;
    public Sprite RareProductFrame;
    public Sprite UniqueProductFrame;
    public Sprite SpecialProductFrame;
    public Sprite DetailFrame;
    public Sprite Star;
    public float EntranceSeconds = .78f;
    public Color Ink = new Color32(83, 48, 26, 255);
    public Color Cream = new Color32(255, 248, 226, 255);
    public Color Wood = new Color32(174, 112, 69, 255);
    public Color Accent = new Color32(230, 169, 64, 255);
    public static GachaCollectionUiTheme Load() => Resources.Load<GachaCollectionUiTheme>("GachaCollectionUiTheme");
    public Sprite ProductFrame(Rank rank) => rank == Rank.Special ? SpecialProductFrame :
        rank == Rank.Unique ? UniqueProductFrame : rank == Rank.Rare ? RareProductFrame : NormalProductFrame;
}
