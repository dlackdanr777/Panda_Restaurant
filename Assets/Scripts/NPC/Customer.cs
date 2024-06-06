using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Muks.PathFinding.AStar;

public class Customer : MonoBehaviour
{
    [SerializeField] private GameObject _moveObj;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private Coroutine _moveCoroutine;
    private Coroutine _teleportCoroutine;

    private CustomerData _customerData;
    private Vector2 _targetPos;
    private int _targetFloor;
    private int _moveObjFloor;
    private float _scaleX;


    private void Start()
    {
        _scaleX = transform.localScale.x;
    }


    public void Init(CustomerData data)
    {
        _customerData = data;
        _spriteRenderer.sprite = data.Sprite;

        float heightMul = (float)data.Sprite.textureRect.height * 0.005f - AStar.Instance.NodeSize;
        _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
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

                if(dir.x < 0)
                    transform.localScale = new Vector3(_scaleX, transform.localScale.y, transform.localScale.z);
                else if(0 < dir.x)
                    transform.localScale = new Vector3(-_scaleX, transform.localScale.y, transform.localScale.z);

                yield return null;
            }
        }

        onCompleted?.Invoke();
    }


    private IEnumerator TeleportFloorRoutine(Action onCompleted)
    {
        yield return YieldCache.WaitForSeconds(1);
        _moveObj.transform.position = AStar.Instance.GetFloorPos(_targetFloor);
        transform.localScale = new Vector3(-_scaleX, transform.localScale.y, transform.localScale.z);
        yield return YieldCache.WaitForSeconds(1);
        onCompleted?.Invoke();
    }
}
