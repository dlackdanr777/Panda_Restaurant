using Muks.PathFinding.AStar;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerController : MonoBehaviour
{
    
    [Range(1, 30)] [SerializeField] private int _maxCustomers;
    [Range(1, 10)] [SerializeField] private int _lineSpacingGrid;
    [SerializeField] private Transform _startLine;

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
        if (_maxCustomers <= _customers.Count)
            return;

        List<CustomerData> data = CustomerDataManager.Instance.GetAppearCustomerList();
        for (int i = 0, cnt = GameManager.Instance.AddPromotionCustomer; i < cnt; i++)
        {
            if (_maxCustomers <= _customers.Count)
                break;

            Customer customer = ObjectPoolManager.Instance.SpawnCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            int randInt = UnityEngine.Random.Range(0, data.Count);
            customer.Init(data[randInt]);
            _customers.Enqueue(customer);
        }

        if (_sortCoroutine != null)
            StopCoroutine(_sortCoroutine);

        _sortCoroutine = StartCoroutine(SortCustomerLine());
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
        int orderLayer = 0;
        float gridSize = AStar.Instance.NodeSize;
        foreach (Customer c in _customers)
        {
            c.Move(startLinePos, -1);
            c.SetLayer("WaitCustomer", orderLayer++);
            startLinePos.x += (_lineSpacingGrid * gridSize);

            yield return YieldCache.WaitForSeconds(0.05f);
        }
    }
}
