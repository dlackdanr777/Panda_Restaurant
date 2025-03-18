using Muks.Tween;

public class ChefAction : IStaffAction
{
    private KitchenSystem _kitchenSystem;
    public ChefAction(KitchenSystem kitchenSystem)
    {
        _kitchenSystem = kitchenSystem;
    }

    public void Destructor()
    {
    }

    public void PerformAction(Staff staff)
    {
    }
}
