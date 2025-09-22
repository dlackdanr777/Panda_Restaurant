using System;
using System.Collections.Generic;
using UnityEngine;

public class SkinDataManager : MonoBehaviour
{
    public static SkinDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SkinDataManager");
                _instance = obj.AddComponent<SkinDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static SkinDataManager _instance;


    private static List<CustomerSkinData> _customerSkinDataList = new List<CustomerSkinData>();
    private static Dictionary<string, CustomerSkinData> _customerSkinDataDic = new Dictionary<string, CustomerSkinData>();
    private static Dictionary<string, List<CustomerSkinData>> _customerSkinDataByLocationDic = new Dictionary<string, List<CustomerSkinData>>();
    private static Dictionary<string, Sprite> _customerSpriteDic = new Dictionary<string, Sprite>();
    private Dictionary<string, SkinCustomerUpgradeType> _customerSkinUpgradeTypeDic = new Dictionary<string, SkinCustomerUpgradeType>
    {
        { "SKIN_CUSTOMER_UPGRADE01", SkinCustomerUpgradeType.Type1 },
        { "SKIN_CUSTOMER_UPGRADE02", SkinCustomerUpgradeType.Type2 },
        { "SKIN_CUSTOMER_UPGRADE03", SkinCustomerUpgradeType.Type3 },
        { "SKIN_CUSTOMER_UPGRADE04", SkinCustomerUpgradeType.Type4 },
        { "SKIN_CUSTOMER_UPGRADE05", SkinCustomerUpgradeType.Type5 }
    };



    private static List<StaffSkinData> _staffSkinDataList = new List<StaffSkinData>();
    private static Dictionary<string, StaffSkinData> _staffSkinDataDic = new Dictionary<string, StaffSkinData>();
    private static Dictionary<string, List<StaffSkinData>> _staffSkinDataByLocationDic = new Dictionary<string, List<StaffSkinData>>();
    private static Dictionary<string, Sprite> _staffSpriteDic = new Dictionary<string, Sprite>();
    private Dictionary<string, StaffSkinUpgradeType> _staffSkinUpgradeTypeDic = new Dictionary<string, StaffSkinUpgradeType>
    {
        { "SKIN_STAFF_UPGRADE01", StaffSkinUpgradeType.Type1 },
        { "SKIN_STAFF_UPGRADE02", StaffSkinUpgradeType.Type2 },
        { "SKIN_STAFF_UPGRADE03", StaffSkinUpgradeType.Type3 },
        { "SKIN_STAFF_UPGRADE04", StaffSkinUpgradeType.Type4 },
    };




    public List<CustomerSkinData> GetCustomerSkinDataList(string id)
    {
        if (!_customerSkinDataByLocationDic.TryGetValue(id, out List<CustomerSkinData> skinDataList))
        {
            throw new Exception($"고객 스킨 데이터를 찾을 수 없습니다: {id}");
        }

        return skinDataList;
    }

    public CustomerSkinData GetCustomerSkinData(string id)
    {
        if (!_customerSkinDataDic.TryGetValue(id, out CustomerSkinData skinData))
        {
            throw new Exception($"고객 스킨 데이터를 찾을 수 없습니다: {id}");
        }

        return skinData;
    }

    public List<StaffSkinData> GetStaffSkinDataList(string id)
    {
        if (!_staffSkinDataByLocationDic.TryGetValue(id, out List<StaffSkinData> skinDataList))
        {
            throw new Exception($"직원 스킨 데이터를 찾을 수 없습니다: {id}");
        }

        return skinDataList;
    }

    public StaffSkinData GetStaffSkinData(string id)
    {
        if (!_staffSkinDataDic.TryGetValue(id, out StaffSkinData skinData))
        {
            throw new Exception($"직원 스킨 데이터를 찾을 수 없습니다: {id}");
        }

        return skinData;
    }

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCustomerSkinSprites();
        SkinCustomerDataParse("CustomerData/CSVData/Skin/CustomerSkinDataList");

