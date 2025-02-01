

using BackEnd;
using LitJson;
using Muks.BackEnd;
using System.Collections.Generic;

public class LoadData
{
    public bool IsFirstTutorialClear;
    public bool IsMiniGameTutorialClear;
    public bool IsGatecrasher1TutorialClear;
    public bool IsGatecrasher2TutorialClear;
    public bool IsSpecialCustomer1TutorialClear;
    public bool IsSpecialCustomer2TutorialClear;

    public ERestaurantFloorType CurrentFloor;
    public int Dia;
    public long Money;
    public long TotalAddMoney;
    public long DailyAddMoney;
    public int Score;
    public int Tip;
    public int TotalCookCount;
    public int DailyCookCount;
    public int TotalCumulativeCustomerCount;
    public int DailyCumulativeCustomerCount;
    public int PromotionCount;
    public int TotalAdvertisingViewCount;
    public int DailyAdvertisingViewCount;
    public int TotalCleanCount;
    public int DailyCleanCount;
    public int TotalVisitSpecialCustomerCount;
    public int TotalExterminationGatecrasherCustomer1Count;
    public int TotalExterminationGatecrasherCustomer2Count;
    public int TotalUseGachaMachineCount;

    public string UserId;
    public string FirstAccessTime;
    public string LastAccessTime;
    public string LastAttendanceTime;
    public int TotalAttendanceDays;

    public List<string> EquipStaffDataList = new List<string>();
    public Dictionary<string, int> GiveStaffLevelDic = new Dictionary<string, int>();

    public Dictionary<string, int> GiveRecipeLevelDic = new Dictionary<string, int>();
    public Dictionary<string, int> RecipeCookCountDic = new Dictionary<string, int>();

    public Dictionary<string, int> GiveGachaItemCountDic = new Dictionary<string, int>();
    public Dictionary<string, int> GiveGachaItemLevelDic = new Dictionary<string, int>();

    public List<string> GiveFurnitureList = new List<string>();
    public List<List<string>> EquipFurnitureList = new List<List<string>>();

    public List<string> GiveKitchenUtensilList = new List<string>();
    public List<List<string>> EquipKitchenUtensilList = new List<List<string>>();

    public HashSet<string> DoneMainChallengeSet = new HashSet<string>();
    public HashSet<string> ClearMainChallengeSet = new HashSet<string>();
    public HashSet<string> DoneAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> ClearAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> DoneDailyChallengeSet = new HashSet<string>();
    public HashSet<string> ClearDailyChallengeSet = new HashSet<string>();

    public HashSet<string> EnabledCustomerSet = new HashSet<string>();
    public HashSet<string> VisitedCustomerSet = new HashSet<string>();
    

    public  List<SaveCoinAreaData> CoinAreaDataList = new List<SaveCoinAreaData>();
    public  List<SaveGarbageAreaData> GarbageAreaDataList = new List<SaveGarbageAreaData>();

