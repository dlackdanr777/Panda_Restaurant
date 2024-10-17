using UnityEngine;

public class GachaItemData
{
    public GachaItemRank GachaItemRank => _rank - 1 < 0 ? throw new System.Exception("Rank�� ���� �ۿ� �ֽ��ϴ�.") : (int)GachaItemRank.Length <= _rank - 1 ? throw new System.Exception("Rank�� ���� �ۿ� �ֽ��ϴ�.") : (GachaItemRank)_rank - 1;

    private string _id;
    public string Id => _id;

    private string _name;
    public string Name => _name;

    private string _description;
    public string Description => _description;

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
    public float UpdatingValue => _upgradeValue;

    private int _maxLevel;
    public int MaxLevel => _maxLevel;

    private Sprite _sprite;
    public Sprite Sprite => _sprite;


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
