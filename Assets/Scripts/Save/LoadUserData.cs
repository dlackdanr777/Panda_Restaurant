using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;

public class LoadUserData
{
    public bool IsValid { get; private set; }
    /// <summary>IsValid가 false일 때 원인 진단용 메시지(민감정보 없음).</summary>
    public string FailReason { get; private set; }

    public bool IsFirstTutorialClear;
    public bool IsMiniGameTutorialClear;
    public bool IsFeverTutorialClear;
    public bool IsGatecrasher1TutorialClear;
    public bool IsGatecrasher2TutorialClear;
    public bool IsSpecialCustomer1TutorialClear;
    public bool IsSpecialCustomer2TutorialClear;
    public bool IsFurnitureTutorialClear;
    public bool IsRecipeTutorialClear;

    public EStage _unlockStage;
    public int Dia;
    public long Money;
    public long TotalAddMoney;
    public long DailyAddMoney;
    public long WeeklyAddMoney;
    public int Score;
    public int TotalCookCount;
    public int DailyCookCount;
    public int WeeklyCookCount;
    public int TotalCumulativeCustomerCount;
    public int DailyCumulativeCustomerCount;
    public int WeeklyCumulativeCustomerCount;
    public int PromotionCount;
    public int TotalAdvertisingViewCount;
    public int DailyAdvertisingViewCount;
    public int TotalCleanCount;
    public int DailyCleanCount;
    public int WeeklyCleanCount;
    public int TotalVisitSpecialCustomerCount;
    public int TotalExterminationGatecrasherCustomer1Count;
    public int TotalExterminationGatecrasherCustomer2Count;
    public int WeeklyExterminationGatecrasherCustomerCount;
    public int TotalUseGachaMachineCount;

    public string UserId;
    public string FirstAccessTime;
    public string LastAccessTime;
    public string LastAttendanceTime;
    public int TotalAttendanceDays;

    public int SkinToken;



    public Dictionary<string, int> GiveRecipeLevelDic = new Dictionary<string, int>();
    public Dictionary<string, int> RecipeCookCountDic = new Dictionary<string, int>();

    public Dictionary<string, int> GiveGachaItemCountDic = new Dictionary<string, int>();
    public Dictionary<string, int> GiveGachaItemLevelDic = new Dictionary<string, int>();

    public HashSet<string> GiveStaffSkinSet = new HashSet<string>();
    public HashSet<string> DoneMainChallengeSet = new HashSet<string>();
    public HashSet<string> ClearMainChallengeSet = new HashSet<string>();
    public HashSet<string> DoneAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> ClearAllTimeChallengeSet = new HashSet<string>();
    public HashSet<string> DoneDailyChallengeSet = new HashSet<string>();
    public HashSet<string> ClearDailyChallengeSet = new HashSet<string>();
    public HashSet<string> DoneWeeklyChallengeSet = new HashSet<string>();
    public HashSet<string> ClearWeeklyChallengeSet = new HashSet<string>();

    public Dictionary<string, SaveCustomerData> EnabledCustomerDataDic = new Dictionary<string, SaveCustomerData>();
    public HashSet<string> GiveCustomerSkinSet = new HashSet<string>();

    public HashSet<string> NotificationMessageSet = new HashSet<string>();
    public HashSet<string> ClearNotificationMessageSet = new HashSet<string>();

    public Dictionary<string, SaveTimeData> TimeDataDic = new Dictionary<string, SaveTimeData>();


    //#####광고 관련 변수#############

    public int AddCustomerAdCount;
    public int FeverAdCount;
    public int DoubleTipCounterAdCount;
    public int AddCustomerDiaCount;
    public int FeverDiaCount;

    public int DailyAdGoldRewardCount;
    public int DailyAdDiaRewardCount;

    //###############################

