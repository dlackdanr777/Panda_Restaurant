using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerData", menuName = "Scriptable Object/Staff/Server")]
public class ServerData : StaffData
{
    [SerializeField] private ServerLevelData[] _serverLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _serverLevelData.Length;


    public override float GetActionValue(int level)
    {
        if (_serverLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _serverLevelData[level - 1].ServingTime;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ServerAction(tableManager);
    }


    public override bool UpgradeEnable(int level)
    {
        return level < _serverLevelData.Length;
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
        staff.SetSpriteDir(-1);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _serverLevelData.Length - 1);
        return _serverLevelData[level].UpgradeMinScore;
    }

    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _serverLevelData.Length - 1);
        return _serverLevelData[level].UpgradeMoneyData;
    }

    public override float GetSpeed(int level)
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class ServerLevelData : StaffLevelData
{
    [SerializeField] private float _servingTime;
    public float ServingTime => _servingTime;
}
