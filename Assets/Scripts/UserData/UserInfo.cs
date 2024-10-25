using Muks.DataBind;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

public static class UserInfo
{
    public static event Action OnChangeMoneyHandler;
    public static event Action OnChangeTipHandler;
    public static event Action OnChangeScoreHandler;
    public static event Action OnAddCustomerCountHandler;
    public static event Action OnAddPromotionCountHandler;
    public static event Action OnAddAdvertisingViewCountHandler;
    public static event Action OnAddCleanCountHandler;

    public static event Action OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnUpgradeStaffHandler;

    public static event Action OnGiveRecipeHandler;
    public static event Action OnUpgradeRecipeHandler;
    public static event Action OnAddCookCountHandler;

    public static event Action OnGiveGachaItemHandler;
    public static event Action OnUpgradeGachaItemHandler;

    public static event Action<FurnitureType> OnChangeFurnitureHandler;
    public static event Action OnGiveFurnitureHandler;
    public static event Action OnChangeFurnitureSetDataHandler;

    public static event Action<KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public static event Action OnGiveKitchenUtensilHandler;
    public static event Action OnChangeKitchenUtensilSetDataHandler;

    public static event Action OnDoneChallengeHandler;
    public static event Action OnClearChallengeHandler;

    public static event Action OnVisitedCustomerHandler;


    private static int _money;
    public static int Money => _money;

    private static int _totalAddMoney;
    public static int TotalAddMoney => _totalAddMoney;

    private static int _dailyAddMoney;
    public static int DailyAddMoney => _dailyAddMoney;

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


    private static StaffData[] _equipStaffDatas = new StaffData[(int)StaffType.Length];
    private static List<string> _giveStaffList = new List<string>();
    private static HashSet<string> _giveStaffSet = new HashSet<string>();
    private static Dictionary<string, int> _giveStaffLevelDic = new Dictionary<string, int>();

    private static List<string> _giveRecipeList = new List<string>();
    private static HashSet<string> _giveRecipeSet = new HashSet<string>();
    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _recipeCookCountDic = new Dictionary<string, int>();

    private static Dictionary<string, int> _giveGachaItemCountDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _giveGachaItemLevelDic = new Dictionary<string, int>();

    private static FurnitureData[] _equipFurnitureDatas = new FurnitureData[(int)FurnitureType.Length];
    private static List<string> _giveFurnitureList = new List<string>();
    private static HashSet<string> _giveFurnitureSet = new HashSet<string>();

    private static SetData _furnitureEnabledSetData;
    private static SetData _kitchenuntensilEnabledSetData;

    private static KitchenUtensilData[] _equipKitchenUtensilDatas = new KitchenUtensilData[(int)KitchenUtensilType.Length];
    private static List<string> _giveKitchenUtensilList = new List<string>();
    private static HashSet<string> _giveKitchenUtensilSet = new HashSet<string>();


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

    private static HashSet<string> _visitedCustomerSet = new HashSet<string>();


    //################################환경 설정 관련 변수################################
    public static Action OnChangeGachaItemSortTypeHandler;
    public static Action OnChangeCustomerSortTypeHandler;


    public static SortType _customerSortType = SortType.None;
    public static SortType CustomerSortType => _customerSortType;

    private static SortType _gachaItemSortType = SortType.GradeDescending;
    public static SortType GachaItemSortType => _gachaItemSortType;



    #region UserData

    public static void AppendMoney(int value)
    {
        _money += value;
        _totalAddMoney += Mathf.Clamp(value, 0, 100000000);
        _dailyAddMoney += Mathf.Clamp(value, 0, 100000000);
        DataBindMoney();
        OnChangeMoneyHandler?.Invoke();
    }


    public static void AppendScore(int value)
    {
        _score += value;
        OnChangeScoreHandler?.Invoke();
    }

    public static void TipCollection(bool isWatchingAds = false)
    {
        int addMoneyValue = isWatchingAds ? _tip * 2 : _tip;
        AppendMoney(addMoneyValue);
        _tip = 0;

        DataBindTip();
        OnChangeTipHandler?.Invoke();
    }


