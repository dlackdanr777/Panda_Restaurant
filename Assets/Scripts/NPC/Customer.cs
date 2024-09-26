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
    Jump,
    Action,
    Length
}


public class Customer : MonoBehaviour
{

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


    public void ChangeState(CustomerState state)
    {
        if (_currentState == state)
            return;

        _currentState = state;
        switch(_currentState)
        {
            case CustomerState.Idle:
                _animator.SetBool("Run", false);
                break;

            case CustomerState.Run:
                _animator.SetBool("Run", true);
                break;

            case CustomerState.Sit:
                _animator.SetTrigger("Sit");
                _animator.SetBool("Run", false);
                break;

            case CustomerState.Action:
                _animator.SetBool("Run", false);
                _animator.SetBool("Action", true);
                break;
        }
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
                _moveObj.transform.Translate(dir * Time.deltaTime * _moveSpeed, Space.World);

                SetSpriteDir(dir.x);
                ChangeState(CustomerState.Run);
                yield return null;
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
}
