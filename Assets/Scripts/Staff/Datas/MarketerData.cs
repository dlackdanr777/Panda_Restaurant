using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MarketerData", menuName = "Scriptable Object/Staff/Marketer")]
public class MarketerData : StaffData
{
    [SerializeField] private MarketerLevelData[] _marketerLevelData;


    public override float GetActionValue(int level)
    {
        if (_marketerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("������ ������ ������ �Ѿ���ϴ�.");

        return _marketerLevelData[level - 1].MarketingTime;
    }

    public override IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new MarketerAction(customerController);
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.transform.position = tableManager.CashRegisterTr.position;
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override void UseSkill()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class MarketerLevelData
{
    [SerializeField] private float _marketingTime;
    public float MarketingTime => _marketingTime;
    [SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [SerializeField] private float _scoreIncrement;
    public float ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _nextLevelUpgradeMoney;
    public int NextLevelUpgradeMoney => _nextLevelUpgradeMoney;
}
