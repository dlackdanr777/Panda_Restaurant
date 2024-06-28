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
        customer.SetOrderCount(1);

        if(Random.Range(0f, 100f) < SkillActivatePercent)
            customer.SetFoodPriceMul(_foodPriceMul);

        else
            customer.SetFoodPriceMul(1);
    }

    public override void Deactivate(Customer customer)
    {
        customer.SetOrderCount(1);
        customer.SetFoodPriceMul(1);
    }
}
