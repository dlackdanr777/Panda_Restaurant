using UnityEngine;

[CreateAssetMenu(fileName = "SpeedUpSkill", menuName = "Scriptable Object/Skill/SpeedUpSkill")]
public class SpeedUpSkill : SkillBase
{
    [Range(0f, 200f)] [SerializeField] private float _speedUpMul;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.AddSpeedMul(_speedUpMul);
        Debug.Log(staff.gameObject.name + " �ӵ� ����");
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.AddSpeedMul(-_speedUpMul);
        Debug.Log(staff.gameObject.name + " �ӵ� ����");
    }
}
