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
    }
}
