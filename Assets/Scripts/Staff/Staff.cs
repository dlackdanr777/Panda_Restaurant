using Muks.PathFinding.AStar;
using System.Collections;
using UnityEngine;



public class Staff : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    private StaffData _staffData;
    private IStaffAction _staffAction;
    private EStaffState _state;
    private bool _usingSkill;
    private bool _skillEnabled;

    private int _level = 1;
    public int Level => _level;
    private float _scaleX;
    private float _actionTimer;
    private float _skillTimer;
    private float _secondValue;
    public float SecondValue => _secondValue;
    private float _speed;
    public float Speed => _speed;

    private float _speedMul;
    public float SpeedMul => _speedMul;

    private Coroutine _useSkillRoutine;

    public void SetStaffData(StaffData staffData, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (_staffData != null)
            _staffData.RemoveSlot(this, tableManager, kitchenSystem, customerController);

        if (_useSkillRoutine != null)
            StopCoroutine(_useSkillRoutine);

        if(_usingSkill)
            _staffData.Skill.Deactivate(this, tableManager, kitchenSystem, customerController);

        if(staffData == null)
        {
            _staffData = null;
            _staffAction = null;
            return;
        }

        _staffData = staffData;
        _staffData.AddSlot(this, tableManager, kitchenSystem, customerController);
        _staffAction = staffData.GetStaffAction(tableManager, kitchenSystem, customerController);
        _secondValue = staffData.SecondValue;
        _speed = 1;
        _usingSkill = false;
        _skillEnabled = false;
        _level = 1;

        _spriteRenderer.sprite = staffData.Sprite;
        float heightMul = (staffData.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y - (AStar.Instance.NodeSize * 0.5f);
        _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);

        ResetAction();
        ResetSkill();
    }

    public void SetAlpha(float alpha)
    {
        Color nowColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(nowColor.r, nowColor.g, nowColor.b, alpha);
    }

    public void AddSpeedMul(float value)
    {
        _speedMul += value * 0.01f;
    }

    public void ResetAction()
    {
        _actionTimer = _staffData.GetActionValue(_level);
        _state = EStaffState.None;
    }

    public void ResetSkill()
    {
        _skillTimer = _staffData.Skill.Cooldown;
        _skillEnabled = false;
    }


    public void SetStaffState(EStaffState state)
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
    }

    public void SetOrderLayer(int orderInLayer)
    {
        _spriteRenderer.sortingOrder = orderInLayer;
    }

    public void StaffAction()
    {
        if(_staffAction == null)
            return;

        if (_state != EStaffState.ActionEnable)
            return;

        _staffAction.PerformAction(this);
    }

    public void UsingStaffSkill(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        if (!_skillEnabled)
            return;

        if (_usingSkill)
        {
            Debug.Log("스킬이 이미 사용중 입니다.");
            return;
        }

        ResetSkill();
        _useSkillRoutine = StartCoroutine(UseSkillCoroutine(tableManager, kitchenSystem, customerController));

    }

    public void UpdateStaffSkill(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        if (!_usingSkill)
            return;

        _staffData.Skill.ActivateUpdate(this, tableManager, kitchenSystem, customerController);
    }


    private void Start()
    {
        _scaleX = transform.localScale.x;
    }


    private void Update()
    {
        UpdateAction();
        UpdateSkill();
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
            _actionTimer -= Time.deltaTime * (_speed + _speedMul);
        }
    }

    private void UpdateSkill()
    {
        if (_staffData == null)
            return;

        if (_staffData.Skill == null)
            return;

        if (_skillTimer <= 0)
        {
            _skillEnabled = true;
        }
        else
        {
            _skillTimer -= Time.deltaTime;
        }
    }

    private IEnumerator UseSkillCoroutine(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _usingSkill = true;
        _staffData.Skill.Activate(this, tableManager, kitchenSystem, customerController);

        yield return YieldCache.WaitForSeconds(_staffData.Skill.Duration);

        _staffData.Skill.Deactivate(this, tableManager, kitchenSystem, customerController);
        _usingSkill = false;
    }
}

