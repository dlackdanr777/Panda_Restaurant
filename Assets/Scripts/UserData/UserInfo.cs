using BackEnd;
using LitJson;
using Muks.BackEnd;
using Muks.DataBind;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class UserInfo
{
    public static event Action OnChangeFloorHandler;
    public static event Action OnChangeDiaHandler;
    public static event Action OnChangeMoneyHandler;
    public static event Action OnChangeTipHandler;
    public static event Action OnChangeScoreHandler;
    public static event Action OnAddCustomerCountHandler;
    public static event Action OnAddPromotionCountHandler;
    public static event Action OnAddAdvertisingViewCountHandler;
    public static event Action OnAddCleanCountHandler;

    public static event Action<ERestaurantFloorType, StaffType> OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnUpgradeStaffHandler;

    public static event Action OnGiveRecipeHandler;
    public static event Action OnUpgradeRecipeHandler;
    public static event Action OnAddCookCountHandler;

    public static event Action OnUseGachaMachineHandler;
    public static event Action OnGiveGachaItemHandler;
    public static event Action OnUpgradeGachaItemHandler;

    public static event Action<ERestaurantFloorType, FurnitureType> OnChangeFurnitureHandler;
    public static event Action OnGiveFurnitureHandler;
    public static event Action OnChangeFurnitureSetDataHandler;

    public static event Action<ERestaurantFloorType, KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public static event Action OnGiveKitchenUtensilHandler;
    public static event Action OnChangeKitchenUtensilSetDataHandler;

    public static event Action OnDoneChallengeHandler;
    public static event Action OnClearChallengeHandler;

    public static event Action OnEnabledCustomerHandler;
    public static event Action OnVisitedCustomerHandler;
    public static event Action OnVisitSpecialCustomerHandler;
    public static event Action OnExterminationGatecrasherCustomerHandler;

    public static event Action<string> OnAddNotificationHandler;
    public static event Action<string> OnRemoveNotificationHandler;

    public static event Action OnUpdateAttendanceDataHandler;


    public static bool IsTutorialStart = false;
    public static bool IsFirstTutorialClear = true;
    public static bool IsMiniGameTutorialClear = false;
    public static bool IsGatecrasher1TutorialClear = false;
    public static bool IsGatecrasher2TutorialClear = false;
    public static bool IsSpecialCustomer1TutorialClear = false;
    public static bool IsSpecialCustomer2TutorialClear = false;

    private static ERestaurantFloorType _currentFloor;
    public static ERestaurantFloorType CurrentFloor => _currentFloor;

    private static EStage _unlockStage;
    public static EStage UnlockStage => _unlockStage;

    private static EStage _currentStage;
    public static EStage CurrentStage => _currentStage;



    private static string _userId;
    public static string UserId => _userId;

    private static string _firstAccessTime;
    public static string FirstAccessTime => _firstAccessTime;

    private static string _lastAccessTime;
    public static string LastAccessTime => _lastAccessTime;

    private static string _lastAttendanceTime;
    public static string LastAttendanceTime => _lastAttendanceTime;

    private static int _dia;
    public static int Dia => _dia;

    private static long _money;
    public static long Money => _money;

    private static long _totalAddMoney;
    public static long TotalAddMoney => _totalAddMoney;

    private static long _dailyAddMoney;
    public static long DailyAddMoney => _dailyAddMoney;

    private static int _score;
    public static int Score => _score + GameManager.Instance.AddSocre;

    private static int _tip;
    public static int Tip => _tip;

    private static int _totalCookCount;
    public static int TotalCookCount => _totalCookCount;

    private static int _dailyCookCount;
    public static int DailyCookCount => _dailyCookCount;

    private static int _totalCumulativeCustomerCount;
    public static int TotalCumulativeCustomerCount => _totalCumulativeCustomerCount;

    private static int _dailyCumulativeCustomerCount;
    public static int DailyCumulativeCustomerCount => _dailyCumulativeCustomerCount;

    private static int _promotionCount;
    public static int PromotionCount => _promotionCount;

    private static int _totalAdvertisingViewCount;
    public static int TotalAdvertisingViewCount => _totalAdvertisingViewCount;

    private static int _dailyAdvertisingViewCount;
    public static int DailyAdvertisingViewCount => _dailyAdvertisingViewCount;

    private static int _totalCleanCount;
    public static int TotalCleanCount => _totalCleanCount;

    private static int _dailyCleanCount;
    public static int DailyCleanCount => _dailyCleanCount;

    private static int _totalAttendanceDays = 0;
    public static int TotalAttendanceDays => _totalAttendanceDays;

    private static int _totalVisitSpecialCustomerCount;
    public static int TotalVisitSpecialCustomerCount => _totalVisitSpecialCustomerCount;

    private static int _totalExterminationGatecrasherCustomer1Count;
    public static int TotalExterminationGatecrasherCustomer1Count => _totalExterminationGatecrasherCustomer1Count;

    private static int _totalExterminationGatecrasherCustomer2Count;
    public static int TotalExterminationGatecrasherCustomer2Count => _totalExterminationGatecrasherCustomer2Count;

    private static int _totalUseGachaMachineCount;
    public static int TotalUseGachaMachineCount => _totalUseGachaMachineCount;


    private static StaffData[,] _equipStaffDatas = new StaffData[(int)ERestaurantFloorType.Length, (int)StaffType.Length];
    private static Dictionary<string, int> _giveStaffLevelDic = new Dictionary<string, int>();

    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _recipeCookCountDic = new Dictionary<string, int>();

    private static Dictionary<string, int> _giveGachaItemCountDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _giveGachaItemLevelDic = new Dictionary<string, int>();

    private static FurnitureData[,] _equipFurnitureDatas = new FurnitureData[(int)ERestaurantFloorType.Length, (int)FurnitureType.Length];
    private static List<string> _giveFurnitureList = new List<string>();

    private static KitchenUtensilData[,] _equipKitchenUtensilDatas = new KitchenUtensilData[(int)ERestaurantFloorType.Length, (int)KitchenUtensilType.Length];
    private static List<string> _giveKitchenUtensilList = new List<string>();

    private static SetData[] _furnitureEnabledSetData = new SetData[(int)ERestaurantFloorType.Length];
    private static SetData[] _kitchenuntensilEnabledSetData = new SetData[(int)ERestaurantFloorType.Length];

    private static Dictionary<string, int> _furnitureEffectSetCountDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _kitchenUtensilEffectSetCountDic = new Dictionary<string, int>();
    private static HashSet<string> _activatedFurnitureEffectSet = new HashSet<string>();
    private static HashSet<string> _activatedKitchenUtensilEffectSet = new HashSet<string>();


    private static HashSet<string> _doneMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneDailyChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearDailyChallengeSet = new HashSet<string>();

    private static HashSet<string> _enabledCustomerSet = new HashSet<string>();
    private static HashSet<string> _visitedCustomerSet = new HashSet<string>();

    private static HashSet<string> _notificationMessageSet = new HashSet<string>(); //�˸��� �ʿ��� Id���� ��Ƶδ� �ؽ���



    //################################ȯ�� ���� ���� ����################################
    public static Action OnChangeGachaItemSortTypeHandler;
    public static Action OnChangeCustomerSortTypeHandler;


    public static SortType _customerSortType = SortType.None;
    public static SortType CustomerSortType => _customerSortType;

    private static SortType _gachaItemSortType = SortType.GradeDescending;
    public static SortType GachaItemSortType => _gachaItemSortType;


    //################################����, ������ �ʿ� �����ϴ��� Ȯ�� ����################################
    private static List<List<SaveCoinAreaData>> _saveCoinAreaDataList = new List<List<SaveCoinAreaData>>();
    public static List<List<SaveCoinAreaData>> SaveCounAreaDataList => _saveCoinAreaDataList;

    private static List<List<SaveGarbageAreaData>> _saveGarbageAreaDataList = new List<List<SaveGarbageAreaData>>();
    public static List<List<SaveGarbageAreaData>> SaveGarbageAreaDataList => _saveGarbageAreaDataList;


    private static StageInfo[] _stageInfos = new StageInfo[(int)EStage.Length];

    #region Init


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        for (int i = 0, cnt = _stageInfos.Length; i < cnt; ++i)
        {
            _stageInfos[i] = new StageInfo();
            _stageInfos[i].OnChangeStaffHandler += OnChangeStaffEvent;
            _stageInfos[i].OnGiveStaffHandler += OnGiveStaffEvent;
            _stageInfos[i].OnUpgradeStaffHandler += OnUpgradeStaffEvent;

            _stageInfos[i].OnChangeFurnitureHandler += OnChangeFurnitureEvent;
            _stageInfos[i].OnGiveFurnitureHandler += OnGiveFurnitureEvent;
            _stageInfos[i].OnChangeFurnitureSetDataHandler += OnChangeFurnitureSetDataEvent;

            _stageInfos[i].OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
            _stageInfos[i].OnGiveKitchenUtensilHandler += OnGiveKitchenUtensilEvent;
            _stageInfos[i].OnChangeKitchenUtensilSetDataHandler += OnChangeKitchenUtensilSetDataEvent;
        }
    }

    private static void OnChangeStaffEvent(ERestaurantFloorType floor, StaffType type)
    {
        OnChangeStaffHandler?.Invoke(floor, type);
    }

    public static void OnGiveStaffEvent()
    {
        OnGiveStaffHandler?.Invoke();   
    }

    public static void OnUpgradeStaffEvent()
    {
        OnUpgradeStaffHandler?.Invoke();
    }


    private static void OnChangeFurnitureEvent(ERestaurantFloorType floor, FurnitureType type)
    {
        OnChangeFurnitureHandler?.Invoke(floor, type);
    }

    private static void OnGiveFurnitureEvent()
    {
        OnGiveFurnitureHandler?.Invoke();
    }

    private static void OnChangeFurnitureSetDataEvent()
    {
        OnChangeFurnitureSetDataHandler?.Invoke();
    }


    private static void OnChangeKitchenUtensilEvent(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        OnChangeKitchenUtensilHandler?.Invoke(floor, type);
    }

    private static void OnGiveKitchenUtensilEvent()
    {
        OnGiveKitchenUtensilHandler?.Invoke();
    }

    private static void OnChangeKitchenUtensilSetDataEvent()
    {
        OnChangeKitchenUtensilSetDataHandler?.Invoke();
    }


    #endregion

    public static Param GetSaveUserData()
    {
        Param param = new Param();

        if(CheckLastAccessTime())
        {
            UpdateLastAccessTime();
            ResetDailyChallenges();
        }

        param.Add("IsFirstTutorialClear", IsFirstTutorialClear);
        param.Add("IsMiniGameTutorialClear", IsMiniGameTutorialClear);
        param.Add("IsGatecrasher1TutorialClear", IsGatecrasher1TutorialClear);
        param.Add("IsGatecrasher2TutorialClear", IsGatecrasher2TutorialClear);
        param.Add("IsSpecialCustomer1TutorialClear", IsSpecialCustomer1TutorialClear);
        param.Add("IsSpecialCustomer2TutorialClear", IsSpecialCustomer2TutorialClear);

        param.Add("CurrentFloor", (int)_currentFloor);
        param.Add("Dia", _dia);
        param.Add("Money", _money);
        param.Add("TotalAddMoney", _totalAddMoney);
        param.Add("DailyAddMoney", _dailyAddMoney);
        param.Add("Score", _score);
        param.Add("Tip", _tip);
        param.Add("TotalCookCount", _totalCookCount);
        param.Add("DailyCookCount", _dailyCookCount);
        param.Add("TotalCumulativeCustomerCount", _totalCumulativeCustomerCount);
        param.Add("DailyCumulativeCustomerCount", _dailyCumulativeCustomerCount);
        param.Add("PromotionCount", _promotionCount);
        param.Add("TotalAdvertisingViewCount", _totalAdvertisingViewCount);
        param.Add("DailyAdvertisingViewCount", _dailyAdvertisingViewCount);
        param.Add("TotalCleanCount", _totalCleanCount);
        param.Add("DailyCleanCount", _dailyCleanCount);
        param.Add("TotalVisitSpecialCustomerCount", _totalVisitSpecialCustomerCount);
        param.Add("TotalExterminationGatecrasherCustomer1Count", _totalExterminationGatecrasherCustomer1Count);
        param.Add("TotalExterminationGatecrasherCustomer2Count", _totalExterminationGatecrasherCustomer2Count);
        param.Add("TotalUseGachaMachineCount", _totalUseGachaMachineCount);

        param.Add("UserId", _userId);
        param.Add("FirstAccessTime", _firstAccessTime);
        param.Add("LastAccessTime", BackendManager.Instance.ServerTime.ToString());
        param.Add("LastAttendanceTime", _lastAttendanceTime);
        param.Add("TotalAttendanceDays", _totalAttendanceDays);

        List<SaveLevelData> giveRecipeSaveDataList = new List<SaveLevelData>();
        foreach (var value in _giveRecipeLevelDic)
        {
            giveRecipeSaveDataList.Add(new SaveLevelData(value.Key, value.Value));
        }
        param.Add("GiveRecipeList", giveRecipeSaveDataList);

        List<SaveCountData> recipeCookCountSaveDataList = new List<SaveCountData>();
        foreach (var value in _recipeCookCountDic)
        {
            recipeCookCountSaveDataList.Add(new SaveCountData(value.Key, value.Value));
        }
        param.Add("RecipeCookCountList", recipeCookCountSaveDataList);

        List<SaveCountData> giveGachaItemCountList = new List<SaveCountData>();
        foreach (var value in _giveGachaItemCountDic)
        {
            giveGachaItemCountList.Add(new SaveCountData(value.Key, value.Value));
        }
        param.Add("GiveGachaItemCountList", giveGachaItemCountList);

        List<SaveLevelData> giveGachaItemLevelList = new List<SaveLevelData>();
        foreach (var value in _giveGachaItemLevelDic)
        {
            giveGachaItemLevelList.Add(new SaveLevelData(value.Key, value.Value));
        }
        param.Add("GiveGachaItemLevelList", giveGachaItemLevelList);

        param.Add("DoneMainChallengeList", _doneMainChallengeSet.ToList());
        param.Add("ClearMainChallengeList", _clearMainChallengeSet.ToList());
        param.Add("DoneAllTimeChallengeList", _doneAllTimeChallengeSet.ToList());
        param.Add("ClearAllTimeChallengeList", _clearAllTimeChallengeSet.ToList());
        param.Add("DoneDailyChallengeList", _doneDailyChallengeSet.ToList());
        param.Add("ClearDailyChallengeList", _clearDailyChallengeSet.ToList());
        param.Add("NotificationMessageList", _notificationMessageSet.ToList());

        param.Add("EnabledCustomerList", _enabledCustomerSet.ToList());
        param.Add("VisitedCustomerList", _visitedCustomerSet.ToList());

        List<List<TableData>> _tableDataList = TableManager.GetTableDataList();
        if(_tableDataList != null || 0 < _tableDataList.Count)
        {
            _saveGarbageAreaDataList.Clear();
            _saveCoinAreaDataList.Clear();
            for(int i = 0, cnt = _tableDataList.Count; i < cnt; ++i)
            {
                List<TableData> dataList = _tableDataList[i];
                List<SaveGarbageAreaData> saveGarbageAreaDataList = new List<SaveGarbageAreaData>();
                List<SaveCoinAreaData> saveCoinAreaDataList = new List<SaveCoinAreaData>();

                if (dataList == null || dataList.Count <= 0)
                {
                    _saveGarbageAreaDataList.Add(saveGarbageAreaDataList);
                    _saveCoinAreaDataList.Add(saveCoinAreaDataList);
                    continue;
                }

                for(int j = 0, cntJ = dataList.Count; j < cntJ; ++j)
                {
                    TableData tableData = dataList[j];
                    saveGarbageAreaDataList.Add(new SaveGarbageAreaData(tableData.DropGarbageArea.Count));
                    for(int k = 0, cntK = tableData.DropCoinAreas.Length; k < cntK; ++k)
                    {
                        DropCoinArea dropCoinArea = tableData.DropCoinAreas[k];
                        saveCoinAreaDataList.Add(new SaveCoinAreaData(dropCoinArea.Count, dropCoinArea.CurrentMoney));
                    }
                }

                _saveGarbageAreaDataList.Add(saveGarbageAreaDataList);
                _saveCoinAreaDataList.Add(saveCoinAreaDataList);
            }

        }
        param.Add("SaveCoinAreaDataList", _saveCoinAreaDataList);
        param.Add("SaveGarbageAreaDataList", _saveGarbageAreaDataList);

        return param;
    }


    public static void SaveStageData()
    {
        for(int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
        {
            SaveStageData((EStage)i);
        }
    }


    public static void SaveStageDataAsync()
    {
        for (int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
        {
            SaveStageDataAsync((EStage)i);
        }
    }


    public static void SaveStageData(EStage stage)
    {
        int stageIndex = (int)stage;
        if (_stageInfos[stageIndex] == null)
        {
            DebugLog.LogError("���� �������� ������ �����ϴ�: " + stage.ToString());
            return;
        }

        Param param = _stageInfos[stageIndex].SaveData().GetParam();
        BackendManager.Instance.SaveGameData(stage.ToString()+ "Data", 3, param);
    }


    public static void SaveStageDataAsync(EStage stage)
    {
        int stageIndex = (int)stage;
        if (_stageInfos[stageIndex] == null)
        {
            DebugLog.LogError("���� �������� ������ �����ϴ�: " + stage.ToString());
            return;
        }

        Param param = _stageInfos[stageIndex].SaveData().GetParam();
        BackendManager.Instance.SaveGameDataAsync(stage.ToString() + "Data", 3, param);
    }


    public static void LoadStageData()
    {
        for (int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
        {
            LoadStageData((EStage)i);
        }
    }


    public static void LoadStageData(EStage stage)
    {
        BackendManager.Instance.GetMyData(stage.ToString() + "Data", 10, (bro) =>
        {
            JsonData json = bro.FlattenRows();
            if (json.Count <= 0)
            {
                Debug.LogError("����� �����Ͱ� �����ϴ�.");
                return;
            }

            ServerStageData data = new ServerStageData();
            data.SetData(json);
            _stageInfos[(int)stage].LoadData(data);
        });
    }


    public static void LoadGameData(BackendReturnObject bro)
    {
        JsonData json = bro.FlattenRows();
        if (json.Count <= 0)
        {
            Debug.LogError("����� �����Ͱ� �����ϴ�.");
            return;
        }

        LoadUserData loadData = new LoadUserData(json);
        if (loadData == null)
        {
            Debug.LogError("�ε� �����͸� �Ľ��ϴ� �������� ������ �߻��߽��ϴ�.");
            return;
        }

        IsFirstTutorialClear = loadData.IsFirstTutorialClear;
        IsMiniGameTutorialClear = loadData.IsMiniGameTutorialClear;
        IsGatecrasher1TutorialClear = loadData.IsGatecrasher1TutorialClear;
        IsGatecrasher2TutorialClear = loadData.IsGatecrasher2TutorialClear;
        IsSpecialCustomer1TutorialClear = loadData.IsSpecialCustomer1TutorialClear;
        IsSpecialCustomer2TutorialClear = loadData.IsSpecialCustomer2TutorialClear;

        _dia = loadData.Dia;
        _money = loadData.Money;
        _totalAddMoney = loadData.TotalAddMoney;
        _dailyAddMoney = loadData.DailyAddMoney;
        _score = loadData.Score;
        _tip = loadData.Tip;
        _totalCookCount = loadData.TotalCookCount;
        _dailyCookCount = loadData.DailyCookCount;
        _totalCumulativeCustomerCount = loadData.TotalCumulativeCustomerCount;
        _dailyCumulativeCustomerCount = loadData.DailyCumulativeCustomerCount;
        _promotionCount = loadData.PromotionCount;
        _totalAdvertisingViewCount = loadData.TotalAdvertisingViewCount;
        _dailyAdvertisingViewCount = loadData.DailyAdvertisingViewCount;
        _totalCleanCount = loadData.TotalCleanCount;
        _dailyCleanCount = loadData.DailyCleanCount;
        _totalVisitSpecialCustomerCount = loadData.TotalVisitSpecialCustomerCount;
        _totalExterminationGatecrasherCustomer1Count = loadData.TotalExterminationGatecrasherCustomer1Count;
        _totalExterminationGatecrasherCustomer2Count = loadData.TotalExterminationGatecrasherCustomer2Count;

        _userId = string.IsNullOrWhiteSpace(loadData.UserId) || !loadData.UserId.StartsWith("User") ? "User" + UnityEngine.Random.Range(10000000, 20000000) : loadData.UserId;
        _firstAccessTime = loadData.FirstAccessTime;
        _lastAccessTime = loadData.LastAccessTime;
        _lastAttendanceTime = loadData.LastAttendanceTime;
        _totalAttendanceDays = loadData.TotalAttendanceDays;
 
        _giveRecipeLevelDic = loadData.GiveRecipeLevelDic;
        _recipeCookCountDic = loadData.RecipeCookCountDic;

        _giveGachaItemCountDic = loadData.GiveGachaItemCountDic;
        _giveGachaItemLevelDic = loadData.GiveGachaItemLevelDic;

        _doneMainChallengeSet = loadData.DoneMainChallengeSet;
        _clearMainChallengeSet = loadData.ClearMainChallengeSet;
        _doneAllTimeChallengeSet = loadData.DoneAllTimeChallengeSet;
        _clearAllTimeChallengeSet = loadData.ClearAllTimeChallengeSet;
        _doneDailyChallengeSet = loadData.DoneDailyChallengeSet;
        _clearDailyChallengeSet = loadData.ClearDailyChallengeSet;

        _enabledCustomerSet = loadData.EnabledCustomerSet;
        _visitedCustomerSet = loadData.VisitedCustomerSet;

        _saveCoinAreaDataList = loadData.SaveCoinAreaDataList;
        _saveGarbageAreaDataList = loadData.SaveGarbageAreaDataList;

        _notificationMessageSet = loadData.NotificationMessageSet;

        if (CheckAttendance())
        {
            UpdateLastAccessTime();
            ResetDailyChallenges();
        }

        CheckFurnitureSetCount();
        CheckKitchenSetCount();

        OnChangeMoneyHandler?.Invoke();
        OnChangeTipHandler?.Invoke();
        OnChangeScoreHandler?.Invoke();
        OnAddCustomerCountHandler?.Invoke();
        OnAddPromotionCountHandler?.Invoke();
        OnAddAdvertisingViewCountHandler?.Invoke();
        OnAddCleanCountHandler?.Invoke();
        OnGiveStaffHandler?.Invoke();
        OnUpgradeStaffHandler?.Invoke();
        OnGiveRecipeHandler?.Invoke();
        OnUpgradeRecipeHandler?.Invoke();
        OnAddCookCountHandler?.Invoke();
        OnGiveGachaItemHandler?.Invoke();
        OnUpgradeGachaItemHandler?.Invoke();
        DebugLog.Log("������ �ε� �Ϸ�");
    }

    public static void SetFirstAccessTime(DateTime time)
    {
        if (string.IsNullOrEmpty(_userId))
            _userId = "User" + UnityEngine.Random.Range(10000000, 20000000);

        AddDia(100);
        AddMoney(15000);
        _firstAccessTime = time.ToString();
    }


    public static bool CheckAttendance()
    {
        if (string.IsNullOrWhiteSpace(_lastAttendanceTime))
            return true;

        DateTime currentServerTime = BackendManager.Instance.ServerTime;
        DateTime lastAttendanceTime = DateTime.Parse(_lastAttendanceTime);
        TimeSpan timeDifference = currentServerTime - lastAttendanceTime;

        return 1 <= timeDifference.TotalDays;
    }

    public static bool CheckLastAccessTime()
    {
        if (string.IsNullOrWhiteSpace(_lastAccessTime))
            return true;

        DateTime currentServerTime = BackendManager.Instance.ServerTime;
        DateTime lastAccessTime = DateTime.Parse(_lastAccessTime);
        TimeSpan timeDifference = currentServerTime - lastAccessTime;

        return 1 <= timeDifference.TotalDays;
    }


    public static void UpdateAttendanceData()
    {
        _lastAttendanceTime = BackendManager.Instance.ServerTime.ToString();
        _totalAttendanceDays += 1;
        OnUpdateAttendanceDataHandler?.Invoke();
    }

    public static int GetTotalAttendanceDays()
    {
        return _totalAttendanceDays;
    }

    public static void ChangeStage(EStage stage)
    {
        _currentStage = stage;
    }


    #region UserData


    public static void ChangeFloor(ERestaurantFloorType floorType)
    {
        if (floorType <= _currentFloor)
            return;

        _currentFloor = floorType;
        OnChangeFloorHandler?.Invoke();
    }

    public static void AddDia(int value)
    {
        _dia += value;
        _dia =  Mathf.Max(0, _dia);
        DataBindDia();
        OnChangeDiaHandler?.Invoke();
    }


    public static void AddMoney(long value)
    {
        _money += value;
        if (value < 0) value = 0;
        _totalAddMoney += value;
        _dailyAddMoney += value;
        DataBindMoney();
        OnChangeMoneyHandler?.Invoke();
    }


    public static void AddScore(int value)
    {
        _score += value;
        OnChangeScoreHandler?.Invoke();
    }

    public static void TipCollection(bool isWatchingAds = false)
    {
        int addMoneyValue = isWatchingAds ? _tip * 2 : _tip;
        AddMoney(addMoneyValue);
        _tip = 0;

        DataBindTip();
        OnChangeTipHandler?.Invoke();
    }


    public static void TipCollection(int value, bool isWatchingAds = false)
    {
        if(_tip < value)
        {
            DebugLog.LogError("���� �� ���� ���� ���� ȸ���Ϸ��� �մϴ�(Tip: " + _tip + ", Value: " + value + ")");
            return;
        }

        _tip -= value;
        int addMoneyValue = isWatchingAds ? value * 2 : value;
        AddMoney(addMoneyValue);
        DataBindTip();
        OnChangeTipHandler?.Invoke();
    }

    public static void AddTip(int value)
    {
        if (GameManager.Instance.MaxTipVolume <= _tip)
            return;

        _tip = _tip + value;
        DataBindTip();
        OnChangeTipHandler?.Invoke();
    }

    public static void AddCustomerCount()
    {
        _totalCumulativeCustomerCount += 1;
        _dailyCumulativeCustomerCount += 1;
        OnAddCustomerCountHandler?.Invoke();
    }

    public static void AddPromotionCount()
    {
        _promotionCount += 1;
        OnAddPromotionCountHandler?.Invoke();
    }

    public static void AddAdvertisingViewCount()
    {
        _totalAdvertisingViewCount += 1;
        _dailyAdvertisingViewCount += 1;
        OnAddAdvertisingViewCountHandler?.Invoke();
    }


    public static void AddCleanCount()
    {
        _totalCleanCount += 1;
        _dailyCleanCount += 1;
        OnAddCleanCountHandler?.Invoke();
    }

    public static void AddVisitSpecialCustomerCount()
    {
        _totalVisitSpecialCustomerCount += 1;
        OnVisitSpecialCustomerHandler?.Invoke();
    }

    public static void AddExterminationGatecrasherCustomerCount(CustomerData data)
    {
        if (data is GatecrasherCustomer1Data)
            _totalExterminationGatecrasherCustomer1Count += 1;

        else if (data is GatecrasherCustomer2Data)
            _totalExterminationGatecrasherCustomer2Count += 1;

        else
            return;

        OnExterminationGatecrasherCustomerHandler?.Invoke();
    }

    public static void AddUserGachaMachineCount(int cnt = 1)
    {
        _totalUseGachaMachineCount += cnt;
        OnUseGachaMachineHandler?.Invoke();
    }


    public static void DataBindTip()
    {
        DataBind.SetTextValue("Tip", _tip.ToString());
        DataBind.SetTextValue("TipStr", _tip.ToString("N0"));
        DataBind.SetTextValue("TipConvert", Utility.ConvertToMoney(_tip));
    }

    public static void DataBindMoney()
    {
        DataBind.SetTextValue("Money", _money.ToString());
        DataBind.SetTextValue("MoneyStr", _money.ToString("N0"));
        DataBind.SetTextValue("MoneyConvert", Utility.ConvertToMoney(_money));
    }

    public static void DataBindDia()
    {
        DataBind.SetTextValue("Dia", _dia.ToString());
        DataBind.SetTextValue("DiaStr", _dia.ToString("N0"));
        DataBind.SetTextValue("DiaConvert", Utility.ConvertToMoney(_dia));
    }

    public static void UpdateLastAccessTime()
    {
        BackendManager.Instance.GetServerTimeAsync((serverTime) => _lastAccessTime = serverTime.ToString());
    }


    public static bool IsScoreValid(ShopData data)
    {
        if (Score < data.BuyScore)
            return false;

        return true;
    }

    public static bool IsScoreValid(int score)
    {
        if (Score < score)
            return false;

        return true;
    }

    public static bool IsMoneyValid(ShopData data)
    {
        if (Money < data.BuyPrice)
            return false;

        return true;
    }

    public static bool IsMoneyValid(long money)
    {
        if (Money < money)
            return false;

        return true;
    }


    public static bool IsDiaValid(ShopData data)
    {
        if (Dia < data.BuyPrice)
            return false;

        return true;
    }

    public static bool IsDiaValid(int dia)
    {
        if (Dia < dia)
            return false;

        return true;
    }

    public static bool IsFloorValid(ERestaurantFloorType type)
    {
        if (type < _currentFloor)
            return false;

        return true;
    }

    #endregion

    #region StaffData

    public static void GiveStaff(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveStaff(data);
    }


    public static void GiveStaff(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveStaff(id);
    }


    public static bool IsGiveStaff(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveStaff(id);
    }


    public static bool IsGiveStaff(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveStaff(data.Id);
    }


    public static bool IsEquipStaff(EStage stage, ERestaurantFloorType floor, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipStaff(floor, data);
    }


    public static bool IsEquipStaff(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipStaff(data);
    }


    public static ERestaurantFloorType GetEquipStaffFloorType(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaffFloorType(data);
    }


    public static void SetEquipStaff(EStage stage, ERestaurantFloorType floor, StaffData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipStaff(floor, data);
    }


    public static void SetEquipStaff(EStage stage, ERestaurantFloorType floor, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipStaff(floor, id);
    }


    public static void SetNullEquipStaff(EStage stage, ERestaurantFloorType floor, StaffType type)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetNullEquipStaff(floor, type);
    }


    public static StaffData GetEquipStaff(EStage stage, ERestaurantFloorType floor, StaffType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaff(floor, type);
    }


    public static int GetStaffLevel(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetStaffLevel(data);
    }


    public static int GetStaffLevel(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetStaffLevel(id);
    }


    public static bool UpgradeStaff(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].UpgradeStaff(data);
    }


    public static bool UpgradeStaff(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].UpgradeStaff(id);
    }


    #endregion

    #region Furniture & Kitchen Data

    public static int GetFurnitureAndKitchenUtensilCount(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetFurnitureAndKitchenUtensilCount();
    }


    #endregion

    #region FurnitureData

    public static SetData GetEquipFurnitureSetData(EStage stage, ERestaurantFloorType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipFurnitureSetData(type);
    }


    public static void SetEquipFurnitureSetData(EStage stage, ERestaurantFloorType type, SetData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipFurnitureSetData(type, data);
    }


    public static void GiveFurniture(EStage stage, FurnitureData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveFurniture(data);
    }


    public static void GiveFurniture(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveFurniture(id);
    }

    public static int GetGiveFurnitureCount(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetGiveFurnitureCount();
    }


    public static bool IsGiveFurniture(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveFurniture(id);
    }

    public static bool IsGiveFurniture(EStage stage, FurnitureData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveFurniture(data);
    }


    public static bool IsEquipFurniture(EStage stage, ERestaurantFloorType floor, FurnitureData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipFurniture(floor, data);

    }

    public static bool IsEquipFurniture(EStage stage, ERestaurantFloorType floor, FurnitureType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipFurniture(floor, type);
    }


    public static ERestaurantFloorType GetEquipFurnitureFloorType(EStage stage, FurnitureData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipFurnitureFloorType(data);
    }

    public static ERestaurantFloorType GetEquipFurnitureFloorType(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipFurnitureFloorType(id);
    }

    public static FurnitureData GetEquipFurniture(EStage stage, ERestaurantFloorType floor, FurnitureType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipFurniture(floor, type);
    }


    public static void SetEquipFurniture(EStage stage, ERestaurantFloorType type, FurnitureData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipFurniture(type, data);
    }

    public static void SetEquipFurniture(EStage stage, ERestaurantFloorType type, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipFurniture(type, id);
    }


    public static void SetNullEquipFurniture(EStage stage, ERestaurantFloorType floor, FurnitureType type)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetNullEquipFurniture(floor, type);
    }


    #endregion

    #region KitchenData

    public static SetData GetEquipKitchenUntensilSetData(ERestaurantFloorType type)
    {
        return _kitchenuntensilEnabledSetData[(int)type];
    }

    public static void SetEquipKitchenUntensilSetData(ERestaurantFloorType type, SetData data)
    {
        if (_kitchenuntensilEnabledSetData[(int)type] == data)
            return;

        _kitchenuntensilEnabledSetData[(int)type] = data;
        OnChangeKitchenUtensilSetDataHandler?.Invoke();
    }


    public static void GiveKitchenUtensil(KitchenUtensilData data)
    {
        if (_giveKitchenUtensilList.Contains(data.Id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        _giveKitchenUtensilList.Add(data.Id);
        CheckKitchenSetCount();
        OnGiveKitchenUtensilHandler?.Invoke();
    }


    public static void GiveKitchenUtensil(string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.Log("�������� �ʴ� ID�Դϴ�" + id);
            return;
        }
        GiveKitchenUtensil(data);
    }

    public static int GetGiveKitchenUtensilCount()
    {
        return _giveKitchenUtensilList.Count;
    }


    public static bool IsGiveKitchenUtensil(string id)
    {
        return _giveKitchenUtensilList.Contains(id);
    }

    public static bool IsGiveKitchenUtensil(KitchenUtensilData data)
    {
        return _giveKitchenUtensilList.Contains(data.Id);
    }


    public static bool IsEquipKitchenUtensil(ERestaurantFloorType type, KitchenUtensilData data)
    {
        int typeIndex = (int)type;
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            if (_equipKitchenUtensilDatas[typeIndex, i] == null)
                continue;

            if (_equipKitchenUtensilDatas[typeIndex, i].Id == data.Id)
                return true;
        }

        return false;
    }

    public static ERestaurantFloorType GetEquipKitchenUtensilFloorType(KitchenUtensilData data)
    {
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            for (int j = 0, cntJ = (int)KitchenUtensilType.Length; j < cntJ; ++j)
            {
                if (_equipKitchenUtensilDatas[i, j] == null)
                    continue;

                if (_equipKitchenUtensilDatas[i, j].Id == data.Id)
                    return (ERestaurantFloorType)i;
            }
        }

        return ERestaurantFloorType.Error;
    }

    public static ERestaurantFloorType GetEquipKitchenUtensilFloorType(string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.LogError("�������� �ʴ� ID�Դϴ�" + id);
            return ERestaurantFloorType.Error;
        }

        return GetEquipKitchenUtensilFloorType(data);
    }


    public static void SetEquipKitchenUtensil(ERestaurantFloorType type, KitchenUtensilData data)
    {
        if (!_giveKitchenUtensilList.Contains(data.Id))
        {
            DebugLog.LogError("���� �ֹ� �ⱸ�� �������� �ʾҽ��ϴ�: " + data.Id);
            return;
        }

        _equipKitchenUtensilDatas[(int)type, (int)data.Type] = data;
        OnChangeKitchenUtensilHandler?.Invoke(type, data.Type);
    }

    public static void SetEquipKitchenUtensil(ERestaurantFloorType type, string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.LogError("�������� �ʴ� ID�Դϴ�" + id);
            return;
        }

        SetEquipKitchenUtensil(type, data);
    }


    public static void SetNullEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        _equipKitchenUtensilDatas[(int)floor, (int)type] = null;
        OnChangeKitchenUtensilHandler?.Invoke(floor, type);
    }

    public static KitchenUtensilData GetEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        return _equipKitchenUtensilDatas[(int)floor, (int)type];
    }

    #endregion

    #region EffectSetData

    public static int GetActivatedFurnitureEffectSetCount()
    {
        return _activatedFurnitureEffectSet.Count;
    }

    public static int GetActivatedKitchenUtensilEffectSetCount()
    {
        return _activatedKitchenUtensilEffectSet.Count;
    }

    public static bool IsActivatedFurnitureEffectSet(string setId)
    {
        if (_activatedFurnitureEffectSet.Contains(setId))
            return true;

        return false;
    }


    public static bool IsActivatedKitchenUtensilEffectSet(string setId)
    {
        if (_activatedKitchenUtensilEffectSet.Contains(setId))
            return true;

        return false;
    }


    public static int GetEffectSetFurnitureCount(string setId)
    {
        if (_activatedFurnitureEffectSet.Contains(setId))
            return ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT;

        if (_furnitureEffectSetCountDic.ContainsKey(setId))
            return _furnitureEffectSetCountDic[setId];

        _furnitureEffectSetCountDic.Add(setId, 0);
        return 0;
    }


    public static int GetEffectSetKitchenUtensilCount(string setId)
    {
        if (_activatedKitchenUtensilEffectSet.Contains(setId))
            return ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT;

        if (_kitchenUtensilEffectSetCountDic.ContainsKey(setId))
            return _kitchenUtensilEffectSetCountDic[setId];

        _kitchenUtensilEffectSetCountDic.Add(setId, 0);
        return 0;
    }

    private static void CheckFurnitureSetCount()
    {
        _furnitureEffectSetCountDic.Clear();
        string setId = string.Empty;

        for (int i = 0, cnt = _giveFurnitureList.Count; i < cnt; ++i)
        {
            var furnitureData = FurnitureDataManager.Instance.GetFurnitureData(_giveFurnitureList[i]);
            if (furnitureData == null)
                continue;

            setId = furnitureData.SetId;
            if (string.IsNullOrEmpty(setId) || SetDataManager.Instance.GetSetData(setId) == null)
                continue;

            if (_furnitureEffectSetCountDic.ContainsKey(setId))
                _furnitureEffectSetCountDic[setId] += 1;
            else
                _furnitureEffectSetCountDic.Add(setId, 1);
        }

        foreach (var data in _furnitureEffectSetCountDic)
        {
            if (_activatedFurnitureEffectSet.Contains(data.Key))
                continue;

            if (data.Value >= ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT)
                _activatedFurnitureEffectSet.Add(data.Key);
        }
    }


    private static void CheckKitchenSetCount()
    {
        _kitchenUtensilEffectSetCountDic.Clear();
        string setId = string.Empty;

        for (int i = 0, cnt = _giveKitchenUtensilList.Count; i < cnt; ++i)
        {
            var kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(_giveKitchenUtensilList[i]);
            if (kitchenData == null)
                continue;

            setId = kitchenData.SetId;
            if (string.IsNullOrEmpty(setId) || SetDataManager.Instance.GetSetData(setId) == null)
                continue;

            if (_kitchenUtensilEffectSetCountDic.ContainsKey(setId))
                _kitchenUtensilEffectSetCountDic[setId] += 1;
            else
                _kitchenUtensilEffectSetCountDic.Add(setId, 1);
        }

        foreach (var data in _kitchenUtensilEffectSetCountDic)
        {
            if (_activatedKitchenUtensilEffectSet.Contains(data.Key))
                continue;

            if (data.Value >= ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT)
                _activatedKitchenUtensilEffectSet.Add(data.Key);
        }
    }


    #endregion

    #region FoodData

    public static void GiveRecipe(FoodData data)
    {
        if (_giveRecipeLevelDic.ContainsKey(data.Id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        _giveRecipeLevelDic.Add(data.Id, 1);
        OnGiveRecipeHandler?.Invoke();
    }


    public static void GiveRecipe(string id)
    {
        if (_giveRecipeLevelDic.ContainsKey(id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        FoodData data = FoodDataManager.Instance.GetFoodData(id);
        if(data == null)
        {
            DebugLog.Log("�������� �ʴ� ID�Դϴ�" + id);
            return;
        }

        _giveRecipeLevelDic.Add(id, 1);
        OnGiveRecipeHandler?.Invoke();
    }


    public static List<string> GetGiveRecipeList()
    {
        return _giveRecipeLevelDic.Keys.ToList();
    }


    public static int GetRecipeLevel(string id)
    {
        if(_giveRecipeLevelDic.TryGetValue(id, out int level))
        {
            return level;
        }

        return 1;
    }

    public static int GetRecipeLevel(FoodData data)
    {
        if (_giveRecipeLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        throw new Exception("�ش� ������ �����ϰ� ���� �ʽ��ϴ�: " + data.Id);
    }

    public static int GetCookCount(string id)
    {
        if (_recipeCookCountDic.TryGetValue(id, out int count))
            return count;

        return 0;
    }

    public static int GetCookCount(FoodData data)
    {
        if (_recipeCookCountDic.TryGetValue(data.Id, out int count))
            return count;

        return 0;
    }

    public static void AddCookCount(string id)
    {
        if (_recipeCookCountDic.ContainsKey(id))
        {
            _recipeCookCountDic[id] += 1;
            _dailyCookCount += 1;
            _totalCookCount += 1;
            OnAddCookCountHandler?.Invoke();
        }
        else
        {
            _recipeCookCountDic.Add(id, 1);
            _dailyCookCount += 1;
            _totalCookCount += 1;
            OnAddCookCountHandler?.Invoke();
        }
    }


    public static bool UpgradeRecipe(string id)
    {
        if (_giveRecipeLevelDic.TryGetValue(id, out int level))
        {
            FoodData data = FoodDataManager.Instance.GetFoodData(id);
            if (!IsMoneyValid(data))
            {
                DebugLog.LogError("�� ����: " + data.Id);
                return false;
            }

            if (data.UpgradeEnable(level))
            {
                _giveRecipeLevelDic[id] = level + 1;
                OnUpgradeRecipeHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("Level �ʰ�: " + id);
            return false;
        }

        DebugLog.LogError("���������� ����: " + id);
        return false;
    }


    public static bool UpgradeRecipe(FoodData data)
    {
        if (_giveRecipeLevelDic.TryGetValue(data.Id, out int level))
        {
            if(!IsMoneyValid(data))
            {
                DebugLog.LogError("�� ����: " + data.Id);
                return false;
            }

            if (FoodDataManager.Instance.GetFoodData(data.Id).UpgradeEnable(level))
            {
                _giveRecipeLevelDic[data.Id] = level + 1;
                OnUpgradeRecipeHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("Level �ʰ�: " + data.Id);
            return false;
        }

        DebugLog.LogError("���������� ����: " + data.Id);
        return false;
    }


    public static bool IsGiveRecipe(string id)
    {
        return _giveRecipeLevelDic.ContainsKey(id);
    }

    public static bool IsGiveRecipe(FoodData data)
    {
        return _giveRecipeLevelDic.ContainsKey(data.Id);
    }


    public static int GetRecipeCount()
    {
        return _giveRecipeLevelDic.Count;
    }


    #endregion

    #region ItemData

    public static Dictionary<string, int> GetGiveGachaItemCountDic()
    {
        return _giveGachaItemCountDic;
    }

    public static bool IsGiveGachaItem(GachaItemData data)
    {
        if (_giveGachaItemCountDic.ContainsKey(data.Id))
            return true;

        return false;
    }


    public static bool IsGiveGachaItem(string id)
    {
        if (_giveGachaItemCountDic.ContainsKey(id))
            return true;

        return false;
    }

    public static int GetGachaItemLevel(GachaItemData data)
    {
        if (_giveGachaItemLevelDic.TryGetValue(data.Id, out int level))
            return level;

        return 0;
    }


    public static int GetGachaItemLevel(string id)
    {
        if (_giveGachaItemLevelDic.TryGetValue(id, out int level))
            return level;

        return 0;
    }


    public static bool GiveGachaItem(GachaItemData data)
    {
        if (!ItemManager.Instance.IsGachaItem(data.Id))
        {
            DebugLog.Log("��í ������ ���̵� �ƴմϴ�: " + data.Id);
            return false;
        }

        if (_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            if (CanAddMoreItems(data))
            {
                _giveGachaItemCountDic[data.Id]++;
                OnGiveGachaItemHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("���̻� ȹ���� �� �����ϴ�: " + data.Id);
            return false;
        }

        _giveGachaItemCountDic.Add(data.Id, 0);
        _giveGachaItemLevelDic.Add(data.Id, 1);
        AddNotification(data.Id);
        OnGiveGachaItemHandler?.Invoke();
        return true;
    }


    public static void GiveGachaItem(List<GachaItemData> dataList)
    {
        for(int i = 0, cnt =  dataList.Count; i < cnt; ++i)
        {
            if (!ItemManager.Instance.IsGachaItem(dataList[i].Id))
            {
                DebugLog.Log("��í ������ ���̵� �ƴմϴ�: " + dataList[i].Id);
                continue;
            }

            if (_giveGachaItemCountDic.ContainsKey(dataList[i].Id))
            {
                if (CanAddMoreItems(dataList[i]))
                {
                    _giveGachaItemCountDic[dataList[i].Id]++;
                    AddNotification(dataList[i].Id);
                    continue;
                }

                DebugLog.LogError("���̻� ȹ���� �� �����ϴ�: " + dataList[i].Id);
                continue;
            }

            _giveGachaItemCountDic.Add(dataList[i].Id, 0);
            _giveGachaItemLevelDic.Add(dataList[i].Id, 1);
            AddNotification(dataList[i].Id);
        }
       
        OnGiveGachaItemHandler?.Invoke();
    }

    public static bool GiveGachaItem(string id)
    {
        GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
        if (data == null)
        {
            DebugLog.Log("��í ������ ���̵� �ƴմϴ�: " + data.Id);
            return false;
        }

        if (_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            if (CanAddMoreItems(data))
            {
                _giveGachaItemCountDic[data.Id]++;
                OnGiveGachaItemHandler?.Invoke();
                return true;
            }
            DebugLog.LogError("���̻� ȹ���� �� �����ϴ�: " + data.Id);
            return false;
        }

        _giveGachaItemCountDic.Add(data.Id, 0);
        _giveGachaItemLevelDic.Add(data.Id, 1);
        AddNotification(data.Id);
        OnGiveGachaItemHandler?.Invoke();
        return true;
    }


    public static int GetGiveItemCount(GachaItemData data)
    {
        if (_giveGachaItemCountDic.TryGetValue(data.Id, out int count))
            return count;

        return 0;
    }


    public static int GetGiveItemCount(string id)
    {
        GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
        if (data == null)
            throw new Exception("�ش� �ϴ� �������� �������� �ʽ��ϴ�: " + id);

        if (_giveGachaItemCountDic.TryGetValue(data.Id, out int count))
            return count;

        return 0;
    }



    public static bool UpgradeGachaItem(GachaItemData data)
    {
        if(!_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("�������� �������� �ƴմϴ�: " + data.Id);
            return false;
        }

        if (!IsGachaItemUpgradeRequirementMet(data))
        {
            DebugLog.LogError("���׷��̵带 �� �� �����ϴ�: " + data.Id);
            return false;
        }

        int currentItemCount = _giveGachaItemCountDic[data.Id];
        int requiredItemCount = GetUpgradeRequiredItemCount(data);
        if(currentItemCount < requiredItemCount)
        {
            DebugLog.LogError("�������� �������� ������ �����մϴ�: �ʿ� ����(" + requiredItemCount + "), ���� ����(" + currentItemCount + ")");
            return false;
        }

        _giveGachaItemLevelDic[data.Id]++;
        _giveGachaItemCountDic[data.Id] -= requiredItemCount;
        OnUpgradeGachaItemHandler?.Invoke();
        return true;
    }

    public static bool UpgradeGachaItem(string id)
    {
        GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
        if (data == null)
        {
            DebugLog.Log("��í ������ ���̵� �ƴմϴ�: " + data.Id);
            return false;
        }

        if (!_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("�������� �������� �ƴմϴ�: " + data.Id);
            return false;
        }

        if(!IsGachaItemUpgradeRequirementMet(data))
        {
            DebugLog.LogError("���׷��̵带 �� �� �����ϴ�: " + data.Id);
            return false;
        }

        int currentItemCount = _giveGachaItemCountDic[data.Id];
        int requiredItemCount = GetMaxUpgradeRequiredItemCount(data);
        if (currentItemCount < requiredItemCount)
        {
            DebugLog.LogError("�������� �������� ������ �����մϴ�: �ʿ� ����(" + requiredItemCount + "), ���� ����(" + currentItemCount + ")");
            return false;
        }

        _giveGachaItemLevelDic[data.Id]++;
        _giveGachaItemCountDic[data.Id] -= requiredItemCount;
        OnUpgradeGachaItemHandler?.Invoke();
        return true;
    }

    public static Dictionary<string, int> GetGiveGachaItemDic()
    {
        return _giveGachaItemCountDic;
    }

    public static Dictionary<string, int> GetGiveGachaItemLevelDic()
    {
        return _giveGachaItemLevelDic;
    }


    public static bool CanAddMoreItems(GachaItemData data)
    {
        if(_giveGachaItemCountDic.TryGetValue(data.Id, out int giveCount))
        {
            int itemLevel = _giveGachaItemLevelDic[data.Id];
            int maxLevel = data.MaxLevel;

            if(maxLevel <= itemLevel)
            {
                DebugLog.Log("�������� �̹� �ִ� �����Դϴ�.: " + data.Id + "Lv." + itemLevel);
                return false;
            }

            int requiredItems = 0;
            for(int level = itemLevel; level < maxLevel; level++)
            {
                requiredItems += level * ConstValue.ADD_ITEM_UPGRADE_COUNT;
            }

            if (giveCount < requiredItems)
                return true;

            return false;
        }

        return true;
    }

    public static int GetMaxUpgradeRequiredItemCount(GachaItemData data)
    {
        int requiredItems = 0;
        int maxLevel = data.MaxLevel;

        if (!_giveGachaItemLevelDic.TryGetValue(data.Id, out int itemLevel))
            return -1;

        if (maxLevel <= requiredItems)
            return 0;

        for (int level = itemLevel; level < maxLevel; level++)
        {
            requiredItems += level * ConstValue.ADD_ITEM_UPGRADE_COUNT;
        }

        return requiredItems;
    }

    public static int GetUpgradeRequiredItemCount(GachaItemData data)
    {
        if (!_giveGachaItemLevelDic.TryGetValue(data.Id, out int itemLevel))
            return 0;

        if (data.MaxLevel <= itemLevel)
            return 0;

        return itemLevel * ConstValue.ADD_ITEM_UPGRADE_COUNT;
    }

    public static bool IsGachaItemUpgradeEnabled(GachaItemData data)
    {
        if (!_giveGachaItemLevelDic.TryGetValue(data.Id, out int level))
            return false;

        if (data.MaxLevel <= level)
            return false;

        return true;
    }

    public static bool IsGachaItemUpgradeRequirementMet(GachaItemData data)
    {
        if (!_giveGachaItemLevelDic.TryGetValue(data.Id, out int level))
            return false;

        if (data.MaxLevel <= level)
            return false;

        if (!_giveGachaItemCountDic.TryGetValue(data.Id, out int itemCount))
            return false;

        int requiredItemCount = GetUpgradeRequiredItemCount(data);
        if (itemCount < requiredItemCount)
            return false;

        return true;
    }


    public static bool IsGachaItemMaxLevel(GachaItemData data)
    {
        if (!_giveGachaItemLevelDic.TryGetValue(data.Id, out int level))
            return false;

        if (data.MaxLevel <= level)
            return true;

        return false;
    }


    #endregion 

    #region CustomerData

    public static void CustomerEnabled(string id)
    {
        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        if (data == null)
        {
            DebugLog.LogError("�ش� Id���� ��ġ�ϴ� �մ� ������ �����ϴ�: " + id);
            return;
        }

        CustomerEnabled(data);
    }

    public static void CustomerEnabled(CustomerData data)
    {
        if (_enabledCustomerSet.Contains(data.Id))
            return;

        _enabledCustomerSet.Add(data.Id);
        OnEnabledCustomerHandler?.Invoke();
    }


    public static bool GetCustomerEnableState(CustomerData data)
    {
        return _enabledCustomerSet.Contains(data.Id);
    }

    public static bool GetCustomerEnableState(string id)
    {
        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        if (data == null)
        {
            DebugLog.LogError("�ش� Id���� ��ġ�ϴ� �մ� ������ �����ϴ�: " + id);
            return false;
        }

        return GetCustomerEnableState(data);
    }


    public static void CustomerVisits(CustomerData customer)
    {
        if (_visitedCustomerSet.Contains(customer.Id))
            return;

        _visitedCustomerSet.Add(customer.Id);
        OnVisitedCustomerHandler?.Invoke();
    }


    public static int GetVisitedCustomerCount()
    {
        return _visitedCustomerSet.Count;
    }

    public static CustomerVisitState GetCustomerVisitState(CustomerData data)
    {
        bool isScoreValid = IsScoreValid(data.MinScore);
        bool isGiveRecipe = string.IsNullOrEmpty(data.RequiredDish) || IsGiveRecipe(data.RequiredDish);
        bool isGiveItem = string.IsNullOrEmpty(data.RequiredItem) || IsGiveGachaItem(data.RequiredItem);

        return new CustomerVisitState(isScoreValid, isGiveRecipe, isGiveItem);
    }

    public static CustomerVisitState GetCustomerVisitState(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            DebugLog.LogError("�ش� �մ��� id�� �̻��մϴ�: " + id);
            return default;
        }

        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        if (data == null)
        {
            DebugLog.LogError("�ش� Id���� ��ġ�ϴ� �մ� ������ �����ϴ�: " + id);
            return default;
        }

        return GetCustomerVisitState(data);
    }

    #endregion

    #region ChallengeData

    public static int GetClearDailyChallengeCount()
    {
        return _clearDailyChallengeSet.Count;
    }

    public static bool GetIsDoneChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);

        switch (data.Challenges)
        {
            case Challenges.Main:
                return _doneMainChallengeSet.Contains(id);

            case Challenges.Daily:
                return _doneDailyChallengeSet.Contains(id);

            case Challenges.AllTime:
                return _doneAllTimeChallengeSet.Contains(id);
        }

        return false;
    }

    public static bool GetIsDoneChallenge(ChallengeData data)
    {
        switch (data.Challenges)
        {
            case Challenges.Main:
                return _doneMainChallengeSet.Contains(data.Id);

            case Challenges.Daily:
                return _doneDailyChallengeSet.Contains(data.Id);

            case Challenges.AllTime:
                return _doneAllTimeChallengeSet.Contains(data.Id);
        }

        return false;
    }


    public static bool GetIsClearChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);

        switch (data.Challenges)
        {
            case Challenges.Main:
                return _clearMainChallengeSet.Contains(id);

            case Challenges.Daily:
                return _clearDailyChallengeSet.Contains(id);

            case Challenges.AllTime:
                return _clearAllTimeChallengeSet.Contains(id);
        }

        return false;
    }


    public static bool GetIsClearChallenge(ChallengeData data)
    {
        switch (data.Challenges)
        {
            case Challenges.Main:
                return _clearMainChallengeSet.Contains(data.Id);

            case Challenges.Daily:
                return _clearDailyChallengeSet.Contains(data.Id);

            case Challenges.AllTime:
                return _clearAllTimeChallengeSet.Contains(data.Id);
        }

        return false;
    }


    public static void DoneChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);

        if (GetIsDoneChallenge(id))
        {
            DebugLog.LogError("�̹� �Ϸ� ó���� ���������Դϴ�: " + id);
            return;
        }

        if(GetIsClearChallenge(id))
        {
            DebugLog.LogError("�̹� Ŭ���� ó���� ���������Դϴ�: " + id);
            return;
        }

        switch (data.Challenges)
        {
            case Challenges.Main:
                _doneMainChallengeSet.Add(id);
                break;

            case Challenges.Daily:
                _doneDailyChallengeSet.Add(id);
                break;

            case Challenges.AllTime:
                _doneAllTimeChallengeSet.Add(id);
                break;
        }

        OnDoneChallengeHandler?.Invoke();
    }

    public static void DoneChallenge(ChallengeData data)
    {

        if (GetIsDoneChallenge(data))
        {
            DebugLog.LogError("�̹� �Ϸ� ó���� ���������Դϴ�: " + data.Id);
            return;
        }

        if (GetIsClearChallenge(data))
        {
            DebugLog.LogError("�̹� Ŭ���� ó���� ���������Դϴ�: " + data.Id);
            return;
        }

        switch (data.Challenges)
        {
            case Challenges.Main:
                _doneMainChallengeSet.Add(data.Id);
                break;

            case Challenges.Daily:
                _doneDailyChallengeSet.Add(data.Id);
                break;

            case Challenges.AllTime:
                _doneAllTimeChallengeSet.Add(data.Id);
                break;
        }

        OnDoneChallengeHandler?.Invoke();
    }


    public static void ClearChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);

        if (GetIsClearChallenge(id))
        {
            DebugLog.LogError("�̹� Ŭ���� ó���� ���������Դϴ�: " + id);
            return;
        }

        if(!GetIsDoneChallenge(id))
        {
            DebugLog.Log("�Ϸ� ó���� ���� ���� ���������Դϴ�: " + id);
            return;
        }

        switch (data.Challenges)
        {
            case Challenges.Main:
                _clearMainChallengeSet.Add(id);
                break;

            case Challenges.Daily:
                _clearDailyChallengeSet.Add(id);
                break;

            case Challenges.AllTime:
                _clearAllTimeChallengeSet.Add(id);
                break;
        }

        ChallengeManager.Instance.UpdateChallengeByChallenges(data.Challenges);
        OnClearChallengeHandler?.Invoke();
    }

    public static void ClearChallenge(ChallengeData data)
    {
        if (GetIsClearChallenge(data))
        {
            DebugLog.LogError("�̹� Ŭ���� ó���� ���������Դϴ�: " + data.Id);
            return;
        }

        if (!GetIsDoneChallenge(data))
        {
            DebugLog.Log("�Ϸ� ó���� ���� ���� ���������Դϴ�: " + data.Id);
            return;
        }

        switch (data.Challenges)
        {
            case Challenges.Main:
                _clearMainChallengeSet.Add(data.Id);
                break;

            case Challenges.Daily:
                _clearDailyChallengeSet.Add(data.Id);
                break;

            case Challenges.AllTime:
                _clearAllTimeChallengeSet.Add(data.Id);
                break;
        }

        ChallengeManager.Instance.UpdateChallengeByChallenges(data.Challenges);
        OnClearChallengeHandler?.Invoke();
    }


    public static void ResetDailyChallenges()
    {
        _dailyAddMoney = 0;
        _dailyAdvertisingViewCount = 0;
        _dailyCleanCount = 0;
        _dailyCookCount = 0;
        _dailyCumulativeCustomerCount = 0;

        _doneDailyChallengeSet.Clear();
        _clearDailyChallengeSet.Clear();

        ChallengeManager.Instance.UpdateChallengeByChallenges(Challenges.Daily);
        OnClearChallengeHandler?.Invoke();
        OnDoneChallengeHandler?.Invoke();
    }

    #endregion

    #region NotificationMessageData

    public static void AddNotification(string id)
    {
        if(string.IsNullOrWhiteSpace(id))
        {
            DebugLog.LogError("���� �˸��� ����Ϸ��� ID���� �߸�����ϴ�: " + id);
            return;
        }

        if (_notificationMessageSet.Contains(id))
            return;

        _notificationMessageSet.Add(id);
        OnAddNotificationHandler?.Invoke(id);
    }

    public static void RemoveNotification(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            DebugLog.LogError("���� �˸��� �����Ϸ��� ID���� �߸�����ϴ�: " + id);
            return;
        }

        if (!_notificationMessageSet.Contains(id))
            return;

        _notificationMessageSet.Remove(id);
        OnRemoveNotificationHandler?.Invoke(id);
    }

    public static bool IsAddNotification(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            DebugLog.LogError("���� �˸��� Ȯ���Ϸ��� ID���� �߸�����ϴ�: " + id);
            return false;
        }

        return _notificationMessageSet.Contains(id);
    }


    #endregion

    #region ȯ�� ����

    public static void ChangeGachaItemSortType(SortType sortType)
    {
        if (_gachaItemSortType == sortType)
            return;

        _gachaItemSortType = sortType;
        OnChangeGachaItemSortTypeHandler?.Invoke();
    }


    public static void ChangeCustomerSortType(SortType sortType)
    {
        if (_customerSortType == sortType)
            return;

        _customerSortType = sortType;
        OnChangeCustomerSortTypeHandler?.Invoke();
    }


    #endregion

}
