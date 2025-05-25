using Muks.PathFinding.AStar;
using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CustomerController : MonoBehaviour
{
    public event Action OnChangeCustomerHandler;
    public event Action OnGuideCustomerHandler;

    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private FeverSystem _feverSystem;
    [SerializeField] private UIAddCutomerController _uiController;
    public Image MarketerSkillEffect => _uiController.MarketerSkillEffect;
    [SerializeField] private UICustomerTutorial _uiCustomerTutorial;

    [Space]
    [Header("Option")]
    [Range(1, 10)] [SerializeField] private int _lineSpacingGrid;
    [SerializeField] private Transform _startLine;
    [SerializeField] private List<Vector3> _specialCustomerTargetPosList;
    [SerializeField] private Vector3 _gatecrasherCustomer2TargetPos;



    private List<NormalCustomer> _callCustomers = new List<NormalCustomer>();
    private Queue<NormalCustomer> _waitCustomers = new Queue<NormalCustomer>();
    private GatecrasherCustomer[] _gatecrasherCustomers = new global::GatecrasherCustomer[(int)ERestaurantFloorType.Length];
    private Coroutine _sortCoroutine;
    private float _breakInCustomerTime => 1200;
    private float _breakInCustomerTimer;
    private bool _breakCustomerEnabled = true;
    public int Count => _callCustomers.Count;

    public Vector3 _GatecrasherCustomer2TargetPos => _gatecrasherCustomer2TargetPos;
    public GatecrasherCustomer[] GatecrasherCustomer => _gatecrasherCustomers;
    public bool IsMaxCount => GameManager.Instance.MaxWaitCustomerCount <= _callCustomers.Count;


    //// UI관련 변수
    public event Action OnAddCustomerHandler;
    public event Action OnAddTabCountHandler;
    public event Action OnMaxCustomerHandler; 

    private int _tabCount = 0;
    public int TabCount => _tabCount;


    public void AddTabCount()
    {
        if (GameManager.Instance.TotalTabCount - 1 <= _tabCount)
        {
            if (IsMaxCount)
            {
                OnMaxCustomerHandler?.Invoke();
                return;
            }

            _tabCount = 0;
            AddCustomer();
            OnAddCustomerHandler?.Invoke();
            return;
        }
        _tabCount++;
        OnAddTabCountHandler?.Invoke();
    }

    public NormalCustomer GetFirstCustomer()
    {
        return _waitCustomers.Peek();
    }

    public void RemoveCustomerFromLineQueue(NormalCustomer customer)
    {
        if (_waitCustomers.Count <= 0)
            return;

        if (!_waitCustomers.Contains(customer))
        {
            DebugLog.LogError("해당 고객이 대기열에 없습니다.");
            return;
        }

        _waitCustomers = new Queue<NormalCustomer>(_waitCustomers.Where(c => c != customer));
        SortCustomerLine();
        OnChangeCustomerHandler?.Invoke();
    }


    public bool IsEmpty()
    {
        bool result = _waitCustomers.Count == 0;
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
            if (!UserInfo.IsTutorialStart && _breakCustomerEnabled && _breakInCustomerTimer <= 0 && randSpawnProbability < ConstValue.DEFAULT_EXCEPTIONAL_CUSTOMER_SPAWN_PERCENT)
            {
                WeightedRandom<CustomerData> randomDataList = new WeightedRandom<CustomerData>();
                for (int j = 0, cntJ = specialCustomerDataList.Count; j < cntJ; ++j)
                    randomDataList.Add(specialCustomerDataList[j], specialCustomerDataList[j].SpawnChance * GameManager.Instance.AddSpecialCustomerSpawnMul);

                for (int j = 0, cntJ = gatecrasherCustomerDataList.Count; j < cntJ; ++j)
                    randomDataList.Add(gatecrasherCustomerDataList[j], gatecrasherCustomerDataList[j].SpawnChance);

                if (randomDataList.Count <= 0)
                {
                    i--;
                    continue;
                }

                CustomerData getData = randomDataList.GetRamdomItem();
                ERestaurantFloorType visitFloor = (ERestaurantFloorType)UnityEngine.Random.Range(0, (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage) + 1);
                if (getData is SpecialCustomerData)
                {
                    _breakInCustomerTimer = _breakInCustomerTime;
                    _breakCustomerEnabled = false;
                    SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    specialCustomer.SetData(getData, this, _tableManager);
                    specialCustomer.SetVisitFloor(visitFloor);
                    specialCustomer.SetFeverSystem(_feverSystem);
                    specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
                    UserInfo.AddVisitSpecialCustomerCount();
                    _uiCustomerTutorial.ShowTutorial(getData);
                }
                else if (getData is GatecrasherCustomerData)
                {
                    _breakInCustomerTimer = _breakInCustomerTime;
                    _breakCustomerEnabled = false;

                    int floorIndex = (int)visitFloor;
                    _gatecrasherCustomers[floorIndex] = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    _gatecrasherCustomers[floorIndex].SetData(getData, this, _tableManager);
                    _gatecrasherCustomers[floorIndex].SetVisitFloor(visitFloor);
                    if (getData is GatecrasherCustomer1Data)
                    {
                        _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(visitFloor), _specialCustomerTargetPosList, OnCustomerEvent, () => _uiCustomerTutorial.ShowTutorial(getData));
                    }
                    else if (getData is GatecrasherCustomer2Data)
                    {
                        _uiCustomerTutorial.ShowTutorial(getData);
                        _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos, _tableManager, OnCustomerEvent);
                    }
                }
            }

            else
            {
                NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                randInt = UnityEngine.Random.Range(0, normalCustomerDataList.Count);
                CustomerData customerData = normalCustomerDataList[randInt];
                customer.SetData(customerData, this, _tableManager);
                customer.StartWaiting();
                _callCustomers.Add(customer);
                UserInfo.AddPromotionCount();

                Vector2 startLinePos = _startLine.position;
                int orderLayer = Count;
                float gridSize = AStar.Instance.NodeSize;

                customer.Move(_startLine.position + new Vector3(_lineSpacingGrid * gridSize * (Count - 1), 0), -1, () =>
                {
                    _waitCustomers.Enqueue(customer);
                    _tableManager.UpdateTable();
                });
                customer.SetLayer("WaitCustomer", orderLayer--);
                startLinePos.x += _lineSpacingGrid * gridSize;
            }

            OnChangeCustomerHandler?.Invoke();
        }
    }


    public bool GuideCustomer(Vector3 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        if (_waitCustomers.Count <= 0)
            return false;

        NormalCustomer customer = _waitCustomers.Dequeue();
        _callCustomers.Remove(customer);
        customer.Move(targetPos, moveEndDir, onCompleted);


        SortCustomerLine();
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
        if (!UserInfo.IsTutorialStart)
            return;

        for (int i = 0, cnt = _gatecrasherCustomers.Length; i < cnt; i++)
        {
            if (_gatecrasherCustomers[i] == null)
                continue;

            ObjectPoolManager.Instance.DespawnGatecrasherCustomer(_gatecrasherCustomers[i]);
            _gatecrasherCustomers[i] = null;
        }

    }


    private void SortCustomerLine()
    {
        if (_sortCoroutine != null)
            StopCoroutine(_sortCoroutine);

        _sortCoroutine = StartCoroutine(SortCustomerLineRoutine());
    }

    private IEnumerator SortCustomerLineRoutine()
    {
        yield return YieldCache.WaitForSeconds(0.5f);
        Vector2 startLinePos = _startLine.position;
        int orderLayer = _waitCustomers.Count;
        float gridSize = AStar.Instance.NodeSize;

        foreach (Customer c in _waitCustomers)
        {
            c.Move(startLinePos, -1);
            c.SetLayer("WaitCustomer", orderLayer--);
            startLinePos.x += _lineSpacingGrid * gridSize;

            yield return YieldCache.WaitForSeconds(0.05f);
        }
    }

    private void OnCustomerEvent(Customer customer)
    {
        _breakInCustomerTimer = _breakInCustomerTime;
        _breakCustomerEnabled = true;

        for(int i = 0, cnt = _gatecrasherCustomers.Length; i < cnt; ++i)
        {
            if (_gatecrasherCustomers[i] != customer)
                continue;

            _gatecrasherCustomers[i] = null;
            return;
        }

    }


    public void SpawnCustomer(string id)
    {
        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        _uiCustomerTutorial.ShowTutorial(data);

        ERestaurantFloorType visitFloor = (ERestaurantFloorType)UnityEngine.Random.Range(0, (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage) + 1);
        int floorIndex = (int)visitFloor;
        if (data is GatecrasherCustomer1Data)
        {
            _gatecrasherCustomers[floorIndex] = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            _gatecrasherCustomers[floorIndex].SetData(data, this, _tableManager);
            _gatecrasherCustomers[floorIndex].SetVisitFloor(visitFloor);
            _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(visitFloor), _specialCustomerTargetPosList, OnCustomerEvent);
        }
        else if (data is GatecrasherCustomer2Data)
        {
            _gatecrasherCustomers[floorIndex] = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            _gatecrasherCustomers[floorIndex].SetData(data, this, _tableManager);
            _gatecrasherCustomers[floorIndex].SetVisitFloor(visitFloor);
            _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos, _tableManager, OnCustomerEvent);
        }

        else if(data is SpecialCustomerData)
        {
            SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            specialCustomer.SetData(data, this, _tableManager);
            specialCustomer.SetVisitFloor(visitFloor);
            specialCustomer.SetFeverSystem(_feverSystem);
            specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
        }

        else
        {
            NormalCustomer customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            customer.SetData(data, this, _tableManager);
            customer.StartWaiting();
            _waitCustomers.Enqueue(customer);
            
            SortCustomerLine();
            UserInfo.AddPromotionCount();
        }

        OnChangeCustomerHandler?.Invoke();
    }
}
