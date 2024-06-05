using Muks.PathFinding.AStar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TestButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Transform _targetTr;
    [SerializeField] private GameObject _moveObj;

    private int _targetFloor;
    private int _moveObjFloor;

    private Coroutine _coroutine;
    private Coroutine _waitCoroutine;


    void Start()
    {
        _button.onClick.AddListener(OnButtonClicked);
        _targetFloor = GetTransformFloor(_targetTr);
    }


    private int GetTransformFloor(Transform tr)
    {
        float tmpY = 10000;
        int floor = 1;
        int tmpFloor = 1;
        foreach (Transform t in AStar.Instance.GetFloorTrs())
        {
            float y = tr.position.y - t.position.y;

            if (Mathf.Abs(y) < Mathf.Abs(tmpY))
            {
                tmpY = y;
                tmpFloor = floor;
            }

            floor++;
        }

        return tmpFloor;
    }


    private void OnButtonClicked()
    {
        _moveObjFloor = GetTransformFloor(_moveObj.transform);

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        if (_waitCoroutine != null)
            StopCoroutine(_waitCoroutine);


        if (_moveObjFloor == _targetFloor)
            AStar.Instance.RequestPath(_moveObj.transform.position, _targetTr.transform.position, Move);

        else
        {
            AStar.Instance.RequestPath(_moveObj.transform.position, AStar.Instance.GetFloorPos(_moveObjFloor), StairsMove);
        }

    }


    private void Move(List<Vector2> nodeList)
    {
        _coroutine = StartCoroutine(MoveRoutine(nodeList));
    }


    private void StairsMove(List<Vector2> nodeList)
    {
        _coroutine = StartCoroutine(MoveRoutine(nodeList, () =>
        {

            _waitCoroutine = StartCoroutine(TeleportObj(() => AStar.Instance.RequestPath(AStar.Instance.GetFloorPos(_targetFloor), _targetTr.transform.position, Move)));
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
                _moveObj.transform.Translate(dir * Time.deltaTime * 3, Space.World);
                yield return null;
            }
        }

        onCompleted?.Invoke();
    }


    private IEnumerator TeleportObj(Action onCompleted)
    {
        yield return new WaitForSeconds(1);

        _moveObj.transform.position = AStar.Instance.GetFloorPos(_targetFloor);

        yield return new WaitForSeconds(1);

        onCompleted?.Invoke();
    }
}
