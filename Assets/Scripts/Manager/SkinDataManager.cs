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
    private static Dictionary<string, Sprite> _customerThumbnailSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite[]> _customerIdleSpriteDic = new Dictionary<string, Sprite[]>();
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
        LoadStaffSkinThumbnails();
        LoadStaffSkinIdleSprites();
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
        Sprite[] sprites = Resources.LoadAll<Sprite>("StaffData/Skin/Sprites/Sprite");

        foreach (var sprite in sprites)
        {
            string key = sprite.name;
            key = key.Trim();
            // 중복 체크
            if (_staffSpriteDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }

            _staffSpriteDic.Add(key, sprite);
            DebugLog.Log(key);
        }
    }

    private static void LoadStaffSkinThumbnails()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("StaffData/Skin/Sprites/Thumbnail");

        foreach (var sprite in sprites)
        {
            string key = sprite.name;
            key = key.Trim();
            // 중복 체크
            if (_customerThumbnailSpriteDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }

            _customerThumbnailSpriteDic.Add(key, sprite);
            DebugLog.Log(key);
        }
    }

    private static void LoadStaffSkinIdleSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("StaffData/Skin/Sprites/IdleSprites");

        // ID별로 스프라이트들을 그룹화하기 위한 임시 딕셔너리
        Dictionary<string, List<(Sprite sprite, int index)>> tempSpriteDic = new Dictionary<string, List<(Sprite, int)>>();

        foreach (var sprite in sprites)
        {
            string spriteName = sprite.name.Trim();
            
            // SKIN_STAFF01-01 형태에서 ID와 인덱스 분리
            string[] parts = spriteName.Split('-');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"스프라이트 이름 형식이 올바르지 않습니다: {spriteName}");
                continue;
            }

            string id = parts[0].Trim(); // SKIN_STAFF01
            if (!int.TryParse(parts[1].Trim(), out int index))
            {
                Debug.LogWarning($"스프라이트 인덱스를 파싱할 수 없습니다: {spriteName}");
                continue;
            }

            // 임시 딕셔너리에 추가
            if (!tempSpriteDic.ContainsKey(id))
            {
                tempSpriteDic[id] = new List<(Sprite, int)>();
            }
            tempSpriteDic[id].Add((sprite, index));
        }

        // 각 ID별로 인덱스 순서대로 정렬하여 최종 딕셔너리에 저장
        foreach (var kvp in tempSpriteDic)
        {
            string id = kvp.Key;
            var spriteList = kvp.Value;
            
            // 인덱스 기준으로 오름차순 정렬
            spriteList.Sort((a, b) => a.index.CompareTo(b.index));
            
            // Sprite 배열로 변환
            Sprite[] sortedSprites = new Sprite[spriteList.Count];
            for (int i = 0; i < spriteList.Count; i++)
            {
                sortedSprites[i] = spriteList[i].sprite;
            }
            
            // 최종 딕셔너리에 저장
            if (_customerIdleSpriteDic.ContainsKey(id))
            {
                Debug.LogWarning($"중복된 스프라이트 ID가 있습니다: {id}");
                continue;
            }
            
            _customerIdleSpriteDic.Add(id, sortedSprites);
            DebugLog.Log($"Idle 스프라이트 로드 완료: {id} ({sortedSprites.Length}개)");
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

            if (!_customerThumbnailSpriteDic.TryGetValue(id, out Sprite thumbnail))
            {
                Debug.LogError($"썸네일 스프라이트를 찾을 수 없습니다: {id}");
                thumbnail = sprite;
            }

            // IdleSprites 배열 가져오기
            Sprite[] idleSprites = null;
            if (_customerIdleSpriteDic.TryGetValue(id, out Sprite[] originalSprites))
            {
                // 애니메이션 패턴 생성 (예: 길이 3일 경우 1 2 3 3 3 2 1)
                idleSprites = CreateIdleAnimationPattern(originalSprites);
            }
            else
            {
                Debug.LogWarning($"Idle 스프라이트를 찾을 수 없습니다: {id}");
                idleSprites = null; // null로 설정
            }

            StaffSkinData skinData = new StaffSkinData(sprite, thumbnail, idleSprites, id, name, description, addScore, addTipPerMinute, (Rank)Mathf.Clamp(rank - 1, 0, rank), salesLocationType, money, upgradeType, upgradeValue, equipTargetId);

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

    #region Animation Pattern Creation
    private static Sprite[] CreateIdleAnimationPattern(Sprite[] originalSprites)
    {
        if (originalSprites == null || originalSprites.Length == 0)
            return null;

        if (originalSprites.Length == 1)
        {
            // 스프라이트가 1개일 경우: 그대로 반환
            return originalSprites;
        }
        else if (originalSprites.Length == 2)
        {
            // 스프라이트가 2개일 경우: 1 2 2 1 패턴
            return new Sprite[] 
            { 
                originalSprites[0], 
                originalSprites[1], 
                originalSprites[1], 
                originalSprites[0] 
            };
        }
        else if (originalSprites.Length >= 3)
        {
            // 스프라이트가 3개 이상일 경우: 1 2 3 3 3 2 1 패턴
            List<Sprite> pattern = new List<Sprite>();
            
            // 앞으로 진행 (1 2 3)
            for (int i = 0; i < originalSprites.Length; i++)
            {
                pattern.Add(originalSprites[i]);
            }
            
            // 마지막 스프라이트를 3번 추가 (3 3 3)
            for (int i = 0; i < 3; i++)
            {
                pattern.Add(originalSprites[originalSprites.Length - 1]);
            }
            
            // 뒤로 진행 (2 1) - 마지막과 첫번째 제외
            for (int i = originalSprites.Length - 2; i >= 0; i--)
            {
                pattern.Add(originalSprites[i]);
            }
            
            return pattern.ToArray();
        }

        return originalSprites;
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
