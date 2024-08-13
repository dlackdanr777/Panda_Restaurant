using System.Collections.Generic;
using UnityEngine;

public class KitchenUtensilDataManager : MonoBehaviour
{
    public static KitchenUtensilDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("KitchenUtensilDataManager");
                _instance = obj.AddComponent<KitchenUtensilDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static KitchenUtensilDataManager _instance;

    private static List<KitchenUtensilData> _kitchenUtensilDataList = new List<KitchenUtensilData>();
    private static List<KitchenUtensilData>[] _kitchenUtensilDataListType = new List<KitchenUtensilData>[(int)KitchenUtensilType.Length];
    private static Dictionary<string, KitchenUtensilData> _kitchenUtensilDataDic = new Dictionary<string, KitchenUtensilData>();


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitData();
    }


    public KitchenUtensilData GetKitchenUtensilData(string id)
    {
        if (!_kitchenUtensilDataDic.TryGetValue(id, out KitchenUtensilData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }


    public List<KitchenUtensilData> GetKitchenUtensilDataList()
    {
        return _kitchenUtensilDataList;
    }


    public List<KitchenUtensilData> GetKitchenUtensilDataList(KitchenUtensilType type)
    {
        return _kitchenUtensilDataListType[(int)type];
    }


    private static void InitData()
    {
        if (0 < _kitchenUtensilDataList.Count)
            return;

        _kitchenUtensilDataList.Clear();
        _kitchenUtensilDataDic.Clear();

        for(int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            _kitchenUtensilDataListType[i] = new List<KitchenUtensilData>();
        }

        _kitchenUtensilDataList.AddRange(Resources.LoadAll<KitchenUtensilData>("KitchenUtensilData"));
        KitchenUtensilData data;
        for (int i = 0, cnt = _kitchenUtensilDataList.Count; i < cnt; i++)
        {
            data = _kitchenUtensilDataList[i];
            _kitchenUtensilDataDic.Add(data.Id, data);
            _kitchenUtensilDataListType[(int)data.Type].Add(data);
        }
    }
}
