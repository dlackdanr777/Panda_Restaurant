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

    private const float _duration = 2f;

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
    private WaiterActionType _actionType = WaiterActionType.None;

    public WaiterAction(Staff staff, TableManager tableManager)
    {
        if (!(staff is StaffWaiter))
            throw new System.Exception("�����Ͱ� �ƴ� �Ϲ� ������ ���Խ��ϴ�.");

        _waiter = (StaffWaiter)staff;
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _notEqulsFloor = false;
        _time = 0;
        EquipStaffType type = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, staff.StaffData);
        if (type == EquipStaffType.Length)
            throw new System.Exception("������ Ÿ���� �̻��մϴ�:" + type);

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

        if(_tweenData != null && _actionType != WaiterActionType.None)
        {
            _currentData.OrdersCount += 1;
            _tableManager.OnCustomerSeating(_currentData);
            _currentData = null;
        }
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

        if (UserInfo.IsTutorialStart)
        {
            ResetStaffState(staff);
            return;
        }

        List<TableData> servingTableList = _tableManager.GetTableDataList(ERestaurantFloorType.Floor1, ETableState.CanServing);
        List<TableData> orderTableList = _tableManager.GetTableDataList(staff.EquipFloorType, ETableState.Seating);

        TableData servingData = _tableManager.GetMinDistanceTable(_waiter.EquipFloorType, _waiter.transform.position, servingTableList);
        TableData orderData = _tableManager.GetMinDistanceTable(_waiter.EquipFloorType, _waiter.transform.position, orderTableList);

        // �� �� ������ �ʱ�ȭ
        if (servingData == null && orderData == null)
        {
            ResetStaffState(staff);
            return;
        }

        _isUsed = true;

        // �� �� �ϳ��� �ִ� ���
        if (servingData == null)
        {
            OrderAction(orderData);
            return;
        }
        else if (orderData == null)
        {
            ServingAction(servingData);
            return;
        }

        // �� �� �ִ� ���, Y�� ���� Ȯ��
        float servingYDiff = Mathf.Abs(servingData.transform.position.y - _waiter.transform.position.y);
        float orderYDiff = Mathf.Abs(orderData.transform.position.y - _waiter.transform.position.y);
        
        bool servingYInRange = servingYDiff <= 1f;
        bool orderYInRange = orderYDiff <= 1f;

        // Y�� ���̰� 1 ������ �͸� ���
        if (servingYInRange && !orderYInRange)
        {
            // ������ Y�� ���� ��
            ServingAction(servingData);
            return;
        }
        else if (!servingYInRange && orderYInRange)
        {
            // �ֹ��� Y�� ���� ��
            OrderAction(orderData);
            return;
        }
        else if (servingYInRange && orderYInRange)
        {
            // �� �� Y�� ���� ���� X�� �Ÿ��� �Ǵ�
            float servingXDistance = Vector2.Distance(
                new Vector2(servingData.transform.position.x, servingData.transform.position.z),
                new Vector2(_waiter.transform.position.x, _waiter.transform.position.z)
            );
            
            float orderXDistance = Vector2.Distance(
                new Vector2(orderData.transform.position.x, orderData.transform.position.z),
                new Vector2(_waiter.transform.position.x, _waiter.transform.position.z)
            );

            // ���� �Ÿ��� �� ª�� �� ����
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
            // �� �� Y�� ������ ����� X�� �Ÿ��� �Ǵ�
            float servingXDistance = Vector2.Distance(
                new Vector2(servingData.transform.position.x, servingData.transform.position.z),
                new Vector2(doorPos.x, doorPos.z)
            );
            
            float orderXDistance = Vector2.Distance(
                new Vector2(orderData.transform.position.x, orderData.transform.position.z),
                new Vector2(doorPos.x, doorPos.z)
            );

            // ���� �Ÿ��� �� ª�� �� ����
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


    private void ServingAction(List<TableData> servingTableList)
    {
        TableData data = _tableManager.GetMinDistanceTable(_waiter.EquipFloorType, _waiter.transform.position, servingTableList);
        if (data == null)
            return;

        if(0.5f < Mathf.Abs(data.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _actionType = WaiterActionType.None;
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        _actionType = WaiterActionType.Serving;
        _currentData = data;

        _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, data.TableFurniture.transform.position), 0, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    SetNoneState();
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
                                SetNoneState();
                                return;
                            }

                            _waiter.HideFood();
                            _tableManager.OnServing(data);
                            _tweenData = Tween.Wait(1 / speedMul, () =>
                            {
                                SetNoneState();
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

        if (0.5f < Mathf.Abs(data.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _actionType = WaiterActionType.None;
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        _actionType = WaiterActionType.Order;
        _currentData = data;

        _waiter.Move(data.transform.position, data.SitDir, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    SetNoneState();
                    return;
                }

                _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, _waiter.transform.position), 0, () =>
                {

                    _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                    {
                        if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                        {
                            SetNoneState();
                            return;
                        }

                        _waiter.SetStaffState(EStaffState.Action);

                        _tableManager.OnCustomerOrder(data);
                        _tweenData = Tween.Wait(1 / speedMul, () =>
                        {
                            SetNoneState();
                        });
                    });
                });
            });

        });
    }


     private void ServingAction(TableData servingTableData)
    {
        if (servingTableData == null)
            return;

        if(0.5f < Mathf.Abs(servingTableData.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _actionType = WaiterActionType.None;
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(servingTableData);
        _actionType = WaiterActionType.Serving;
        _currentData = servingTableData;

        _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, servingTableData.TableFurniture.transform.position), 0, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (servingTableData.CurrentCustomer == null || servingTableData.TableState != ETableState.UseStaff)
                {
                    SetNoneState();
                    return;
                }

                _tableManager.OnServigStaff(servingTableData);
                _waiter.BowlSetActive(true);
                _waiter.Move(servingTableData.transform.position, servingTableData.SitDir, () =>
                {
                    _tweenData = Tween.Wait(((_duration * _durationMul) / speedMul) * 0.5f, () =>
                    {
                        _waiter.BowlSetAction();
                        _waiter.ShowFood(servingTableData.CurrentFood.FoodData);
                        _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                        {
                            if (servingTableData.CurrentCustomer == null || servingTableData.TableState != ETableState.StaffServing)
                            {
                                SetNoneState();
                                return;
                            }

                            _waiter.HideFood();
                            _tableManager.OnServing(servingTableData);
                            _tweenData = Tween.Wait(1 / speedMul, () =>
                            {
                                SetNoneState();
                            });
                        });
                    });
                });
            });
        });
    }

    private void OrderAction(TableData orderTableData)
    {
        if (orderTableData == null)
            return;

        if (0.5f < Mathf.Abs(orderTableData.transform.position.y - _waiter.transform.position.y) && !_notEqulsFloor)
        {
            _waiter.SetStaffState(EStaffState.None);
            _actionType = WaiterActionType.None;
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        _waiter.BowlSetActive(false);
        float speedMul = _waiter.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(orderTableData);
        _actionType = WaiterActionType.Order;
        _currentData = orderTableData;

        _waiter.Move(orderTableData.transform.position, orderTableData.SitDir, () =>
        {
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                if (orderTableData.CurrentCustomer == null || orderTableData.TableState != ETableState.UseStaff)
                {
                    SetNoneState();
                    return;
                }

                _waiter.Move(_tableManager.GetFoodPos(_waiter.EquipFloorType, RestaurantType.Hall, _waiter.transform.position), 0, () =>
                {

                    _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                    {
                        if (orderTableData.CurrentCustomer == null || orderTableData.TableState != ETableState.UseStaff)
                        {
                            SetNoneState();
                            return;
                        }

                        _waiter.SetStaffState(EStaffState.Action);

                        _tableManager.OnCustomerOrder(orderTableData);
                        _tweenData = Tween.Wait(1 / speedMul, () =>
                        {
                            SetNoneState();
                        });
                    });
                });
            });

        });
    }



    private void SetNoneState()
    {
        _waiter.SetStaffState(EStaffState.None);
        _actionType = WaiterActionType.None;
        _isUsed = false;
        _notEqulsFloor = false;
        _time = 0;
    }

    private void OnLevelUpEvent()
    {
        if (_waiter == null)
            return;

        _durationMul = 6f / _waiter.StaffData.GetSpeed(_waiter.Level);
    }
}
