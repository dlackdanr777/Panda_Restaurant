using System.Collections.Generic;
using UnityEngine;

public class FurnitureDataManager : MonoBehaviour
{
    public static FurnitureDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("FurnitureDataManager");
                _instance = obj.AddComponent<FurnitureDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static FurnitureDataManager _instance;

    private static List<FurnitureData> _furnitureDataList = new List<FurnitureData>();
    private static List<FurnitureData>[] _furnitureDataListType = new List<FurnitureData>[(int)FurnitureType.Length];
    private static Dictionary<string, FurnitureData> _furnitureDataDic = new Dictionary<string, FurnitureData>();



    public FurnitureData GetFurnitureData(string id)
    {
        if (!_furnitureDataDic.TryGetValue(id, out FurnitureData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }


    public List<FurnitureData> GetFoodDataList()
    {
        return _furnitureDataList;
    }


    public List<FurnitureData> GetFoodDataList(FurnitureType type)
    {
        return _furnitureDataListType[(int)type];
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
        _furnitureDataList.Clear();
        _furnitureDataDic.Clear();

        for(int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            _furnitureDataListType[i] = new List<FurnitureData>();
        }

        _furnitureDataList.AddRange(Resources.LoadAll<FurnitureData>("FurnitureData"));
        FurnitureData data;
        for (int i = 0, cnt = _furnitureDataList.Count; i < cnt; i++)
        {
            data = _furnitureDataList[i];
            _furnitureDataDic.Add(data.Id, data);
            _furnitureDataListType[(int)data.Type].Add(data);
        }
    }
}
