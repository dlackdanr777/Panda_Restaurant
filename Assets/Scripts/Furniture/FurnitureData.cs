using UnityEngine;

public class FurnitureData : ShopData
{
    private string _setId;
    public string SetId => _setId;

    private FurnitureType _type;
    public FurnitureType Type => _type;

    private FoodType _foodType;
    public FoodType FoodType => _foodType;

    private int _addScore;
    public int AddScore => _addScore;

    private EquipEffectType _equipEffectType;
    public EquipEffectType EquipEffectType => _equipEffectType;

    private int _effectValue;
    public int EffectValue => _effectValue;

    private UnlockConditionData _unlockData;
    public UnlockConditionData UnlockData => _unlockData;

    public FurnitureData(Sprite sprite, Sprite thumbnailSprite, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, FurnitureType furnitureType,  FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount)
    {
        _sprite = sprite;
        _thumbnailSPrite = thumbnailSprite;
        _name = name;
        _id = id;
        _setId = setId;
        _foodType = foodType;
        _moneyType = moneyType;
        _buyScore = buyScore;
        _buyPrice = buyPrice;

        _type = furnitureType;
        _foodType = foodType;
        _addScore = addScore;

        _equipEffectType = euipEffectType;
        _effectValue = effectValue;

        _unlockData = new UnlockConditionData(unlockType, unlockId, unlockCount);
    }
}