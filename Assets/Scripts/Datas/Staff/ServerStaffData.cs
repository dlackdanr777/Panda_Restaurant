using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerStaffData", menuName = "Scriptable Object/Staff/Server")]
public class ServerStaffData : StaffData
{
    [SerializeField] private WaiterLevelData[] _waiterLevelData;


    public override float GetActionTime(int level)
    {
        if (_waiterLevelData.Length < level - 1 || level < 0)
            return 1000000;

        return _waiterLevelData[level - 1].ServingTime;
    }


    public override void AddSlot()
    {
        throw new NotImplementedException();
    }

    public override bool IsUpgradeEnabed()
    {
        throw new NotImplementedException();
    }

    public override void RemoveSlot()
    {
        throw new NotImplementedException();
    }

    public override void Update()
    {
        throw new NotImplementedException();
    }

    public override void UseSkill()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class WaiterLevelData
{
    [SerializeField] private float _servingTime;
    public float ServingTime => _servingTime;
    [SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [SerializeField] private float _scoreIncrement;
    public float ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _nextLevelUpgradeMoney;
    public int NextLevelUpgradeMoney => _nextLevelUpgradeMoney;
}
