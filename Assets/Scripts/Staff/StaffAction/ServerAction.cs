using Muks.Tween;

public class ServerAction : IStaffAction
{
    private TableManager _tableManager;
    private TweenData _tweenData;

    public ServerAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public void Destructor()
    {
        _tweenData?.TweenStop();
    }

    public void PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.Seating);
        DebugLog.Log("½ÇÇà");
        if (index == -1)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        staff.transform.position = _tableManager.GetStaffPos(index, StaffType.Server);
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.25f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(0.5f, () =>
            {
                _tableManager.OnCustomerOrder(index);
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(staff.ResetAction);
                staff.ResetAction();
            });
        });


        return;
    }
}
