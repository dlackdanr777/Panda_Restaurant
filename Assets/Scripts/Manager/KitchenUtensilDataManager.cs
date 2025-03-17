using System.Collections.Generic;
using UnityEngine;

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
    private static Dictionary<string, KitchenUtensilData> _kitchenUtensilDataDic = new Dictionary<string, KitchenUtensilData>();

    private static Dictionary<string, Sprite> _spriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _thumbnailSpriteDic = new Dictionary<string, Sprite>();



    public KitchenUtensilData GetKitchenUtensilData(string id)
    {
        if (!_kitchenUtensilDataDic.TryGetValue(id, out KitchenUtensilData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }


    public List<KitchenUtensilData> GetKitchenUtensilDataList(KitchenUtensilType type)
    {
        return _kitchenUtensilDataListType[(int)type];
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
        KitchenDataParse(basePath + "SinkData", KitchenUtensilType.Sink, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "CooktoolData", KitchenUtensilType.CookingTools, EquipEffectType.AddTipPerMinute);
        KitchenDataParse(basePath + "KitchenrackData", KitchenUtensilType.Kitchenrack, EquipEffectType.AddTipPerMinute);
    }


    private static void LoadFurnitureSprites(string basePath)
    {
        string[] setFolders = { "01", "02", "03", "04", "05" };

        foreach (string setFolder in setFolders)
        {
            string fullPath = $"{basePath}/{setFolder}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(fullPath);

            foreach (var sprite in sprites)
            {
                string spriteName = sprite.name;
                bool isThumbnail = spriteName.Contains("썸네일"); // 썸네일 여부 확인
                string key = CutStringUpToChar(spriteName, '_'); // 기본 키 생성
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
            float size = float.Parse(row[9].Trim());

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
                size
            );


            _kitchenUtensilDataDic.Add(id, kitchenData);
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
            MoneyType moneyType = row[6].Trim() == "게임 머니" || row[6].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            DebugLog.Log(row[6] + ", " + moneyType);
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

            if (!_spriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            KitchenUtensilData cooker01Data = new KitchenUtensilData(
                sprite,
                thumbnailSprite,
                id + "_01",
                setId,
                name,
                moneyType,
                table1BuyScore,
                table1BuyPrice,
                KitchenUtensilType.Burner1,
                foodType,
                table1AddScore,
                effectType,
                effectValue,
                size
            );

            KitchenUtensilData cooker02Data = new KitchenUtensilData(
    sprite,
    thumbnailSprite,
    id + "_02",
    setId,
    name,
    moneyType,
    table2BuyScore,
    table2BuyPrice,
    KitchenUtensilType.Burner2,
    foodType,
    table2AddScore,
    effectType,
    effectValue,
    size
);

            KitchenUtensilData cooker03Data = new KitchenUtensilData(
    sprite,
    thumbnailSprite,
    id + "_03",
    setId,
    name,
    moneyType,
    table3BuyScore,
    table3BuyPrice,
    KitchenUtensilType.Burner3,
    foodType,
    table3AddScore,
    effectType,
    effectValue,
    size
);

            KitchenUtensilData cooker04Data = new KitchenUtensilData(
    sprite,
    thumbnailSprite,
    id + "_04",
    setId,
    name,
    moneyType,
    table4BuyScore,
    table4BuyPrice,
    KitchenUtensilType.Burner4,
    foodType,
    table4AddScore,
    effectType,
    effectValue,
    size
);

            KitchenUtensilData cooker05Data = new KitchenUtensilData(
    sprite,
    thumbnailSprite,
    id + "_05",
    setId,
    name,
    moneyType,
    table5BuyScore,
    table5BuyPrice,
    KitchenUtensilType.Burner5,
    foodType,
    table5AddScore,
    effectType,
    effectValue,
    size
);


            _kitchenUtensilDataDic.Add(id + "_01", cooker01Data);
            _kitchenUtensilDataDic.Add(id + "_02", cooker02Data);
            _kitchenUtensilDataDic.Add(id + "_03", cooker03Data);
            _kitchenUtensilDataDic.Add(id + "_04", cooker04Data);
            _kitchenUtensilDataDic.Add(id + "_05", cooker05Data);
            _kitchenUtensilDataListType[(int)cooker01Data.Type].Add(cooker01Data);
            _kitchenUtensilDataListType[(int)cooker02Data.Type].Add(cooker02Data);
            _kitchenUtensilDataListType[(int)cooker03Data.Type].Add(cooker03Data);
            _kitchenUtensilDataListType[(int)cooker04Data.Type].Add(cooker04Data);
            _kitchenUtensilDataListType[(int)cooker05Data.Type].Add(cooker05Data);
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
