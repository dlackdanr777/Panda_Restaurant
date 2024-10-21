using UnityEngine;

public class ShopData : BasicData
{
    [Space]
    [Header("ShopData")]

    [SerializeField] protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] protected int _buyScore;
    public int BuyScore => _buyScore;

    [SerializeField] protected int _buyPrice;
    public int BuyPrice => _buyPrice;
}
