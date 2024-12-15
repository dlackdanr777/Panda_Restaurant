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


    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;
        _duration = staff.SecondValue;

        staff.transform.position = _tableManager.CleanerWaitTr.position;
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


        if(_time < _duration)
        {
            _time += Time.deltaTime;
            return;
        }

        _isUsed = true;
        DropGarbageArea targetArea = _tableManager.GetMinDistanceGarbageArea(staff.transform.position);

        if (targetArea == null)
        {
            _isUsed = false;
            _time = 0;
            if (_isNoAction)
                return;

            _isNoAction = true;
            if (0.1f < Vector2.Distance(staff.transform.position, _tableManager.CleanerWaitTr.position))
                staff.Move(_tableManager.CleanerWaitTr.position, -1);
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
                _tweenData = Tween.Wait(staff.SecondValue / staff.SpeedMul, () =>
                {              
                    targetArea.CleanGarbage();
                    _tweenData = Tween.Wait(1, () =>
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
