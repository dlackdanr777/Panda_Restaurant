using UnityEngine;

public class GachaItemData : BasicData
{
    public GachaItemRank GachaItemRank => _rank - 1 < 0 ? throw new System.Exception("Rank가 범위 밖에 있습니다.") : (int)GachaItemRank.Length <= _rank - 1 ? throw new System.Exception("Rank가 범위 밖에 있습니다.") : (GachaItemRank)_rank - 1;

    private int _addScore;
    public int AddScore => _addScore;

    private int _tipPerMinute;
    public int TipPerMinute => _tipPerMinute;

    private int _rank;
    public int Rank => _rank;
    
    private UpgradeType _upgradeType;
    public UpgradeType UpgradeType => _upgradeType;

    private float _defaultValue;
    public float DefaultValue => _defaultValue;

    private float _upgradeValue;
    public float UpgradeValue => _upgradeValue;

    private int _maxLevel;
    public int MaxLevel => _maxLevel;


    public GachaItemData(string id, string name, string description, int addScore, int minutePerTip, int rank, UpgradeType upgradeType, float defaultValue, float upgradeValue, int maxLevel, Sprite sprite)
    {
        _id = id;
        _name = name;
        _description = description;
        _addScore = addScore;
        _tipPerMinute = minutePerTip;
        _rank = rank;
        _upgradeType = upgradeType;
        _defaultValue = defaultValue;
        _upgradeValue = upgradeValue;
        _maxLevel = maxLevel;
        _sprite = sprite;
    }
}
