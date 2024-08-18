using UnityEngine;

[CreateAssetMenu(fileName = "CookingSpeedUpSetData", menuName = "Scriptable Object/SetData/CookingSpeedUpSetData")]
public class CookingSpeedUpSetData : SetData
{

    [Range(0f, 100f)] [SerializeField] private float _cookingSpeedUpMul;
    public float CookingSpeedUpMul => _cookingSpeedUpMul;

    public override void Activate()
    {
        GameManager.Instance.AddCookingSpeedMul(_cookingSpeedUpMul);
    }

    public override void Deactivate()
    {
        GameManager.Instance.AddCookingSpeedMul(-_cookingSpeedUpMul);
    }
}
