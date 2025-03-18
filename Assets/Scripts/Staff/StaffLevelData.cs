using System;
using UnityEngine;

[Serializable]
public class StaffLevelData
{
    [SerializeField] private int _upgradeMinScore;
    public int UpgradeMinScore => _upgradeMinScore;

    [SerializeField] private UpgradeMoneyData _upgradeMoneyData;
    public UpgradeMoneyData UpgradeMoneyData => _upgradeMoneyData;
}