using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Muks.DataBind;
using UnityEngine.Events;

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
    private static Dictionary<string, ChallengeData> _dailyChallengeDataDic = new Dictionary<string, ChallengeData>();

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
    private static Dictionary<string, Type13ChallengeData> _type13ChallengeDataDic = new Dictionary<string, Type13ChallengeData>();
    private static Dictionary<string, Type14ChallengeData> _type14ChallengeDataDic = new Dictionary<string, Type14ChallengeData>();
    private static Dictionary<string, Type15ChallengeData> _type15ChallengeDataDic = new Dictionary<string, Type15ChallengeData>();
    private static Dictionary<string, Type16ChallengeData> _type16ChallengeDataDic = new Dictionary<string, Type16ChallengeData>();
    private static Dictionary<string, Type17ChallengeData> _type17ChallengeDataDic = new Dictionary<string, Type17ChallengeData>();
    private static Dictionary<string, Type18ChallengeData> _type18ChallengeDataDic = new Dictionary<string, Type18ChallengeData>();
    private static Dictionary<string, Type19ChallengeData> _type19ChallengeDataDic = new Dictionary<string, Type19ChallengeData>();

    private static Dictionary<string, Type28ChallengeData> _type28ChallengeDataDic = new Dictionary<string, Type28ChallengeData>();
    private static Dictionary<string, Type31ChallengeData> _type31ChallengeDataDic = new Dictionary<string, Type31ChallengeData>();
    private static Dictionary<string, Type32ChallengeData> _type32ChallengeDataDic = new Dictionary<string, Type32ChallengeData>();
    private static Dictionary<string, Type33ChallengeData> _type33ChallengeDataDic = new Dictionary<string, Type33ChallengeData>();
    private static Dictionary<string, Type34ChallengeData> _type34ChallengeDataDic = new Dictionary<string, Type34ChallengeData>();
    private static Dictionary<string, Type35ChallengeData> _type35ChallengeDataDic = new Dictionary<string, Type35ChallengeData>();



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
        UserInfo.OnGiveFurnitureHandler += Type13ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type13ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler += Type14ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type15ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler += Type16ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type17ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler += Type18ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type19ChallengeCheck;

        UserInfo.OnAddCleanCountHandler += Type28ChallengeCheck;

        UserInfo.OnAddCustomerCountHandler += Type31ChallengeCheck;
        UserInfo.OnChangeMoneyHandler += Type32ChallengeCheck;
        UserInfo.OnAddCookCountHandler += Type33ChallengeCheck;
        UserInfo.OnAddCleanCountHandler += Type34ChallengeCheck;
        UserInfo.OnAddAdvertisingViewCountHandler += Type35ChallengeCheck;

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
        Type13ChallengeCheck();
        Type14ChallengeCheck();
        Type15ChallengeCheck();
        Type16ChallengeCheck();
        Type17ChallengeCheck();
        Type18ChallengeCheck();
        Type19ChallengeCheck();

        Type28ChallengeCheck();

        Type31ChallengeCheck();
        Type32ChallengeCheck();
        Type33ChallengeCheck();
        Type34ChallengeCheck();
        Type35ChallengeCheck();
    }


    private void SetChallengeDatas()
    {
        _mainChallengeDataDic.Clear();
        _mainChallengeDataDic = SetData("REWARD_MAIN");

        _alltimeChallengeDataDic.Clear();
        _alltimeChallengeDataDic = SetData("REWARD_ALLTIME");

        _dailyChallengeDataDic.Clear();
        _dailyChallengeDataDic = SetData("REWARD_DAILY");
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
            BindData<UnityAction> shortcutAction = GetShortCutAction(row[2]);
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
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(challengeType, id, description, needFurnitureIds, moneyType, rewardMoney, shortcutAction);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    dic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    challengeType = ChallengeType.TYPE02;
                    string[] needKitchenUtensilIds = needItemId.Split(new char[] { '.' });
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(challengeType, id, description, needKitchenUtensilIds, moneyType, rewardMoney, shortcutAction);
                    _type02ChallengeDataDic.Add(id, challengeData02); 
                    dic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    challengeType = ChallengeType.TYPE03;
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney, shortcutAction);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    dic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    challengeType = ChallengeType.TYPE04;
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney, shortcutAction);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    dic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    challengeType = ChallengeType.TYPE05;
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney, shortcutAction);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    dic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    challengeType = ChallengeType.TYPE06;
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    dic.Add(id, challengeData06);
                    break;

                case "TYPE07":
                    challengeType = ChallengeType.TYPE07;
                    Type07ChallengeData challengeData07 = new Type07ChallengeData(challengeType, id, description, needItemId, count, moneyType, rewardMoney, shortcutAction);
                    _type07ChallengeDataDic.Add(id, challengeData07);
                    dic.Add(id, challengeData07);
                    break;

                case "TYPE08":
                    challengeType = ChallengeType.TYPE08;
                    Type08ChallengeData challengeData08 = new Type08ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type08ChallengeDataDic.Add(id, challengeData08);
                    dic.Add(id, challengeData08);
                    break;

                case "TYPE09":
                    challengeType = ChallengeType.TYPE09;
                    Type09ChallengeData challengeData09 = new Type09ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type09ChallengeDataDic.Add(id, challengeData09);
                    dic.Add(id, challengeData09);
                    break;

                case "TYPE10":
                    challengeType = ChallengeType.TYPE10;
                    Type10ChallengeData challengeData10 = new Type10ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type10ChallengeDataDic.Add(id, challengeData10);
                    dic.Add(id, challengeData10);
                    break;

                case "TYPE11":
                    challengeType = ChallengeType.TYPE11;
                    Type11ChallengeData challengeData11 = new Type11ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type11ChallengeDataDic.Add(id, challengeData11);
                    dic.Add(id, challengeData11);
                    break;

                case "TYPE12":
                    challengeType = ChallengeType.TYPE12;
                    Type12ChallengeData challengeData12 = new Type12ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type12ChallengeDataDic.Add(id, challengeData12);
                    dic.Add(id, challengeData12);
                    break;

                case "TYPE13":
                    challengeType = ChallengeType.TYPE13;
                    Type13ChallengeData challengeData13 = new Type13ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type13ChallengeDataDic.Add(id, challengeData13);
                    dic.Add(id, challengeData13);
                    break;

                case "TYPE14":
                    challengeType = ChallengeType.TYPE14;
                    Type14ChallengeData challengeData14 = new Type14ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type14ChallengeDataDic.Add(id, challengeData14);
                    dic.Add(id, challengeData14);
                    break;

                case "TYPE15":
                    challengeType = ChallengeType.TYPE15;
                    Type15ChallengeData challengeData15 = new Type15ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type15ChallengeDataDic.Add(id, challengeData15);
                    dic.Add(id, challengeData15);
                    break;

                case "TYPE16":
                    challengeType = ChallengeType.TYPE16;
                    Type16ChallengeData challengeData16 = new Type16ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney, shortcutAction);
                    _type16ChallengeDataDic.Add(id, challengeData16);
                    dic.Add(id, challengeData16);
                    break;

                case "TYPE17":
                    challengeType = ChallengeType.TYPE17;
                    Type17ChallengeData challengeData17 = new Type17ChallengeData(challengeType, id, description, needItemId, moneyType, rewardMoney, shortcutAction);
                    _type17ChallengeDataDic.Add(id, challengeData17);
                    dic.Add(id, challengeData17);
                    break;

                case "TYPE18":
                    challengeType = ChallengeType.TYPE18;
                    Type18ChallengeData challengeData18 = new Type18ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type18ChallengeDataDic.Add(id, challengeData18);
                    dic.Add(id, challengeData18);
                    break;

                case "TYPE19":
                    challengeType = ChallengeType.TYPE19;
                    Type19ChallengeData challengeData19 = new Type19ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type19ChallengeDataDic.Add(id, challengeData19);
                    dic.Add(id, challengeData19);
                    break;

                case "TYPE28":
                    challengeType = ChallengeType.TYPE28;
                    Type28ChallengeData challengeData28 = new Type28ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type28ChallengeDataDic.Add(id, challengeData28);
                    dic.Add(id, challengeData28);
                    break;


                case "TYPE31":
                    challengeType = ChallengeType.TYPE31;
                    Type31ChallengeData challengeData31= new Type31ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type31ChallengeDataDic.Add(id, challengeData31);
                    dic.Add(id, challengeData31);
                    break;

                case "TYPE32":
                    challengeType = ChallengeType.TYPE32;
                    Type32ChallengeData challengeData32 = new Type32ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type32ChallengeDataDic.Add(id, challengeData32);
                    dic.Add(id, challengeData32);
                    break;

                case "TYPE33":
                    challengeType = ChallengeType.TYPE33;
                    Type33ChallengeData challengeData33 = new Type33ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type33ChallengeDataDic.Add(id, challengeData33);
                    dic.Add(id, challengeData33);
                    break;

                case "TYPE34":
                    challengeType = ChallengeType.TYPE34;
                    Type34ChallengeData challengeData34 = new Type34ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type34ChallengeDataDic.Add(id, challengeData34);
                    dic.Add(id, challengeData34);
                    break;

                case "TYPE35":
                    challengeType = ChallengeType.TYPE35;
                    Type35ChallengeData challengeData35 = new Type35ChallengeData(challengeType, id, description, count, moneyType, rewardMoney, shortcutAction);
                    _type35ChallengeDataDic.Add(id, challengeData35);
                    dic.Add(id, challengeData35);
                    break;

            }
        }

        return dic;
    }

    public ChallengeData GetCurrentMainChallengeData()
    {
        foreach(var data in _mainChallengeDataDic)
        {
            if (UserInfo.GetIsClearMainChallenge(data.Key))
                continue;

            return data.Value;
        }

        return null;
    }

    public List<ChallengeData> GetAllTimeChallenge()
    {
        return _alltimeChallengeDataDic.Values.ToList();
    }

    public List<ChallengeData> GetDailyChallenge()
    {
        return _dailyChallengeDataDic.Values.ToList();
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
                int customerCount = UserInfo.TotalCumulativeCustomerCount;
                return customerCount == 0 ? 0 : Math.Min(1, (float)customerCount / data09.CustomerCount);

            case ChallengeType.TYPE10:
                Type10ChallengeData data10 = (Type10ChallengeData)data;
                int visitedCustomerCount = UserInfo.GetVisitedCustomerCount();
                return visitedCustomerCount == 0 ? 0 : Math.Min(1, (float)visitedCustomerCount / data10.CustomerCount);

            case ChallengeType.TYPE11:
                Type11ChallengeData data11 = (Type11ChallengeData)data;
                int moneyCount = UserInfo.TotalAddMoney;
                return moneyCount == 0 ? 0 : Math.Min(1, (float)moneyCount / data11.MoneyCount);

            case ChallengeType.TYPE12:
                Type12ChallengeData data12 = (Type12ChallengeData)data;
                int promotionCount = UserInfo.PromotionCount;
                return promotionCount == 0 ? 0 : Math.Min(1, (float)promotionCount / data12.PromotionCount);

            case ChallengeType.TYPE13:
                Type13ChallengeData data13 = (Type13ChallengeData)data;
                int furnitureAndKitchenUtensilCount = UserInfo.GetFurnitureAndKitchenUtensilCount();
                return furnitureAndKitchenUtensilCount == 0 ? 0 : Math.Min(1, (float)furnitureAndKitchenUtensilCount / data13.Count);

            case ChallengeType.TYPE14:
                Type14ChallengeData data14 = (Type14ChallengeData)data;
                int furnitureSetCount = UserInfo.GetActivatedFurnitureEffectSetCount();
                return furnitureSetCount == 0 ? 0 : Math.Min(1, (float)furnitureSetCount / data14.Count);

            case ChallengeType.TYPE15:
                Type15ChallengeData data15 = (Type15ChallengeData)data;
                int kitchenUtensilSetCount = UserInfo.GetActivatedKitchenUtensilEffectSetCount();
                return kitchenUtensilSetCount == 0 ? 0 : Math.Min(1, (float)kitchenUtensilSetCount / data15.Count);

            case ChallengeType.TYPE16:
                Type16ChallengeData data16 = (Type16ChallengeData)data;
                int giveFurnitureSetCount = UserInfo.GetEffectSetFurnitureCount(data16.SetId);
               
                return giveFurnitureSetCount == 0 ? 0 : Math.Min(1, (float)giveFurnitureSetCount / ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT);

            case ChallengeType.TYPE17:
                Type17ChallengeData data17 = (Type17ChallengeData)data;
                int giveKitchenUntensilSetCount = UserInfo.GetEffectSetKitchenUtensilCount(data17.SetId);
                return giveKitchenUntensilSetCount == 0 ? 0 : Math.Min(1, (float)giveKitchenUntensilSetCount / ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT);

            case ChallengeType.TYPE18:
                Type18ChallengeData data18 = (Type18ChallengeData)data;
                int giveFurnitureCount = UserInfo.GetGiveFurnitureCount();
                return giveFurnitureCount == 0 ? 0 : Math.Min(1, (float)giveFurnitureCount / data18.Count);

            case ChallengeType.TYPE19:
                Type19ChallengeData data19 = (Type19ChallengeData)data;
                int giveKitchenUntensilCount = UserInfo.GetGiveKitchenUtensilCount();
                return giveKitchenUntensilCount == 0 ? 0 : Math.Min(1, (float)giveKitchenUntensilCount / data19.Count);

            case ChallengeType.TYPE28:
                Type28ChallengeData data28 = (Type28ChallengeData)data;
                int cleanCount = UserInfo.TotalCleanCount;
                return cleanCount == 0 ? 0 : Math.Min(1, (float)cleanCount / data28.Count);

            case ChallengeType.TYPE31:
                Type31ChallengeData data31 = (Type31ChallengeData)data;
                int dailyCustomerCount = UserInfo.DailyCumulativeCustomerCount;
                return GetChallengeAchievementRate(dailyCustomerCount, data31.Count);

            case ChallengeType.TYPE32:
                Type32ChallengeData data32 = (Type32ChallengeData)data;
                int dailyAddMoney = UserInfo.DailyAddMoney;
                return GetChallengeAchievementRate(dailyAddMoney, data32.Count);

            case ChallengeType.TYPE33:
                Type33ChallengeData data33 = (Type33ChallengeData)data;
                int dailyCookCount  = UserInfo.DailyCookCount;
                return GetChallengeAchievementRate(dailyCookCount, data33.Count);

            case ChallengeType.TYPE34:
                Type34ChallengeData data34 = (Type34ChallengeData)data;
                int dailyCleanCount = UserInfo.DailyCleanCount;
                return GetChallengeAchievementRate(dailyCleanCount, data34.Count);

            case ChallengeType.TYPE35:
                Type35ChallengeData data35 = (Type35ChallengeData)data;
                int dailyAdvertisingViewCount = UserInfo.DailyAdvertisingViewCount;
                return GetChallengeAchievementRate(dailyAdvertisingViewCount, data35.Count);


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

        else if (_dailyChallengeDataDic.ContainsKey(id))
            UserInfo.ClearDailyChallenge(id);

        OnChallengeUpdateHandler?.Invoke();
    }


    private float GetChallengeAchievementRate(int currentValue, int targetValue)
    {
        return currentValue == 0 ? 0 : Math.Min(1, (float)currentValue / targetValue);
    }


    private BindData<UnityAction> GetShortCutAction(string shortCutType)
    {
        shortCutType = string.Concat(shortCutType.Where(c => !Char.IsWhiteSpace(c)));
        switch (shortCutType)
        {
            case "ShortCut01":
                return DataBind.GetUnityActionBindData("PopUI");

            case "ShortCut02":
                return DataBind.GetUnityActionBindData("ShowKitchenUI");

            case "ShortCut03":
                return DataBind.GetUnityActionBindData("ShowFurnitureUI");

            case "ShortCut04":
                return DataBind.GetUnityActionBindData("ShowKitchenUI");

            case "ShortCut05":
                return DataBind.GetUnityActionBindData("ShowStaffUI");

            case "ShortCut06":
                return DataBind.GetUnityActionBindData("ShowRestaurantAdminUI");

            case "ShortCut08":
                return DataBind.GetUnityActionBindData("ShowChallengeUI");

            default:
                return DataBind.GetUnityActionBindData("PopUI");
        }
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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            if (UserInfo.TotalCumulativeCustomerCount < data.CustomerCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if(_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            if (UserInfo.TotalAddMoney < data.MoneyCount)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

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

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE12);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type13ChallengeCheck()
    {
        int count = 0;
        int furnitureCount = UserInfo.GetFurnitureAndKitchenUtensilCount();
        foreach (Type13ChallengeData data in _type13ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (furnitureCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE13);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type14ChallengeCheck()
    {
        int count = 0;
        int furnitureSetCount = UserInfo.GetActivatedFurnitureEffectSetCount();
        foreach (Type14ChallengeData data in _type14ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (furnitureSetCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE14);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type15ChallengeCheck()
    {
        int count = 0;
        int kitchenUtensilSetCount = UserInfo.GetActivatedKitchenUtensilEffectSetCount();
        foreach (Type15ChallengeData data in _type15ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (kitchenUtensilSetCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE15);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type16ChallengeCheck()
    {
        int count = 0;
        foreach (Type16ChallengeData data in _type16ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT != UserInfo.GetEffectSetFurnitureCount(data.SetId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE15);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type17ChallengeCheck()
    {
        int count = 0;
        foreach (Type17ChallengeData data in _type17ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT != UserInfo.GetEffectSetKitchenUtensilCount(data.SetId))
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE15);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type18ChallengeCheck()
    {
        int count = 0;
        int giveFurnitureCount = UserInfo.GetGiveFurnitureCount();
        foreach (Type18ChallengeData data in _type18ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (giveFurnitureCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE18);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type19ChallengeCheck()
    {
        int count = 0;
        int giveKitchenUtensilCount = UserInfo.GetGiveKitchenUtensilCount();
        foreach (Type19ChallengeData data in _type19ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (giveKitchenUtensilCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE19);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type28ChallengeCheck()
    {
        int count = 0;
        int cleanCount = UserInfo.TotalCleanCount;
        foreach (Type28ChallengeData data in _type28ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (cleanCount < data.Count)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE28);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type31ChallengeCheck()
    {
        int count = 0;
        int dailyCustomerCount = UserInfo.DailyCumulativeCustomerCount;
        foreach (Type31ChallengeData data in _type31ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCustomerCount, data.Count) < 1)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE31);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type32ChallengeCheck()
    {
        int count = 0;
        int dailyAddMoney = UserInfo.DailyAddMoney;
        foreach (Type32ChallengeData data in _type32ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyAddMoney, data.Count) < 1)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE32);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type33ChallengeCheck()
    {
        int count = 0;
        int dailyCookCount = UserInfo.DailyCookCount;
        foreach (Type33ChallengeData data in _type33ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCookCount, data.Count) < 1)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE33);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }

    private void Type34ChallengeCheck()
    {
        int count = 0;
        int dailyCleanCount = UserInfo.DailyCleanCount;
        foreach (Type34ChallengeData data in _type34ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCleanCount, data.Count) < 1)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE34);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }


    private void Type35ChallengeCheck()
    {
        int count = 0;
        int dailyAdvertisingViewCount = UserInfo.DailyAdvertisingViewCount;
        foreach (Type35ChallengeData data in _type35ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyAdvertisingViewCount, data.Count) < 1)
                continue;

            if (_mainChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneMainChallenge(data.Id);

            else if (_alltimeChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneAllTimeChallenge(data.Id);

            else if (_dailyChallengeDataDic.ContainsKey(data.Id))
                UserInfo.DoneDailyChallenge(data.Id);

            count++;
        }

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE35);
        if (0 < count)
            OnChallengeUpdateHandler?.Invoke();
    }
}
