using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CleanerData", menuName = "Scriptable Object/Staff/Cleaner")]
public class CleanerData : StaffData
{
    [SerializeField] private CleanerLevelData[] _cleanerLevelData;

    public override float SecondValue => 0;

    public override float GetActionValue(int level)
    {
        if (_cleanerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("웨이터 레벨의 범위를 넘어섰습니다.");

        return _cleanerLevelData[level - 1].CleaningTime;
    }

    public override IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new CleanerAction(tableManager);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _cleanerLevelData.Length;
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);

        GameManager.Instance.AddScore(_cleanerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(_cleanerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
        GameManager.Instance.AddScore(-_cleanerLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(-_cleanerLevelData[staff.Level - 1].TipAddPercent);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].UpgradeMinScore;
    }


    public override int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _cleanerLevelData.Length - 1);
        return _cleanerLevelData[level].UpgradeMoneyData.Price;
    }
}


[Serializable]
public class CleanerLevelData : StaffLevelData
{
    [SerializeField] private float _cleaningTime;
    public float CleaningTime => _cleaningTime;
}
