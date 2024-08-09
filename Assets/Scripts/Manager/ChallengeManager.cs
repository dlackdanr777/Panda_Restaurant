using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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

    public event Action OnChallengeUpdateHandler;

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
        UserInfo.OnGiveRecipeHandler += Type04ChallengeCheck;
        UserInfo.OnGiveStaffHandler += Type05ChallengeCheck;
        UserInfo.OnGiveRecipeHandler += Type06ChallengeCheck;

        Type01ChallengeCheck();
        Type02ChallengeCheck();
        Type03ChallengeCheck();
        Type04ChallengeCheck();
        Type05ChallengeCheck();
        Type06ChallengeCheck();
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
            ChallengeType challengeType;
            switch (type)
            {
                case "TYPE01":
                    challengeType = ChallengeType.TYPE01;
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(challengeType, id, description, row[4], rewardMoney);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    _mainChallengeDataDic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    challengeType = ChallengeType.TYPE02;
                    string row4 = string.Concat(row[4].Where(c => !Char.IsWhiteSpace(c)));
                    string[] needKitcenUtensilIds = row4.Split(new char[] { '.' });
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(challengeType, id, description, needKitcenUtensilIds, rewardMoney);
                    _type02ChallengeDataDic.Add(id, challengeData02);
                    _mainChallengeDataDic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    challengeType = ChallengeType.TYPE03;
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(challengeType, id, description, row[4], rewardMoney);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    _mainChallengeDataDic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    challengeType = ChallengeType.TYPE04;
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(challengeType, id, description, row[4], rewardMoney);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    _mainChallengeDataDic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    challengeType = ChallengeType.TYPE05;
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(challengeType, id, description, row[4], rewardMoney);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    _mainChallengeDataDic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    challengeType = ChallengeType.TYPE06;
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(challengeType, id, description, int.Parse(row[5]), rewardMoney);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    _mainChallengeDataDic.Add(id, challengeData06);
                    break;
            }
        }
    }

    public List<ChallengeData> GetMainChallenge()
    {
        return _mainChallengeDataDic.Values.ToList();
    }

    public float GetChallengePercent(ChallengeData data)
    {
        switch (data.Type)
        {
            case ChallengeType.TYPE01:
                Type01ChallengeData data01 = (Type01ChallengeData)data;
                if (UserInfo.IsGiveFurniture(data01.NeedFurnitureId))
                    return 1;
                return 0;

            case ChallengeType.TYPE02:
                Type02ChallengeData data02 = (Type02ChallengeData)data;

                float percent = 0;
                for(int i = 0, cnt = data02.NeedKitchenUtensilId.Length; i < cnt; i++)
                {
                    if (UserInfo.IsGiveKitchenUtensil(data02.NeedKitchenUtensilId[i]))
                        percent += 1f / cnt;
                }
                return percent;

            case ChallengeType.TYPE03:
                Type03ChallengeData data03 = (Type03ChallengeData)data;
                if (UserInfo.IsGiveRecipe(data03.BuyRecipeId))
                    return 1;
                return 0;

            case ChallengeType.TYPE04:
                Type04ChallengeData data04 = (Type04ChallengeData)data;
                if (UserInfo.IsGiveRecipe(data04.NeedRecipeId))
                    return 1;
                return 0;

            case ChallengeType.TYPE05:
                Type05ChallengeData data05 = (Type05ChallengeData)data;
                if (UserInfo.IsGiveStaff(data05.NeedStaffId))
                    return 1;
                return 0;

            case ChallengeType.TYPE06:
                Type06ChallengeData data06 = (Type06ChallengeData)data;
                int recipeCount = UserInfo.GetRecipeCount();
                return recipeCount == 0 ? 0 : recipeCount <= data06.RecipeCount ? 1 : (float)recipeCount / data06.RecipeCount; 


            default: return 0;
        }
    }


    public void ChallengeClear(string id)
    {
        if (_mainChallengeDataDic.ContainsKey(id))
            UserInfo.ClearMainChallenge(id);

        OnChallengeUpdateHandler?.Invoke();
    }


    private void Type01ChallengeCheck()
    {
        int count = 0;
        foreach (Type01ChallengeData data in _type01ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveFurniture(data.NeedFurnitureId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type02ChallengeCheck()
    {
        int count = 0;
        foreach (Type02ChallengeData data in _type02ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            bool _isGives = true;
            for (int i = 0, cnt = data.NeedKitchenUtensilId.Length; i < cnt; i++)
            {
                if (!UserInfo.IsGiveKitchenUtensil(data.NeedKitchenUtensilId[i]))
                {
                    _isGives = false;
                    break;
                }
            }

            if (!_isGives) continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type03ChallengeCheck()
    {
        int count = 0;
        foreach (Type03ChallengeData data in _type03ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.BuyRecipeId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type04ChallengeCheck()
    {
        int count = 0;
        foreach (Type04ChallengeData data in _type04ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.NeedRecipeId))
                continue;

            if(_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type05ChallengeCheck()
    {
        int count = 0;
        foreach (Type05ChallengeData data in _type05ChallengeDataDic.Values)
        {
            DebugLog.Log(data.Id);
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveStaff(data.NeedStaffId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type06ChallengeCheck()
    {
        int count = 0;
        foreach (Type06ChallengeData data in _type06ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneMainChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearMainChallenge(data.Id))
                continue;

            if (data.RecipeCount < UserInfo.GetRecipeCount())
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            count++;
        }

        if(0 < count)
         OnChallengeUpdateHandler?.Invoke();
    }

}
