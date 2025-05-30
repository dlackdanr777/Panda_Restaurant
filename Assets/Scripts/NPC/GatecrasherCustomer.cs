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
    [SerializeField] private ParticleSystem _disguiseOffParticle;

    [Space]
    [Header("GatecrasherCustomer2 Option")]
    [SerializeField] private ParticleSystem _soundParticle;
    [SerializeField] private AudioSource _guitarSource;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _visitSound;
    [SerializeField] private AudioClip _touchSound;


    private int _activeDuration;
    private float _currentTouchCount;
    private float _totalTouchCount;
    public float TotalTouchCount => _totalTouchCount;
    private bool _isEndEvent;
    public bool IsEndEvent => _isEndEvent;
    private bool _isInDisguise;
    public bool IsInDisguise => _isInDisguise;
    private bool _touchEnabled;

    private Sprite _gatecrasher1DisquiseSprite;
    private Coroutine _gatecrasher1Coroutine;
    private Coroutine _actionCoroutine;
    private Coroutine _enabledCoroutine;
    private Coroutine _speedRecoveryCoroutine;
    private Coroutine _subSatisfactionCoroutine;
    private TweenData _tween;
    private Action<Customer> _onCompleted;
    private Action _gatecrasher1OnChangeShape;

    private float _touchDamage => 1;


    private void OnDisable()
    {
        if(_tween != null)
            _tween.TweenStop();

        gameObject.TweenStop();
        StopAllCoroutines();
    }


    public override void SetData(CustomerData data, CustomerController customerController, TableManager tableManager)
    {
        if (!(data is GatecrasherCustomerData))
        {
            DebugLog.LogError("해당 오브젝트는 GatecrasherCustomerData만 받을 수 있습니다.");
            return;
        }
        GatecrasherCustomerData gatecrasherData = (GatecrasherCustomerData)data;
        _animator.runtimeAnimatorController = gatecrasherData.Controller;
        base.SetData(data,customerController, tableManager);
        _activeDuration = gatecrasherData.ActiveDuration;
        _currentTouchCount = 0;
        _totalTouchCount = gatecrasherData.TouchCount;
        _isEndEvent = false;
        _touchEnabled = false;
        _spriteGroup.SetAlpha(0);
        _spritePressEffect.Interactable = false;
        _spritePressEffect.SetTmpScale(_spriteParent.transform.localScale);
        _touchParticle.Stop();
        _soundParticle.Stop();
        _stealParticle.Stop();
        _disguiseOffParticle.Stop();
        _guitarSource.Stop();
        _spritePressEffect.RemoveAllListeners();
        _spritePressEffect.AddListener(OnTouchEvent);
        _spriteFillAmount.SetFillAmount(0);
        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _visitSound, 0.15f);
        UserInfo.CustomerVisits(data);
        UserInfo.AddSatisfaction(UserInfo.CurrentStage, -5);
        if (_enabledCoroutine != null)
            StopCoroutine(_enabledCoroutine);

        if (_gatecrasher1Coroutine != null)
            StopCoroutine(_gatecrasher1Coroutine);

        if (_actionCoroutine != null)
            StopCoroutine(_actionCoroutine);

        if(_speedRecoveryCoroutine != null)
            StopCoroutine(_speedRecoveryCoroutine);

        if(_subSatisfactionCoroutine != null)
            StopCoroutine(_subSatisfactionCoroutine);
    }

    public void StartGatecreasherCustomer2Event(Vector3 targetPos, TableManager tableManager, Action<Customer> onCompleted)
    {
        _touchEnabled = false;
        _onCompleted = onCompleted;
        _spriteRenderer.sprite = _customerData.Sprite;
        Move(targetPos, -1, () =>
        {
            _spriteRenderer.sprite = _customerData.Sprite;
            _tween = Tween.Wait(1f, () =>
            {
                _spritePressEffect.Interactable = true;
                _touchEnabled = true;
                ChangeState(CustomerState.Action);
                _soundParticle.Play();
                _guitarSource.Play();
                if (_gatecrasher1Coroutine != null)
                    StopCoroutine(_gatecrasher1Coroutine);

                if (_subSatisfactionCoroutine != null)
                    StopCoroutine(_subSatisfactionCoroutine);
                _subSatisfactionCoroutine = StartCoroutine(SubSatisfactionRoutine());

                if (_enabledCoroutine != null)
                    StopCoroutine(_enabledCoroutine);
                _enabledCoroutine = StartCoroutine(OnEndTimeEvent());

                if (_actionCoroutine != null)
                    StopCoroutine(_actionCoroutine);
                _actionCoroutine = StartCoroutine(Gatecrasher2Action(tableManager));
            });
        });
    }

    public void StartGatecreasherCustomer1Event(List<DropCoinArea> dropCoinAreaList, List<Vector3> noCoinTargetPosList, Action<Customer> onCompleted, Action onChangeShape = null)
    {
        _touchEnabled = true;
        _isInDisguise = true;
        _spritePressEffect.Interactable = true;
        _onCompleted = onCompleted;
        _gatecrasher1OnChangeShape = onChangeShape;

        List<CustomerData> customerList = CustomerDataManager.Instance.GetAppearNormalCustomerList();
        _gatecrasher1DisquiseSprite = customerList[UnityEngine.Random.Range(0, customerList.Count)].Sprite;
        _spriteRenderer.sprite = _gatecrasher1DisquiseSprite;
        _animator.runtimeAnimatorController = ((GatecrasherCustomer1Data)_customerData).Controller;

        if (_enabledCoroutine != null)
            StopCoroutine(_enabledCoroutine);
        _enabledCoroutine = StartCoroutine(OnEndTimeEvent());

        if (_subSatisfactionCoroutine != null)
            StopCoroutine(_subSatisfactionCoroutine);
        _subSatisfactionCoroutine = StartCoroutine(SubSatisfactionRoutine());

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
                _tween = Tween.Wait(UnityEngine.Random.Range(0f, 2f), () => LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList));
            });
        }

        else
        {
            Move(targetArea.transform.position, 0, () =>
            {

                _tween = Tween.Wait(1, () =>
                {
                    if (targetArea.Count <= 0)
                    {
                        if (_isEndEvent)
                            return;

                        LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList);
                    }

                    else
                    {
                        if (_isInDisguise)
                        {
                            GatecrasherCustomer1Data gatecrasher2Data = (GatecrasherCustomer1Data)_customerData;
                            _disguiseOffParticle.Emit(1);
                            _gatecrasher1OnChangeShape?.Invoke();
                            _spriteRenderer.sprite = _customerData.Sprite;
                            _animator.runtimeAnimatorController = gatecrasher2Data.GatecrasherController;
                            _isInDisguise = false;
                        }

                        ChangeState(CustomerState.Idle);
                        _stealParticle.Play();
                        _tween = Tween.Wait(2, () =>
                        {
                            if(_isEndEvent)
                                return;

                            targetArea.OnCoinStealEvent(_spriteRenderer.transform.position + Vector3.up);
                            _tween = Tween.Wait(1.5f, () => LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList));
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

            yield return YieldCache.WaitForSeconds(0.5f);
            targetArea = GetMinDistanceCoinArea(dropCoinAreaList, transform.position);
            
            if(targetArea != null)
            {
                LoopGatecreasherCustomer1Event(dropCoinAreaList, noCoinTargetPosList);
                yield break;
            }
        }
    }



    public void OnTouchEvent()
    {
        if (_isEndEvent || !_touchEnabled)
            return;

        if (_customerData == null)
            return;

        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _touchSound);
        _currentTouchCount += _touchDamage * GameManager.Instance.AddGatecrasherCustomerDamageMul;
        _touchParticle.Emit(UnityEngine.Random.Range(2, 4));
        _spriteGroup.SetAlpha(1);
        _spriteFillAmount.TweenFillAmount(_currentTouchCount / _totalTouchCount, 0.05f);

        if (_speedRecoveryCoroutine != null)
            StopCoroutine(_speedRecoveryCoroutine);
        _speedRecoveryCoroutine = StartCoroutine(SpeedRecoveryRoutine());
        if (_totalTouchCount <= _currentTouchCount)
        {
            _isEndEvent = true;

            if (_enabledCoroutine != null)
                StopCoroutine(_enabledCoroutine);

            if (_actionCoroutine != null)
                StopCoroutine(_actionCoroutine);

            if (_speedRecoveryCoroutine != null)
                StopCoroutine(_speedRecoveryCoroutine);

            if (_subSatisfactionCoroutine != null)
                StopCoroutine(_subSatisfactionCoroutine);

            StopMove();
            _spritePressEffect.Interactable = false;
            _spriteGroup.SetAlpha(1);
            _spriteGroup.TweenSetAlpha(0, 0.7f);
            _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
            _guitarSource.Stop();
            UserInfo.AddExterminationGatecrasherCustomerCount(_customerData);
            UserInfo.AddSatisfaction(UserInfo.CurrentStage, 5);
            _onCompleted?.Invoke(this);
            return;
        }
    }


    public DropCoinArea GetMinDistanceCoinArea(List<DropCoinArea> dropCoinAreaList, Vector3 startPos)
    {
        Vector3 targetDoorPos = _tableManager.GetDoorPos(RestaurantType.Hall, startPos);
        List<DropCoinArea> equalFloorArea = new List<DropCoinArea>();
        List<DropCoinArea> notEqualFloorArea = new List<DropCoinArea>();

        for (int i = 0, cnt = dropCoinAreaList.Count; i < cnt; i++)
        {
            Vector3 coinDoorPos = _tableManager.GetDoorPos(RestaurantType.Hall, dropCoinAreaList[i].transform.position);
            if (dropCoinAreaList[i].Count <= 0)
                continue;

            if (Vector3.Distance(targetDoorPos, coinDoorPos) <= 0.5f)
                equalFloorArea.Add(dropCoinAreaList[i]);

            else
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
                Vector3 coinDoorPos = _tableManager.GetDoorPos(RestaurantType.Hall, notEqualFloorArea[i].transform.position);

                if (Vector2.Distance(notEqualFloorArea[i].transform.position, coinDoorPos) < minDis)
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
        List<TableData> tableDataList = tableManager.GetTableDataList(_visitFloor);
        int maxIndex = (int)TableType.Table2 + 1;
        float time = 0;

        while(time < _activeDuration * 0.5f)
        {
            tableDataList = tableManager.GetTableDataList(_visitFloor);
            for (int i = 0; i < maxIndex; ++i)
            {

                if (tableDataList[i].CurrentCustomer != null)
                {
                    tableDataList[i].CurrentCustomer.StartAnger();
                    tableManager.ExitCustomer(tableDataList[i]);
                    continue;
                }
            }
            time += 1;
            yield return YieldCache.WaitForSeconds(1);
        }

        maxIndex = (int)TableType.Length;
        while(time < _activeDuration)
        {
            tableDataList = tableManager.GetTableDataList(_visitFloor);
            for (int i = 0; i < maxIndex; ++i)
            {

                if (tableDataList[i].CurrentCustomer != null)
                {
                    tableDataList[i].CurrentCustomer.StartAnger();
                    tableManager.ExitCustomer(tableDataList[i]);
                    continue;
                }
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

        if (_speedRecoveryCoroutine != null)
            StopCoroutine(_speedRecoveryCoroutine);

        if (_subSatisfactionCoroutine != null)
            StopCoroutine(_subSatisfactionCoroutine);

        StopMove();
        _spritePressEffect.Interactable = false;
        _spriteGroup.TweenSetAlpha(0, 0.7f);
        _spriteRenderer.TweenAlpha(0, 0.7f).OnComplete(() => ObjectPoolManager.Instance.DespawnGatecrasherCustomer(this));
        _guitarSource.Stop();
        _onCompleted?.Invoke(this);
    }

    private IEnumerator SpeedRecoveryRoutine()
    {
        _moveSpeed = 0.1f;
        float startSpeed = 0.1f;
        float targetSpeed = _customerData.MoveSpeed;
        float targetTime = 0.1f + GameManager.Instance.AddGatecrasherCustomerSpeedDownTime;
        float timer = 0;

        while(timer < targetTime)
        {
            _moveSpeed = Mathf.Lerp(startSpeed, targetSpeed, timer / targetTime);
            timer += 0.02f;
            yield return new WaitForSeconds(0.02f);
        }

        _moveSpeed = targetSpeed;
    }

    private IEnumerator SubSatisfactionRoutine()
    {
        while(true)
        {
            yield return YieldCache.WaitForSeconds(10f);
            UserInfo.AddSatisfaction(UserInfo.CurrentStage, -3);        
        }

    }
}
