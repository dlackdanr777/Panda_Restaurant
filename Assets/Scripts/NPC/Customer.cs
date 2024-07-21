using Muks.PathFinding.AStar;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

public class Customer : MonoBehaviour
{

    [Space]
    [Header("Components")]
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _spriteParent;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private Coroutine _moveCoroutine;
    private Coroutine _teleportCoroutine;

    private CustomerData _customerData;
    public CustomerData CustomerData => _customerData;

    private CustomerSkill _skill;

    private Action _moveCompleted;

    private Vector2 _targetPos;
    private int _targetFloor;
    private int _moveEndDir;
    private float _scaleX;
    private bool _isStairsMove;

    private int _orderCount = 1;
    public int OrderCount => _orderCount;

    private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;



    private void Start()
    {
        _scaleX = transform.localScale.x;
    }


    public void Init(CustomerData data)
    {
        _customerData = data;
        _animator.runtimeAnimatorController = data.AnimatorController;
        _spriteParent.transform.localPosition = new Vector3(0, -AStar.Instance.NodeSize * 2, 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.sprite = data.Sprite;
        _animator.SetBool("Run", false);
        _animator.SetBool("Eat", false);

        if (_skill != null)
            _skill.Deactivate(this);

        if(data.Skill != null)
        {
            _skill = data.Skill;
            data.Skill.Activate(this);
        }
        else
        {
            SetOrderCount(1);
            SetFoodPriceMul(1);
        }
    }


    public void SetSpriteDir(float dir)
    {
        if (dir < 0) _spriteRenderer.flipX = false;
        else if (0 < dir) _spriteRenderer.flipX = true;
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

    public void SetOrderCount(int value)
    {
        _orderCount = value;
    }

    public void SetFoodPriceMul(float value)
    {
        _foodPriceMul = value;
    }


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

        foreach (Vector2 vec in nodeList)
        {
            while (Vector3.Distance(_moveObj.transform.position, vec) > 0.05f)
            {
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                _moveObj.transform.Translate(dir * Time.deltaTime * _customerData.MoveSpeed, Space.World);

                SetSpriteDir(dir.x);
                _animator.SetBool("Run", true);
                yield return null;
            }
        }

        _animator.SetBool("Run", false);

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
}
