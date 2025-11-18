using UnityEngine;

public class SkinData : GachaData, ShopData
{
    protected int _addScore;
    public int AddScore => _addScore;

    protected int _addTipPerMinute;
    public int AddTipPerMinute => _addTipPerMinute;

    protected float _upgradeValue;
    public float UpgradeValue => _upgradeValue;

    protected string _equipId;
    public string EquipId => _equipId;

    protected SalesLocationType _salesLocationType;
    public SalesLocationType SalesLocationType => _salesLocationType;
    
    protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    protected int _buyScore;
    public int BuyScore => _buyScore;

    protected int _buyPrice;
    public int BuyPrice => _buyPrice;

    protected int _duplicationToken;
    public int DuplicationToken => _duplicationToken;
}
