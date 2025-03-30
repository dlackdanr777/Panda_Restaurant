using Muks.Tween;
using UnityEngine;

public class WaiterTestAction : IStaffAction
{
    private TableManager _tableManager;
    private TweenData _tweenData;

    public WaiterTestAction(TableManager tableManager)
    {
        _tableManager = tableManager;
    }

    public void Destructor()
    {
        _tweenData?.TweenStop();
    }

    public void PerformAction(Staff staff)
    {
        TableData data = _tableManager.GetTableTypeByNeedFood(staff.EquipFloorType, ETableState.CanServing);
        if (data == null)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(data);
        Vector3 pos = _tableManager.GetStaffPos(data, EquipStaffType.Waiter1);
        staff.transform.position = pos;
        ObjectPoolManager.Instance.SpawnSmokeParticle(pos + new Vector3(0, 1f, 0), Quaternion.identity).Play();
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.1f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(0.1f, () =>
            {
                _tableManager.OnServing(data);
                _tweenData = Tween.Wait(2.5f, () =>
                {
                    staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(() =>
                    {
                        staff.transform.position = Vector3.zero;
                    });
                });
            });
        });
    }


    private void ServingAction(Staff staff)
    {
        TableData data = _tableManager.GetTableTypeByNeedFood(staff.EquipFloorType, ETableState.CanServing);
        if (data == null)
            return;

        staff.SetAlpha(0);
        staff.SetStaffState(EStaffState.Used);
        _tableManager.OnUseStaff(data);
        Vector3 pos = _tableManager.GetStaffPos(data, EquipStaffType.Waiter1);
        staff.transform.position = pos;
        ObjectPoolManager.Instance.SpawnSmokeParticle(pos + new Vector3(0, 1f, 0), Quaternion.identity).Play();
        _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.1f).OnComplete(() =>
        {
            _tweenData = Tween.Wait(0.1f, () =>
            {
                _tableManager.OnServing(data);
                _tweenData = Tween.Wait(2.5f, () =>
                {
                    staff.SpriteRenderer.TweenAlpha(0, 0.25f).OnComplete(() =>
                    {
                        staff.transform.position = Vector3.zero;
                    });
                });
            });
        });
    }

    private float GetSpeedMul(Staff staff)
    {
        return staff.SpeedMul * GameManager.Instance.AddStaffSpeedMul;
    }
}
