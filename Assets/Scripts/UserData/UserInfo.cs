using BackEnd;
using LitJson;
using Muks.BackEnd;
using Muks.DataBind;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
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

    public static event Action<ERestaurantFloorType, EquipStaffType> OnChangeStaffHandler;
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

    public static event Action OnChangeSinkBowlHandler;

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
    public static bool IsFirstTutorialClear = false;
    public static bool IsMiniGameTutorialClear = false;
    public static bool IsGatecrasher1TutorialClear = false;
    public static bool IsGatecrasher2TutorialClear = false;
    public static bool IsSpecialCustomer1TutorialClear = false;
    public static bool IsSpecialCustomer2TutorialClear = false;

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
    public static int Score => GameManager.Instance.AddSocre;

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


    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _recipeCookCountDic = new Dictionary<string, int>();

    private static Dictionary<string, int> _giveGachaItemCountDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _giveGachaItemLevelDic = new Dictionary<string, int>();

    private static HashSet<string> _doneMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneDailyChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearDailyChallengeSet = new HashSet<string>();

    private static HashSet<string> _enabledCustomerSet = new HashSet<string>();
    private static HashSet<string> _visitedCustomerSet = new HashSet<string>();

    private static HashSet<string> _notificationMessageSet = new HashSet<string>(); //알림이 필요한 Id값을 모아두는 해쉬셋



    //################################환경 설정 관련 변수################################
    public static Action OnChangeGachaItemSortTypeHandler;
    public static Action OnChangeCustomerSortTypeHandler;


    public static SortType _customerSortType = SortType.None;
    public static SortType CustomerSortType => _customerSortType;

    private static SortType _gachaItemSortType = SortType.GradeDescending;
    public static SortType GachaItemSortType => _gachaItemSortType;


    private static StageInfo[] _stageInfos = new StageInfo[(int)EStage.Length];

    #region Init


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        for (int i = 0, cnt = _stageInfos.Length; i < cnt; ++i)
        {
            _stageInfos[i] = new StageInfo();

            _stageInfos[i].OnChangeFloorHandler += OnChangeFloorEvent;
            _stageInfos[i].OnChangeTipHandler += OnChangeTipEvent;

            _stageInfos[i].OnChangeStaffHandler += OnChangeStaffEvent;
            _stageInfos[i].OnGiveStaffHandler += OnGiveStaffEvent;
            _stageInfos[i].OnUpgradeStaffHandler += OnUpgradeStaffEvent;

            _stageInfos[i].OnChangeFurnitureHandler += OnChangeFurnitureEvent;
            _stageInfos[i].OnGiveFurnitureHandler += OnGiveFurnitureEvent;
            _stageInfos[i].OnChangeFurnitureSetDataHandler += OnChangeFurnitureSetDataEvent;

            _stageInfos[i].OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
            _stageInfos[i].OnGiveKitchenUtensilHandler += OnGiveKitchenUtensilEvent;
            _stageInfos[i].OnChangeKitchenUtensilSetDataHandler += OnChangeKitchenUtensilSetDataEvent;

            _stageInfos[i].OnAddSinkBowlHandler += OnAddSinkBowlEvent;
        }
    }

    private static void OnChangeFloorEvent()
    {
        OnChangeFloorHandler?.Invoke();
    }

    private static void OnChangeTipEvent()
    {
        OnChangeTipHandler?.Invoke();
    }

    private static void OnChangeStaffEvent(ERestaurantFloorType floor, EquipStaffType type)
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

    private static void OnAddSinkBowlEvent()
    {
        OnChangeSinkBowlHandler?.Invoke();
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

        param.Add("UnlockStage", (int)_unlockStage);
        param.Add("Dia", _dia);
        param.Add("Money", _money);
        param.Add("TotalAddMoney", _totalAddMoney);
        param.Add("DailyAddMoney", _dailyAddMoney);
        param.Add("Score", _score);
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
            DebugLog.LogError("현재 스테이지 정보가 없습니다: " + stage.ToString());
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
            DebugLog.LogError("현재 스테이지 정보가 없습니다: " + stage.ToString());
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
                Debug.LogError("저장된 데이터가 없습니다.");
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
            Debug.LogError("저장된 데이터가 없습니다.");
            return;
        }

        LoadUserData loadData = new LoadUserData(json);
        if (loadData == null)
        {
            Debug.LogError("로드 데이터를 파싱하는 과정에서 오류가 발생했습니다.");
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

        _notificationMessageSet = loadData.NotificationMessageSet;

        if (CheckNoAttendance())
        {
            UpdateLastAccessTime();
            ResetDailyChallenges();
        }

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
        DebugLog.Log("데이터 로드 완료");
    }

    public static void SetFirstAccessTime(DateTime time)
    {
        if (string.IsNullOrWhiteSpace(_userId))
            _userId = "User" + UnityEngine.Random.Range(10000000, 20000000);

        AddDia(100);
        AddMoney(15000);
        _firstAccessTime = time.ToString();
    }


    public static bool CheckNoAttendance()
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


    public static ERestaurantFloorType GetUnlockFloor(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].UnlockFloor;
    }

    public static void ChangeStage(EStage stage)
    {
        _currentStage = stage;
    }


    #region UserData


    public static void ChangeUnlockFloor(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].ChangeUnlockFloor(floor);
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


    public static int GetTip(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].Tip;
    }

    public static void TipCollection(EStage stage, bool isWatchingAds = false)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].TipCollection(isWatchingAds);
        DataBindTip(stage);
        OnChangeTipHandler?.Invoke();
    }


    public static void TipCollection(EStage stage, int value, bool isWatchingAds = false)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].TipCollection(value, isWatchingAds);
        DataBindTip(stage);
        OnChangeTipHandler?.Invoke();
    }


    public static void AddTip(EStage stage, int value)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].AddTip(value);
        DataBindTip(stage);
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


    public static void DataBindTip(EStage stage)
    {
        int stageIndex = (int)stage;
        DataBind.SetTextValue(stage.ToString() + "Tip", _stageInfos[stageIndex].Tip.ToString());
        DataBind.SetTextValue(stage.ToString() + "TipStr", _stageInfos[stageIndex].Tip.ToString("N0"));
        DataBind.SetTextValue(stage.ToString() + "TipConvert", Utility.ConvertToMoney(_stageInfos[stageIndex].Tip));
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

    public static bool IsFloorValid(EStage stage, ERestaurantFloorType type)
    {
        int stageIndex = (int)stage;
        ERestaurantFloorType unlockFloor = _stageInfos[stageIndex].UnlockFloor;
        return type <= unlockFloor;
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


    public static EquipStaffType GetEquipStaffType(EStage stage, StaffData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaffType(data);
    }


    public static void SetEquipStaff(EStage stage, ERestaurantFloorType floor, EquipStaffType type, StaffData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipStaff(floor, type, data);
    }


    public static void SetEquipStaff(EStage stage, ERestaurantFloorType floor, EquipStaffType type, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipStaff(floor, type, id);
    }


    public static void SetNullEquipStaff(EStage stage, ERestaurantFloorType floor, EquipStaffType type)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetNullEquipStaff(floor, type);
    }

    public static void SetNullEquipStaff(EStage stage, ERestaurantFloorType floor, StaffData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetNullEquipStaff(floor, data);
    }


    public static StaffData GetEquipStaff(EStage stage, ERestaurantFloorType floor, EquipStaffType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaff(floor, type);
    }


    public static StaffData GetEquipStaff(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        for(int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            EquipStaffType type = (EquipStaffType)i;
            StaffData data = GetEquipStaff(stage, floor, type);
            if (data == null)
                continue;

            return data;
        }

        return null;
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

    public static void GiveKitchenUtensil(EStage stage, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveKitchenUtensil(data);
    }


    public static void GiveKitchenUtensil(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].GiveKitchenUtensil(id);
    }

    public static int GetGiveKitchenUtensilCount(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetGiveKitchenUtensilCount();
    }


    public static bool IsGiveKitchenUtensil(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveKitchenUtensil(id);
    }


    public static bool IsGiveKitchenUtensil(EStage stage, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsGiveKitchenUtensil(data);
    }


    public static bool IsEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipKitchenUtensil(floor, data);
    }

    public static KitchenUtensilData GetEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, KitchenUtensilType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipKitchenUtensil(floor, type);
    }


    public static ERestaurantFloorType GetEquipKitchenUtensilFloorType(EStage stage, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipKitchenUtensilFloorType(data);
    }


    public static ERestaurantFloorType GetEquipKitchenUtensilFloorType(EStage stage, string id)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipKitchenUtensilFloorType(id);
    }


    public static void SetEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipKitchenUtensil(floor, data);
    }


    public static void SetEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, string id)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetEquipKitchenUtensil(floor, id);
    }


    public static void SetNullEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, KitchenUtensilType type)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetNullEquipKitchenUtensil(floor, type);
    }


    #endregion

    #region EffectSetData

    public static FoodType GetEquipFurnitureFoodType(EStage stage, ERestaurantFloorType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipFurnitureFoodType(type);
    }

    public static FoodType GetEquipKitchenUtensilFoodType(EStage stage, ERestaurantFloorType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipKitchenFoodType(type);
    }


    public static List<string> GetCollectKitchenUtensilSetDataList(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetCollectKitchenUtensilSetDataList();
    }


    public static List<string> GetCollectFurnitureSetDataList(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetCollectFurnitureSetDataList();
    }

    #endregion

    #region TableData

    public static SaveTableData GetTableData(EStage stage, ERestaurantFloorType floor, TableType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetTableData(floor, type);
    }


    #endregion

    #region FoodData

    public static void GiveRecipe(FoodData data)
    {
        if (_giveRecipeLevelDic.ContainsKey(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveRecipeLevelDic.Add(data.Id, 1);
        OnGiveRecipeHandler?.Invoke();
    }


    public static void GiveRecipe(string id)
    {
        if (_giveRecipeLevelDic.ContainsKey(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        FoodData data = FoodDataManager.Instance.GetFoodData(id);
        if(data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
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

        DebugLog.LogError("해당 음식을 보유하고 있지 않습니다: " + id);
        return 1;
    }

    public static int GetRecipeLevel(FoodData data)
    {
        if (_giveRecipeLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        DebugLog.LogError("해당 음식을 보유하고 있지 않습니다: " + data.Id);
        return 1;
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
                DebugLog.LogError("돈 부족: " + data.Id);
                return false;
            }

            if (data.UpgradeEnable(level))
            {
                _giveRecipeLevelDic[id] = level + 1;
                OnUpgradeRecipeHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("Level 초과: " + id);
            return false;
        }

        DebugLog.LogError("보유중이지 않음: " + id);
        return false;
    }


    public static bool UpgradeRecipe(FoodData data)
    {
        if (_giveRecipeLevelDic.TryGetValue(data.Id, out int level))
        {
            if(!IsMoneyValid(data))
            {
                DebugLog.LogError("돈 부족: " + data.Id);
                return false;
            }

            if (FoodDataManager.Instance.GetFoodData(data.Id).UpgradeEnable(level))
            {
                _giveRecipeLevelDic[data.Id] = level + 1;
                OnUpgradeRecipeHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("Level 초과: " + data.Id);
            return false;
        }

        DebugLog.LogError("보유중이지 않음: " + data.Id);
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
            DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + data.Id);
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

            DebugLog.LogError("더이상 획득할 수 없습니다: " + data.Id);
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
                DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + dataList[i].Id);
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

                DebugLog.LogError("더이상 획득할 수 없습니다: " + dataList[i].Id);
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
            DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + data.Id);
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
            DebugLog.LogError("더이상 획득할 수 없습니다: " + data.Id);
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
            throw new Exception("해당 하는 아이템이 존재하지 않습니다: " + id);

        if (_giveGachaItemCountDic.TryGetValue(data.Id, out int count))
            return count;

        return 0;
    }



    public static bool UpgradeGachaItem(GachaItemData data)
    {
        if(!_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("보유중인 아이템이 아닙니다: " + data.Id);
            return false;
        }

        if (!IsGachaItemUpgradeRequirementMet(data))
        {
            DebugLog.LogError("업그레이드를 할 수 없습니다: " + data.Id);
            return false;
        }

        int currentItemCount = _giveGachaItemCountDic[data.Id];
        int requiredItemCount = GetUpgradeRequiredItemCount(data);
        if(currentItemCount < requiredItemCount)
        {
            DebugLog.LogError("보유중인 아이템의 갯수가 부족합니다: 필요 수량(" + requiredItemCount + "), 보유 수량(" + currentItemCount + ")");
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
            DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + data.Id);
            return false;
        }

        if (!_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("보유중인 아이템이 아닙니다: " + data.Id);
            return false;
        }

        if(!IsGachaItemUpgradeRequirementMet(data))
        {
            DebugLog.LogError("업그레이드를 할 수 없습니다: " + data.Id);
            return false;
        }

        int currentItemCount = _giveGachaItemCountDic[data.Id];
        int requiredItemCount = GetMaxUpgradeRequiredItemCount(data);
        if (currentItemCount < requiredItemCount)
        {
            DebugLog.LogError("보유중인 아이템의 갯수가 부족합니다: 필요 수량(" + requiredItemCount + "), 보유 수량(" + currentItemCount + ")");
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
                DebugLog.Log("아이템이 이미 최대 레벨입니다.: " + data.Id + "Lv." + itemLevel);
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

    #region KitchenData

    public static int GetSinkBowlCount(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetSinkBowlCount(floor);
    }

    public static int GetMaxSinkBowlCount(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetMaxSinkBowlCount(floor);
    }

    public static void AddSinkBowlCount(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].AddSinkBowlCount(floor);
    }

    public static bool GetBowlAddEnabled(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetBowlAddEnabled(floor);
    }

    #endregion

    #region CustomerData

    public static void CustomerEnabled(string id)
    {
        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        if (data == null)
        {
            DebugLog.LogError("해당 Id값에 일치하는 손님 정보가 없습니다: " + id);
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
            DebugLog.LogError("해당 Id값에 일치하는 손님 정보가 없습니다: " + id);
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
            DebugLog.LogError("해당 손님의 id가 이상합니다: " + id);
            return default;
        }

        CustomerData data = CustomerDataManager.Instance.GetCustomerData(id);
        if (data == null)
        {
            DebugLog.LogError("해당 Id값에 일치하는 손님 정보가 없습니다: " + id);
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
            DebugLog.LogError("이미 완료 처리된 도전과제입니다: " + id);
            return;
        }

        if(GetIsClearChallenge(id))
        {
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + id);
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
            DebugLog.LogError("이미 완료 처리된 도전과제입니다: " + data.Id);
            return;
        }

        if (GetIsClearChallenge(data))
        {
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + data.Id);
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
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + id);
            return;
        }

        if(!GetIsDoneChallenge(id))
        {
            DebugLog.Log("완료 처리가 되지 않은 도전과제입니다: " + id);
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
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + data.Id);
            return;
        }

        if (!GetIsDoneChallenge(data))
        {
            DebugLog.Log("완료 처리가 되지 않은 도전과제입니다: " + data.Id);
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
            DebugLog.LogError("현재 알림을 등록하려한 ID값이 잘못됬습니다: " + id);
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
            DebugLog.LogError("현재 알림을 해제하려한 ID값이 잘못됬습니다: " + id);
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
            DebugLog.LogError("현재 알림을 확인하려한 ID값이 잘못됬습니다: " + id);
            return false;
        }

        return _notificationMessageSet.Contains(id);
    }


    #endregion

    #region 환경 설정

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
