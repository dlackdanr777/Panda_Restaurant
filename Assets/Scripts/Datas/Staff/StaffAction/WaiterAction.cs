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
        staff.transform.position = _tableManager.GetTablePos(index);
        staff.SpriteRenderer.TweenAlpha(1, 0.5f).OnComplete(() =>
        {
            Tween.Wait(0.5f, () =>
            {
                staff.SpriteRenderer.TweenAlpha(0, 0.5f).OnComplete(staff.ResetAction);
                staff.ResetAction();
            });
        });

        _tableManager.OnServing(index);
        return true;
    }
}
