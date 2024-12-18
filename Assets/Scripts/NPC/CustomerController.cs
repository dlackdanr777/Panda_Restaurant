using Muks.PathFinding.AStar;
using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerController : MonoBehaviour
{
    public event Action OnAddCustomerHandler;
    public event Action OnGuideCustomerHandler;

    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private UICustomerTutorial _uiCustomerTutorial;

    [Space]
    [Header("Option")]
    [Range(1, 10)] [SerializeField] private int _lineSpacingGrid;
    [SerializeField] private Transform _startLine;
    [SerializeField] private List<Vector3> _specialCustomerTargetPosList;
    [SerializeField] private Vector3 _gatecrasherCustomer2TargetPos;


    private Queue<NormalCustomer> _customers = new Queue<NormalCustomer>();
    private GatecrasherCustomer _gatecrasherCustomer;
    private Coroutine _sortCoroutine;
    private float _breakInCustomerTime => 60;
    private float _breakInCustomerTimer;
    private bool _breakCustomerEnabled = true;


    public Vector3 _GatecrasherCustomer2TargetPos => _gatecrasherCustomer2TargetPos;
    public GatecrasherCustomer GatecrasherCustomer => _gatecrasherCustomer;
    public int Count => _customers.Count;
    public bool IsMaxCount => GameManager.Instance.MaxWaitCustomerCount <= _customers.Count;


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
        if (IsMaxCount)
            return;

        List<CustomerData> normalCustomerDataList = CustomerDataManager.Instance.GetAppearNormalCustomerList();
        List<SpecialCustomerData> specialCustomerDataList = CustomerDataManager.Instance.GetAppearSpecialCustomerDataList();
        List<GatecrasherCustomerData> gatecrasherCustomerDataList = CustomerDataManager.Instance.GetAppearGatecrasherCustomerDataList();
        int randInt = 0;

        for (int i = 0, cnt = GameManager.Instance.AddPromotionCustomer; i < cnt; i++)
        {
            if (IsMaxCount)
                break;

            int randSpawnProbability = UnityEngine.Random.Range(0, 100);
            if(!UserInfo.IsTutorialStart && _breakCustomerEnabled && _breakInCustomerTimer <= 0 && randSpawnProbability < ConstValue.DEFAULT_EXCEPTIONAL_CUSTOMER_SPAWN_PERCENT)
            {
                WeightedRandom<CustomerData> randomDataList = new WeightedRandom<CustomerData>();
                for(int j = 0, cntJ = specialCustomerDataList.Count; j < cntJ; ++j)
                    randomDataList.Add(specialCustomerDataList[j], specialCustomerDataList[j].SpawnChance * GameManager.Instance.AddSpecialCustomerSpawnMul);

                for (int j = 0, cntJ = gatecrasherCustomerDataList.Count; j < cntJ; ++j)
                    randomDataList.Add(gatecrasherCustomerDataList[j], gatecrasherCustomerDataList[j].SpawnChance);

                if(randomDataList.Count <= 0)
                {
                    i--;
                    continue;
                }

                CustomerData getData = randomDataList.GetRamdomItem();

                if(getData is SpecialCustomerData)
                {
                    _breakInCustomerTimer = _breakInCustomerTime;
                    _breakCustomerEnabled = false;
                    SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    specialCustomer.SetData(getData);
                    specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
                    UserInfo.AddVisitSpecialCustomerCount();
                    _uiCustomerTutorial.ShowTutorial(getData);
                }
                else if(getData is GatecrasherCustomerData)
                {
                    _breakInCustomerTimer = _breakInCustomerTime;
                    _breakCustomerEnabled = false;
                    _gatecrasherCustomer = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    _gatecrasherCustomer.SetData(getData);
                    if (getData is GatecrasherCustomer1Data)
                    {
                        _gatecrasherCustomer.StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(), _specialCustomerTargetPosList, OnCustomerEvent, () => _uiCustomerTutorial.ShowTutorial(getData));
                    }
                    else if (getData is GatecrasherCustomer2Data)
                    {
                        _uiCustomerTutorial.ShowTutorial(getData);
                        _gatecrasherCustomer.StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos, _tableManager, OnCustomerEvent);
                    }
                }
            }

            else
            {
                NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                randInt = UnityEngine.Random.Range(0, normalCustomerDataList.Count);
                CustomerData customerData = normalCustomerDataList[randInt];
                customer.SetData(customerData);
                _customers.Enqueue(customer);
                UserInfo.CustomerVisits(customerData);
                UserInfo.AddPromotionCount();

                if (_sortCoroutine != null)
                    StopCoroutine(_sortCoroutine);

                _sortCoroutine = StartCoroutine(SortCustomerLine());
            }
        }

        OnAddCustomerHandler?.Invoke();
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
        OnGuideCustomerHandler.Invoke();
        return true;
    }


    private void Awake()
    {
        _breakInCustomerTimer = _breakInCustomerTime;
        _breakCustomerEnabled = true;
    }


    private void Update()
    {
        UpdateBreakTimer();
        CheckGatecrasherCustomer();
    }


    private void UpdateBreakTimer()
    {
        if (!_breakCustomerEnabled)
            return;

        if (0 < _breakInCustomerTimer)
            _breakInCustomerTimer -= Time.deltaTime;
    }


    private void CheckGatecrasherCustomer()
    {
        if (_gatecrasherCustomer == null)
            return;

        if (!UserInfo.IsTutorialStart)
            return;

        ObjectPoolManager.Instance.DespawnGatecrasherCustomer(_gatecrasherCustomer);
        _gatecrasherCustomer = null;
        DebugLog.Log("Áø»ó ¼Õ´Ô »ç¶óÁü");
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

    private void OnCustomerEvent()
    {
        _breakInCustomerTimer = _breakInCustomerTime;
        _breakCustomerEnabled = true;
        _gatecrasherCustomer = null;
    }


    public void SpawnCustomer(string id)
    {
        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        _uiCustomerTutorial.ShowTutorial(data);
        if (data is GatecrasherCustomer1Data)
        {
            _gatecrasherCustomer = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            _gatecrasherCustomer.SetData(data);
            _gatecrasherCustomer.StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(), _specialCustomerTargetPosList, OnCustomerEvent);
        }
        else if (data is GatecrasherCustomer2Data)
        {
            _gatecrasherCustomer = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            _gatecrasherCustomer.SetData(data);
            _gatecrasherCustomer.StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos, _tableManager, OnCustomerEvent);
        }

        else if(data is SpecialCustomerData)
        {
            SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            specialCustomer.SetData(data);
            specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
        }

        else
        {
            NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            customer.SetData(data);
            _customers.Enqueue(customer);
            UserInfo.CustomerVisits(data);

            if (_sortCoroutine != null)
                StopCoroutine(_sortCoroutine);
            _sortCoroutine = StartCoroutine(SortCustomerLine());
            UserInfo.AddPromotionCount();
        }

        OnAddCustomerHandler?.Invoke();
    }
}
