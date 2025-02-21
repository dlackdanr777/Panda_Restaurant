using BackEnd;
using LitJson;
using System.Collections.Generic;
using System.Linq;

public class ServerStageData
{
    public ERestaurantFloorType UnlockFloor;

    public int Score;
    public int Tip;

    public List<List<string>> EquipStaffDataList = new List<List<string>>();
    public Dictionary<string, int> GiveStaffLevelDic = new Dictionary<string, int>();

    public List<string> GiveFurnitureList = new List<string>();
    public List<List<string>> EquipFurnitureList = new List<List<string>>();

    public List<string> GiveKitchenUtensilList = new List<string>();
    public List<List<string>> EquipKitchenUtensilList = new List<List<string>>();

    public List<List<CoinAreaData>> CoinAreaDataList = new List<List<CoinAreaData>>();
    public List<List<GarbageAreaData>> GarbageAreaDataList = new List<List<GarbageAreaData>>();


    public Param GetParam()
    {
        Param param = new Param();

        param.Add("UnlockFloor", (int)UnlockFloor);
        param.Add("Score", Score);
        param.Add("Tip", Tip);
        param.Add("GiveStaffLevelDic", GiveStaffLevelDic.ToDictionary(x => x.Key, x => x.Value));
        param.Add("EquipStaffDataList", EquipStaffDataList);
        param.Add("GiveFurnitureList", GiveFurnitureList.ToList());
        param.Add("EquipFurnitureList", EquipFurnitureList);
        param.Add("GiveKitchenUtensilList", GiveKitchenUtensilList.ToList());
        param.Add("EquipKitchenUtensilList", EquipKitchenUtensilList);
        param.Add("DropCoinAreaDataList", CoinAreaDataList);
        param.Add("DropGarbageAreaDataList", GarbageAreaDataList);

        return param;
    }

    public void SetData(JsonData json)
    {
        if (json == null || json.Count == 0)
            return;

        JsonData data = json[0];

        // 안전한 데이터 가져오기 메서드
        bool GetBool(string key) => data.ContainsKey(key) && data[key].ToString().ToLower() == "true";
        int GetInt(string key) => data.ContainsKey(key) && int.TryParse(data[key].ToString(), out int value) ? value : 0;
        long GetLong(string key) => data.ContainsKey(key) && long.TryParse(data[key].ToString(), out long value) ? value : 0;
        string GetString(string key) => data.ContainsKey(key) ? data[key].ToString() : string.Empty;

        // 기본 데이터 세팅
        UnlockFloor = (ERestaurantFloorType)GetInt("UnlockFloor");
        Score = GetInt("Score");
        Tip = GetInt("Tip");


        // Dictionary 데이터 변환
        if (data.ContainsKey("GiveStaffLevelDic"))
        {
            JsonData staffDic = data["GiveStaffLevelDic"];
            GiveStaffLevelDic.Clear();
            foreach (var key in staffDic.Keys)
            {
                int value = int.TryParse(staffDic[key].ToString(), out int level) ? level : 1;
                GiveStaffLevelDic[key] = value;
            }
        }

        // List<List<string>> 데이터 변환
        EquipStaffDataList = ConvertJsonTo2DList(data, "EquipStaffDataList");
        EquipFurnitureList = ConvertJsonTo2DList(data, "EquipFurnitureList");
        EquipKitchenUtensilList = ConvertJsonTo2DList(data, "EquipKitchenUtensilList");

        // List<string> 데이터 변환
        GiveFurnitureList = ConvertJsonToList(data, "GiveFurnitureList");
        GiveKitchenUtensilList = ConvertJsonToList(data, "GiveKitchenUtensilList");

        if (data.ContainsKey("DropCoinAreaDataList"))
            CoinAreaDataList = ConvertJsonToCoinAreaList(data["DropCoinAreaDataList"]);

        if (data.ContainsKey("DropGarbageAreaDataList"))
            GarbageAreaDataList = ConvertJsonToGarbageAreaList(data["DropGarbageAreaDataList"]);
    }

    //JSON 데이터를 1차원 리스트로 변환하는 헬퍼 함수
    private List<string> ConvertJsonToList(JsonData data, string key)
    {
        List<string> list = new List<string>();

        if (data.ContainsKey(key) && data[key].IsArray)
        {
            foreach (JsonData item in data[key])
            {
                list.Add(item.ToString());
            }
        }
        return list;
    }

    //JSON 데이터를 2차원 리스트로 변환하는 헬퍼 함수
    private List<List<string>> ConvertJsonTo2DList(JsonData data, string key)
    {
        List<List<string>> list = new List<List<string>>();

        if (data.ContainsKey(key) && data[key].IsArray)
        {
            foreach (JsonData row in data[key])
            {
                List<string> subList = new List<string>();
                foreach (JsonData item in row)
                {
                    subList.Add(item.ToString());
                }
                list.Add(subList);
            }
        }
        return list;
    }


    private List<List<CoinAreaData>> ConvertJsonToCoinAreaList(JsonData jsonData)
    {
        List<List<CoinAreaData>> list = new List<List<CoinAreaData>>();

        if (jsonData.IsArray)
        {
            foreach (JsonData row in jsonData)
            {
                List<CoinAreaData> rowList = new List<CoinAreaData>();
                foreach (JsonData item in row)
                {
                    int coinCount = item.ContainsKey("CoinCount") ? int.Parse(item["CoinCount"].ToString()) : 0;
                    long money = item.ContainsKey("Money") ? long.Parse(item["Money"].ToString()) : 0;
                    CoinAreaData data = new CoinAreaData();
                    data.SetCoinCount(coinCount);
                    data.SetMoney(money);
                    rowList.Add(data);
                }
                list.Add(rowList);
            }
        }
        return list;
    }

    // 🔹 List<List<GarbageAreaData>> 변환 함수
    private List<List<GarbageAreaData>> ConvertJsonToGarbageAreaList(JsonData jsonData)
    {
        List<List<GarbageAreaData>> list = new List<List<GarbageAreaData>>();

        if (jsonData.IsArray)
        {
            foreach (JsonData row in jsonData)
            {
                List<GarbageAreaData> rowList = new List<GarbageAreaData>();
                foreach (JsonData item in row)
                {
                    int count = item.ContainsKey("Count") ? int.Parse(item["Count"].ToString()) : 0;
                    GarbageAreaData data = new GarbageAreaData();
                    data.SetCount(count);
                    rowList.Add(data);

                }
                list.Add(rowList);
            }
        }
        return list;
    }
}
