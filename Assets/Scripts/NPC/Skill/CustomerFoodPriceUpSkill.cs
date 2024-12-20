using System.Collections;
using System.Collections.Generic;
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
            DebugLog.LogError("�Ϲ� �մ��� �ƴ� �ٸ� Ÿ���� �մ��� �Է���ϴ�.");
            return;
        }
        NormalCustomer normalCustomer = (NormalCustomer)customer;
        normalCustomer.AddFoodPricePercent(_foodPriceMul);
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
