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
            }

            return _instance;
        }
    }
    private static FoodDataManager _instance;


    public static int Count => _foodDataList.Count;
    private static List<FoodData> _foodDataList = new List<FoodData>();
    private static List<FoodMiniGameData> _foodMiniGameDataList = new List<FoodMiniGameData>();
    private static Dictionary<string, FoodData> _foodDataDic = new Dictionary<string, FoodData>();
    private static Dictionary<string, FoodMiniGameData> _foodMiniGameDataDic = new Dictionary<string, FoodMiniGameData>();


    public FoodData GetFoodData(string id)
    {
        if (!_foodDataDic.TryGetValue(id, out FoodData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다: " + id);

        return data;
    }

    public List<FoodData> GetFoodDataList()
    {
        return _foodDataList;
    }


    public bool IsNeedMiniGame(FoodData data)
    {
        if (_foodMiniGameDataDic.ContainsKey(data.Id))
            return true;

        return false;
    }


    public bool IsNeedMiniGame(string id)
    {
        if (_foodMiniGameDataDic.ContainsKey(id))
            return true;

        return false;
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(_instance);
        Init();
    }


    private void Init()
    {
        Dictionary<string, Sprite> spriteDic = new Dictionary<string, Sprite>();
        Dictionary<string, Sprite> thumbnailSpriteDic = new Dictionary<string, Sprite>();
        Sprite[] sprites = Resources.LoadAll<Sprite>("FoodData/Sprites");
        Sprite[] thumbnailSprites = Resources.LoadAll<Sprite>("FoodData/ThumbnailSprites");

        for (int i = 0, cnt = thumbnailSprites.Length; i < cnt; ++i)
            thumbnailSpriteDic.Add(CutStringUpToChar(thumbnailSprites[i].name, '_'), thumbnailSprites[i]);

        for (int i = 0, cnt = sprites.Length; i < cnt; ++i)
            spriteDic.Add(CutStringUpToChar(sprites[i].name, '_'), sprites[i]);


        TextAsset csvData = Resources.Load<TextAsset>("FoodData/FoodDataList");
        TextAsset miniGameData = Resources.Load<TextAsset>("FoodData/FoodMiniGameDataList");

        string[] data = miniGameData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = row[0].Replace(" ", "");

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!int.TryParse(row[2].Replace(" ", ""), out int successCount))
                successCount = 0;

            if (!int.TryParse(row[3].Replace(" ", ""), out int maxHealth))
                maxHealth = 0;

            if (!int.TryParse(row[4].Replace(" ", ""), out int firstHealth))
                firstHealth = 0;

            if (!int.TryParse(row[5].Replace(" ", ""), out int addHealth))
                addHealth = 0;

            if (!int.TryParse(row[6].Replace(" ", ""), out int clearAddTime))
                clearAddTime = 0;

            FoodMiniGameData foodMiniGameData = new FoodMiniGameData(id, successCount, maxHealth, firstHealth, addHealth, clearAddTime);
            _foodMiniGameDataList.Add(foodMiniGameData);
            _foodMiniGameDataDic.Add(id, foodMiniGameData);
        }

            data = csvData.text.Split(new char[] { '\n' });
        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = row[0].Replace(" ", "");

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!spriteDic.TryGetValue(id, out Sprite sprite))
            {
                DebugLog.LogError("스프라이트가 없습니다: " + id);
                continue;
            }

            if (!thumbnailSpriteDic.TryGetValue(id, out Sprite thumbnailSprite))
            {
                DebugLog.LogError("스프라이트가 없습니다: " + id);
                continue;
            }

            string name = row[1];
            FoodType foodType = GetFoodType(row[2].Replace(" ", ""));
            string description = row[3];
            string needItem = row[5].Replace(" ", "");
            if (!int.TryParse(row[6].Replace(" ", ""), out int buyScore))
                buyScore = 0;

            if (!int.TryParse(row[8].Replace(" ", ""), out int buyPrice))
                buyPrice = 0;

            int level1SellPrice = int.Parse(row[9].Replace(" ", ""));
            float level1CookSpeed = float.Parse(row[10].Replace(" ", ""));
            int level1UpgradeScore = int.Parse(row[11].Replace(" ", ""));
            string level1UpgradeNeedItem = row[12].Replace(" ", "");

            int level1UpgradePrice = int.Parse(row[15].Replace(" ", ""));

            int level2SellPrice = int.Parse(row[16].Replace(" ", ""));
            float level2CookSpeed = float.Parse(row[17].Replace(" ", ""));
            int level2UpgradeScore = 0;
            string level2UpgradeNeedItem = string.Empty;
            int level2UpgradePrice = 0;

            List<FoodLevelData> foodLevelDataList = new List<FoodLevelData>();
            foodLevelDataList.Add(new FoodLevelData(level1SellPrice, level1UpgradeScore, level1UpgradePrice, level1UpgradeNeedItem, level1CookSpeed));
            foodLevelDataList.Add(new FoodLevelData(level2SellPrice, level2UpgradeScore, level2UpgradePrice, level2UpgradeNeedItem, level2CookSpeed));

            if (!_foodMiniGameDataDic.TryGetValue(id, out FoodMiniGameData foodMiniGameData))
                foodMiniGameData = null;

            FoodData foodData = new FoodData(sprite, thumbnailSprite, name, id, foodType, MoneyType.Gold, buyScore, buyPrice, needItem, foodLevelDataList, foodMiniGameData);

            _foodDataList.Add(foodData);
            _foodDataDic.Add(id, foodData);
        }
    }


    private string CutStringUpToChar(string str, char delimiter)
    {
        str = str.ToUpper();
        int index = str.IndexOf(delimiter);

        if (index >= 0)
            return str.Substring(0, index);

        else
            return str;
    }


    private FoodType GetFoodType(string foodTypeStr)
    {
        return foodTypeStr switch
        {
            "내추럴" => FoodType.Natural,
            "전통적" => FoodType.Traditional,
            "빈티지" => FoodType.Vintage,
            "럭셔리" => FoodType.Luxury,
            "모던" => FoodType.Modern,
            "코지" => FoodType.Cozy,
            "트로피컬" => FoodType.Tropical,
            _ => throw new System.Exception("해당 음식 문자열이 이상합니다: " + foodTypeStr)
        };
    }
}
