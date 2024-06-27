using UnityEngine;

[CreateAssetMenu(fileName = "SpeedUpSkill", menuName = "Scriptable Object/Skill/SpeedUpSkill")]
public class SpeedUpSkill : SkillBase
{
    [Range(0f, 1000f)] [SerializeField] private float _speedUpMul;

    public override float FirstValue => _speedUpMul;

    public override float SecondValue => 0;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.AddSpeedMul(_speedUpMul);
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.AddSpeedMul(-_speedUpMul);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
