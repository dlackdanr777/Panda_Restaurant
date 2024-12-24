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
                float speedMul = Mathf.Max(staff.SpeedMul, 0.01f); // SpeedMul이 0이 되지 않도록 최소값 설정
                float addStaffSpeedMul = Mathf.Max(GameManager.Instance.AddStaffSpeedMul, 0.01f); // AddStaffSpeedMul 최소값 설정
                float actionTime = Mathf.Max((staff.SecondValue / speedMul) / addStaffSpeedMul, 0.1f);
                _tweenData = Tween.Wait(actionTime, () =>
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
