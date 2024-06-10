public class ManagerAction : IStaffAction
{
    private TableManager _tableManager;
    public ManagerAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public bool PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.NotUse);

        if (index == -1)
            return false;

        _tableManager.OnCustomerGuide(index);
        staff.ResetAction();

        return true;
    }
}
