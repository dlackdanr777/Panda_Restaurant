using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class StaffDataManager : MonoBehaviour
{
    public static StaffDataManager Instance
    {
        get
        {
            if (_instance == null)
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
    private static List<StaffData>[] _staffTypeSortedCache;
    private static ShopSortType _lastSortType = (ShopSortType)(-1);

    // GroupType별 EquipStaffType 목록 사전 캐시 (GC 압력 제거)
    private static readonly Dictionary<StaffGroupType, List<EquipStaffType>> _equipStaffTypeCache =
        new Dictionary<StaffGroupType, List<EquipStaffType>>
        {
            { StaffGroupType.Manager,  new List<EquipStaffType> { EquipStaffType.Manager  } },
            { StaffGroupType.Chef,     new List<EquipStaffType> { EquipStaffType.Chef     } },
            { StaffGroupType.Waiter,   new List<EquipStaffType> { EquipStaffType.Waiter   } },
            { StaffGroupType.Cleaner,  new List<EquipStaffType> { EquipStaffType.Cleaner  } },
            { StaffGroupType.Marketer, new List<EquipStaffType> { EquipStaffType.Marketer } },
            { StaffGroupType.Guard,    new List<EquipStaffType> { EquipStaffType.Guard    } },
        };
    //private static Dictionary<string, MarketerLightStickData> _marketerLightStickDataDic = new Dictionary<string, MarketerLightStickData>();



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
        return _staffTypeDataList[typeIndex];
    }

    public List<StaffData> GetSortStaffDataList(EquipStaffType type)
    {
        int typeIndex = (int)GetStaffGroupType(type);
        ShopSortType sortType = UserInfo.StaffSortType;

        // 정렬 기준이 바뀌었을 때만 전체 캐시 재계산
        if (sortType != _lastSortType)
        {
            _lastSortType = sortType;
            int cnt = (int)StaffGroupType.Length;
            _staffTypeSortedCache = new List<StaffData>[cnt];
            for (int i = 0; i < cnt; i++)
            {
                _staffTypeSortedCache[i] = sortType switch
                {
                    ShopSortType.NameAscending  => _staffTypeDataList[i].OrderBy(d => d.Name).ToList(),
                    ShopSortType.NameDescending => _staffTypeDataList[i].OrderByDescending(d => d.Name).ToList(),
                    ShopSortType.PriceAscending => ShopItemSort.SortByPrice(_staffTypeDataList[i], true),
                    ShopSortType.PriceDescending => ShopItemSort.SortByPrice(_staffTypeDataList[i], false),
                    _ => _staffTypeDataList[i]
                };
            }
        }

        return _staffTypeSortedCache[typeIndex];
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
        return type switch
        {
            EquipStaffType.Manager  => StaffGroupType.Manager,
            EquipStaffType.Chef     => StaffGroupType.Chef,
            EquipStaffType.Waiter   => StaffGroupType.Waiter,
            EquipStaffType.Cleaner  => StaffGroupType.Cleaner,
            EquipStaffType.Marketer => StaffGroupType.Marketer,
            EquipStaffType.Guard    => StaffGroupType.Guard,
            _ => throw new System.Exception("해당 타입이 이상합니다: " + type)
        };
    }

    public List<EquipStaffType> GetEquipStaffTypeList(StaffData data)
    {
        StaffGroupType type = GetStaffGroupType(data);
        return GetEquipStaffType(type);
    }

    public List<EquipStaffType> GetEquipStaffType(StaffGroupType type)
    {
        if (_equipStaffTypeCache.TryGetValue(type, out var cached))
            return cached;

        throw new System.Exception("해당 타입이 이상합니다: " + type);
    }

    public RestaurantType GetStaffRestaurantType(StaffData data)
    {
        if (data is ManagerData || data is WaiterData || data is CleanerData || data is MarketerData || data is GuardData)
            return RestaurantType.Hall;

        else if (data is ChefData)
            return RestaurantType.Kitchen;

        throw new System.Exception("해당 타입이 이상합니다: " + data.Id);
    }


    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private static void Init()
    {
        _staffDataDic.Clear();
        _lastSortType = (ShopSortType)(-1);
        _staffTypeDataList = new List<StaffData>[(int)StaffGroupType.Length];

        for (int i = 0, cnt = (int)StaffGroupType.Length; i < cnt; i++)
        {
            _staffTypeDataList[i] = new List<StaffData>();
        }

        _staffDatas = Resources.LoadAll<StaffData>("StaffData");
        for (int i = 0, cnt = _staffDatas.Length; i < cnt; i++)
        {
            _staffDataDic.Add(_staffDatas[i].Id, _staffDatas[i]);
            _staffTypeDataList[(int)_instance.GetStaffGroupType(_staffDatas[i])].Add(_staffDatas[i]);
        }

        //InitMarketerLightStickData("StaffData/LightStickData");
    }

    // public MarketerLightStickData GetMarketerLightStickData(string id)
    // {
    //     if (!_marketerLightStickDataDic.TryGetValue(id, out MarketerLightStickData data))
    //     {
    //         float size = 1;
    //         float animeLeftPosX = 0;
    //         float animeLeftPosY = 0;
    //         float animeRightPosX = 0;
    //         float animeRightPosY = 0;
    //         float idleLeftPosX = 0;
    //         float idleLeftPosY = 0;
    //         float idleRightPosX = 0;
    //         float idleRightPosY = 0;

    //         MarketerLightStickData lightStickData = new MarketerLightStickData(size,
    //             new Vector2(animeLeftPosX, animeLeftPosY),
    //             new Vector2(animeRightPosX, animeRightPosY),
    //             new Vector2(idleLeftPosX, idleLeftPosY),
    //             new Vector2(idleRightPosX, idleRightPosY));

    //         _marketerLightStickDataDic.Add(id, lightStickData);
    //         data = lightStickData;
    //     }

    //     return data;
    // }


    // private static void InitMarketerLightStickData(string loadPath)
    // {
    //     _marketerLightStickDataDic.Clear();
    //     TextAsset csvData = Resources.Load<TextAsset>(loadPath);
    //     if (csvData == null)
    //     {
    //         Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
    //         return;
    //     }

    //     string[] data = csvData.text.Split('\n');
    //     for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
    //     {
    //         string[] row = data[i].Split(',');
    //         string id = row[0].Trim();

    //         if( string.IsNullOrEmpty(id))
    //             continue;

    //         DebugLog.Log($"LightStickData ID: {id}");
    //         DebugLog.Log($"Data Length: {row.Length}"); 
    //         float size = Utility.StrToFloat(row[2].Trim());
    //         float animeLeftPosX = Utility.StrToFloat(row[3].Trim());
    //         float animeLeftPosY = Utility.StrToFloat(row[4].Trim());
    //         float animeRightPosX = Utility.StrToFloat(row[5].Trim());
    //         float animeRightPosY = Utility.StrToFloat(row[6].Trim());
    //         float idleLeftPosX = Utility.StrToFloat(row[7].Trim());
    //         float idleLeftPosY = Utility.StrToFloat(row[8].Trim());
    //         float idleRightPosX = Utility.StrToFloat(row[9].Trim());
    //         float idleRightPosY = Utility.StrToFloat(row[10].Trim());

    //         MarketerLightStickData lightStickData = new MarketerLightStickData(size,
    //             new Vector2(animeLeftPosX, animeLeftPosY),
    //             new Vector2(animeRightPosX, animeRightPosY),
    //             new Vector2(idleLeftPosX, idleLeftPosY),
    //             new Vector2(idleRightPosX, idleRightPosY));

    //         _marketerLightStickDataDic.Add(id, lightStickData);
    //     }
    // }
}
