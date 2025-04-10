using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("ItemManager");
                _instance = obj.AddComponent<ItemManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static ItemManager _instance;

    private static List<GachaItemData> _gachaItemDataList = new List<GachaItemData>();
    private static Dictionary<string, GachaItemData> _gachaItemDataDic = new Dictionary<string, GachaItemData>();

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }


    public GachaItemData GetGachaItemData(string id)
    {
        if (_gachaItemDataDic.TryGetValue(id, out GachaItemData data))
            return data;

        throw new System.Exception("해당 id를 가진 가챠 아이템이 존재하지 않습니다: " + id);
    }


    public List<GachaItemData> GetSortGachaItemDataList()
    {
        return UserInfo.GachaItemSortType switch
        {
            GradeSortType.NameAscending => _gachaItemDataList.OrderBy(data => data.Name).ToList(),
            GradeSortType.NameDescending => _gachaItemDataList.OrderByDescending(data => data.Name).ToList(),
            GradeSortType.GradeAscending => _gachaItemDataList.OrderBy(data => data.Rank).ThenBy(data => data.Name).ToList(),
            GradeSortType.GradeDescending => _gachaItemDataList.OrderByDescending(data => data.Rank).ThenBy(data => data.Name).ToList(),
            _ => null
        };
    }

    public List<GachaItemData> GetGachaItemDataList()
    {
        return _gachaItemDataList;
    }

    public GachaItemData GetRandomGachaItem(List<GachaItemData> gachaItemDataList)
    {
        List<GachaItemData> itemList = new List<GachaItemData>();
        float randF = UnityEngine.Random.Range(0f, 1f);
        float tmp = 0;
        GachaItemRank currentRank = GachaItemRank.Normal1;
        for(int i = 0, cnt = (int)GachaItemRank.Length; i < cnt; ++i)
        {
            tmp += Utility.GetGachaItemRankRange((GachaItemRank)i);
            if(randF < tmp)
            {
                currentRank = (GachaItemRank)i;
                break;
            }
        }

        for(int i = 0, cnt = gachaItemDataList.Count; i < cnt; ++i)
        {
            if (gachaItemDataList[i].GachaItemRank != currentRank)
                continue;

            itemList.Add(gachaItemDataList[i]); 
        }

        return itemList[UnityEngine.Random.Range(0, itemList.Count)];
    }


    public bool IsGachaItem(string id)
    {
        return _gachaItemDataDic.ContainsKey(id);
    }


    private void Init()
    {
        _gachaItemDataList.Clear();
        _gachaItemDataDic.Clear();
        Dictionary<string, Sprite> spriteDic = new Dictionary<string, Sprite>();
        Sprite[] sprites = Resources.LoadAll<Sprite>("ItemData/GachaItemData/Sprites");

        for(int i = 0, cnt = sprites.Length; i < cnt; ++i)
            spriteDic.Add(CutStringUpToChar(sprites[i].name, '_'), sprites[i]);

        TextAsset csvData = Resources.Load<TextAsset>("ItemData/GachaItemData/GachaItemList");
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = row[0].Replace(" ", ""); ;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            string name = row[1];
            string description = row[2];
            int addScore = int.Parse(row[4].Replace(" ", ""));
            int minutePerTip = int.Parse(row[5].Replace(" ", ""));
            int rank = int.Parse(row[6].Replace(" ", ""));
            UpgradeType upgradeType = StrToUpgradeType(row[8].Replace(" ", ""));
            float defaultValue = float.Parse(row[9].Replace(" ", ""));
            float upgradeValue = float.Parse(row[10].Replace(" ", ""));
            int maxLevel = int.Parse(row[11].Replace(" ", ""));

            GachaItemData gachaItemData = new GachaItemData(id, name, description, addScore, minutePerTip, rank, upgradeType, defaultValue, upgradeValue, maxLevel, spriteDic[id]);
            _gachaItemDataList.Add(gachaItemData);
            _gachaItemDataDic.Add(id, gachaItemData);
        }
    }


    private string CutStringUpToChar(string str, char delimiter)
    {
        int index = str.IndexOf(delimiter);

        if (index >= 0)
            return str.Substring(0, index);

        else
            return str;
    }


    private UpgradeType StrToUpgradeType(string str)
    {
        if (Enum.TryParse(str, true, out UpgradeType upgradeType))
            return upgradeType;

        throw new ArgumentException("해당 문자열을 가진 UpgradeType이 없습니다: " + str);
    }
}
