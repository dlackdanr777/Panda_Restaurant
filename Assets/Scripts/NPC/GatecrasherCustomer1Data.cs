using UnityEngine;

[CreateAssetMenu(fileName = " GatecrasherCustomer1Data", menuName = "Scriptable Object/GatecrasherCustomer1Data")]
public class GatecrasherCustomer1Data : GatecrasherCustomerData
{
    [Space]
    [Header("Gatecrasher1 Option")]
    [SerializeField] private RuntimeAnimatorController _gatecrasherController;
    public RuntimeAnimatorController GatecrasherController => _gatecrasherController;

    public GatecrasherCustomer1Data(Sprite sprite, string id, string name, string description, float moveSpeed, int minScore, string requiredDish, string requiredItem, int activeDuration, int touchCount, float spawnChance, RuntimeAnimatorController customerController, RuntimeAnimatorController gatecrasherController) : base(sprite, id, name, description, moveSpeed, minScore, requiredDish, requiredItem, activeDuration, touchCount, spawnChance, customerController)
    {
        _gatecrasherController = gatecrasherController;
    }

}
