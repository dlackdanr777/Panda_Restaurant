using Muks.Tween;

public class WaiterAction : IStaffAction
{
    private TableManager _tableManager;
    private TweenData _tweenData;

    public WaiterAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public void Destructor()
    {
        _tweenData?.TweenStop();
    }

    public void PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.CanServing);

        if (index == -1)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        staff.transform.position = _tableManager.GetStaffPos(index, StaffType.Waiter);
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.25f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(0.5f, () =>
            {
                _tableManager.OnServing(index);
                staff.ResetAction();
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(staff.ResetAction);
            });
        });
    }
}
