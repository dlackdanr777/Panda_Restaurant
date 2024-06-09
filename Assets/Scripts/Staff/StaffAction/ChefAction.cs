using Muks.Tween;

public class ChefAction : IStaffAction
{
    private KitchenSystem _kitchenSystem;
    public ChefAction(KitchenSystem kitchenSystem)
    {
        _kitchenSystem = kitchenSystem;
    }

    public bool PerformAction(Staff staff)
    {
        staff.ResetAction();
        return true;
    }
}
