using Muks.PathFinding.AStar;
using System;
using UnityEngine;

public class Staff : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    public static Action OnStaffActionHandler;

    public bool _actionEnabled;
    private StaffData _staffData;
    private int _level = 1;
    private float _scaleX;
    private float _actionTimer;

    private EStaffType _staffType;
    public EStaffType StaffType => _staffType;

    private void StaffAction(int index)
    {

        OnStaffActionHandler?.Invoke();
    }


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
        _staffData.Update();


        if(_actionTimer <= 0)
        {
            _actionEnabled = true;
        }
        else
        {
            _actionTimer -= Time.deltaTime;
            _actionEnabled = false;
        }
    }

}

