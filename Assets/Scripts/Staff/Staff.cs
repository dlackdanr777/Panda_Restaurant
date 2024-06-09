using Muks.PathFinding.AStar;
using System;
using UnityEditor.Rendering;
using UnityEngine;



public class Staff : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    private StaffData _staffData;
    private IStaffAction _staffAction;
    private EStaffState _state;
    private int _level = 1;
    private float _scaleX;
    private float _actionTimer;
    private float _speed;
    public float Speed => _speed;


    public void SetStaffData(StaffData staffData, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (_staffData != null)
            _staffData.RemoveSlot(this, tableManager, kitchenSystem, customerController);

        _staffData = staffData;
        _staffAction = staffData.GetStaffAction(tableManager, kitchenSystem, customerController);
        _speed = staffData.Speed;
        _level = 1;

        _spriteRenderer.sprite = staffData.Sprite;
        float heightMul = staffData.Sprite.bounds.size.y * 0.5f - AStar.Instance.NodeSize;
        _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);

        _staffData.AddSlot(this, tableManager, kitchenSystem, customerController);
        ResetAction();
    }

    public void SetAlpha(float alpha)
    {
        Color nowColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(nowColor.r, nowColor.g, nowColor.b, alpha);
    }

    public void ResetAction()
    {
        _actionTimer = _staffData.GetActionValue(_level);
        _state = EStaffState.None;
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

    public void StaffAction()
    {
        if(_staffAction == null)
        {
            Debug.LogError("직원 행동 데이터가 존재하지 않습니다.");
            return;
        }

        if (_state != EStaffState.ActionEnable)
            return;

        _staffAction.PerformAction(this);
    }


    private void Start()
    {
        _scaleX = transform.localScale.x;
    }


    private void Update()
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
            _actionTimer -= Time.deltaTime;
        }
    }
}

