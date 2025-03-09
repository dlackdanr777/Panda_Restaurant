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
            DebugLog.LogError("일반 손님이 아닌 다른 타입의 손님이 입력됬습니다.");
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
            DebugLog.LogError("일반 손님이 아닌 다른 타입의 손님이 입력됬습니다.");
            return;
        }
        NormalCustomer normalCustomer = (NormalCustomer)customer;
    }
}
