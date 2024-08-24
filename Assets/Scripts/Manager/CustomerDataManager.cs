using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CustomerDataManager : MonoBehaviour
{
    public static CustomerDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("CustomerDataManager");
                _instance = obj.AddComponent<CustomerDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static CustomerDataManager _instance;

    private static List<CustomerData> _custoemrDataList = new List<CustomerData>();
    private static Dictionary<string, CustomerData> _customerDataDic = new Dictionary<string, CustomerData>();


    public CustomerData GetCustomerData(string id)
    {
        if (!_customerDataDic.TryGetValue(id, out CustomerData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
    }


    public List<CustomerData> GetAppearCustomerList()
    {
        List<CustomerData> returnList = new List<CustomerData>();
        for(int i = 0, cnt = _custoemrDataList.Count; i < cnt; ++i)
        {
            if (UserInfo.Score < _custoemrDataList[i].MinScore)
                continue;

            if (!UserInfo.IsGiveRecipe(_custoemrDataList[i].RequiredDish))
                continue;

            returnList.Add(_custoemrDataList[i]);
        }

        return returnList;
    }

    public List<CustomerData> GetSortCustomerList()
    {
        return UserInfo.CustomerSortType switch
        {
            SortType.NameAscending => _custoemrDataList.OrderBy(data => data.Name).ToList(),
            SortType.NameDescending => _custoemrDataList.OrderByDescending(data => data.Name).ToList(),
            SortType.GradeAscending => _custoemrDataList.OrderBy(data => data.Name).ToList(),
            SortType.GradeDescending => _custoemrDataList.OrderBy(data => data.Name).ToList(),
            _ => null
        };
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
        _customerDataDic.Clear();
        _custoemrDataList.Clear();
        _custoemrDataList.AddRange(Resources.LoadAll<CustomerData>("CustomerData"));
        for(int i = 0, cnt = _custoemrDataList.Count; i < cnt; i++)
        {
            _customerDataDic.Add(_custoemrDataList[i].Id, _custoemrDataList[i]);
        }
    }
}