    public HashSet<string> NotificationMessageSet = new HashSet<string>();
    public LoadData(JsonData json)
    {
        IsFirstTutorialClear = json[0].ContainsKey("IsFirstTutorialClear") ? json[0]["IsFirstTutorialClear"].ToString().ToLower() == "true" : false;
        IsMiniGameTutorialClear = json[0].ContainsKey("IsMiniGameTutorialClear") ? json[0]["IsMiniGameTutorialClear"].ToString().ToLower() == "true" : false;
        IsGatecrasher1TutorialClear = json[0].ContainsKey("IsGatecrasher1TutorialClear") ? json[0]["IsGatecrasher1TutorialClear"].ToString().ToLower() == "true" : false;
        IsGatecrasher2TutorialClear = json[0].ContainsKey("IsGatecrasher2TutorialClear") ? json[0]["IsGatecrasher2TutorialClear"].ToString().ToLower() == "true" : false;
        IsSpecialCustomer1TutorialClear = json[0].ContainsKey("IsSpecialCustomer1TutorialClear") ? json[0]["IsSpecialCustomer1TutorialClear"].ToString().ToLower() == "true" : false;
        IsSpecialCustomer2TutorialClear = json[0].ContainsKey("IsSpecialCustomer2TutorialClear") ? json[0]["IsSpecialCustomer2TutorialClear"].ToString().ToLower() == "true" : false;

        CurrentFloor = json[0].ContainsKey("CurrentFloor") && int.TryParse(json[0]["CurrentFloor"].ToString(), out int floor) ? (ERestaurantFloorType)floor : ERestaurantFloorType.Floor1;
        Dia = json[0].ContainsKey("Dia") && int.TryParse(json[0]["Dia"].ToString(), out int dia) ? dia : 0;
        Money = json[0].ContainsKey("Money") && long.TryParse(json[0]["Money"].ToString(), out long money) ? money : 0;
        TotalAddMoney = json[0].ContainsKey("TotalAddMoney") && long.TryParse(json[0]["TotalAddMoney"].ToString(), out long totalAddMoney) ? totalAddMoney : 0;
        DailyAddMoney = json[0].ContainsKey("DailyAddMoney") && long.TryParse(json[0]["DailyAddMoney"].ToString(), out long dailyAddMoney) ? dailyAddMoney : 0;
        Score = json[0].ContainsKey("Score") && int.TryParse(json[0]["Score"].ToString(), out int score) ? score : 0;
        Tip = json[0].ContainsKey("Tip") && int.TryParse(json[0]["Tip"].ToString(), out int tip) ? tip : 0;
        TotalCookCount = json[0].ContainsKey("TotalCookCount") && int.TryParse(json[0]["TotalCookCount"].ToString(), out int totalCookCount) ? totalCookCount : 0;
        DailyCookCount = json[0].ContainsKey("DailyCookCount") && int.TryParse(json[0]["DailyCookCount"].ToString(), out int dailyCookCount) ? dailyCookCount : 0;
        TotalCumulativeCustomerCount = json[0].ContainsKey("TotalCumulativeCustomerCount") && int.TryParse(json[0]["TotalCumulativeCustomerCount"].ToString(), out int totalCustomerCount) ? totalCustomerCount : 0;
        DailyCumulativeCustomerCount = json[0].ContainsKey("DailyCumulativeCustomerCount") && int.TryParse(json[0]["DailyCumulativeCustomerCount"].ToString(), out int dailyCustomerCount) ? dailyCustomerCount : 0;
        PromotionCount = json[0].ContainsKey("PromotionCount") && int.TryParse(json[0]["PromotionCount"].ToString(), out int promotionCount) ? promotionCount : 0;
        TotalAdvertisingViewCount = json[0].ContainsKey("TotalAdvertisingViewCount") && int.TryParse(json[0]["TotalAdvertisingViewCount"].ToString(), out int totalAdCount) ? totalAdCount : 0;
        DailyAdvertisingViewCount = json[0].ContainsKey("DailyAdvertisingViewCount") && int.TryParse(json[0]["DailyAdvertisingViewCount"].ToString(), out int dailyAdCount) ? dailyAdCount : 0;
        TotalCleanCount = json[0].ContainsKey("TotalCleanCount") && int.TryParse(json[0]["TotalCleanCount"].ToString(), out int totalCleanCount) ? totalCleanCount : 0;
        DailyCleanCount = json[0].ContainsKey("DailyCleanCount") && int.TryParse(json[0]["DailyCleanCount"].ToString(), out int dailyCleanCount) ? dailyCleanCount : 0;

        TotalVisitSpecialCustomerCount = json[0].ContainsKey("TotalVisitSpecialCustomerCount") && int.TryParse(json[0]["TotalVisitSpecialCustomerCount"].ToString(), out int specialCustomerCount) ? specialCustomerCount : 0;
        TotalExterminationGatecrasherCustomer1Count = json[0].ContainsKey("TotalExterminationGatecrasherCustomer1Count") && int.TryParse(json[0]["TotalExterminationGatecrasherCustomer1Count"].ToString(), out int gatecrasher1Count) ? gatecrasher1Count : 0;
        TotalExterminationGatecrasherCustomer2Count = json[0].ContainsKey("TotalExterminationGatecrasherCustomer2Count") && int.TryParse(json[0]["TotalExterminationGatecrasherCustomer2Count"].ToString(), out int gatecrasher2Count) ? gatecrasher2Count : 0;
        TotalUseGachaMachineCount = json[0].ContainsKey("TotalUseGachaMachineCount") && int.TryParse(json[0]["TotalUseGachaMachineCount"].ToString(), out int gachaCount) ? gachaCount : 0;

        UserId = json[0].ContainsKey("UserId") ? json[0]["UserId"].ToString() : string.Empty;
        FirstAccessTime = json[0].ContainsKey("FirstAccessTime") ? json[0]["FirstAccessTime"].ToString() : string.Empty;
        LastAccessTime = json[0].ContainsKey("LastAccessTime") ? json[0]["LastAccessTime"].ToString() : string.Empty;
        LastAttendanceTime = json[0].ContainsKey("LastAttendanceTime") ? json[0]["LastAttendanceTime"].ToString() : string.Empty;
        TotalAttendanceDays = json[0].ContainsKey("TotalAttendanceDays") && int.TryParse(json[0]["TotalAttendanceDays"].ToString(), out int totalAttendance) ? totalAttendance : 0;

        if (json[0].ContainsKey("GiveStaffList"))
        {
            GiveStaffLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveStaffList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveStaffLevelDic[key] = value;
            }
        }

