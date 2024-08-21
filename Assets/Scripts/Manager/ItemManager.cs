using System.Collections.Generic;
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


    public List<GachaItemData> GetGachaItemDataList()
    {
        return _gachaItemDataList;
    }

    public bool IsGachaItem(string id)
    {
        return _gachaItemDataDic.ContainsKey(id);
    }


    private void Init()
    {
        _gachaItemDataList.Clear();
        _gachaItemDataDic.Clear();
        TextAsset csvData = Resources.Load<TextAsset>("ItemData/GachaItemList");
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = row[0].Replace(" ", ""); ;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            string name = row[1].Replace(" ", "");
            string description = row[2].Replace(" ", ""); ;
            int addScore = int.Parse(row[4].Replace(" ", ""));
            int minutePerTip = int.Parse(row[5].Replace(" ", ""));
            int rank = int.Parse(row[6].Replace(" ", ""));
            int exchangeCount = int.Parse(row[8].Replace(" ", ""));
            int duplicatePaymentCount = int.Parse(row[9].Replace(" ", ""));

            GachaItemData gachaItemData = new GachaItemData(id, name, description, addScore, minutePerTip, rank, exchangeCount, duplicatePaymentCount);
            _gachaItemDataList.Add(gachaItemData);
            _gachaItemDataDic.Add(id, gachaItemData);
        }
    }
}
