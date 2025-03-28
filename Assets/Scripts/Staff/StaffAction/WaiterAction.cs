using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;

public class WaiterAction : IStaffAction
{
    private const float _duration = 1.5f;

    private TableManager _tableManager;
    private TweenData _tweenData;


    private bool _isUsed = false;
    private bool _isNoAction = false;
    private float _time;
    private Vector3 _defaultPos;


    public WaiterAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;
        _defaultPos = _tableManager.GetStaffPos(staff.EquipFloorType, StaffType.Waiter);
        staff.transform.position = _defaultPos;
        staff.SetAlpha(1);
    }


    public void Destructor()
    {
        _tweenData?.TweenStop();
    }


    public void PerformAction(Staff staff)
    {
        if (_isUsed)
            return;

        float speedMul = staff.SpeedMul;

        if (_time < _duration)
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
        if (servingTableList.Count == 0 && orderTableList.Count == 0)
        {
            ResetStaffState(staff);
            return;
        }

        else if(0 < servingTableList.Count)
        {
            ServingAction(staff, servingTableList);
        }

        else if(0 < orderTableList.Count)
        {
            OrderAction(staff, orderTableList);
        }

        _isUsed = true;
    }


    private void ResetStaffState(Staff staff)
    {
        _isUsed = false;
        _time = 0;

        if (_isNoAction)
            return;

        _isNoAction = true;
        if (Vector2.Distance(staff.transform.position, _defaultPos) > 0.1f)
            staff.Move(_defaultPos, -1);
    }


    private void ServingAction(Staff staff, List<TableData> servingTableList)
    {
        TableData data = _tableManager.GetMinDistanceTable(staff.EquipFloorType, staff.transform.position, servingTableList);
        if (data == null)
            return;

        float speedMul = staff.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        staff.Move(_tableManager.GetFoodPos(staff.EquipFloorType, RestaurantType.Hall), 0, () =>
        {
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                    return;
                }

                staff.Move(data.transform.position, 0, () =>
                {
                    _tweenData = Tween.Wait(_duration / speedMul, () =>
                    {
                        if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                        {
                            staff.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _time = 0;
                            return;
                        }

                        staff.SetStaffState(EStaffState.Action);
                        _tableManager.OnServing(data);
                        _tweenData = Tween.Wait(1 / speedMul, () =>
                        {
                            staff.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _time = 0;
                        });
                    });
                });
            });

        });
    }

    private void OrderAction(Staff staff, List<TableData> orderTableList)
    {
        TableData data = _tableManager.GetMinDistanceTable(staff.EquipFloorType, staff.transform.position, orderTableList);
        if (data == null)
            return;

        float speedMul = staff.SpeedMul;
        _isNoAction = false;
        _tableManager.OnUseStaff(data);
        staff.Move(data.transform.position, 0, () =>
        {
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                    return;
                }

                staff.Move(_tableManager.GetFoodPos(staff.EquipFloorType, RestaurantType.Hall), 0, () =>
                {

                    _tweenData = Tween.Wait(_duration / speedMul, () =>
                    {
                        if (data.CurrentCustomer == null || data.TableState != ETableState.UseStaff)
                        {
                            staff.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _time = 0;
                            return;
                        }

                        staff.SetStaffState(EStaffState.Action);

                        _tableManager.OnCustomerOrder(data);
                        _tweenData = Tween.Wait(1 / speedMul, () =>
                        {
                            staff.SetStaffState(EStaffState.None);
                            _isUsed = false;
                            _time = 0;
                        });
                    });
                });
            });

        });
    }


    private void OldAction(Staff staff)
    {
        TableData data = _tableManager.GetTableTypeByNeedFood(staff.EquipFloorType, ETableState.CanServing);
        if (data == null)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(data);
        Vector3 pos = _tableManager.GetStaffPos(data, StaffType.Waiter);
        staff.transform.position = pos;
        ObjectPoolManager.Instance.SpawnSmokeParticle(pos + new Vector3(0, 1f, 0), Quaternion.identity).Play();
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.1f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(0.1f, () =>
            {
                _tableManager.OnServing(data);
                _tweenData = Tween.Wait(2.5f, () =>
                {
                    staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(() =>
                    {
                        staff.transform.position = Vector3.zero;
                    });
                });
            });
        });
    }
}
