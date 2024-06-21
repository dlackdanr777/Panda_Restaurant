using UnityEngine;

[CreateAssetMenu(fileName = "AddPromotionCustomerSkill", menuName = "Scriptable Object/Skill/AddPromotionCustomerSkill")]
public class AddPromotionCustomerSkill : SkillBase
{
    [Range(1f, 10f)] [SerializeField] private int _addCustomerValue = 1;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        GameManager.Instance.AppendPromotionCustomer(_addCustomerValue);
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        GameManager.Instance.AppendPromotionCustomer(-_addCustomerValue);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
