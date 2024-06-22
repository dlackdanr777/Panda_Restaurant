using System.Collections.Generic;
using UnityEngine;

public class FoodDataManager : MonoBehaviour
{
    public static FoodDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("FoodDataManager");
                _instance = obj.AddComponent<FoodDataManager>();
                DontDestroyOnLoad(obj);
                Init();
            }

            return _instance;
        }
    }
    private static FoodDataManager _instance;


    public static int Count => _foodDataList.Count;
    private static List<FoodData> _foodDataList = new List<FoodData>();
    private static Dictionary<string, FoodData> _foodDataDic = new Dictionary<string, FoodData>();


    public FoodData GetFoodData(string id)
    {
        if (!_foodDataDic.TryGetValue(id, out FoodData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }

    public List<FoodData> GetFoodDataList()
    {
        return _foodDataList;
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
        _foodDataDic.Clear();
        _foodDataList.Clear();

        _foodDataList.AddRange(Resources.LoadAll<FoodData>("FoodData"));
        for (int i = 0, cnt = _foodDataList.Count; i < cnt; i++)
        {
            _foodDataDic.Add(_foodDataList[i].Id, _foodDataList[i]);
        }
    }
}
