using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Muks.Tween;

public class Staff : MonoBehaviour
{
    public event Action OnLevelUpEventHandler;
    [SerializeField] protected Animator _animator;
    [SerializeField] protected GameObject _moveObj;
    [SerializeField] protected GameObject _spriteParent;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    [Space]
    [Header("Skill")]
    [SerializeField] protected SpriteRenderer _skillEffect;
    [SerializeField] protected AudioClip _skillActiveSound;

    protected TableManager _tableManager;
    protected KitchenSystem _kitchenSystem;
    protected CustomerController _customerController;
    protected FeverSystem _feverSystem;

    private readonly StaffSkillRuntimeContext _skillRuntimeContext =
        new StaffSkillRuntimeContext();
    private GameManager _gameManager;
    private StaffSkillEffectRegistry _skillEffectRegistry;
    private SkillBase _activeRuntimeSkill;
    private StaffSkillSourceToken _activeSkillToken;
    private TableManager _activeSkillTableManager;
    private KitchenSystem _activeSkillKitchenSystem;
    private CustomerController _activeSkillCustomerController;
    private bool _isApplicationQuitting;

    public StaffSkillRuntimeContext RuntimeSkillContext => _skillRuntimeContext;
    public StaffSkillEffectRegistry SkillEffectRegistry => _skillEffectRegistry;
    public StaffSkillSourceToken CurrentSkillSourceToken => _activeSkillToken;

    protected StaffData _staffData;
    public StaffData StaffData => _staffData;

    protected EquipStaffType _staffType;
    public EquipStaffType EquipStaffType => _staffType;
    protected StaffGroupType _staffGroupType;
    protected IStaffAction _staffAction;
    protected EStaffState _state;
    protected ERestaurantFloorType _equipFloorType;
    public ERestaurantFloorType EquipFloorType => _equipFloorType;
    protected RestaurantType _restaurantType;
    public RestaurantType RestaurantType => _restaurantType;

    protected Sprite _sprite;
    protected Sprite[] _idleSprites;


