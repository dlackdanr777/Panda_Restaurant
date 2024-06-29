using UnityEngine;

public abstract class FurnitureData : ScriptableObject
{
    [SerializeField] private FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private string _name;
    public string Name => _name;

    [TextArea][SerializeField] private string _effectDescription;
    public string EffectDescription => _effectDescription;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;


    public abstract void AddSlot();
    public abstract void RemoveSlot();



    [Space] [Header("Buy Option")]

    [SerializeField] private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] private int _buyMinPrice;
    public int BuyMinPrice => _buyMinPrice;

    [SerializeField] private int _buyMinScore;
    public int BuyMinScore => _buyMinScore;
}