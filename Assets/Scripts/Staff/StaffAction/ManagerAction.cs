public class ManagerAction : IStaffAction
{
    private TableManager _tableManager;
    public ManagerAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public void Destructor()
    {
    }

    public void PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.NotUse);

        if (index == -1)
            return;

        _tableManager.OnCustomerGuide(index);
        staff.ResetAction();

        return;
    }
}
