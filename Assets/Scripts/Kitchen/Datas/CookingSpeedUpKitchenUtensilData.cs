using UnityEngine;

[CreateAssetMenu(fileName = "CookingSpeedUpKitchenUtensilData", menuName = "Scriptable Object/KitchenUtensilData/CookingSpeedUpKichenUtensilData")]
public class CookingSpeedUpKitchenUtensilData : KitchenUtensilData
{
    [Space]
    [Header("CookingSpeedUpData")]
    [SerializeField] private int _cookingSpeedMul;
    public override int EffectValue => _cookingSpeedMul;

    public override void AddSlot()
    {
        GameManager.Instance.AddCookingSpeedMul(_cookingSpeedMul);
    }

    public override void RemoveSlot()
    {
        GameManager.Instance.AddCookingSpeedMul(-_cookingSpeedMul);
    }
}
