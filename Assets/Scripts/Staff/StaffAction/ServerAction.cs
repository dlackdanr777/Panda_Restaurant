using Muks.Tween;
using UnityEngine;

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
        int index = _tableManager.GetTableTypeByNeedFood(ETableState.Seating);
        if (index == -1)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(index);
        Vector3 pos = _tableManager.GetStaffPos(index, StaffType.Server);
        staff.transform.position = pos;
        ObjectPoolManager.Instance.SpawnSmokeParticle(pos + new Vector3(0, 1f, 0), Quaternion.identity).Play();
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.1f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(1f, () =>
            {
                _tableManager.OnCustomerOrder(index);
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(() =>
                {
                    staff.ResetAction();
                    staff.transform.position = Vector3.zero;
                });

            });
        });


        return;
    }
}
