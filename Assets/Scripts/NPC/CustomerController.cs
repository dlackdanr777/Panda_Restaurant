using Muks.PathFinding.AStar;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerController : MonoBehaviour
{
    [SerializeField] private Transform _startLine;
    [SerializeField] private float _lineSpacing;
    [SerializeField] private CustomerData _data;

    private Queue<Customer> _customers = new Queue<Customer>();

    private Coroutine _sortCoroutine;

    public event Action AddCustomerHandler;
    public event Action GuideCustomerHandler;

    public Customer GetFirstCustomer()
    {
        return _customers.Peek();
    }


    public bool IsEmpty()
    {
        bool result = _customers.Count == 0;
        return result;
    }


    public void AddCustomer()
    {
        Customer customer = ObjectPoolManager.Instance.SpawnCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
        customer.Init(_data);
        _customers.Enqueue(customer);

        Vector2 startLinePos = _startLine.position;
        startLinePos.x += _lineSpacing * _customers.Count;
        customer.Move(startLinePos, -1);

        AddCustomerHandler?.Invoke();
    }


    public bool GuideCustomer(Vector3 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        if (_customers.Count <= 0)
            return false;

        Customer customer = _customers.Dequeue();

        customer.Move(targetPos, moveEndDir, onCompleted);

        if (_sortCoroutine != null)
            StopCoroutine(_sortCoroutine);

        _sortCoroutine = StartCoroutine(SortCustomerLine());
        GuideCustomerHandler.Invoke();
        return true;
    }
     
    private IEnumerator SortCustomerLine()
    {
        yield return YieldCache.WaitForSeconds(0.5f);
        Vector2 startLinePos = _startLine.position;
        foreach (Customer c in _customers)
        {
            c.Move(startLinePos, -1);
            startLinePos.x += _lineSpacing;
            yield return YieldCache.WaitForSeconds(0.05f);
        }
    }
}
