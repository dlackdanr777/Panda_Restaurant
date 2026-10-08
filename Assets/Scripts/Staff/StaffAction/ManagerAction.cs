using UnityEngine;

public class ManagerAction : IStaffAction
{
    private TableManager _tableManager;
    private float _actionCoolTime = 0;
    private readonly Staff _owner;
    private readonly ERestaurantFloorType _assignedFloor;
    private bool _disposed;

    public ManagerAction(Staff staff, TableManager tableManager)
    {
        _tableManager = tableManager;
        _owner = staff;
        _assignedFloor = staff.EquipFloorType;
        _actionCoolTime = staff.GetActionValue();
    }

    public void Destructor()
    {
        _disposed = true;
    }

    public void PerformAction(Staff staff)
    {
        if (_disposed || staff != _owner || staff == null || !staff.isActiveAndEnabled
            || staff.StaffData == null || staff.EquipFloorType != _assignedFloor)
            return;

        if (_actionCoolTime <= 0)
        {
            _tableManager.OnManagerCustomerGuideEvent(staff, ManagerCustomerGuideSource.Action, true);
            _actionCoolTime = staff.GetActionValue();
        }
        else
        {
            float existingActionMultiplier = staff.SpeedMul;
            float feverRoleMultiplier =
                GameManager.Instance.FeverRuntimeContext.ManagerGuideMultiplier;
            float finalActionMultiplier =
                FeverRuntimeMultiplierCalculator.CalculateRoleActionMultiplier(
                    existingActionMultiplier,
                    feverRoleMultiplier);
            _actionCoolTime -= Time.deltaTime * finalActionMultiplier;
        }
    }
}