    public LoadUserData(JsonData json)
    {
        if (json == null || json.Count == 0)
            return;

        // 필드가 존재하는데 변환에 실패한 경우("손상")만 검증 실패로 취급합니다.
        // 필드 자체가 없는 경우(구버전 호환)는 기존 기본값을 그대로 사용합니다.
        bool hasCorruption = false;
        string firstCorruptKey = null;

        void MarkCorrupt(string key)
        {
            if (!hasCorruption)
                firstCorruptKey = key;
            hasCorruption = true;
        }

        try
        {
            JsonData data = json[0];

            // 안전한 데이터 가져오기 헬퍼 함수 - 필드 누락은 기본값, 필드 존재+변환 실패는 손상으로 기록
            bool GetBool(string key)
            {
                if (!data.ContainsKey(key))
                    return false;
                string s = data[key].ToString();
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                    return false;
                MarkCorrupt(key);
                return false;
            }
            int GetInt(string key)
            {
                if (!data.ContainsKey(key))
                    return 0;
                if (int.TryParse(data[key].ToString(), out int value))
                    return value;
                MarkCorrupt(key);
                return 0;
            }
            long GetLong(string key)
            {
                if (!data.ContainsKey(key))
                    return 0;
                if (long.TryParse(data[key].ToString(), out long value))
                    return value;
                MarkCorrupt(key);
                return 0;
            }
            string GetString(string key) => data.ContainsKey(key) ? data[key].ToString() : string.Empty;

            // 아래 Required 헬퍼는 git 이력상(a6af9c7 최초 커밋) 저장 스키마 도입 시점부터 항상 존재했던
            // 필드에만 사용합니다. 필드 자체가 없으면 구버전 호환이 아니라 데이터 누락(손상)으로 처리합니다.
            bool GetRequiredBool(string key)
            {
                if (!data.ContainsKey(key))
                {
                    MarkCorrupt(key);
                    return false;
                }
                return GetBool(key);
            }
            int GetRequiredInt(string key)
            {
                if (!data.ContainsKey(key))
                {
                    MarkCorrupt(key);
                    return 0;
                }
                return GetInt(key);
            }
            long GetRequiredLong(string key)
            {
                if (!data.ContainsKey(key))
                {
                    MarkCorrupt(key);
                    return 0;
                }
                return GetLong(key);
            }

            IsFirstTutorialClear = GetRequiredBool("IsFirstTutorialClear");
            IsMiniGameTutorialClear = GetRequiredBool("IsMiniGameTutorialClear");
            IsFeverTutorialClear = GetBool("IsFeverTutorialClear");
            IsGatecrasher1TutorialClear = GetRequiredBool("IsGatecrasher1TutorialClear");
            IsGatecrasher2TutorialClear = GetRequiredBool("IsGatecrasher2TutorialClear");
            IsSpecialCustomer1TutorialClear = GetRequiredBool("IsSpecialCustomer1TutorialClear");
            IsSpecialCustomer2TutorialClear = GetRequiredBool("IsSpecialCustomer2TutorialClear");
            IsFurnitureTutorialClear = GetBool("IsFurnitureTutorialClear");
            IsRecipeTutorialClear = GetBool("IsRecipeTutorialClear");

            // Dia는 최초 스키마에 없던(이후 추가된) 확인된 선택 필드 - 누락 시 기본값 0 허용
            Dia = GetInt("Dia");
            Money = GetRequiredLong("Money");
            TotalAddMoney = GetRequiredLong("TotalAddMoney");
            DailyAddMoney = GetRequiredLong("DailyAddMoney");
            Score = GetRequiredInt("Score");
            TotalCookCount = GetRequiredInt("TotalCookCount");
            DailyCookCount = GetRequiredInt("DailyCookCount");
            TotalCumulativeCustomerCount = GetRequiredInt("TotalCumulativeCustomerCount");
            DailyCumulativeCustomerCount = GetRequiredInt("DailyCumulativeCustomerCount");
            PromotionCount = GetRequiredInt("PromotionCount");
            TotalAdvertisingViewCount = GetRequiredInt("TotalAdvertisingViewCount");
            DailyAdvertisingViewCount = GetRequiredInt("DailyAdvertisingViewCount");
            TotalCleanCount = GetRequiredInt("TotalCleanCount");
            DailyCleanCount = GetRequiredInt("DailyCleanCount");
            // 아래부터는 최초 스키마(a6af9c7)에 없던, 이후 버전에서 추가된 것으로 확인된 선택 필드입니다.
            TotalVisitSpecialCustomerCount = GetInt("TotalVisitSpecialCustomerCount");
            TotalExterminationGatecrasherCustomer1Count = GetInt("TotalExterminationGatecrasherCustomer1Count");
            TotalExterminationGatecrasherCustomer2Count = GetInt("TotalExterminationGatecrasherCustomer2Count");
            TotalUseGachaMachineCount = GetInt("TotalUseGachaMachineCount");
            TotalAttendanceDays = GetInt("TotalAttendanceDays");

            WeeklyAddMoney = GetLong("WeeklyAddMoney");
            WeeklyCookCount = GetInt("WeeklyCookCount");
            WeeklyCumulativeCustomerCount = GetInt("WeeklyCumulativeCustomerCount");
            WeeklyCleanCount = GetInt("WeeklyCleanCount");
            WeeklyExterminationGatecrasherCustomerCount = GetInt("WeeklyExterminationGatecrasherCustomerCount");

            UserId = GetString("UserId");
            FirstAccessTime = GetString("FirstAccessTime");
            LastAccessTime = GetString("LastAccessTime");
            LastAttendanceTime = GetString("LastAttendanceTime");

            if (!LoadDictionaryData(data, "GiveRecipeList", GiveRecipeLevelDic, "Id", "Level")) MarkCorrupt("GiveRecipeList");
            if (!LoadDictionaryData(data, "RecipeCookCountList", RecipeCookCountDic, "Id", "Count")) MarkCorrupt("RecipeCookCountList");
            if (!LoadDictionaryData(data, "GiveGachaItemCountList", GiveGachaItemCountDic, "Id", "Count")) MarkCorrupt("GiveGachaItemCountList");
            if (!LoadDictionaryData(data, "GiveGachaItemLevelList", GiveGachaItemLevelDic, "Id", "Level")) MarkCorrupt("GiveGachaItemLevelList");
            if (!LoadDictionaryData(data, "TimeDataList", TimeDataDic, "Id", "Time")) MarkCorrupt("TimeDataList");

            if (!LoadCustomerDataList(data)) MarkCorrupt("EnabledCustomerDataList");
            if (!LoadStringSet(data, "GiveCustomerSkinList", GiveCustomerSkinSet)) MarkCorrupt("GiveCustomerSkinList");
            SkinToken = GetInt("SkinToken");
            if (!LoadTimeData(data)) MarkCorrupt("TimeDataList");
            foreach (var item in TimeDataDic)
            {
                DebugLog.Log($"TimeDataDic: {item.Key} - {item.Value.Time}");
            }

            if (!LoadStringSet(data, "GiveStaffSkinList", GiveStaffSkinSet)) MarkCorrupt("GiveStaffSkinList");
            if (!LoadStringSet(data, "DoneMainChallengeList", DoneMainChallengeSet)) MarkCorrupt("DoneMainChallengeList");
            if (!LoadStringSet(data, "ClearMainChallengeList", ClearMainChallengeSet)) MarkCorrupt("ClearMainChallengeList");
            if (!LoadStringSet(data, "DoneAllTimeChallengeList", DoneAllTimeChallengeSet)) MarkCorrupt("DoneAllTimeChallengeList");
            if (!LoadStringSet(data, "ClearAllTimeChallengeList", ClearAllTimeChallengeSet)) MarkCorrupt("ClearAllTimeChallengeList");
            if (!LoadStringSet(data, "DoneDailyChallengeList", DoneDailyChallengeSet)) MarkCorrupt("DoneDailyChallengeList");
            if (!LoadStringSet(data, "ClearDailyChallengeList", ClearDailyChallengeSet)) MarkCorrupt("ClearDailyChallengeList");
            if (!LoadStringSet(data, "DoneWeeklyChallengeList", DoneWeeklyChallengeSet)) MarkCorrupt("DoneWeeklyChallengeList");
            if (!LoadStringSet(data, "ClearWeeklyChallengeList", ClearWeeklyChallengeSet)) MarkCorrupt("ClearWeeklyChallengeList");

            if (!LoadStringSet(data, "NotificationMessageList", NotificationMessageSet)) MarkCorrupt("NotificationMessageList");
            if (!LoadStringSet(data, "ClearNotificationMessageList", ClearNotificationMessageSet)) MarkCorrupt("ClearNotificationMessageList");

            AddCustomerAdCount = GetInt("AddCustomerAdCount");
            FeverAdCount = GetInt("FeverAdCount");
            DoubleTipCounterAdCount = GetInt("DoubleTipCounterAdCount");
            AddCustomerDiaCount = GetInt("AddCustomerDiaCount");
            FeverDiaCount = GetInt("FeverDiaCount");

            DailyAdGoldRewardCount = GetInt("DailyAdGoldRewardCount");
            DailyAdDiaRewardCount = GetInt("DailyAdDiaRewardCount");

            if (hasCorruption)
            {
                IsValid = false;
                FailReason = $"필드 손상: {firstCorruptKey}";
                DebugLog.LogError($"[LoadUserData] 데이터 손상 감지, 검증 실패 처리: {firstCorruptKey}");
            }
            else
            {
                IsValid = true;
            }
        }
        catch (Exception e)
        {
            IsValid = false;
            FailReason = e.Message;
            DebugLog.LogError($"Failed to load user data: {e.Message}");
        }
    }

