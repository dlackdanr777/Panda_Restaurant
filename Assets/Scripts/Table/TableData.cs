using System;
using UnityEngine;

[Serializable]
public class TableData : MonoBehaviour
{
    public event Action<ETableState> OnChangeTableStateHandler;
    public event Action OnServiceChangedHandler;
    public string LastServiceDiagnostic { get; internal set; }
    public void NotifyServiceChanged()
    {
        try { RefreshPartyOrderButtons(); } catch (Exception error) { LastServiceDiagnostic = error.Message; }
        var handlers = OnServiceChangedHandler;
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception error) { LastServiceDiagnostic = error.Message; }
    }

    [SerializeField] private Transform _customerMoveTr;
    public Transform CustomerMoveTr => _customerMoveTr;

    [SerializeField] private Transform _tableButtonTr;
    public Transform TableButtonTr => _tableButtonTr;

    [SerializeField] private Transform[] _chairTrs;
    public Transform[] ChairTrs => _chairTrs;
    [SerializeField] private Vector2 _leftSeatOffset;
    [SerializeField] private Vector2 _rightSeatOffset;
    public Vector3 GetSeatPosition(int index) => _chairTrs[index].position
        + (Vector3)(index == 0 ? _leftSeatOffset : _rightSeatOffset);
    public int OccupantCount => CurrentParty == null ? 0 : CurrentParty.Members.Count;

    public void SetLeftChairTrPos(Vector3 pos) => _chairTrs[0].position = pos;
    public void SetRightChairTrPos(Vector3 pos) => _chairTrs[1].position = pos;

    /// <summary>ChairTrs[]와 Index 연동을 통해 해당 의자위치에서 버리는 코인의 위치 클래스를 담는다.</summary>
    [SerializeField] private DropCoinArea[] _dropCoinAreas;
    public DropCoinArea[] DropCoinAreas => _dropCoinAreas;

    [SerializeField] private DropGarbageArea _dropGarbageArea;
    public DropGarbageArea DropGarbageArea => _dropGarbageArea;

    public TableButton OrderButton;
    public TableButton CompanionOrderButton;
    [SerializeField] private Vector3 _leftOrderOffset;
    [SerializeField] private Vector3 _rightOrderOffset;
    public PartyOrderBatch PartyOrders => CurrentUse == null ? null : CurrentUse.Orders;

    public void RefreshPartyOrderButtons()
    {
        var batch = PartyOrders;
        if (CompanionOrderButton != null) CompanionOrderButton.gameObject.SetActive(false);
        if (CompanionServingButton != null) CompanionServingButton.gameObject.SetActive(false);
        if (batch == null)
        {
            if (ServingButton != null)
            {
                ServingButton.Interactable(CurrentUse == null || CurrentFood.Identity == null || CurrentFood.Identity.ServingReservation == null);
                ServingButton.gameObject.SetActive(CurrentUse != null && CurrentUse.IsSingle && TableState == ETableState.CanServing);
            }
            if (OrderButton != null) OrderButton.gameObject.SetActive(CurrentUse != null && CurrentUse.IsSingle && TableState == ETableState.Seating);
            return;
        }
        if (OrderButton != null) OrderButton.gameObject.SetActive(false);
        if (ServingButton != null) ServingButton.gameObject.SetActive(false);
        bool show = batch.State == PartyOrderBatchState.Prepared && !batch.Use.PolicyPending && !batch.Use.CompletionStarted && !batch.Use.ServiceAborted;
        for (int index = 0; index < batch.Orders.Count; index++)
        {
            var order = batch.Orders[index];
            var button = order.SeatIndex == 0 ? OrderButton : CompanionOrderButton;
            if (button == null) continue;
            int seat = order.SeatIndex;
            button.SetWorldTransform(ChairTrs[seat], GetSeatPosition(seat) - ChairTrs[seat].position
                + (seat == 0 ? _leftOrderOffset : _rightOrderOffset));
            button.SetData(order.Food);
            button.gameObject.SetActive(show);
            var serving = seat == 0 ? ServingButton : CompanionServingButton;
            if (serving != null)
            {
                serving.SetWorldTransform(ChairTrs[seat], GetSeatPosition(seat) - ChairTrs[seat].position
                    + (seat == 0 ? _leftServingOffset : _rightServingOffset));
                serving.SetData(order.Food);
                serving.Interactable(order.Identity.ServingReservation == null);
                serving.gameObject.SetActive(TableManager.IsFoodAtCounter(order.Identity));
            }
        }

    }

    public TableButton ServingButton;
    public TableButton CompanionServingButton;
    [SerializeField] private Vector3 _leftServingOffset;
    [SerializeField] private Vector3 _rightServingOffset;
    public PartyFoodOrder GetPartyOrderAtSeat(int seat)
    {
        if (PartyOrders != null) foreach (var food in PartyOrders.Orders) if (food.SeatIndex == seat) return food;
        return null;
    }

    internal object CleaningCycle { get; private set; } = new object();
    private ETableState _tableState;
    public ETableState TableState
    {
        get => _tableState;
        set
        {
            if(value == _tableState)
                return;

            if (value == ETableState.NeedCleaning) CleaningCycle = new object();
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

    // CurrentUse/CurrentParty own table occupancy and seats. CurrentCustomer/CurrentFood
    // are compatibility fields for the unchanged single-person meal flow only.
    internal TableUseIdentity CurrentUse { get; private set; }

    public CustomerVisitParty CurrentParty => CurrentUse != null && CurrentUse.Party.IsActive ? CurrentUse.Party : null;

    internal TableUseIdentity BeginUse(TableManager owner, object execution, NormalCustomer customer,
        CustomerVisitMember member, int seatIndex)
    {
        if (CurrentUse != null || CurrentCustomer != null || member == null
            || !ReferenceEquals(member.Customer, customer)) return null;
        var use = new TableUseIdentity(owner, execution, customer, this, member);
        int[] seats = member.Party.Members.Count == 1 ? new[] { seatIndex } : new[] { 0, 1 };
        if (!member.Party.TryBind(use, seats)) return null;
        CurrentUse = use;
        return use;
    }

    internal void EndUse(TableUseIdentity expected = null, ETableState releasedState = ETableState.Empty)
    {
        var use = CurrentUse;
        if (use == null || (expected != null && !ReferenceEquals(use, expected))) return;
        CurrentUse = null;
        use.Invalidated = true;
        use.OrderReservation?.Release();
        if (use.IsSingle) CurrentFood.Identity?.ServingReservation?.Release();
        use.Orders?.Cancel();
        use.Party.End();
        if (!use.IsSingle)
        {

            foreach (var member in use.Party.Members)
                if (member.Customer != null && ReferenceEquals(member.Customer.VisitIdentity, member.VisitIdentity))
                {
                    member.Customer.StopMove();
                    if (member.Customer.gameObject.activeSelf) ObjectPoolManager.Instance.DespawnNormalCustomer(member.Customer);
                }
            TableState = releasedState;
            NotifyServiceChanged();
        }
    }
    internal void ReleaseCompletedParty(TableUseIdentity use)
    {
        if (!ReferenceEquals(CurrentUse, use) || !use.SettlementCompleted) return;
        CurrentUse = null;
        use.Party.End();
        NotifyServiceChanged();
    }
    private void OnDisable() { EndUse(); }

    public NormalCustomer CurrentCustomer;

    public CookingData CurrentFood;

    public int SitDir;

    public int SitIndex;
}
