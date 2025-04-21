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
        if (_actionCoolTime <= 0)
        {
            _tableManager.OnCustomerGuideEventPlaySound();
            _actionCoolTime = staff.GetActionValue();
        }
        else
        {
            _actionCoolTime -= Time.deltaTime * staff.SpeedMul;
        }
    }
}
