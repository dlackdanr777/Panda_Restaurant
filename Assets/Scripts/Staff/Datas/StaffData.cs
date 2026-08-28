using UnityEngine;

public abstract class StaffData : BasicData,ShopData
{
    public const int OfficialMaxLevel = 5;

    [Space]
    [Header("StaffData")]

    [SerializeField] protected SkillBase _skill;
    public SkillBase Skill => _skill;

    [Space]
    [Header("Animation Option")]
    [SerializeField] private Sprite[] _idleSprites;
    public Sprite[] IdleSprites => _idleSprites;

    [SerializeField] protected RuntimeAnimatorController _animatorController;
    public RuntimeAnimatorController AnimatorController => _animatorController;

    [SerializeField] protected SalesLocationType _salesLocationType;
    public SalesLocationType SalesLocationType => _salesLocationType;
    
    [SerializeField] protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] protected int _buyScore;
    public int BuyScore => _buyScore;

    [SerializeField] protected int _buyPrice;
    public int BuyPrice => _buyPrice;

    [Range(1, 30)] [SerializeField] protected float _speed;

    public abstract float SecondValue { get; }
    public abstract int MaxLevel { get; }


    public abstract int GetUpgradeMinScore(int level);

    public abstract UpgradeMoneyData GetUpgradeMoneyData(int level);

    public abstract float GetSpeed(int level);

    public abstract float GetActionValue(int level);

    public abstract bool UpgradeEnable(int level);

    public int GetRuntimeLevel(int savedLevel)
    {
        int runtimeMaxLevel = Mathf.Clamp(MaxLevel, 1, OfficialMaxLevel);
        return Mathf.Clamp(savedLevel, 1, runtimeMaxLevel);
    }

    public bool IsMaxLevel(int level)
    {
        return level >= Mathf.Min(MaxLevel, OfficialMaxLevel);
    }

    public bool CanUpgradeFromSavedLevel(int savedLevel)
    {
        return savedLevel >= 1
               && !IsMaxLevel(savedLevel)
               && UpgradeEnable(savedLevel);
    }

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public virtual void Destroy()
    {

    }
}
