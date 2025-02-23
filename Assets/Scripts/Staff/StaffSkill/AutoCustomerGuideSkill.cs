using UnityEngine;

[CreateAssetMenu(fileName = "AutoCustomerGuideSkill", menuName = "Scriptable Object/Skill/AutoCustomerGuideSkill")]
public class AutoCustomerGuideSkill : SkillBase
{
    public override float FirstValue => 0;

    public override float SecondValue => 0;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        tableManager.OnCustomerGuideEvent();
    }
}
