

using BackEnd;
using LitJson;
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
    public int Money;
    public int TotalAddMoney;
    public int DailyAddMoney;
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
    public string FirstAccessTime;

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

    public LoadData(JsonData json)
    {
        IsFirstTutorialClear = json[0]["IsFirstTutorialClear"].ToString().ToLower() == "true";
        IsMiniGameTutorialClear = json[0]["IsMiniGameTutorialClear"].ToString().ToLower() == "true";
        IsGatecrasher1TutorialClear = json[0]["IsGatecrasher1TutorialClear"].ToString().ToLower() == "true";
        IsGatecrasher2TutorialClear = json[0]["IsGatecrasher2TutorialClear"].ToString().ToLower() == "true";
        IsSpecialCustomer1TutorialClear = json[0]["IsSpecialCustomer1TutorialClear"].ToString().ToLower() == "true";
        IsSpecialCustomer2TutorialClear = json[0]["IsSpecialCustomer2TutorialClear"].ToString().ToLower() == "true";
        Dia = json[0].ContainsKey("Dia") ?  int.Parse(json[0]["Dia"].ToString()) : 0;
        Money = int.Parse(json[0]["Money"].ToString());
        TotalAddMoney = int.Parse(json[0]["TotalAddMoney"].ToString());
        DailyAddMoney = int.Parse(json[0]["DailyAddMoney"].ToString());
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
        FirstAccessTime = json[0]["FirstAccessTime"].ToString();

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
