using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CleanerData", menuName = "Scriptable Object/Staff/Cleaner")]
public class CleanerData : StaffData
{
    [SerializeField] private CleanerLevelData[] _cleanerLevelData;
    [Range(0.0f, 100f)] [SerializeField] private float _waitDuration;
    public override float SecondValue => _waitDuration;
    public override int MaxLevel => _cleanerLevelData.Length;


    public override float GetActionValue(int level)
    {
        if (_cleanerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _cleanerLevelData[level - 1].CleaningTime;
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
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].UpgradeMoneyData;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class CleanerLevelData : StaffLevelData
{
    [SerializeField] private float _cleaningTime;
    public float CleaningTime => _cleaningTime;
}
