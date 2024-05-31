using Muks.PathFinding.AStar;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestUnit : MonoBehaviour
{

    Coroutine _coroutine;
    private void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Vector2 targetPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            AStar.Instance.RequestPath(transform.position, targetPos, Move);
        }
    }


    private void Move(List<Node> nodeList)
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(PathFinding(nodeList));

    }

    private IEnumerator PathFinding(List<Node> nodeList)
    {
        
        foreach(Node node in nodeList)
        {
            while(Vector3.Distance(transform.position, node.toWorldPosition()) > 0.05f)
            {
                Vector2 dir = (node.toWorldPosition() - (Vector2)transform.position).normalized;
                transform.Translate(dir * Time.deltaTime, Space.World);

                yield return null;
            }
        }
    }
}
