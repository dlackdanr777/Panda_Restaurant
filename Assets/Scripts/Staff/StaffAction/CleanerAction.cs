using Muks.Tween;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CleanerAction : IStaffAction
{
    private const float _duration = 1f;

    private TableManager _tableManager;
    private bool _isUsed = false;
    private bool _isNoAction;
    private float _time;
    private float _durationMul;
    private TweenWait _tweenData;
    private Vector3 _cleanerPos;
    private StaffCleaner _cleaner;


    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _cleaner = (StaffCleaner)staff;

        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;
        _cleanerPos = _tableManager.GetStaffPos(staff.EquipFloorType, EquipStaffType.Cleaner);
        staff.transform.position = _cleanerPos;

        _durationMul = 6f / staff.StaffData.GetSpeed(staff.Level);

        staff.SetAlpha(1);

        _cleaner.OnLevelUpEventHandler += OnLevelUpEvent;
    }

    public void Destructor()
    {
        _cleaner.OnLevelUpEventHandler -= OnLevelUpEvent;
        _tweenData?.TweenStop();
    }

    public void PerformAction(Staff staff)
    {
        if (_isUsed)
            return;

        float speedMul = staff.SpeedMul;

        if (_time < _duration * _durationMul)
        {
            _time += Time.deltaTime * speedMul;
            return;
        }

        _isUsed = true;

        Vector3 staffPos = staff.transform.position;

        // 대상 수집
        DropGarbageArea garbageArea = _tableManager.GetMinDistanceGarbageArea(staff.EquipFloorType, staffPos);
        DropCoinArea coinArea = _tableManager.GetMinDistanceCoinArea(staff.EquipFloorType, staffPos);
        TableData tableData = UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, staff.EquipFloorType) ? _tableManager.GetMinDistanceTable(
            staff.EquipFloorType,
            staffPos,
            _tableManager.GetTableDataList(staff.EquipFloorType, ETableState.NeedCleaning))
            : null;

        if (UserInfo.IsTutorialStart || (garbageArea == null && coinArea == null && tableData == null))
        {
            ResetStaffState(staff);
            return;
        }

        // 거리 계산 함수
        float GetYDist(Transform t) => Mathf.Abs(t.position.y - staffPos.y);
        float GetXDist(Transform t) => Mathf.Abs(t.position.x - staffPos.x);
        float TOLERANCE_Y = 1f;

        // 후보 리스트 구성
        var candidates = new List<(string type, Transform transform)>
    {
        ("Garbage", garbageArea?.transform),
        ("Coin", coinArea?.transform),
        ("Table", tableData?.transform)
    };

        // 1. Y 유효 거리 내 대상만 추려서 X 거리 기준 정렬
        var valid = candidates
            .Where(c => c.transform != null && GetYDist(c.transform) <= TOLERANCE_Y)
            .Select(c => (c.type, dist: GetXDist(c.transform)))
            .ToList();

        string selectedType = null;

        if (valid.Count > 0)
        {
            selectedType = valid.OrderBy(c => c.dist).First().type;
        }
        else
        {
            // 2. Y 거리 가장 작은 값 찾기
            float garbageY = garbageArea ? GetYDist(garbageArea.transform) : float.MaxValue;
            float coinY = coinArea ? GetYDist(coinArea.transform) : float.MaxValue;
            float tableY = tableData ? GetYDist(tableData.transform) : float.MaxValue;

            float minY = Mathf.Min(garbageY, coinY, tableY);

            // 3. 오차 범위 내(Y 차이 ±1) 후보 추림
            var yMinCandidates = new List<(string type, Transform transform)>
        {
            (Mathf.Abs(garbageY - minY) <= TOLERANCE_Y && garbageArea != null ? "Garbage" : null, garbageArea?.transform),
            (Mathf.Abs(coinY - minY) <= TOLERANCE_Y && coinArea != null ? "Coin" : null, coinArea?.transform),
            (Mathf.Abs(tableY - minY) <= TOLERANCE_Y && tableData != null ? "Table" : null, tableData?.transform),
        }.Where(c => c.type != null).ToList();

            // 4. 문 위치와의 거리 기준으로 선택
            selectedType = yMinCandidates
                .OrderBy(c => Vector3.Distance(_tableManager.GetDoorPos(RestaurantType.Hall, c.transform.position), c.transform.position))
                .First().type;
        }

        // 최종 대상에 맞는 액션 수행
        switch (selectedType)
        {
            case "Garbage":
                if (garbageArea != null)
                    CleanGarbageAction(staff, garbageArea);
                break;
            case "Coin":
                if (coinArea != null)
                    GiveCoinAction(staff, coinArea);
                break;
            case "Table":
                if (tableData != null)
                    CleanTableAction(staff, tableData);
                break;
            default:
                ResetStaffState(staff);
                break;
        }
    }




    private void ResetStaffState(Staff staff)
    {
        _isUsed = false;
        _time = 0;

        if (_isNoAction)
            return;

        _isNoAction = true;
        if (Vector2.Distance(staff.transform.position, _cleanerPos) > 0.1f)
            staff.Move(_cleanerPos, -1);
    }


    private void CleanGarbageAction(Staff staff, DropGarbageArea area)
    {
        float speedMul = staff.SpeedMul;
        _isNoAction = false;
        staff.Move(area.transform.position, 0, () =>
        {
            if (UserInfo.IsTutorialStart|| area.Count <= 0)
            {
                ResetStaffState(staff);
                return;
            }

            staff.SetStaffState(EStaffState.Action);
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                area.CleanGarbage();
                _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                });
            });
        });
    }

    private void GiveCoinAction(Staff staff, DropCoinArea area)
    {
        float speedMul = staff.SpeedMul;
        _isNoAction = false;
        staff.Move(area.transform.position, 0, () =>
        {
            if (UserInfo.IsTutorialStart || area.Count <= 0)
            {
                ResetStaffState(staff);
                return;
            }

            staff.SetStaffState(EStaffState.Action);
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                area.GiveCoin();
                _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                });
            });
        });
    }


    private void CleanTableAction(Staff staff, TableData data)
    {
        float speedMul = staff.SpeedMul;
        _isNoAction = false;
        staff.Move(data.transform.position, 0, () =>
        {
            if (UserInfo.IsTutorialStart || data.TableState != ETableState.NeedCleaning)
            {
                ResetStaffState(staff);
                return;
            }

            staff.SetStaffState(EStaffState.Action);
            _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
            {
                data.TableFurniture.OnCleanAction();
                _tweenData = Tween.Wait((_duration * _durationMul) / speedMul, () =>
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                });
            });
        });
    }

    private void OnLevelUpEvent()
    {
        if(_cleaner == null)
            return;

        _durationMul = 6f / _cleaner.StaffData.GetSpeed(_cleaner.Level);
    }
}