        LoadStaffSkinSprites();
        SkinStaffDataParse("StaffData/Skin/CSVData/StaffSkinDataList");
    }


    #region Customer Skin Data
    private static void LoadCustomerSkinSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("CustomerData/Sprites/NormalCustomer/Skin");

        foreach (var sprite in sprites)
        {
            string key = sprite.name;
            // 중복 체크
            if (_customerSpriteDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }
            _customerSpriteDic.Add(key, sprite);
            DebugLog.Log(key);
        }
    }

    private void SkinCustomerDataParse(string loadPath)
    {

        _customerSkinDataList.Clear();
        _customerSkinDataDic.Clear();
        _customerSkinDataByLocationDic.Clear();

        // ? Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');


        for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                break;
            }
            string name = row[1].Trim();
            string description = row[2].Trim();

            int addScore = Utility.StrToInt(row[4].Trim());
            int addTipPerMinute = Utility.StrToInt(row[5].Trim());
            int rank = Utility.StrToInt(row[6].Trim());
            SalesLocationType salesLocationType = GetSalesLocationType(row[8].Trim());
            int money = Utility.StrToInt(row[9].Trim());
            SkinCustomerUpgradeType upgradeType = GetSkinCustomerUpgradeType(row[10].Trim());
            float upgradeValue = Utility.StrToFloat(row[11].Trim());
            string equipTargetId = row[12].Trim();

            if (!_customerSpriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트를 찾을 수 없습니다: {id}");
                continue;
            }

            CustomerSkinData skinData = new CustomerSkinData(sprite, sprite, id, name, description, addScore, addTipPerMinute, (Rank)Mathf.Clamp(rank - 1, 0, rank), salesLocationType, money, upgradeType, upgradeValue, equipTargetId);

            _customerSkinDataList.Add(skinData);
            _customerSkinDataDic.Add(id, skinData);

            if (_customerSkinDataByLocationDic.TryGetValue(equipTargetId, out List<CustomerSkinData> skinList))
            {
                skinList.Add(skinData);
            }
            else
            {
                _customerSkinDataByLocationDic[equipTargetId] = new List<CustomerSkinData> { skinData };
            }
        }
    }



    private SkinCustomerUpgradeType GetSkinCustomerUpgradeType(string typeStr)
    {
        if (_customerSkinUpgradeTypeDic.TryGetValue(typeStr, out SkinCustomerUpgradeType upgradeType))
        {
            return upgradeType;
        }

        return SkinCustomerUpgradeType.None;
    }

    #endregion


    #region Staff Skin Data

    private static void LoadStaffSkinSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("StaffData/Skin/Sprites");

        foreach (var sprite in sprites)
        {
            string key = sprite.name;
            key = key.Trim();
            // 중복 체크
            if (_customerSpriteDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }

            _staffSpriteDic.Add(key, sprite);
            DebugLog.Log(key);
        }
    }

    private void SkinStaffDataParse(string loadPath)
    {

        _staffSkinDataList.Clear();
        _staffSkinDataDic.Clear();
        _staffSkinDataByLocationDic.Clear();

        // ? Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');


        for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                break;
            }
            string name = row[1].Trim();
            string description = row[2].Trim();

            int addScore = Utility.StrToInt(row[4].Trim());
            int addTipPerMinute = Utility.StrToInt(row[5].Trim());
            int rank = Utility.StrToInt(row[6].Trim());
            SalesLocationType salesLocationType = GetSalesLocationType(row[8].Trim());
            int money = Utility.StrToInt(row[9].Trim());
            StaffSkinUpgradeType upgradeType = GetSkinStaffUpgradeType(row[10].Trim());
            float upgradeValue = Utility.StrToFloat(row[11].Trim());
            string equipTargetId = row[12].Trim();

            if (!_staffSpriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트를 찾을 수 없습니다: {id}");
                continue;
            }

            StaffSkinData skinData = new StaffSkinData(sprite, sprite, id, name, description, addScore, addTipPerMinute, (Rank)Mathf.Clamp(rank - 1, 0, rank), salesLocationType, money, upgradeType, upgradeValue, equipTargetId);

            _staffSkinDataList.Add(skinData);
            _staffSkinDataDic.Add(id, skinData);

            if (_staffSkinDataByLocationDic.TryGetValue(equipTargetId, out List<StaffSkinData> skinList))
            {
                skinList.Add(skinData);
            }
            else
            {
                _staffSkinDataByLocationDic[equipTargetId] = new List<StaffSkinData> { skinData };
            }
        }
    }



    private StaffSkinUpgradeType GetSkinStaffUpgradeType(string typeStr)
    {
        if (_staffSkinUpgradeTypeDic.TryGetValue(typeStr, out StaffSkinUpgradeType upgradeType))
        {
            return upgradeType;
        }

        return StaffSkinUpgradeType.None;
    }



    #endregion


    #region  Utility
    
        private SalesLocationType GetSalesLocationType(string typeStr)
    {
        typeStr = typeStr.Trim();

        if (string.IsNullOrEmpty(typeStr))
            return SalesLocationType.None;

        string[] types = typeStr.Split('.');
        SalesLocationType result = SalesLocationType.None;

        foreach (string type in types)
        {
            string trimmedType = type.Trim();

            if (trimmedType.Contains("가챠"))
            {
                result |= SalesLocationType.Gacha;
            }
            else if (trimmedType.Contains("상점"))
            {
                result |= SalesLocationType.Shop;
            }
        }

        return result;
    }

    #endregion
}
