using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CleanerData", menuName = "Scriptable Object/Staff/Cleaner")]
public class CleanerData : StaffData
{
    [SerializeField] private CleanerLevelData[] _cleanerLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _cleanerLevelData.Length;

    public override float GetSpeed(int level)
    {
        level -= 1;
        if (_cleanerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _speed + _cleanerLevelData[level].AddSpeed;
    }

    public override float GetActionValue(int level)
    {
        level -= 1;
        if (_cleanerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _cleanerLevelData[level].CleaningTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new CleanerAction(staff, tableManager);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _cleanerLevelData.Length;
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
        level -= 1;
        if (_cleanerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _cleanerLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level -= 1;
        if (_cleanerLevelData.Length <= level || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _cleanerLevelData[level].UpgradeMoneyData;
    }
}


[Serializable]
public class CleanerLevelData : StaffLevelData
{
    [SerializeField] private float _cleaningTime;
    public float CleaningTime => _cleaningTime;

    [Range(0, 10)] [SerializeField] private float _addSpeed;
    public float AddSpeed => _addSpeed;

}
