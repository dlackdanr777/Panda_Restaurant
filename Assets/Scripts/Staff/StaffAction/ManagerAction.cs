using UnityEngine;

public class ManagerAction : IStaffAction
{
    private TableManager _tableManager;
    private float _actionCoolTime = 0;

    public ManagerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _actionCoolTime = staff.GetActionValue();
    }

    public void Destructor()
    {
    }

    public void PerformAction(Staff staff)
    {
        DebugLog.Log("액션 실행중: " + _actionCoolTime);
        if (_actionCoolTime <= 0)
        {
            _tableManager.OnCustomerGuideEvent();
            _actionCoolTime = staff.GetActionValue();
            DebugLog.Log("매니저 실행: " + _actionCoolTime);

        }
        else
        {
            _actionCoolTime -= Time.deltaTime * staff.SpeedMul;
        }
    }
}
