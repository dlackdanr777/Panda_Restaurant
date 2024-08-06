using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly string _csvFilePath = "Challenge/";

    private static Dictionary<string, ChallengeData> _mainChallengeDataDic = new Dictionary<string, ChallengeData>();



    private static Dictionary<string, Type01ChallengeData> _type01ChallengeDataDic = new Dictionary<string, Type01ChallengeData>();
    private static Dictionary<string, Type02ChallengeData> _type02ChallengeDataDic = new Dictionary<string, Type02ChallengeData>();
    private static Dictionary<string, Type03ChallengeData> _type03ChallengeDataDic = new Dictionary<string, Type03ChallengeData>();
    private static Dictionary<string, Type04ChallengeData> _type04ChallengeDataDic = new Dictionary<string, Type04ChallengeData>();
    private static Dictionary<string, Type05ChallengeData> _type05ChallengeDataDic = new Dictionary<string, Type05ChallengeData>();
    private static Dictionary<string, Type06ChallengeData> _type06ChallengeDataDic = new Dictionary<string, Type06ChallengeData>();



    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Init()
    {
        MainChallengeInit();

        UserInfo.OnGiveFurnitureHandler += Type01ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type02ChallengeCheck;
        UserInfo.OnGiveRecipeHandler += Type03ChallengeCheck;
        UserInfo.OnGiveStaffHandler += Type05ChallengeCheck;

        Type01ChallengeCheck();
        Type02ChallengeCheck();
        Type03ChallengeCheck();
        Type04ChallengeCheck();
        Type05ChallengeCheck();
    }


    private void MainChallengeInit()
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath + "REWARD_MAIN");
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = row[0];
            string type = row[1];
            string shortCutType = row[2];
            string description = row[3];
            int rewardMoney = Convert.ToInt32(row[6]);
            switch (type)
            {
                case "TYPE01":
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(id, description, row[4], rewardMoney);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    _mainChallengeDataDic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    string row4 = string.Concat(row[4].Where(c => !Char.IsWhiteSpace(c)));
                    string[] needKitcenUtensilIds = row4.Split(new char[] { '.' });
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(id, description, needKitcenUtensilIds, rewardMoney);
                    _type02ChallengeDataDic.Add(id, challengeData02);
                    _mainChallengeDataDic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(id, description, row[4], rewardMoney);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    _mainChallengeDataDic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(id, description, row[4], int.Parse(row[5]), rewardMoney);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    _mainChallengeDataDic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(id, description, row[4], rewardMoney);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    _mainChallengeDataDic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(id, description, row[4], rewardMoney);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    _mainChallengeDataDic.Add(id, challengeData06);
                    break;
            }
        }
    }

    private void Type01ChallengeCheck()
    {
        foreach (Type01ChallengeData data in _type01ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveFurniture(data.NeedFurnitureId))
                continue;

            UserInfo.DoneMainChallenge(data.Id);
        }
    }

    private void Type02ChallengeCheck()
    {
        foreach (Type02ChallengeData data in _type02ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            for (int i = 0, cnt = data.NeedKitchenUtensilId.Length; i < cnt; i++)
            {
                if (!UserInfo.IsGiveKitchenUtensil(data.NeedKitchenUtensilId[i]))
                    continue;
            }

            UserInfo.DoneMainChallenge(data.Id);
        }
    }

    private void Type03ChallengeCheck()
    {
        foreach (Type03ChallengeData data in _type03ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.BuyRecipeId))
                continue;

            UserInfo.DoneMainChallenge(data.Id);
        }
    }


    //TODO: 수정 예정 레시피 조리 Count값이 아직 설정안됨
    private void Type04ChallengeCheck()
    {
        foreach (Type04ChallengeData data in _type04ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.RecipeId))
                continue;

            UserInfo.DoneMainChallenge(data.Id);
        }
    }


    private void Type05ChallengeCheck()
    {
        foreach (Type05ChallengeData data in _type05ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveStaff(data.NeedStaffId))
                continue;

            UserInfo.DoneMainChallenge(data.Id);
        }
    }

}
