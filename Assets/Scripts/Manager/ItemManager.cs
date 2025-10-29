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
    private static Dictionary<UpgradeType, List<GachaItemData>> _upgradeTypeGachaItemDataDic = new Dictionary<UpgradeType, List<GachaItemData>>();

    private static Dictionary<UpgradeType, Sprite> _upgradeTypeSpriteDic = new Dictionary<UpgradeType, Sprite>();

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitCsv();
        InitGachaItemUpgradeSprite();
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
            DebugLog.LogError("가챠 아이템 리스트가 비어있습니다.");
            return null;
        }

        // 랭크별로 아이템 분류 (존재하는 랭크만 저장)
        Dictionary<Rank, List<GachaItemData>> rankItemDict = new Dictionary<Rank, List<GachaItemData>>();
        List<Rank> availableRanks = new List<Rank>();

        foreach (var item in gachaItemDataList)
        {
            if (!rankItemDict.ContainsKey(item.Rank))
            {
                rankItemDict[item.Rank] = new List<GachaItemData>();
                availableRanks.Add(item.Rank);
            }
            rankItemDict[item.Rank].Add(item);
        }

        // 랭크 선택 시도
        int maxAttempts = 5; // 최대 시도 횟수 제한
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float randF = UnityEngine.Random.Range(0f, 1f);
            float tmp = 0;
            Rank currentRank = Rank.Normal1;

            // 랭크 선택
            for (int i = 0, cnt = (int)Rank.Length; i < cnt; ++i)
            {
                tmp += Utility.GetGachaItemRankRange((Rank)i);
                if (randF < tmp)
                {
                    currentRank = (Rank)i;
                    break;
                }
            }

            // 선택된 랭크에 아이템이 있는지 확인
            if (rankItemDict.ContainsKey(currentRank) && rankItemDict[currentRank].Count > 0)
            {
                // 해당 랭크의 아이템 중 하나를 랜덤 선택
                List<GachaItemData> itemsOfRank = rankItemDict[currentRank];
                return itemsOfRank[UnityEngine.Random.Range(0, itemsOfRank.Count)];
            }

            // 해당 랭크에 아이템이 없으면 로그 출력
            DebugLog.Log($"랭크 {currentRank}에 아이템이 없어 다시 시도합니다. (시도 {attempt + 1}/{maxAttempts})");
        }

        // 여러 번 시도 후에도 실패했다면 사용 가능한 랭크에서 무작위로 선택
        if (availableRanks.Count > 0)
        {
            Rank fallbackRank = availableRanks[UnityEngine.Random.Range(0, availableRanks.Count)];
            DebugLog.Log($"랭크 선택 실패, 대체 랭크 {fallbackRank} 사용");
            List<GachaItemData> fallbackItems = rankItemDict[fallbackRank];
            return fallbackItems[UnityEngine.Random.Range(0, fallbackItems.Count)];
        }

        // 여기까지 오면 심각한 문제가 있음
        DebugLog.LogError("사용 가능한 가챠 아이템이 없습니다.");
        return null;
    }

        public GachaData GetRandomGachaData(List<GachaData> gachaDataList)
    {
        if (gachaDataList == null || gachaDataList.Count == 0)
        {
            DebugLog.LogError("가챠 아이템 리스트가 비어있습니다.");
            return null;
        }

        // 랭크별로 아이템 분류 (존재하는 랭크만 저장)
        Dictionary<Rank, List<GachaData>> rankItemDict = new Dictionary<Rank, List<GachaData>>();
        List<Rank> availableRanks = new List<Rank>();

        foreach (var item in gachaDataList)
        {
            if (!rankItemDict.ContainsKey(item.Rank))
            {
                rankItemDict[item.Rank] = new List<GachaData>();
                availableRanks.Add(item.Rank);
            }
            rankItemDict[item.Rank].Add(item);
        }

        // 랭크 선택 시도
        int maxAttempts = 5; // 최대 시도 횟수 제한
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float randF = UnityEngine.Random.Range(0f, 1f);
            float tmp = 0;
            Rank currentRank = Rank.Normal1;

            // 랭크 선택
            for (int i = 0, cnt = (int)Rank.Length; i < cnt; ++i)
            {
                tmp += Utility.GetGachaItemRankRange((Rank)i);
                if (randF < tmp)
                {
                    currentRank = (Rank)i;
                    break;
                }
            }

            // 선택된 랭크에 아이템이 있는지 확인
            if (rankItemDict.ContainsKey(currentRank) && rankItemDict[currentRank].Count > 0)
            {
                // 해당 랭크의 아이템 중 하나를 랜덤 선택
                List<GachaData> itemsOfRank = rankItemDict[currentRank];
                return itemsOfRank[UnityEngine.Random.Range(0, itemsOfRank.Count)];
            }

            // 해당 랭크에 아이템이 없으면 로그 출력
            DebugLog.Log($"랭크 {currentRank}에 아이템이 없어 다시 시도합니다. (시도 {attempt + 1}/{maxAttempts})");
        }

        // 여러 번 시도 후에도 실패했다면 사용 가능한 랭크에서 무작위로 선택
        if (availableRanks.Count > 0)
        {
            Rank fallbackRank = availableRanks[UnityEngine.Random.Range(0, availableRanks.Count)];
            DebugLog.Log($"랭크 선택 실패, 대체 랭크 {fallbackRank} 사용");
            List<GachaData> fallbackItems = rankItemDict[fallbackRank];
            return fallbackItems[UnityEngine.Random.Range(0, fallbackItems.Count)];
        }

        // 여기까지 오면 심각한 문제가 있음
        DebugLog.LogError("사용 가능한 가챠 아이템이 없습니다.");
        return null;
    }


    public bool IsGachaItem(string id)
    {
        return _gachaItemDataDic.ContainsKey(id);
    }

    public Sprite GetUpgradeIcon(UpgradeType upgradeType)
    {
        if (_upgradeTypeSpriteDic.TryGetValue(upgradeType, out Sprite sprite))
            return sprite;

        DebugLog.LogError($"업그레이드 타입에 해당하는 스프라이트를 찾을 수 없습니다: {upgradeType}");
        return null;
    }


    private void InitCsv()
    {
        _gachaItemDataList.Clear();
        _gachaItemDataDic.Clear();
        Dictionary<string, Sprite> spriteDic = new Dictionary<string, Sprite>();
        Sprite[] sprites = Resources.LoadAll<Sprite>("ItemData/GachaItemData/Sprites");

        for (int i = 0, cnt = sprites.Length; i < cnt; ++i)
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

            DebugLog.Log($"아이템 생성: {id}, {name}, {description}, {addScore}, {minutePerTip}, {rank}, {upgradeType}, {defaultValue}, {upgradeValue}, {maxLevel}");
            GachaItemData gachaItemData = new GachaItemData(id, name, description, addScore, minutePerTip, rank, upgradeType, defaultValue, upgradeValue, maxLevel, spriteDic[id]);
            _gachaItemDataList.Add(gachaItemData);
            _gachaItemDataDic.Add(id, gachaItemData);
            AddUpgradeGachaItemDic(gachaItemData);

            float ReplaceFloatValue(string str) => float.TryParse(str.Replace(" ", ""), out float value) ? value : 0f;
            int ReplaceIntValue(string str) => int.TryParse(str.Replace(" ", ""), out int value) ? value : 0;

            void AddUpgradeGachaItemDic(GachaItemData gachaItemData)
            {
                if (!_upgradeTypeGachaItemDataDic.ContainsKey(gachaItemData.UpgradeType))
                    _upgradeTypeGachaItemDataDic[gachaItemData.UpgradeType] = new List<GachaItemData>();

                _upgradeTypeGachaItemDataDic[gachaItemData.UpgradeType].Add(gachaItemData);
            }
        }
    }

    private void InitGachaItemUpgradeSprite()
    {
        for(int i = 0, cnt = (int)UpgradeType.Length; i < cnt; ++i)
        {
            UpgradeType upgradeType = (UpgradeType)i;
            Sprite sprite = Resources.Load<Sprite>($"ItemData/GachaItemData/Sprites/UpgradeSprites/{upgradeType}");
            if (sprite != null)
                _upgradeTypeSpriteDic[upgradeType] = sprite;
            else
                DebugLog.LogError($"업그레이드 타입에 해당하는 스프라이트를 찾을 수 없습니다: {upgradeType}");
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

        DebugLog.Log("해당 문자열을 가진 UpgradeType이 없습니다: " + str);
        return UpgradeType.None;
    }
}
