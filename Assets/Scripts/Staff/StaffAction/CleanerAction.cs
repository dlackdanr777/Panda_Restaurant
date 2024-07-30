using Muks.Tween;
using UnityEngine;

public class CleanerAction : IStaffAction
{
    private TableManager _tableManager;
    private bool _isUsed = false;
    private bool _isNoAction;
    private float _time;
    private float _duration = 2f;


    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;
        _isNoAction = false;
        _time = 0;

        staff.transform.position = _tableManager.CleanerWaitTr.position;
        staff.SetAlpha(1);
    }

    public bool PerformAction(Staff staff)
    {
        if (_isUsed)
            return false;


        if(_time < _duration)
        {
            _time += Time.deltaTime;
            return false;
        }


        _isUsed = true;
        DropGarbageArea targetArea = _tableManager.GetMinDistanceGarbageArea(staff.transform.position);

        if (targetArea == null)
        {
            _isUsed = false;
            _time = 0;
            if (_isNoAction)
                return false;

            _isNoAction = true;
            if (0.1f < Vector2.Distance(staff.transform.position, _tableManager.CleanerWaitTr.position))
                staff.Move(_tableManager.CleanerWaitTr.position, -1);
        }

        else
        {
            _isNoAction = false;
            staff.Move(targetArea.transform.position, 0, () =>
            {
                Tween.Wait(1f, () =>
                {
                    targetArea.CleanGarbage();
                    Tween.Wait(1, () =>
                    {
                        _isUsed = false;
                        _time = 0;
                    });
                });
            });
        }

        return true;
    }
}
