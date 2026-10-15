using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;

public class WaiterAction : IStaffAction
{
    private enum WaiterActionType
    {
        None,
        Serving,
        Order,
    }

    private const float _duration = 0.5f;

    private TableManager _tableManager;
    private TweenWait _tweenData;


    private bool _isUsed = false;
    private bool _isNoAction = false;
    private bool _notEqulsFloor = false;

    private float _time;
    private float _durationMul;


    private Vector3 _defaultPos;
    private StaffWaiter _waiter;

    private TableData _currentData;
    private TableUseIdentity _use;
    private TableOrderIdentity _order;
    private bool _disposed;
    private object _request;
    private object _seatLeg;
    private Vector3 _seatDestination;
    private int _deliveryStep;
    private float _deliveryDue;
    private TableServiceReservation _lease;
    private long _staffLifetime;
    private ERestaurantFloorType _floor;
    private bool IsCurrent => !_disposed && _waiter.IsWorkCurrent(this, _staffLifetime, _floor)
        && _use != null && _use.IsPartyCurrent(_currentData)
        && (_order == null || _order.IsCurrent(_currentData))
        && (_lease == null || _lease.IsCurrent);
    private WaiterActionType _actionType = WaiterActionType.None;
    private bool ContinueRequest(object request)
    {
        if (_disposed || !ReferenceEquals(_request, request)) return false;
        if (IsCurrent) return true;
        CancelCurrentWork();
        return false;
    }
    public WaiterAction(Staff staff, TableManager tableManager)
    {
        if (!(staff is StaffWaiter))
            throw new System.Exception("웨이터가 아닌 일반 스탭이 들어왔습니다.");

        _waiter = (StaffWaiter)staff;
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _notEqulsFloor = false;
        _time = 0;
        EquipStaffType type = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, staff.StaffData);
        if (type == EquipStaffType.Length)
            throw new System.Exception("웨이터 타입이 이상합니다:" + type);

        _defaultPos = _tableManager.GetStaffPos(staff.EquipFloorType, type);
        staff.transform.position = _defaultPos;

        _durationMul = 6f / staff.StaffData.GetSpeed(staff.Level);
    
        staff.SetAlpha(1);
        staff.SetSpriteDir(1);