    protected bool _usingSkill;
    protected float _skillTimer;
    protected float _skillCoolTime;
    public int Level => _staffData != null ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, _staffData) : 1;

    protected float _scaleX;
    protected float _moveSpeed;
    protected float _speedMul;
    public float SpeedMul => Mathf.Clamp((1 + _speedMul) + (1 * GameManager.Instance.GetStaffSpeedMul(_staffGroupType)), 0.5f, 3f);
    public float MoveSpeedMul => Mathf.Clamp((1 + _speedMul) + (1 * GameManager.Instance.GetStaffMoveSpeedMul(_staffGroupType)), 0.5f, 3f);
    public float WorkSpeedMul => Mathf.Clamp(1 + GameManager.Instance.GetStaffWorkSpeedMul(), 0.5f, 3f);
    public float GuardEliminationSpeedMul => Mathf.Clamp(1 + GameManager.Instance.GetGuardEliminationSpeedMul(), 0.5f, 3f);

    protected Coroutine _useSkillRoutine;
    protected Coroutine _idleAnimationRoutine;
    protected RuntimeAnimatorController _defaultAnimatorController;


    public virtual void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        CancelActiveSkill(StaffSkillCancellationReason.ManualCancellation, true);
        _gameManager = GameManager.Instance;
        _skillEffectRegistry = _gameManager.StaffSkillEffectRegistry;
        _skillRuntimeContext.ResetLocalState();
        _activeRuntimeSkill = null;
        _activeSkillToken = default;
        _usingSkill = false;
        _useSkillRoutine = null;
        _isApplicationQuitting = false;
        _staffType = type;
        _staffGroupType = StaffDataManager.Instance.GetStaffGroupType(type);
        _tableManager = tableManager;
        _customerController = customerController;
        _kitchenSystem = kitchenSystem;
        _feverSystem = feverSystem;
        _spriteRenderer.color = Color.white;
        _scaleX = transform.localScale.x;
        _gameManager.OnChangeStaffSkillValueHandler += OnChangeSkillValueEvent;
        UserInfo.OnUpgradeStaffHandler += OnLevelUpEvent;
        UserInfo.OnChangeStaffSkinHandler += OnChangeSkinEvent;
        _feverSystem.OnStartFeverHandler += OnStartFeverEvent;
        _feverSystem.OnEndFeverHandler += OnEndFeverEvent;

        if (_animator != null)
        {
            DebugLog.Log("스탭 애니메이터 컨트롤러 초기화: " + name + " - " + _animator.runtimeAnimatorController);
            _defaultAnimatorController = _animator.runtimeAnimatorController;
        }

        gameObject.SetActive(false);
    }

    public float GetActionValue()
    {
        if (_staffData == null)
        {
            throw new Exception("현재 스탭 데이터가 null입니다.");
        }

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _staffData);
        if (level <= 0)
        {
            throw new Exception("현재 스탭 데이터를 보유하고 있지 않습니다: " + _staffData.Id);
        }

        return _staffData.GetActionValue(level);
    }



    public virtual void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        StaffSkillCancellationReason cancellationReason = staffData == null
            ? StaffSkillCancellationReason.StaffDataCleared
            : staffData == _staffData
                ? StaffSkillCancellationReason.SameStaffDataReassigned
                : StaffSkillCancellationReason.StaffDataChanged;
        CancelActiveSkill(cancellationReason, true);
        StopAllCoroutines();
        SkillEffectSetActive(false);
        if (staffData == _staffData)
            return;

        if (_staffData != null)
        {
            _staffData.RemoveSlot(this, _tableManager, _kitchenSystem, _customerController);
            _staffAction?.Destructor();
        }

        if (staffData == null)
        {
            _staffData = null;
            _staffAction = null;
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _staffData = staffData;
        _equipFloorType = equipFloorType;
        _restaurantType = StaffDataManager.Instance.GetStaffRestaurantType(staffData);
        _staffData.AddSlot(this, _tableManager, _kitchenSystem, _customerController);

        if (_animator != null)
        {
            _animator.runtimeAnimatorController = _staffData.AnimatorController == null ? _defaultAnimatorController : _staffData.AnimatorController;
        }

        _moveSpeed = staffData.GetSpeed(Level);
        _speedMul = 0;
        _usingSkill = false;
        _skillTimer = 0;

        OnChangeSkinEvent();
        _spriteRenderer.enabled = false;
        _spriteRenderer.sprite = _sprite;
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteParent.transform.localPosition = new Vector3(0, -(AStar.Instance.NodeSize * 0.5f), 0);
        _spriteRenderer.enabled = true;

        _staffAction = staffData.GetStaffAction(this, _tableManager, _kitchenSystem, _customerController);

        OnChangeSkillValueEvent();
        // 스탭 데이터 설정 완료 후 기본적으로 Idle 상태로 설정하여 Idle 애니메이션 시작
        SetStaffState(EStaffState.None);
    }

    public virtual void SetAlpha(float alpha)
    {
        _spriteRenderer.color = new Color(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, alpha);
    }

    public virtual void TweenAlpha(float alpha, float duration, Ease ease, Action onCompleted = null)
    {
        if (_skillEffect != null)
            _skillEffect.TweenStop();
        
        if (_spriteRenderer != null)
            _spriteRenderer.TweenStop();
        
        _skillEffect?.TweenAlpha(alpha, duration, ease);
        
        if (_spriteRenderer != null)
            _spriteRenderer.TweenAlpha(alpha, duration, ease).OnComplete(onCompleted);
    }


    public void AddSpeedMul(float value)
    {
        _speedMul = Mathf.Clamp(_speedMul + value * 0.01f, 0f, 10f);
    }

    public virtual void SetStaffState(EStaffState state)
    {
        _state = state;

        // Idle 상태(None)일 때만 Idle 애니메이션 시작
        if (_state == EStaffState.None)
        {
            StartIdleAnimation();
        }
        else
        {
            StopIdleAnimation();
        }
    }


    public void SetSpriteDir(float dir)
    {
        if (dir < 0) transform.localScale = new Vector3(_scaleX, transform.localScale.y, transform.localScale.z);
        else if (0 < dir) transform.localScale = new Vector3(-_scaleX, transform.localScale.y, transform.localScale.z);
    }

    public void SetLayer(string sortingLayerName, int orderInLayer)
    {
        _spriteRenderer.sortingLayerName = sortingLayerName;
        _spriteRenderer.sortingOrder = orderInLayer;

        if (_skillEffect != null)
        {
            _skillEffect.sortingLayerName = sortingLayerName;
            _skillEffect.sortingOrder = orderInLayer - 1;
        }
    }

    public void SetOrderLayer(int orderInLayer)
    {
        _spriteRenderer.sortingOrder = orderInLayer;
    }


    public void StaffAction()
    {
        if (_staffAction == null)
            return;

        _staffAction.PerformAction(this);
    }


    public void UsingStaffSkill(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
        {
            return;
        }

        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        bool contextActive = _skillRuntimeContext.IsActive;
        bool tokenValid = _activeSkillToken.IsValid;
        bool hasRoutine = _useSkillRoutine != null;
        if (_usingSkill || contextActive || tokenValid || hasRoutine)
        {
            bool synchronized = _usingSkill
                && contextActive
                && tokenValid
                && _skillRuntimeContext.IsCurrentToken(_activeSkillToken);
            if (synchronized)
            {
                DebugLog.Log("스킬이 이미 사용중 입니다.");
                return;
            }

            DebugLog.LogError("[Staff Skill] Runtime 상태 불일치를 감지해 현재 스킬을 정리합니다: " + name);
            CancelActiveSkill(StaffSkillCancellationReason.CoroutineInterrupted, true);
            _skillTimer = 0;
            return;
        }

        int cooldown = StaffSkillTimeCalculator.CalculateCooldownSeconds(_skillCoolTime, Level);
        if (cooldown <= _skillTimer)
        {
            if (_skillEffectRegistry == null)
            {
                DebugLog.LogError("[Staff Skill] Effect Registry가 없어 스킬 발동을 중단합니다: " + name);
                return;
            }

            StaffData capturedStaffData = _staffData;
            SkillBase capturedSkill = capturedStaffData.Skill;
            TableManager capturedTableManager = tableManager;
            KitchenSystem capturedKitchenSystem = kitchenSystem;
            CustomerController capturedCustomerController = customerController;
            int capturedLevel = Level;
            string debugId = capturedStaffData.Id + "/" + capturedSkill.GetType().Name;

            _skillTimer = 0;
            StaffSkillSourceToken sourceToken = _skillRuntimeContext.BeginActivation(debugId);
            _activeRuntimeSkill = capturedSkill;
            _activeSkillToken = sourceToken;
            _activeSkillTableManager = capturedTableManager;
            _activeSkillKitchenSystem = capturedKitchenSystem;
            _activeSkillCustomerController = capturedCustomerController;
            _usingSkill = true;

            try
            {
                Coroutine startedRoutine = StartCoroutine(
                    UseSkillCoroutine(
                        capturedSkill,
                        capturedTableManager,
                        capturedKitchenSystem,
                        capturedCustomerController,
                        capturedLevel,
                        sourceToken));
                if (_skillRuntimeContext.IsCurrentToken(sourceToken))
                {
                    _useSkillRoutine = startedRoutine;
                }
                else if (startedRoutine != null)
                {
                    StopCoroutine(startedRoutine);
                }
            }
            catch (Exception exception)
            {
                CancelActiveSkill(StaffSkillCancellationReason.ActivationFailed, true);
                DebugLog.LogError("[Staff Skill] Coroutine 시작 실패: " + debugId + "\n" + exception);
            }
        }
        else
        {
            _skillTimer += Time.deltaTime;
        }
    }


    public void DestroyStaff()
    {
        _staffData?.Destroy();
    }

    private void OnDisable()
    {
        CancelActiveSkill(StaffSkillCancellationReason.GameObjectDisabled, true);

        if (transform != null)
            transform.TweenStop();

        if (_spriteRenderer != null)
            _spriteRenderer.TweenStop();

        StopIdleAnimation();
    }


    private IEnumerator UseSkillCoroutine(
        SkillBase activeSkill,
        TableManager tableManager,
        KitchenSystem kitchenSystem,
        CustomerController customerController,
        int staffLevel,
        StaffSkillSourceToken sourceToken)
    {
        StaffSkillCancellationReason cancellationReason =
            StaffSkillCancellationReason.CoroutineInterrupted;
        Exception executionException = null;

        try
        {
            bool activationSucceeded = false;
            try
            {
                Vibration.Vibrate(500);
                SkillEffectSetActive(true);
                EffectType effectType = SoundManager.Instance.GetHallEffectType(_equipFloorType, _restaurantType);
                SoundManager.Instance.PlayEffectAudio(effectType, _skillActiveSound);
                activeSkill.Activate(this, tableManager, kitchenSystem, customerController);
                activationSucceeded = true;
            }
            catch (Exception exception)
            {
                cancellationReason = StaffSkillCancellationReason.ActivationFailed;
                executionException = exception;
            }

            bool durationCalculated = false;
            int duration = 0;
            if (activationSucceeded)
            {
                try
                {
                    if (_gameManager == null)
                    {
                        throw new InvalidOperationException("Cached GameManager is unavailable.");
                    }

                    float permanentDurationBonusRate =
                        _gameManager.GetStaffSkillTimeMul(_staffGroupType);
                    duration = StaffSkillTimeCalculator.CalculateDurationSeconds(
                        activeSkill.Duration,
                        staffLevel,
                        permanentDurationBonusRate);
                    durationCalculated = true;
                }
                catch (Exception exception)
                {
                    cancellationReason = StaffSkillCancellationReason.DurationCalculationFailed;
                    executionException = exception;
                }
            }

            if (durationCalculated)
            {
                float timer = 0;
                bool updateSucceeded = true;
                while (timer < duration)
                {
                    try
                    {
                        activeSkill.ActivateUpdate(this, tableManager, kitchenSystem, customerController);
                    }
                    catch (Exception exception)
                    {
                        cancellationReason = StaffSkillCancellationReason.UpdateFailed;
                        executionException = exception;
                        updateSucceeded = false;
                    }

                    if (!updateSucceeded)
                    {
                        break;
                    }

                    timer += 0.02f;
                    yield return YieldCache.WaitForSeconds(0.02f);
                }

                if (updateSucceeded)
                {
                    cancellationReason = StaffSkillCancellationReason.NormalDurationCompleted;
                }
            }
        }
        finally
        {
            if (_skillRuntimeContext.IsCurrentToken(sourceToken))
            {
                CancelActiveSkill(cancellationReason, false);
            }
        }

        if (executionException != null)
        {
            DebugLog.LogError(
                "[Staff Skill] 실행 오류 후 안전하게 정리했습니다: "
                + activeSkill.GetType().Name
                + " / "
                + cancellationReason
                + "\n"
                + executionException);
        }
    }

    private void CancelActiveSkill(
        StaffSkillCancellationReason reason,
        bool stopRunningCoroutine)
    {
        SkillBase activeSkill = _activeRuntimeSkill;
        StaffSkillSourceToken activeFieldToken = _activeSkillToken;
        Coroutine runningCoroutine = _useSkillRoutine;
        StaffSkillEffectRegistry effectRegistry = _skillEffectRegistry;
        TableManager tableManager = _activeSkillTableManager;
        KitchenSystem kitchenSystem = _activeSkillKitchenSystem;
        CustomerController customerController = _activeSkillCustomerController;
        StaffSkillSourceToken cancellationToken = _skillRuntimeContext.IsActive
            ? _skillRuntimeContext.CurrentActivationToken
            : activeFieldToken;
        Exception deactivateException = null;

        if (effectRegistry != null)
        {
            if (activeFieldToken.IsValid && activeFieldToken != cancellationToken)
            {
                effectRegistry.RemoveAllForSource(activeFieldToken);
            }

            if (cancellationToken.IsValid)
            {
                StaffSkillCancellationCoordinator.TryCancel(
                    _skillRuntimeContext,
                    effectRegistry,
                    cancellationToken,
                    activeSkill == null
                        ? null
                        : (Action)(() => activeSkill.Deactivate(
                            this,
                            tableManager,
                            kitchenSystem,
                            customerController)),
                    out deactivateException);
            }
            else if (_skillRuntimeContext.IsActive)
            {
                _skillRuntimeContext.ResetLocalState();
            }
        }
        else if (_skillRuntimeContext.IsActive)
        {
            DebugLog.LogError("[Staff Skill] Effect Registry 없이 활성 Context를 정리합니다: " + name);
            _skillRuntimeContext.ResetLocalState();
        }

        if (stopRunningCoroutine && runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
        }

        if (_activeSkillToken == activeFieldToken)
        {
            _activeRuntimeSkill = null;
            _activeSkillToken = default;
            _activeSkillTableManager = null;
            _activeSkillKitchenSystem = null;
            _activeSkillCustomerController = null;
            _usingSkill = false;
            _useSkillRoutine = null;
        }

        bool keepFeverEffect = reason == StaffSkillCancellationReason.NormalDurationCompleted
            && !_isApplicationQuitting
            && gameObject != null
            && gameObject.activeInHierarchy
            && _feverSystem != null
            && _feverSystem.IsFeverStart;
        SkillEffectSetActive(keepFeverEffect);

        if (deactivateException != null)
        {
            DebugLog.LogError(
                "[Staff Skill] Deactivate 오류 후 Registry와 Context를 정리했습니다: "
                + reason
                + "\n"
                + deactivateException);
        }
    }


    protected Coroutine _moveCoroutine;
    protected Coroutine _teleportCoroutine;

    protected Action _moveCompleted;
    protected Vector2 _targetPos;
    protected int _targetFloor;
    protected int _moveEndDir;
    protected bool _isStairsMove;

    protected virtual void CancelTeleportEffects()
    {
        if (_skillEffect != null) _skillEffect.TweenStop();
        if (_spriteRenderer != null) _spriteRenderer.TweenStop();
        SetAlpha(1f);
    }

    public void Move(Vector2 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
            CancelTeleportEffects();
        }

        _moveCompleted = onCompleted;
        RestaurantType type = RestaurantType.Hall;
        if (_staffType == EquipStaffType.Chef /*|| _staffType == EquipStaffType.Chef2*/)
            type = RestaurantType.Kitchen;

        Vector3 customerDoorPos = _tableManager.GetDoorPos(type, transform.position);
        Vector3 targetDoorPos = _tableManager.GetDoorPos(type, targetPos);
        _targetPos = targetPos;
        _moveEndDir = moveEndDir;

        bool isEqualPos = customerDoorPos.y == targetDoorPos.y;
        Vector3 pathPos = isEqualPos ? targetPos : customerDoorPos;
        AStar.Instance.RequestPath(_moveObj.transform.position, pathPos, isEqualPos ? TargetMove : StairsMove);
    }

    public void StopMove()
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
            // 텔레포트 중단 시 알파값 복구 (Tween도 중단)
            CancelTeleportEffects();
        }
    }


    protected void TargetMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _isStairsMove = false;
        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList));
    }


    protected virtual void StairsMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _isStairsMove = true;

        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList, () =>
        {
            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() =>
            {
                _spriteRenderer.color = Color.white;
                _skillEffect.color = Color.white;
                // 텔레포트 후 현재 위치에서 목적지로 경로 탐색
                AStar.Instance.RequestPath(_moveObj.transform.position, _targetPos, TargetMove);
            }));
        }
        ));
    }


    protected IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        // 현재 위치에서 너무 가까운 첫 노드들을 모두 제거 (순간이동 방지)
        Vector3 currentPos = _moveObj.transform.position;
        while (nodeList.Count > 1)
        {
            float dx = nodeList[0].x - currentPos.x;
            float dy = nodeList[0].y - currentPos.y;
            float distSqr = dx * dx + dy * dy;
            
            // 0.5 유닛 이내면 스킵
            if (distSqr < 0.25f)
                nodeList.RemoveAt(0);
            else
                break;
        }

        // nodeList가 비어있거나 모두 가까운 노드였을 경우 즉시 완료 처리
        if (nodeList.Count == 0)
        {
            SetStaffState(EStaffState.None);
            SetSpriteDir(_moveEndDir);
            onCompleted?.Invoke();
            
            if (!_isStairsMove)
            {
                _moveCompleted?.Invoke();
                _moveCompleted = null;
            }
            yield break;
        }

        SetStaffState(EStaffState.Run);
        
        Vector2 targetVec;
        float distanceSqr;
        float step;

        
        foreach (Vector2 vec in nodeList)
        {
            targetVec = vec;
            
            while (true)
            {
                currentPos = _moveObj.transform.position;
                
                // 거리 제곱 계산 (GC 없음)
                float dx = targetVec.x - currentPos.x;
                float dy = targetVec.y - currentPos.y;
                distanceSqr = dx * dx + dy * dy;
                
                if (distanceSqr <= 0.01f)
                    break;
                
                // 방향 계산 및 정규화
                float distance = Mathf.Sqrt(distanceSqr);
                float dirX = dx / distance;
                float dirY = dy / distance;
                
                SetSpriteDir(dirX);
                
                step = Time.deltaTime * _moveSpeed * MoveSpeedMul;
                
                // MoveTowards 직접 구현 (GC 없음)
                if (distance > step)
                {
                    currentPos.x += dirX * step;
                    currentPos.y += dirY * step;
                }
                else
                {
                    currentPos.x = targetVec.x;
                    currentPos.y = targetVec.y;
                }
                
                _moveObj.transform.position = currentPos;
                yield return null;
            }

            _moveObj.transform.position = new Vector3(targetVec.x, targetVec.y, currentPos.z);
        }

        SetStaffState(EStaffState.None);
        SetSpriteDir(_moveEndDir);
        onCompleted?.Invoke();

        if (_isStairsMove)
            yield break;

        _moveCompleted?.Invoke();
        _moveCompleted = null;
    }


    protected IEnumerator TeleportFloorRoutine(Action onCompleted)
    {
        yield return YieldCache.WaitForSeconds(0.6f);
        
        if (!gameObject.activeInHierarchy)
        {
            SetAlpha(1f);
            yield break;
        }
        
        RestaurantType type = RestaurantType.Hall;
        if (_staffType == EquipStaffType.Chef /*|| _staffType == EquipStaffType.Chef2*/)
            type = RestaurantType.Kitchen;
        
        Vector3 doorPos = _tableManager.GetDoorPos(type, _targetPos);
        TweenAlpha(0, 0.4f, Ease.Constant, () => {
            _moveObj.transform.position = doorPos;
            SetAlpha(0f);
            });
        yield return YieldCache.WaitForSeconds(1f);
        SetAlpha(0f);
        if (!gameObject.activeInHierarchy)
        {
            SetAlpha(1f);
            yield break;
        }
        
        TweenAlpha(1, 0.4f, Ease.Constant, () => SetAlpha(1f));
        yield return YieldCache.WaitForSeconds(1f);
        SetAlpha(1f);
        if (_skillEffect != null)
            _skillEffect.color = Color.white;
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
        
        onCompleted?.Invoke();
    }


    private void OnChangeSkillValueEvent()
    {
        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        _skillCoolTime = _staffData.Skill.Cooldown;
        _skillCoolTime = _skillCoolTime < 0.1f ? 0.1f : _skillCoolTime;
    }

    protected virtual void SkillEffectSetActive(bool isActive)
    {
        if (_skillEffect == null)
            return;

        _skillEffect.gameObject.SetActive(isActive);
    }

    protected IEnumerator IdleSpriteCoroutine()
    {
        if (_staffData == null || _idleSprites == null || _idleSprites.Length == 0)
            yield break;

        for (int i = 0, cnt = _idleSprites.Length; i < cnt; ++i)
        {
            _spriteRenderer.sprite = _idleSprites[i];
            DebugLog.Log($"[{name}] Idle 애니메이션 - 스프라이트 변경: {_idleSprites[i].name} ({i + 1}/{cnt})");
            yield return YieldCache.WaitForSeconds(0.1f);
        }

        _spriteRenderer.sprite = _sprite;
    }

    protected IEnumerator IdleAnimationRoutine()
    {
        while (true)
        {
            // 10~20초 사이 랜덤 대기
            float waitTime = UnityEngine.Random.Range(5f, 10f);
            yield return YieldCache.WaitForSeconds(waitTime);

            // 여전히 Idle 상태인지 확인
            if (_state == EStaffState.None && _staffData != null)
            {
                yield return StartCoroutine(IdleSpriteCoroutine());
            }
        }
    }

    protected void StartIdleAnimation()
    {
        StopIdleAnimation();
        if (_idleSprites != null && _idleSprites.Length > 0)
        {
            DebugLog.Log($"[{name}] Idle 애니메이션 시작 - {_idleSprites.Length}개의 스프라이트");
            _idleAnimationRoutine = StartCoroutine(IdleAnimationRoutine());
        }
        else
        {
            DebugLog.LogError($"[{name}] Idle 애니메이션을 시작할 수 없음 - IdleSprites: {(_idleSprites == null ? "null" : "empty")}");
        }
    }

    protected void StopIdleAnimation()
    {
        if (_idleAnimationRoutine != null)
        {
            StopCoroutine(_idleAnimationRoutine);
            _idleAnimationRoutine = null;
        }
    }


    private void OnLevelUpEvent()
    {
        if (_staffData == null)
            return;

        _moveSpeed = _staffData.GetSpeed(Level);
        OnLevelUpEventHandler?.Invoke();
    }

    public void ObjectPoolSpawnEvent()
    {
        LoadingSceneManager.OnLoadSceneHandler += OnChangeSceneEvent;

        if (_gameManager == null
            && GameManager.TryGetExistingInstance(out GameManager existingGameManager))
        {
            _gameManager = existingGameManager;
            _skillEffectRegistry = existingGameManager.StaffSkillEffectRegistry;
        }

        if (_gameManager != null)
        {
            _gameManager.OnChangeStaffSkillValueHandler += OnChangeSkillValueEvent;
        }
        else
        {
            DebugLog.LogWarning("[Staff Skill] 기존 GameManager가 없어 Skill 이벤트 구독을 건너뜁니다: " + name);
        }

        UserInfo.OnUpgradeStaffHandler += OnLevelUpEvent;
    }

    public void ObjectPoolDespawnEvent()
    {
        CancelActiveSkill(StaffSkillCancellationReason.ObjectPoolDespawned, true);

        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
        if (_gameManager != null)
        {
            _gameManager.OnChangeStaffSkillValueHandler -= OnChangeSkillValueEvent;
        }

        UserInfo.OnUpgradeStaffHandler -= OnLevelUpEvent;
    }


    private void OnChangeSceneEvent()
    {
        CancelActiveSkill(StaffSkillCancellationReason.SceneChanged, true);

        if (_staffData != null)
        {
            _staffData.RemoveSlot(this, _tableManager, _kitchenSystem, _customerController);
        }

        StopAllCoroutines();
        StopIdleAnimation();
        _staffData = null;
        _staffAction = null;
        ObjectPoolManager.Instance.DespawnStaff(_staffType, this);
    }

    private void OnStartFeverEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        SkillEffectSetActive(true);
    }

    private void OnEndFeverEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_usingSkill)
            return;

        SkillEffectSetActive(false);
    }

    protected virtual void OnChangeSkinEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_staffData == null)
            return;

        DebugLog.Log("스탭 스킨 변경: " + name + " - " + _staffData.Id);

        StaffSkinData data = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, _staffData);
        if (data == null)
        {
            _sprite = _staffData.Sprite;
            _idleSprites = _staffData.IdleSprites;
            DebugLog.Log($" - 기본 스킨 사용: IdleSprites {(_idleSprites != null ? _idleSprites.Length.ToString() : "null")}개");
            return;
        }

        _sprite = data.Sprite;
        _idleSprites = data.IdleSprites;
        DebugLog.Log($" - 커스텀 스킨 적용 ({data.Id}): IdleSprites {(_idleSprites != null ? _idleSprites.Length.ToString() : "null")}개");

        if (_state == EStaffState.None)
        {
            _spriteRenderer.sprite = _sprite;
        }

        SetStaffState(_state);
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
        CancelActiveSkill(StaffSkillCancellationReason.ApplicationQuitting, true);
    }

    protected virtual void OnDestroy()
    {
        StaffSkillCancellationReason cancellationReason = _isApplicationQuitting
            ? StaffSkillCancellationReason.ApplicationQuitting
            : StaffSkillCancellationReason.ComponentDestroyed;
        CancelActiveSkill(cancellationReason, true);

        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
        if (_gameManager != null)
        {
            _gameManager.OnChangeStaffSkillValueHandler -= OnChangeSkillValueEvent;
        }

        UserInfo.OnUpgradeStaffHandler -= OnLevelUpEvent;
        UserInfo.OnChangeStaffSkinHandler -= OnChangeSkinEvent;
        if (_feverSystem != null)
        {
            _feverSystem.OnStartFeverHandler -= OnStartFeverEvent;
            _feverSystem.OnEndFeverHandler -= OnEndFeverEvent;
        }
    }

}
