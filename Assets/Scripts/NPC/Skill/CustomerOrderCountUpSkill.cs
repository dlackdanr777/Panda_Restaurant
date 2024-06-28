using UnityEngine;

[CreateAssetMenu(fileName = "OrderCountUpSkill", menuName = "Scriptable Object/Customer/OrderCountUpSkill")]
public class CustomerOrderCountUpSkill : CustomerSkill
{
    [Range(1, 10)] [SerializeField] private int _addOrderCount;

    public override float FirstValue => _addOrderCount;

    public override void Activate(Customer customer)
    {
        customer.SetFoodPriceMul(1);

        if (Random.Range(0f, 100f) < SkillActivatePercent)
            customer.SetOrderCount(_addOrderCount + 1);

        else
            customer.SetOrderCount(1);
    }

    public override void Deactivate(Customer customer)
    {
        customer.SetOrderCount(1);
        customer.SetFoodPriceMul(1);
    }
}
