

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
    public List<string> EquipFurnitureList = new List<string>();

    public List<string> EquipKitchenUtensilList = new List<string>();
    public List<string> GiveKitchenUtensilList = new List<string>();

    public HashSet<string> DoneMainChallengeSet = new HashSet<string>();
    public HashSet<string> ClearMainChallengeSet = new HashSet<string>();
    public HashSet<string> DoneAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> ClearAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> DoneDailyChallengeSet = new HashSet<string>();
    public HashSet<string> ClearDailyChallengeSet = new HashSet<string>();

    public  List<SaveCoinAreaData> CoinAreaDataList = new List<SaveCoinAreaData>();
    public  List<SaveGarbageAreaData> GarbageAreaDataList = new List<SaveGarbageAreaData>();
    public LoadData(JsonData json)
    {
        IsFirstTutorialClear = json[0]["IsFirstTutorialClear"].ToString().ToLower() == "true";
        IsMiniGameTutorialClear = json[0]["IsMiniGameTutorialClear"].ToString().ToLower() == "true";
        IsGatecrasher1TutorialClear = json[0]["IsGatecrasher1TutorialClear"].ToString().ToLower() == "true";
        IsGatecrasher2TutorialClear = json[0]["IsGatecrasher2TutorialClear"].ToString().ToLower() == "true";
        IsSpecialCustomer1TutorialClear = json[0]["IsSpecialCustomer1TutorialClear"].ToString().ToLower() == "true";
        IsSpecialCustomer2TutorialClear = json[0]["IsSpecialCustomer2TutorialClear"].ToString().ToLower() == "true";
        Dia = json[0].ContainsKey("Dia") ?  int.Parse(json[0]["Dia"].ToString()) : 0;
        Money = long.Parse(json[0]["Money"].ToString());
        TotalAddMoney = long.Parse(json[0]["TotalAddMoney"].ToString());
        DailyAddMoney = long.Parse(json[0]["DailyAddMoney"].ToString());
        Score = int.Parse(json[0]["Score"].ToString());
        Tip = int.Parse(json[0]["Tip"].ToString());
        TotalCookCount = int.Parse(json[0]["TotalCookCount"].ToString());
        DailyCookCount = int.Parse(json[0]["DailyCookCount"].ToString());
        TotalCumulativeCustomerCount = int.Parse(json[0]["TotalCumulativeCustomerCount"].ToString());
        DailyCumulativeCustomerCount = int.Parse(json[0]["DailyCumulativeCustomerCount"].ToString());
        PromotionCount = int.Parse(json[0]["PromotionCount"].ToString());
        TotalAdvertisingViewCount = int.Parse(json[0]["TotalAdvertisingViewCount"].ToString());
        DailyAdvertisingViewCount = int.Parse(json[0]["DailyAdvertisingViewCount"].ToString());
        TotalCleanCount = int.Parse(json[0]["TotalCleanCount"].ToString());
        DailyCleanCount = int.Parse(json[0]["DailyCleanCount"].ToString());
        TotalVisitSpecialCustomerCount = json[0].ContainsKey("TotalVisitSpecialCustomerCount") ? int.Parse(json[0]["TotalVisitSpecialCustomerCount"].ToString()) : 0;
        TotalExterminationGatecrasherCustomer1Count = json[0].ContainsKey("TotalExterminationGatecrasherCustomer1Count") ? int.Parse(json[0]["TotalExterminationGatecrasherCustomer1Count"].ToString()) : 0;
        TotalExterminationGatecrasherCustomer2Count = json[0].ContainsKey("TotalExterminationGatecrasherCustomer2Count") ? int.Parse(json[0]["TotalExterminationGatecrasherCustomer2Count"].ToString()) : 0;
        TotalUseGachaMachineCount = json[0].ContainsKey("TotalUseGachaMachineCount") ? int.Parse(json[0]["TotalUseGachaMachineCount"].ToString()) : 0;

        UserId = json[0].ContainsKey("UserId") ? json[0]["UserId"].ToString() : string.Empty;
        FirstAccessTime = json[0].ContainsKey("FirstAccessTime") ? json[0]["FirstAccessTime"].ToString() : string.Empty;
        LastAccessTime = json[0].ContainsKey("LastAccessTime") ? json[0]["LastAccessTime"].ToString() : string.Empty;
        LastAttendanceTime = json[0].ContainsKey("LastAttendanceTime") ? json[0]["LastAttendanceTime"].ToString() : string.Empty;
        TotalAttendanceDays = json[0].ContainsKey("TotalAttendanceDays") ? int.Parse(json[0]["TotalAttendanceDays"].ToString()) : 0;

        if (json[0]["GiveStaffList"] != null)
        {
            GiveStaffLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveStaffList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveStaffLevelDic[key] = value;
            }
        }

        if (json[0]["EquipStaffDatas"] != null)
        {
            foreach (JsonData item in json[0]["EquipStaffDatas"])
            {
                EquipStaffDataList.Add(item.ToString());
            }
        }

        if (json[0]["GiveRecipeList"] != null)
        {
            GiveRecipeLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveRecipeList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveRecipeLevelDic[key] = value;
            }
        }

        if (json[0]["RecipeCookCountList"] != null)
        {
            RecipeCookCountDic.Clear();
            foreach (JsonData item in json[0]["RecipeCookCountList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Count"].ToString());
                RecipeCookCountDic[key] = value;
            }
        }

        if (json[0]["GiveGachaItemCountList"] != null)
        {
            GiveGachaItemCountDic.Clear();
            foreach (JsonData item in json[0]["GiveGachaItemCountList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Count"].ToString());
                GiveGachaItemCountDic[key] = value;
            }
        }

        if (json[0]["GiveGachaItemLevelList"] != null)
        {
            GiveGachaItemLevelDic.Clear();
            foreach (JsonData item in json[0]["GiveGachaItemLevelList"])
            {
                string key = item["Id"].ToString();
                int value = int.Parse(item["Level"].ToString());
                GiveGachaItemLevelDic[key] = value;
            }
        }

        if (json[0]["GiveFurnitureList"] != null)
        {
            foreach (JsonData item in json[0]["GiveFurnitureList"])
            {
                GiveFurnitureList.Add(item.ToString());
            }
        }

        if (json[0]["EquipFurnitureList"] != null)
        {
            foreach (JsonData item in json[0]["EquipFurnitureList"])
            {
                EquipFurnitureList.Add(item.ToString());
            }
        }

        if (json[0]["GiveKitchenUtensilList"] != null)
        {
            foreach (JsonData item in json[0]["GiveKitchenUtensilList"])
            {
                GiveKitchenUtensilList.Add(item.ToString());
            }
        }

        if (json[0]["EquipKitchenUtensilList"] != null)
        {
            foreach (JsonData item in json[0]["EquipKitchenUtensilList"])
            {
                EquipKitchenUtensilList.Add(item.ToString());
            }
        }


        if (json[0]["DoneMainChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["DoneMainChallengeList"])
            {
                DoneMainChallengeSet.Add(item.ToString());
            }
        }

        if (json[0]["ClearMainChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["ClearMainChallengeList"])
            {
                ClearMainChallengeSet.Add(item.ToString());
            }
        }

        if (json[0]["DoneAllTimeChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["DoneAllTimeChallengeList"])
            {
                DoneAllTimeChallengeSet.Add(item.ToString());
            }
        }

        if (json[0]["ClearAllTimeChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["ClearAllTimeChallengeList"])
            {
                ClearAllTimeChallengeSet.Add(item.ToString());
            }
        }

        if (json[0]["DoneDailyChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["DoneDailyChallengeList"])
            {
                DoneDailyChallengeSet.Add(item.ToString());
            }
        }

        if (json[0]["ClearDailyChallengeList"] != null)
        {
            foreach (JsonData item in json[0]["ClearDailyChallengeList"])
            {
                ClearDailyChallengeSet.Add(item.ToString());
            }
        }

        if (json[0].ContainsKey("CoinAreaDataList"))
        {
            foreach (JsonData item in json[0]["CoinAreaDataList"])
            {
                // JSON 데이터를 클래스 객체로 변환
                int coinCount = (int)item["CoinCount"];
                long money = (long)item["Money"];
                SaveCoinAreaData data = new SaveCoinAreaData(coinCount, money);

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
