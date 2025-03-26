using UnityEngine;

public class TableFurnitureData : FurnitureData
{
    [Space]
    [Header("TableFurnitureData")]
    [Range(0.5f, 2f)] [SerializeField] private float _scale = 1;
    public float Scale => _scale;

    [SerializeField] private Sprite _chairSprite;
    public Sprite ChairSprite => _chairSprite;

    private Sprite _rightChairSprite;
    public Sprite RightChairSprite => _rightChairSprite;

    private Sprite _dirtyTableSprite;
    public Sprite DirtyTableSprite => _dirtyTableSprite;


    public TableFurnitureData(Sprite sprite, Sprite thumbnailSprite, string id, string setId, string name, MoneyType moneyType, int buyScore, int buyPrice, FurnitureType furnitureType, FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, UnlockConditionType unlockType, string unlockId, int unlockCount, float scale, Sprite leftChair, Sprite rightChair, Sprite dirtyTableSprite) : base(sprite, thumbnailSprite, id, setId, name, moneyType, buyScore, buyPrice, furnitureType, foodType, addScore, euipEffectType, effectValue, unlockType, unlockId, unlockCount)
    {
        _scale = scale;
        _chairSprite = leftChair;
        _rightChairSprite = rightChair;
        _dirtyTableSprite = dirtyTableSprite;
    }
}
