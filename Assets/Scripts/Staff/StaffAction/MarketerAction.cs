using UnityEngine;

public class MarketerAction : IStaffAction
{
    private CustomerController _customerController;
    private float _actionCoolTime = 0;


    public MarketerAction(Staff staff, CustomerController customerController)
    {
        _customerController = customerController;
        _actionCoolTime = staff.GetActionValue();
    }

    public void Destructor()
    {

    }

    public void PerformAction(Staff staff)
    {
        if (_actionCoolTime <= 0)
        {
            _customerController.AddCustomerButtonClickEvent();
            _actionCoolTime = staff.GetActionValue();
        }
        else
        {
            _actionCoolTime -= Time.deltaTime * staff.SpeedMul;
        }
    }
}
