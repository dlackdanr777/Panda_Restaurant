using UnityEngine;

public abstract class StaffData : BasicData
{
    [Space]
    [Header("StaffData")]

    [SerializeField] private SkillBase _skill;
    public SkillBase Skill => _skill;

    public abstract float SecondValue { get; }
    public abstract int MaxLavel { get; }

    public abstract int GetUpgradeMinScore(int level);

    public abstract int GetUpgradePrice(int level);

    public abstract float GetActionValue(int level);

    public abstract int GetAddScore(int level);

    public abstract float GetAddTipMul(int level);

    public abstract bool UpgradeEnable(int level);

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}
