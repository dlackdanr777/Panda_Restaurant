using System.Collections.Generic;
using UnityEngine;

public class StaffDataManager : MonoBehaviour
{
    public static StaffDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("StaffDataManager");
                _instance = obj.AddComponent<StaffDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static StaffDataManager _instance;

    private static StaffData[] _staffDatas;
    private static Dictionary<string, StaffData> _staffDataDic = new Dictionary<string, StaffData>();
    private static List<StaffData>[] _staffTypeDataList;



    public StaffData GetStaffData(string id)
    {
        if (!_staffDataDic.TryGetValue(id, out StaffData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다:" + id);

        return data;
    }

    public List<StaffData> GetStaffDataList(StaffGroupType type)
    {
        return _staffTypeDataList[(int)type];
    }

    public List<StaffData> GetStaffDataList(EquipStaffType type)
    {
        int typeIndex = (int)GetStaffGroupType(type);
        return _staffTypeDataList[(int)typeIndex];
    }


    public StaffGroupType GetStaffGroupType(StaffData data)
    {
        if (data is ManagerData)
            return StaffGroupType.Manager;

        else if (data is WaiterData)
            return StaffGroupType.Waiter;

        else if (data is CleanerData)
            return StaffGroupType.Cleaner;

        else if (data is MarketerData)
            return StaffGroupType.Marketer;

        else if (data is GuardData)
            return StaffGroupType.Guard;

        else if (data is ChefData)
            return StaffGroupType.Chef;

        throw new System.Exception("해당 타입이 이상합니다: " + data.Id);
    }

    public StaffGroupType GetStaffGroupType(EquipStaffType type)
    {
        if (type == EquipStaffType.Manager)
            return StaffGroupType.Manager;

        else if (type == EquipStaffType.Waiter1 || type == EquipStaffType.Waiter2)
            return StaffGroupType.Waiter;

        else if (type == EquipStaffType.Cleaner)
            return StaffGroupType.Cleaner;

        else if (type == EquipStaffType.Marketer)
            return StaffGroupType.Marketer;

        else if (type == EquipStaffType.Guard)
            return StaffGroupType.Guard;

        else if (type == EquipStaffType.Chef1 || type == EquipStaffType.Chef2)
            return StaffGroupType.Chef;

        throw new System.Exception("해당 타입이 이상합니다: " + type);
    }

    public List<EquipStaffType> GetEquipStaffTypeList(StaffData data)
    {
        StaffGroupType type = GetStaffGroupType(data);
        return GetEquipStaffType(type);
    }

    public List<EquipStaffType> GetEquipStaffType(StaffGroupType type)
    {
        List<EquipStaffType> typeList = new List<EquipStaffType>();
        if (type == StaffGroupType.Manager)
            typeList.Add(EquipStaffType.Manager);

        if (type == StaffGroupType.Waiter)
            typeList.Add(EquipStaffType.Waiter1);

        if (type == StaffGroupType.Waiter)
            typeList.Add(EquipStaffType.Waiter2);

        if (type == StaffGroupType.Cleaner)
            typeList.Add(EquipStaffType.Cleaner);

        if (type == StaffGroupType.Marketer)
            typeList.Add(EquipStaffType.Marketer);

        if (type == StaffGroupType.Guard)
            typeList.Add(EquipStaffType.Guard);

        if (type == StaffGroupType.Chef)
            typeList.Add(EquipStaffType.Chef1);

        if (type == StaffGroupType.Chef)
            typeList.Add(EquipStaffType.Chef2);

        return typeList;
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(_instance);
        Init();
    }

    private static void Init()
    {
        _staffDataDic.Clear();
        _staffTypeDataList = new List<StaffData>[(int)EquipStaffType.Length];

        for(int i = 0, cnt = (int)StaffGroupType.Length; i < cnt; i++)
        {
            _staffTypeDataList[i] = new List<StaffData>();
        }

        _staffDatas = Resources.LoadAll<StaffData>("StaffData");
        for(int i = 0, cnt = _staffDatas.Length; i < cnt; i++)
        {
            _staffDataDic.Add(_staffDatas[i].Id, _staffDatas[i]);
            _staffTypeDataList[(int)_instance.GetStaffGroupType(_staffDatas[i])].Add(_staffDatas[i]);
        }
    }
}
