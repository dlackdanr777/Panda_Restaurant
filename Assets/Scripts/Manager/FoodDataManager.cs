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

    private static FoodData[] _foodDatas;
    private static Dictionary<string, FoodData> _foodDataDic = new Dictionary<string, FoodData>();


    public FoodData GetFoodData(string id)
    {
        if (!_foodDataDic.TryGetValue(id, out FoodData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
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

        _foodDatas = Resources.LoadAll<FoodData>("FoodData");
        for(int i = 0, cnt = _foodDatas.Length; i < cnt; i++)
        {
            _foodDataDic.Add(_foodDatas[i].Id, _foodDatas[i]);
        }
    }
}
