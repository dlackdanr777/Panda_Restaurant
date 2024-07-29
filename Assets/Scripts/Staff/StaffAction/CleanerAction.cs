using Muks.Tween;
using UnityEngine;

public class CleanerAction : IStaffAction
{
    private TableManager _tableManager;
    private bool _isUsed = false;

    public CleanerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _isUsed = false;

        staff.transform.position = _tableManager.CleanerWaitTr.position;
        staff.SetAlpha(1);
    }

    public bool PerformAction(Staff staff)
    {
        if (_isUsed)
            return false;


        Vector3 targetPos = _tableManager.GetMinDistanceGarbageAreaPos(staff.transform.position);
        if (Vector3.Distance(targetPos, _tableManager.CleanerWaitTr.position) < 0.1f)
            return true;

        _isUsed = true;
        DropGarbageArea targetArea = _tableManager.GetMinDistanceGarbageArea(staff.transform.position);
        staff.Move(targetPos, 0, () =>
        {
            Tween.Wait(0.5f, () =>
            {
                targetArea.CleanGarbage();
                Tween.Wait(1, () =>
                {
                    _isUsed = false;
                });
            });
        });

        return true;
    }
}
