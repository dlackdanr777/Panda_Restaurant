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
    public float FeverGauge;

    public List<string> WaitingCustomerIdList = new List<string>();
    public WaitingPartySaveData WaitingParties;

    public Dictionary<string, Dictionary<string, string>> EquipStaffDataDic = new Dictionary<string, Dictionary<string, string>>();
    public List<SaveStaffData> GiveStaffList = new List<SaveStaffData>();
    public List<string> GiveStaffSkinList = new List<string>();

    public List<string> GiveFurnitureList = new List<string>();
    public List<List<string>> EquipFurnitureList = new List<List<string>>();

    public List<string> GiveKitchenUtensilList = new List<string>();
    public List<List<string>> EquipKitchenUtensilList = new List<List<string>>();

    public List<List<CoinAreaData>> CoinAreaDataList = new List<List<CoinAreaData>>();
    public List<List<GarbageAreaData>> GarbageAreaDataList = new List<List<GarbageAreaData>>();

    public List<SaveKitchenData> SaveKitchenDataList = new List<SaveKitchenData>();
    public List<List<SaveTableData>> SaveTableDataList = new List<List<SaveTableData>>();


    public Param GetParam(bool existingRowUpdate = false)
    {
        var waiting = WaitingParties ?? WaitingPartySaveData.FromLegacy(WaitingCustomerIdList);
        if (!waiting.CanSave && !existingRowUpdate) throw new System.InvalidOperationException("Waiting fields require an existing-row partial update: " + waiting.Reason);
        Param param = new Param();

        param.Add("UnlockFloor", (int)UnlockFloor);
        param.Add("Score", Score);
        param.Add("Tip", Tip);
        param.Add("Satisfaction", Satisfaction);
        param.Add("FeverGauge", FeverGauge);
        // Legacy output is derived, never a second source. Multi-member teams are not
        // split for old clients; alternating old/new application writes is unsupported.
        if (waiting.CanSave)
        {
            param.Add("WaitingCustomerIdList", waiting.Teams.All(t => t.Count == 1)
                ? waiting.Teams.Select(t => t[0]).ToList() : new List<string>());
            param.Add(WaitingPartySaveData.FieldName, waiting.Serialize());
        }
        // A partial Update leaves BOTH original columns untouched, including their
        // unknown JSON/SDK types. Never use this payload to insert a replacement row.

        param.Add("GiveStaffList", GiveStaffList);
        param.Add("EquipStaffDataDic", EquipStaffDataDic);
        param.Add("GiveStaffSkinList", GiveStaffSkinList);
        param.Add("GiveFurnitureList", GiveFurnitureList);
        param.Add("EquipFurnitureList", EquipFurnitureList);
        param.Add("GiveKitchenUtensilList", GiveKitchenUtensilList);
        param.Add("EquipKitchenUtensilList", EquipKitchenUtensilList);
        param.Add("DropCoinAreaDataList", CoinAreaDataList);
        param.Add("DropGarbageAreaDataList", GarbageAreaDataList);
        param.Add("SaveTableDataList", SaveTableDataList);
        param.Add("SaveKitchenDataList", SaveKitchenDataList);

        return param;
    }

    public void SetData(JsonData json, WaitingPartySaveData waiting = null)
    {
        if (json == null || json.Count == 0)
            return;

        JsonData data = json[0];
        // This path may now load normal fields beside an opaque waiting queue.
        // Do not authorize a later partial save from silently defaulted bad fields.
        if (waiting != null && (waiting.IsProtected || waiting.Teams.Count > 0))
            ValidateOtherStageFields(WaitingPartySaveData.ParseRawObject(data.ToJson()));

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
        FeverGauge = GetFloat("FeverGauge");

        WaitingParties = (waiting ?? WaitingPartySaveData.ParsePlain(Newtonsoft.Json.Linq.JObject.Parse(data.ToJson()))).RequireValidation();
        WaitingCustomerIdList = WaitingParties.Teams.All(t => t.Count == 1)
            ? WaitingParties.Teams.Select(t => t[0]).ToList() : new List<string>();


        if (data.ContainsKey("GiveStaffList"))
        {
            JsonData staffListJson = data["GiveStaffList"];
            GiveStaffList.Clear();

            foreach (JsonData staffData in staffListJson)
            {
                string id = staffData["Id"].ToString();
                int level = int.TryParse(staffData["Level"].ToString(), out int parsedLevel) ? parsedLevel : 1;
                string skinId = staffData.ContainsKey("SkinId") ? staffData["SkinId"].ToString() : string.Empty;
                SaveStaffData staff = new SaveStaffData(id, level);
                staff.SetSkinId(skinId);
                GiveStaffList.Add(staff);
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

        GiveStaffSkinList = ConvertJsonToList(data, "GiveStaffSkinList");

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

    private static void ValidateOtherStageFields(Newtonsoft.Json.Linq.JObject row)
    {
        void Require(bool valid, string field)
        { if (!valid) throw new System.FormatException("Invalid Stage field: " + field); }
        void Number(Newtonsoft.Json.Linq.JObject value, string name, bool floating = false, bool wide = false)
        {
            if (!value.TryGetValue(name, out var token)) return; // Existing legacy defaults for absent fields.
            string text = token.Type == Newtonsoft.Json.Linq.JTokenType.Null ? null : token.ToString();
            bool valid = floating ? float.TryParse(text, out float f) && !float.IsNaN(f) && !float.IsInfinity(f)
                : wide ? long.TryParse(text, out _) : int.TryParse(text, out _);
            Require(valid, name);
        }
        void Strings(Newtonsoft.Json.Linq.JToken token)
        { Require(token is Newtonsoft.Json.Linq.JArray && token.All(t => t.Type == Newtonsoft.Json.Linq.JTokenType.String), token.Path); }
        void Array(string field, bool nested, System.Action<Newtonsoft.Json.Linq.JToken> validate)
        {
            if (!row.TryGetValue(field, out var token)) return;
            Require(token is Newtonsoft.Json.Linq.JArray, field);
            foreach (var item in token)
            {
                if (!nested) validate(item);
                else { Require(item is Newtonsoft.Json.Linq.JArray, field); foreach (var cell in item) validate(cell); }
            }
        }
        void Record(Newtonsoft.Json.Linq.JToken token)
        {
            Require(token is Newtonsoft.Json.Linq.JObject, token.Path);
            var value = (Newtonsoft.Json.Linq.JObject)token;
            foreach (string name in new[] { "CoinCount", "Count", "FloorType", "TableType", "MaxSinkBowlCount", "SinkBowlCount", "Level" }) Number(value, name);
            Number(value, "Money", wide: true);
            foreach (string name in new[] { "Id", "SkinId" })
                if (value.TryGetValue(name, out var id)) Require(id.Type == Newtonsoft.Json.Linq.JTokenType.String, name);
            if (value.TryGetValue("NeedCleaning", out var clean)) Require(bool.TryParse(clean.ToString(), out _), "NeedCleaning");
            if (value.TryGetValue("CleaningBowlProcessed", out var bowl)) Require(bool.TryParse(bowl.ToString(), out _), "CleaningBowlProcessed");
            if (value.TryGetValue("FloorType", out var floor)) Require((int)floor >= 0 && (int)floor < (int)ERestaurantFloorType.Length, "FloorType");
            if (value.TryGetValue("TableType", out var table)) Require((int)table >= 0 && (int)table < (int)TableType.Length, "TableType");
            if (value.TryGetValue("CoinAreaDatas", out var coins))
            { Require(coins is Newtonsoft.Json.Linq.JArray && coins.Count() <= 2, "CoinAreaDatas"); foreach (var coin in coins) Record(coin); }
            if (value.TryGetValue("GarbageAreaData", out var garbage)) Record(garbage);
        }
        foreach (string field in new[] { "UnlockFloor", "Score", "Tip" }) Number(row, field);
        foreach (string field in new[] { "Satisfaction", "FeverGauge" }) Number(row, field, floating: true);
        foreach (string field in new[] { "GiveFurnitureList", "GiveKitchenUtensilList", "GiveStaffSkinList" })
            if (row.TryGetValue(field, out var list)) Strings(list);
        foreach (string field in new[] { "EquipFurnitureList", "EquipKitchenUtensilList", "EquipStaffDataList" }) Array(field, false, Strings);
        foreach (string field in new[] { "GiveStaffList", "SaveKitchenDataList" }) Array(field, false, Record);
        foreach (string field in new[] { "DropCoinAreaDataList", "DropGarbageAreaDataList", "SaveTableDataList" }) Array(field, true, Record);
        if (row.TryGetValue("EquipStaffDataDic", out var equipment))
        {
            Require(equipment is Newtonsoft.Json.Linq.JObject, "EquipStaffDataDic");
            foreach (var floor in ((Newtonsoft.Json.Linq.JObject)equipment).Properties())
            {
                Require(floor.Value is Newtonsoft.Json.Linq.JObject, floor.Name);
                foreach (var slot in ((Newtonsoft.Json.Linq.JObject)floor.Value).Properties())
                    Require(slot.Value.Type == Newtonsoft.Json.Linq.JTokenType.String, slot.Name);
            }
        }
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

                    if (item.ContainsKey("CleaningBowlProcessed"))
                    {
                        if (!bool.TryParse(item["CleaningBowlProcessed"].ToString(), out bool processed))
                            throw new System.FormatException("Invalid CleaningBowlProcessed");
                        saveData.SetCleaningBowlProcessed(processed);
                    }
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
    // ⚠️ 리스트 인덱스는 저장 당시 enum 순서(Manager=0, Marketer=1, Waiter=2, Cleaner=3, Guard=4, Chef=5)에 고정.
    //    현재 enum 순서가 바뀌어도 이 매핑은 변경하지 않아야 구 데이터가 올바르게 마이그레이션됩니다.
    private static readonly string[] _legacyEquipStaffOrder = new string[]
    {
        "Manager",   // 0
        "Marketer",  // 1
        "Waiter",    // 2
        "Cleaner",   // 3
        "Guard",     // 4
        "Chef",      // 5
    };

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
                if (equipIndex >= _legacyEquipStaffOrder.Length)
                    break;

                string equipKey = _legacyEquipStaffOrder[equipIndex];
                string staffId = staffList[floorIndex][equipIndex];
                
                equipTypes[equipKey] = staffId ?? "";
            }
            
            result[floorKey] = equipTypes;
        }

        return result;
    }
}
