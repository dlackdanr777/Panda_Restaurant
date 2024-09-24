
public class GuardAction : IStaffAction
{
    private CustomerController _customerController;
    public GuardAction(CustomerController customerController)
    {
        _customerController = customerController;
    }

    public bool PerformAction(Staff staff)
    {
        return true;
    }
}
