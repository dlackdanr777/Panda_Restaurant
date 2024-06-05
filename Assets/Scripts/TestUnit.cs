using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestUnit : MonoBehaviour
{
/*
    Coroutine _coroutine;

    Vector2 _targetPos;

    private void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            _targetPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            AStar.Instance.RequestPath(transform.position, _targetPos, Move);
        }
    }


    private void Move(List<Vector2> nodeList)
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(PathFinding(nodeList));
        Debug.Log("Ω««‡¡ﬂ");
        Debug.Log(_targetPos);
    }


    private IEnumerator PathFinding(List<Vector2> nodeList)
    {
        
        foreach(Vector2 vec in nodeList)
        {
            while(Vector3.Distance(transform.position, vec) > 0.05f)
            {
                Vector2 dir = (vec - (Vector2)transform.position).normalized;
                transform.Translate(dir * Time.deltaTime, Space.World);

                yield return null;
            }
        }

        if (nodeList.Count == 0)
            yield break;

        Vector2 targetPos = nodeList[nodeList.Count - 1];

        foreach (Collider2D col in Physics2D.OverlapCircleAll(targetPos, 0.5f))
        {
            if (col.gameObject.layer != LayerMask.NameToLayer("Stairs"))
                continue;

            yield return new WaitForSeconds(1);


            float tmpY = 100000;

            List<Node> stairsNodeList = AStar.Instance.GetStairsNodeList();
            int targetIndex = -1;


            for(int i = 0, cnt = stairsNodeList.Count; i < cnt; ++i)
            {
                float y = _targetPos.y - stairsNodeList[i].toWorldPosition().y;

                if(Mathf.Abs(y) < Mathf.Abs(tmpY))
                {
                    tmpY = y;
                    targetIndex = i;
                }
            }

            gameObject.transform.position = stairsNodeList[targetIndex].toWorldPosition();

            yield return new WaitForSeconds(1);

            AStar.Instance.RequestPath(transform.position, _targetPos, Move);
            yield break;
        }
    }*/
}
