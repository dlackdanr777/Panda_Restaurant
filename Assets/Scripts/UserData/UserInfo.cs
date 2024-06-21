using System;
using System.Collections.Generic;

public static class UserInfo
{
    public static event Action OnChangeMoneyHandler;
    public static event Action OnChangeStaffHandler;
    public static event Action OnGiveStaffHandler;
    public static event Action OnGiveFoodHandler;

    private static StaffData[] _equipStaffDatas = new StaffData[(int)StaffType.Length];
    private static List<string> _giveStaffList = new List<string>();
    private static HashSet<string> _giveStaffSet = new HashSet<string>();

    private static List<string> _giveFoodList = new List<string>();
    private static HashSet<string> _giveFoodSet = new HashSet<string>();
    private static Dictionary<string, int> _giveFoodLevelDic = new Dictionary<string, int>();

    #region StaffData

    public static void GiveStaff(StaffData data)
    {
        if(_giveStaffSet.Contains(data.Id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        _giveStaffList.Add(data.Id);
        _giveStaffSet.Add(data.Id);
        OnGiveStaffHandler?.Invoke();
    }

    public static void GiveStaff(string id)
    {
        if (_giveStaffSet.Contains(id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        if(data == null)
        {
            DebugLog.Log("�������� �ʴ� ID�Դϴ�: " + id);
            return;
        }

        _giveStaffList.Add(id);
        _giveStaffSet.Add(id);
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

    public static StaffData GetSEquipStaff(StaffType type)
    {
        return _equipStaffDatas[(int)type];
    }

    #endregion

    #region FoodData

    public static void GiveFood(FoodData data)
    {
        if (_giveFoodSet.Contains(data.Id))
        {
            DebugLog.Log("�̹� ������ �ֽ��ϴ�.");
            return;
        }

        _giveFoodList.Add(data.Id);
        _giveFoodSet.Add(data.Id);
        _giveFoodLevelDic.Add(data.Id, 1);
        OnGiveFoodHandler?.Invoke();
    }


    public static void GiveFood(string id)
    {
        if (_giveFoodSet.Contains(id))
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

        _giveFoodList.Add(id);
        _giveFoodSet.Add(id);
        _giveFoodLevelDic.Add(id, 1);
        OnGiveFoodHandler?.Invoke();
    }

    public static int GetFoodLevel(string id)
    {
        if(_giveFoodLevelDic.TryGetValue(id, out int level))
        {
            return level;
        }

        throw new Exception("�ش��ϴ� ID�� ������ �����ϰ� ���� �ʽ��ϴ�: " + id);
    }

    public static int GetFoodLevel(FoodData data)
    {
        if (_giveFoodLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        throw new Exception("�ش� ������ �����ϰ� ���� �ʽ��ϴ�: " + data.Id);
    }


    public static bool IsGiveFood(string id)
    {
        return _giveFoodSet.Contains(id);
    }

    public static bool IsGiveFood(FoodData data)
    {
        return _giveFoodSet.Contains(data.Id);
    }


    #endregion

}
