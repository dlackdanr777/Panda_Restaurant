using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialCustomer : Customer
{
    [Space]
    [Header("SpecialCustomer Components")]
    [SerializeField] private SpritePressEffect _spritePressEffect;
    [SerializeField] private ParticleSystem _coinParticle;

    private int _activeDuration;
    private int _touchCount;
    private int _touchAddMoney;
    private bool _isEndEvent;
    private Sprite _normalSprite;
    private Sprite _touchSprite;
    private Coroutine _coroutine;
    private Action _onCompleted;


    public override void SetData(CustomerData data)
    {
        if(!(data is SpecialCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 SpecialCustomerData만 받을 수 있습니다.");
            return;
        }

        base.SetData(data);
        SpecialCustomerData specialData = (SpecialCustomerData)data;

        _normalSprite = specialData.Sprite;
        _touchSprite = specialData.TouchSprite;
        _activeDuration = specialData.ActiveDuration;
        _touchCount = specialData.TouchCount;
        _touchAddMoney = specialData.TouchAddMoney;
        _isEndEvent = false;
        _spritePressEffect.Interactable = true;
        _spritePressEffect.RemoveAllListeners();
        _spritePressEffect.AddListener(OnTouchEvent);

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(OnEndTimeEvent());
    }

    public void StartEvent(List<Vector3> targetPosList, Action onCompleted)
    {
        LoopEvent(-1, targetPosList);
        _onCompleted = onCompleted;
    }


    private void LoopEvent(int currentIndex, List<Vector3> targetPosList)
    {
        if (_isEndEvent)
            return;

        List<Vector3> posList = targetPosList;

        int randInt = currentIndex;
        while (currentIndex == randInt)
            randInt = UnityEngine.Random.Range(0, posList.Count);

        Move(posList[randInt], 0, () => Tween.Wait(UnityEngine.Random.Range(0.02f, 2f), () => LoopEvent(randInt, posList)));
    }


    private void OnTouchEvent()
    {
        if (_isEndEvent)
            return;

        if (_customerData == null)
            return;

        _touchCount--;
        _coinParticle.Emit(UnityEngine.Random.Range(1, 4));
        _spriteRenderer.sprite = _touchSprite;

        Tween.Wait(1f, () =>
        {
            if (!gameObject.activeSelf)
                return;

            _spriteRenderer.sprite = _normalSprite;
        });

        UserInfo.AppendMoney(_touchAddMoney);

        if (_touchCount <= 0)
        {
            _isEndEvent = true;
            if (_coroutine != null)
                StopCoroutine(_coroutine);

            StopMove();
            _spriteRenderer.TweenAlpha(0, 1f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
            _onCompleted?.Invoke();
            return;
        }
    }


    private IEnumerator OnEndTimeEvent()
    {
        yield return YieldCache.WaitForSeconds(_activeDuration);

        if (_isEndEvent)
            yield break;

        _isEndEvent = true;
        _spritePressEffect.Interactable = false;

        StopMove();
        _spriteRenderer.TweenAlpha(0, 1f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
        _onCompleted?.Invoke();
    }
}
