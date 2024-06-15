using Muks.Tween;

public class WaiterAction : IStaffAction
{
    private TableManager _tableManager;
    public WaiterAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public bool PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.CanServing);

        if (index == -1)
            return false;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        staff.transform.position = _tableManager.GetStaffPos(index, StaffType.Waiter);
        staff.SpriteRenderer.TweenAlpha(1, 0.25f).OnComplete(() =>
        {
            Tween.Wait(0.5f, () =>
            {
                _tableManager.OnServing(index);
                staff.ResetAction();
                staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(staff.ResetAction);
            });
        });


        return true;
    }
}
