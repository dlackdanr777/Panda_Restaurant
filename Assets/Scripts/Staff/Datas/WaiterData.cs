using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WaiterData", menuName = "Scriptable Object/Staff/Waiter")]
public class WaiterData : StaffData
{
    [SerializeField] private WaiterLevelData[] _waiterLevelData;
    public override float SecondValue => 0;

    public override float GetActionValue(int level)
    {
        if (_waiterLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _waiterLevelData[level - 1].ServingTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new WaiterAction(tableManager);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _waiterLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _waiterLevelData.Length - 1);
        return _waiterLevelData[level].UpgradeMinScore;
    }


    public override int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _waiterLevelData.Length - 1);
        return _waiterLevelData[level].UpgradeMoneyData.Price;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _waiterLevelData.Length - 1);
        return _waiterLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _waiterLevelData.Length - 1);
        return _waiterLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class WaiterLevelData : StaffLevelData
{
    [SerializeField] private float _servingTime;
    public float ServingTime => _servingTime;
}
