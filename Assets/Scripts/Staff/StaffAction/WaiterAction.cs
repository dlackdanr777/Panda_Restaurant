using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;

public class WaiterAction : IStaffAction
{
    private const float _duration = 2f;

    private TableManager _tableManager;
    private TweenData _tweenData;


    private bool _isUsed = false;
    private bool _isNoAction = false;
    private bool _notEqulsFloor = false;

    private float _time;
    private float _durationMul;


    private Vector3 _defaultPos;
    private StaffWaiter _waiter;

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
        _waiter.OnLevelUpEventHandler -= OnLevelUpEvent;
        _tweenData?.TweenStop();
    }


    public void PerformAction(Staff staff)
    {
        if (_isUsed)
            return;

        float speedMul = staff.SpeedMul;
        if (_time < (_duration * _durationMul))
        {
            _time += Time.deltaTime * speedMul;
            return;
        }

        if (UserInfo.IsTutorialStart || !UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, staff.EquipFloorType))
        {
            ResetStaffState(staff);
            return;
        }

        List<TableData> servingTableList =  _tableManager.GetTableDataList(ERestaurantFloorType.Floor1, ETableState.CanServing);
        List<TableData> orderTableList = _tableManager.GetTableDataList(staff.EquipFloorType, ETableState.Seating);
        _isUsed = true;
        if (servingTableList.Count == 0 && orderTableList.Count == 0)
        {
            ResetStaffState(staff);
            return;
        }

        else if(0 < servingTableList.Count)
        {
            ServingAction(servingTableList);
        }

        else if(0 < orderTableList.Count)
        {
            OrderAction(orderTableList);
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


    private void ServingAction(List<TableData> servingTableList)
    {
        TableData data = _tableManager.GetMinDistanceTable(_waiter.EquipFloorType, _waiter.transform.position, servingTableList);
        if (data == null)
            return;

        if(1 < Mathf.Abs(data.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, data.TableFurniture.transform.position), 0, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    _waiter.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _notEqulsFloor = false;
                    _time = 0;
                    return;
                }

                _tableManager.OnServigStaff(data);
                _waiter.BowlSetActive(true);
                _waiter.Move(data.transform.position, data.SitDir, () =>
                {
                    _tweenData = Tween.Wait(((_duration * _durationMul) / speedMul) * 0.5f, () =>
                    {
                        _waiter.BowlSetAction();
                        _waiter.ShowFood(data.CurrentFood.FoodData);
                        _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                        {
                            if (data.CurrentCustomer == null || data.TableState != ETableState.StaffServing)
                            {
                                _waiter.SetStaffState(EStaffState.None);
                                _isUsed = false;
                                _notEqulsFloor = false;
                                _time = 0;
                                return;
                            }

                            _waiter.HideFood();
                            _tableManager.OnServing(data);
                            _tweenData = Tween.Wait(1 / speedMul, () =>
                            {
                                _waiter.SetStaffState(EStaffState.None);
                                _isUsed = false;
                                _notEqulsFloor = false;
                                _time = 0;
                            });
                        });
                    });
                });
            });
        });
    }

    private void OrderAction(List<TableData> orderTableList)
    {
        TableData data = _tableManager.GetMinDistanceTable(_waiter.EquipFloorType, _waiter.transform.position, orderTableList);
        if (data == null)
            return;

        if (1 < Mathf.Abs(data.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        _waiter.Move(data.transform.position, data.SitDir, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    _waiter.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _notEqulsFloor = false;
                    _time = 0;
                    return;
                }

                _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, _waiter.transform.position), 0, () =>
                {

                    _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                    {
                        if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                        {
                            _waiter.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _notEqulsFloor = false;
                            _time = 0;
                            return;
                        }

                        _waiter.SetStaffState(EStaffState.Action);

                        _tableManager.OnCustomerOrder(data);
                        _tweenData = Tween.Wait(1 / speedMul, () =>
                        {
                            _waiter.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _notEqulsFloor = false;
                            _time = 0;
                        });
                    });
                });
            });

        });
    }

    private void OnLevelUpEvent()
    {
        if (_waiter == null)
            return;

        _durationMul = 6f / _waiter.StaffData.GetSpeed(_waiter.Level);
    }
}
