using Muks.PathFinding.AStar;
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

    protected CustomerState _currentState;
    protected Coroutine _moveCoroutine;
    protected Coroutine _teleportCoroutine;
    protected Action _moveCompleted;
    protected Vector2 _targetPos;
    protected int _targetFloor;
    protected int _moveEndDir;
    protected float _moveSpeed;
    protected bool _isStairsMove;
    private List<Vector2> _path;


    public virtual void Init()
    {

    }


    public virtual void SetData(CustomerData data)
    {
        _moveSpeed = data.MoveSpeed;

        _customerData = data;
        _spriteParent.localScale = data.Scale <= 0 ? Vector3.one : Vector3.one * data.Scale;
        _spriteParent.transform.localPosition = new Vector3(0, -AStar.Instance.NodeSize * 2, 0);
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.sprite = data.Sprite;
        _spriteRenderer.color = Color.white;
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
        int moveObjFloor = AStar.Instance.GetTransformFloor(_moveObj.transform.position);
        _targetFloor = AStar.Instance.GetTransformFloor(targetPos);
        _targetPos = targetPos;
        _moveEndDir = moveEndDir;
        _moveCompleted = onCompleted;
        AStar.Instance.RequestPath(_moveObj.transform.position, moveObjFloor == _targetFloor ? targetPos : AStar.Instance.GetFloorPos(moveObjFloor), moveObjFloor == _targetFloor ? TargetMove : StairsMove);
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
            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() => AStar.Instance.RequestPath(AStar.Instance.GetFloorPos(_targetFloor), _targetPos, TargetMove)));
        }
        ));
    }


    private IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        _path = nodeList;

        if (2 <= nodeList.Count)
            nodeList.RemoveAt(0);


        foreach (Vector2 vec in nodeList)
        {
            while (Vector3.Distance(_moveObj.transform.position, vec) > 0.1f)
            {
                float step = 0.01f * _moveSpeed; // 이동 거리 제한
                _moveObj.transform.position = Vector2.MoveTowards(_moveObj.transform.position, vec, step);
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                SetSpriteDir(dir.x);
                ChangeState(CustomerState.Run);
                yield return YieldCache.WaitForSeconds(0.01f);
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
        yield return YieldCache.WaitForSeconds(1);
        _moveObj.transform.position = AStar.Instance.GetFloorPos(_targetFloor);
        SetSpriteDir(1);
        yield return YieldCache.WaitForSeconds(1);
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
