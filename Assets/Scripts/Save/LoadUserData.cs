using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;

public class LoadUserData
{
    public bool IsFirstTutorialClear;
    public bool IsMiniGameTutorialClear;
    public bool IsGatecrasher1TutorialClear;
    public bool IsGatecrasher2TutorialClear;
    public bool IsSpecialCustomer1TutorialClear;
    public bool IsSpecialCustomer2TutorialClear;

    public EStage _unlockStage;
    public int Dia;
    public long Money;
    public long TotalAddMoney;
    public long DailyAddMoney;
    public int Score;
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

    public Dictionary<string, int> GiveRecipeLevelDic = new Dictionary<string, int>();
    public Dictionary<string, int> RecipeCookCountDic = new Dictionary<string, int>();

    public Dictionary<string, int> GiveGachaItemCountDic = new Dictionary<string, int>();
    public Dictionary<string, int> GiveGachaItemLevelDic = new Dictionary<string, int>();

    public HashSet<string> DoneMainChallengeSet = new HashSet<string>();
    public HashSet<string> ClearMainChallengeSet = new HashSet<string>();
    public HashSet<string> DoneAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> ClearAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> DoneDailyChallengeSet = new HashSet<string>();
    public HashSet<string> ClearDailyChallengeSet = new HashSet<string>();

    public Dictionary<string, SaveCustomerData> EnabledCustomerDataDic = new Dictionary<string, SaveCustomerData>();

    public HashSet<string> NotificationMessageSet = new HashSet<string>();

    public Dictionary<string, SaveTimeData> TimeDataDic = new Dictionary<string, SaveTimeData>();

    public LoadUserData(JsonData json)
    {
        if (json == null || json.Count == 0)
            return;

        try
        {
            JsonData data = json[0];

            // 안전한 데이터 가져오기 헬퍼 함수
            bool GetBool(string key) => data.ContainsKey(key) && data[key].ToString().ToLower() == "true";
            int GetInt(string key) => data.ContainsKey(key) && int.TryParse(data[key].ToString(), out int value) ? value : 0;
            long GetLong(string key) => data.ContainsKey(key) && long.TryParse(data[key].ToString(), out long value) ? value : 0;
            string GetString(string key) => data.ContainsKey(key) ? data[key].ToString() : string.Empty;

            IsFirstTutorialClear = GetBool("IsFirstTutorialClear");
            IsMiniGameTutorialClear = GetBool("IsMiniGameTutorialClear");
            IsGatecrasher1TutorialClear = GetBool("IsGatecrasher1TutorialClear");
            IsGatecrasher2TutorialClear = GetBool("IsGatecrasher2TutorialClear");
            IsSpecialCustomer1TutorialClear = GetBool("IsSpecialCustomer1TutorialClear");
            IsSpecialCustomer2TutorialClear = GetBool("IsSpecialCustomer2TutorialClear");

            Dia = GetInt("Dia");
            Money = GetLong("Money");
            TotalAddMoney = GetLong("TotalAddMoney");
            DailyAddMoney = GetLong("DailyAddMoney");
            Score = GetInt("Score");
            TotalCookCount = GetInt("TotalCookCount");
            DailyCookCount = GetInt("DailyCookCount");
            TotalCumulativeCustomerCount = GetInt("TotalCumulativeCustomerCount");
            DailyCumulativeCustomerCount = GetInt("DailyCumulativeCustomerCount");
            PromotionCount = GetInt("PromotionCount");
            TotalAdvertisingViewCount = GetInt("TotalAdvertisingViewCount");
            DailyAdvertisingViewCount = GetInt("DailyAdvertisingViewCount");
            TotalCleanCount = GetInt("TotalCleanCount");
            DailyCleanCount = GetInt("DailyCleanCount");
            TotalVisitSpecialCustomerCount = GetInt("TotalVisitSpecialCustomerCount");
            TotalExterminationGatecrasherCustomer1Count = GetInt("TotalExterminationGatecrasherCustomer1Count");
            TotalExterminationGatecrasherCustomer2Count = GetInt("TotalExterminationGatecrasherCustomer2Count");
            TotalUseGachaMachineCount = GetInt("TotalUseGachaMachineCount");
            TotalAttendanceDays = GetInt("TotalAttendanceDays");

            UserId = GetString("UserId");
            FirstAccessTime = GetString("FirstAccessTime");
            LastAccessTime = GetString("LastAccessTime");
            LastAttendanceTime = GetString("LastAttendanceTime");

            LoadDictionaryData(data, "GiveRecipeList", GiveRecipeLevelDic, "Id", "Level");
            LoadDictionaryData(data, "RecipeCookCountList", RecipeCookCountDic, "Id", "Count");
            LoadDictionaryData(data, "GiveGachaItemCountList", GiveGachaItemCountDic, "Id", "Count");
            LoadDictionaryData(data, "GiveGachaItemLevelList", GiveGachaItemLevelDic, "Id", "Level");
            LoadDictionaryData(data, "TimeDataList", TimeDataDic, "Id", "Time");

            LoadCustomerDataList(data);
            LoadTimeData(data);
            foreach (var item in TimeDataDic)
            {
                DebugLog.Log($"TimeDataDic: {item.Key} - {item.Value.Time}");
            }


            LoadStringSet(data, "DoneMainChallengeList", DoneMainChallengeSet);
            LoadStringSet(data, "ClearMainChallengeList", ClearMainChallengeSet);
            LoadStringSet(data, "DoneAllTimeChallengeList", DoneAllTimeChallengeSet);
            LoadStringSet(data, "ClearAllTimeChallengeList", ClearAllTimeChallengeSet);
            LoadStringSet(data, "DoneDailyChallengeList", DoneDailyChallengeSet);
            LoadStringSet(data, "ClearDailyChallengeList", ClearDailyChallengeSet);
            LoadStringSet(data, "NotificationMessageList", NotificationMessageSet);
        }
        catch (Exception e)
        {
            DebugLog.LogError($"Failed to load user data: {e.Message}");
        }
    }

