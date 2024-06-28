using UnityEngine;


[CreateAssetMenu(fileName = "FurnitureData", menuName = "Scriptable Object/FurnitureData")]
public class FurnitureData : ScriptableObject
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

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;

    [SerializeField] private int _moneyPerMinute;
    public int MoneyPerMinute => _moneyPerMinute;


    [Space] [Header("Buy Option")]

    [SerializeField] private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] private int _buyMinPrice;
    public int BuyMinPrice => _buyMinPrice;

    [SerializeField] private int _buyMinScore;
    public int BuyMinScore => _buyMinScore;
}