using System.Collections.Generic;
using UnityEngine;

public class FurnitureData : BasicData, ShopData
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

    private SalesLocationType _salesLocationType;
    public SalesLocationType SalesLocationType => _salesLocationType;
    
    private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    private int _buyScore;
    public int BuyScore => _buyScore;

    private int _buyPrice;
    public int BuyPrice => _buyPrice;

    protected List<Sprite> _animationSpriteList;
    public List<Sprite> AnimationSpriteList => _animationSpriteList;

    public FurnitureData(Sprite sprite, Sprite thumbnailSprite, List<Sprite> animationSpriteList, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, FurnitureType furnitureType,  FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount)
    {
        _salesLocationType = SalesLocationType.Shop;
        
        _sprite = sprite;
        _thumbnailSprite = thumbnailSprite;
        _animationSpriteList = animationSpriteList;
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