    // 문자열 세트를 로드하는 헬퍼 메서드
    private void LoadStringSet(JsonData data, string key, HashSet<string> targetSet)
    {
        targetSet.Clear();
        if (data.ContainsKey(key) && data[key].IsArray)
        {
            foreach (JsonData item in data[key])
            {
                try
                {
                    targetSet.Add(item.ToString());
                }
                catch (Exception) { /* 오류 항목 스킵 */ }
            }
        }
    }

    // 딕셔너리 데이터를 로드하는 헬퍼 메서드
    private void LoadDictionaryData<T>(JsonData data, string key, Dictionary<string, T> targetDict,
        string keyField, string valueField)
    {
        targetDict.Clear();
        if (data.ContainsKey(key) && data[key].IsArray)
        {
            foreach (JsonData item in data[key])
            {
                try
                {
                    if (item.ContainsKey(keyField) && item.ContainsKey(valueField))
                    {
                        string dictKey = item[keyField].ToString();
                        if (typeof(T) == typeof(int))
                        {
                            int value;
                            if (int.TryParse(item[valueField].ToString(), out value))
                                targetDict[dictKey] = (T)(object)value;
                        }
                        // 다른 타입이 필요하다면 여기에 추가
                    }
                }
                catch (Exception) { /* 오류 항목 스킵 */ }
            }
        }
    }

    // 고객 데이터 목록 로드 (특별한 구조를 가진 데이터)
    private void LoadCustomerDataList(JsonData data)
    {
        EnabledCustomerDataDic.Clear();
        if (data.ContainsKey("EnabledCustomerDataList") && data["EnabledCustomerDataList"].IsArray)
        {
            foreach (JsonData item in data["EnabledCustomerDataList"])
            {
                try
                {
                    if (item.ContainsKey("Id") && item.ContainsKey("VisitCount"))
                    {
                        string id = item["Id"].ToString();
                        int visitCount;
                        if (int.TryParse(item["VisitCount"].ToString(), out visitCount))
                            EnabledCustomerDataDic.Add(id, new SaveCustomerData(id, visitCount));
                    }
                }
                catch (Exception) { /* 오류 항목 스킵 */ }
            }
        }
    }

    // 타이머 데이터 목록 로드 (특별한 구조를 가진 데이터)
    private void LoadTimeData(JsonData data)
    {
        TimeDataDic.Clear();
        if (data.ContainsKey("TimeDataList") && data["TimeDataList"].IsArray)
        {
            foreach (JsonData item in data["TimeDataList"])
            {
                try
                {
                    if (item.ContainsKey("Id") && item.ContainsKey("Time"))
                    {
                        string id = item["Id"].ToString();
                        int time;
                        if (int.TryParse(item["Time"].ToString(), out time))
                        {
                            TimeDataDic.Add(id, new SaveTimeData(id, time));
                            DebugLog.Log($"타이머 데이터 로드: {id} - {time}초");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog.LogError($"타이머 데이터 로드 오류: {ex.Message}");
                }
            }
        }
        else
        {
            DebugLog.Log("TimeDataList 없음 또는 배열 형식이 아님");
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

[Serializable]
public class CoinAreaData
{
    private int _coinCount;
    public int CoinCount => _coinCount;

    private long _money;
    public long Money => _money;

    public CoinAreaData()
    {
    }

    public void SetCoinCount(int count)
    {
        _coinCount = count;
    }

    public void SetMoney(long money)
    {
        _money = money;
    }

    public void AddMoney(long money)
    {
        _money += money;
    }
}

[Serializable]
public class GarbageAreaData
{
    private int _count;
    public int Count => _count;

    public GarbageAreaData()
    {
    }

    public void SetCount(int count)
    {
        _count = count;
    }
}
