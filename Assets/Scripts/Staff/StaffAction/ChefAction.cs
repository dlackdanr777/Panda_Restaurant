using Muks.Tween;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ChefAction : IStaffAction
{
    private const float _duration = 1f;

    private KitchenSystem _kitchenSystem;
    private TableManager _tableManager;
    private SinkKitchenUtensil _sink;
    private ChefData _chefData;


    private bool _isUsed = false;
    private bool _isNoAction = false;
    private bool _notEqulsFloor = false;

    private float _time;

    private Vector3 _defaultPos;
    private TweenData _tweenData;
    private Staff _staff;

    private KitchenBurnerData _burnerData;


    public ChefAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem)
    {
        if (!(staff.StaffData is ChefData))
            throw new System.Exception("쉐프 액션에 쉐프 데이터가 들어오지 않았습니다.");

        _staff = staff;
        _chefData = (ChefData)staff.StaffData;
        _tableManager = tableManager;
        _kitchenSystem = kitchenSystem;
        _sink = kitchenSystem.GetSinkKitchenUtensil(staff.EquipFloorType);

        _isUsed = false;
        _isNoAction = false;
        _notEqulsFloor = false;
        _time = 0;

        EquipStaffType type = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, staff.StaffData);
        _defaultPos = kitchenSystem.GetStaffPos(staff.EquipFloorType, type);


        staff.SetAlpha(1);
        staff.SetSpriteDir(1);
    }

    public void Destructor()
    {
        _tweenData?.TweenStop();

        if(_sink.UseStaff != null && _sink.UseStaff == _staff)
        {
            _sink.SetUseStaff(null);
            _sink.EndStaffAction();
        }

        if (_burnerData != null && _burnerData.UseStaff != null && _burnerData.UseStaff == _staff)
        {
            _burnerData.SetStaffUsable(false);
            _burnerData.SetUseStaff(null);
            _burnerData.SetAddCookSpeedMul(0);
        }
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

        if (UserInfo.IsTutorialStart)
        {
            ResetStaffState(staff);
            return;
        }

        List<KitchenBurnerData> burnerDataList = _kitchenSystem.GetCookingBurnerDataList(staff.EquipFloorType);
        int bowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, staff.EquipFloorType);
        if (burnerDataList.Count <= 0 && bowlCount <= 0)
        {
            ResetStaffState(staff);
            return;
        }

        _isUsed = true;
        if(!_sink.IsStaffWashing && !UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, staff.EquipFloorType) && _sink.UseStaff == null)
        {
            SinkAction(staff, () => UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, staff.EquipFloorType) <= 0);
            return;
        }

        DebugLog.Log(burnerDataList.Count);
        if (0 < burnerDataList.Count)
        {
            BurnerAction(burnerDataList);
            return;
        }

        if (0 < bowlCount && _sink.UseStaff == null)
        {
            SinkAction(staff, () =>
            {
                int bowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, staff.EquipFloorType);
                List<KitchenBurnerData> burnerDataList = _kitchenSystem.GetCookingBurnerDataList(staff.EquipFloorType);
                return (0 < burnerDataList.Count) || (bowlCount <= 0);
            });

            return;
        }

        ResetStaffState(staff);
        return;
    }


    private void ResetStaffState(Staff staff)
    {
        _tweenData?.TweenStop();
        _isUsed = false;
        _notEqulsFloor = false;
        _time = 0;

        if (_sink.UseStaff != null && _sink.UseStaff == _staff)
        {
            _sink.SetUseStaff(null);
            _sink.EndStaffAction();
        }

        if (_isNoAction)
            return;

        _isNoAction = true;
        if (Vector2.Distance(staff.transform.position, _defaultPos) > 0.05f)
            staff.Move(_defaultPos, 1);
    }


    private void BurnerAction(List<KitchenBurnerData> dataList)
    {
        KitchenBurnerData data = _tableManager.GetMinDistanceBurner(_staff.EquipFloorType, _staff.transform.position, dataList);
        DebugLog.Log(data);
        if (data == null)
        {
            ResetStaffState(_staff);
            return;
        }

        if (1 < Mathf.Abs(data.KitchenUtensil.transform.position.y - _staff.transform.position.y) && !_notEqulsFloor)
        {
            _staff.SetStaffState(EStaffState.None);
            _notEqulsFloor = true;
            _isUsed = false;
            return;
        }

        float speedMul = _staff.SpeedMul;
        _isNoAction = false;

        if (data.CookingData.IsDefault() || data.UseStaff != null)
        {
            _staff.SetStaffState(EStaffState.None);
            _isUsed = false;
            _notEqulsFloor = false;
            _time = 0;
            return;
        }

        data.SetStaffUsable(true);
        data.SetUseStaff(_staff);
        _burnerData = data;
        _staff.Move(data.KitchenUtensil.transform.position, 0, () =>
        {
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                if (data.CookingData.IsDefault() || (data.UseStaff != null && data.UseStaff != _staff))
                {
                    _staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _notEqulsFloor = false;
                    _time = 0;
                    return;
                }

                _staff.SetStaffState(EStaffState.Action);
                UpdateBurnerAction(data);
            });
        });
    }




    private void SinkAction(Staff staff, Func<bool> actionStopResult)
    {
        float speedMul = staff.SpeedMul;
        _sink.SetUseStaff(staff);
        staff.Move(_sink.transform.position, 0, () =>
        {
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                if (_sink.UseStaff == null && _sink.UseStaff != staff && UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, staff.EquipFloorType) <= 0)
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _notEqulsFloor = false;
                    _time = 0;
                    return;
                }

                staff.SetStaffState(EStaffState.Action);
                _sink.StartStaffAction();
                UpdateSinkAction(actionStopResult);
            });
        });
    }

    private void UpdateSinkAction(Func<bool> stopResult)
    {
        if (_sink.UseStaff == null || _sink.UseStaff != _staff || UserInfo.IsTutorialStart || stopResult())
        {
            _isNoAction = false;
            ResetStaffState(_staff);
            return;
        }

        _tweenData = Tween.Wait(0.1f, () =>
        {
            _sink.SetUseStaff(_staff);
            _sink.StartStaffAction();
            UpdateSinkAction(stopResult);
        });
    }


    private void UpdateBurnerAction(KitchenBurnerData data)
    {
        if (data.UseStaff == null)
        {
            ResetStaffState(_staff);
            return;
        }

        if (data.UseStaff != null && data.UseStaff != _staff)
        {
            ResetStaffState(_staff);
            return;
        }

        if (UserInfo.IsTutorialStart)
        {
            ResetStaffState(_staff);
            if(data.UseStaff != null && data.UseStaff == _staff)
            {
                data.SetStaffUsable(false);
                data.SetUseStaff(null);
                data.SetAddCookSpeedMul(0);
                _burnerData = null;
            }
            return;
        }

        if (data.CookingData.IsDefault())
        {
            ResetStaffState(_staff);
            if (data.UseStaff != null && data.UseStaff == _staff)
            {
                data.SetStaffUsable(false);
                data.SetUseStaff(null);
                data.SetAddCookSpeedMul(0);
                _burnerData = null;
            }

            return;
        }

        if(!_sink.IsStaffWashing && !UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, _staff.EquipFloorType) && _sink.UseStaff == null)
        {
            ResetStaffState(_staff);
            if (data.UseStaff != null && data.UseStaff == _staff)
            {
                data.SetStaffUsable(false);
                data.SetUseStaff(null);
                data.SetAddCookSpeedMul(0);
                _burnerData = null;
            }
        }

        data.SetAddCookSpeedMul(_staff.GetActionValue());
        _tweenData = Tween.Wait(1f, () =>
        {
            UpdateBurnerAction(data);
        });
    }
}
