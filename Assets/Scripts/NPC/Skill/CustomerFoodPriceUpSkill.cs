using UnityEngine;

[CreateAssetMenu(fileName = "FoodPriceUpSkill", menuName = "Scriptable Object/Customer/FoodPriceUpSkill")]
public class CustomerFoodPriceUpSkill : CustomerSkill
{
    [Range(1f, 100f)] [SerializeField] private float _foodPriceMul;

    public override float FirstValue => _foodPriceMul;

    public override void Activate(Customer customer)
    {
        if (!(customer is NormalCustomer))
        {
            DebugLog.LogError("일반 손님이 아닌 다른 타입의 손님이 입력됬습니다.");
            return;
        }
        NormalCustomer normalCustomer = (NormalCustomer)customer;

        if (Random.Range(0f, 100f) < SkillActivatePercent)
            normalCustomer.AddFoodPricePercent(_foodPriceMul);
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
