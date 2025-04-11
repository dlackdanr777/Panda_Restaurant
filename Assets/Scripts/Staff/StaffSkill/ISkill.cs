using UnityEngine;

public interface ISkill
{
    string Description { get; }
    float Cooldown { get; }
    float Duration { get; }
    void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
    void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}

public abstract class SkillBase : ScriptableObject, ISkill
{

    [TextArea][SerializeField] private string _description;
    [SerializeField] private float _cooldown;
    [SerializeField] private float _duration;
    public abstract float FirstValue { get; }
    public abstract float SecondValue { get; }
    public string Description => _description;
    public float Cooldown => _cooldown;
    public float Duration => _duration;

    public abstract void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
    public abstract void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
    public abstract void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}