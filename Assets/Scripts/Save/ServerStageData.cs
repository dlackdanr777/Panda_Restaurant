using BackEnd;
using LitJson;
using System.Collections.Generic;
using System.Linq;

public class ServerStageData
{
    public ERestaurantFloorType UnlockFloor;

    public int Score;
    public int Tip;

    public float Satisfaction;

    public Dictionary<string, Dictionary<string, string>> EquipStaffDataDic = new Dictionary<string, Dictionary<string, string>>();
    public List<SaveStaffData> GiveStaffList = new List<SaveStaffData>();

    public List<string> GiveFurnitureList = new List<string>();
    public List<List<string>> EquipFurnitureList = new List<List<string>>();

    public List<string> GiveKitchenUtensilList = new List<string>();
    public List<List<string>> EquipKitchenUtensilList = new List<List<string>>();

    public List<List<CoinAreaData>> CoinAreaDataList = new List<List<CoinAreaData>>();
    public List<List<GarbageAreaData>> GarbageAreaDataList = new List<List<GarbageAreaData>>();

    public List<SaveKitchenData> SaveKitchenDataList = new List<SaveKitchenData>();
    public List<List<SaveTableData>> SaveTableDataList = new List<List<SaveTableData>>();


    public Param GetParam()
    {
        Param param = new Param();

        param.Add("UnlockFloor", (int)UnlockFloor);
        param.Add("Score", Score);
        param.Add("Tip", Tip);
        param.Add("Satisfaction", Satisfaction);

        param.Add("GiveStaffList", GiveStaffList);
        param.Add("EquipStaffDataDic", EquipStaffDataDic);
        param.Add("GiveFurnitureList", GiveFurnitureList.ToList());
        param.Add("EquipFurnitureList", EquipFurnitureList);
        param.Add("GiveKitchenUtensilList", GiveKitchenUtensilList.ToList());
        param.Add("EquipKitchenUtensilList", EquipKitchenUtensilList);
        param.Add("DropCoinAreaDataList", CoinAreaDataList);
        param.Add("DropGarbageAreaDataList", GarbageAreaDataList);
        param.Add("SaveTableDataList", SaveTableDataList);
        param.Add("SaveKitchenDataList", SaveKitchenDataList);

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
        float GetFloat(string key) => data.ContainsKey(key) && float.TryParse(data[key].ToString(), out float value) ? value : 0f;
        long GetLong(string key) => data.ContainsKey(key) && long.TryParse(data[key].ToString(), out long value) ? value : 0;
        string GetString(string key) => data.ContainsKey(key) ? data[key].ToString() : string.Empty;

        // 기본 데이터 세팅
        UnlockFloor = (ERestaurantFloorType)GetInt("UnlockFloor");
        Score = GetInt("Score");
        Tip = GetInt("Tip");
        Satisfaction = GetFloat("Satisfaction");


        if (data.ContainsKey("GiveStaffList"))
        {
            JsonData staffListJson = data["GiveStaffList"];
            GiveStaffList.Clear();

            foreach (JsonData staffData in staffListJson)
            {
                string id = staffData["Id"].ToString();
                int level = int.TryParse(staffData["Level"].ToString(), out int parsedLevel) ? parsedLevel : 1;
                GiveStaffList.Add(new SaveStaffData(id, level));
            }
        }

        // List<List<string>> 데이터 변환
        if (data.ContainsKey("EquipStaffDataDic"))
        {
            // 새로운 딕셔너리 형태로 저장된 데이터 로드
            EquipStaffDataDic = ConvertJsonToStaffDictionary(data["EquipStaffDataDic"]);
        }
        else if (data.ContainsKey("EquipStaffDataList"))
        {
            // 기존 리스트 형태에서 딕셔너리로 변환 (하위 호환성)
            List<List<string>> tempList = ConvertJsonTo2DList(data, "EquipStaffDataList");
            EquipStaffDataDic = ConvertListToDictionary(tempList);
        }
        else
        {
            // 기본값으로 빈 딕셔너리 설정
            EquipStaffDataDic = new Dictionary<string, Dictionary<string, string>>();
        }

        EquipFurnitureList = ConvertJsonTo2DList(data, "EquipFurnitureList");
        EquipKitchenUtensilList = ConvertJsonTo2DList(data, "EquipKitchenUtensilList");

        // List<string> 데이터 변환
        GiveFurnitureList = ConvertJsonToList(data, "GiveFurnitureList");
        GiveKitchenUtensilList = ConvertJsonToList(data, "GiveKitchenUtensilList");

        if (data.ContainsKey("DropCoinAreaDataList"))
            CoinAreaDataList = ConvertJsonToCoinAreaList(data["DropCoinAreaDataList"]);

        if (data.ContainsKey("DropGarbageAreaDataList"))
            GarbageAreaDataList = ConvertJsonToGarbageAreaList(data["DropGarbageAreaDataList"]);

        if (data.ContainsKey("SaveTableDataList"))
            SaveTableDataList = ConvertJsonToSaveTableList(data["SaveTableDataList"]);

        if (data.ContainsKey("SaveKitchenDataList"))
            SaveKitchenDataList = ConvertJsonToKitchenList(data["SaveKitchenDataList"]);
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


    private List<List<SaveTableData>> ConvertJsonToSaveTableList(JsonData jsonData)
    {
        List<List<SaveTableData>> result = new List<List<SaveTableData>>();

        if (jsonData.IsArray)
        {
            foreach (JsonData row in jsonData)
            {
                List<SaveTableData> innerList = new List<SaveTableData>();
                foreach (JsonData item in row)
                {
                    var saveData = new SaveTableData(ERestaurantFloorType.Floor1, TableType.Table1); // 기본값 생성 후 setter로 채움

                    if (item.ContainsKey("FloorType"))
                        saveData.SetFloorType((ERestaurantFloorType)int.Parse(item["FloorType"].ToString()));

                    if (item.ContainsKey("TableType"))
                        saveData.SetTableType((TableType)int.Parse(item["TableType"].ToString()));

                    if (item.ContainsKey("NeedCleaning"))
                        saveData.SetNeedCleaning(item["NeedCleaning"].ToString().ToLower() == "true");

                    // CoinAreaData[]
                    if (item.ContainsKey("CoinAreaDatas"))
                    {
                        JsonData coinArray = item["CoinAreaDatas"];
                        for (int i = 0; i < coinArray.Count && i < 2; i++)
                        {
                            var coinItem = coinArray[i];
                            int coinCount = coinItem.ContainsKey("CoinCount") ? int.Parse(coinItem["CoinCount"].ToString()) : 0;
                            long money = coinItem.ContainsKey("Money") ? long.Parse(coinItem["Money"].ToString()) : 0;

                            var coinData = new CoinAreaData();
                            coinData.SetCoinCount(coinCount);
                            coinData.SetMoney(money);
                            saveData.SetCoinAreaData(i, coinData);
                        }
                    }

                    // GarbageAreaData
                    if (item.ContainsKey("GarbageAreaData"))
                    {
                        JsonData g = item["GarbageAreaData"];
                        int count = g.ContainsKey("Count") ? int.Parse(g["Count"].ToString()) : 0;

                        var garbageData = new GarbageAreaData();
                        garbageData.SetCount(count);
                        saveData.SetGarbageAreaData(garbageData);
                    }

                    innerList.Add(saveData);
                }
                result.Add(innerList);
            }
        }

        return result;
    }

    private List<SaveKitchenData> ConvertJsonToKitchenList(JsonData jsonData)
    {
        List<SaveKitchenData> list = new List<SaveKitchenData>();

        if (jsonData.IsArray)
        {
            foreach (JsonData item in jsonData)
            {
                var kitchenData = new SaveKitchenData();

                if (item.ContainsKey("FloorType"))
                    kitchenData.SetFloorType((ERestaurantFloorType)int.Parse(item["FloorType"].ToString()));

                if (item.ContainsKey("MaxSinkBowlCount"))
                    kitchenData.SetMaxSinkBowlCount(int.Parse(item["MaxSinkBowlCount"].ToString()));

                if (item.ContainsKey("SinkBowlCount"))
                    kitchenData.SetSinkBowlCount(int.Parse(item["SinkBowlCount"].ToString()));

                // 필요한 필드 추가

                list.Add(kitchenData);
            }
        }

        return list;
    }
    

     // ✅ JSON에서 딕셔너리로 직접 변환
    private Dictionary<string, Dictionary<string, string>> ConvertJsonToStaffDictionary(JsonData jsonData)
    {
        Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();

        if (jsonData != null)
        {
            foreach (string floorKey in jsonData.Keys)
            {
                Dictionary<string, string> equipTypes = new Dictionary<string, string>();
                JsonData floorData = jsonData[floorKey];
                
                foreach (string equipKey in floorData.Keys)
                {
                    equipTypes[equipKey] = floorData[equipKey].ToString();
                }
                
                result[floorKey] = equipTypes;
            }
        }

        return result;
    }

    // ✅ 기존 리스트를 딕셔너리로 변환 (하위 호환성)
    private Dictionary<string, Dictionary<string, string>> ConvertListToDictionary(List<List<string>> staffList)
    {
        Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();

        for (int floorIndex = 0; floorIndex < staffList.Count; floorIndex++)
        {
            if (floorIndex >= (int)ERestaurantFloorType.Length)
                break;

            ERestaurantFloorType floor = (ERestaurantFloorType)floorIndex;
            string floorKey = floor.ToString();
            
            Dictionary<string, string> equipTypes = new Dictionary<string, string>();
            
            for (int equipIndex = 0; equipIndex < staffList[floorIndex].Count; equipIndex++)
            {
                if (equipIndex >= (int)EquipStaffType.Length)
                    break;

                EquipStaffType equipType = (EquipStaffType)equipIndex;
                string equipKey = equipType.ToString();
                string staffId = staffList[floorIndex][equipIndex];
                
                equipTypes[equipKey] = staffId ?? "";
            }
            
            result[floorKey] = equipTypes;
        }

        return result;
    }
}
