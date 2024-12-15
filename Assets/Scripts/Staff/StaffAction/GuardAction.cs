
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
    private TweenData _tweenData;
    private TweenData _guardSpriteTweenData;
    private Vector3 _tmpScale;

    public GuardAction(CustomerController customerController, TableManager tableManager)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _actionTimer = _actionTime;
    }


    public void Destructor()
    {
        _tweenData?.TweenStop();
        _guardSpriteTweenData?.TweenStop();

    }


    public void PerformAction(Staff staff)
    {
        if (_startAction)
            return;

        _actionTimer -= Time.deltaTime;

        if (_actionTimer < 0)
            return;

        _actionTimer = _actionTime;

        if(_gatecrasherCustomer == null)
        {
            if (_customerController.GatecrasherCustomer == null)
                return;

            if (_customerController.GatecrasherCustomer.CustomerData is GatecrasherCustomer1Data)
            {
                _gatecrasherCustomer = _customerController.GatecrasherCustomer;
                return;
            }
        }

        else
        {
            if(_gatecrasherCustomer.IsEndEvent)
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
            _tweenData = Tween.Wait(1, () =>
            {
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 0.3f).OnComplete(() =>
                {
                    staff.transform.position = _gatecrasherCustomer.transform.position + new Vector3(0.5f, 0);
                    staff.SetSpriteDir(-1);
                    _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.3f).OnComplete(() =>
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
        if (_gatecrasherCustomer == null)
        {
            _tweenData = Tween.Wait(1f, () =>
            {
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 0.3f).OnComplete(() =>
                {
                    staff.transform.position = _tableManager.GetStaffPos(0, StaffType.Guard);
                    staff.SetSpriteDir(-1);
                    _tweenData = staff.SpriteRenderer.TweenAlpha(1, 0.3f).OnComplete(() =>
                    {
                        _startAction = false;
                    });
                });
            });
            return;
        }


        if (_gatecrasherCustomer.IsEndEvent)
        {
            _gatecrasherCustomer = null;
            _tweenData = Tween.Wait(1f, () =>
            {
                _tweenData = staff.SpriteRenderer.TweenAlpha(0, 1).OnComplete(() =>
                {
                    staff.transform.position = _tableManager.GetStaffPos(0, StaffType.Guard);
                    staff.SetSpriteDir(-1);
                    _tweenData = staff.SpriteRenderer.TweenAlpha(1, 1).OnComplete(() =>
                    {
                        _startAction = false;
                    });
                });
            });
            return;
        }

        float duration = staff.GetActionValue() / _gatecrasherCustomer.TotalTouchCount;
        _tweenData = Tween.Wait(duration, () =>
        {
            staff.SetSpriteDir(_gatecrasherCustomer.transform.position.x < staff.transform.position.x ? -1 : 1);
            _gatecrasherCustomer.OnTouchEvent();

            staff.SpriteRenderer.transform.localScale = _tmpScale;
            ObjectPoolManager.Instance.SpawnSmokeParticle(_gatecrasherCustomer.transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity).Play();
            _guardSpriteTweenData = staff.SpriteRenderer.TweenScale(_tmpScale * 0.99f, duration * 0.5f, Ease.OutBack).OnComplete(() =>
            {
                staff.SpriteRenderer.transform.localScale = _tmpScale * 0.99f;
                _guardSpriteTweenData = staff.SpriteRenderer.TweenScale(_tmpScale, duration * 0.5f, Ease.OutBack);
            });
            EliminatingGatecrasherCustomer2(staff);
        });
    }
}
