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

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _visitSound;
    [SerializeField] private AudioClip _goldSound;

    private int _activeDuration;
    private int _touchCount;
    private int _touchAddMoney;
    private bool _isEndEvent;
    private Sprite _normalSprite;
    private Sprite _touchSprite;
    private Coroutine _coroutine;
    private Coroutine _touchCoroutine;
    private Action<Customer> _onCompleted;


    private FeverSystem _feverSystem;


    public override void SetData(CustomerData data, CustomerController customerController, TableManager tableManager)
    {
        if(!(data is SpecialCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 SpecialCustomerData만 받을 수 있습니다.");
            return;
        }

        base.SetData(data, customerController, tableManager);
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

        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _visitSound, 0.15f);

        if (_touchCoroutine != null)
            StopCoroutine(_touchCoroutine);

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(OnEndTimeRoutine());
    }


    public void StartEvent(List<Vector3> targetPosList, Action<Customer> onCompleted)
    {
        LoopEvent(-1, targetPosList);
        UserInfo.AddSatisfaction(UserInfo.CurrentStage, 5);
        _onCompleted = onCompleted;
    }

    public void SetFeverSystem(FeverSystem feverSystem)
    {
        _feverSystem = feverSystem;
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

        if (_touchCoroutine != null)
            StopCoroutine(_touchCoroutine);
        _touchCoroutine = StartCoroutine(OnTouchRoutine());

        UserInfo.AddMoney(_touchAddMoney + GameManager.Instance.AddSpecialCustomerMoney);
        _feverSystem?.AddFeverGauge();
        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _goldSound);
        if (_touchCount <= 0)
        {
            _isEndEvent = true;
            if (_coroutine != null)
                StopCoroutine(_coroutine);

            StopMove();
            _spriteRenderer.TweenAlpha(0, 1f).OnComplete(() =>
            {
                _spriteRenderer.TweenAlpha(0, 0.5f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
            });

            _onCompleted?.Invoke(this);
            return;
        }
    }


    private IEnumerator OnEndTimeRoutine()
    {
        yield return YieldCache.WaitForSeconds(_activeDuration);

        if (_isEndEvent)
            yield break;

        _isEndEvent = true;
        _spritePressEffect.Interactable = false;

        if (_touchCoroutine != null)
            StopCoroutine(_touchCoroutine);

        StopMove();
        _spriteRenderer.TweenAlpha(0, 1f).OnComplete(() =>
        {
            _spriteRenderer.TweenAlpha(0, 0.5f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
        });
        _onCompleted?.Invoke(this);
    }

    private IEnumerator OnTouchRoutine()
    {
        _spriteRenderer.sprite = _touchSprite;
        yield return YieldCache.WaitForSeconds(0.5f);
        _spriteRenderer.sprite = _normalSprite;
    }
}
