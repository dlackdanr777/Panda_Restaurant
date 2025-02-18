using UnityEngine;

public abstract class StaffData : ShopData
{
    [Space]
    [Header("StaffData")]

    [SerializeField] private SkillBase _skill;
    public SkillBase Skill => _skill;

    public abstract float SecondValue { get; }
    public abstract int MaxLevel { get; }

    public abstract int GetUpgradeMinScore(int level);

    public abstract UpgradeMoneyData GetUpgradeMoneyData(int level);

    public abstract float GetActionValue(int level);

    public abstract int GetAddScore(int level);

    public abstract float GetAddTipMul(int level);

    public abstract bool UpgradeEnable(int level);

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public virtual void Destroy()
    {

    }
}
