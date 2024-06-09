using Muks.Tween;

public class CleanerAction : IStaffAction
{
    private TableManager _tableManager;
    public CleanerAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public bool PerformAction(Staff staff)
    {
        int index = _tableManager.GetTableType(ETableState.NeedCleaning);

        if (index == -1)
            return false;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        staff.transform.position = _tableManager.GetTablePos(index);
        staff.SpriteRenderer.TweenAlpha(1, 0.25f).OnComplete(() =>
        {
            Tween.Wait(0.5f, () =>
            {
                staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(staff.ResetAction);
                staff.ResetAction();
                _tableManager.OnCleanTable(index);
            });
        });


        return true;
    }
}
