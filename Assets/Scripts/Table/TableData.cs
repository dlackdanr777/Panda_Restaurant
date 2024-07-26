using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TableData
{
    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    [SerializeField] private Transform _tableButtonTr;
    public Transform TableButtonTr => _tableButtonTr;

    [SerializeField] private Transform[] _chairTrs;
    public Transform[] ChairTrs => _chairTrs;

    [SerializeField] private Transform _coinTr;
    public Transform CoinTr => _coinTr;

    [SerializeField] private Transform _garbageTr;
    public Transform GarbageTr => _garbageTr;


    [SerializeField] private Transform _leftStaffTr;
    public Transform LeftStaffTr => _leftStaffTr;

    [SerializeField] private Transform _rightStaffTr;
    public Transform RightStaffTr => _rightStaffTr;

    private int _tipValue;
    public int TipValue => _tipValue;

    public int TotalPrice;

    public int OrdersCount;

    public ETableState TableState;

    public Customer CurrentCustomer;

    public CookingData CurrentFood;

    public int SitDir;

    public int CoinCount;

    public int GarbageCount;

    public List<GameObject> CoinList = new List<GameObject>();


    public void SetTipValue(int value)
    {
        _tipValue += value;
    }

}
