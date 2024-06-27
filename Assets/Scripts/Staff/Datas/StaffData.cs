using UnityEngine;

public abstract class StaffData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _name;
    public string Name => _name;

    [TextArea][SerializeField] private string _description;
    public string Description => _description;

    [TextArea][SerializeField] private string _effectDescription;
    public string EffectDescription => _effectDescription;

    [SerializeField] private SkillBase _skill;
    public SkillBase Skill => _skill;

    [SerializeField] private int _buyMinScore;
    public int BuyMinScore => _buyMinScore;

    [SerializeField] private StaffMoneyData _moneyData;
    public StaffMoneyData MoneyData => _moneyData;

    public abstract float SecondValue { get; }

    public abstract int GetUpgradeMinScore(int level);

    public abstract int GetUpgradePrice(int level);

    public abstract float GetActionValue(int level);

    public abstract int GetEquipAddScore(int level);

    public abstract float GetEquipAddTip(int level);

    public abstract bool UpgradeEnable(int level);

    public abstract void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);

    public abstract IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController);
}
