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
    public static event Action OnChangeSkinTokenHandler;

    public static event Action<ERestaurantFloorType, EquipStaffType> OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnUpgradeStaffHandler;
    public static event Action OnGiveStaffSkinHandler;
    public static event Action OnChangeStaffSkinHandler;


    public static event Action OnGiveRecipeHandler;
    public static event Action OnUpgradeRecipeHandler;
    public static event Action OnAddCookCountHandler;

    public static event Action OnUseGachaMachineHandler;
    public static event Action OnGiveGachaItemHandler;
    public static event Action OnUpgradeGachaItemHandler;

    public static event Action<ERestaurantFloorType, FurnitureType> OnChangeFurnitureHandler;
    public static event Action OnGiveFurnitureHandler;

    public static event Action<ERestaurantFloorType, KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public static event Action OnGiveKitchenUtensilHandler;

    public static event Action OnChangeSinkBowlHandler;
    public static event Action OnChangeMaxSinkBowlHandler;

    public static event Action OnChangeSatisfactionHandler;

    public static event Action OnDoneChallengeHandler;
    public static event Action OnClearChallengeHandler;

    public static event Action OnEnabledCustomerHandler;
    public static event Action OnVisitedCustomerHandler;
    public static event Action OnVisitSpecialCustomerHandler;
    public static event Action OnExterminationGatecrasherCustomerHandler;
    public static event Action OnGiveCustomerSkinHandler;
    public static event Action OnChangeCustomerSkinHandler;

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

    private static long _weeklyAddMoney;
    public static long WeeklyAddMoney => _weeklyAddMoney;

    private static int _score;
    public static int Score => GameManager.Instance.AddScore + _score;

    private static int _totalCookCount;
    public static int TotalCookCount => _totalCookCount;

    private static int _dailyCookCount;
    public static int DailyCookCount => _dailyCookCount;

    private static int _weeklyCookCount;
    public static int WeeklyCookCount => _weeklyCookCount;

    private static int _totalCumulativeCustomerCount;
    public static int TotalCumulativeCustomerCount => _totalCumulativeCustomerCount;

    private static int _dailyCumulativeCustomerCount;
    public static int DailyCumulativeCustomerCount => _dailyCumulativeCustomerCount;
    private static int _weeklyCumulativeCustomerCount;
    public static int WeeklyCumulativeCustomerCount => _weeklyCumulativeCustomerCount;

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

    private static int _weeklyCleanCount;
    public static int WeeklyCleanCount => _weeklyCleanCount;

    private static int _totalAttendanceDays = 0;
    public static int TotalAttendanceDays => _totalAttendanceDays;

    private static int _totalVisitSpecialCustomerCount;
    public static int TotalVisitSpecialCustomerCount => _totalVisitSpecialCustomerCount;

    private static int _totalExterminationGatecrasherCustomer1Count;
    public static int TotalExterminationGatecrasherCustomer1Count => _totalExterminationGatecrasherCustomer1Count;

    private static int _totalExterminationGatecrasherCustomer2Count;
    public static int TotalExterminationGatecrasherCustomer2Count => _totalExterminationGatecrasherCustomer2Count;

    private static int _weeklyExterminationGatecrasherCustomerCount;
    public static int WeeklyExterminationGatecrasherCustomerCount => _weeklyExterminationGatecrasherCustomerCount;

    private static int _totalUseGachaMachineCount;
    public static int TotalUseGachaMachineCount => _totalUseGachaMachineCount;


    private static HashSet<string> _giveStaffSkinSet = new HashSet<string>();

    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _recipeCookCountDic = new Dictionary<string, int>();

    private static Dictionary<string, int> _giveGachaItemCountDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _giveGachaItemLevelDic = new Dictionary<string, int>();

    private static Dictionary<string, SaveCustomerData> _enabledCustomerDic = new Dictionary<string, SaveCustomerData>();
    private static HashSet<string> _giveCustomerSkinSet = new HashSet<string>();

    private static HashSet<string> _doneMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearAllTimeChallengeSet = new HashSet<string>();
    public static HashSet<string> ClearAllTimeChallengeSet => _clearAllTimeChallengeSet;
    private static HashSet<string> _doneDailyChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearDailyChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneWeeklyChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearWeeklyChallengeSet = new HashSet<string>();

    private static HashSet<string> _notificationMessageSet = new HashSet<string>(); //알림이 필요한 Id값을 모아두는 해쉬셋
    private static HashSet<string> _clearNotificationMessageSet = new HashSet<string>(); //알림이 완료된 Id값을 모아두는 해쉬셋

    private static int _skinToken;
    public static int SkinToken => _skinToken;


    //################################환경 설정 관련 변수################################
    public static Action OnChangeGachaItemSortTypeHandler;
    public static Action OnChangeCustomerSortTypeHandler;


    public static GradeSortType _customerSortType = GradeSortType.None;
    public static GradeSortType CustomerSortType => _customerSortType;

    private static GradeSortType _gachaItemSortType = GradeSortType.GradeDescending;
    public static GradeSortType GachaItemSortType => _gachaItemSortType;
    private static GradeSortType _skinSortType = GradeSortType.GradeDescending;
    public static GradeSortType SkinSortType => _skinSortType;

    private static ShopSortType _furnitureSortType = ShopSortType.PriceAscending;
    public static ShopSortType FurnitureSortType => _furnitureSortType;

    private static ShopSortType _kitchenUtensilSortType = ShopSortType.PriceAscending;
    public static ShopSortType KitchenUtensilSortType => _kitchenUtensilSortType;

    private static ShopSortType _staffSortType = ShopSortType.PriceAscending;
    public static ShopSortType StaffSortType => _staffSortType;

    private static ShopSortType _foodSortType = ShopSortType.PriceAscending;
    public static ShopSortType FoodSortType => _foodSortType;


    private static StageInfo[] _stageInfos = new StageInfo[(int)EStage.Length];

    //####################광고 관련 설정 변수##########################
    private static int _addCustomerAdCount;
    public static int AddCustomerAdCount => _addCustomerAdCount;
    public static void AddAddCustomerAdCount() => _addCustomerAdCount++;
    private static int _doubleTipCounterAdCount;
    public static int DoubleTipCounterAdCount => _doubleTipCounterAdCount;
    public static void AddDoubleTipCounterAdCount() => _doubleTipCounterAdCount++;

    private static int _feverAdCount;
    public static int FeverAdCount => _feverAdCount;
    public static void AddFeverAdCount() => _feverAdCount++;

    private static int _dailyAdGoldRewardCount;
    public static int DailyAdGoldRewardCount => _dailyAdGoldRewardCount;
    public static void AddDailyAdGoldRewardCount() => _dailyAdGoldRewardCount++;
    private static int _dailyAdDiaRewardCount;
    public static int DailyAdDiaRewardCount => _dailyAdDiaRewardCount;
    public static void AddDailyAdDiaRewardCount() => _dailyAdDiaRewardCount++;

    //###############################################################

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

            _stageInfos[i].OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
            _stageInfos[i].OnGiveKitchenUtensilHandler += OnGiveKitchenUtensilEvent;

            _stageInfos[i].OnChangeSinkBowlHandler += OnAddSinkBowlEvent;
            _stageInfos[i].OnChangeMaxSinkBowlHandler += OnChangeMaxBowlEvent;
            _stageInfos[i].OnChangeSatisfactionHandler += OnChangeSatisfactionEvent;

            _stageInfos[i].OnChangeStaffSkinHandler += OnChangeStaffSkinEvent;
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


    private static void OnChangeKitchenUtensilEvent(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        OnChangeKitchenUtensilHandler?.Invoke(floor, type);
    }

    private static void OnGiveKitchenUtensilEvent()
    {
        OnGiveKitchenUtensilHandler?.Invoke();
    }


    private static void OnAddSinkBowlEvent()
    {
        OnChangeSinkBowlHandler?.Invoke();
    }

    public static void OnChangeMaxBowlEvent()

    {
        OnChangeMaxSinkBowlHandler?.Invoke();
    }


    private static void OnChangeSatisfactionEvent()
    {
        OnChangeSatisfactionHandler?.Invoke();
    }

    private static void OnChangeStaffSkinEvent()
    {
        OnChangeStaffSkinHandler?.Invoke();
    }


    #endregion

    public static Param GetSaveUserData()
    {
        Param param = new Param();

        if (CheckLastAccessTime())
        {
            UpdateLastAccessTime();
            ResetDailyChallenges();
            ResetAdCount();
        }

        if (CheckLastWeeklyAccessTime())
        {
            ResetWeeklyChallenges();
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

        param.Add("WeeklyAddMoney", _weeklyAddMoney);
        param.Add("WeeklyCookCount", _weeklyCookCount);
        param.Add("WeeklyCumulativeCustomerCount", _weeklyCumulativeCustomerCount);
        param.Add("WeeklyCleanCount", _weeklyCleanCount);
        param.Add("WeeklyExterminationGatecrasherCustomerCount", _weeklyExterminationGatecrasherCustomerCount);


        param.Add("UserId", _userId);
        param.Add("FirstAccessTime", _firstAccessTime);
        param.Add("LastAccessTime", BackendManager.Instance.ServerTime.ToString());
        param.Add("LastAttendanceTime", _lastAttendanceTime);
        param.Add("TotalAttendanceDays", _totalAttendanceDays);

        param.Add("SkinToken", _skinToken);

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

        List<SaveCustomerData> _saveCustomerDataList = _enabledCustomerDic.Values.ToList();

        param.Add("GiveStaffSkinList", _giveStaffSkinSet.ToList());
        param.Add("EnabledCustomerDataList", _saveCustomerDataList);
        param.Add("GiveCustomerSkinList", _giveCustomerSkinSet.ToList());

        param.Add("DoneMainChallengeList", _doneMainChallengeSet.ToList());
        param.Add("ClearMainChallengeList", _clearMainChallengeSet.ToList());
        param.Add("DoneAllTimeChallengeList", _doneAllTimeChallengeSet.ToList());
        param.Add("ClearAllTimeChallengeList", _clearAllTimeChallengeSet.ToList());
        param.Add("DoneDailyChallengeList", _doneDailyChallengeSet.ToList());
        param.Add("ClearDailyChallengeList", _clearDailyChallengeSet.ToList());
        param.Add("DoneWeeklyChallengeList", _doneWeeklyChallengeSet.ToList());
        param.Add("ClearWeeklyChallengeList", _clearWeeklyChallengeSet.ToList());


        param.Add("NotificationMessageList", _notificationMessageSet.ToList());
        param.Add("ClearNotificationMessageList", _clearNotificationMessageSet.ToList());


        //광고 관련 변수
        param.Add("AddCustomerAdCount", _addCustomerAdCount);
        param.Add("DoubleTipCounterAdCount", _doubleTipCounterAdCount);
        param.Add("FeverAdCount", _feverAdCount);

        param.Add("DailyAdGoldRewardCount", _dailyAdGoldRewardCount);
        param.Add("DailyAdDiaRewardCount", _dailyAdDiaRewardCount);
        //--------------------------------

        Dictionary<string, int> timeDic = TimeManager.Instance.GetTimeDic();
        List<SaveTimeData> timeDataList = new List<SaveTimeData>();
        foreach (var data in timeDic)
        {
            timeDataList.Add(new SaveTimeData(data.Key, data.Value));
        }
        param.Add("TimeDataList", timeDataList);

        return param;
    }


    public static void SaveStageData()
    {
        for (int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
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
        BackendManager.Instance.SaveGameData(stage.ToString() + "Data", param);
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
        BackendManager.Instance.SaveGameDataAsync(stage.ToString() + "Data", param);
    }


    public static void LoadStageData()
    {
        for (int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
        {
            LoadStageData((EStage)i);
        }
    }

    public static void LoadStageDataAsync()
    {
        for (int i = 0, cnt = (int)EStage.Length; i < cnt; ++i)
        {
            LoadStageDataAsync((EStage)i);
        }
    }


    public static void LoadStageData(EStage stage)
    {
        BackendReturnObject bro = BackendManager.Instance.GetMyData(stage.ToString() + "Data");

        JsonData json = bro.FlattenRows();
        if (json.Count <= 0)
        {
            Debug.LogError("저장된 데이터가 없습니다.");
            return;
        }

        ServerStageData data = new ServerStageData();
        data.SetData(json);
        _stageInfos[(int)stage].LoadData(data);
    }

    public static void LoadStageDataAsync(EStage stage)
    {
        BackendManager.Instance.GetMyDataAsync(stage.ToString() + "Data", (bro) =>
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
        if (!bro.IsSuccess())
        {
            Debug.LogError("bro Not Success");
            return;
        }

        JsonData json = bro.FlattenRows();
        if (json.Count <= 0)
        {
            Debug.LogError("No Server Data");
            return;
        }

        LoadUserData loadData = new LoadUserData(json);
        if (loadData == null)
        {
            Debug.LogError("Parsing Error");
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

        _weeklyAddMoney = loadData.WeeklyAddMoney;
        _weeklyCookCount = loadData.WeeklyCookCount;
        _weeklyCumulativeCustomerCount = loadData.WeeklyCumulativeCustomerCount;
        _weeklyCleanCount = loadData.WeeklyCleanCount;
        _weeklyExterminationGatecrasherCustomerCount = loadData.WeeklyExterminationGatecrasherCustomerCount;

        _userId = string.IsNullOrWhiteSpace(loadData.UserId) || !loadData.UserId.StartsWith("User") ? "User" + UnityEngine.Random.Range(10000000, 20000000) : loadData.UserId;
        _firstAccessTime = loadData.FirstAccessTime;
        _lastAccessTime = loadData.LastAccessTime;
        _lastAttendanceTime = loadData.LastAttendanceTime;
        _totalAttendanceDays = loadData.TotalAttendanceDays;

        _giveCustomerSkinSet = loadData.GiveCustomerSkinSet;
        _giveStaffSkinSet = loadData.GiveStaffSkinSet;

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
        _doneWeeklyChallengeSet = loadData.DoneWeeklyChallengeSet;
        _clearWeeklyChallengeSet = loadData.ClearWeeklyChallengeSet;

        _enabledCustomerDic = loadData.EnabledCustomerDataDic;

        _notificationMessageSet = loadData.NotificationMessageSet;
        _clearNotificationMessageSet = loadData.ClearNotificationMessageSet;

        _skinToken = loadData.SkinToken;

        _addCustomerAdCount = loadData.AddCustomerAdCount;
        _doubleTipCounterAdCount = loadData.DoubleTipCounterAdCount;
        _feverAdCount = loadData.FeverAdCount;

        _dailyAdGoldRewardCount = loadData.DailyAdGoldRewardCount;
        _dailyAdDiaRewardCount = loadData.DailyAdDiaRewardCount;

        if (CheckNoAttendance())
        {
            UpdateLastAccessTime();
            ResetDailyChallenges();
            ResetAdCount();
        }

        if (CheckLastWeeklyAccessTime())
        {
            ResetWeeklyChallenges();
        }

        Dictionary<string, SaveTimeData> timeDic = loadData.TimeDataDic;
        TimeManager.Instance.ResetTime();

        // 마지막 접속 시간과 현재 서버 시간의 차이(분) 계산
        int elapsedSeconds = 0;
        DateTime currentServerTime = GetKoreanTime();

        if (!string.IsNullOrEmpty(_lastAccessTime) && DateTime.TryParse(_lastAccessTime, out DateTime lastAccessTime))
        {
            // 시간 차이 계산 (분 단위)
            TimeSpan timeDifference = currentServerTime - lastAccessTime;
            elapsedSeconds = Mathf.Clamp((int)timeDifference.TotalSeconds, 0, int.MaxValue);

            DebugLog.Log($"마지막 접속 후 {elapsedSeconds}초가 경과했습니다.");
        }

        // 타이머 데이터 처리
        foreach (var data in timeDic)
        {
            // 경과 시간만큼 타이머 시간 감소
            int remainingTime = data.Value.Time - elapsedSeconds;

            // 남은 시간이 0보다 큰 경우에만 타이머 추가
            if (remainingTime > 0)
            {
                TimeManager.Instance.SetTime(data.Key, remainingTime);
                DebugLog.Log($"타이머 {data.Key}: {data.Value.Time}초 -> {remainingTime}초으로 조정됨");
            }
            else
            {
                DebugLog.Log($"타이머 {data.Key}: 시간 만료로 추가되지 않음");
            }
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

        try
        {
            // 서버 시간을 한국 시간으로 변환
            DateTime currentServerTime = GetKoreanTime();

            // 저장된 시간은 이미 한국 시간이므로 그대로 사용
            if (DateTime.TryParse(_lastAttendanceTime, out DateTime lastAttendanceTime))
            {
                TimeSpan timeDifference = currentServerTime - lastAttendanceTime;
                return 1 <= timeDifference.TotalDays;
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.LogError($"출석 체크 중 오류 발생: {ex.Message}");
            return true;
        }
    }

    public static bool CheckLastAccessTime()
    {
        if (string.IsNullOrWhiteSpace(_lastAccessTime))
            return true;

        try
        {
            // 현재 한국 시간 가져오기
            DateTime currentKoreaTime = GetKoreanTime();

            // 저장된 마지막 접속 시간은 이미 한국 시간이므로 그대로 파싱
            if (DateTime.TryParse(_lastAccessTime, out DateTime lastAccessTime))
            {
                // 게임 내 하루 기준: 오후 12시(정오)를 기준으로 날짜 계산
                DateTime currentGameDay = GetGameDay(currentKoreaTime);
                DateTime lastGameDay = GetGameDay(lastAccessTime);

                bool isDifferentDay = currentGameDay > lastGameDay;
                DebugLog.Log($"현재 시간: {currentKoreaTime}, 마지막 접속 시간: {lastAccessTime}");
                DebugLog.Log($"현재 게임 날짜: {currentGameDay}, 마지막 게임 날짜: {lastGameDay}, 날짜 차이: {isDifferentDay}");
                return isDifferentDay;
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.LogError($"접속 시간 확인 중 오류 발생: {ex.Message}");
            return true;  // 오류 발생 시 기본값으로 true 반환
        }
    }

    public static bool CheckLastWeeklyAccessTime()
    {
        if (string.IsNullOrWhiteSpace(_lastAccessTime))
            return true;

        try
        {
            // 현재 한국 시간 가져오기
            DateTime currentKoreaTime = GetKoreanTime();

            // 저장된 마지막 접속 시간은 이미 한국 시간이므로 그대로 파싱
            if (DateTime.TryParse(_lastAccessTime, out DateTime lastAccessTime))
            {
                // 게임 내 주간 기준: 매주 수요일 오후 12시를 기준으로 주차 계산
                DateTime currentGameWeek = GetGameWeek(currentKoreaTime);
                DateTime lastGameWeek = GetGameWeek(lastAccessTime);

                bool isDifferentWeek = currentGameWeek > lastGameWeek;
                DebugLog.Log($"현재 시간: {currentKoreaTime}, 마지막 접속 시간: {lastAccessTime}");
                DebugLog.Log($"현재 게임 주차: {currentGameWeek}, 마지막 게임 주차: {lastGameWeek}, 주차 차이: {isDifferentWeek}");
                return isDifferentWeek;
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.LogError($"주간 접속 시간 확인 중 오류 발생: {ex.Message}");
            return true;  // 오류 발생 시 기본값으로 true 반환
        }
    }

    // 오후 12시를 기준으로 게임 내 날짜를 계산하는 메서드
    private static DateTime GetGameDay(DateTime dateTime)
    {
        // 오후 12시(12:00) 이전이면 전날로 계산
        if (dateTime.Hour < 12)
        {
            return dateTime.Date.AddDays(-1);
        }
        // 오후 12시 이후면 당일로 계산
        else
        {
            return dateTime.Date;
        }
    }

    // 매주 수요일 오후 12시를 기준으로 게임 내 주차를 계산하는 메서드
    private static DateTime GetGameWeek(DateTime dateTime)
    {
        // 먼저 게임 날짜를 구함 (오후 12시 기준)
        DateTime gameDay = GetGameDay(dateTime);

        // 수요일을 기준으로 주차 계산
        // DayOfWeek.Wednesday = 3
        int daysFromWednesday = ((int)gameDay.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;

        // 가장 최근 수요일 (또는 오늘이 수요일이면 오늘)을 구함
        DateTime weekStart = gameDay.AddDays(-daysFromWednesday);

        return weekStart;
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

    public static float GetSatisfaction(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].Satisfaction;
    }

    public static void AddSatisfaction(EStage stage, float value)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].AddSatisfaction(value);
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
        _dia = Math.Max(0, _dia);
        DataBindDia();
        OnChangeDiaHandler?.Invoke();
    }


    public static void AddMoney(long value)
    {
        _money += value;
        if (value < 0) value = 0;
        _totalAddMoney += value;
        _dailyAddMoney += value;
        _weeklyAddMoney += value;
        DataBindMoney();
        OnChangeMoneyHandler?.Invoke();
    }

    public static void AddScore(int score)
    {
        if (score <= 0) return;
        _score += score;
        GameManager.Instance.OnChangeScoreEvent();
        OnChangeScoreHandler?.Invoke();
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
        _weeklyCumulativeCustomerCount += 1;
        OnAddCustomerCountHandler?.Invoke();
    }

    public static void AddCustomerCount(int count)
    {
        if (count <= 0) return;

        _totalCumulativeCustomerCount += count;
        _dailyCumulativeCustomerCount += count;
        _weeklyCumulativeCustomerCount += count;
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
        _weeklyCleanCount += 1;
        OnAddCleanCountHandler?.Invoke();
    }

    public static void AddCleanCount(int count)
    {
        _totalCleanCount += count;
        _dailyCleanCount += count;
        _weeklyCleanCount += count;
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
        _weeklyExterminationGatecrasherCustomerCount += 1;
        OnExterminationGatecrasherCustomerHandler?.Invoke();
    }

    public static void AddUserGachaMachineCount(int cnt = 1)
    {
        _totalUseGachaMachineCount += cnt;
        OnUseGachaMachineHandler?.Invoke();
    }

    public static void GiveSkin(SkinData data)
    {
        if (data is StaffSkinData staffSkinData)
        {
            GiveStaffSkin(staffSkinData);
        }
        else if (data is CustomerSkinData customerSkinData)
        {
            GiveCustomerSkin(customerSkinData);
        }
        else
        {
            DebugLog.LogError("스킨 데이터 타입이 올바르지 않습니다: " + data.Id);
            return;
        }
    }

    public static void GiveSkinList(List<SkinData> data)
    {
        foreach (var skin in data)
        {
            if (skin is StaffSkinData staffSkinData)
            {
                GiveStaffSkin(staffSkinData);
            }
            else if (skin is CustomerSkinData customerSkinData)
            {
                GiveCustomerSkin(customerSkinData);
            }
            else
            {
                DebugLog.LogError("스킨 데이터 타입이 올바르지 않습니다: " + skin.Id);
            }
        }
    }

    public static void AddSkinToken(int value)
    {
        _skinToken += value;
        DebugLog.Log($"스킨 토큰이 {value}만큼 추가되었습니다. 현재 스킨 토큰: {_skinToken}");
        OnChangeSkinTokenHandler?.Invoke();
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
        try
        {
            // 현재 한국 시간 가져와서 저장
            DateTime koreaTime = GetKoreanTime();
            _lastAccessTime = koreaTime.ToString();
            DebugLog.Log($"마지막 접속 시간 업데이트: {_lastAccessTime}");
        }
        catch (Exception ex)
        {
            DebugLog.LogError($"접속 시간 업데이트 중 오류 발생: {ex.Message}");
        }
    }

    // 현재 한국 시간(UTC+9)을 반환하는 메서드
    public static DateTime GetKoreanTime()
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // 안드로이드에서는 서버 시간에 9시간을 더한 값 사용
            return BackendManager.Instance.ServerTime.ToUniversalTime().AddHours(9);
#else
            try
            {
                // 다른 플랫폼에서는 시스템 타임존 사용 시도
                TimeZoneInfo koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(BackendManager.Instance.ServerTime.ToUniversalTime(), koreaTimeZone);
            }
            catch
            {
                // 타임존 정보를 찾을 수 없는 경우 UTC+9 사용
                return BackendManager.Instance.ServerTime.ToUniversalTime().AddHours(9);
            }
#endif
        }
        catch (Exception ex)
        {
            // 모든 방법 실패 시 로컬 시간 + 9시간 사용
            DebugLog.LogError($"한국 시간 변환 오류: {ex.Message}, 로컬 시간으로 대체합니다.");
            return DateTime.UtcNow.AddHours(9);
        }
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

    public static bool IsSkinTokenValid(int skinToken)
    {
        if (SkinToken < skinToken)
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

    public static bool IsEquipStaff(EStage stage, ERestaurantFloorType floor, EquipStaffType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipStaff(floor, type);
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
        for (int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            EquipStaffType type = (EquipStaffType)i;
            StaffData data = GetEquipStaff(stage, floor, type);
            if (data == null)
                continue;

            return data;
        }

        return null;
    }

    public static List<StaffData> GetGiveStaffDataList(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetGiveStaffDataList();
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


    public static void GiveStaffSkin(StaffSkinData data)
    {
        if (data == null)
        {
            DebugLog.LogError("고객 스킨 데이터가 null입니다.");
            return;
        }

        if (_giveStaffSkinSet.Contains(data.Id))
        {
            AddSkinToken(data.DuplicationToken);
            return;
        }

        _giveStaffSkinSet.Add(data.Id);
        OnGiveStaffSkinHandler?.Invoke();
    }

    public static void GiveStaffSkin(string skinId)
    {
        StaffSkinData skinData = SkinDataManager.Instance.GetStaffSkinData(skinId);
        if (skinData == null)
        {
            DebugLog.LogError("해당 스킨 아이디가 존재하지 않습니다: " + skinId);
            return;
        }

        GiveStaffSkin(skinData);
    }

    public static void SetStaffSkin(EStage stage, StaffData staff, StaffSkinData skinData)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetStaffSkin(staff, skinData);
    }


    public static void SetStaffSkin(EStage stage, StaffData staff, string skinId)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetStaffSkin(staff, skinId);
    }

    public static StaffSkinData GetEquipStaffSkin(EStage stage, StaffData staff)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaffSkin(staff);
    }

    public static StaffSkinData GetEquipStaffSkin(EStage stage, string staffId)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetEquipStaffSkin(staffId);
    }

    public static bool IsGiveStaffSkin(string skinId)
    {
        return _giveStaffSkinSet.Contains(skinId);
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

    public static List<FurnitureData> GetGiveFurnitureDataList(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetGiveFurnitureDataList();
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

    public static bool IsEquipFurniture(EStage stage, FurnitureData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipFurniture(data);

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

    public static List<KitchenUtensilData> GetGiveKitchenUtensilDataList(EStage stage)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].GetGiveKitchenUtensilDataList();
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

    public static bool IsEquipKitchenUtensil(EStage stage, ERestaurantFloorType floor, KitchenUtensilType type)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipKitchenUtensil(floor, type);
    }


    public static bool IsEquipKitchenUtensil(EStage stage, KitchenUtensilData data)
    {
        int stageIndex = (int)stage;
        return _stageInfos[stageIndex].IsEquipKitchenUtensil(data);
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

    public static void CheckFurnitureFoodType(EStage stage)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].CheckKitchenUtensilFoodType();
    }

    public static void CheckKitchenUtensilFoodType(EStage stage)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].CheckFurnitureFoodType();
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
        if (data == null)
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
        if (_giveRecipeLevelDic.TryGetValue(id, out int level))
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
            _weeklyCookCount += 1;
            OnAddCookCountHandler?.Invoke();
        }
        else
        {
            _recipeCookCountDic.Add(id, 1);
            _dailyCookCount += 1;
            _totalCookCount += 1;
            _weeklyCookCount += 1;
            OnAddCookCountHandler?.Invoke();
        }
    }

    public static void AddCookCount(string id, int count)
    {
        if (_recipeCookCountDic.ContainsKey(id))
        {
            _recipeCookCountDic[id] += count;
            _dailyCookCount += count;
            _totalCookCount += count;
            _weeklyCookCount += count;
            OnAddCookCountHandler?.Invoke();
        }
        else
        {
            _recipeCookCountDic.Add(id, count);
            _dailyCookCount += count;
            _totalCookCount += count;
            _weeklyCookCount += count;
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
            if (!IsMoneyValid(data))
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

    public static List<GachaItemData> GetGiveGachaItemDataList(UpgradeType upgradeType)
    {
        List<GachaItemData> dataList = new List<GachaItemData>();
        foreach (var item in _giveGachaItemCountDic)
        {
            GachaItemData data = ItemManager.Instance.GetGachaItemData(item.Key);
            if (data != null && data.UpgradeType == upgradeType)
                dataList.Add(data);
        }
        return dataList;
    }

    public static float GetGiveGachaItemValue(GachaItemData data)
    {
        if (_giveGachaItemCountDic.TryGetValue(data.Id, out int count))
        {
            float addValue = data.DefaultValue + ((count - 1) * data.UpgradeValue);
            return addValue;
        }

        return 0f;
    }

    public static float GetGiveGachaItemValue(string id)
    {
        GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
        if (data == null)
        {
            DebugLog.LogError("해당 아이템이 존재하지 않습니다: " + id);
            return 0f;
        }

        return GetGiveGachaItemValue(data);
    }


    public static int GetGachaItemLevel(string id)
    {
        if (_giveGachaItemLevelDic.TryGetValue(id, out int level))
            return level;

        return 0;
    }


    public static bool GiveGachaItem(GachaItemData data)
    {
        if (data == null)
        {
            DebugLog.LogError("가챠 아이템 데이터가 null입니다.");
            return false;
        }

        if (!ItemManager.Instance.IsGachaItem(data.Id))
        {
            DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + data.Id);
            return false;
        }

        if (_giveGachaItemCountDic.ContainsKey(data.Id))
        {
            _giveGachaItemCountDic[data.Id]++;
            OnGiveGachaItemHandler?.Invoke();
            return true;
        }

        _giveGachaItemCountDic.Add(data.Id, 1);
        _giveGachaItemLevelDic.Add(data.Id, 1);
        AddNotification(data.Id);
        OnGiveGachaItemHandler?.Invoke();
        DebugLog.Log("가챠 아이템 추가: " + data.Id + "  획득 갯수: " + _giveGachaItemCountDic[data.Id]);
        return true;
    }

    public static bool GiveGachaItem(string id)
    {
        GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
        if (data == null)
        {
            DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + data.Id);
            return false;
        }

        return GiveGachaItem(data);
    }


    public static void GiveGachaItem(List<GachaItemData> dataList)
    {
        for (int i = 0, cnt = dataList.Count; i < cnt; ++i)
        {
            if (dataList[i] == null)
            {
                DebugLog.LogError("가챠 아이템 데이터가 null입니다.");
                continue;
            }

            if (!ItemManager.Instance.IsGachaItem(dataList[i].Id))
            {
                DebugLog.Log("가챠 아이템 아이디가 아닙니다: " + dataList[i].Id);
                continue;
            }

            if (_giveGachaItemCountDic.ContainsKey(dataList[i].Id))
            {
                _giveGachaItemCountDic[dataList[i].Id]++;
                AddNotification(dataList[i].Id);
                continue;
            }

            _giveGachaItemCountDic.Add(dataList[i].Id, 1);
            _giveGachaItemLevelDic.Add(dataList[i].Id, 1);
            AddNotification(dataList[i].Id);
            DebugLog.Log("가챠 아이템 추가: " + dataList[i].Id + "  획득 갯수: " + _giveGachaItemCountDic[dataList[i].Id]);
        }

        OnGiveGachaItemHandler?.Invoke();
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
        if (!_giveGachaItemCountDic.ContainsKey(data.Id))
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

        if (!IsGachaItemUpgradeRequirementMet(data))
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


    // public static bool CanAddMoreItems(GachaItemData data)
    // {
    //     if(_giveGachaItemCountDic.TryGetValue(data.Id, out int giveCount))
    //     {
    //         int itemLevel = _giveGachaItemLevelDic[data.Id];
    //         int maxLevel = data.MaxLevel;

    //         if(maxLevel <= itemLevel)
    //         {
    //             DebugLog.Log("아이템이 이미 최대 레벨입니다.: " + data.Id + "Lv." + itemLevel);
    //             return false;
    //         }

    //         int requiredItems = 0;
    //         for(int level = itemLevel; level < maxLevel; level++)
    //         {
    //             requiredItems += level * ConstValue.ADD_ITEM_UPGRADE_COUNT;
    //         }

    //         if (giveCount < requiredItems)
    //             return true;

    //         return false;
    //     }

    //     return true;
    // }

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

        return Math.Max(1, itemLevel) * ConstValue.ADD_ITEM_UPGRADE_COUNT;
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

    public static void SetMaxSinkBowlCount(EStage stage, ERestaurantFloorType floor, int maxBowlCount)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SetMaxSinkBowlCount(floor, maxBowlCount);
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

    public static void SubSinkBowlCount(EStage stage, ERestaurantFloorType floor)
    {
        int stageIndex = (int)stage;
        _stageInfos[stageIndex].SubSinkBowlCount(floor);
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
        if (_enabledCustomerDic.ContainsKey(data.Id))
            return;

        _enabledCustomerDic.Add(data.Id, new SaveCustomerData(data.Id, string.Empty, 0));
        OnEnabledCustomerHandler?.Invoke();
    }


    public static bool GetCustomerEnableState(CustomerData data)
    {
        return _enabledCustomerDic.ContainsKey(data.Id);
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


    public static void GiveCustomerSkin(CustomerSkinData data)
    {
        if (data == null)
        {
            DebugLog.LogError("고객 스킨 데이터가 null입니다.");
            return;
        }

        if (_giveCustomerSkinSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다: " + data.Id);
            return;
        }

        _giveCustomerSkinSet.Add(data.Id);
        OnGiveCustomerSkinHandler?.Invoke();
    }

    public static void GiveCustomerSkin(string skinId)
    {
        CustomerSkinData skinData = SkinDataManager.Instance.GetCustomerSkinData(skinId);
        if (skinData == null)
        {
            DebugLog.LogError("해당 스킨 아이디가 존재하지 않습니다: " + skinId);
            return;
        }

        GiveCustomerSkin(skinData);
    }

    public static void SetCustomerSkin(CustomerData customer, CustomerSkinData skinData)
    {
        if (customer == null)
        {
            DebugLog.LogError("고객 데이터가 null입니다.");
            return;
        }

        if (!_enabledCustomerDic.TryGetValue(customer.Id, out SaveCustomerData saveData))
        {
            DebugLog.LogError("해당 고객이 활성화되지 않았습니다: " + customer.Id);
            return;
        }

        saveData.SetSkinId(skinData == null ? string.Empty : skinData.Id);
        OnChangeCustomerSkinHandler?.Invoke();
    }


    public static void SetCustomerSkin(CustomerData customer, string skinId)
    {
        if (customer == null)
        {
            DebugLog.LogError("고객 데이터가 null입니다.");
            return;
        }

        CustomerSkinData skinData = SkinDataManager.Instance.GetCustomerSkinData(skinId);
        if (skinData == null)
        {
            DebugLog.LogError("해당 스킨 아이디가 존재하지 않습니다: " + skinId);
            return;
        }
        SetCustomerSkin(customer, skinData);
    }

    public static CustomerSkinData GetEquipCustomerSkin(CustomerData customer)
    {
        if (customer == null)
        {
            DebugLog.LogError("고객 데이터가 null입니다.");
            return null;
        }

        if (!_enabledCustomerDic.TryGetValue(customer.Id, out SaveCustomerData saveData))
        {
            DebugLog.LogError("해당 고객이 활성화되지 않았습니다: " + customer.Id);
            return null;
        }

        string skinId = saveData.SkinId;
        if (string.IsNullOrEmpty(skinId))
        {
            DebugLog.Log("고객 스킨이 설정되어 있지 않습니다: " + customer.Id);
            return null;
        }

        CustomerSkinData skinData = SkinDataManager.Instance.GetCustomerSkinData(skinId);
        if (skinData == null)
        {
            DebugLog.LogError("해당 스킨 아이디가 존재하지 않습니다: " + skinId);
            return null;
        }

        return skinData;
    }

    public static CustomerSkinData GetEquipCustomerSkin(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            DebugLog.LogError("고객 ID가 비어있습니다.");
            return null;
        }

        CustomerData customer = CustomerDataManager.Instance.GetCustomerData(customerId);
        if (customer == null)
        {
            DebugLog.LogError("해당 Id값에 일치하는 손님 정보가 없습니다: " + customerId);
            return null;
        }

        return GetEquipCustomerSkin(customer);
    }

    public static bool IsGiveCustomerSkin(string skinId)
    {
        return _giveCustomerSkinSet.Contains(skinId);
    }


    public static void CustomerVisits(CustomerData customer)
    {
        if (!_enabledCustomerDic.TryGetValue(customer.Id, out SaveCustomerData data))
            return;

        _enabledCustomerDic[customer.Id].AddVisitCount();
        OnVisitedCustomerHandler?.Invoke();
    }

    public static void CustomerVisits(CustomerData customer, int visitCount)
    {
        if (visitCount <= 0)
        {
            DebugLog.LogError("방문 횟수는 0보다 커야 합니다: " + visitCount);
            return;
        }

        if (!_enabledCustomerDic.TryGetValue(customer.Id, out SaveCustomerData data))
            return;

        _enabledCustomerDic[customer.Id].AddVisitCount(visitCount);
        OnVisitedCustomerHandler?.Invoke();
    }

    public static int GetVisitedCustomerCount(string id)
    {
        if (_enabledCustomerDic.TryGetValue(id, out SaveCustomerData data))
            return data.VisitCount;

        DebugLog.LogError("해당 Id값에 일치하는 손님 정보가 없습니다: " + id);
        return 0;
    }

    public static int GetVisitedCustomerCount(CustomerData customer)
    {
        return GetVisitedCustomerCount(customer.Id);
    }


    public static int GetVisitedCustomerTypeCount()
    {
        int visitCount = 0;
        foreach (var customer in _enabledCustomerDic.Values)
        {
            if (0 < customer.VisitCount)
                visitCount++;
        }

        return visitCount;
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

    public static int GetClearWeeklyChallengeCount()
    {
        return _clearWeeklyChallengeSet.Count;
    }

    public static bool GetIsDoneChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);

        if (data == null)
            return false;

        switch (data.Challenges)
        {
            case Challenges.Main:
                return _doneMainChallengeSet.Contains(id);

            case Challenges.Daily:
                return _doneDailyChallengeSet.Contains(id);

            case Challenges.AllTime:
                return _doneAllTimeChallengeSet.Contains(id);

            case Challenges.Weekly:
                return _doneWeeklyChallengeSet.Contains(id);
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

            case Challenges.Weekly:
                return _doneWeeklyChallengeSet.Contains(data.Id);
        }

        return false;
    }


    public static bool GetIsClearChallenge(string id)
    {
        ChallengeData data = ChallengeManager.Instance.GetCallengeData(id);
        if (data == null)
            return false;

        switch (data.Challenges)
        {
            case Challenges.Main:
                return _clearMainChallengeSet.Contains(id);

            case Challenges.Daily:
                return _clearDailyChallengeSet.Contains(id);

            case Challenges.Weekly:
                return _clearWeeklyChallengeSet.Contains(id);

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

            case Challenges.Weekly:
                return _clearWeeklyChallengeSet.Contains(data.Id);

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

        if (GetIsClearChallenge(id))
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

            case Challenges.Weekly:
                _doneWeeklyChallengeSet.Add(id);
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

            case Challenges.Weekly:
                _doneWeeklyChallengeSet.Add(data.Id);
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

        if (!GetIsDoneChallenge(id))
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

            case Challenges.Weekly:
                _clearWeeklyChallengeSet.Add(id);
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

            case Challenges.Weekly:
                _clearWeeklyChallengeSet.Add(data.Id);
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

    public static void ResetWeeklyChallenges()
    {
        _weeklyAddMoney = 0;
        _weeklyCookCount = 0;
        _weeklyCumulativeCustomerCount = 0;
        _weeklyCleanCount = 0;
        _weeklyExterminationGatecrasherCustomerCount = 0;

        _doneWeeklyChallengeSet.Clear();
        _clearWeeklyChallengeSet.Clear();

        DebugLog.Log("주간 챌린지가 초기화되었습니다.");
    }

    #endregion

    #region NotificationMessageData
    public static void AddNotification(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
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

        if (!_clearNotificationMessageSet.Contains(id))
            _clearNotificationMessageSet.Add(id);

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

    public static bool IsClearNotification(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            DebugLog.LogError("현재 알림을 확인하려한 ID값이 잘못됬습니다: " + id);
            return false;
        }

        return _clearNotificationMessageSet.Contains(id);
    }

    #endregion

    #region 환경 설정


    public static void ChangeGachaItemSortType(GradeSortType sortType)
    {
        if (_gachaItemSortType == sortType)
            return;

        _gachaItemSortType = sortType;
        OnChangeGachaItemSortTypeHandler?.Invoke();
    }


    public static void ChangeCustomerSortType(GradeSortType sortType)
    {
        if (_customerSortType == sortType)
            return;

        _customerSortType = sortType;
        OnChangeCustomerSortTypeHandler?.Invoke();
    }


    #endregion


    #region Ad

    public static void ResetAdCount()
    {
        _addCustomerAdCount = 0;
        _feverAdCount = 0;
        _doubleTipCounterAdCount = 0;
        _dailyAdGoldRewardCount = 0;
        _dailyAdDiaRewardCount = 0;
    }

    #endregion

}
