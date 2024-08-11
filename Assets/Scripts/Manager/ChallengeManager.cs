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
    public event Action<ChallengeType> OnChallengePercentUpdateHandler;

    private static readonly string _csvFilePath = "Challenge/";

    private static Dictionary<string, ChallengeData> _mainChallengeDataDic = new Dictionary<string, ChallengeData>();
    private static Dictionary<string, ChallengeData> _alltimeChallengeDataDic = new Dictionary<string, ChallengeData>();

    private static Dictionary<string, Type01ChallengeData> _type01ChallengeDataDic = new Dictionary<string, Type01ChallengeData>();
    private static Dictionary<string, Type02ChallengeData> _type02ChallengeDataDic = new Dictionary<string, Type02ChallengeData>();
    private static Dictionary<string, Type03ChallengeData> _type03ChallengeDataDic = new Dictionary<string, Type03ChallengeData>();
    private static Dictionary<string, Type04ChallengeData> _type04ChallengeDataDic = new Dictionary<string, Type04ChallengeData>();
    private static Dictionary<string, Type05ChallengeData> _type05ChallengeDataDic = new Dictionary<string, Type05ChallengeData>();
    private static Dictionary<string, Type06ChallengeData> _type06ChallengeDataDic = new Dictionary<string, Type06ChallengeData>();
    private static Dictionary<string, Type07ChallengeData> _type07ChallengeDataDic = new Dictionary<string, Type07ChallengeData>();
    private static Dictionary<string, Type08ChallengeData> _type08ChallengeDataDic = new Dictionary<string, Type08ChallengeData>();
    private static Dictionary<string, Type09ChallengeData> _type09ChallengeDataDic = new Dictionary<string, Type09ChallengeData>();
    private static Dictionary<string, Type10ChallengeData> _type10ChallengeDataDic = new Dictionary<string, Type10ChallengeData>();
    private static Dictionary<string, Type11ChallengeData> _type11ChallengeDataDic = new Dictionary<string, Type11ChallengeData>();
    private static Dictionary<string, Type12ChallengeData> _type12ChallengeDataDic = new Dictionary<string, Type12ChallengeData>();



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
        SetChallengeDatas();

        UserInfo.OnGiveFurnitureHandler += Type01ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type02ChallengeCheck;
        UserInfo.OnGiveRecipeHandler += Type03ChallengeCheck;
        UserInfo.OnGiveRecipeHandler += Type04ChallengeCheck;
        UserInfo.OnGiveStaffHandler += Type05ChallengeCheck;
        UserInfo.OnGiveRecipeHandler += Type06ChallengeCheck;
        UserInfo.OnAddCookCountHandler += Type07ChallengeCheck;
        UserInfo.OnChangeScoreHandler += Type08ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler +=  Type08ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type08ChallengeCheck;
        UserInfo.OnGiveStaffHandler += Type08ChallengeCheck;
        UserInfo.OnAddCustomerCountHandler += Type09ChallengeCheck;
        UserInfo.OnVisitedCustomerHandler += Type10ChallengeCheck;
        UserInfo.OnChangeMoneyHandler += Type11ChallengeCheck;
        UserInfo.OnAddPromotionCountHandler += Type12ChallengeCheck;


        Type01ChallengeCheck();
        Type02ChallengeCheck();
        Type03ChallengeCheck();
        Type04ChallengeCheck();
        Type05ChallengeCheck();
        Type06ChallengeCheck();
        Type07ChallengeCheck();
        Type08ChallengeCheck();
        Type09ChallengeCheck();
        Type10ChallengeCheck();
        Type11ChallengeCheck();
        Type12ChallengeCheck();
    }


    private void SetChallengeDatas()
    {
        _mainChallengeDataDic.Clear();
        _mainChallengeDataDic = SetData("REWARD_MAIN");

        _alltimeChallengeDataDic.Clear();
        _alltimeChallengeDataDic = SetData("REWARD_ALLTIME");
    }


    private Dictionary<string, ChallengeData> SetData(string csvFileName)
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath + csvFileName);
        Dictionary<string, ChallengeData> dic = new Dictionary<string, ChallengeData>();
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = data[i].Split(new char[] { ',' });
            string id = string.Concat(row[0].Where(c => !Char.IsWhiteSpace(c)));

            if (string.IsNullOrWhiteSpace(id))
                continue;

            string type = string.Concat(row[1].Where(c => !Char.IsWhiteSpace(c)));
            string shortCutType = string.Concat(row[2].Where(c => !Char.IsWhiteSpace(c)));
            string description = row[3];
            string needItemId = string.Concat(row[4].Where(c => !Char.IsWhiteSpace(c)));
            int count = Convert.ToInt32(string.Concat(row[5].Where(c => !Char.IsWhiteSpace(c))));
            MoneyType moneyType = row[6] == "게임 코인" ? MoneyType.Gold : MoneyType.Dia;
            int rewardMoney = Convert.ToInt32(string.Concat(row[7].Where(c => !Char.IsWhiteSpace(c))));
            ChallengeType challengeType;
            switch (type)
            {
                case "TYPE01":
                    challengeType = ChallengeType.TYPE01;
                    string[] needFurnitureIds = needItemId.Split(new char[] { '.' });
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(challengeType, id, description, needFurnitureIds, moneyType, rewardMoney);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    dic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    challengeType = ChallengeType.TYPE02;
                    string[] needKitchenUtensilIds = needItemId.Split(new char[] { '.' });
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(challengeType, id, description, needKitchenUtensilIds, moneyType, rewardMoney);
                    _type02ChallengeDataDic.Add(id, challengeData02); 
                    dic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    challengeType = ChallengeType.TYPE03;
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    dic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    challengeType = ChallengeType.TYPE04;
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    dic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    challengeType = ChallengeType.TYPE05;
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    dic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    challengeType = ChallengeType.TYPE06;
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    dic.Add(id, challengeData06);
                    break;

                case "TYPE07":
                    challengeType = ChallengeType.TYPE07;
                    Type07ChallengeData challengeData07 = new Type07ChallengeData(challengeType, id, description, needItemId, count, moneyType, rewardMoney);
                    _type07ChallengeDataDic.Add(id, challengeData07);
                    dic.Add(id, challengeData07);
                    break;

                case "TYPE08":
                    challengeType = ChallengeType.TYPE08;
                    Type08ChallengeData challengeData08 = new Type08ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type08ChallengeDataDic.Add(id, challengeData08);
                    dic.Add(id, challengeData08);
                    break;

                case "TYPE09":
                    challengeType = ChallengeType.TYPE09;
                    Type09ChallengeData challengeData09 = new Type09ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type09ChallengeDataDic.Add(id, challengeData09);
                    dic.Add(id, challengeData09);
                    break;

                case "TYPE10":
                    challengeType = ChallengeType.TYPE10;
                    Type10ChallengeData challengeData10 = new Type10ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type10ChallengeDataDic.Add(id, challengeData10);
                    dic.Add(id, challengeData10);
                    break;

                case "TYPE11":
                    challengeType = ChallengeType.TYPE11;
                    Type11ChallengeData challengeData11 = new Type11ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type11ChallengeDataDic.Add(id, challengeData11);
                    dic.Add(id, challengeData11);
                    break;

                case "TYPE12":
                    challengeType = ChallengeType.TYPE12;
                    Type12ChallengeData challengeData12 = new Type12ChallengeData(challengeType, id, description, count, moneyType, rewardMoney);
                    _type12ChallengeDataDic.Add(id, challengeData12);
                    dic.Add(id, challengeData12);
                    break;

            }
        }

        return dic;
    }


    public List<ChallengeData> GetMainChallenge()
    {
        return _mainChallengeDataDic.Values.ToList();
    }

    public List<ChallengeData> GetAllTimeChallenge()
    {
        return _alltimeChallengeDataDic.Values.ToList();
    }

    public float GetChallengePercent(ChallengeData data)
    {
        float percent = 0;
        switch (data.Type)
        {
            case ChallengeType.TYPE01:
                Type01ChallengeData data01 = (Type01ChallengeData)data;
                for (int i = 0, cnt = data01.NeedFurnitureIds.Length; i < cnt; i++)
                {
                    if (UserInfo.IsGiveKitchenUtensil(data01.NeedFurnitureIds[i]))
                        percent += 1f / cnt;
                }
                return percent;

            case ChallengeType.TYPE02:
                Type02ChallengeData data02 = (Type02ChallengeData)data;
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
                return recipeCount == 0 ? 0 : Math.Min(1, (float)recipeCount / data06.RecipeCount);

            case ChallengeType.TYPE07:
                Type07ChallengeData data07 = (Type07ChallengeData)data;
                int cookCount = UserInfo.GetCookCount(data07.Id);
                return cookCount == 0 ? 0 : Math.Min(1, (float)cookCount / data07.CookCount);

            case ChallengeType.TYPE08:
                Type08ChallengeData data08 = (Type08ChallengeData)data;
                int socre = UserInfo.MaxScore;
                return socre == 0 ? 0 : Math.Min(1, (float)socre / data08.Rank);

            case ChallengeType.TYPE09:
                Type09ChallengeData data09 = (Type09ChallengeData)data;
                int customerCount = UserInfo.CumulativeCustomerCount;
                return customerCount == 0 ? 0 : Math.Min(1, (float)customerCount / data09.CustomerCount);

            case ChallengeType.TYPE10:
                Type10ChallengeData data10 = (Type10ChallengeData)data;
                int visitedCustomerCount = UserInfo.GetVisitedCustomerCount();
                return visitedCustomerCount == 0 ? 0 : Math.Min(1, (float)visitedCustomerCount / data10.CustomerCount);

            case ChallengeType.TYPE11:
                Type11ChallengeData data11 = (Type11ChallengeData)data;
                int moneyCount = UserInfo.MaxMoney;
                return moneyCount == 0 ? 0 : Math.Min(1, (float)moneyCount / data11.MoneyCount);

            case ChallengeType.TYPE12:
                Type12ChallengeData data12 = (Type12ChallengeData)data;
                int promotionCount = UserInfo.PromotionCount;
                return promotionCount == 0 ? 0 : Math.Min(1, (float)promotionCount / data12.PromotionCount);

            default: return 0;
        }
    }


    public void ChallengeClear(string id)
    {
        if (!UserInfo.GetIsDoneChallenge(id))
        {
            DebugLog.LogError("해당 도전과제는 완료가 안된 상태로 클리어 요청을 했습니다: " + id);
            return;
        }

        if (_mainChallengeDataDic.ContainsKey(id))
            UserInfo.ClearMainChallenge(id);

        else if (_alltimeChallengeDataDic.ContainsKey(id))
            UserInfo.ClearAllTimeChallenge(id);

        OnChallengeUpdateHandler?.Invoke();
    }


    private void Type01ChallengeCheck()
    {
        int count = 0;
        foreach (Type01ChallengeData data in _type01ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            bool _isGives = true;
            for (int i = 0, cnt = data.NeedFurnitureIds.Length; i < cnt; i++)
            {
                if (!UserInfo.IsGiveFurniture(data.NeedFurnitureIds[i]))
                {
                    _isGives = false;
                    break;
                }
            }

            if (!_isGives) continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE01);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type02ChallengeCheck()
    {
        int count = 0;
        foreach (Type02ChallengeData data in _type02ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
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

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE02);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type03ChallengeCheck()
    {
        int count = 0;
        foreach (Type03ChallengeData data in _type03ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.BuyRecipeId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE03);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type04ChallengeCheck()
    {
        int count = 0;
        foreach (Type04ChallengeData data in _type04ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.NeedRecipeId))
                continue;

            if(_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE04);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type05ChallengeCheck()
    {
        int count = 0;
        foreach (Type05ChallengeData data in _type05ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveStaff(data.NeedStaffId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE05);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type06ChallengeCheck()
    {
        int count = 0;
        foreach (Type06ChallengeData data in _type06ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetRecipeCount() < data.RecipeCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE06);
        if (0 < count)
         OnChallengeUpdateHandler?.Invoke();
    }

    private void Type07ChallengeCheck()
    {
        int count = 0;
        foreach (Type07ChallengeData data in _type07ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetCookCount(data.RecipeId) < data.CookCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE07);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type08ChallengeCheck()
    {
        int count = 0;
        foreach (Type08ChallengeData data in _type08ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.MaxScore < data.Rank)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE08);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type09ChallengeCheck()
    {
        int count = 0;
        foreach (Type09ChallengeData data in _type09ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.CumulativeCustomerCount < data.CustomerCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if(_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE09);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type10ChallengeCheck()
    {
        int count = 0;
        foreach (Type10ChallengeData data in _type10ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetVisitedCustomerCount() < data.CustomerCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE10);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type11ChallengeCheck()
    {
        int count = 0;
        foreach (Type11ChallengeData data in _type11ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.MaxMoney < data.MoneyCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE11);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type12ChallengeCheck()
    {
        int count = 0;
        foreach (Type12ChallengeData data in _type12ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.PromotionCount < data.PromotionCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE12);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }
}
