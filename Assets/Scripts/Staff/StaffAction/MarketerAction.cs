using Muks.Tween;

public class MarketerAction : IStaffAction
{
    private CustomerController _customerController;
    public MarketerAction(CustomerController customerController)
    {
        _customerController = customerController;
    }

    public bool PerformAction(Staff staff)
    {
        _customerController.AddCustomer();
        staff.ResetAction();
        return true;
    }
}