    // 문자열 세트를 로드하는 헬퍼 메서드. 필드가 없으면 정상(빈 세트)으로 true, 존재하는데 항목이 손상되면 false를 반환합니다.
    private bool LoadStringSet(JsonData data, string key, HashSet<string> targetSet)
    {
        targetSet.Clear();
        if (!data.ContainsKey(key))
            return true;

        if (!data[key].IsArray)
            return false;

        foreach (JsonData item in data[key])
        {
            try
            {
                targetSet.Add(item.ToString());
            }
            catch (Exception)
            {
                return false;
            }
        }
        return true;
    }

    // 딕셔너리 데이터를 로드하는 헬퍼 메서드. 필드가 없으면 정상(빈 딕셔너리)으로 true, 항목이 손상되면 false를 반환합니다.
    private bool LoadDictionaryData<T>(JsonData data, string key, Dictionary<string, T> targetDict,
        string keyField, string valueField)
    {
        targetDict.Clear();
        if (!data.ContainsKey(key))
            return true;

        if (!data[key].IsArray)
            return false;

        foreach (JsonData item in data[key])
        {
            if (!item.ContainsKey(keyField) || !item.ContainsKey(valueField))
                return false;

            try
            {
                string dictKey = item[keyField].ToString();
                if (typeof(T) == typeof(int))
                {
                    if (!int.TryParse(item[valueField].ToString(), out int value))
                        return false;
                    targetDict[dictKey] = (T)(object)value;
                }
                // 다른 타입이 필요하다면 여기에 추가
            }
            catch (Exception)
            {
                return false;
            }
        }
        return true;
    }

