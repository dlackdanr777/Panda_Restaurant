using Muks.PathFinding.AStar;
using System;
using UnityEngine;

public class Staff : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    public bool ActionEnabled;
    public bool IsUsed;
    private StaffData _staffData;
    private int _level = 1;
    private float _scaleX;
    private float _actionTimer;

    private EStaffType _staffType;
    public EStaffType StaffType => _staffType;


    public void SetStaffData(StaffData staffData)
    {
        _staffData = staffData;

        _spriteRenderer.sprite = staffData.Sprite;

        float heightMul = (float)staffData.Sprite.textureRect.height * 0.005f - AStar.Instance.NodeSize;
        _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);

        _level = 1;
        if (_staffData is ServerStaffData)
            _staffType = EStaffType.Server;
    }

    public void SetAlpha(float alpha)
    {
        Color nowColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(nowColor.r, nowColor.g, nowColor.b, alpha);
    }

    public void ResetAction()
    {
        _actionTimer = _staffData.GetActionTime(_level);
        ActionEnabled = false;
        IsUsed = false;
    }


    public void SetSpriteDir(float dir)
    {
        if (dir < 0) transform.localScale = new Vector3(_scaleX, transform.localScale.y, transform.localScale.z);
        else if (0 < dir) transform.localScale = new Vector3(-_scaleX, transform.localScale.y, transform.localScale.z);
    }

    private void Start()
    {
        _scaleX = transform.localScale.x;
    }


    private void Update()
    {
        if (_staffData == null)
            return;

        if(_actionTimer <= 0)
        {
            if (!IsUsed)
                ActionEnabled = true;
        }
        else
        {
            _actionTimer -= Time.deltaTime;
            ActionEnabled = false;
        }
    }



}

