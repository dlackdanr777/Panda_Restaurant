using UnityEngine;


public enum EquipEffectType
{
    AddScore,
    AddTipPerMinute,
    AddCookSpeed,
    AddMaxTip,
    AddDishWashSpeedMul,
    None,
}


public class KitchenUtensilData : BasicData, ShopData
{

    private KitchenUtensilType _type;
    public KitchenUtensilType Type => _type;

    private FoodType _foodType;
    public FoodType FoodType => _foodType;

    private string _setId;
    public string SetId => _setId;

    private int _addScore;
    public int AddScore => _addScore;
    
    private EquipEffectType _equipEffectType;
    public EquipEffectType EquipEffectType => _equipEffectType;

    private float _effectValue;
    public float EffectValue => _effectValue;

    private UnlockConditionData _unlockData;
    public UnlockConditionData UnlockData => _unlockData;

    protected SalesLocationType _salesLocationType;
    public SalesLocationType SalesLocationType => _salesLocationType;
    
    protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    protected int _buyScore;
    public int BuyScore => _buyScore;

    protected int _buyPrice;
    public int BuyPrice => _buyPrice;


    public KitchenUtensilData(Sprite sprite, Sprite thumbnailSprite, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, KitchenUtensilType kitchenType, FoodType foodType, int addScore, EquipEffectType euipEffectType, float effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount)
    {
        _salesLocationType = SalesLocationType.Shop;

        _sprite = sprite;
        _thumbnailSprite = thumbnailSprite;
        _name = name;
        _id = id;
        _setId = setId;
        _foodType = foodType;
        _moneyType = moneyType;
        _buyScore = buyScore;
        _buyPrice = buyPrice;

        _type = kitchenType;
        _foodType = foodType;
        _addScore = addScore;

        _equipEffectType = euipEffectType;
        _effectValue = effectValue;

        _unlockData = new UnlockConditionData(unlockType, unlockId, unlockCount);
    }
}