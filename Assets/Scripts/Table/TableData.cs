using System;
using UnityEngine;

[Serializable]
public class TableData : MonoBehaviour
{
    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    [SerializeField] private Transform _tableButtonTr;
    public Transform TableButtonTr => _tableButtonTr;

    [SerializeField] private Transform[] _chairTrs;
    public Transform[] ChairTrs => _chairTrs;

    /// <summary>ChairTrs[]�� Index ������ ���� �ش� ������ġ���� ������ ������ ��ġ Ŭ������ ��´�.</summary>
    [SerializeField] private DropCoinArea[] _dropCoinAreas;
    public DropCoinArea DropCoinArea => _dropCoinAreas[SitIndex];

    [SerializeField] private DropGarbageArea _dropGarbageArea;
    public DropGarbageArea DropGarbageArea => _dropGarbageArea;

    [SerializeField] private Transform _leftStaffTr;
    public Transform LeftStaffTr => _leftStaffTr;

    [SerializeField] private Transform _rightStaffTr;
    public Transform RightStaffTr => _rightStaffTr;


    public int TotalTip;

    public int TotalPrice;

    public int OrdersCount;

    public ETableState TableState;

    public Customer CurrentCustomer;

    public CookingData CurrentFood;

    public int SitDir;

    public int SitIndex;
}
