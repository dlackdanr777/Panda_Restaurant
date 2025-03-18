using Muks.Tween;
using UnityEngine;

public class CleanerAction : IStaffAction
{
    private TableManager _tableManager;
    private bool _isUsed = false;
    private bool _isNoAction;
    private float _time;
    private float _duration = 2f;
    private TweenData _tweenData;
    private Vector3 _cleanerPos;

    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;
        _duration = 2f;
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

        if(_time < _duration)
        {
            _time += Time.deltaTime * speedMul;
            return;
        }

        _isUsed = true;
        DropGarbageArea targetArea = _tableManager.GetMinDistanceGarbageArea(staff.EquipFloorType, staff.transform.position);

        if (targetArea == null)
        {
            _isUsed = false;
            _time = 0;
            if (_isNoAction)
                return;

            _isNoAction = true;
            if (0.1f < Vector2.Distance(staff.transform.position, _cleanerPos))
                staff.Move(_cleanerPos, -1);
        }

        else
        {
            _isNoAction = false;
            staff.Move(targetArea.transform.position, 0, () =>
            {
                if(targetArea.Count <= 0)
                {
                    _isUsed = false;
                    _time = 0;
                    return;
                }

                staff.SetStaffState(EStaffState.Action);
                _tweenData = Tween.Wait(_duration / speedMul, () =>
                {              
                    targetArea.CleanGarbage();
                    _tweenData = Tween.Wait(1 / speedMul, () =>
                    {
                        staff.SetStaffState(EStaffState.None);
                        _isUsed = false;
                        _time = 0;
                    });
                });
            });
        }

        return;
    }
}
