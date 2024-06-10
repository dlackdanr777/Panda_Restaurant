using UnityEngine;

public interface ISkill
{
    string SkillName { get; }
    string Description { get; }
    float Cooldown { get; }
    float Duration { get; }
    void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
    void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}

public abstract class SkillBase : ScriptableObject, ISkill
{
    [SerializeField] private Sprite _sprite;
    [SerializeField] private string _skillName;
    [SerializeField] private string _description;
    [SerializeField] private float _cooldown;
    [SerializeField] private float _duration;

    public Sprite Sprite => _sprite;
    public string SkillName => _skillName;
    public string Description => _description;
    public float Cooldown => _cooldown;
    public float Duration => _duration;

    public abstract void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
    public abstract void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}