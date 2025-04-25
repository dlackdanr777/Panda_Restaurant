using UnityEngine;

[CreateAssetMenu(fileName = "TouchAddCustomerButtonSkill", menuName = "Scriptable Object/Skill/TouchAddCustomerButtonSkill")]
public class TouchAddCustomerButtonSkill : SkillBase
{
    [Range(0.02f, 100f)] [SerializeField] private float _touchInterval = 0.5f;

    public override float FirstValue => _touchInterval;

    public override float SecondValue => 0;


    private float _timer;
    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _timer = 0;
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _timer += 0.02f * staff.SpeedMul;
        if (_touchInterval <= _timer)
        {
            _timer = 0;
            customerController.AddTabCount();
        }
    }
}
