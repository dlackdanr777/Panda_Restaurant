using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GuardData", menuName = "Scriptable Object/Staff/Guard")]
public class GuardData : StaffData
{
    [Range(0f, 10f)] [SerializeField] private float _actionTime;
    [SerializeField] private GuardLevelData[] _guardLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _guardLevelData.Length;


    public override float GetActionValue(int level)
    {
        return _actionTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new GuardAction(customerController, tableManager);
    }


    public override bool UpgradeEnable(int level)
    {
        return level < _guardLevelData.Length;
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.SetSpriteDir(-1);
        staff.transform.position = tableManager.GetStaffPos(0, StaffType.Guard);
        staff.SetLayer("Guard", 0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _guardLevelData.Length - 1);
        return _guardLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _guardLevelData.Length - 1);
        return _guardLevelData[level].UpgradeMoneyData;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _guardLevelData.Length - 1);
        return _guardLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _guardLevelData.Length - 1);
        return _guardLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class GuardLevelData : StaffLevelData
{
}
