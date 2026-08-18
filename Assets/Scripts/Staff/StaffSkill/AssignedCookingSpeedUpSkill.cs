using UnityEngine;

[CreateAssetMenu(fileName = "AssignedCookingSpeedUpSkill", menuName = "Scriptable Object/Skill/AssignedCookingSpeedUpSkill")]
public class AssignedCookingSpeedUpSkill : SkillBase
{
    [Range(0f, 1000f)] [SerializeField] private float _assignedCookingSpeedUpPercent = 150f;

    public override float FirstValue => _assignedCookingSpeedUpPercent;

    public override float SecondValue => 0;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.RuntimeSkillContext.SetAssignedCookingBonusPercent(
            staff.CurrentSkillSourceToken,
            _assignedCookingSpeedUpPercent);
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.RuntimeSkillContext.SetAssignedCookingBonusPercent(
            staff.CurrentSkillSourceToken,
            0f);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
