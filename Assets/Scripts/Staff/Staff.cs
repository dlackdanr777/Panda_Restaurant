using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;



public class Staff : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private GameObject _spriteParent;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    [Header("Cleaner Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _cleanSound;
    [SerializeField] private GameObject _cleanerItem;
    [SerializeField] private GameObject _cleanParticle;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _skillActiveSound;


    private StaffData _staffData;
    private StaffType _staffType;
    private IStaffAction _staffAction;
    private EStaffState _state;

    private bool _usingSkill;
    private float _skillTimer;
    private float _skillCoolTime;
    public int Level => _staffData != null ? UserInfo.GetStaffLevel(_staffData) : 0;
    private float _scaleX;
    private float _actionTimer;
    private float _secondValue;
    public float SecondValue => _secondValue;

    private float _speedMul;
    public float SpeedMul => 1 + _speedMul;

    private Coroutine _useSkillRoutine;


    public void Init()
    {
        _scaleX = transform.localScale.x;
        GameManager.Instance.OnChangeStaffSkillValueHandler += OnChangeSkillValueEvent;
        gameObject.SetActive(false);
    }

    public float GetActionValue()
    {
        if(_staffData == null)
        {
            throw new Exception("현재 스탭 데이터가 null입니다.");
        }

        int level = UserInfo.GetStaffLevel(_staffData);
        if(level <= 0)
        {
            throw new Exception("현재 스탭 데이터를 보유하고 있지 않습니다: " + _staffData.Id);
        }

        return _staffData.GetActionValue(level) / SpeedMul;
    }



    public void SetStaffData(StaffData staffData, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (staffData == _staffData)
            return;

        if (_staffData != null)
            _staffData.RemoveSlot(this, tableManager, kitchenSystem, customerController);

        if (_useSkillRoutine != null)
            StopCoroutine(_useSkillRoutine);

        if(_usingSkill)
            _staffData.Skill.Deactivate(this, tableManager, kitchenSystem, customerController);

        if (staffData == null)
        {
            _staffData = null;
            _staffAction = null;
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _staffData = staffData;
        _staffData.AddSlot(this, tableManager, kitchenSystem, customerController);
        _staffAction = staffData.GetStaffAction(this, tableManager, kitchenSystem, customerController);
        _secondValue = staffData.SecondValue;
        _animator.enabled = staffData is CleanerData;
        _cleanerItem.gameObject.SetActive(staffData is CleanerData);
        _cleanParticle.gameObject.SetActive(false);
        _speedMul = 0;
        _usingSkill = false;
        _skillTimer = 0;
        _spriteRenderer.enabled = false;
        _spriteRenderer.sprite = staffData.Sprite;
        _spriteParent.transform.localPosition = new Vector3(0, -(AStar.Instance.NodeSize * 0.5f), 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.enabled = true;

        _staffType = staffData switch
        {
            ManagerData => StaffType.Manager,
            MarketerData => StaffType.Marketer,
            WaiterData => StaffType.Waiter,
            ServerData => StaffType.Server,
            CleanerData => StaffType.Cleaner,
            GuardData => StaffType.Guard,
            ChefData => StaffType.Chef,
            _ => StaffType.Length
        };

        OnChangeSkillValueEvent();
        ResetAction();
    }

    public void SetAlpha(float alpha)
    {
        Color nowColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(nowColor.r, nowColor.g, nowColor.b, alpha);
    }

    public void AddSpeedMul(float value)
    {
        _speedMul = Mathf.Clamp(_speedMul + value * 0.01f, 0f, 10f);
    }

    public void ResetAction()
    {
        _actionTimer = _staffData.GetActionValue(Level);
        _state = EStaffState.None;
    }

    public void PlayCleanSound()
    {
        _audio.PlayOneShot(_cleanSound);
    }

    public void StopSound()
    {
        if (!_audio.isPlaying)
            return;

        _audio.Stop();
    }


    public void SetStaffState(EStaffState state)
    {
        _state = state;

        if(_animator.enabled)
            _animator.SetInteger("State", (int)_state);
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
    }

    public void SetOrderLayer(int orderInLayer)
    {
        _spriteRenderer.sortingOrder = orderInLayer;
    }

    public void StaffAction()
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
        {
            DebugLog.Log("튜토리얼 진행중");
            return;
        }   

        if(_staffAction == null)
            return;

        if (_state != EStaffState.ActionEnable)
            return;

        _staffAction.PerformAction(this);
    }


    public void UsingStaffSkill(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
        {
            DebugLog.Log("튜토리얼 진행중");
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


    private void Update()
    {
        UpdateAction();
    }

    private void UpdateAction()
    {
        if (_staffData == null)
            return;

        if (_state != EStaffState.None)
            return;

        if (_actionTimer <= 0)
        {
            _state = EStaffState.ActionEnable;
        }
        else
        {
            _actionTimer -= Time.deltaTime * SpeedMul;
        }
    }

    private IEnumerator UseSkillCoroutine(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _usingSkill = true;
        Vibration.Vibrate(500);
        //SoundManager.Instance.PlayEffectAudio(_skillActiveSound);
        _staffData.Skill.Activate(this, tableManager, kitchenSystem, customerController);

        float duration = _staffData.Skill.Duration + GameManager.Instance.AddStaffSkillTime + _staffType switch
        {
            StaffType.Marketer => GameManager.Instance.AddMarketerSkillTime,
            StaffType.Waiter => GameManager.Instance.AddWaiterSkillTime,
            StaffType.Server => GameManager.Instance.AddServerSkillTime,
            StaffType.Cleaner => GameManager.Instance.AddCleanerSkillTime,
            StaffType.Guard => GameManager.Instance.AddGuardSkillTime,
            _ => 0
        };
        float timer = 0;
        while (timer < duration)
        {
            _staffData.Skill.ActivateUpdate(this, tableManager, kitchenSystem, customerController);
            timer += 0.02f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _staffData.Skill.Deactivate(this, tableManager, kitchenSystem, customerController);
        _usingSkill = false;
    }


    private Coroutine _moveCoroutine;
    private Coroutine _teleportCoroutine;

    private Action _moveCompleted;
    private Vector2 _targetPos;
    private int _targetFloor;
    private int _moveEndDir;
    private bool _isStairsMove;

    public void Move(Vector2 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        int moveObjFloor = AStar.Instance.GetTransformFloor(_moveObj.transform.position);
        _targetFloor = AStar.Instance.GetTransformFloor(targetPos);
        _targetPos = targetPos;
        _moveEndDir = moveEndDir;
        _moveCompleted = onCompleted;

        if (moveObjFloor == _targetFloor)
            AStar.Instance.RequestPath(_moveObj.transform.position, targetPos, TargetMove);

        else
            AStar.Instance.RequestPath(_moveObj.transform.position, AStar.Instance.GetFloorPos(moveObjFloor), StairsMove);
    }

    public void StopMove()
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

    }


    private void TargetMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _isStairsMove = false;
        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList));
    }


    private void StairsMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _isStairsMove = true;

        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList, () =>
        {
            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() => AStar.Instance.RequestPath(AStar.Instance.GetFloorPos(_targetFloor), _targetPos, TargetMove)));
        }
        ));
    }


    private IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        if (2 <= nodeList.Count)
            nodeList.RemoveAt(0);

        SetStaffState(EStaffState.Run);
        foreach (Vector2 vec in nodeList)
        {
            while (Vector3.Distance(_moveObj.transform.position, vec) > 0.1f)
            {
                float step = 0.02f * 5 * SpeedMul; // 이동 거리 제한
                _moveObj.transform.position = Vector2.MoveTowards(_moveObj.transform.position, vec, step);
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                SetSpriteDir(dir.x);
                yield return YieldCache.WaitForSeconds(0.01f);
            }

            // 목표 지점에 정확히 도달하도록 위치 보정
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


    private IEnumerator TeleportFloorRoutine(Action onCompleted)
    {
        yield return YieldCache.WaitForSeconds(1);
        _moveObj.transform.position = AStar.Instance.GetFloorPos(_targetFloor);
        SetSpriteDir(1);
        yield return YieldCache.WaitForSeconds(1);
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
            StaffType.Marketer => GameManager.Instance.SubMarketerSkillCoolTime,
            StaffType.Waiter => GameManager.Instance.SubWaiterSkillCoolTime,
            StaffType.Server => GameManager.Instance.SubServerSkillCoolTime,
            StaffType.Cleaner => GameManager.Instance.SubCleanerSkillCoolTime,
            StaffType.Guard => GameManager.Instance.SubGuardSkillCoolTime,
            _ => 0
        };

        _skillCoolTime = _skillCoolTime < 0.1f ? 0.1f : _skillCoolTime;
    }
}

