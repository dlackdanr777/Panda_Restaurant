using System.Collections.Generic;
using UnityEngine;


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

    private static List<FurnitureData> _furnitureDataList = new List<FurnitureData>();
    private static Dictionary<string, FurnitureData> _furnitureDataDic = new Dictionary<string, FurnitureData>();
    private static List<FurnitureData>[] _furnitureDataListType = new List<FurnitureData>[(int)FurnitureType.Length];

    private static Dictionary<string, Sprite> _spriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _thumbnailSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _leftChairSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _rightChairSpriteDic = new Dictionary<string, Sprite>();

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
        if (_furnitureDataList.Count > 0)
            return;

        _furnitureDataList.Clear();
        _furnitureDataDic.Clear();
        _spriteDic.Clear();
        _thumbnailSpriteDic.Clear();
        _leftChairSpriteDic.Clear();
        _rightChairSpriteDic.Clear();   

        for (int i = 0; i < (int)FurnitureType.Length; i++)
        {
            _furnitureDataListType[i] = new List<FurnitureData>();
        }

        // 🔹 가구 세트별 폴더에서 스프라이트 로드
        LoadFurnitureSprites("FurnitureData/Sprites");
        // 🔹 가구 데이터 CSV 파싱

        string basePath = "FurnitureData/CSVData/";
        TableDataParse(basePath + "TableData", EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "CounterData", FurnitureType.Counter, EquipEffectType.AddMaxTip);
        FurnitureDataParse(basePath + "RackData", FurnitureType.Rack, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FrameData", FurnitureType.Frame, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FlowerData", FurnitureType.Flower, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "AccData", FurnitureType.Acc, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "WallpaperData", FurnitureType.Wallpaper, EquipEffectType.AddTipPerMinute);
    }

    /// <summary>
    /// 가구 세트별 폴더에서 모든 스프라이트 로드
    /// </summary>
    /// 
    private static void LoadFurnitureSprites(string basePath)
    {
        string[] setFolders = { "01", "02", "03", "04", "05", "06", "07", "08" };

        foreach (string setFolder in setFolders)
        {
            string fullPath = $"{basePath}/{setFolder}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(fullPath);

            foreach (var sprite in sprites)
            {
                string spriteName = sprite.name;
                bool isThumbnail = spriteName.Contains("썸네일"); // 썸네일 여부 확인
                bool isChair = spriteName.Contains("의자");

                string key = CutStringUpToChar(spriteName, '_'); // 기본 키 생성
                DebugLog.Log(spriteName);
                if (isThumbnail)
                    _thumbnailSpriteDic.Add(key, sprite);
                else if(isChair)
                {
                    if (spriteName.Contains("좌"))
                    {
                        _leftChairSpriteDic.Add(key, sprite);
                    }
                    else if (spriteName.Contains("우"))
                    {
                        _rightChairSpriteDic.Add(key, sprite);
                    }
                    else
                    {
                        _leftChairSpriteDic.Add(key, sprite);
                        _rightChairSpriteDic.Add(key, sprite);
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
            DebugLog.Log(id);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);

            int addScore = int.Parse(row[4].Trim());

            int effectValue = int.Parse(row[5].Trim());
            int unlockScore = int.Parse(row[6].Trim());
            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int price = int.Parse(row[8].Trim());

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
                name,
                moneyType,
                unlockScore,
                price,
                type,
                foodType,
                addScore,
                effectType, // 엑셀에 해당 데이터가 없다면 기본값 사용
                effectValue
            );

            // 🔹 리스트 및 딕셔너리에 추가
            _furnitureDataList.Add(furnitureData);
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
            DebugLog.Log(id);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);

            int table1AddScore = int.Parse(row[4].Trim());

            int effectValue = int.Parse(row[5].Trim());
            MoneyType moneyType = row[6].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int table1BuyScore = int.Parse(row[7].Trim());
            int table1BuyPrice = int.Parse(row[8].Trim());

            int table2BuyScore = int.Parse(row[9].Trim());
            int table2BuyPrice = int.Parse(row[10].Trim());
            int table2AddScore = int.Parse(row[11].Trim());

            int table3BuyScore = int.Parse(row[12].Trim());
            int table3BuyPrice = int.Parse(row[13].Trim());
            int table3AddScore = int.Parse(row[14].Trim());

            int table4BuyScore = int.Parse(row[15].Trim());
            int table4BuyPrice = int.Parse(row[16].Trim());
            int table4AddScore = int.Parse(row[17].Trim());

            int table5BuyScore = int.Parse(row[18].Trim());
            int table5BuyPrice = int.Parse(row[19].Trim());
            int table5AddScore = int.Parse(row[20].Trim());

            float size = float.Parse(row[21].Trim());

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
                continue;
            }


            FurnitureData table1Data = new TableFurnitureData(
                sprite,
                thumbnailSprite,
                id,
                name,
                moneyType,
                table1BuyScore,
                table1BuyPrice,
                FurnitureType.Table1,
                foodType,
                table1AddScore,
                effectType,
                effectValue,
                size,
                leftChairSprite,
                rightChairSprite
            );

            FurnitureData table2Data = new TableFurnitureData(
    sprite,
    thumbnailSprite,
    id,
    name,
    moneyType,
    table2BuyScore,
    table2BuyPrice,
    FurnitureType.Table2,
    foodType,
    table2AddScore,
    effectType,
    effectValue,
    size,
    leftChairSprite,
    rightChairSprite
);

            FurnitureData table3Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id,
name,
moneyType,
table3BuyScore,
table3BuyPrice,
FurnitureType.Table3,
foodType,
table3AddScore,
effectType,
effectValue,
size,
leftChairSprite,
rightChairSprite
);

            FurnitureData table4Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id,
name,
moneyType,
table4BuyScore,
table4BuyPrice,
FurnitureType.Table4,
foodType,
table4AddScore,
effectType,
effectValue,
size,
leftChairSprite,
rightChairSprite
);

            FurnitureData table5Data = new TableFurnitureData(
sprite,
thumbnailSprite,
id,
name,
moneyType,
table5BuyScore,
table5BuyPrice,
FurnitureType.Table5,
foodType,
table5AddScore,
effectType,
effectValue,
size,
leftChairSprite,
rightChairSprite
);

            // 🔹 리스트 및 딕셔너리에 추가
            _furnitureDataList.Add(table1Data);
            _furnitureDataList.Add(table2Data);
            _furnitureDataList.Add(table3Data);
            _furnitureDataList.Add(table4Data);
            _furnitureDataList.Add(table5Data);
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


    public FurnitureData GetFurnitureData(string id)
    {
        if (!_furnitureDataDic.TryGetValue(id, out FurnitureData data))
            throw new System.Exception($"해당 ID가 존재하지 않습니다: {id}");

        return data;
    }

    public List<FurnitureData> GetFurnitureDataList()
    {
        return _furnitureDataList;
    }

    public List<FurnitureData> GetFurnitureDataList(FurnitureType type)
    {
        return _furnitureDataListType[(int)type];
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
