using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Muks.PathFinding.AStar;

public class Customer : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _sprite;

    private Coroutine _moveCoroutine;
    private Coroutine _teleportCoroutine;

    private CustomerData _customerData;
    private Vector2 _targetPos;
    private int _targetFloor;
    private int _moveObjFloor;

    public void Init(CustomerData data)
    {
        _customerData = data;
        _sprite.sprite = data.Sprite;
    }


    public void Move(Vector2 targetPos)
    {
        _moveObjFloor = AStar.Instance.GetTransformFloor(_moveObj.transform.position);
        _targetFloor = AStar.Instance.GetTransformFloor(targetPos);
        _targetPos = targetPos;

        if (_moveObjFloor == _targetFloor)
            AStar.Instance.RequestPath(_moveObj.transform.position, targetPos, Move);

        else
            AStar.Instance.RequestPath(_moveObj.transform.position, AStar.Instance.GetFloorPos(_moveObjFloor), StairsMove);
    }


    private void Move(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList));
    }


    private void StairsMove(List<Vector2> nodeList)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);

        _moveCoroutine = StartCoroutine(MoveRoutine(nodeList, () =>
        {

            _teleportCoroutine = StartCoroutine(TeleportFloorRoutine(() => AStar.Instance.RequestPath(AStar.Instance.GetFloorPos(_targetFloor), _targetPos, Move)));
        }
        ));
    }


    private IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)
    {
        foreach (Vector2 vec in nodeList)
        {
            while (Vector3.Distance(_moveObj.transform.position, vec) > 0.02f)
            {
                Vector2 dir = (vec - (Vector2)_moveObj.transform.position).normalized;
                _moveObj.transform.Translate(dir * Time.deltaTime * _customerData.MoveSpeed, Space.World);
                yield return null;
            }
        }

        onCompleted?.Invoke();
    }


    private IEnumerator TeleportFloorRoutine(Action onCompleted)
    {
        yield return YieldCache.WaitForSeconds(1);
        _moveObj.transform.position = AStar.Instance.GetFloorPos(_targetFloor);
        yield return YieldCache.WaitForSeconds(1);
        onCompleted?.Invoke();
    }
}
