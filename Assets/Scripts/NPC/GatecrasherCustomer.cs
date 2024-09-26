using Muks.PathFinding.AStar;
using Muks.Tween;
using System.Collections;
using UnityEngine;

public class GatecrasherCustomer : Customer
{
    [Space]
    [Header("GatecrasherCustomer Components")]
    [SerializeField] private PointerClickSpriteRenderer _pointerClickSpriteRenderer;
    [SerializeField] private SpriteRendererGroup _spriteGroup;
    [SerializeField] private SpriteFillAmount _spriteFillAmount;


    [Space]
    [Header("GatecrasherCustomer2 Option")]
    [SerializeField] private ParticleSystem _soundParticle;

    private int _activeDuration;
    private int _touchCount;
    private int _totalTouchCount;
    private bool _isEndEvent;
    private bool _touchEnabled;
    private Coroutine _coroutine;

    public override void SetData(CustomerData data)
    {
        if(!(data is GatecrasherCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 GatecrasherCustomerData만 받을 수 있습니다.");
            return;
        }

        base.SetData(data);
        GatecrasherCustomerData gatecrasherData = (GatecrasherCustomerData)data;
        _activeDuration = gatecrasherData.ActiveDuration;
        _touchCount = 0;
        _totalTouchCount = gatecrasherData.TouchCount;
        _isEndEvent = false;
        _touchEnabled = false;
        _spriteGroup.SetAlpha(0);

        _soundParticle.Stop();
        _pointerClickSpriteRenderer.RemoveAllEvent();
        _pointerClickSpriteRenderer.AddEvent(OnTouchEvent);

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(OnEndTimeEvent());
    }

    public void StartEvent(Vector3 targetPos)
    {
        _touchEnabled = false;
        Move(targetPos, -1, () =>
        {
            Tween.Wait(1f, () =>
            {
                _touchEnabled = true;
                ChangeState(CustomerState.Action);
                _soundParticle.Play();
            });
        });
    }


    private void OnTouchEvent()
    {
        if (_isEndEvent || !_touchEnabled)
            return;

        if (_customerData == null)
            return;

        _touchCount++;
        _spriteGroup.SetAlpha(1);
        _spriteFillAmount.SetFillAmount((float)_touchCount / _totalTouchCount);

        if (_totalTouchCount <= _touchCount)
        {
            _isEndEvent = true;
            if (_coroutine != null)
                StopCoroutine(_coroutine);

            _spriteGroup.SetAlpha(0);
            StopMove();

            _spriteRenderer.TweenAlpha(0, 0.5f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
            return;
        }
    }


    private IEnumerator OnEndTimeEvent()
    {
        yield return YieldCache.WaitForSeconds(_activeDuration);

        if (_isEndEvent)
            yield break;

        _isEndEvent = true;
        _spriteGroup.SetAlpha(0);
        StopMove();
        _spriteRenderer.TweenAlpha(0, 0.5f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
    }
}