    public static void TipCollection(int value, bool isWatchingAds = false)
    {
        if(_tip < value)
        {
            DebugLog.LogError("보유 팁 보다 많은 팁을 회수하려고 합니다(Tip: " + _tip + ", Value: " + value + ")");
            return;
        }

        _tip -= value;
        int addMoneyValue = isWatchingAds ? value * 2 : value;
        AppendMoney(addMoneyValue);
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

    public static bool IsMoneyValid(int money)
    {
        if (Money < money)
            return false;

        return true;
    }

    #endregion

    #region StaffData

    public static void GiveStaff(StaffData data)
    {
        if(_giveStaffSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveStaffList.Add(data.Id);
        _giveStaffSet.Add(data.Id);
        _giveStaffLevelDic.Add(data.Id, 1);
        OnGiveStaffHandler?.Invoke();
    }

    public static void GiveStaff(string id)
    {
        if (_giveStaffSet.Contains(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        if(data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다: " + id);
            return;
        }

        _giveStaffList.Add(id);
        _giveStaffSet.Add(id);
        _giveStaffLevelDic.Add(id, 1);
        OnGiveStaffHandler?.Invoke();
    }

    public static bool IsGiveStaff(string id)
    {
        return _giveStaffSet.Contains(id);
    }

    public static bool IsGiveStaff(StaffData data)
    {
        return _giveStaffSet.Contains(data.Id);
    }

    public static bool IsEquipStaff(StaffData data)
    {
        for(int i = 0, cnt = _equipStaffDatas.Length; i < cnt; i++)
        {
            if (_equipStaffDatas[i] == null)
                continue;
            
            if (_equipStaffDatas[i].Id == data.Id)
                return true;
        }

        return false;
    }

    public static void SetEquipStaff(StaffData data)
    {
        _equipStaffDatas[(int)StaffDataManager.Instance.GetStaffType(data)] = data;
        OnChangeStaffHandler?.Invoke();
    }

    public static StaffData GetEquipStaff(StaffType type)
    {
        return _equipStaffDatas[(int)type];
    }

    public static int GetStaffLevel(StaffData data)
    {
        if (_giveStaffLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + data.Id);
    }

    public static int GetStaffLevel(string id)
    {
        if (_giveStaffLevelDic.TryGetValue(id, out int level))
        {
            return level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + id);
    }

    public static bool UpgradeStaff(StaffData data)
    {
        if (_giveStaffLevelDic.TryGetValue(data.Id, out int level))
        {
            if(data.UpgradeEnable(level))
            {
                _giveStaffLevelDic[data.Id] = level + 1;
                OnUpgradeStaffHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("레벨 초과: " + data.Id);
            return false;
        }

        DebugLog.LogError("소유중이지 않음: " + data.Id);
        return false;
    }

    public static bool UpgradeStaff(string id)
    {
        if (_giveStaffLevelDic.TryGetValue(id, out int level))
        {
            StaffData data = StaffDataManager.Instance.GetStaffData(id);
            if (data.UpgradeEnable(level))
            {
                _giveStaffLevelDic[id] = level + 1;
                OnUpgradeStaffHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("레벨 초과: " + id);
            return false;
        }

        DebugLog.LogError("소유중이지 않음: " + id);
        return false;
    }

    #endregion

    #region FoodData

    public static void GiveRecipe(FoodData data)
    {
        if (_giveRecipeSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveRecipeList.Add(data.Id);
        _giveRecipeSet.Add(data.Id);
        _giveRecipeLevelDic.Add(data.Id, 1);
        OnGiveRecipeHandler?.Invoke();
    }


    public static void GiveRecipe(string id)
    {
        if (_giveRecipeSet.Contains(id))
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

        _giveRecipeList.Add(id);
        _giveRecipeSet.Add(id);
        _giveRecipeLevelDic.Add(id, 1);
        OnGiveRecipeHandler?.Invoke();
    }


    public static List<string> GetGiveRecipeList()
    {
        return _giveRecipeList;
    }


    public static int GetRecipeLevel(string id)
    {
        if(_giveRecipeLevelDic.TryGetValue(id, out int level))
        {
            return level;
        }

        throw new Exception("해당하는 ID의 음식을 보유하고 있지 않습니다: " + id);
    }

    public static int GetRecipeLevel(FoodData data)
    {
        if (_giveRecipeLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        throw new Exception("해당 음식을 보유하고 있지 않습니다: " + data.Id);
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
        return _giveRecipeSet.Contains(id);
    }

    public static bool IsGiveRecipe(FoodData data)
    {
        return _giveRecipeSet.Contains(data.Id);
    }


    public static int GetRecipeCount()
    {
        return _giveRecipeSet.Count;
    }


    #endregion

    #region ItemData

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
                    DebugLog.Log(dataList[i].Id + ", 보유량: " + (_giveGachaItemCountDic[dataList[i].Id]));
                    continue;
                }

                DebugLog.LogError("더이상 획득할 수 없습니다: " + dataList[i].Id);
                continue;
            }

            _giveGachaItemCountDic.Add(dataList[i].Id, 0);
            _giveGachaItemLevelDic.Add(dataList[i].Id, 1);
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

    #region Furniture & Kitchen Data

    public static int GetFurnitureAndKitchenUtensilCount()
    {
        return _giveKitchenUtensilSet.Count + _giveFurnitureSet.Count; 
    }


    #endregion

    #region FurnitureData

    public static SetData GetEquipFurnitureSetData()
    {
        return _furnitureEnabledSetData;
    }

    public static void SetEquipFurnitureSetData(SetData data)
    {
        if (_furnitureEnabledSetData == data)
            return;

        _furnitureEnabledSetData = data;
        OnChangeFurnitureSetDataHandler?.Invoke();
    }


    public static void GiveFurniture(FurnitureData data)
    {
        if (_giveFurnitureSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveFurnitureList.Add(data.Id);
        _giveFurnitureSet.Add(data.Id);
        CheckEffectSetCount();
        OnGiveFurnitureHandler?.Invoke();
    }


    public static void GiveFurniture(string id)
    {
        if (_giveFurnitureSet.Contains(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
            return;
        }

        _giveFurnitureList.Add(id);
        _giveFurnitureSet.Add(id);
        CheckEffectSetCount();
        OnGiveFurnitureHandler?.Invoke();
    }

    public static int GetGiveFurnitureCount()
    {
        return _giveFurnitureSet.Count;
    }


    public static bool IsGiveFurniture(string id)
    {
        return _giveFurnitureSet.Contains(id);
    }

    public static bool IsGiveFurniture(FurnitureData data)
    {
        return _giveFurnitureSet.Contains(data.Id);
    }


    public static bool IsEquipFurniture(FurnitureData data)
    {
        for (int i = 0, cnt = _equipFurnitureDatas.Length; i < cnt; i++)
        {
            if (_equipFurnitureDatas[i] == null)
                continue;

            if (_equipFurnitureDatas[i].Id == data.Id)
                return true;
        }

        return false;
    }

    public static void SetEquipFurniture(FurnitureData data)
    {
        if(!_giveFurnitureSet.Contains(data.Id))
        {
            DebugLog.LogError("현재 가구를 보유하지 않았습니다.");
            return;
        }

        if (_equipFurnitureDatas[(int)data.Type] != null && _equipFurnitureDatas[(int)data.Type].EffectData != null)
            _equipFurnitureDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)data.Type] = data;
        if (data.EffectData != null) data.EffectData.AddSlot();

        OnChangeFurnitureHandler?.Invoke(data.Type);
    }

    public static void SetEquipFurniture(string id)
    {
        if (!_giveFurnitureSet.Contains(id))
        {
            DebugLog.LogError("현재 가구를 보유하지 않았습니다.");
            return;
        }

        FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(id);

        if (_equipFurnitureDatas[(int)data.Type] != null && _equipFurnitureDatas[(int)data.Type].EffectData != null)
            _equipFurnitureDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)data.Type] = data;
        if(_equipFurnitureDatas[(int)data.Type].EffectData != null) data.EffectData.AddSlot();

        OnChangeFurnitureHandler?.Invoke(data.Type);
    }

    public static void DisarmEquipFurniture(FurnitureType type)
    {
        if (_equipFurnitureDatas[(int)type] != null && _equipFurnitureDatas[(int)type].EffectData != null)
            _equipFurnitureDatas[(int)type].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)type] = null;
        OnChangeFurnitureHandler?.Invoke(type);
    }

    public static FurnitureData GetEquipFurniture(FurnitureType type)
    {
        return _equipFurnitureDatas[(int)type];
    }

    #endregion

    #region KitchenData

    public static SetData GetEquipKitchenUntensilSetData()
    {
        return _kitchenuntensilEnabledSetData;
    }

    public static void SetEquipKitchenUntensilSetData(SetData data)
    {
        if (_kitchenuntensilEnabledSetData == data)
            return;

        _kitchenuntensilEnabledSetData = data;
        OnChangeKitchenUtensilSetDataHandler?.Invoke();
    }


    public static void GiveKitchenUtensil(KitchenUtensilData data)
    {
        if (_giveKitchenUtensilSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveKitchenUtensilList.Add(data.Id);
        _giveKitchenUtensilSet.Add(data.Id);
        CheckEffectSetCount();
        OnGiveKitchenUtensilHandler?.Invoke();
    }


    public static void GiveKitchenUtensil(string id)
    {
        if (_giveKitchenUtensilSet.Contains(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
            return;
        }

        _giveKitchenUtensilList.Add(id);
        _giveKitchenUtensilSet.Add(id);
        CheckEffectSetCount();
        OnGiveKitchenUtensilHandler?.Invoke();
    }

    public static int GetGiveKitchenUtensilCount()
    {
        return _giveKitchenUtensilSet.Count;
    }


    public static bool IsGiveKitchenUtensil(string id)
    {
        return _giveKitchenUtensilSet.Contains(id);
    }

    public static bool IsGiveKitchenUtensil(KitchenUtensilData data)
    {
        return _giveKitchenUtensilSet.Contains(data.Id);
    }


    public static bool IsEquipKitchenUtensil(KitchenUtensilData data)
    {
        for (int i = 0, cnt = _equipKitchenUtensilDatas.Length; i < cnt; i++)
        {
            if (_equipKitchenUtensilDatas[i] == null)
                continue;

            if (_equipKitchenUtensilDatas[i].Id == data.Id)
                return true;
        }

        return false;
    }

    public static void SetEquipKitchenUtensil(KitchenUtensilData data)
    {
        if (!_giveKitchenUtensilSet.Contains(data.Id))
        {
            DebugLog.LogError("현재 주방 기구를 보유하지 않았습니다.");
            return;
        }

        if (_equipKitchenUtensilDatas[(int)data.Type] != null && _equipKitchenUtensilDatas[(int)data.Type].EffectData != null)
            _equipKitchenUtensilDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipKitchenUtensilDatas[(int)data.Type] = data;
        if (_equipKitchenUtensilDatas[(int)data.Type].EffectData != null)
            data.EffectData.AddSlot();

        OnChangeKitchenUtensilHandler?.Invoke(data.Type);
    }

    public static void SetEquipKitchenUtensil(string id)
    {
        if (!_giveKitchenUtensilSet.Contains(id))
        {
            DebugLog.LogError("현재 가구를 보유하지 않았습니다.");
            return;
        }

        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);

        if (_equipKitchenUtensilDatas[(int)data.Type] != null && _equipKitchenUtensilDatas[(int)data.Type].EffectData != null)
            _equipKitchenUtensilDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipKitchenUtensilDatas[(int)data.Type] = data;
        if (_equipKitchenUtensilDatas[(int)data.Type].EffectData != null)
            data.EffectData.AddSlot();

        OnChangeKitchenUtensilHandler?.Invoke(data.Type);
    }

    public static void DisarmEquipKitchenUtensil(KitchenUtensilType type)
    {
        if (_equipKitchenUtensilDatas[(int)type] != null && _equipKitchenUtensilDatas[(int)type].EffectData != null)
            _equipKitchenUtensilDatas[((int)type)].EffectData.RemoveSlot();

        _equipKitchenUtensilDatas[(int)type] = null;
        OnChangeKitchenUtensilHandler?.Invoke(type);
    }

    public static KitchenUtensilData GetEquipKitchenUtensil(KitchenUtensilType type)
    {
        return _equipKitchenUtensilDatas[(int)type];
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


    public static void CheckEffectSetCount()
    {
        _furnitureEffectSetCountDic.Clear();
        _kitchenUtensilEffectSetCountDic.Clear();
        string setId = string.Empty;
        for(int i = 0, cnt = _giveFurnitureList.Count; i < cnt; ++i)
        {
            setId = FurnitureDataManager.Instance.GetFurnitureData(_giveFurnitureList[i]).SetId;

            if (SetDataManager.Instance.GetSetData(setId) == null)
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

            if (data.Value < ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT)
                continue;

            _activatedFurnitureEffectSet.Add(data.Key);
        }

        for (int i = 0, cnt = _giveKitchenUtensilList.Count; i < cnt; ++i)
        {
            setId = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(_giveKitchenUtensilList[i]).SetId;

            if (SetDataManager.Instance.GetSetData(setId) == null)
                continue;

            if (_kitchenUtensilEffectSetCountDic.ContainsKey(setId))
                _kitchenUtensilEffectSetCountDic[setId] += 1;

            else
                _kitchenUtensilEffectSetCountDic.Add(setId, 1);
        }

        foreach(var data in _kitchenUtensilEffectSetCountDic)
        {
            if (_activatedKitchenUtensilEffectSet.Contains(data.Key))
                continue;

            if (data.Value < ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT)
                continue;

            _activatedKitchenUtensilEffectSet.Add(data.Key);
        }
    }


    #endregion

    #region CustomerData

    public static bool IsCustomerVisitEnabled(CustomerData data)
    {
        bool gachaItemCheck = !string.IsNullOrWhiteSpace(data.RequiredItem) && !IsGiveGachaItem(data.RequiredItem);
        bool recipeCheck = !string.IsNullOrWhiteSpace(data.RequiredDish) && !IsGiveRecipe(data.RequiredDish);
        if (gachaItemCheck || recipeCheck || !IsScoreValid(data.MinScore))
            return false;

        return true;
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
