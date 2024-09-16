using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MarketerData", menuName = "Scriptable Object/Staff/Marketer")]
public class MarketerData : StaffData
{
    [Range(0f, 100f)] [SerializeField] private float _customerCallPercentage;
    [SerializeField] private MarketerLevelData[] _marketerLevelData;
    public override float SecondValue => _customerCallPercentage;
    public override int MaxLevel => _marketerLevelData.Length;

    public override float GetActionValue(int level)
    {
        if (_marketerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _marketerLevelData[level - 1].MarketingTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new MarketerAction(customerController);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _marketerLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.transform.position = tableManager.GetStaffPos(0, StaffType.Marketer);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _marketerLevelData.Length - 1);
        return _marketerLevelData[level].UpgradeMinScore;
    }


    public override int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _marketerLevelData.Length - 1);
        return _marketerLevelData[level].UpgradeMoneyData.Price;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _marketerLevelData.Length - 1);
        return _marketerLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _marketerLevelData.Length - 1);
        return _marketerLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class MarketerLevelData : StaffLevelData
{
    [SerializeField] private float _marketingTime;
    public float MarketingTime => _marketingTime;
}
