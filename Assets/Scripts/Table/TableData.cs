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

    /// <summary>ChairTrs[]와 Index 연동을 통해 해당 의자위치에서 버리는 코인의 위치 클래스를 담는다.</summary>
    [SerializeField] private DropCoinArea[] _dropCoinAreas;
    public DropCoinArea[] DropCoinAreas => _dropCoinAreas;

    [SerializeField] private DropGarbageArea _dropGarbageArea;
    public DropGarbageArea DropGarbageArea => _dropGarbageArea;

    [SerializeField] private Transform _leftStaffTr;
    public Transform LeftStaffTr => _leftStaffTr;

    [SerializeField] private Transform _rightStaffTr;
    public Transform RightStaffTr => _rightStaffTr;

    public TableButton OrderButton;

    public TableButton ServingButton;

    public ETableState TableState;

    public ERestaurantFloorType FloorType;

    public TableType TableType;

    public int TotalTip;

    public int TotalPrice;

    public int OrdersCount;

    public NormalCustomer CurrentCustomer;

    public CookingData CurrentFood;

    public int SitDir;

    public int SitIndex;
}
