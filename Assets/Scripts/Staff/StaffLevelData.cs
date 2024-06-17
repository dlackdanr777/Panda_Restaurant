using System;
using UnityEngine;

[Serializable]
public class StaffLevelData
{
    [Range(0f, 200f)][SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [Range(0f, 200f)][SerializeField] private int _scoreIncrement;
    public int ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _upgradeMinScore;
    public int UpgradeMinScore => _upgradeMinScore;

    [SerializeField] private StaffMoneyData _upgradeMoneyData;
    public StaffMoneyData UpgradeMoneyData => _upgradeMoneyData;
}