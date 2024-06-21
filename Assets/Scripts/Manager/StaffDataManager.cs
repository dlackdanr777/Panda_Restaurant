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
                Init();
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
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
    }

    public List<StaffData> GetStaffDataList(StaffType type)
    {
        return _staffTypeDataList[(int)type];
    }

    public StaffType GetStaffType(StaffData data)
    {
        if (data is ManagerData)
            return StaffType.Manager;

        else if (data is WaiterData)
            return StaffType.Waiter;

        else if (data is ServerData)
            return StaffType.Server;

        else if (data is CleanerData)
            return StaffType.Cleaner;

        else if (data is MarketerData)
            return StaffType.Marketer;

        else if (data is ChefData)
            return StaffType.Chef;

        else throw new System.Exception("해당 스태프의 종류가 존재하지 않습니다.");
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
        _staffTypeDataList = new List<StaffData>[(int)StaffType.Length];

        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; i++)
        {
            _staffTypeDataList[i] = new List<StaffData>();
        }

        _staffDatas = Resources.LoadAll<StaffData>("StaffData");
        for(int i = 0, cnt = _staffDatas.Length; i < cnt; i++)
        {
            _staffDataDic.Add(_staffDatas[i].Id, _staffDatas[i]);
            _staffTypeDataList[(int)_instance.GetStaffType(_staffDatas[i])].Add(_staffDatas[i]);
        }
    }
}
