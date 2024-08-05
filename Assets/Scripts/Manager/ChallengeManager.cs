using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChallengeManager : MonoBehaviour
{
    public static ChallengeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("ChallengeManager");
                _instance = obj.AddComponent<ChallengeManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static ChallengeManager _instance;
    private static readonly string _csvFilePath = "Data/REWARD/MAIN";


    private static Dictionary<string, Type01ChallengeData> _type01ChallengeDataDic = new Dictionary<string, Type01ChallengeData>();
    private static Dictionary<string, Type02ChallengeData> _type02ChallengeDataDic = new Dictionary<string, Type02ChallengeData>();
    private static Dictionary<string, Type03ChallengeData> _type03ChallengeDataDic = new Dictionary<string, Type03ChallengeData>();
    private static Dictionary<string, Type04ChallengeData> _type04ChallengeDataDic = new Dictionary<string, Type04ChallengeData>();
    private static Dictionary<string, Type05ChallengeData> _type05ChallengeDataDic = new Dictionary<string, Type05ChallengeData>();


    public void Init()
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath);
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string type = row[1];
            string id = row[2];
            string description = row[3];
            int rewardMoney = Convert.ToInt32(row[6]);
            switch (type)
            {
                case "TYPE01":
                    string needFurnitureId = row[4];
                    Type01ChallengeData challengeData = new Type01ChallengeData(id, description, needFurnitureId, rewardMoney);
                    _type01ChallengeDataDic.Add(id, challengeData);
                break;
            }


        }

    }

}
