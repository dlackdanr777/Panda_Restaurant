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

    private static List<FurnitureSetData> _furnitureSetDataList = new List<FurnitureSetData>();
    private static Dictionary<string, FurnitureSetData> _furnitureSetDataDic = new Dictionary<string, FurnitureSetData>();



    public FurnitureData GetFurnitureData(string id)
    {
        if (!_furnitureDataDic.TryGetValue(id, out FurnitureData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }


    public FurnitureSetData GetFurnitureSetData(string id)
    {
        if (!_furnitureSetDataDic.TryGetValue(id, out FurnitureSetData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }


    public Dictionary<string, FurnitureSetData> GetFurnitureSetDic()
    {
        return _furnitureSetDataDic;
    }


    public List<FurnitureData> GetFurnitureDataList()
    {
        return _furnitureDataList;
    }


    public List<FurnitureData> GetFurnitureDataList(FurnitureType type)
    {
        return _furnitureDataListType[(int)type];
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitData();
        InitSetData();
    }


    private static void InitData()
    {
        if (0 < _furnitureDataList.Count)
            return;

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


    private static void InitSetData()
    {
        if (0 < _furnitureSetDataList.Count)
            return;

        _furnitureSetDataList.Clear();
        _furnitureSetDataDic.Clear();

        _furnitureSetDataList.AddRange(Resources.LoadAll<FurnitureSetData>("FurnitureSetData"));

        FurnitureSetData data;
        for (int i = 0, cnt = _furnitureSetDataList.Count; i < cnt; i++)
        {
            data = _furnitureSetDataList[i];
            _furnitureSetDataDic.Add(data.Id, data);
        }
    }
}