        _waiter.OnLevelUpEventHandler += OnLevelUpEvent;
    }


    public void Destructor()
    {
        if (_disposed) return;
        _disposed = true;
        CancelCurrentWork();
        _waiter.OnLevelUpEventHandler -= OnLevelUpEvent;
    }

    internal void CancelCurrentWork()
    {
        _request = null; _seatLeg = null; _deliveryStep = 0;
        _tweenData?.TweenStop(); _tweenData = null;
        var cancelled = _lease; _lease = null;
        _use = null; _order = null; _currentData = null;
        _actionType = WaiterActionType.None; _isUsed = false; _isNoAction = false;
        _notEqulsFloor = false; _time = 0;
        cancelled?.Release();
        if (_request == null && _waiter != null && _waiter.IsActionOwner(this))
        {
            _waiter.StopMove(); _waiter.HideFood(); _waiter.BowlSetActive(false);
            if (_waiter.isActiveAndEnabled) _waiter.SetStaffState(EStaffState.None);
        }
    }
    public void PerformAction(Staff staff)
    {
        if (_disposed) return;
        if (_isUsed)
        {
            if (_request != null) ContinueRequest(_request);
            return;
        }

        float speedMul = staff.SpeedMul;
        if (_time < (_duration * _durationMul))
        {
            _time += Time.deltaTime * speedMul;
            return;
        }

        if (UserInfo.IsTutorialStart)
        {
            ResetStaffState(staff);
            return;
        }

        TableData servingData = _tableManager.FindWaiterTable(_waiter.transform.position, staff.EquipFloorType, true);
        TableData orderData = _tableManager.FindWaiterTable(_waiter.transform.position, staff.EquipFloorType, false);
       
        // 둘 다 없으면 초기화
        if (servingData == null && orderData == null)
        {
            ResetStaffState(staff);
            return;
        }
        _waiter.BowlSetActive(false);
        _isUsed = true;

        // 둘 중 하나만 있는 경우
        if (servingData == null)
        {
            OrderAction(orderData);
            return;
        }
        else if (orderData == null)
        {
            DebugLog.Log("서빙만 있음");
            ServingAction(servingData);
            return;
        }

        // 둘 다 있는 경우, Y값 차이 확인
        float servingYDiff = Mathf.Abs(servingData.transform.position.y - _waiter.transform.position.y);
        float orderYDiff = Mathf.Abs(orderData.transform.position.y - _waiter.transform.position.y);
        
        bool servingYInRange = servingYDiff <= 1f;
        bool orderYInRange = orderYDiff <= 1f;

        // Y값 차이가 1 이하인 것만 고려
        if (servingYInRange && !orderYInRange)
        {
            // 서빙만 Y값 범위 내
            ServingAction(servingData);
            return;
        }
        else if (!servingYInRange && orderYInRange)
        {
            // 주문만 Y값 범위 내
            OrderAction(orderData);
            return;
        }
        else if (servingYInRange && orderYInRange)
        {
            // 둘 다 Y값 범위 내면 X축 거리로 판단
            float servingXDistance = Vector2.Distance(
                new Vector2(servingData.transform.position.x, servingData.transform.position.z),
                new Vector2(_waiter.transform.position.x, _waiter.transform.position.z)
            );
            
            float orderXDistance = Vector2.Distance(
                new Vector2(orderData.transform.position.x, orderData.transform.position.z),
                new Vector2(_waiter.transform.position.x, _waiter.transform.position.z)
            );

            // 수평 거리가 더 짧은 쪽 선택
            if (servingXDistance <= orderXDistance)
            {
                ServingAction(servingData);
            }
            else
            {
                OrderAction(orderData);
            }
            return;
        }
        else
        {
            Vector3 doorPos = _tableManager.GetDoorPos(RestaurantType.Hall, servingData.transform.position);
            // 둘 다 Y값 범위를 벗어나면 X축 거리로 판단
            float servingXDistance = Vector2.Distance(
                new Vector2(servingData.transform.position.x, servingData.transform.position.z),
                new Vector2(doorPos.x, doorPos.z)
            );
            
            float orderXDistance = Vector2.Distance(
                new Vector2(orderData.transform.position.x, orderData.transform.position.z),
                new Vector2(doorPos.x, doorPos.z)
            );

            // 수평 거리가 더 짧은 쪽 선택
            if (servingXDistance <= orderXDistance)
            {
                ServingAction(servingData);
            }
            else
            {
                OrderAction(orderData);
            }
        }
    }


    private void ResetStaffState(Staff staff)
    {
        _waiter.BowlSetActive(false);
        _isUsed = false;
        _notEqulsFloor = false;
        _time = 0;

        if (_isNoAction)
            return;

        _isNoAction = true;
        if (Vector2.Distance(staff.transform.position, _defaultPos) > 0.1f)
            staff.Move(_defaultPos, 1);
    }

    private object BeginWork(TableData table, bool serving)
    {
        if (table == null || _disposed) { _isUsed = false; return null; }
        if (0.5f < Mathf.Abs(table.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None); _actionType = WaiterActionType.None;
            _notEqulsFloor = true; _isUsed = false; return null;
        }
        _staffLifetime = _waiter.WorkLifetime; _floor = _waiter.EquipFloorType;
        _currentData = table; _use = table.CurrentUse;
        var request = new object(); _request = request;
        TableServiceReservation lease;
        try { lease = _tableManager.ReserveWaiterWork(table, _waiter, this, serving); }
        catch { CancelCurrentWork(); throw; }
        if (!ReferenceEquals(_request, request)) { lease?.Release(); return null; }
        _lease = lease;
        if (_lease == null) { CancelCurrentWork(); return null; }
        _order = _lease.Order; _isUsed = true; _isNoAction = false;
        _actionType = serving ? WaiterActionType.Serving : WaiterActionType.Order;
        _waiter.BowlSetActive(false);
        return ContinueRequest(request) ? request : null;
    }

    private bool Arrived(object request)
    {
        if (!ContinueRequest(request)) return false;
        if (_waiter.ConfirmReachedMoveTarget()) return true;
        CancelCurrentWork(); return false;
    }

    private void ServingAction(TableData table)
    {
        var request = BeginWork(table, true);
        if (request == null) return;
        var lease = _lease;
        var food = lease.Food; // Keep the selected plate through every callback.
        float speedMul = _waiter.SpeedMul;
        _waiter.Move(_tableManager.GetFoodPos(_floor, RestaurantType.Hall, table.TableFurniture.transform.position), 0, () =>
        {
            if (!Arrived(request)) return;
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (!ContinueRequest(request)) return;
                if (!_tableManager.PickUpWaiterFood(lease)) { if (ReferenceEquals(_request, request)) CancelCurrentWork(); return; }
                if (!ContinueRequest(request)) return;
                _waiter.BowlSetActive(true); _waiter.ShowFood(food);
                MoveCarriedFoodToSeat(request, lease, speedMul);
            });
        });
    }

    internal void RefreshPartySeat(TableUseIdentity use)
    {
        if (!ReferenceEquals(_use, use) || !IsCurrent || _seatLeg == null
            || _lease == null || !_lease.IsServing || !_lease.PickedUp) return;
        var target = _currentData.transform.position;
        target.x = _currentData.GetSeatPosition(_lease.Order.PartyOrder.SeatIndex).x;
        if (target == _seatDestination) return;
        MoveCarriedFoodToSeat(_request, _lease, _waiter.SpeedMul);
    }

    private void MoveCarriedFoodToSeat(object request, TableServiceReservation lease, float speedMul)
    {
        if (!ContinueRequest(request)) return;
        _tweenData?.TweenStop(); _tweenData = null;
        var leg = new object(); _seatLeg = leg;
        var table = lease.Table;
        int seat = lease.Order.PartyOrder == null ? table.SitIndex : lease.Order.PartyOrder.SeatIndex;
        int direction = lease.Order.PartyOrder == null ? table.SitDir : (seat == 0 ? -1 : 1);
        Vector3 destination = table.transform.position;
        if (!lease.Use.IsSingle) destination.x = table.GetSeatPosition(seat).x;
        _seatDestination = destination;
        _waiter.Move(destination, direction, () =>
        {
            if (!ReferenceEquals(_seatLeg, leg) || !Arrived(request)) return;
            if (_deliveryStep == 0)
            { _deliveryStep = 1; _deliveryDue = Time.time + ((_duration * _durationMul) / speedMul) * .5f; }
            ContinueSeatDelivery(request, lease, speedMul, leg);
        });
    }

    private void ContinueSeatDelivery(object request, TableServiceReservation lease, float speedMul, object leg)
    {
        _tweenData = Tween.Wait(Mathf.Max(0, _deliveryDue - Time.time), () =>
        {
            if (!ReferenceEquals(_seatLeg, leg) || !ContinueRequest(request)) return;
            if (_deliveryStep == 1)
            {
                _waiter.BowlSetAction(); _deliveryStep = 2;
                _deliveryDue = Time.time + (_duration * _durationMul) / speedMul;
                ContinueSeatDelivery(request, lease, speedMul, leg); return;
            }
            _seatLeg = null;
            bool delivered = _tableManager.TryDeliverFood(lease.Order, lease);
            if (ReferenceEquals(_lease, lease)) _lease = null;
            if (!delivered) { lease.Release(); if (ReferenceEquals(_request, request)) CancelCurrentWork(); return; }
            if (!ContinueRequest(request)) return;
            _waiter.HideFood();
            _tweenData = Tween.Wait(1 / speedMul, () => { if (ContinueRequest(request)) SetNoneState(); });
        });
    }

    private void OrderAction(TableData table)
    {
        var request = BeginWork(table, false);
        if (request == null) return;
        var lease = _lease;
        float speedMul = _waiter.SpeedMul;
        _waiter.Move(table.transform.position, table.SitDir, () =>
        {
            if (!Arrived(request)) return;
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (!ContinueRequest(request)) return;
                _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                {
                    if (!ContinueRequest(request)) return;
                    _waiter.SetStaffState(EStaffState.Action);
                    bool handled = _tableManager.AcceptWaiterOrder(lease);
                    if (ReferenceEquals(_lease, lease)) _lease = null;
                    if (!handled) { if (ReferenceEquals(_request, request)) CancelCurrentWork(); return; }
                    if (!ContinueRequest(request)) return;
                    _tweenData = Tween.Wait(1 / speedMul, () => { if (ContinueRequest(request)) SetNoneState(); });
                });
            });
        });
    }

    private void SetNoneState() { CancelCurrentWork(); }
    private void OnLevelUpEvent()
    {
        if (_waiter == null)
            return;

        _durationMul = 6f / _waiter.StaffData.GetSpeed(_waiter.Level);
    }
}
