using UnityEngine;

public class SkinData : ShopData
{
    protected int _addScore;
    public int AddScore => _addScore;

    protected int _addTipPerMinute;
    public int AddTipPerMinute => _addTipPerMinute;

    protected Rank _rank;
    public Rank Rank => _rank;

    protected float _upgradeValue;
    public float UpgradeValue => _upgradeValue;

    protected string _equipId;
    public string EquipId => _equipId;

    
    
}
