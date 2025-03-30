using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GuardData", menuName = "Scriptable Object/Staff/Guard")]
public class GuardData : StaffData
{

    [SerializeField] private GuardLevelData[] _guardLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _guardLevelData.Length;


    public override float GetSpeed(int level) => _speed;

    public override float GetActionValue(int level)
    {
        level -= 1;
        if (_guardLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _guardLevelData[level].ActionTime;
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
        staff.transform.position = tableManager.GetStaffPos(staff.EquipFloorType, EquipStaffType.Guard);
        staff.SetLayer("Guard", 0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level -= 1;
        if (_guardLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");
        return _guardLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level -= 1;
        if (_guardLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _guardLevelData[level].UpgradeMoneyData;
    }
}


[Serializable]
public class GuardLevelData : StaffLevelData
{
    [Range(0f, 10f)][SerializeField] private float _actionTime;
    public float ActionTime => _actionTime;
}
