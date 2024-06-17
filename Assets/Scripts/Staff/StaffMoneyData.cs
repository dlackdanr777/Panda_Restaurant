using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StaffMoneyData
{
    [SerializeField] private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    [SerializeField] private int _price;
    public int Price => _price;
}
