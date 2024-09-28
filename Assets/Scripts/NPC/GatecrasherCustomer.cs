using Muks.PathFinding.AStar;
using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GatecrasherCustomer : Customer
{
    [Space]
    [Header("GatecrasherCustomer Components")]
    [SerializeField] private SpritePressEffect _spritePressEffect;
    [SerializeField] private SpriteRendererGroup _spriteGroup;
    [SerializeField] private SpriteFillAmount _spriteFillAmount;
    [SerializeField] private ParticleSystem _touchParticle;

    [Space]
    [Header("GatecrasherCustomer1 Option")]
    [SerializeField] private ParticleSystem _stealParticle;

    [Space]
    [Header("GatecrasherCustomer2 Option")]
    [SerializeField] private ParticleSystem _soundParticle;

    private int _activeDuration;
    private int _touchCount;
    private int _totalTouchCount;
    private bool _isEndEvent;
    private bool _touchEnabled;
    private Coroutine _gatecrasher1Coroutine;
    private Coroutine _actionCoroutine;
    private Coroutine _enabledCoroutine;
    private Action _onCompleted;

    public override void SetData(CustomerData data)
    {
        if (!(data is GatecrasherCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 GatecrasherCustomerData만 받을 수 있습니다.");
            return;
        }
        GatecrasherCustomerData gatecrasherData = (GatecrasherCustomerData)data;
        _animator.runtimeAnimatorController = gatecrasherData.Controller;
        base.SetData(data);
        _activeDuration = gatecrasherData.ActiveDuration;
        _touchCount = 0;
        _totalTouchCount = gatecrasherData.TouchCount;
        _isEndEvent = false;
        _touchEnabled = false;
        _spriteGroup.SetAlpha(0);
        _spritePressEffect.Interactable = false;

        _touchParticle.Stop();
        _soundParticle.Stop();
        _stealParticle.Stop();
        _spritePressEffect.RemoveAllListeners();
        _spritePressEffect.AddListener(OnTouchEvent);

        if (_enabledCoroutine != null)
            StopCoroutine(_enabledCoroutine);

        if (_gatecrasher1Coroutine != null)
            StopCoroutine(_gatecrasher1Coroutine);
    }

    public void StartGatecreasherCustomer2Event(Vector3 targetPos, TableManager tableManager, Action onCompleted)
    {
        _touchEnabled = false;
        _onCompleted = onCompleted;
        _spriteRenderer.sprite = _customerData.Sprite;
        Move(targetPos, -1, () =>
        {
            _spriteRenderer.sprite = _customerData.Sprite;
            Tween.Wait(1f, () =>
            {
                _spritePressEffect.Interactable = true;
                _touchEnabled = true;
                ChangeState(CustomerState.Action);
                _soundParticle.Play();

                if (_enabledCoroutine != null)
                    StopCoroutine(_enabledCoroutine);
                _enabledCoroutine = StartCoroutine(OnEndTimeEvent());

                if (_actionCoroutine != null)
                    StopCoroutine(_actionCoroutine);
                _actionCoroutine = StartCoroutine(Gatecrasher2Action(tableManager));
            });
        });
    }

    public void StartGatecreasherCustomer1Event(List<DropCoinArea> dropCoinAreaList, List<Vector3> noCoinTargetPosList, Action onCompleted)
    {
        _touchEnabled = true;
        _spritePressEffect.Interactable = true;
        _onCompleted = onCompleted;

        if (_enabledCoroutine != null)
            StopCoroutine(_enabledCoroutine);
        _enabledCoroutine = StartCoroutine(OnEndTimeEvent());

        if (_actionCoroutine != null)
            StopCoroutine(_actionCoroutine);

        if (_gatecrasher1Coroutine != null)
            StopCoroutine(_gatecrasher1Coroutine);

        LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList);
    }


    public void LoopGatecreasherCustomer1Event(List<DropCoinArea> dropCoinAreaList, List<Vector3> noCoinTargetPosList)
    {
        if (_isEndEvent)
            return;

        if (_gatecrasher1Coroutine != null)
            StopCoroutine(_gatecrasher1Coroutine);

        _stealParticle.Stop();
        DropCoinArea targetArea = GetMinDistanceCoinArea(dropCoinAreaList, transform.position);

        if (targetArea == null)
        {
            _gatecrasher1Coroutine = StartCoroutine(GatecreasherCustomer1Routine(dropCoinAreaList, noCoinTargetPosList));
            int randInt = UnityEngine.Random.Range(0, noCoinTargetPosList.Count);
            Move(noCoinTargetPosList[randInt], 0, () =>
            {
                Tween.Wait(UnityEngine.Random.Range(0f, 2f), () => LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList));
            });
        }

        else
        {
            Move(targetArea.transform.position, 0, () =>
            {
                Tween.Wait(1, () =>
                {
                    if (targetArea.Count <= 0)
                    {
                        LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList);
                    }

                    else
                    {
                        ChangeState(CustomerState.Idle);
                        _stealParticle.Play();
                        Tween.Wait(2, () =>
                        {
                            targetArea.OnCoinStealEvent(_spriteRenderer.transform.position + Vector3.up);
                            Tween.Wait(1.5f, () => LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList));
                        });
                    }

                });
            });
        }     
    }

    public IEnumerator GatecreasherCustomer1Routine(List<DropCoinArea> dropCoinAreaList, List<Vector3> noCoinTargetPosList)
    {
        DropCoinArea targetArea;
        while (true)
        {
            if (_isEndEvent)
                yield break;

            DebugLog.Log("탐색중");
            yield return YieldCache.WaitForSeconds(0.5f);
            targetArea = GetMinDistanceCoinArea(dropCoinAreaList, transform.position);
            
            if(targetArea != null)
            {
                LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList);
                yield break;
            }
        }
    }



    private void OnTouchEvent()
    {
        if (_isEndEvent || !_touchEnabled)
            return;

        if (_customerData == null)
            return;

        _touchCount++;
        _touchParticle.Emit(UnityEngine.Random.Range(2, 4));
        _spriteGroup.SetAlpha(1);
        _spriteFillAmount.TweenFillAmount((float)_touchCount / _totalTouchCount, 0.05f);

        if (_totalTouchCount <= _touchCount)
        {
            _isEndEvent = true;

            if (_enabledCoroutine != null)
                StopCoroutine(_enabledCoroutine);

            if (_actionCoroutine != null)
                StopCoroutine(_actionCoroutine);

            StopMove();
            _spritePressEffect.Interactable = false;
            _spriteGroup.SetAlpha(1);
            _spriteGroup.TweenSetAlpha(0, 0.7f);
            _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
            _onCompleted?.Invoke();
            return;
        }
    }


    public DropCoinArea GetMinDistanceCoinArea(List<DropCoinArea> dropCoinAreaList, Vector3 startPos)
    {

        int moveObjFloor = AStar.Instance.GetTransformFloor(startPos);
        List<DropCoinArea> equalFloorArea = new List<DropCoinArea>();
        List<DropCoinArea> notEqualFloorArea = new List<DropCoinArea>();

        for (int i = 0, cnt = dropCoinAreaList.Count; i < cnt; i++)
        {
            int targetFloor = AStar.Instance.GetTransformFloor(dropCoinAreaList[i].transform.position);
            if (dropCoinAreaList[i].Count <= 0)
                continue;

            if (moveObjFloor == targetFloor)
                equalFloorArea.Add(dropCoinAreaList[i]);

            else if (moveObjFloor != targetFloor)
                notEqualFloorArea.Add(dropCoinAreaList[i]);
        }


        if (equalFloorArea.Count == 0 && notEqualFloorArea.Count == 0)
            return null;

        else if (0 < equalFloorArea.Count)
        {
            float minDis = 10000000;
            int minIndex = 0;
            for (int i = 0, cnt = equalFloorArea.Count; i < cnt; i++)
            {
                if (Vector2.Distance(equalFloorArea[i].transform.position, startPos) < minDis)
                {
                    minDis = Vector2.Distance(equalFloorArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }
            return equalFloorArea[minIndex];
        }


        else
        {
            float minDis = 10000000;
            int minIndex = 0;

            for (int i = 0, cnt = notEqualFloorArea.Count; i < cnt; i++)
            {
                int targetFloor = AStar.Instance.GetTransformFloor(notEqualFloorArea[i].transform.position);
                Vector2 floorDoorPos = AStar.Instance.GetFloorPos(targetFloor);

                if (Vector2.Distance(notEqualFloorArea[i].transform.position, floorDoorPos) < minDis)
                {
                    minDis = Vector2.Distance(notEqualFloorArea[i].transform.position, startPos);
                    minIndex = i;
                }
            }

            return notEqualFloorArea[minIndex];
        }
    }



    private IEnumerator Gatecrasher2Action(TableManager tableManager)
    {
        yield return YieldCache.WaitForSeconds(2);
        List<NormalCustomer> sitCustomerList = tableManager.GetSitCustomerList();
        NormalCustomer[] tmpCustomers = new NormalCustomer[sitCustomerList.Count];
        int maxIndex = 2;
        float time = 0;

        while(time < _activeDuration * 0.5f)
        {
            sitCustomerList = tableManager.GetSitCustomerList();
            for (int i = 0; i < maxIndex; ++i)
            {
                if (sitCustomerList[i] == null)
                {
                    sitCustomerList[i] = null;
                    tmpCustomers[i] = null;
                    continue;
                }

                if (sitCustomerList[i] == tmpCustomers[i])
                {
                    sitCustomerList[i].StartAnger();
                    tableManager.ExitCustomer(i);
                    continue;
                }

                tmpCustomers[i] = sitCustomerList[i];
            }
            time += 1;
            yield return YieldCache.WaitForSeconds(1);
        }

        maxIndex = sitCustomerList.Count;
        while(time < _activeDuration)
        {
            sitCustomerList = tableManager.GetSitCustomerList();
            for (int i = 0; i < maxIndex; ++i)
            {
                if (sitCustomerList[i] == null)
                {
                    sitCustomerList[i] = null;
                    tmpCustomers[i] = null;
                    continue;
                }

                if (sitCustomerList[i] == tmpCustomers[i])
                {
                    sitCustomerList[i].StartAnger();
                    tableManager.ExitCustomer(i);
                    continue;
                }

                tmpCustomers[i] = sitCustomerList[i];
            }
            time += 1;
            yield return YieldCache.WaitForSeconds(1);
        }
    }


    private IEnumerator OnEndTimeEvent()
    {
        yield return YieldCache.WaitForSeconds(_activeDuration);

        if (_isEndEvent)
            yield break;

        _isEndEvent = true;

        if (_actionCoroutine != null)
            StopCoroutine(_actionCoroutine);

        if (_gatecrasher1Coroutine != null)
            StopCoroutine(_gatecrasher1Coroutine);

        StopMove();
        _spritePressEffect.Interactable = false;
        _spriteGroup.TweenSetAlpha(0, 0.7f);
        _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
        _onCompleted?.Invoke();
    }
}
