using UnityEngine;

[CreateAssetMenu(fileName = "FoodPriceUpSkill", menuName = "Scriptable Object/Skill/FoodPriceUpSkill")]
public class FoodPriceUpSkill : SkillBase
{
    [Range(0, 1000)] [SerializeField] private float _foodPriceUpPercent;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        GameManager.Instance.AddFoodPriceMul(_foodPriceUpPercent);
        Debug.Log(staff.gameObject.name + "�ݾ� ����");
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        GameManager.Instance.AddFoodPriceMul(-_foodPriceUpPercent);
        Debug.Log(staff.gameObject.name + "�ݾ� ����");
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
