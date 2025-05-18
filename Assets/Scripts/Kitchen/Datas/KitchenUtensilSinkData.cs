using UnityEngine;


public class KitchenUtensilSinkData : KitchenUtensilData
{
    private int _maxSinkBowlCount;
    public int MaxSinkBowlCount => _maxSinkBowlCount;


    public KitchenUtensilSinkData(Sprite sprite, Sprite thumbnailSprite, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, KitchenUtensilType kitchenType, FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount, int maxSinkBowlCount) : base(sprite, thumbnailSprite, id, setId, name, moneyType, buyScore, buyPrice, kitchenType, foodType, addScore, euipEffectType, effectValue, unlockType, unlockId, unlockCount)
    {
        _maxSinkBowlCount = maxSinkBowlCount;
    }
}