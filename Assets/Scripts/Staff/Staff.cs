using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Muks.Tween;

public class Staff : MonoBehaviour
{
    public event Action OnLevelUpEventHandler;
    [SerializeField] protected GameObject _moveObj;
    [SerializeField] protected GameObject _spriteParent;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    [Space]
    [Header("Skill")]
    [SerializeField] private SpriteRenderer _skillEffect;
    [SerializeField] private AudioClip _skillActiveSound;

    protected TableManager _tableManager;
    protected KitchenSystem _kitchenSystem;
    protected CustomerController _customerController;
    protected FeverSystem _feverSystem;

    protected StaffData _staffData;
    public StaffData StaffData => _staffData;

    protected EquipStaffType _staffType;
    public EquipStaffType EquipStaffType => _staffType;
    protected IStaffAction _staffAction;
    protected EStaffState _state;
    protected ERestaurantFloorType _equipFloorType;
    public ERestaurantFloorType EquipFloorType => _equipFloorType;

    protected bool _usingSkill;
    protected float _skillTimer;
    protected float _skillCoolTime;
    public int Level => _staffData != null ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, _staffData) : 1;

    protected float _scaleX;
    protected float _moveSpeed;
    protected float _speedMul;
    public float SpeedMul => Mathf.Clamp((1 + _speedMul) * GameManager.Instance.AddStaffSpeedMul, 0.5f, 3f);

    protected Coroutine _useSkillRoutine;


    public virtual void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        _staffType = type;
        _tableManager = tableManager;
        _customerController = customerController;
        _kitchenSystem = kitchenSystem;
        _feverSystem = feverSystem;
        _tableManager = tableManager;
        _spriteRenderer.color = Color.white;
        _scaleX = transform.localScale.x;
        GameManager.Instance.OnChangeStaffSkillValueHandler += OnChangeSkillValueEvent;
        UserInfo.OnUpgradeStaffHandler += OnLevelUpEvent;
        _feverSystem.OnStartFeverHandler += OnStartFeverEvent;
        _feverSystem.OnEndFeverHandler += OnEndFeverEvent;
        gameObject.SetActive(false);
    }

    public float GetActionValue()
    {
        if(_staffData == null)
        {
            throw new Exception("현재 스탭 데이터가 null입니다.");
        }

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _staffData);
        if(level <= 0)
        {
            throw new Exception("현재 스탭 데이터를 보유하고 있지 않습니다: " + _staffData.Id);
        }

        return _staffData.GetActionValue(level);
    }



    public virtual void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        StopAllCoroutines();
        SkillEffectSetActive(false);
        if (staffData == _staffData)
            return;

        if (_staffData != null)
        {
            _staffData.RemoveSlot(this, _tableManager, _kitchenSystem, _customerController);

            if (_usingSkill)
                _staffData.Skill.Deactivate(this, _tableManager, _kitchenSystem, _customerController);

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
        _staffData.AddSlot(this, _tableManager, _kitchenSystem, _customerController);
        _moveSpeed = staffData.GetSpeed(Level);
        _speedMul = 0;
        _usingSkill = false;
        _skillTimer = 0;
        _spriteRenderer.enabled = false;
        _spriteRenderer.sprite = staffData.Sprite;
        _spriteParent.transform.localPosition = new Vector3(0, -(AStar.Instance.NodeSize * 0.5f), 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.enabled = true;

        _staffAction = staffData.GetStaffAction(this, _tableManager, _kitchenSystem, _customerController);

        OnChangeSkillValueEvent();
    }

    public void SetAlpha(float alpha)
    {
        Color nowColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(nowColor.r, nowColor.g, nowColor.b, alpha);
    }

    public virtual void TweenAlpha(float alpha, float duration, Ease ease, Action onCompleted = null)
    {
        _skillEffect?.TweenAlpha(alpha, duration, ease);
        _spriteRenderer.TweenAlpha(alpha, duration, ease).OnComplete(onCompleted);
    }


    public void AddSpeedMul(float value)
    {
        _speedMul = Mathf.Clamp(_speedMul + value * 0.01f, 0f, 10f);
    }

    public virtual void SetStaffState(EStaffState state)
    {
        _state = state;
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

        if(_skillEffect != null)
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
        if(_staffAction == null)
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

        if (_usingSkill)
        {
            Debug.Log("스킬이 이미 사용중 입니다.");
            return;
        }

        if (_skillCoolTime <= _skillTimer)
        {
            _skillTimer = 0;
            _useSkillRoutine = StartCoroutine(UseSkillCoroutine(tableManager, kitchenSystem, customerController));
        }
        else
        {
            _skillTimer += Time.deltaTime * SpeedMul;
        }
    }


    public void DestroyStaff()
    {
        _staffData?.Destroy();
    }

    private void OnDisable()
    {
        if(transform != null)
            transform.TweenStop();

        if (_spriteRenderer != null)
            _spriteRenderer.TweenStop();
    }


    private IEnumerator UseSkillCoroutine(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _usingSkill = true;
        Vibration.Vibrate(500);
        SkillEffectSetActive(true);
        SoundManager.Instance.PlayEffectAudio(EffectType.Restaurant, _skillActiveSound);
        _staffData.Skill.Activate(this, tableManager, kitchenSystem, customerController);

        float duration = _staffData.Skill.Duration + GameManager.Instance.AddStaffSkillTime + _staffType switch
        {
            EquipStaffType.Marketer => GameManager.Instance.AddMarketerSkillTime,
            EquipStaffType.Waiter1 => GameManager.Instance.AddWaiterSkillTime,
            EquipStaffType.Waiter2 => GameManager.Instance.AddServerSkillTime,
            EquipStaffType.Cleaner => GameManager.Instance.AddCleanerSkillTime,
            EquipStaffType.Guard => GameManager.Instance.AddGuardSkillTime,
            _ => 0
        };
        float timer = 0;
        while (timer < duration)
        {
            _staffData.Skill.ActivateUpdate(this, tableManager, kitchenSystem, customerController);
            timer += 0.02f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        SkillEffectSetActive(_feverSystem.IsFeverStart);
        _staffData.Skill.Deactivate(this, tableManager, kitchenSystem, customerController);
        _usingSkill = false;
    }


    protected Coroutine _moveCoroutine;
    protected Coroutine _teleportCoroutine;

    protected Action _moveCompleted;
    protected Vector2 _targetPos;
    protected int _targetFloor;
    protected int _moveEndDir;
    protected bool _isStairsMove;

    public void Move(Vector2 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _moveCompleted = onCompleted;
        RestaurantType type = RestaurantType.Hall;
        if (_staffType == EquipStaffType.Chef1 || _staffType == EquipStaffType.Chef2)
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
            StopCoroutine(_teleportCoroutine);

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
            RestaurantType type = RestaurantType.Hall;
            if (_staffType == EquipStaffType.Chef1 || _staffType == EquipStaffType.Chef2)
                type = RestaurantType.Kitchen;

            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() => AStar.Instance.RequestPath(_tableManager.GetDoorPos(type, _targetPos), _targetPos, TargetMove)));
        }
        ));
    }


    protected IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        if (1 < nodeList.Count)
            nodeList.RemoveAt(0);

        SetStaffState(EStaffState.Run);
        foreach (Vector2 vec in nodeList)
        {
            while ((vec - (Vector2)_moveObj.transform.position).sqrMagnitude > 0.01f)
            {
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                SetSpriteDir(dir.x);
                float step = Time.deltaTime * _moveSpeed * SpeedMul;
                _moveObj.transform.position = Vector2.MoveTowards(_moveObj.transform.position, vec, step);
                yield return null;
            }

            _moveObj.transform.position = vec;
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
        RestaurantType type = RestaurantType.Hall;
        if (_staffType == EquipStaffType.Chef1 || _staffType == EquipStaffType.Chef2)
            type = RestaurantType.Kitchen;
        TweenAlpha(0, 0.4f, Ease.Constant, () => _moveObj.transform.position = _tableManager.GetDoorPos(type, _targetPos));
        yield return YieldCache.WaitForSeconds(1f);
        TweenAlpha(1, 0.4f, Ease.Constant);
        yield return YieldCache.WaitForSeconds(1f);
        onCompleted?.Invoke();
    }


    private void OnChangeSkillValueEvent()
    {
        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        _skillCoolTime = _staffData.Skill.Cooldown + GameManager.Instance.SubStaffSkillCoolTime + _staffType switch
        {
            EquipStaffType.Marketer => GameManager.Instance.SubMarketerSkillCoolTime,
            EquipStaffType.Waiter1 => GameManager.Instance.SubWaiterSkillCoolTime,
            EquipStaffType.Waiter2 => GameManager.Instance.SubServerSkillCoolTime,
            EquipStaffType.Cleaner => GameManager.Instance.SubCleanerSkillCoolTime,
            EquipStaffType.Guard => GameManager.Instance.SubGuardSkillCoolTime,
            _ => 0
        };

        _skillCoolTime = _skillCoolTime < 0.1f ? 0.1f : _skillCoolTime;
    }

    protected virtual void SkillEffectSetActive(bool isActive)
    {
        if (_skillEffect == null)
            return;

        _skillEffect.gameObject.SetActive(isActive);
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
        GameManager.Instance.OnChangeStaffSkillValueHandler += OnChangeSkillValueEvent;
        UserInfo.OnUpgradeStaffHandler += OnLevelUpEvent;
    }

    public void ObjectPoolDespawnEvent()
    {

        LoadingSceneManager.OnLoadSceneHandler -= OnChangeSceneEvent;
        GameManager.Instance.OnChangeStaffSkillValueHandler -= OnChangeSkillValueEvent;
        UserInfo.OnUpgradeStaffHandler -= OnLevelUpEvent;
    }


    private void OnChangeSceneEvent()
    {
        if (_staffData != null)
        {
            _staffData.RemoveSlot(this, _tableManager, _kitchenSystem, _customerController);

            if(_staffData.Skill != null && _usingSkill)
                _staffData.Skill.Deactivate(this, _tableManager, _kitchenSystem, _customerController);
        }

        StopAllCoroutines();
        _staffData = null;
        _staffAction = null;
        ObjectPoolManager.Instance.DespawnStaff(_staffType, this);
    }

    private void OnStartFeverEvent()
    {
        if(!gameObject.activeInHierarchy)
            return;

        SkillEffectSetActive(true);
    }

    private void OnEndFeverEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if(_usingSkill)
            return;

        SkillEffectSetActive(false);
    }

}

