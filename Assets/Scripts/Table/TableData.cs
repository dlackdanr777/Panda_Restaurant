using System;
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

    [SerializeField] private Transform _leftStaffTr;
    public Transform LeftStaffTr => _leftStaffTr;

    [SerializeField] private Transform _rightStaffTr;
    public Transform RightStaffTr => _rightStaffTr;

    private int _tipValue;
    public int TipValue => _tipValue;

    public ETableState TableState;

    public Customer CurrentCustomer;

    public CookingData CurrentFood;

    public void SetTipValue(int value)
    {
        _tipValue += value;
    }
}