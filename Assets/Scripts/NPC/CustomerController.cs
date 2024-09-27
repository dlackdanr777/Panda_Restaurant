using Muks.PathFinding.AStar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;

    [Space]
    [Header("Option")]
    [Range(1, 30)] [SerializeField] private int _maxCustomers;
    [Range(1, 10)] [SerializeField] private int _lineSpacingGrid;
    [SerializeField] private Transform _startLine;
    [SerializeField] private List<Vector3> _specialCustomerTargetPosList;
    [SerializeField] private Vector3 _gatecrasherCustomer2TargetPos;
    public Vector3 _GatecrasherCustomer2TargetPos => _gatecrasherCustomer2TargetPos;

    private Queue<NormalCustomer> _customers = new Queue<NormalCustomer>();

    private Coroutine _sortCoroutine; 

    public event Action AddCustomerHandler;
    public event Action GuideCustomerHandler;

    public int Count => _customers.Count;
    public bool IsMaxCount => Count >= _maxCustomers;

    public NormalCustomer GetFirstCustomer()
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

        int spawnSpecialCustomerProbability = 100;
        List<CustomerData> normalCustomerDataList = CustomerDataManager.Instance.GetAppearNormalCustomerList();
        List<SpecialCustomerData> specialCustomerDataList = CustomerDataManager.Instance.GetAppearSpecialCustomerDataList();
        List<GatecrasherCustomerData> gatecrasherCustomerDataList = CustomerDataManager.Instance.GetAppearGatecrasherCustomerDataList();
        int randInt = 0;

        for (int i = 0, cnt = GameManager.Instance.AddPromotionCustomer; i < cnt; i++)
        {
            if (_maxCustomers <= _customers.Count)
                break;


            int randSpawnProbability = UnityEngine.Random.Range(0, 100);
            bool specialCutomerEnabled = (0 < specialCustomerDataList.Count);
            GatecrasherCustomer gatecrasherCustomer = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            randInt = UnityEngine.Random.Range(0, gatecrasherCustomerDataList.Count);
            gatecrasherCustomer.SetData(gatecrasherCustomerDataList[randInt]);
            DebugLog.Log(gatecrasherCustomerDataList[randInt].Id);
            if(gatecrasherCustomerDataList[randInt] is GatecrasherCustomer1Data)
            {
                gatecrasherCustomer.StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(), _specialCustomerTargetPosList);
            }
            else if (gatecrasherCustomerDataList[randInt] is GatecrasherCustomer2Data)
            {
                gatecrasherCustomer.StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos, _tableManager);
            }


            NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            randInt = UnityEngine.Random.Range(0, normalCustomerDataList.Count);
            CustomerData customerData = normalCustomerDataList[randInt];
            customer.SetData(customerData);
            _customers.Enqueue(customer);
            UserInfo.CustomerVisits(customerData);
            continue;


            if (specialCutomerEnabled && randSpawnProbability < spawnSpecialCustomerProbability)
            {
                SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                randInt = UnityEngine.Random.Range(0, specialCustomerDataList.Count);
                specialCustomer.SetData(specialCustomerDataList[randInt]);
                specialCustomer.StartEvent(_specialCustomerTargetPosList);
            }
            else
            {
/*                NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                randInt = UnityEngine.Random.Range(0, normalCustomerDataList.Count);
                CustomerData customerData = normalCustomerDataList[randInt];
                customer.SetData(customerData);
                _customers.Enqueue(customer);
                UserInfo.CustomerVisits(customerData);*/
            }
        }

        if (_sortCoroutine != null)
            StopCoroutine(_sortCoroutine);

        _sortCoroutine = StartCoroutine(SortCustomerLine());
        UserInfo.AddPromotionCount();
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
        int orderLayer = Count;
        float gridSize = AStar.Instance.NodeSize;

        foreach (Customer c in _customers)
        {
            c.Move(startLinePos, -1);
            c.SetLayer("WaitCustomer", orderLayer--);
            startLinePos.x += (_lineSpacingGrid * gridSize);

            yield return YieldCache.WaitForSeconds(0.05f);
        }
    }
}
