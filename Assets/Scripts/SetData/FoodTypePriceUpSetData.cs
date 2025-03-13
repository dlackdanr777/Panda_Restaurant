using UnityEngine;

[CreateAssetMenu(fileName = "FoodTypePriceUpSetData", menuName = "Scriptable Object/SetData/FoodTypePriceUpSetData")]
public class FoodTypePriceUpSetData : SetData
{

    [Range(0, 10000)] [SerializeField] private int _foodPriceUpMul;
    public override float Value => _foodPriceUpMul;
}
