
using Random = UnityEngine.Random;

public class MarketerAction : IStaffAction
{
    private CustomerController _customerController;
    public MarketerAction(CustomerController customerController)
    {
        _customerController = customerController;
    }

    public void Destructor()
    {

    }

    public void PerformAction(Staff staff)
    {
        if(Random.Range(0f, 100f) < staff.SecondValue)
            _customerController.AddCustomer();

        staff.ResetAction();
    }
}
