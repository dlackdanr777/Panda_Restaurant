using System;
using Muks.Tween;
using UnityEngine;

public class GuardAction : IStaffAction
{
    private CustomerController _customerController;
    private TableManager _tableManager;
    private GatecrasherCustomer _gatecrasherCustomer;
    private float _actionTime = 1f;
    private float _actionTimer;
    private bool _startAction;
    private Vector3 _tmpScale;

    private Staff _owner;
    private long _staffLifetime;
    private ERestaurantFloorType _assignedFloor;
    private object _targetEvent;
    private long _workVersion;
    private bool _disposed;
    private TweenWait _wait;
    private object _waitIdentity;
    private TweenData _colorTween, _scaleTween;

    public GuardAction(CustomerController customerController, TableManager tableManager)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _actionTimer = _actionTime;
    }

    public void Destructor()
    {
        CancelCurrentWork();
        _disposed = true;
    }

    // Also used before the same StaffData early return and when the object is disabled.
    public void CancelCurrentWork()
    {
        ++_workVersion;
        _waitIdentity = null;
        var wait = _wait;
        _wait = null;
        if (wait != null && wait.enabled)
        {
            // TweenWait defers completion for one frame; stop that coroutine before
            // returning its component to the pool so it cannot complete a later wait.
            wait.StopAllCoroutines();
            wait.Stop();
        }
        if (_owner != null && _owner.IsActionOwner(this))
        {
            _colorTween?.Clear();
            _scaleTween?.Clear();
            if (_startAction)
            {
                _owner.SetAlpha(1f);
                _owner.SpriteRenderer.transform.localScale = _tmpScale;
            }
        }
        _colorTween = null;
        _scaleTween = null;
        _gatecrasherCustomer = null;
        _targetEvent = null;
        _startAction = false;
        _actionTimer = _actionTime;
    }

    private bool IsWorkCurrent(Staff staff, long version) => !_disposed && version == _workVersion
        && staff != null && ReferenceEquals(staff, _owner)
        && staff.IsWorkCurrent(this, _staffLifetime, _assignedFloor);

    private bool IsTargetCurrent => _gatecrasherCustomer != null
        && _gatecrasherCustomer.gameObject.activeInHierarchy && !_gatecrasherCustomer.IsEndEvent
        && !_gatecrasherCustomer.IsInDisguise && _gatecrasherCustomer.VisitFloor == _assignedFloor
        && ReferenceEquals(_targetEvent, _gatecrasherCustomer.ServiceEventIdentity)
        && ReferenceEquals(_customerController.GatecrasherCustomer[(int)_assignedFloor], _gatecrasherCustomer);

    private void Wait(Staff staff, long version, float duration, Action completed)
    {
        var identity = new object();
        _waitIdentity = identity;
        _wait = Tween.Wait(duration, () =>
        {
            if (!ReferenceEquals(_waitIdentity, identity) || !IsWorkCurrent(staff, version)) return;
            _waitIdentity = null;
            _wait = null;
            completed();
        });
    }

    private void FinishWork(Staff staff, long version)
    {
        if (!IsWorkCurrent(staff, version)) return;
        CancelCurrentWork();
    }

    public void PerformAction(Staff staff)
    {
        if (_disposed || staff == null || !staff.IsActionOwner(this) || !staff.isActiveAndEnabled) return;
        if (_startAction) return;
        float speedMul = staff.SpeedMul;
        _actionTimer -= Time.deltaTime * speedMul;
        if (_actionTimer > 0) return;
        _actionTimer = _actionTime;

        var target = _customerController.GatecrasherCustomer[(int)staff.EquipFloorType];
        if (target == null || !target.gameObject.activeInHierarchy || target.IsEndEvent
            || target.VisitFloor != staff.EquipFloorType || target.IsInDisguise) return;

        _owner = staff;
        _staffLifetime = staff.WorkLifetime;
        _assignedFloor = staff.EquipFloorType;
        _gatecrasherCustomer = target;
        _targetEvent = target.ServiceEventIdentity;
        _tmpScale = staff.SpriteRenderer.transform.localScale;
        _startAction = true;
        long version = ++_workVersion;
        Wait(staff, version, 1f / speedMul, () =>
        {
            if (!IsTargetCurrent) { FinishWork(staff, version); return; }
            _colorTween = staff.SpriteRenderer.TweenAlpha(0, .25f / speedMul).OnComplete(() =>
            {
                if (!IsWorkCurrent(staff, version)) return;
                if (!IsTargetCurrent)
                {
                    staff.transform.position = _tableManager.GetStaffPos(_assignedFloor, EquipStaffType.Guard);
                    staff.SetSpriteDir(-1);
                    _colorTween = staff.SpriteRenderer.TweenAlpha(1, .25f / speedMul)
                        .OnComplete(() => FinishWork(staff, version));
                    return;
                }
                staff.transform.position = target.transform.position + new Vector3(.2f, 0);
                staff.SetSpriteDir(-1);
                _colorTween = staff.SpriteRenderer.TweenAlpha(1, .25f / speedMul).OnComplete(() =>
                {
                    if (!IsWorkCurrent(staff, version)) return;
                    if (!IsTargetCurrent) { FinishWork(staff, version); return; }
                    EliminatingGatecrasherCustomer2(staff, version);
                });
            });
        });
    }

    private void ReturnHome(Staff staff, long version, float speedMul)
    {
        if (!IsWorkCurrent(staff, version)) return;
        _gatecrasherCustomer = null;
        _targetEvent = null;
        _colorTween = staff.SpriteRenderer.TweenAlpha(0, .3f / speedMul).OnComplete(() =>
        {
            if (!IsWorkCurrent(staff, version)) return;
            staff.transform.position = _tableManager.GetStaffPos(_assignedFloor, EquipStaffType.Guard);
            staff.SetSpriteDir(-1);
            _colorTween = staff.SpriteRenderer.TweenAlpha(1, .3f / speedMul)
                .OnComplete(() => FinishWork(staff, version));
        });
    }

    private void EliminatingGatecrasherCustomer2(Staff staff, long version)
    {
        if (!IsWorkCurrent(staff, version)) return;
        float speedMul = staff.SpeedMul;
        if (!IsTargetCurrent)
        {
            Wait(staff, version, 1f / speedMul, () => ReturnHome(staff, version, speedMul));
            return;
        }

        var target = _gatecrasherCustomer;
        float duration = (staff.GetActionValue() / staff.SpeedMul) / Mathf.Max(target.TotalTouchCount, 1);
        Wait(staff, version, duration, () =>
        {
            if (!IsTargetCurrent) { ReturnHome(staff, version, speedMul); return; }
            staff.SetSpriteDir(target.transform.position.x < staff.transform.position.x ? -1 : 1);
            target.OnTouchEvent();
            // The touch completion can synchronously replace the employee or pooled target.
            if (!IsWorkCurrent(staff, version)) return;
            if (!ReferenceEquals(_targetEvent, target.ServiceEventIdentity))
            { ReturnHome(staff, version, speedMul); return; }
            staff.transform.position = target.transform.position + new Vector3(.2f, 0);
            staff.SpriteRenderer.transform.localScale = _tmpScale;
            ObjectPoolManager.Instance.SpawnSmokeParticle(target.transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity).Play();
            _scaleTween = staff.SpriteRenderer.TweenScale(_tmpScale * .99f, duration * .5f, Ease.OutBack).OnComplete(() =>
            {
                if (!IsWorkCurrent(staff, version)) return;
                staff.SpriteRenderer.transform.localScale = _tmpScale * .99f;
                _scaleTween = staff.SpriteRenderer.TweenScale(_tmpScale, duration * .5f, Ease.OutBack);
            });
            EliminatingGatecrasherCustomer2(staff, version);
        });
    }
}
