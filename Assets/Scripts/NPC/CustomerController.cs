using Muks.PathFinding.AStar;
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

    public Customer GetFirstCustomer()
    {
        return _customers.Peek();
    }


    public void AddCustomer()
    {
        Customer customer = ObjectPoolManager.Instance.SpawnCustomer(new Vector2(15, 3.5f), Quaternion.identity);
        customer.Init(_data);
        _customers.Enqueue(customer);

        Vector2 startLinePos = _startLine.position;
        startLinePos.x += _lineSpacing * _customers.Count;
        customer.Move(startLinePos);
    }


    public void CustomerGuide(Vector3 targetPos)
    {
        if (_customers.Count <= 0)
            return;

        Customer customer = _customers.Dequeue();

        customer.Move(targetPos);

        if (_sortCoroutine != null)
            StopCoroutine(_sortCoroutine);

        _sortCoroutine = StartCoroutine(SortCustomerLine());
    }

    private IEnumerator SortCustomerLine()
    {
        yield return YieldCache.WaitForSeconds(0.5f);
        Vector2 startLinePos = _startLine.position;
        foreach (Customer c in _customers)
        {
            c.Move(startLinePos);
            startLinePos.x += _lineSpacing;
            yield return YieldCache.WaitForSeconds(0.05f);
        }
    }
}