        if (json[0].ContainsKey("EquipStaffDatas"))
        {
            foreach (JsonData item in json[0]["EquipStaffDatas"])
            {
                EquipStaffDataList.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("GiveRecipeList"))
        {
            GiveRecipeLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveRecipeList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveRecipeLevelDic[key] = value;
            }
        }

        if (json[0].ContainsKey("RecipeCookCountList"))
        {
            RecipeCookCountDic.Clear();
            foreach (JsonData item in json[0]["RecipeCookCountList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Count"].ToString());
                RecipeCookCountDic[key] = value;
            }
        }

        if (json[0].ContainsKey("GiveGachaItemCountList"))
        {
            GiveGachaItemCountDic.Clear();
            foreach (JsonData item in json[0]["GiveGachaItemCountList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Count"].ToString());
                GiveGachaItemCountDic[key] = value;
            }
        }

        if (json[0].ContainsKey("GiveGachaItemLevelList"))
        {
            GiveGachaItemLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveGachaItemLevelList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveGachaItemLevelDic[key] = value;
            }
        }

        if (json[0].ContainsKey("GiveFurnitureList"))
        {
            foreach (JsonData item in json[0]["GiveFurnitureList"])
            {
                GiveFurnitureList.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("EquipFurnitureList"))
        {
            JsonData furnitureJsonList = json[0]["EquipFurnitureList"];
            EquipFurnitureList.Clear();

            if (furnitureJsonList.Count > 0 && furnitureJsonList[0].IsString)
            {
                // 저장된 데이터가 1차원 배열인 경우 (이전 방식)
                List<string> row = new List<string>();
                foreach (JsonData item in furnitureJsonList)
                {
                    row.Add(item.ToString());
                }
                EquipFurnitureList.Add(row);
            }
            else
            {
                // 저장된 데이터가 2차원 배열인 경우 (새로운 방식)
                for (int i = 0; i < furnitureJsonList.Count; i++)
                {
                    List<string> row = new List<string>();
                    JsonData rowData = furnitureJsonList[i];

                    for (int j = 0; j < rowData.Count; j++)
                    {
                        row.Add(rowData[j].ToString());
                    }

                    EquipFurnitureList.Add(row);
                }
            }
        }


        if (json[0].ContainsKey("GiveKitchenUtensilList"))
        {
            foreach (JsonData item in json[0]["GiveKitchenUtensilList"])
            {
                GiveKitchenUtensilList.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("EquipKitchenUtensilList"))
        {
            JsonData utensilJsonList = json[0]["EquipKitchenUtensilList"];
            EquipKitchenUtensilList.Clear();

            if (utensilJsonList.Count > 0 && utensilJsonList[0].IsString)
            {
                // 저장된 데이터가 1차원 배열인 경우 (이전 방식)
                List<string> row = new List<string>();
                foreach (JsonData item in utensilJsonList)
                {
                    row.Add(item.ToString());
                }
                EquipKitchenUtensilList.Add(row);
            }
            else
            {
                // 저장된 데이터가 2차원 배열인 경우
                for (int i = 0; i < utensilJsonList.Count; i++)
                {
                    List<string> row = new List<string>();
                    JsonData rowData = utensilJsonList[i];

                    for (int j = 0; j < rowData.Count; j++)
                    {
                        row.Add(rowData[j].ToString());
                    }

                    EquipKitchenUtensilList.Add(row);
                }
            }
        }


        if (json[0].ContainsKey("DoneMainChallengeList"))
        {
            foreach (JsonData item in json[0]["DoneMainChallengeList"])
            {
                DoneMainChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("ClearMainChallengeList"))
        {
            foreach (JsonData item in json[0]["ClearMainChallengeList"])
            {
                ClearMainChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("DoneAllTimeChallengeList"))
        {
            foreach (JsonData item in json[0]["DoneAllTimeChallengeList"])
            {
                DoneAllTimeChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("ClearAllTimeChallengeList"))
        {
            foreach (JsonData item in json[0]["ClearAllTimeChallengeList"])
            {
                ClearAllTimeChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("DoneDailyChallengeList"))
        {
            foreach (JsonData item in json[0]["DoneDailyChallengeList"])
            {
                DoneDailyChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("ClearDailyChallengeList"))
        {
            foreach (JsonData item in json[0]["ClearDailyChallengeList"])
            {
                ClearDailyChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("EnabledCustomerList"))
        {
            foreach (JsonData item in json[0]["EnabledCustomerList"])
            {
                EnabledCustomerSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("VisitedCustomerList"))
        {
            foreach (JsonData item in json[0]["VisitedCustomerList"])
            {
                VisitedCustomerSet.Add(item.ToString());
            }
        }


        if (json[0].ContainsKey("CoinAreaDataList"))
        {
            foreach (JsonData item in json[0]["CoinAreaDataList"])
            {
                // JSON 데이터를 클래스 객체로 변환
                int coinCount = (int)item["CoinCount"];
                long giveMoney = (long)item["Money"];
                SaveCoinAreaData data = new SaveCoinAreaData(coinCount, giveMoney);

                // 리스트에 추가
                CoinAreaDataList.Add(data);
            }
        }

        if (json[0].ContainsKey("GarbageAreaDataList"))
        {
            foreach (JsonData item in json[0]["GarbageAreaDataList"])
            {
                // JSON 데이터를 클래스 객체로 변환
                int count = (int)item["Count"];
                SaveGarbageAreaData data = new SaveGarbageAreaData(count);

                // 리스트에 추가
                GarbageAreaDataList.Add(data);
            }
        }



        if (json[0].ContainsKey("NotificationMessageList"))
        {
            foreach (JsonData item in json[0]["NotificationMessageList"])
            {
                NotificationMessageSet.Add(item.ToString());
            }
        }
    }
}


public class SaveLevelData
{
    public string Id;
    public int Level;

    public SaveLevelData(string id, int level)
    {
        Id = id;
        Level = level;
    }
}

public class SaveCountData
{
    public string Id;
    public int Count;

    public SaveCountData(string id, int count)
    {
        Id = id;
        Count = count;
    }
}


public class SaveCoinAreaData
{
    public int CoinCount;
    public long Money;

    public SaveCoinAreaData(int coinCount, long money)
    {
        CoinCount = coinCount;
        Money = money;
    }
}

public class SaveGarbageAreaData
{
    public int Count;

    public SaveGarbageAreaData(int count)
    {
        Count = count;
    }
}
