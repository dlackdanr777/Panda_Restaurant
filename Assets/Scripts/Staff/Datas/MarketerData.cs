using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MarketerData", menuName = "Scriptable Object/Staff/Marketer")]
public class MarketerData : StaffData
{
    [Range(0f, 100f)] [SerializeField] private float _customerCallPercentage;
    [SerializeField] private MarketerLevelData[] _marketerLevelData;
    public override float SecondValue => _customerCallPercentage;

    public override float GetActionValue(int level)
    {
        if (_marketerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _marketerLevelData[level - 1].MarketingTime;
    }

    public override IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new MarketerAction(customerController);
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.transform.position = tableManager.GetStaffPos(0, StaffType.Marketer);

        GameManager.Instance.AddScore(_marketerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(_marketerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
        GameManager.Instance.AddScore(-_marketerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(-_marketerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void UseSkill()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class MarketerLevelData
{
    [SerializeField] private float _marketingTime;
    public float MarketingTime => _marketingTime;
    [Range(0f, 200f)] [SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [SerializeField] private int _scoreIncrement;
    public int ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _nextLevelUpgradeMoney;
    public int NextLevelUpgradeMoney => _nextLevelUpgradeMoney;
}
