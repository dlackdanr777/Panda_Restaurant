using UnityEngine;

public abstract class StaffData : ShopData
{
    [Space]
    [Header("StaffData")]

    [SerializeField] protected SkillBase _skill;
    public SkillBase Skill => _skill;

    [Range(6, 30)] [SerializeField] protected float _speed;

    public abstract float SecondValue { get; }
    public abstract int MaxLevel { get; }


    public abstract int GetUpgradeMinScore(int level);

    public abstract UpgradeMoneyData GetUpgradeMoneyData(int level);

    public abstract float GetSpeed(int level);

    public abstract float GetActionValue(int level);

    public abstract bool UpgradeEnable(int level);

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public virtual void Destroy()
    {

    }
}
