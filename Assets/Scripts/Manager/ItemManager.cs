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

        throw new System.Exception("�ش� id�� ���� ��í �������� �������� �ʽ��ϴ�: " + id);
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

    public List<GachaItemData> GetSortGachaItemDataList(GradeSortType sortType)
    {
        return sortType switch
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
        if (gachaItemDataList == null || gachaItemDataList.Count == 0)
        {
            DebugLog.LogError("��í ������ ����Ʈ�� ����ֽ��ϴ�.");
            return null;
        }

        // ��ũ���� ������ �з� (�����ϴ� ��ũ�� ����)
        Dictionary<GachaItemRank, List<GachaItemData>> rankItemDict = new Dictionary<GachaItemRank, List<GachaItemData>>();
        List<GachaItemRank> availableRanks = new List<GachaItemRank>();

        foreach (var item in gachaItemDataList)
        {
            if (!rankItemDict.ContainsKey(item.GachaItemRank))
            {
                rankItemDict[item.GachaItemRank] = new List<GachaItemData>();
                availableRanks.Add(item.GachaItemRank);
            }
            rankItemDict[item.GachaItemRank].Add(item);
        }

        // ��ũ ���� �õ�
        int maxAttempts = 5; // �ִ� �õ� Ƚ�� ����
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float randF = UnityEngine.Random.Range(0f, 1f);
            float tmp = 0;
            GachaItemRank currentRank = GachaItemRank.Normal1;

            // ��ũ ����
            for (int i = 0, cnt = (int)GachaItemRank.Length; i < cnt; ++i)
            {
                tmp += Utility.GetGachaItemRankRange((GachaItemRank)i);
                if (randF < tmp)
                {
                    currentRank = (GachaItemRank)i;
                    break;
                }
            }

            // ���õ� ��ũ�� �������� �ִ��� Ȯ��
            if (rankItemDict.ContainsKey(currentRank) && rankItemDict[currentRank].Count > 0)
            {
                // �ش� ��ũ�� ������ �� �ϳ��� ���� ����
                List<GachaItemData> itemsOfRank = rankItemDict[currentRank];
                return itemsOfRank[UnityEngine.Random.Range(0, itemsOfRank.Count)];
            }

            // �ش� ��ũ�� �������� ������ �α� ���
            DebugLog.Log($"��ũ {currentRank}�� �������� ���� �ٽ� �õ��մϴ�. (�õ� {attempt + 1}/{maxAttempts})");
        }

        // ���� �� �õ� �Ŀ��� �����ߴٸ� ��� ������ ��ũ���� �������� ����
        if (availableRanks.Count > 0)
        {
            GachaItemRank fallbackRank = availableRanks[UnityEngine.Random.Range(0, availableRanks.Count)];
            DebugLog.Log($"��ũ ���� ����, ��ü ��ũ {fallbackRank} ���");
            List<GachaItemData> fallbackItems = rankItemDict[fallbackRank];
            return fallbackItems[UnityEngine.Random.Range(0, fallbackItems.Count)];
        }

        // ������� ���� �ɰ��� ������ ����
        DebugLog.LogError("��� ������ ��í �������� �����ϴ�.");
        return null;
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
            DebugLog.Log("id: " + id);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            string name = row[1];
            string description = row[2];
            int addScore = ReplaceIntValue(row[4]);
            int minutePerTip = ReplaceIntValue(row[5]);
            int rank = ReplaceIntValue(row[6]);
            UpgradeType upgradeType = StrToUpgradeType(row[8].Replace(" ", ""));
            float defaultValue = ReplaceFloatValue(row[9]);
            float upgradeValue = ReplaceFloatValue(row[10]);
            int maxLevel = ReplaceIntValue(row[11]) == 0 ? 1 : ReplaceIntValue(row[11]);

            GachaItemData gachaItemData = new GachaItemData(id, name, description, addScore, minutePerTip, rank, upgradeType, defaultValue, upgradeValue, maxLevel, spriteDic[id]);
            _gachaItemDataList.Add(gachaItemData);
            _gachaItemDataDic.Add(id, gachaItemData);

            float ReplaceFloatValue(string str) => float.TryParse(str.Replace(" ", ""), out float value) ? value : 0f;
            int ReplaceIntValue(string str) => int.TryParse(str.Replace(" ", ""), out int value) ? value : 0;
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

        DebugLog.Log("�ش� ���ڿ��� ���� UpgradeType�� �����ϴ�: " + str);
        return UpgradeType.None;
    }
}
