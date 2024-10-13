using UnityEngine;

public class BasicData : ScriptableObject
{
    [Header("DefaultData")]
    [SerializeField] protected Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] protected Sprite _thumbnailSPrite;
    public Sprite ThumbnailSprite => _thumbnailSPrite;

    [SerializeField] protected string _name;
    public string Name => _name;

    [SerializeField] protected string _id;
    public string Id => _id;

    [TextArea][SerializeField] protected string _description;
    public string Description => _description;

    [Space]
    [Header("ShopData")]

    [SerializeField] protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] protected int _buyScore;
    public int BuyScore => _buyScore;

    [SerializeField] protected int _buyPrice;
    public int BuyPrice => _buyPrice;
}
