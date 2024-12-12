using System;
using UnityEngine;

[Serializable]
public class StaffLevelData
{
    [Range(0f, 1000f)][SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [Range(0f, 1000f)][SerializeField] private int _scoreIncrement;
    public int ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _upgradeMinScore;
    public int UpgradeMinScore => _upgradeMinScore;

    [SerializeField] private UpgradeMoneyData _upgradeMoneyData;
    public UpgradeMoneyData UpgradeMoneyData => _upgradeMoneyData;
}