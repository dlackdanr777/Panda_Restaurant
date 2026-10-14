using Muks.PathFinding.AStar;
using Muks.WeightedRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public partial class CustomerController : MonoBehaviour
{
    private const string TIME_TAG = "customerBreakTime";


    public event Action OnChangeCustomerHandler;
    public event Action OnGuideCustomerHandler;

    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private FeverSystem _feverSystem;
    [SerializeField] private UIAddCutomerController _uiController;
    public Image MarketerSkillEffect => _uiController.MarketerSkillEffect;
    [SerializeField] private SatisfactionSystem _satisfactionSystem;
    [SerializeField] private UICustomerTutorial _uiCustomerTutorial;

    [Space]
    [Header("Option")]
    [Range(1, 10)][SerializeField] private int _lineSpacingGrid;
    [SerializeField] private Transform _startLine;
    [SerializeField] private List<Vector3> _specialCustomerTargetPosList;
    [SerializeField] private Transform[] _gatecrasherCustomer2TargetPos;





    private GatecrasherCustomer[] _gatecrasherCustomers = new global::GatecrasherCustomer[(int)ERestaurantFloorType.Length];
    private bool _breakCustomerEnabled = true;



    // Lifetime ownership includes entering, waiting and seated visits. This is a
    // registry only; CustomerPartyQueue owns waiting order, capacity and persistence.
    private readonly List<CustomerVisitParty> _visitParties = new List<CustomerVisitParty>();
    private object _visitExecution = new object();
    internal object VisitExecution => _visitExecution;
    public string LastVisitDiagnostic { get; private set; }
    internal bool IsVisitExecutionCurrent(object execution) => execution != null
        && ReferenceEquals(execution, _visitExecution);
    internal void RegisterVisitParty(CustomerVisitParty party) { _visitParties.Add(party); }
    internal void UnregisterVisitParty(CustomerVisitParty party, bool preserveWaitingSave)
    { _visitParties.Remove(party); OnWaitingPartyEnded(party, preserveWaitingSave); }

    internal bool CreateSingleVisit(NormalCustomer customer)
    {
        bool created = CustomerVisitParty.TryCreate(this, new[] { customer }, out _, out string reason);
        LastVisitDiagnostic = reason;
        return created;
    }

    internal bool TryGetSingleVisit(NormalCustomer customer, bool requireUnseated, out CustomerVisitMember member)
    {
        member = customer == null ? null : customer.VisitMember;
        var party = member?.Party;
        if (party == null || !party.IsActive || !ReferenceEquals(party.Owner, this))
            LastVisitDiagnostic = "InvalidVisit";
        else if (party.Members.Count != 1)
            LastVisitDiagnostic = "UnsupportedPartySize";
        else if (!ReferenceEquals(party.Members[0], member) || !member.IsVisitCurrent)
            LastVisitDiagnostic = "VisitMemberMismatch";
        else if (requireUnseated && party.CurrentUse != null)
            LastVisitDiagnostic = "VisitAlreadySeated";
        else { LastVisitDiagnostic = "SingleVisitAccepted"; return true; }
        member = null;
        return false;
    }

    internal void EndVisitParties()
    {
        bool restoring = _restoringWaiting;
        _restoringWaiting = true;
        try
        {
            while (_visitParties.Count > 0)
            {
                var party = _visitParties[_visitParties.Count - 1];
                party.End(); ReturnPartyMembers(party);
            }
            _waitingTeams.Clear();
        }
        finally { _restoringWaiting = restoring; }
    }

    private void OnEnable()
    {
        EndVisitParties();
        _visitExecution = new object();
        LoadingSceneManager.OnLoadSceneHandler += EndVisitExecution;
        UserInfo.OnWaitingPartiesLoaded += OnWaitingPartiesLoaded;
    }

    private void OnWaitingPartiesLoaded(EStage stage)
    {
        if (stage == UserInfo.CurrentStage && _visitExecution != null) LoadWaitingCustomers();
    }

    private void EndVisitExecution()
    {
        _visitExecution = null;
        EndVisitParties();
    }

    private void OnDisable()
    {
        EndVisitExecution();
        LoadingSceneManager.OnLoadSceneHandler -= EndVisitExecution;
        UserInfo.OnWaitingPartiesLoaded -= OnWaitingPartiesLoaded;
    }

    private void OnDestroy() { OnDisable(); }

    private void RefreshVisitParties()
    {
        // Existing account query identity also covers logout/authentication replacement.
        for (int i = _visitParties.Count - 1; i >= 0; i--) _ = _visitParties[i].IsActive;
    }

    public Transform[] _GatecrasherCustomer2TargetPos => _gatecrasherCustomer2TargetPos;
    public GatecrasherCustomer[] GatecrasherCustomer => _gatecrasherCustomers;



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

    public void AddCustomer()
    {
        if (IsMaxCount)
            return;

        List<NormalCustomerData> normalCustomerDataList = CustomerDataManager.Instance.GetAppearNormalCustomerList();
        List<NormalCustomerData> normalCustomerDataListCopy = new List<NormalCustomerData>(normalCustomerDataList);
        if (!_satisfactionSystem.CheckCustomerTendency(CustomerTendencyType.Sensitive))
        {
            normalCustomerDataListCopy.RemoveAll(data => data.TendencyType == CustomerTendencyType.Sensitive);
        }

        if (!_satisfactionSystem.CheckCustomerTendency(CustomerTendencyType.HighlySensitive))
        {
            normalCustomerDataListCopy.RemoveAll(data => data.TendencyType == CustomerTendencyType.HighlySensitive);
        }
        if (0 < normalCustomerDataListCopy.Count)
        {
            normalCustomerDataList = normalCustomerDataListCopy;
        }

        List<SpecialCustomerData> specialCustomerDataList = CustomerDataManager.Instance.GetAppearSpecialCustomerDataList();
        List<GatecrasherCustomerData> gatecrasherCustomerDataList = CustomerDataManager.Instance.GetAppearGatecrasherCustomerDataList();
        int randInt = 0;

        for (int i = 0, cnt = GameManager.Instance.AddPromotionCustomer; i < cnt; i++)
        {
            if (IsMaxCount)
                break;

            int randSpawnProbability = UnityEngine.Random.Range(0, 100);
            float timer = TimeManager.Instance.GetTime(TIME_TAG);
            if (!UserInfo.IsTutorialStart && !GameManager.Instance.IsMiniGameStart && _breakCustomerEnabled && timer <= 0 && randSpawnProbability < ConstValue.DEFAULT_EXCEPTIONAL_CUSTOMER_SPAWN_PERCENT)
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
                //TODO: 나중에 삭제 후 위에꺼 쓰기(임시로 1층만 사용)
                //ERestaurantFloorType visitFloor = ERestaurantFloorType.Floor1;
                if (getData is SpecialCustomerData)
                {
                    TimeManager.Instance.SetTime(TIME_TAG, ConstValue.DEFAULT_CUSTOMER_BREAK_TIME);
                    _breakCustomerEnabled = false;
                    SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    specialCustomer.transform.position = GameManager.Instance.OutDoorPos;
                    specialCustomer.SetData(getData, this, _tableManager);
                    specialCustomer.SetVisitFloor(visitFloor);
                    specialCustomer.SetFeverSystem(_feverSystem);
                    specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
                    UserInfo.AddVisitSpecialCustomerCount();
                    _uiCustomerTutorial.ShowTutorial(getData);
                }
                else if (getData is GatecrasherCustomerData)
                {
                    TimeManager.Instance.SetTime(TIME_TAG, ConstValue.DEFAULT_CUSTOMER_BREAK_TIME);
                    _breakCustomerEnabled = false;

                    int floorIndex = (int)visitFloor;
                    _gatecrasherCustomers[floorIndex] = ObjectPoolManager.Instance.SpawnGatecrasherCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
                    _gatecrasherCustomers[floorIndex].transform.position = GameManager.Instance.OutDoorPos;
                    _gatecrasherCustomers[floorIndex].SetData(getData, this, _tableManager);
                    _gatecrasherCustomers[floorIndex].SetVisitFloor(visitFloor);
                    if (getData is GatecrasherCustomer1Data)
                    {
                        _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer1Event(_tableManager.GetDropCoinAreaList(visitFloor), _specialCustomerTargetPosList, OnCustomerEvent, () => _uiCustomerTutorial.ShowTutorial(getData));
                    }
                    else if (getData is GatecrasherCustomer2Data)
                    {
                        _uiCustomerTutorial.ShowTutorial(getData);
                        _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos[(int)visitFloor].position, _tableManager, OnCustomerEvent);
                    }
                }
            }

            else
            {
                randInt = UnityEngine.Random.Range(0, normalCustomerDataList.Count);
                CustomerData customerData = normalCustomerDataList[randInt];
                if (SpawnWaitingParty(new[] { customerData.Id }, false, out _, false)) UserInfo.AddPromotionCount();
            }
            OnChangeCustomerHandler?.Invoke();
        }
    }



    private void Awake()
    {
        _queueOwner = this;
        _breakCustomerEnabled = true;
        OnChangeCustomerHandler += UpdateWaitingCustomerSave;
    }

    private void Start()
    {
        LoadWaitingCustomers();
    }

    private void Update()
    {
        RefreshVisitParties();
        CheckGatecrasherCustomer();
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


    private void OnCustomerEvent(Customer customer)
    {
        _breakCustomerEnabled = true;

        for (int i = 0, cnt = _gatecrasherCustomers.Length; i < cnt; ++i)
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
        //TODO: 나중에 삭제 후 위에꺼 쓰기(임시로 1층만 사용)
        //ERestaurantFloorType visitFloor = ERestaurantFloorType.Floor1;
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
            _gatecrasherCustomers[floorIndex].StartGatecreasherCustomer2Event(_gatecrasherCustomer2TargetPos[(int)visitFloor].position, _tableManager, OnCustomerEvent);
        }

        else if (data is SpecialCustomerData)
        {
            SpecialCustomer specialCustomer = ObjectPoolManager.Instance.SpawnSpecialCustomer(GameManager.Instance.OutDoorPos, Quaternion.identity);
            specialCustomer.SetData(data, this, _tableManager);
            specialCustomer.SetVisitFloor(visitFloor);
            specialCustomer.SetFeverSystem(_feverSystem);
            specialCustomer.StartEvent(_specialCustomerTargetPosList, OnCustomerEvent);
        }

        else
        {
            if (SpawnWaitingParty(new[] { data.Id }, false, out _, false)) UserInfo.AddPromotionCount();
        }
        OnChangeCustomerHandler?.Invoke();
    }
}
