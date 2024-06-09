using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CleanerData", menuName = "Scriptable Object/Staff/Cleaner")]
public class CleanerDataData : StaffData
{
    [SerializeField] private CleanerLevelData[] _cleanerLevelData;


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


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        throw new NotImplementedException();
    }

    public override void UseSkill()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class CleanerLevelData
{
    [SerializeField] private float _cleaningTime;
    public float CleaningTime => _cleaningTime;
    [SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [SerializeField] private float _scoreIncrement;
    public float ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _nextLevelUpgradeMoney;
    public int NextLevelUpgradeMoney => _nextLevelUpgradeMoney;
}
