using UnityEngine;

public class GachaItemData : GachaData
{
    

    private int _addScore;
    public int AddScore => _addScore;

    private int _tipPerMinute;
    public int TipPerMinute => _tipPerMinute;
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
        _rank = (Rank)rank - 1;
        _upgradeType = upgradeType;
        _defaultValue = defaultValue;
        _upgradeValue = upgradeValue;
        _maxLevel = maxLevel;
        _sprite = sprite;
        _thumbnailSprite = sprite;
    }
}
