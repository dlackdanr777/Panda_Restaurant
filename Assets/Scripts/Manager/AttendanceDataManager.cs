using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class AttendanceData
{
    private MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    private int _rewardValue;
    public int RewardValue => _rewardValue;


    public AttendanceData(MoneyType moneyType, int rwardValue)
    {
        _moneyType = moneyType;
        _rewardValue = rwardValue;
    }
}

public class AttendanceDataManager : MonoBehaviour
{

    private static readonly string _csvFilePath = "Attendance/";

    public static AttendanceDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("AttendanceDataManager");
                _instance = obj.AddComponent<AttendanceDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static AttendanceDataManager _instance;


    Dictionary<int, AttendanceData> _attendanceDic = new Dictionary<int, AttendanceData>();


    public Dictionary<int, AttendanceData> GetRewardDic()
    {
        return _attendanceDic;
    }


    public List<AttendanceData> GetRewardDataList(int startDay)
    {
        List<AttendanceData> list = new List<AttendanceData>();
        int baseStartDay = ((startDay - 1) / 7) * 7 + 1;

        for (int i = baseStartDay; i < baseStartDay + 7; i++)
        {
            if (_attendanceDic.ContainsKey(i))
            {
                list.Add(_attendanceDic[i]);
            }
        }

        return list;
    }




    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(_instance.gameObject);
        _attendanceDic = SetData("AttendanceREWARD");
    }



    private Dictionary<int, AttendanceData> SetData(string csvFileName)
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath + csvFileName);
        Dictionary<int, AttendanceData> dic = new Dictionary<int, AttendanceData>();

        if (csvData == null)
        {
            Debug.LogError($"[AttendanceDataManager] CSV not found: {csvFileName}");
            return dic;
        }

        string[] data = csvData.text.Split(new char[] { '\n' });

        int dayCounter = 0;
        for (int i = 1, cnt = data.Length; i < cnt; ++i)
        {
            if (string.IsNullOrWhiteSpace(data[i]))
                continue;

            string[] row = data[i].Split(new char[] { ',' });

            if (row.Length < 3)
            {
                Debug.LogWarning($"[AttendanceDataManager] invalid row at line {i}: column count {row.Length} < 3");
                continue;
            }

            dayCounter++;

            string rewardType = string.Concat(row[1].Where(c => !Char.IsWhiteSpace(c)));
            // ??? ?? ??? ? ?? ?? ?? ??
            MoneyType moneyType = (rewardType == "ÄÚÀÎ" || rewardType == "??" ||
                                   rewardType == "????" || rewardType == "Gold")
                ? MoneyType.Gold
                : MoneyType.Dia;

            string rewardStr = string.Concat(row[2].Where(c => !Char.IsWhiteSpace(c)));
            if (!int.TryParse(rewardStr, out int rewardValue))
            {
                Debug.LogWarning($"[AttendanceDataManager] invalid reward at line {i}: {row[2]}");
                continue;
            }

            dic[dayCounter] = new AttendanceData(moneyType, rewardValue);
        }

        return dic;
    }
}
