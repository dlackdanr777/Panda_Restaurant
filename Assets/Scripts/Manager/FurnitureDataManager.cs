using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class FurnitureDataManager : MonoBehaviour
{
    public static FurnitureDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("FurnitureDataManager");
                _instance = obj.AddComponent<FurnitureDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static FurnitureDataManager _instance;

    private static List<FurnitureData>[] _furnitureDataListType = new List<FurnitureData>[(int)FurnitureType.Length];
    private static Dictionary<string, FurnitureData> _furnitureDataDic = new Dictionary<string, FurnitureData>();

    private static Dictionary<string, Sprite> _spriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _thumbnailSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _leftChairSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _rightChairSpriteDic = new Dictionary<string, Sprite>();




    public FurnitureData GetFurnitureData(string id)
    {
        if (!_furnitureDataDic.TryGetValue(id, out FurnitureData data))
            throw new System.Exception($"해당 ID가 존재하지 않습니다: {id}");

        return data;
    }


    public List<FurnitureData> GetFurnitureDataList(FurnitureType type)
    {
        return _furnitureDataListType[(int)type];
    }

    public List<FurnitureData> GetSortFurnitureDataList(FurnitureType type)
    {
        return UserInfo.FurnitureSortType switch
        {
            ShopSortType.NameAscending => _furnitureDataListType[(int)type].OrderBy(data => data.Name).ToList(),
            ShopSortType.NameDescending => _furnitureDataListType[(int)type].OrderByDescending(data => data.Name).ToList(),
            ShopSortType.PriceAscending => ShopItemSort.SortByPrice(_furnitureDataListType[(int)type], true),
            ShopSortType.PriceDescending => ShopItemSort.SortByPrice(_furnitureDataListType[(int)type], false),
            ShopSortType.None => _furnitureDataListType[(int)type],
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
        _furnitureDataDic.Clear();
        _spriteDic.Clear();
        _thumbnailSpriteDic.Clear();
        _leftChairSpriteDic.Clear();
        _rightChairSpriteDic.Clear();   

        for (int i = 0; i < (int)FurnitureType.Length; i++)
        {
            _furnitureDataListType[i] = new List<FurnitureData>();
        }

        LoadFurnitureSprites("FurnitureData/Sprites");

        string basePath = "FurnitureData/CSVData/";
        TableDataParse(basePath + "TableData", EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "CounterData", FurnitureType.Counter, EquipEffectType.AddMaxTip);
        FurnitureDataParse(basePath + "RackData", FurnitureType.Rack, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FrameData", FurnitureType.Frame, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FlowerData", FurnitureType.Flower, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "AccData", FurnitureType.Acc, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "WallpaperData", FurnitureType.Wallpaper, EquipEffectType.AddTipPerMinute);
    }


    private static void LoadFurnitureSprites(string basePath)
    {
        string[] setFolders = { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15" };

        foreach (string setFolder in setFolders)
        {
            string fullPath = $"{basePath}/{setFolder}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(fullPath);

            foreach (var sprite in sprites)
            {
                string spriteName = sprite.name;
                string key = Utility.CutStringUpToChar(spriteName, '_'); // 기본 키 생성
                string afterUnderStr = Utility.GetStringAfterChar(spriteName, '_'); // 언더바 이후 문자열
                bool isThumbnail = spriteName.Contains("썸네일"); // 썸네일 여부 확인
                bool isChair = afterUnderStr.Contains("의자") || afterUnderStr.Contains("체어");
                DebugLog.Log(spriteName);
                if (isThumbnail)
                {
                    _thumbnailSpriteDic.Add(key, sprite);
                }

                else if(isChair)
                {
                    if (afterUnderStr.Contains("L") || spriteName.Contains("좌"))
                    {
                        _leftChairSpriteDic.Add(key, sprite);
                    }
                    else if (afterUnderStr.Contains("R") || spriteName.Contains("우"))
                    {
                        _rightChairSpriteDic.Add(key, sprite);
                    }
                    else
                    {
                        _leftChairSpriteDic.Add(key, sprite);
                    }

                }

                else
                {
                    _spriteDic.Add(key, sprite);
                }
            }
        }
    }

    private static void FurnitureDataParse(string loadPath, FurnitureType type, EquipEffectType effectType)
    {
        // 🔹 Resources 폴더에서 CSV 데이터 불러오기
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
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);

            string setId = (row[3].Trim());
            int addScore = int.Parse(row[4].Trim());

            int effectValue = int.Parse(row[5].Trim());
            int unlockScore = int.Parse(row[6].Trim());
            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int price = int.Parse(row[8].Trim());
            UnlockConditionType unlockType = row.Length < 10 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[9].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[10].Trim();
            if (unlockType == UnlockConditionType.None || !int.TryParse(row[11].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }

            // 🔹 스프라이트 가져오기 (딕셔너리에서 가져오므로 성능 향상)
            if (!_spriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            // 🔹 FurnitureData 생성
            FurnitureData furnitureData = new FurnitureData(
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
                effectType, // 엑셀에 해당 데이터가 없다면 기본값 사용
                effectValue,
                unlockType,
                unlockId,
                unlockCount
            );

            // 🔹 리스트 및 딕셔너리에 추가
            _furnitureDataDic.Add(id, furnitureData);
            _furnitureDataListType[(int)furnitureData.Type].Add(furnitureData);
        }
    }

    private static void TableDataParse(string loadPath, EquipEffectType effectType)
    {
        // 🔹 Resources 폴더에서 CSV 데이터 불러오기
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
            int table1EffectValue = int.Parse(row[5].Trim());

            MoneyType moneyType = row[6].Trim() == "게임 머니" || row[6].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int table1BuyScore = int.Parse(row[7].Trim());
            int table1BuyPrice = int.Parse(row[8].Trim());

            int table2BuyScore = int.Parse(row[9].Trim());
            int table2BuyPrice = int.Parse(row[10].Trim());
            int table2AddScore = int.Parse(row[11].Trim());
            int table2EffectValue = int.Parse(row[12].Trim());

            int table3BuyScore = int.Parse(row[13].Trim());
            int table3BuyPrice = int.Parse(row[14].Trim());
            int table3AddScore = int.Parse(row[15].Trim());
            int table3EffectValue = int.Parse(row[16].Trim());

            int table4BuyScore = int.Parse(row[17].Trim());
            int table4BuyPrice = int.Parse(row[18].Trim());
            int table4AddScore = int.Parse(row[19].Trim());
            int table4EffectValue = int.Parse(row[20].Trim());

            int table5BuyScore = int.Parse(row[21].Trim());
            int table5BuyPrice = int.Parse(row[22].Trim());
            int table5AddScore = int.Parse(row[23].Trim());
            int table5EffectValue = int.Parse(row[24].Trim());

            UnlockConditionType unlockType = Utility.GetUnlockConditionType(row[25].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[26].Trim();
            if (unlockType == UnlockConditionType.None || !int.TryParse(row[27].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }

            // 🔹 스프라이트 가져오기 (딕셔너리에서 가져오므로 성능 향상)
            if (!_spriteDic.TryGetValue(id, out Sprite sprite))
            {
                DebugLog.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            if (!_leftChairSpriteDic.TryGetValue(id, out Sprite leftChairSprite))
            {
                DebugLog.LogError($"좌측 의자 스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_rightChairSpriteDic.TryGetValue(id, out Sprite rightChairSprite))
            {
                DebugLog.LogError($"우측 의자 스프라이트가 없습니다: {id}");
                rightChairSprite = null;
            }


            FurnitureData table1Data = new TableFurnitureData(
                sprite,
                thumbnailSprite,
                id + "_01",
                setId,
                name,
                moneyType,
                table1BuyScore,
                table1BuyPrice,
                FurnitureType.Table1,
                foodType,
                table1AddScore,
                effectType,
                table1EffectValue,
                unlockType,
                unlockId,
                unlockCount,
                leftChairSprite,
                rightChairSprite
            );

            FurnitureData table2Data = new TableFurnitureData(
    sprite,
    thumbnailSprite,
    id + "_02",
    setId,
    name,
    moneyType,
    table2BuyScore,
    table2BuyPrice,
    FurnitureType.Table2,
    foodType,
    table2AddScore,
    effectType,
    table2EffectValue,
                    unlockType,
                unlockId,
                unlockCount,
    leftChairSprite,
    rightChairSprite
);

            FurnitureData table3Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id + "_03",
setId,
name,
moneyType,
table3BuyScore,
table3BuyPrice,
FurnitureType.Table3,
foodType,
table3AddScore,
effectType,
table3EffectValue,
                unlockType,
                unlockId,
                unlockCount,
leftChairSprite,
rightChairSprite
);

            FurnitureData table4Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id + "_04",
setId,
name,
moneyType,
table4BuyScore,
table4BuyPrice,
FurnitureType.Table4,
foodType,
table4AddScore,
effectType,
table4EffectValue,
                unlockType,
                unlockId,
                unlockCount,
leftChairSprite,
rightChairSprite
);

            FurnitureData table5Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id + "_05",
setId,
name,
moneyType,
table5BuyScore,
table5BuyPrice,
FurnitureType.Table5,
foodType,
table5AddScore,
effectType,
table5EffectValue,
                unlockType,
                unlockId,
                unlockCount,
leftChairSprite,
rightChairSprite
);

            _furnitureDataDic.Add(id + "_01", table1Data);
            _furnitureDataDic.Add(id + "_02", table2Data);
            _furnitureDataDic.Add(id + "_03", table3Data);
            _furnitureDataDic.Add(id + "_04", table4Data);
            _furnitureDataDic.Add(id + "_05", table5Data);
            _furnitureDataListType[(int)table1Data.Type].Add(table1Data);
            _furnitureDataListType[(int)table2Data.Type].Add(table2Data);
            _furnitureDataListType[(int)table3Data.Type].Add(table3Data);
            _furnitureDataListType[(int)table4Data.Type].Add(table4Data);
            _furnitureDataListType[(int)table5Data.Type].Add(table5Data);
        }
    }
}
