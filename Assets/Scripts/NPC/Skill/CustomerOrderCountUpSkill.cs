using UnityEngine;

[CreateAssetMenu(fileName = "OrderCountUpSkill", menuName = "Scriptable Object/Customer/OrderCountUpSkill")]
public class CustomerOrderCountUpSkill : CustomerSkill
{
    [Range(1, 10)] [SerializeField] private int _addOrderCount;

    public override float FirstValue => _addOrderCount;

    public override void Activate(Customer customer)
    {
        if (!(customer is NormalCustomer))
        {
            DebugLog.LogError("�Ϲ� �մ��� �ƴ� �ٸ� Ÿ���� �մ��� �Է���ϴ�.");
            return;
        }

        NormalCustomer normalCustomer = (NormalCustomer)customer;
        normalCustomer.SetFoodPriceMul(1);

        if (Random.Range(0f, 100f) < SkillActivatePercent)
            normalCustomer.SetOrderCount(_addOrderCount + 1);

        else
            normalCustomer.SetOrderCount(1);
    }

    public override void Deactivate(Customer customer)
    {
        if (!(customer is NormalCustomer))
        {
            DebugLog.LogError("�Ϲ� �մ��� �ƴ� �ٸ� Ÿ���� �մ��� �Է���ϴ�.");
            return;
        }
        NormalCustomer normalCustomer = (NormalCustomer)customer;
        normalCustomer.SetOrderCount(1);
        normalCustomer.SetFoodPriceMul(1);
    }
}
