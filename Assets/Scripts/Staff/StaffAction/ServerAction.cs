using Muks.Tween;

public class ServerAction : IStaffAction
{
    private TableManager _tableManager;
    public ServerAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public bool PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.Seating);

        if (index == -1)
            return false;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        staff.transform.position = _tableManager.GetStaffPos(index, StaffType.Server);
        staff.SpriteRenderer.TweenAlpha(1, 0.25f).OnComplete(() =>
        {
            Tween.Wait(0.5f, () =>
            {
                _tableManager.OnCustomerOrder(index);
                staff.ResetAction();
                staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(staff.ResetAction);
            });
        });


        return true;
    }
}
