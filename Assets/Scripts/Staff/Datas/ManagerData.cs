using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ManagerData", menuName = "Scriptable Object/Staff/Manager")]
public class ManagerData : StaffData
{
    [SerializeField] private ManagerLevelData[] _managerLevelData;
    public override float SecondValue => 0;

    public override float GetActionValue(int level)
    {
        if (_managerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("������ ������ �Ѿ���ϴ�.");

        return _managerLevelData[level - 1].GuideTime;
    }

    public override IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
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

        GameManager.Instance.AddScore(_managerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(_managerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);

        GameManager.Instance.AddScore(-_managerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(-_managerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMinScore;
    }


    public override int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMoneyData.Price;
    }
}


[Serializable]
public class ManagerLevelData : StaffLevelData
{
    [SerializeField] private float _guideTime;
    public float GuideTime => _guideTime;
}