using System.Collections.Generic;
using UnityEngine;

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

    private static CustomerData[] _customerDatas;
    private static Dictionary<string, CustomerData> _customerDataDic = new Dictionary<string, CustomerData>();


    public CustomerData GetCustomerData(string id)
    {
        if (!_customerDataDic.TryGetValue(id, out CustomerData data))
            throw new System.Exception("�ش� id���� �������� �ʽ��ϴ�.");

        return data;
    }


    public List<CustomerData> GetAppearCustomerList()
    {
        List<CustomerData> returnList = new List<CustomerData>();
        for(int i = 0, cnt = _customerDatas.Length; i < cnt; ++i)
        {
            if (_customerDatas[i].MinScore <= GameManager.Instance.Score)
                returnList.Add(_customerDatas[i]);
        }

        return returnList;
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

        _customerDatas = Resources.LoadAll<CustomerData>("CustomerData");
        for(int i = 0, cnt = _customerDatas.Length; i < cnt; i++)
        {
            _customerDataDic.Add(_customerDatas[i].Id, _customerDatas[i]);
        }
    }
}
