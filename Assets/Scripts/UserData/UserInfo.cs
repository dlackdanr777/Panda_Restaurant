using Muks.DataBind;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public static class UserInfo
{
    public static event Action OnChangeMoneyHandler;
    public static event Action OnChangeTipHandler;
    public static event Action OnChangeScoreHandler;
    public static event Action OnAddCustomerCountHandler;
    public static event Action OnAddPromotionCountHandler;

    public static event Action OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnUpgradeStaffHandler;

    public static event Action OnGiveRecipeHandler;
    public static event Action OnUpgradeRecipeHandler;
    public static event Action OnAddCookCountHandler;

    public static event Action<FurnitureType> OnChangeFurnitureHandler;
    public static event Action OnGiveFurnitureHandler;

    public static event Action<KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public static event Action OnGiveKitchenUtensilHandler;

    public static event Action OnDoneChallengeHandler;
    public static event Action OnClearChallengeHandler;

    public static event Action OnVisitedCustomerHandler;


    private static int _money;
    public static int Money => _money;

    private static int _maxMoney;
    public static int MaxMoney => _maxMoney;

    private static int _score;
    public static int Score => _score + GameManager.Instance.AddSocre;

    private static int _maxScore;
    public static int MaxScore
    {
        get
        {
            if(_maxScore < Score)
                _maxScore = Score;

            return _maxScore;
        }
    }

    private static int _tip;
    public static int Tip => _tip;

    private static int _cumulativeCustomerCount;
    public static int CumulativeCustomerCount => _cumulativeCustomerCount;

    private static int _promotionCount;
    public static int PromotionCount => _promotionCount;


    private static StaffData[] _equipStaffDatas = new StaffData[(int)StaffType.Length];
    private static List<string> _giveStaffList = new List<string>();
    private static HashSet<string> _giveStaffSet = new HashSet<string>();
    private static Dictionary<string, int> _giveStaffLevelDic = new Dictionary<string, int>();

    private static List<string> _giveRecipeList = new List<string>();
    private static HashSet<string> _giveRecipeSet = new HashSet<string>();
    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();
    private static Dictionary<string, int> _recipeCookCountDic = new Dictionary<string, int>();

    private static FurnitureData[] _equipFurnitureDatas = new FurnitureData[(int)FurnitureType.Length];
    private static List<string> _giveFurnitureList = new List<string>();
    private static HashSet<string> _giveFurnitureSet = new HashSet<string>();
    private static SetData _enabledSetData;

    private static KitchenUtensilData[] _equipKitchenUtensilDatas = new KitchenUtensilData[(int)KitchenUtensilType.Length];
    private static List<string> _giveKitchenUtensilList = new List<string>();
    private static HashSet<string> _giveKitchenUtensilSet = new HashSet<string>();

    private static HashSet<string> _doneChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearMainChallengeSet = new HashSet<string>();
    private static HashSet<string> _doneAllTimeChallengeSet = new HashSet<string>();
    private static HashSet<string> _clearAllTimeChallengeSet = new HashSet<string>();

    private static HashSet<string> _visitedCustomerSet = new HashSet<string>();

    #region UserData

    public static void AppendMoney(int value)
    {
        _money += value;
        _maxMoney += Mathf.Clamp(value, 0, 100000000);
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
        _tip = Mathf.Clamp(_tip + value, 0, GameManager.Instance.MaxTipVolume);
        DataBindTip();
        OnChangeTipHandler?.Invoke();
    }

    public static void AddCustomerCount()
    {
        _cumulativeCustomerCount += 1;
        OnAddCustomerCountHandler?.Invoke();
    }

    public static void AddPromotionCount()
    {
        _promotionCount += 1;
        OnAddPromotionCountHandler?.Invoke();
    }

    public static void DataBindTip()
    {
        DataBind.SetTextValue("Tip", _tip.ToString());
        DataBind.SetTextValue("TipStr", _tip.ToString("N0"));
        DataBind.SetTextValue("TipConvert", Utility.ConvertToNumber(_tip));
    }

    public static void DataBindMoney()
    {
        DataBind.SetTextValue("Money", _money.ToString());
        DataBind.SetTextValue("MoneyStr", _money.ToString("N0"));
        DataBind.SetTextValue("MoneyConvert", Utility.ConvertToNumber(_money));
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
        GameManager.Instance.AppendAddScore(data.GetAddScore(1));
        GameManager.Instance.AddTipMul(data.GetAddTipMul(1));
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
        GameManager.Instance.AppendAddScore(data.GetAddScore(1));
        GameManager.Instance.AddTipMul(data.GetAddTipMul(1));

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
                GameManager.Instance.AppendAddScore(-data.GetAddScore(level));
                GameManager.Instance.AddTipMul(-data.GetAddTipMul(level));
                GameManager.Instance.AppendAddScore(data.GetAddScore(level + 1));
                GameManager.Instance.AddTipMul(data.GetAddTipMul(level + 1));
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
                GameManager.Instance.AppendAddScore(-data.GetAddScore(level));
                GameManager.Instance.AddTipMul(-data.GetAddTipMul(level));
                GameManager.Instance.AppendAddScore(data.GetAddScore(level + 1));
                GameManager.Instance.AddTipMul(data.GetAddTipMul(level + 1));

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

    public static void AddCookCount(string id)
    {
        if (_recipeCookCountDic.ContainsKey(id))
        {
            _recipeCookCountDic[id] += 1;
            OnAddCookCountHandler?.Invoke();
            return;
        }

        _recipeCookCountDic.Add(id, 1);
        OnAddCookCountHandler?.Invoke();
    }


    public static bool UpgradeRecipe(string id)
    {
        if (_giveRecipeLevelDic.TryGetValue(id, out int level))
        {
            if(FoodDataManager.Instance.GetFoodData(id).UpgradeEnable(level))
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

    #region FurnitureData

    public static void GiveFurniture(FurnitureData data)
    {
        if (_giveFurnitureSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        GameManager.Instance.AppendAddScore(data.AddScore);
        _giveFurnitureList.Add(data.Id);
        _giveFurnitureSet.Add(data.Id);
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

        GameManager.Instance.AppendAddScore(data.AddScore);
        _giveFurnitureList.Add(id);
        _giveFurnitureSet.Add(id);
        OnGiveFurnitureHandler?.Invoke();
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

        if (_equipFurnitureDatas[(int)data.Type] != null)
            _equipFurnitureDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)data.Type] = data;
        data.EffectData.AddSlot();
        CheckFurnitureSetEnabled();

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

        if (_equipFurnitureDatas[(int)data.Type] != null)
            _equipFurnitureDatas[((int)data.Type)].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)data.Type] = data;
        data.EffectData.AddSlot();
        CheckFurnitureSetEnabled();

        OnChangeFurnitureHandler?.Invoke(data.Type);
    }

    public static void DisarmEquipFurniture(FurnitureType type)
    {
        if (_equipFurnitureDatas[(int)type] != null)
            _equipFurnitureDatas[((int)type)].EffectData.RemoveSlot();

        _equipFurnitureDatas[(int)type] = null;
        OnChangeFurnitureHandler?.Invoke(type);
    }

    public static FurnitureData GetEquipFurniture(FurnitureType type)
    {
        return _equipFurnitureDatas[(int)type];
    }

    private static void CheckFurnitureSetEnabled()
    {
        string setId = string.Empty;
        for(int i = 0, cnt = _equipFurnitureDatas.Length; i < cnt; i++)
        {
            if (_equipFurnitureDatas[i] == null)
                return;

            if (string.IsNullOrEmpty(setId))
            {
                setId = _equipFurnitureDatas[i].SetId;
                continue;
            }

            if (setId != _equipFurnitureDatas[i].SetId)
                return;
        }

        if(_enabledSetData != null)
            _enabledSetData.Deactivate();

        _enabledSetData = SetDataManager.Instance.GetSetData(setId);
        _enabledSetData.Activate();
    }


    #endregion

    #region KitchenData

    public static void GiveKitchenUtensil(KitchenUtensilData data)
    {
        if (_giveKitchenUtensilSet.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        GameManager.Instance.AppendAddScore(data.AddScore);
        _giveKitchenUtensilList.Add(data.Id);
        _giveKitchenUtensilSet.Add(data.Id);
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

        GameManager.Instance.AppendAddScore(data.AddScore);
        _giveKitchenUtensilList.Add(id);
        _giveKitchenUtensilSet.Add(id);
        OnGiveKitchenUtensilHandler?.Invoke();
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

        if (_equipKitchenUtensilDatas[(int)data.Type] != null)
            _equipKitchenUtensilDatas[((int)data.Type)].RemoveSlot();

        _equipKitchenUtensilDatas[(int)data.Type] = data;
        data.AddSlot();
        CheckKitchenUtensilSetEnabled();

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

        if (_equipKitchenUtensilDatas[(int)data.Type] != null)
            _equipKitchenUtensilDatas[((int)data.Type)].RemoveSlot();

        _equipKitchenUtensilDatas[(int)data.Type] = data;
        data.AddSlot();
        CheckKitchenUtensilSetEnabled();

        OnChangeKitchenUtensilHandler?.Invoke(data.Type);
    }

    public static void DisarmEquipKitchenUtensil(KitchenUtensilType type)
    {
        if (_equipKitchenUtensilDatas[(int)type] != null)
            _equipKitchenUtensilDatas[((int)type)].RemoveSlot();

        _equipKitchenUtensilDatas[(int)type] = null;
        OnChangeKitchenUtensilHandler?.Invoke(type);
    }

    public static KitchenUtensilData GetEquipKitchenUtensil(KitchenUtensilType type)
    {
        return _equipKitchenUtensilDatas[(int)type];
    }

    private static void CheckKitchenUtensilSetEnabled()
    {
        string setId = string.Empty;
        for (int i = 0, cnt = _equipKitchenUtensilDatas.Length; i < cnt; i++)
        {
            if (_equipKitchenUtensilDatas[i] == null)
                return;

            if (string.IsNullOrEmpty(setId))
            {
                setId = _equipKitchenUtensilDatas[i].SetId;
                continue;
            }

            if (setId != _equipKitchenUtensilDatas[i].SetId)
                return;
        }

        if (_enabledSetData != null)
            _enabledSetData.Deactivate();

        _enabledSetData = SetDataManager.Instance.GetSetData(setId);
        _enabledSetData.Activate();
    }


    #endregion

    #region CustomerData

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

    #region Challenge

    public static bool GetIsDoneChallenge(string id)
    {
        if (_doneChallengeSet.Contains(id))
            return true;

        return false;
    }

    public static bool GetIsClearChallenge(string id)
    {
        if (_clearChallengeSet.Contains(id))
            return true;

        return false;
    }


    public static bool GetIsDoneMainChallenge(string id)
    {
        if (_doneMainChallengeSet.Contains(id))
            return true;

        return false;
    }


    public static bool GetIsClearMainChallenge(string id)
    {
        if (_clearMainChallengeSet.Contains(id))
            return true;

        return false;
    }


    public static void DoneMainChallenge(string id)
    {
        if (GetIsDoneMainChallenge(id))
        {
            DebugLog.LogError("이미 완료 처리된 도전과제입니다: " + id);
            return;
        }

        _doneMainChallengeSet.Add(id);
        _doneChallengeSet.Add(id);
        OnDoneChallengeHandler?.Invoke();
    }


    public static void ClearMainChallenge(string id)
    {
        if (GetIsClearMainChallenge(id))
        {
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + id);
            return;
        }

        _clearMainChallengeSet.Add(id);
        _clearChallengeSet.Add(id);
        OnClearChallengeHandler?.Invoke();
    }


    public static bool GetIsDoneAllTimeChallenge(string id)
    {
        if (_doneAllTimeChallengeSet.Contains(id))
            return true;

        return false;
    }


    public static bool GetIsClearAllTimeChallenge(string id)
    {
        if (_clearAllTimeChallengeSet.Contains(id))
            return true;

        return false;
    }


    public static void DoneAllTimeChallenge(string id)
    {
        if (GetIsDoneAllTimeChallenge(id))
        {
            DebugLog.LogError("이미 완료 처리된 도전과제입니다: " + id);
            return;
        }

        DebugLog.Log(id);
        _doneAllTimeChallengeSet.Add(id);
        _doneChallengeSet.Add(id);
        OnDoneChallengeHandler?.Invoke();
    }


    public static void ClearAllTimeChallenge(string id)
    {
        if (GetIsClearAllTimeChallenge(id))
        {
            DebugLog.LogError("이미 클리어 처리된 도전과제입니다: " + id);
            return;
        }

        _clearAllTimeChallengeSet.Add(id);
        _clearChallengeSet.Add(id);
        OnClearChallengeHandler?.Invoke();
    }


    #endregion

}
