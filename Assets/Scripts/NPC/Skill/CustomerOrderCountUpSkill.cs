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

        if (Random.Range(0f, 100f) < SkillActivatePercent)
            normalCustomer.AddOrderCount(_addOrderCount);
    }

    public override void Deactivate(Customer customer)
    {
        if (!(customer is NormalCustomer))
        {
            DebugLog.LogError("�Ϲ� �մ��� �ƴ� �ٸ� Ÿ���� �մ��� �Է���ϴ�.");
            return;
        }
        NormalCustomer normalCustomer = (NormalCustomer)customer;
    }
}
