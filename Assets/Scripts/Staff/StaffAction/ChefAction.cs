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
    private TweenWait _tweenData;
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
        _tweenData = null; // 중요: null로 설정
        
        _isUsed = false;
        _notEqulsFloor = false;
        _time = 0;

        if (_sink != null && _sink.UseStaff == _staff)
        {
            _sink.SetUseStaff(null);
            _sink.EndStaffAction();
        }

        // 현재 버너 데이터 정리 - 별도 메서드 사용
        if (_burnerData != null)
        {
            CleanupBurnerData(_burnerData);
        }

        if (_isNoAction)
            return;

        _isNoAction = true;
        if (staff != null && _defaultPos != null && Vector2.Distance(staff.transform.position, _defaultPos) > 0.05f)
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
        // 이미 null 체크
        if (data == null || data.UseStaff == null || data.UseStaff != _staff)
        {
            ResetStaffState(_staff);
            return;
        }

        // 튜토리얼 확인
        if (UserInfo.IsTutorialStart)
        {
            CleanupBurnerData(data);
            ResetStaffState(_staff);
            return;
        }

        // 요리 데이터 유효성 확인
        if (data.CookingData.IsDefault())
        {
            CleanupBurnerData(data);
            ResetStaffState(_staff);
            return;
        }

        // 싱크대 조건 확인 - 주의: !_sink.IsStaffWashing 조건은 싱크대로 전환해야 할 수 있음
        if (!_sink.IsStaffWashing && !UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, _staff.EquipFloorType) && _sink.UseStaff == null)
        {
            // 여기서 싱크대로 이동해야 할 수 있음 - 단순히 리셋만 하지 말고 다음 행동 결정
            bool shouldGoToSink = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _staff.EquipFloorType) > 0;
            
            CleanupBurnerData(data);
            
            if (shouldGoToSink) {
                // 싱크대로 전환 (재귀 호출 대신)
                _tweenData?.TweenStop();
                _isUsed = false;
                _notEqulsFloor = false;
                _time = 0;
                
                SinkAction(_staff, () => {
                    int bowlCount = UserInfo.GetSinkBowlCount(UserInfo.CurrentStage, _staff.EquipFloorType);
                    List<KitchenBurnerData> burnerDataList = _kitchenSystem.GetCookingBurnerDataList(_staff.EquipFloorType);
                    return (0 < burnerDataList.Count) || (bowlCount <= 0);
                });
                return;
            } else {
                ResetStaffState(_staff);
                return;
            }
        }

        // 요리 속도 증가 적용
        data.SetAddCookSpeedMul(_staff.GetActionValue());
        
        // 다음 업데이트까지 약간의 지연
        _tweenData = Tween.Wait(0.5f, () => {
            // 상태 변수 명시적 검사 추가
            if (_isUsed && _staff != null && !_isNoAction) {
                UpdateBurnerAction(data);
            } else {
                // 비정상 상태 감지 - 정리
                CleanupBurnerData(data);
                ResetStaffState(_staff);
            }
        });
    }

    // 버너 데이터 정리를 위한 헬퍼 메서드 추가
    private void CleanupBurnerData(KitchenBurnerData data)
    {
        if (data != null && data.UseStaff == _staff)
        {
            data.SetStaffUsable(false);
            data.SetUseStaff(null);
            data.SetAddCookSpeedMul(0);
            
            // _burnerData 초기화 추가
            if (_burnerData == data)
            {
                _burnerData = null;
            }
        }
    }
}
