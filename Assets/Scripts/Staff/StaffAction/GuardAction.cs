
using Muks.Tween;
using UnityEngine;

public class GuardAction : IStaffAction
{
    private CustomerController _customerController;
    private TableManager _tableManager;
    private GatecrasherCustomer _gatecrasherCustomer;

    private float _actionTime = 1f;
    private float _actionTimer = 0;
    private bool _startAction;
    private Vector3 _tmpScale;

    public GuardAction(CustomerController customerController, TableManager tableManager)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _actionTimer = _actionTime;
        _startAction = false;
    }


    public void Destructor()
    {
    }


    public void PerformAction(Staff staff)
    {
        if (_startAction)
            return;

        float speedMul = staff.SpeedMul;
        _actionTimer -= Time.deltaTime * speedMul;
        if (0 < _actionTimer)
            return;

        _actionTimer = _actionTime;

        if(_gatecrasherCustomer == null || !_gatecrasherCustomer.gameObject.activeInHierarchy)
        {
            if (_customerController.GatecrasherCustomer[(int)staff.EquipFloorType] == null)
                return;

            if (_customerController.GatecrasherCustomer[(int)staff.EquipFloorType].CustomerData is GatecrasherCustomer1Data)
            {
                _gatecrasherCustomer = _customerController.GatecrasherCustomer[(int)staff.EquipFloorType];
                return;
            }
        }

        else
        {

            if (_gatecrasherCustomer.IsEndEvent)
            {
                _gatecrasherCustomer = null;
                _startAction = false;
                return;
            }

            if (_gatecrasherCustomer.IsInDisguise)
            {
                _startAction = false;
                return;
            }

            _startAction = true;
            Tween.Wait(1 / speedMul, () =>
            {
                if (!staff.gameObject.activeInHierarchy)
                    return;

                staff.SpriteRenderer.TweenAlpha(0, 0.25f / speedMul).OnComplete(() =>
                {
                    staff.transform.position = _gatecrasherCustomer.transform.position + new Vector3(0.2f, 0);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 0.25f / speedMul).OnComplete(() =>
                    {
                        if (_gatecrasherCustomer.IsEndEvent)
                            return;

                        _tmpScale = staff.SpriteRenderer.transform.localScale;
                        EliminatingGatecrasherCustomer2(staff);
                    });
                });

            });
        }

        return;
    }


    private void EliminatingGatecrasherCustomer2(Staff staff)
    {
        float speedMul = staff.SpeedMul;
        if (_gatecrasherCustomer == null)
        {
            Tween.Wait(1f / speedMul, () =>
            {
                if (!staff.gameObject.activeInHierarchy)
                    return;

                staff.SpriteRenderer.TweenAlpha(0, 0.3f / speedMul).OnComplete(() =>
                {
                    staff.transform.position = _tableManager.GetStaffPos(0, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 0.3f / speedMul).OnComplete(() =>
                    {
                        _startAction = false;
                    });
                });
            });
            return;
        }


        else if (_gatecrasherCustomer.IsEndEvent)
        {
            _gatecrasherCustomer = null;
            Tween.Wait(1f, () =>
            {
                if (!staff.gameObject.activeInHierarchy)
                    return;

                staff.SpriteRenderer.TweenAlpha(0, 1 / speedMul).OnComplete(() =>
                {
                    staff.transform.position = _tableManager.GetStaffPos(0, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    staff.SpriteRenderer.TweenAlpha(1, 1 / speedMul).OnComplete(() =>
                    {
                        _startAction = false;
                    });
                });
            });
            return;
        }

        float duration = (staff.GetActionValue() / staff.SpeedMul) / Mathf.Max(_gatecrasherCustomer.TotalTouchCount, 1);
        Tween.Wait(duration, () =>
        {
            if (!staff.gameObject.activeInHierarchy)
                return;

            staff.SetSpriteDir(_gatecrasherCustomer.transform.position.x < staff.transform.position.x ? -1 : 1);
            _gatecrasherCustomer.OnTouchEvent();
            staff.transform.position = _gatecrasherCustomer.transform.position + new Vector3(0.2f, 0);
            staff.SpriteRenderer.transform.localScale = _tmpScale;
            ObjectPoolManager.Instance.SpawnSmokeParticle(_gatecrasherCustomer.transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity).Play();
            staff.SpriteRenderer.TweenScale(_tmpScale * 0.99f, duration * 0.5f, Ease.OutBack).OnComplete(() =>
            {
                staff.SpriteRenderer.transform.localScale = _tmpScale * 0.99f;
                staff.SpriteRenderer.TweenScale(_tmpScale, duration * 0.5f, Ease.OutBack);
            });
            EliminatingGatecrasherCustomer2(staff);
        });
    }
}
