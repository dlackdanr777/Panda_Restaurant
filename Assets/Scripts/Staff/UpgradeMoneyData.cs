using System;
using UnityEngine;

[Serializable]
public class UpgradeMoneyData
{
    [SerializeField] private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] private int _price;
    public int Price => _price;
}
