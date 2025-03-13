using System.Collections.Generic;
using UnityEngine;

public class SetDataManager : MonoBehaviour
{
/*    public static SetDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("SetDataManager");
                _instance = obj.AddComponent<SetDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static SetDataManager _instance;

    public static int Count => _setDataList.Count;
    
    private static List<SetData> _setDataList = new List<SetData>();
    private static Dictionary<string, SetData> _setDataDic = new Dictionary<string, SetData>();


    public SetData GetSetData(string id)
    {
        if (!_setDataDic.TryGetValue(id, out SetData data))
            return null;

        return data;
    }

    public List<SetData> GetSetDataList()
    {
        return _setDataList;
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitSetData();
    }


    private static void InitSetData()
    {
*//*        if (0 < _setDataList.Count)
            return;

        _setDataList.Clear();
        _setDataDic.Clear();
        _setDataList.AddRange(Resources.LoadAll<SetData>("SetData"));

        SetData data;
        for (int i = 0, cnt = _setDataList.Count; i < cnt; i++)
        {
            data = _setDataList[i];
            _setDataDic.Add(data.Id, data);
        }*//*
    }*/
}
