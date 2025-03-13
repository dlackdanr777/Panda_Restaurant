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


    public TableFurnitureData(Sprite sprite, Sprite thumbnailSprite, string id, string name, MoneyType moneyType, int buyScore, int buyPrice, FurnitureType furnitureType, FoodType foodType, int addScore, EquipEffectType euipEffectType, int effectValue, float scale, Sprite leftChair, Sprite rightChair) : base(sprite, thumbnailSprite, id, name, moneyType, buyScore, buyPrice, furnitureType, foodType, addScore, euipEffectType, effectValue)
    {
        _scale = scale;
        _chairSprite = sprite;
        _rightChairSprite = thumbnailSprite;
    }
}
