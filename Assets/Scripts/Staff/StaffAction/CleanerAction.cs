using Muks.Tween;
using UnityEngine;

public class CleanerAction : IStaffAction
{
    private TableManager _tableManager;
    private bool _isUsed = false;
    private bool _isNoAction;
    private float _time;
    private float _duration = 1f;
    private TweenData _tweenData;
    private Vector3 _cleanerPos;

    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;
        _duration = 1f;
        _cleanerPos = _tableManager.GetStaffPos(staff.EquipFloorType, StaffType.Cleaner);
        staff.transform.position = _cleanerPos;
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

        _isUsed = true;
        DropGarbageArea garbageArea = _tableManager.GetMinDistanceGarbageArea(staff.EquipFloorType, staff.transform.position);
        DropCoinArea coinArea = _tableManager.GetMinDistanceCoinArea(staff.EquipFloorType, staff.transform.position);

        if (UserInfo.IsTutorialStart || (garbageArea == null && coinArea == null))
        {
            ResetStaffState(staff);
            return;
        }

        // 위치 정보 계산
        Vector3 staffPos = staff.transform.position;
        float garbageDistanceY = garbageArea != null ? Mathf.Abs(garbageArea.transform.position.y - staffPos.y) : float.MaxValue;
        float coinDistanceY = coinArea != null ? Mathf.Abs(coinArea.transform.position.y - staffPos.y) : float.MaxValue;
        float garbageDistanceX = garbageArea != null ? Mathf.Abs(garbageArea.transform.position.x - staffPos.x) : float.MaxValue;
        float coinDistanceX = coinArea != null ? Mathf.Abs(coinArea.transform.position.x - staffPos.x) : float.MaxValue;

        bool isGarbageValid = garbageDistanceY <= 1;
        bool isCoinValid = coinDistanceY <= 1;

        // 가장 가까운 유효한 장소 선택
        DropGarbageArea selectedGarbageArea = null;
        DropCoinArea selectedCoinArea = null;

        if (isGarbageValid && isCoinValid)
        {
            if (garbageDistanceX < coinDistanceX)
                selectedGarbageArea = garbageArea;
            else
                selectedCoinArea = coinArea;
        }
        else if (isGarbageValid)
        {
            selectedGarbageArea = garbageArea;
        }
        else if (isCoinValid)
        {
            selectedCoinArea = coinArea;
        }
        else
        {
            // 둘 다 Y 범위를 벗어났을 경우 X 거리 비교
            if (garbageDistanceX < coinDistanceX)
                selectedGarbageArea = garbageArea;
            else
                selectedCoinArea = coinArea;
        }

        // 선택된 장소에 맞는 액션 실행
        if (selectedGarbageArea != null)
            CleanGarbageAction(staff, selectedGarbageArea);
        else if (selectedCoinArea != null)
            GiveCoinAction(staff, selectedCoinArea);
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
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                area.CleanGarbage();
                _tweenData = Tween.Wait(1 / speedMul, () =>
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
            _tweenData = Tween.Wait(_duration / speedMul, () =>
            {
                area.GiveCoin();
                _tweenData = Tween.Wait(1 / speedMul, () =>
                {
                    staff.SetStaffState(EStaffState.None);
                    _isUsed = false;
                    _time = 0;
                });
            });
        });
    }
}
