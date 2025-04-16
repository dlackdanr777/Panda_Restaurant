using System;
using UnityEngine;

[Serializable]
public class TableData : MonoBehaviour
{
    public event Action<ETableState> OnChangeTableStateHandler;

    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    [SerializeField] private Transform _tableButtonTr;
    public Transform TableButtonTr => _tableButtonTr;

    [SerializeField] private Transform[] _chairTrs;
    public Transform[] ChairTrs => _chairTrs;

    public void SetLeftChairTrPos(Vector3 pos) => _chairTrs[0].position = pos;
    public void SetRightChairTrPos(Vector3 pos) => _chairTrs[1].position = pos;

    /// <summary>ChairTrs[]와 Index 연동을 통해 해당 의자위치에서 버리는 코인의 위치 클래스를 담는다.</summary>
    [SerializeField] private DropCoinArea[] _dropCoinAreas;
    public DropCoinArea[] DropCoinAreas => _dropCoinAreas;

    [SerializeField] private DropGarbageArea _dropGarbageArea;
    public DropGarbageArea DropGarbageArea => _dropGarbageArea;

    public TableButton OrderButton;

    public TableButton ServingButton;

    private ETableState _tableState;
    public ETableState TableState
    {
        get => _tableState;
        set
        {
            if(value == _tableState)
                return;

            _beforeTableState = _tableState;
            _tableState = value;
            OnChangeTableStateHandler?.Invoke(_tableState);
        }
    }

    private ETableState _beforeTableState;
    public ETableState BeforeTableState => _beforeTableState;


    public ERestaurantFloorType FloorType;

    private TableType _tableType;
    public TableType TableType
    {
        get => _tableType;
        set => _tableType = value;
    }


    public TableFurniture TableFurniture;

    public int CustomerId;


    public int TotalTip;

    public int TotalPrice;

    public int OrdersCount;

    [HideInInspector] public int Satisfaction = 0;

    public NormalCustomer CurrentCustomer;

    public CookingData CurrentFood;

    public int SitDir;

    public int SitIndex;
}
