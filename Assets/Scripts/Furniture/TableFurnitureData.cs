using UnityEngine;

public class TableFurnitureData : FurnitureData
{
    private Sprite _chairSprite;
    public Sprite ChairSprite => _chairSprite;

    private Sprite _rightChairSprite;
    public Sprite RightChairSprite => _rightChairSprite;



    public TableFurnitureData(Sprite sprite, Sprite thumbnailSprite, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, FurnitureType furnitureType, FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount, Sprite leftChair, Sprite rightChair) : base(sprite, thumbnailSprite, id, setId, name, moneyType, buyScore, buyPrice, furnitureType, foodType, addScore, euipEffectType, effectValue, unlockType, unlockId, unlockCount)
    {
        _chairSprite = leftChair;
        _rightChairSprite = rightChair;
    }
}
