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

    private static List<CustomerData> _customerDataList = new List<CustomerData>();
    private static List<CustomerData> _normalCustomerDataList = new List<CustomerData>();
    private static List<SpecialCustomerData> _specialCustomerDataList = new List<SpecialCustomerData>();
    private static List<GatecrasherCustomerData> _gatecrasherCustomerDataList = new List<GatecrasherCustomerData>();
    private static Dictionary<string, CustomerData> _customerDataDic = new Dictionary<string, CustomerData>();

    public CustomerData GetCustomerData(string id)
    {
        if (!_customerDataDic.TryGetValue(id, out CustomerData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
    }


    public List<CustomerData> GetAppearNormalCustomerList()
    {
        List<CustomerData> returnList = new List<CustomerData>();
        for(int i = 0, cnt = _normalCustomerDataList.Count; i < cnt; ++i)
        {
            if (!CheckAppearCustomer(_normalCustomerDataList[i]))
                continue;

            returnList.Add(_normalCustomerDataList[i]);
        }

        return returnList;
    }

    public List<SpecialCustomerData> GetAppearSpecialCustomerDataList()
    {
        List<SpecialCustomerData> returnList = new List<SpecialCustomerData>();
        for (int i = 0, cnt = _specialCustomerDataList.Count; i < cnt; ++i)
        {
            if (!CheckAppearCustomer(_specialCustomerDataList[i]))
                continue;

            returnList.Add(_specialCustomerDataList[i]);
        }

        return returnList;
    }

    public List<GatecrasherCustomerData> GetAppearGatecrasherCustomerDataList()
    {
        List<GatecrasherCustomerData> returnList = new List<GatecrasherCustomerData>();
        for (int i = 0, cnt = _gatecrasherCustomerDataList.Count; i < cnt; ++i)
        {
            if (!CheckAppearCustomer(_gatecrasherCustomerDataList[i]))
                continue;

            returnList.Add(_gatecrasherCustomerDataList[i]);
        }

        return returnList;
    }


    public List<CustomerData> GetSortCustomerList()
    {
        return UserInfo.CustomerSortType switch
        {
            SortType.NameAscending => _customerDataList.OrderBy(data => data.Name).ToList(),
            SortType.NameDescending => _customerDataList.OrderByDescending(data => data.Name).ToList(),
            SortType.GradeAscending => _customerDataList.OrderBy(data => data.Name).ToList(),
            SortType.GradeDescending => _customerDataList.OrderByDescending(data => data.Name).ToList(),
            SortType.None => _customerDataList,
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
        _customerDataList.Clear();
        _specialCustomerDataList.Clear();
        _gatecrasherCustomerDataList.Clear();
        _customerDataList.AddRange(Resources.LoadAll<CustomerData>("CustomerData"));

        for(int i = 0, cnt = _customerDataList.Count; i < cnt; i++)
        {
            CustomerData data = _customerDataList[i];
            _customerDataDic.Add(data.Id, data);

            if(data is SpecialCustomerData)
            {
                _specialCustomerDataList.Add((SpecialCustomerData)data);
            }

            else if(data is GatecrasherCustomerData)
            {
                _gatecrasherCustomerDataList.Add((GatecrasherCustomerData)data);
            }

            else
            {
                _normalCustomerDataList.Add(data);
            }
        }
    }

    private bool CheckAppearCustomer(CustomerData data)
    {
        if (!UserInfo.IsScoreValid(data.MinScore))
            return false;

        if (!string.IsNullOrEmpty(data.RequiredDish) && !UserInfo.IsGiveRecipe(data.RequiredDish))
            return false;

        if(!string.IsNullOrEmpty(data.RequiredItem) && !UserInfo.IsGiveGachaItem(data.RequiredItem))
            return false;

        return true;
    }
}
