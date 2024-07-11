using UnityEngine;

public class BasicData : ScriptableObject
{
    [Header("DefaultData")]
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private Sprite _thumbnailSPrite;
    public Sprite ThumbnailSPrite => _thumbnailSPrite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _id;
    public string Id => _id;

    [TextArea][SerializeField] private string _description;
    public string Description => _description;

    [Space]
    [Header("ShopData")]

    [SerializeField] private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] private int _buyScore;
    public int BuyScore => _buyScore;

    [SerializeField] private int _buyPrice;
    public int BuyPrice => _buyPrice;
}
