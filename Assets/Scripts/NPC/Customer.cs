using Muks.PathFinding.AStar;
using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum CustomerState
{
    Idle,
    Run,
    Sit,
    Eat,
    Action,
    Length
}


public class Customer : MonoBehaviour
{
    [SerializeField] private bool _drawPath;

    [Space]
    [Header("Components")]
    [SerializeField] protected GameObject _moveObj;
    [SerializeField] protected Animator _animator;
    [SerializeField] protected Transform _spriteParent;
    [SerializeField] protected SpriteRenderer _spriteRenderer;

    protected CustomerData _customerData;
    public CustomerData CustomerData => _customerData;

    protected ERestaurantFloorType _visitFloor;
    public ERestaurantFloorType VisitFloor => _visitFloor;

    protected CustomerState _currentState;
    protected Coroutine _moveCoroutine;
    protected Coroutine _teleportCoroutine;
    protected Action _moveCompleted;
    protected Vector2 _targetPos;
    protected int _targetFloor;
    protected int _moveEndDir;
    protected float _moveSpeed;
    protected bool _isStairsMove;
    protected List<Vector2> _path;

    protected CustomerController _customerController;
    protected TableManager _tableManager;



    public virtual void Init()
    {

    }


    public virtual void SetData(CustomerData data, CustomerController customerController, TableManager tableManager)
    {
        _customerData = data;
        _customerController = customerController;
        _tableManager = tableManager;
        _moveSpeed = data.MoveSpeed;

        _spriteParent.localScale = Vector3.one;
        _spriteParent.transform.localPosition = new Vector3(0, -AStar.Instance.NodeSize * 2, 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.sprite = data.Sprite;
        _spriteRenderer.color = Color.white;
    }

    public void SetVisitFloor(ERestaurantFloorType floor)
    {
        _visitFloor = floor;
    }


    public virtual void SetSpriteDir(float dir)
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

    public void Move(Vector2 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _moveCompleted = onCompleted;

        Vector3 customerDoorPos = _tableManager.GetDoorPos(RestaurantType.Hall, transform.position);
        Vector3 targetDoorPos = _tableManager.GetDoorPos(RestaurantType.Hall, targetPos);
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

        ChangeState(CustomerState.Idle);
    }


    public virtual void ChangeState(CustomerState state)
    {
        if (_currentState == state)
            return;

        _currentState = state;
        _animator.SetInteger("State", (int)_currentState);

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
            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() => AStar.Instance.RequestPath(_tableManager.GetDoorPos(RestaurantType.Hall, _targetPos), _targetPos, TargetMove)));
        }
        ));
    }


    protected virtual IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        _path = nodeList;

        if (1 < nodeList.Count)
            nodeList.RemoveAt(0);

        _spriteRenderer.color = Color.white;
        foreach (Vector2 vec in nodeList)
        {
            while ((vec - (Vector2)_moveObj.transform.position).sqrMagnitude > 0.01f) // 제곱 거리 비교
            {
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                SetSpriteDir(dir.x);
                float step = Time.deltaTime * _moveSpeed * 0.7f; // 프레임 독립적 이동 속도
                _moveObj.transform.position = Vector2.MoveTowards(_moveObj.transform.position, vec, step);
                ChangeState(CustomerState.Run);
                yield return null; // 프레임마다 실행
            }
        }

        ChangeState(CustomerState.Idle);
        SetSpriteDir(_moveEndDir);
        onCompleted?.Invoke();

        if (_isStairsMove)
            yield break;

        _moveCompleted?.Invoke();
        _moveCompleted = null;
    }


    private IEnumerator TeleportFloorRoutine(Action onCompleted)
    {
        yield return YieldCache.WaitForSeconds(0.6f);
        _spriteRenderer.TweenAlpha(0, 0.4f, Ease.Constant).OnComplete(() => _moveObj.transform.position = _tableManager.GetDoorPos(RestaurantType.Hall, _targetPos));
        //SetSpriteDir(-1);
        yield return YieldCache.WaitForSeconds(1f);
        _spriteRenderer.TweenAlpha(1, 0.4f, Ease.Constant);
        yield return YieldCache.WaitForSeconds(1f);
        onCompleted?.Invoke();
    }

    private void OnDrawGizmos()
    {
        if (!_drawPath)
            return;

        // 경로를 그리는 코드
        if (_path != null && _path.Count > 0)
        {
            Gizmos.color = Color.blue;

            for (int i = 0; i < _path.Count - 1; i++)
            {
                Gizmos.DrawLine(_path[i], _path[i + 1]);
            }
        }
    }
}
