using UnityEngine;

public abstract class StaffData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _description;
    public string Description => _description;

    [Range(0.5f, 2.0f)] [SerializeField] private float _speed;
    public float Speed => _speed;

    [SerializeField] private SkillBase _skill;
    public SkillBase Skill => _skill;

    public abstract float SecondValue { get; }

    public abstract float GetActionValue(int level);

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void UseSkill();

    public abstract IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}