    // 고객 데이터 목록 로드 (특별한 구조를 가진 데이터). 필드가 없으면 정상, 항목이 손상되면 false를 반환합니다.
    private bool LoadCustomerDataList(JsonData data)
    {
        EnabledCustomerDataDic.Clear();
        if (!data.ContainsKey("EnabledCustomerDataList"))
            return true;

        if (!data["EnabledCustomerDataList"].IsArray)
            return false;

        foreach (JsonData item in data["EnabledCustomerDataList"])
        {
            if (!item.ContainsKey("Id") || !item.ContainsKey("VisitCount"))
                return false;

            try
            {
                string id = item["Id"].ToString();
                string skinId = item.ContainsKey("SkinId") ? item["SkinId"].ToString() : string.Empty;
                if (!int.TryParse(item["VisitCount"].ToString(), out int visitCount))
                    return false;
                if (EnabledCustomerDataDic.ContainsKey(id))
                    return false;
                EnabledCustomerDataDic.Add(id, new SaveCustomerData(id, skinId, visitCount));
            }
            catch (Exception)
            {
                return false;
            }
        }
        return true;
    }

    // 타이머 데이터 목록 로드 (특별한 구조를 가진 데이터). 필드가 없으면 정상, 항목이 손상되면 false를 반환합니다.
    private bool LoadTimeData(JsonData data)
    {
        TimeDataDic.Clear();
        if (!data.ContainsKey("TimeDataList"))
        {
            DebugLog.Log("TimeDataList 없음 - 구버전 호환으로 간주");
            return true;
        }

        if (!data["TimeDataList"].IsArray)
            return false;

        foreach (JsonData item in data["TimeDataList"])
        {
            if (!item.ContainsKey("Id") || !item.ContainsKey("Time"))
                return false;

            try
            {
                string id = item["Id"].ToString();
                if (!int.TryParse(item["Time"].ToString(), out int time))
                    return false;
                if (TimeDataDic.ContainsKey(id))
                    return false;
                TimeDataDic.Add(id, new SaveTimeData(id, time));
                DebugLog.Log($"타이머 데이터 로드: {id} - {time}초");
            }
            catch (Exception ex)
            {
                DebugLog.LogError($"타이머 데이터 로드 오류: {ex.Message}");
                return false;
            }
        }
        return true;
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
