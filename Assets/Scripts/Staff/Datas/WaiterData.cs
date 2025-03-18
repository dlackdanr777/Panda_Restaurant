using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WaiterData", menuName = "Scriptable Object/Staff/Waiter")]
public class WaiterData : StaffData
{
    [SerializeField] private WaiterLevelData[] _waiterLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _waiterLevelData.Length;

    public override float GetSpeed(int level)
    {
        level -= 1;
        if (_waiterLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _speed + _waiterLevelData[level].AddSpeed;
    }

    public override float GetActionValue(int level)
    {
        DebugLog.Log("사용하지 않음");
        return 0;
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
        staff.SetSpriteDir(1);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level -= 1;
        if (_waiterLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _waiterLevelData[level].UpgradeMinScore;
    }

    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level -= 1;
        if (_waiterLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _waiterLevelData[level].UpgradeMoneyData;
    }
}


[Serializable]
public class WaiterLevelData : StaffLevelData
{
    [Range(0, 10)][SerializeField] private float _addSpeed;
    public float AddSpeed => _addSpeed;
}
