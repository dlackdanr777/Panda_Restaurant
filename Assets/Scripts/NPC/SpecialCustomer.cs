using Muks.PathFinding.AStar;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Muks.Tween;

public class SpecialCustomer : Customer
{
    [Space]
    [Header("SpecialCustomer Components")]
    [SerializeField] private PointerClickSpriteRenderer _pointerClickSpriteRenderer;
    [SerializeField] private ParticleSystem _coinParticle;

    private int _touchCount;
    private int _touchAddMoney;
    private bool _isEndEvent;
    private Coroutine _coroutine;

    public override void SetData(CustomerData data)
    {
        if(!(data is SpecialCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 SpecialCustomerData만 받을 수 있습니다.");
            return;
        }

        base.SetData(data);
        SpecialCustomerData specialData = (SpecialCustomerData)data;

        _customerData = data;
        _spriteParent.transform.localPosition = new Vector3(0, -AStar.Instance.NodeSize * 2, 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.sprite = data.Sprite;
        _spriteRenderer.color = Color.white;
        _touchCount = specialData.TouchCount;
        _touchAddMoney = specialData.TouchAddMoney;
        _isEndEvent = false;

        _pointerClickSpriteRenderer.RemoveAllEvent();
        _pointerClickSpriteRenderer.AddEvent(OnTouchEvent);

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(OnEndTimeEvent());
    }

    public void StartEvent(List<Vector3> targetPosList)
    {
        LoopEvent(-1, targetPosList);
    }


    private void LoopEvent(int currentIndex, List<Vector3> targetPosList)
    {
        if (_isEndEvent)
            return;

        List<Vector3> posList = targetPosList;

        int randInt = currentIndex;
        while (currentIndex == randInt)
            randInt = Random.Range(0, posList.Count);

        Move(posList[randInt], 0, () => Tween.Wait(Random.Range(0.02f, 2f), () => LoopEvent(randInt, posList)));
    }


    private void OnTouchEvent()
    {
        if (_isEndEvent)
            return;

        if (_customerData == null)
            return;

        _touchCount--;
        _coinParticle.Emit(1);
        UserInfo.AppendMoney(_touchAddMoney);

        if (_touchCount <= 0)
        {
            _isEndEvent = true;
            if (_coroutine != null)
                StopCoroutine(_coroutine);

            StopMove();
            _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
            return;
        }
    }


    private IEnumerator OnEndTimeEvent()
    {
        yield return YieldCache.WaitForSeconds(60);

        if (_isEndEvent)
            yield break;

        _isEndEvent = true;

        StopMove();
        _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnSpecialCustomer(this));
    }
}
