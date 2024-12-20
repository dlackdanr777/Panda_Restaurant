using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ManagerData", menuName = "Scriptable Object/Staff/Manager")]
public class ManagerData : StaffData
{
    [SerializeField] private ManagerLevelData[] _managerLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _managerLevelData.Length;


    public override float GetActionValue(int level)
    {
        if (_managerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _managerLevelData[level - 1].MaxWaitCustomerCount;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ManagerAction(tableManager);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _managerLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.transform.position = tableManager.GetStaffPos(0, StaffType.Manager);
        staff.SetLayer("Manager", 0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMoneyData;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class ManagerLevelData : StaffLevelData
{
    [Range(0, 100)] [SerializeField] private int _maxWaitCustomerCount;
    public int MaxWaitCustomerCount => _maxWaitCustomerCount;
}
