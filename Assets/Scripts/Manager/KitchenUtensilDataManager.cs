using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    private static List<KitchenUtensilData>[] _kitchenUtensilDataListType = new List<KitchenUtensilData>[(int)KitchenUtensilType.Length];
    private static List<KitchenUtensilData> _kitchenUtensilDataList = new List<KitchenUtensilData>();
    private static Dictionary<string, KitchenUtensilData> _kitchenUtensilDataDic = new Dictionary<string, KitchenUtensilData>();

    private static Dictionary<string, Sprite> _spriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _thumbnailSpriteDic = new Dictionary<string, Sprite>();



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

    public List<KitchenUtensilData> GetSortKitchenUtensilDataList(KitchenUtensilType type)
    {
        return UserInfo.KitchenUtensilSortType switch
        {
            ShopSortType.NameAscending => _kitchenUtensilDataListType[(int)type].OrderBy(data => data.Name).ToList(),
            ShopSortType.NameDescending => _kitchenUtensilDataListType[(int)type].OrderByDescending(data => data.Name).ToList(),
            ShopSortType.PriceAscending => ShopItemSort.SortByPrice(_kitchenUtensilDataListType[(int)type], true),
            ShopSortType.PriceDescending => ShopItemSort.SortByPrice(_kitchenUtensilDataListType[(int)type], false),
            ShopSortType.None => _kitchenUtensilDataListType[(int)type],
            _ => null
        };
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitData();
    }


    private static void InitData()
    {

        _kitchenUtensilDataDic.Clear();
        _kitchenUtensilDataList.Clear();
        _spriteDic.Clear();
        _thumbnailSpriteDic.Clear();

        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            _kitchenUtensilDataListType[i] = new List<KitchenUtensilData>();
        }

        LoadFurnitureSprites("KitchenUtensilData/Sprites");

        string basePath = "KitchenUtensilData/CSVData/";
        CookerDataParse(basePath + "CookerData", EquipEffectType.AddCookSpeed);
        KitchenDataParse(basePath + "FridgeData", KitchenUtensilType.Fridge, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "CabinetData", KitchenUtensilType.Cabinet, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "WindowData", KitchenUtensilType.Window, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "CooktoolData", KitchenUtensilType.CookingTools, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "KitchenrackData", KitchenUtensilType.Kitchenrack, EquipEffectType.AddTipPerMinute);
        SinkDataParse(basePath + "SinkData", EquipEffectType.AddTipPerMinute);
    }


    private static void LoadFurnitureSprites(string basePath)
    {
        string[] setFolders = { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"};

        foreach (string setFolder in setFolders)
        {
            string fullPath = $"{basePath}/{setFolder}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(fullPath);

            foreach (var sprite in sprites)
            {
                string spriteName = sprite.name;
                string afterUnderStr = Utility.GetStringAfterChar(spriteName, '_'); // 언더바 이후 문자열
                bool isThumbnail = afterUnderStr.Contains("썸네일"); // 썸네일 여부 확인
                string key = CutStringUpToChar(spriteName, '_'); // 기본 키 생성

                DebugLog.Log(spriteName + "is thumbnail: " + isThumbnail);
                if (isThumbnail)
                {
                    _thumbnailSpriteDic.Add(key, sprite);
                }

                else
                {
                    _spriteDic.Add(key, sprite);
                }

            }
        }
    }

    private static void KitchenDataParse(string loadPath, KitchenUtensilType type, EquipEffectType effectType)
    {
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++)
        {
            string[] row = Utility.SplitCsvLine(data[i]);
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                DebugLog.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);
            string setId = row[3].Trim();
            int addScore = int.Parse(row[4].Trim());
            int effectValue = int.Parse(row[5].Trim());
            int unlockScore = int.Parse(row[6].Trim());

            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int price = int.Parse(row[8].Trim());

            UnlockConditionType unlockType = row.Length < 10 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[9].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[10].Trim();
            if(unlockType == UnlockConditionType.None || !int.TryParse(row[11].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }


            if (!_spriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            KitchenUtensilData kitchenData = new KitchenUtensilData(
                sprite,
                thumbnailSprite,
                id,
                setId,
                name,
                moneyType,
                unlockScore,
                price,
                type,
                foodType,
                addScore,
                effectType,
                effectValue,
                unlockType,
                unlockId,
                unlockCount
            );


            _kitchenUtensilDataDic.Add(id, kitchenData);
            _kitchenUtensilDataList.Add(kitchenData);
            _kitchenUtensilDataListType[(int)kitchenData.Type].Add(kitchenData);
        }
    }


    private static void SinkDataParse(string loadPath, EquipEffectType effectType)
    {
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        KitchenUtensilType type = KitchenUtensilType.Sink;
        for (int i = 1; i < data.Length; i++)
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);
            string setId = row[3].Trim();
            int addScore = int.Parse(row[4].Trim());
            int effectValue = int.Parse(row[5].Trim());
            int unlockScore = int.Parse(row[6].Trim());

            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int price = int.Parse(row[8].Trim());
            int maxSinkBowlCount = int.Parse(row[9].Trim());

            UnlockConditionType unlockType = row.Length < 11 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[10].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[11].Trim();
            if (unlockType == UnlockConditionType.None || !int.TryParse(row[12].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }


            if (!_spriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            KitchenUtensilSinkData kitchenData = new KitchenUtensilSinkData(
                sprite,
                thumbnailSprite,
                id,
                setId,
                name,
                moneyType,
                unlockScore,
                price,
                type,
                foodType,
                addScore,
                effectType,
                effectValue,
                unlockType,
                unlockId,
                unlockCount,
                maxSinkBowlCount
            );


            _kitchenUtensilDataDic.Add(id, kitchenData);
            _kitchenUtensilDataList.Add(kitchenData);
            _kitchenUtensilDataListType[(int)kitchenData.Type].Add(kitchenData);
        }
    }

    private static void CookerDataParse(string loadPath, EquipEffectType effectType)
    {
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++)
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);
            string setId = (row[3].Trim());
            int table1AddScore = int.Parse(row[4].Trim());
            int effectValue = int.Parse(row[5].Trim());
            int table1BuyScore = int.Parse(row[6].Trim());
            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int table1BuyPrice = int.Parse(row[8].Trim());

            // UnlockConditionType unlockType = row.Length < 10 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[9].Trim());
            // string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[10].Trim();
            // if (unlockType == UnlockConditionType.None || !int.TryParse(row[11].Trim(), out int unlockCount))
            // {
            //     unlockCount = 0;
            // }
            KitchenUtensilType type = GetKitchenUtensilTypeFromId(id);
            if (!_spriteDic.TryGetValue(CutStringUpToChar(id, '_'), out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            KitchenUtensilData cookerData = new KitchenUtensilData(
                sprite,
                thumbnailSprite,
                id,
                setId,
                name,
                moneyType,
                table1BuyScore,
                table1BuyPrice,
                type,
                foodType,
                table1AddScore,
                effectType,
                effectValue,
                UnlockConditionType.None,
                string.Empty,
                0
            );

            _kitchenUtensilDataDic.Add(id, cookerData);
            _kitchenUtensilDataList.Add(cookerData);
            _kitchenUtensilDataListType[(int)type].Add(cookerData);
        }
    }

    private static KitchenUtensilType GetKitchenUtensilTypeFromId(string id)
{
    try
    {
        // 언더바 다음 문자열 추출
        string afterUnderscore = Utility.GetStringAfterChar(id, '_');
        
        // 숫자 부분만 추출 (예: "01썸네일" -> "01")
        string numberPart = "";
        for (int i = 0; i < afterUnderscore.Length; i++)
        {
            if (char.IsDigit(afterUnderscore[i]))
            {
                numberPart += afterUnderscore[i];
            }
            else
            {
                break; // 숫자가 아닌 문자를 만나면 중단
            }
        }
        
        if (int.TryParse(numberPart, out int burnerNumber))
        {
            // 01 -> 0번째 (Burner1), 02 -> 1번째 (Burner2), 03 -> 2번째 (Burner3)
            int typeIndex = burnerNumber - 1;
            
            // Burner1(0), Burner2(1), Burner3(2) 범위 확인
            if (typeIndex >= 0 && typeIndex <= 4)
            {
                return (KitchenUtensilType)typeIndex; // Burner1, Burner2, Burner3
            }
        }
        
        DebugLog.LogError($"id에서 버너 타입을 파싱할 수 없습니다: {id}, 기본값 Burner1 사용");
        return KitchenUtensilType.Burner1; // 기본값
    }
    catch (System.Exception e)
    {
        DebugLog.LogError($"id 파싱 중 오류 발생: {id}, 오류: {e.Message}");
        return KitchenUtensilType.Burner1; // 기본값
    }
}


    private static string CutStringUpToChar(string str, char delimiter)
    {
        str = str.ToUpper();
        int index = str.IndexOf(delimiter);

        if (index >= 0)
            return str.Substring(0, index);
        else
            return str;
    }
}
