using BackEnd;
using LitJson;
using System.Collections.Generic;
using System.Linq;

public class ServerStageData
{
    public ERestaurantFloorType UnlockFloor;

    public int Score;

    public List<List<string>> EquipStaffDataList = new List<List<string>>();
    public Dictionary<string, int> GiveStaffLevelDic = new Dictionary<string, int>();

    public List<string> GiveFurnitureList = new List<string>();
    public List<List<string>> EquipFurnitureList = new List<List<string>>();

    public List<string> GiveKitchenUtensilList = new List<string>();
    public List<List<string>> EquipKitchenUtensilList = new List<List<string>>();


    public Param GetParam()
    {
        Param param = new Param();

        param.Add("UnlockFloor", (int)UnlockFloor);
        param.Add("Score", Score);
        param.Add("GiveStaffLevelDic", GiveStaffLevelDic.ToDictionary(x => x.Key, x => x.Value));
        param.Add("EquipStaffDataList", EquipStaffDataList);
        param.Add("GiveFurnitureList", GiveFurnitureList.ToList());
        param.Add("EquipFurnitureList", EquipFurnitureList);
        param.Add("GiveKitchenUtensilList", GiveKitchenUtensilList.ToList());
        param.Add("EquipKitchenUtensilList", EquipKitchenUtensilList);

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

        // Dictionary 데이터 변환
        if (data.ContainsKey("GiveStaffLevelDic"))
        {
            JsonData staffDic = data["GiveStaffLevelDic"];
            GiveStaffLevelDic.Clear();
            foreach (var key in staffDic.Keys)
            {
                DebugLog.Log(key);
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

}
