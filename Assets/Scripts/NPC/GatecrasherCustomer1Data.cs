using UnityEngine;

[CreateAssetMenu(fileName = " GatecrasherCustomer1Data", menuName = "Scriptable Object/GatecrasherCustomer1Data")]
public class GatecrasherCustomer1Data : GatecrasherCustomerData
{
    [Space]
    [Header("Gatecrasher1 Option")]
    [SerializeField] private RuntimeAnimatorController _gatecrasherController;
    public RuntimeAnimatorController GatecrasherController => _gatecrasherController;
}
