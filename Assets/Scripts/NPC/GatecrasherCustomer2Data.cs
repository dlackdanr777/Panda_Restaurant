using UnityEngine;

[CreateAssetMenu(fileName = " GatecrasherCustomer2Data", menuName = "Scriptable Object/GatecrasherCustomer2Data")]
public class GatecrasherCustomer2Data : GatecrasherCustomerData
{
    public GatecrasherCustomer2Data(Sprite sprite, string id, string name, string description, float moveSpeed, int minScore, string requiredDish, string requiredItem, int activeDuration, int touchCount, float spawnChance, RuntimeAnimatorController customerController) : base(sprite, id, name, description, moveSpeed, minScore, requiredDish, requiredItem, activeDuration, touchCount, spawnChance, customerController)
    {
    }
}
