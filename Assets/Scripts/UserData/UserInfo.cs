using System;
using System.Collections.Generic;

public static class UserInfo
{
    public static event Action OnChangeMoneyHandler;
    public static event Action OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnUpgradeStaffHandler;
    public static event Action OnGiveRecipeHandler;
    public static event Action OnUpgradeRecipeHandler;

    private static StaffData[] _equipStaffDatas = new StaffData[(int)StaffType.Length];
    private static List<string> _giveStaffList = new List<string>();
    private static HashSet<string> _giveStaffSet = new HashSet<string>();
    private static Dictionary<string, int> _giveStaffLevelDic = new Dictionary<string, int>();

    private static List<string> _giveRecipeList = new List<string>();
    private static HashSet<string> _giveRecipeSet = new HashSet<string>();
    private static Dictionary<string, int> _giveRecipeLevelDic = new Dictionary<string, int>();

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
            if (StaffDataManager.Instance.GetStaffData(id).UpgradeEnable(level))
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


    #endregion

}
