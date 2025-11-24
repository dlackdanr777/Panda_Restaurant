using Muks.DataBind;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class ChallengeManager : MonoBehaviour
{
    public class ChallengeRepeatRule
    {
        public int StartValue;      // 반복 시작 값
        public int Step;            // 반복 간격
        public Func<int, int> RewardFunc; // 반복 보상 계산 함수
        public Func<int, int> ScoreFunc;  // 반복 평점 계산 함수 (필요시)
    }

    public event Action OnDailyChallengeUpdateHandler;
    public event Action OnAllTimeChallengeUpdateHandler;
    public event Action OnMainChallengeUpdateHandler;
    public event Action OnWeeklyChallengeUpdateHandler;

    public event Action OnAddChallengeHandler;

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



    public event Action<ChallengeType> OnChallengePercentUpdateHandler;

    private static readonly string _csvFilePath = "Challenge/";

    private static Dictionary<string, ChallengeData> _challengeDataDic = new Dictionary<string, ChallengeData>();
    private static Dictionary<string, ChallengeData> _mainChallengeDataDic = new Dictionary<string, ChallengeData>();
    private static Dictionary<string, ChallengeData> _alltimeChallengeDataDic = new Dictionary<string, ChallengeData>();
    private static Dictionary<string, ChallengeData> _dailyChallengeDataDic = new Dictionary<string, ChallengeData>();
    private static Dictionary<string, ChallengeData> _weeklyChallengeDataDic = new Dictionary<string, ChallengeData>();

    private static List<ChallengeData> _mainChallengeList = new List<ChallengeData>();
    private static List<ChallengeData> _dailyChallengeList = new List<ChallengeData>();
    private static List<ChallengeData> _weeklyChallengeList = new List<ChallengeData>();
    private static List<ChallengeData> _allTimeChallengeList = new List<ChallengeData>();


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
    private static Dictionary<string, Type20ChallengeData> _type20ChallengeDataDic = new Dictionary<string, Type20ChallengeData>();

    private static Dictionary<string, Type21ChallengeData> _type21ChallengeDataDic = new Dictionary<string, Type21ChallengeData>();
    private static Dictionary<string, Type22ChallengeData> _type22ChallengeDataDic = new Dictionary<string, Type22ChallengeData>();
    private static Dictionary<string, Type23ChallengeData> _type23ChallengeDataDic = new Dictionary<string, Type23ChallengeData>();
    private static Dictionary<string, Type24ChallengeData> _type24ChallengeDataDic = new Dictionary<string, Type24ChallengeData>();
    private static Dictionary<string, Type25ChallengeData> _type25ChallengeDataDic = new Dictionary<string, Type25ChallengeData>();
    private static Dictionary<string, Type26ChallengeData> _type26ChallengeDataDic = new Dictionary<string, Type26ChallengeData>();

    private static Dictionary<string, Type28ChallengeData> _type28ChallengeDataDic = new Dictionary<string, Type28ChallengeData>();
    private static Dictionary<string, Type30ChallengeData> _type30ChallengeDataDic = new Dictionary<string, Type30ChallengeData>();
    private static Dictionary<string, Type31ChallengeData> _type31ChallengeDataDic = new Dictionary<string, Type31ChallengeData>();
    private static Dictionary<string, Type32ChallengeData> _type32ChallengeDataDic = new Dictionary<string, Type32ChallengeData>();
    private static Dictionary<string, Type33ChallengeData> _type33ChallengeDataDic = new Dictionary<string, Type33ChallengeData>();
    private static Dictionary<string, Type34ChallengeData> _type34ChallengeDataDic = new Dictionary<string, Type34ChallengeData>();
    private static Dictionary<string, Type35ChallengeData> _type35ChallengeDataDic = new Dictionary<string, Type35ChallengeData>();
    private static Dictionary<string, Type36ChallengeData> _type36ChallengeDataDic = new Dictionary<string, Type36ChallengeData>();
    private static Dictionary<string, Type37ChallengeData> _type37ChallengeDataDic = new Dictionary<string, Type37ChallengeData>();
    private static Dictionary<string, Type38ChallengeData> _type38ChallengeDataDic = new Dictionary<string, Type38ChallengeData>();
    private static Dictionary<string, Type39ChallengeData> _type39ChallengeDataDic = new Dictionary<string, Type39ChallengeData>();
    private static Dictionary<string, Type40ChallengeData> _type40ChallengeDataDic = new Dictionary<string, Type40ChallengeData>();
    private static Dictionary<string, Type41ChallengeData> _type41ChallengeDataDic = new Dictionary<string, Type41ChallengeData>();


    public ChallengeData GetCallengeData(string id)
    {
        if (_challengeDataDic.TryGetValue(id, out var challengeData))
            return challengeData;

        Debug.LogError("도전과제 ID가 존재하지 않습니다: " + id);
        return null;
    }

    public void UpdateChallenge()
    {
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
        Type20ChallengeCheck();
        Type21ChallengeCheck();
        Type22ChallengeCheck();
        Type23ChallengeCheck();
        Type24ChallengeCheck();
        Type25ChallengeCheck();
        Type26ChallengeCheck();

        Type28ChallengeCheck();

        Type30ChallengeCheck();
        Type31ChallengeCheck();
        Type32ChallengeCheck();
        Type33ChallengeCheck();
        Type34ChallengeCheck();
        Type35ChallengeCheck();
        Type36ChallengeCheck();
        Type37ChallengeCheck();
        Type38ChallengeCheck();
        Type39ChallengeCheck();
        Type40ChallengeCheck();
        Type41ChallengeCheck();

        OnCheckRepeatCoinChallengeEvent();
        OnCheckRepeatVisitChallengeEvent();
        OnCheckRepeatScoreChallengeEvent();
        OnCheckRepeatGatchaChallengeEvent();

    }

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
        GameManager.Instance.OnChangeScoreHandler += Type08ChallengeCheck;
        UserInfo.OnAddCustomerCountHandler += Type09ChallengeCheck;
        UserInfo.OnVisitedCustomerHandler += Type10ChallengeCheck;
        UserInfo.OnChangeMoneyHandler += Type11ChallengeCheck;
        UserInfo.OnAddPromotionCountHandler += Type12ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler += Type13ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type13ChallengeCheck;
        UserInfo.OnGiveFurnitureHandler += Type14ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type15ChallengeCheck;
        UserInfo.OnChangeFurnitureHandler += (floor, type) => Type16ChallengeCheck();
        UserInfo.OnChangeKitchenUtensilHandler += (floor, type) => Type17ChallengeCheck();
        UserInfo.OnGiveFurnitureHandler += Type18ChallengeCheck;
        UserInfo.OnGiveKitchenUtensilHandler += Type19ChallengeCheck;
        UserInfo.OnChangeFloorHandler += Type20ChallengeCheck;
        UserInfo.OnVisitSpecialCustomerHandler += Type21ChallengeCheck;
        UserInfo.OnExterminationGatecrasherCustomerHandler += Type22ChallengeCheck;
        UserInfo.OnExterminationGatecrasherCustomerHandler += Type23ChallengeCheck;
        UserInfo.OnExterminationGatecrasherCustomerHandler += Type24ChallengeCheck;
        UserInfo.OnUseGachaMachineHandler += Type25ChallengeCheck;
        UserInfo.OnUseGachaMachineHandler += Type26ChallengeCheck;

        UserInfo.OnAddCleanCountHandler += Type28ChallengeCheck;

        OnDailyChallengeUpdateHandler += Type30ChallengeCheck;
        UserInfo.OnAddCustomerCountHandler += Type31ChallengeCheck;
        UserInfo.OnChangeMoneyHandler += Type32ChallengeCheck;
        UserInfo.OnAddCookCountHandler += Type33ChallengeCheck;
        UserInfo.OnAddCleanCountHandler += Type34ChallengeCheck;
        UserInfo.OnAddAdvertisingViewCountHandler += Type35ChallengeCheck;

        // 주간 도전과제 이벤트 핸들러
        OnWeeklyChallengeUpdateHandler += Type36ChallengeCheck;
        UserInfo.OnAddCustomerCountHandler += Type37ChallengeCheck;
        UserInfo.OnChangeMoneyHandler += Type38ChallengeCheck;
        UserInfo.OnAddCookCountHandler += Type39ChallengeCheck;
        UserInfo.OnAddCleanCountHandler += Type40ChallengeCheck;
        UserInfo.OnExterminationGatecrasherCustomerHandler += Type41ChallengeCheck;

        UserInfo.OnClearChallengeHandler += OnCheckRepeatCoinChallengeEvent;
        UserInfo.OnClearChallengeHandler += OnCheckRepeatVisitChallengeEvent;
        UserInfo.OnClearChallengeHandler += OnCheckRepeatScoreChallengeEvent;
        UserInfo.OnClearChallengeHandler += OnCheckRepeatGatchaChallengeEvent;

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
        Type20ChallengeCheck();
        Type21ChallengeCheck();
        Type22ChallengeCheck();
        Type23ChallengeCheck();
        Type24ChallengeCheck();
        Type25ChallengeCheck();
        Type26ChallengeCheck();

        Type28ChallengeCheck();

        Type30ChallengeCheck();
        Type31ChallengeCheck();
        Type32ChallengeCheck();
        Type33ChallengeCheck();
        Type34ChallengeCheck();
        Type35ChallengeCheck();
        Type36ChallengeCheck();
        Type37ChallengeCheck();
        Type38ChallengeCheck();
        Type39ChallengeCheck();
        Type40ChallengeCheck();
        Type41ChallengeCheck();
    }


    private void SetChallengeDatas()
    {
        _mainChallengeDataDic.Clear();
        _mainChallengeDataDic = SetData(Challenges.Main, "REWARD_MAIN");
        _mainChallengeList.Clear();
        _mainChallengeList.AddRange(_mainChallengeDataDic.Values);

        // _alltimeChallengeDataDic.Clear();
        // _alltimeChallengeDataDic = SetAllTimeData("REWARD_ALLTIME");
        // _allTimeChallengeList.Clear();
        // _allTimeChallengeList.AddRange(_alltimeChallengeDataDic.Values);

        _dailyChallengeDataDic.Clear();
        _dailyChallengeDataDic = SetData(Challenges.Daily, "REWARD_DAILY");
        _dailyChallengeList.Clear();
        _dailyChallengeList.AddRange(_dailyChallengeDataDic.Values);

        _weeklyChallengeDataDic.Clear();
        _weeklyChallengeDataDic = SetData(Challenges.Weekly, "REWARD_WEEKLY");
        _weeklyChallengeList.Clear();
        _weeklyChallengeList.AddRange(_weeklyChallengeDataDic.Values);

        _challengeDataDic.Clear();
        _challengeDataDic.AddRange(_mainChallengeDataDic);
        _challengeDataDic.AddRange(_alltimeChallengeDataDic);
        _challengeDataDic.AddRange(_dailyChallengeDataDic);
        _challengeDataDic.AddRange(_weeklyChallengeDataDic);
    }


    private Dictionary<string, ChallengeData> SetData(Challenges challenges, string csvFileName)
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath + csvFileName);
        Dictionary<string, ChallengeData> dic = new Dictionary<string, ChallengeData>();
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = Utility.SplitCsvLine(data[i]);
            string id = string.Concat(row[0].Where(c => !Char.IsWhiteSpace(c)));

            if (string.IsNullOrWhiteSpace(id))
                continue;

            string type = string.Concat(row[1].Where(c => !Char.IsWhiteSpace(c)));
            string shortCutType = string.Concat(row[2].Where(c => !Char.IsWhiteSpace(c)));
            BindData<UnityAction> shortcutAction = GetShortCutAction(row[2]);
            string description = row[3];
            string needItemId = string.Concat(row[4].Where(c => !Char.IsWhiteSpace(c)));
            int count = int.TryParse(string.Concat(row[5].Where(c => !Char.IsWhiteSpace(c))), out int parsedCount) ? parsedCount : 0;
            MoneyType moneyType = row[6].Contains("코인") || row[6].Contains("게임머니") ? MoneyType.Gold : MoneyType.Dia;
            int rewardMoney = int.TryParse(string.Concat(row[7].Where(c => !Char.IsWhiteSpace(c))), out int parsedRewardMoney) ? parsedRewardMoney : 0;
            ChallengeType challengeType;

            switch (type)
            {
                case "TYPE01":
                    challengeType = ChallengeType.TYPE01;
                    string[] needFurnitureIds = needItemId.Split(new char[] { '.' });
                    for (int j = 0, cntJ = needFurnitureIds.Length; j < cntJ; j++)
                    {
                        FurnitureData furnitureData = FurnitureDataManager.Instance.GetFurnitureData(needFurnitureIds[j]);
                        if (furnitureData == null)
                        {
                            Debug.LogError($"Furniture ID not found: {needFurnitureIds[j]}");
                            continue;
                        }
                        needFurnitureIds[j] = needFurnitureIds[j].Trim();
                    }
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(challenges, challengeType, id, description, needFurnitureIds, moneyType, rewardMoney, 0, shortcutAction);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    dic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    challengeType = ChallengeType.TYPE02;
                    string[] needKitchenUtensilIds = needItemId.Split(new char[] { '.' });
                    for (int j = 0, cntJ = needKitchenUtensilIds.Length; j < cntJ; j++)
                    {
                        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(needKitchenUtensilIds[j]);
                        if (kitchenData == null)
                        {
                            Debug.LogError($"Kitchen Utensil ID not found: {needKitchenUtensilIds[j]}");
                            continue;
                        }
                        needKitchenUtensilIds[j] = needKitchenUtensilIds[j].Trim();
                    }
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(challenges, challengeType, id, description, needKitchenUtensilIds, moneyType, rewardMoney, 0, shortcutAction);
                    _type02ChallengeDataDic.Add(id, challengeData02);
                    dic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    challengeType = ChallengeType.TYPE03;
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, 0, shortcutAction);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    dic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    challengeType = ChallengeType.TYPE04;
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, 0, shortcutAction);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    dic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    challengeType = ChallengeType.TYPE05;
                    if (StaffDataManager.Instance.GetStaffData(needItemId) == null)
                    {
                        Debug.LogError($"Staff ID not found: {needItemId}");
                        continue;
                    }
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, 0, shortcutAction);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    dic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    challengeType = ChallengeType.TYPE06;
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    dic.Add(id, challengeData06);
                    break;

                case "TYPE07":
                    challengeType = ChallengeType.TYPE07;
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    Type07ChallengeData challengeData07 = new Type07ChallengeData(challenges, challengeType, id, description, needItemId, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type07ChallengeDataDic.Add(id, challengeData07);
                    dic.Add(id, challengeData07);
                    break;

                case "TYPE08":
                    challengeType = ChallengeType.TYPE08;
                    Type08ChallengeData challengeData08 = new Type08ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type08ChallengeDataDic.Add(id, challengeData08);
                    dic.Add(id, challengeData08);
                    break;

                case "TYPE09":
                    challengeType = ChallengeType.TYPE09;
                    Type09ChallengeData challengeData09 = new Type09ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type09ChallengeDataDic.Add(id, challengeData09);
                    dic.Add(id, challengeData09);
                    break;

                case "TYPE10":
                    challengeType = ChallengeType.TYPE10;
                    Type10ChallengeData challengeData10 = new Type10ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type10ChallengeDataDic.Add(id, challengeData10);
                    dic.Add(id, challengeData10);
                    break;

                case "TYPE11":
                    challengeType = ChallengeType.TYPE11;
                    Type11ChallengeData challengeData11 = new Type11ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type11ChallengeDataDic.Add(id, challengeData11);
                    dic.Add(id, challengeData11);
                    break;

                case "TYPE12":
                    challengeType = ChallengeType.TYPE12;
                    Type12ChallengeData challengeData12 = new Type12ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type12ChallengeDataDic.Add(id, challengeData12);
                    dic.Add(id, challengeData12);
                    break;

                case "TYPE13":
                    challengeType = ChallengeType.TYPE13;
                    Type13ChallengeData challengeData13 = new Type13ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type13ChallengeDataDic.Add(id, challengeData13);
                    dic.Add(id, challengeData13);
                    break;

                case "TYPE14":
                    challengeType = ChallengeType.TYPE14;
                    Type14ChallengeData challengeData14 = new Type14ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type14ChallengeDataDic.Add(id, challengeData14);
                    dic.Add(id, challengeData14);
                    break;

                case "TYPE15":
                    challengeType = ChallengeType.TYPE15;
                    Type15ChallengeData challengeData15 = new Type15ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type15ChallengeDataDic.Add(id, challengeData15);
                    dic.Add(id, challengeData15);
                    break;

                case "TYPE16":
                    challengeType = ChallengeType.TYPE16;
                    Type16ChallengeData challengeData16 = new Type16ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, 0, shortcutAction);
                    _type16ChallengeDataDic.Add(id, challengeData16);
                    dic.Add(id, challengeData16);
                    break;

                case "TYPE17":
                    challengeType = ChallengeType.TYPE17;
                    Type17ChallengeData challengeData17 = new Type17ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, 0, shortcutAction);
                    _type17ChallengeDataDic.Add(id, challengeData17);
                    dic.Add(id, challengeData17);
                    break;

                case "TYPE18":
                    challengeType = ChallengeType.TYPE18;
                    Type18ChallengeData challengeData18 = new Type18ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type18ChallengeDataDic.Add(id, challengeData18);
                    dic.Add(id, challengeData18);
                    break;

                case "TYPE19":
                    challengeType = ChallengeType.TYPE19;
                    Type19ChallengeData challengeData19 = new Type19ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type19ChallengeDataDic.Add(id, challengeData19);
                    dic.Add(id, challengeData19);
                    break;

                case "TYPE20":
                    challengeType = ChallengeType.TYPE20;
                    ERestaurantFloorType floorType = (ERestaurantFloorType)(int.Parse(needItemId) - 1);
                    Type20ChallengeData challengeData20 = new Type20ChallengeData(challenges, challengeType, id, description, floorType, moneyType, rewardMoney, 0, shortcutAction);
                    _type20ChallengeDataDic.Add(id, challengeData20);
                    dic.Add(id, challengeData20);
                    break;

                case "TYPE21":
                    challengeType = ChallengeType.TYPE21;
                    Type21ChallengeData challengeData21 = new Type21ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type21ChallengeDataDic.Add(id, challengeData21);
                    dic.Add(id, challengeData21);
                    break;

                case "TYPE22":
                    challengeType = ChallengeType.TYPE22;
                    Type22ChallengeData challengeData22 = new Type22ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type22ChallengeDataDic.Add(id, challengeData22);
                    dic.Add(id, challengeData22);
                    break;

                case "TYPE23":
                    challengeType = ChallengeType.TYPE23;
                    Type23ChallengeData challengeData23 = new Type23ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type23ChallengeDataDic.Add(id, challengeData23);
                    dic.Add(id, challengeData23);
                    break;

                case "TYPE24":
                    challengeType = ChallengeType.TYPE24;
                    Type24ChallengeData challengeData24 = new Type24ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type24ChallengeDataDic.Add(id, challengeData24);
                    dic.Add(id, challengeData24);
                    break;

                case "TYPE25":
                    challengeType = ChallengeType.TYPE25;
                    Type25ChallengeData challengeData25 = new Type25ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type25ChallengeDataDic.Add(id, challengeData25);
                    dic.Add(id, challengeData25);
                    break;

                case "TYPE26":
                    challengeType = ChallengeType.TYPE26;
                    Type26ChallengeData challengeData26 = new Type26ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type26ChallengeDataDic.Add(id, challengeData26);
                    dic.Add(id, challengeData26);
                    break;

                case "TYPE28":
                    challengeType = ChallengeType.TYPE28;
                    Type28ChallengeData challengeData28 = new Type28ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type28ChallengeDataDic.Add(id, challengeData28);
                    dic.Add(id, challengeData28);
                    break;

                case "TYPE30":
                    challengeType = ChallengeType.TYPE30;
                    Type30ChallengeData challengeData30 = new Type30ChallengeData(challenges, challengeType, id, description, moneyType, rewardMoney, 0, shortcutAction);
                    _type30ChallengeDataDic.Add(id, challengeData30);
                    dic.Add(id, challengeData30);
                    break;

                case "TYPE31":
                    challengeType = ChallengeType.TYPE31;
                    Type31ChallengeData challengeData31 = new Type31ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type31ChallengeDataDic.Add(id, challengeData31);
                    dic.Add(id, challengeData31);
                    break;

                case "TYPE32":
                    challengeType = ChallengeType.TYPE32;
                    Type32ChallengeData challengeData32 = new Type32ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type32ChallengeDataDic.Add(id, challengeData32);
                    dic.Add(id, challengeData32);
                    break;

                case "TYPE33":
                    challengeType = ChallengeType.TYPE33;
                    Type33ChallengeData challengeData33 = new Type33ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type33ChallengeDataDic.Add(id, challengeData33);
                    dic.Add(id, challengeData33);
                    break;

                case "TYPE34":
                    challengeType = ChallengeType.TYPE34;
                    Type34ChallengeData challengeData34 = new Type34ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type34ChallengeDataDic.Add(id, challengeData34);
                    dic.Add(id, challengeData34);
                    break;

                case "TYPE35":
                    challengeType = ChallengeType.TYPE35;
                    Type35ChallengeData challengeData35 = new Type35ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type35ChallengeDataDic.Add(id, challengeData35);
                    dic.Add(id, challengeData35);
                    break;

                case "TYPE36":
                    challengeType = ChallengeType.TYPE36;
                    Type36ChallengeData challengeData36 = new Type36ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type36ChallengeDataDic.Add(id, challengeData36);
                    dic.Add(id, challengeData36);
                    break;

                case "TYPE37":
                    challengeType = ChallengeType.TYPE37;
                    Type37ChallengeData challengeData37 = new Type37ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type37ChallengeDataDic.Add(id, challengeData37);
                    dic.Add(id, challengeData37);
                    break;

                case "TYPE38":
                    challengeType = ChallengeType.TYPE38;
                    Type38ChallengeData challengeData38 = new Type38ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type38ChallengeDataDic.Add(id, challengeData38);
                    dic.Add(id, challengeData38);
                    break;

                case "TYPE39":
                    challengeType = ChallengeType.TYPE39;
                    Type39ChallengeData challengeData39 = new Type39ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type39ChallengeDataDic.Add(id, challengeData39);
                    dic.Add(id, challengeData39);
                    break;

                case "TYPE40":
                    challengeType = ChallengeType.TYPE40;
                    Type40ChallengeData challengeData40 = new Type40ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type40ChallengeDataDic.Add(id, challengeData40);
                    dic.Add(id, challengeData40);
                    break;

                case "TYPE41":
                    challengeType = ChallengeType.TYPE41;
                    Type41ChallengeData challengeData41 = new Type41ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, 0, shortcutAction);
                    _type41ChallengeDataDic.Add(id, challengeData41);
                    dic.Add(id, challengeData41);
                    break;

            }
        }

        return dic;
    }


    private Dictionary<string, ChallengeData> SetAllTimeData(string csvFileName)
    {
        TextAsset csvData = Resources.Load<TextAsset>(_csvFilePath + csvFileName);
        Dictionary<string, ChallengeData> dic = new Dictionary<string, ChallengeData>();
        Challenges challenges = Challenges.AllTime;
        string[] data = csvData.text.Split(new char[] { '\n' });
        string[] row;

        for (int i = 1, cnt = data.Length - 1; i < cnt; ++i)
        {
            row = Utility.SplitCsvLine(data[i]);
            string id = string.Concat(row[0].Where(c => !Char.IsWhiteSpace(c)));

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (id.Contains("Repeat"))
            {
                continue;
            }

            string type = string.Concat(row[1].Where(c => !Char.IsWhiteSpace(c)));
            string shortCutType = string.Concat(row[2].Where(c => !Char.IsWhiteSpace(c)));
            BindData<UnityAction> shortcutAction = GetShortCutAction(row[2]);
            string description = row[3];
            string needItemId = string.Concat(row[4].Where(c => !Char.IsWhiteSpace(c)));
            int count = int.TryParse(string.Concat(row[5].Where(c => !Char.IsWhiteSpace(c))), out int parsedCount2) ? parsedCount2 : 0;

            int rewardScore = int.TryParse(string.Concat(row[7].Where(c => !Char.IsWhiteSpace(c))), out int parsedRewardScore) ? parsedRewardScore : 0;
            MoneyType moneyType = row[8].Contains("코인") || row[8].Contains("게임머니") ? MoneyType.Gold : MoneyType.Dia;
            Debug.Log(id + ", " + row[9]);
            int rewardMoney = int.TryParse(string.Concat(row[9].Where(c => !Char.IsWhiteSpace(c))), out int parsedRewardMoney2) ? parsedRewardMoney2 : 0;
            ChallengeType challengeType;
            switch (type)
            {
                case "TYPE01":
                    challengeType = ChallengeType.TYPE01;
                    string[] needFurnitureIds = needItemId.Split(new char[] { '.' });
                    for (int j = 0, cntJ = needFurnitureIds.Length; j < cntJ; j++)
                    {
                        FurnitureData furnitureData = FurnitureDataManager.Instance.GetFurnitureData(needFurnitureIds[j]);
                        if (furnitureData == null)
                        {
                            Debug.LogError($"Furniture ID not found: {needFurnitureIds[j]}");
                            continue;
                        }
                        needFurnitureIds[j] = needFurnitureIds[j].Trim();
                    }
                    Type01ChallengeData challengeData01 = new Type01ChallengeData(challenges, challengeType, id, description, needFurnitureIds, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type01ChallengeDataDic.Add(id, challengeData01);
                    dic.Add(id, challengeData01);
                    break;

                case "TYPE02":
                    challengeType = ChallengeType.TYPE02;
                    string[] needKitchenUtensilIds = needItemId.Split(new char[] { '.' });
                    for (int j = 0, cntJ = needKitchenUtensilIds.Length; j < cntJ; j++)
                    {
                        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(needKitchenUtensilIds[j]);
                        if (kitchenData == null)
                        {
                            Debug.LogError($"Kitchen Utensil ID not found: {needKitchenUtensilIds[j]}");
                            continue;
                        }
                        needKitchenUtensilIds[j] = needKitchenUtensilIds[j].Trim();
                    }
                    Type02ChallengeData challengeData02 = new Type02ChallengeData(challenges, challengeType, id, description, needKitchenUtensilIds, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type02ChallengeDataDic.Add(id, challengeData02);
                    dic.Add(id, challengeData02);
                    break;

                case "TYPE03":
                    challengeType = ChallengeType.TYPE03;
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    Type03ChallengeData challengeData03 = new Type03ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type03ChallengeDataDic.Add(id, challengeData03);
                    dic.Add(id, challengeData03);
                    break;

                case "TYPE04":
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    challengeType = ChallengeType.TYPE04;
                    Type04ChallengeData challengeData04 = new Type04ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type04ChallengeDataDic.Add(id, challengeData04);
                    dic.Add(id, challengeData04);
                    break;

                case "TYPE05":
                    challengeType = ChallengeType.TYPE05;
                    if (StaffDataManager.Instance.GetStaffData(needItemId) == null)
                    {
                        Debug.LogError($"Staff ID not found: {needItemId}");
                        continue;
                    }
                    Type05ChallengeData challengeData05 = new Type05ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type05ChallengeDataDic.Add(id, challengeData05);
                    dic.Add(id, challengeData05);
                    break;

                case "TYPE06":
                    challengeType = ChallengeType.TYPE06;
                    Type06ChallengeData challengeData06 = new Type06ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type06ChallengeDataDic.Add(id, challengeData06);
                    dic.Add(id, challengeData06);
                    break;

                case "TYPE07":
                    challengeType = ChallengeType.TYPE07;
                    if (FoodDataManager.Instance.GetFoodData(needItemId) == null)
                    {
                        Debug.LogError($"Recipe ID not found: {needItemId}");
                        continue;
                    }
                    Type07ChallengeData challengeData07 = new Type07ChallengeData(challenges, challengeType, id, description, needItemId, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type07ChallengeDataDic.Add(id, challengeData07);
                    dic.Add(id, challengeData07);
                    break;

                case "TYPE08":
                    challengeType = ChallengeType.TYPE08;
                    Type08ChallengeData challengeData08 = new Type08ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type08ChallengeDataDic.Add(id, challengeData08);
                    dic.Add(id, challengeData08);
                    break;

                case "TYPE09":
                    challengeType = ChallengeType.TYPE09;
                    Type09ChallengeData challengeData09 = new Type09ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type09ChallengeDataDic.Add(id, challengeData09);
                    dic.Add(id, challengeData09);
                    break;

                case "TYPE10":
                    challengeType = ChallengeType.TYPE10;
                    Type10ChallengeData challengeData10 = new Type10ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type10ChallengeDataDic.Add(id, challengeData10);
                    dic.Add(id, challengeData10);
                    break;

                case "TYPE11":
                    challengeType = ChallengeType.TYPE11;
                    Type11ChallengeData challengeData11 = new Type11ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type11ChallengeDataDic.Add(id, challengeData11);
                    dic.Add(id, challengeData11);
                    break;

                case "TYPE12":
                    challengeType = ChallengeType.TYPE12;
                    Type12ChallengeData challengeData12 = new Type12ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type12ChallengeDataDic.Add(id, challengeData12);
                    dic.Add(id, challengeData12);
                    break;

                case "TYPE13":
                    challengeType = ChallengeType.TYPE13;
                    Type13ChallengeData challengeData13 = new Type13ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type13ChallengeDataDic.Add(id, challengeData13);
                    dic.Add(id, challengeData13);
                    break;

                case "TYPE14":
                    challengeType = ChallengeType.TYPE14;
                    Type14ChallengeData challengeData14 = new Type14ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type14ChallengeDataDic.Add(id, challengeData14);
                    dic.Add(id, challengeData14);
                    break;

                case "TYPE15":
                    challengeType = ChallengeType.TYPE15;
                    Type15ChallengeData challengeData15 = new Type15ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type15ChallengeDataDic.Add(id, challengeData15);
                    dic.Add(id, challengeData15);
                    break;

                case "TYPE16":
                    challengeType = ChallengeType.TYPE16;
                    Type16ChallengeData challengeData16 = new Type16ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type16ChallengeDataDic.Add(id, challengeData16);
                    dic.Add(id, challengeData16);
                    break;

                case "TYPE17":
                    challengeType = ChallengeType.TYPE17;
                    Type17ChallengeData challengeData17 = new Type17ChallengeData(challenges, challengeType, id, description, needItemId, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type17ChallengeDataDic.Add(id, challengeData17);
                    dic.Add(id, challengeData17);
                    break;

                case "TYPE18":
                    challengeType = ChallengeType.TYPE18;
                    Type18ChallengeData challengeData18 = new Type18ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type18ChallengeDataDic.Add(id, challengeData18);
                    dic.Add(id, challengeData18);
                    break;

                case "TYPE19":
                    challengeType = ChallengeType.TYPE19;
                    Type19ChallengeData challengeData19 = new Type19ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type19ChallengeDataDic.Add(id, challengeData19);
                    dic.Add(id, challengeData19);
                    break;

                case "TYPE20":
                    challengeType = ChallengeType.TYPE20;
                    ERestaurantFloorType floorType = (ERestaurantFloorType)(int.Parse(needItemId) - 1);
                    Type20ChallengeData challengeData20 = new Type20ChallengeData(challenges, challengeType, id, description, floorType, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type20ChallengeDataDic.Add(id, challengeData20);
                    dic.Add(id, challengeData20);
                    break;

                case "TYPE21":
                    challengeType = ChallengeType.TYPE21;
                    Type21ChallengeData challengeData21 = new Type21ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type21ChallengeDataDic.Add(id, challengeData21);
                    dic.Add(id, challengeData21);
                    break;

                case "TYPE22":
                    challengeType = ChallengeType.TYPE22;
                    Type22ChallengeData challengeData22 = new Type22ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type22ChallengeDataDic.Add(id, challengeData22);
                    dic.Add(id, challengeData22);
                    break;

                case "TYPE23":
                    challengeType = ChallengeType.TYPE23;
                    Type23ChallengeData challengeData23 = new Type23ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type23ChallengeDataDic.Add(id, challengeData23);
                    dic.Add(id, challengeData23);
                    break;

                case "TYPE24":
                    challengeType = ChallengeType.TYPE24;
                    Type24ChallengeData challengeData24 = new Type24ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type24ChallengeDataDic.Add(id, challengeData24);
                    dic.Add(id, challengeData24);
                    break;

                case "TYPE25":
                    challengeType = ChallengeType.TYPE25;
                    Type25ChallengeData challengeData25 = new Type25ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type25ChallengeDataDic.Add(id, challengeData25);
                    dic.Add(id, challengeData25);
                    break;

                case "TYPE26":
                    challengeType = ChallengeType.TYPE26;
                    Type26ChallengeData challengeData26 = new Type26ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type26ChallengeDataDic.Add(id, challengeData26);
                    dic.Add(id, challengeData26);
                    break;

                case "TYPE28":
                    challengeType = ChallengeType.TYPE28;
                    Type28ChallengeData challengeData28 = new Type28ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type28ChallengeDataDic.Add(id, challengeData28);
                    dic.Add(id, challengeData28);
                    break;

                case "TYPE30":
                    challengeType = ChallengeType.TYPE30;
                    Type30ChallengeData challengeData30 = new Type30ChallengeData(challenges, challengeType, id, description, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type30ChallengeDataDic.Add(id, challengeData30);
                    dic.Add(id, challengeData30);
                    break;

                case "TYPE31":
                    challengeType = ChallengeType.TYPE31;
                    Type31ChallengeData challengeData31 = new Type31ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type31ChallengeDataDic.Add(id, challengeData31);
                    dic.Add(id, challengeData31);
                    break;

                case "TYPE32":
                    challengeType = ChallengeType.TYPE32;
                    Type32ChallengeData challengeData32 = new Type32ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type32ChallengeDataDic.Add(id, challengeData32);
                    dic.Add(id, challengeData32);
                    break;

                case "TYPE33":
                    challengeType = ChallengeType.TYPE33;
                    Type33ChallengeData challengeData33 = new Type33ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type33ChallengeDataDic.Add(id, challengeData33);
                    dic.Add(id, challengeData33);
                    break;

                case "TYPE34":
                    challengeType = ChallengeType.TYPE34;
                    Type34ChallengeData challengeData34 = new Type34ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type34ChallengeDataDic.Add(id, challengeData34);
                    dic.Add(id, challengeData34);
                    break;

                case "TYPE35":
                    challengeType = ChallengeType.TYPE35;
                    Type35ChallengeData challengeData35 = new Type35ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type35ChallengeDataDic.Add(id, challengeData35);
                    dic.Add(id, challengeData35);
                    break;

                case "TYPE36":
                    challengeType = ChallengeType.TYPE36;
                    Type36ChallengeData challengeData36 = new Type36ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type36ChallengeDataDic.Add(id, challengeData36);
                    dic.Add(id, challengeData36);
                    break;

                case "TYPE37":
                    challengeType = ChallengeType.TYPE37;
                    Type37ChallengeData challengeData37 = new Type37ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type37ChallengeDataDic.Add(id, challengeData37);
                    dic.Add(id, challengeData37);
                    break;

                case "TYPE38":
                    challengeType = ChallengeType.TYPE38;
                    Type38ChallengeData challengeData38 = new Type38ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type38ChallengeDataDic.Add(id, challengeData38);
                    dic.Add(id, challengeData38);
                    break;

                case "TYPE39":
                    challengeType = ChallengeType.TYPE39;
                    Type39ChallengeData challengeData39 = new Type39ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type39ChallengeDataDic.Add(id, challengeData39);
                    dic.Add(id, challengeData39);
                    break;

                case "TYPE40":
                    challengeType = ChallengeType.TYPE40;
                    Type40ChallengeData challengeData40 = new Type40ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type40ChallengeDataDic.Add(id, challengeData40);
                    dic.Add(id, challengeData40);
                    break;

                case "TYPE41":
                    challengeType = ChallengeType.TYPE41;
                    Type41ChallengeData challengeData41 = new Type41ChallengeData(challenges, challengeType, id, description, count, moneyType, rewardMoney, rewardScore, shortcutAction);
                    _type41ChallengeDataDic.Add(id, challengeData41);
                    dic.Add(id, challengeData41);
                    break;

            }
        }

        return dic;
    }

    public ChallengeData GetCurrentMainChallengeData()
    {
        foreach (var data in _mainChallengeDataDic)
        {
            if (UserInfo.GetIsClearChallenge(data.Key))
                continue;

            return data.Value;
        }

        return null;
    }

    public List<ChallengeData> GetAllTimeChallenge()
    {
        return _allTimeChallengeList;
    }

    public List<ChallengeData> GetDailyChallenge()
    {
        return _dailyChallengeList;
    }

    public List<ChallengeData> GetWeeklyChallenge()
    {
        return _weeklyChallengeList;
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
                    if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data01.NeedFurnitureIds[i]))
                        percent += 1f / cnt;
                }
                return percent;

            case ChallengeType.TYPE02:
                Type02ChallengeData data02 = (Type02ChallengeData)data;
                for (int i = 0, cnt = data02.NeedKitchenUtensilId.Length; i < cnt; i++)
                {
                    if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data02.NeedKitchenUtensilId[i]))
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
                if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data05.NeedStaffId))
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
                long socre = UserInfo.Score;
                return socre == 0 ? 0 : Math.Min(1, (float)socre / data08.Rank);

            case ChallengeType.TYPE09:
                Type09ChallengeData data09 = (Type09ChallengeData)data;
                int customerCount = UserInfo.TotalCumulativeCustomerCount;
                return customerCount == 0 ? 0 : Math.Min(1, (float)customerCount / data09.CustomerCount);

            case ChallengeType.TYPE10:
                Type10ChallengeData data10 = (Type10ChallengeData)data;
                int visitedCustomerCount = UserInfo.GetVisitedCustomerTypeCount();
                return visitedCustomerCount == 0 ? 0 : Math.Min(1, (float)visitedCustomerCount / data10.CustomerCount);

            case ChallengeType.TYPE11:
                Type11ChallengeData data11 = (Type11ChallengeData)data;
                long moneyCount = UserInfo.TotalAddMoney;
                return moneyCount == 0 ? 0 : Math.Min(1, (float)moneyCount / data11.MoneyCount);

            case ChallengeType.TYPE12:
                Type12ChallengeData data12 = (Type12ChallengeData)data;
                int promotionCount = UserInfo.PromotionCount;
                return promotionCount == 0 ? 0 : Math.Min(1, (float)promotionCount / data12.PromotionCount);

            case ChallengeType.TYPE13:
                Type13ChallengeData data13 = (Type13ChallengeData)data;
                int furnitureAndKitchenUtensilCount = UserInfo.GetFurnitureAndKitchenUtensilCount(UserInfo.CurrentStage);
                return furnitureAndKitchenUtensilCount == 0 ? 0 : Math.Min(1, (float)furnitureAndKitchenUtensilCount / data13.Count);

            case ChallengeType.TYPE14:
                Type14ChallengeData data14 = (Type14ChallengeData)data;
                int collectFurnitureSetCount = UserInfo.GetCollectFurnitureSetDataList(UserInfo.CurrentStage).Count;
                return collectFurnitureSetCount == 0 ? 0 : Math.Min(1, (float)collectFurnitureSetCount / data14.Count);

            case ChallengeType.TYPE15:
                Type15ChallengeData data15 = (Type15ChallengeData)data;
                int collectKitchenUtensilSetCount = UserInfo.GetCollectKitchenUtensilSetDataList(UserInfo.CurrentStage).Count;
                return collectKitchenUtensilSetCount == 0 ? 0 : Math.Min(1, (float)collectKitchenUtensilSetCount / data15.Count);

            case ChallengeType.TYPE16:
                Type16ChallengeData data16 = (Type16ChallengeData)data;

                List<int> _equipSetList = new List<int>();
                for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
                {
                    ERestaurantFloorType floor = (ERestaurantFloorType)i;
                    int equipFurnitureSetCount = 0;
                    for (int j = 0, cntJ = (int)FurnitureType.Length; j < cntJ; ++j)
                    {
                        FurnitureType type = (FurnitureType)j;
                        FurnitureData furnitureData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floor, type);

                        if (furnitureData == null || !furnitureData.SetId.Equals(data16.SetId))
                            continue;

                        equipFurnitureSetCount++;
                    }
                    _equipSetList.Add(equipFurnitureSetCount);
                }
                int maxFurnitureCount = _equipSetList.Max();
                return maxFurnitureCount == 0 ? 0 : Math.Min(1, (float)maxFurnitureCount / ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT);

            case ChallengeType.TYPE17:
                Type17ChallengeData data17 = (Type17ChallengeData)data;
                List<int> _equipKitchenSetList = new List<int>();
                for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
                {
                    ERestaurantFloorType floor = (ERestaurantFloorType)i;
                    int equipKitchenSetCount = 0;
                    for (int j = 0, cntJ = (int)KitchenUtensilType.Length; j < cntJ; ++j)
                    {
                        KitchenUtensilType type = (KitchenUtensilType)j;
                        KitchenUtensilData kitchenData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floor, type);

                        if (kitchenData == null || !kitchenData.SetId.Equals(data17.SetId))
                            continue;

                        equipKitchenSetCount++;
                    }
                    _equipKitchenSetList.Add(equipKitchenSetCount);
                }
                int maxKitchenCount = _equipKitchenSetList.Max();
                return maxKitchenCount == 0 ? 0 : Math.Min(1, (float)maxKitchenCount / ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT);

            case ChallengeType.TYPE18:
                Type18ChallengeData data18 = (Type18ChallengeData)data;
                int giveFurnitureCount = UserInfo.GetGiveFurnitureCount(UserInfo.CurrentStage);
                return giveFurnitureCount == 0 ? 0 : Math.Min(1, (float)giveFurnitureCount / data18.Count);

            case ChallengeType.TYPE19:
                Type19ChallengeData data19 = (Type19ChallengeData)data;
                int giveKitchenUntensilCount = UserInfo.GetGiveKitchenUtensilCount(UserInfo.CurrentStage);
                return giveKitchenUntensilCount == 0 ? 0 : Math.Min(1, (float)giveKitchenUntensilCount / data19.Count);

            case ChallengeType.TYPE20:
                Type20ChallengeData data20 = (Type20ChallengeData)data;
                int openFloorCount = UserInfo.IsFloorValid(UserInfo.CurrentStage, data20.FloorType) ? 1 : 0;
                return openFloorCount;

            case ChallengeType.TYPE21:
                Type21ChallengeData data21 = (Type21ChallengeData)data;
                int totalVisitSpecialCustomerCount = UserInfo.TotalVisitSpecialCustomerCount;
                return totalVisitSpecialCustomerCount == 0 ? 0 : Math.Min(1, (float)totalVisitSpecialCustomerCount / data21.Count);

            case ChallengeType.TYPE22:
                Type22ChallengeData data22 = (Type22ChallengeData)data;
                int totalExterminationGatecrasherCustomer1Count = UserInfo.TotalExterminationGatecrasherCustomer1Count;
                return totalExterminationGatecrasherCustomer1Count == 0 ? 0 : Math.Min(1, (float)totalExterminationGatecrasherCustomer1Count / data22.Count);

            case ChallengeType.TYPE23:
                Type23ChallengeData data23 = (Type23ChallengeData)data;
                int totalExterminationGatecrasherCustomer2Count = UserInfo.TotalExterminationGatecrasherCustomer2Count;
                return totalExterminationGatecrasherCustomer2Count == 0 ? 0 : Math.Min(1, (float)totalExterminationGatecrasherCustomer2Count / data23.Count);

            case ChallengeType.TYPE24:
                Type24ChallengeData data24 = (Type24ChallengeData)data;
                int totalExterminationGatecrasherCustomerCount = UserInfo.TotalExterminationGatecrasherCustomer1Count + UserInfo.TotalExterminationGatecrasherCustomer2Count;
                return totalExterminationGatecrasherCustomerCount == 0 ? 0 : Math.Min(1, (float)totalExterminationGatecrasherCustomerCount / data24.Count);

            case ChallengeType.TYPE25:
                Type25ChallengeData data25 = (Type25ChallengeData)data;
                int totalUseGachaMachineCount = UserInfo.TotalUseGachaMachineCount;
                return totalUseGachaMachineCount == 0 ? 0 : Math.Min(1, (float)totalUseGachaMachineCount / data25.Count);

            case ChallengeType.TYPE26:
                Type26ChallengeData data26 = (Type26ChallengeData)data;
                int totalGachaMachineTypeCount = UserInfo.GetGiveGachaItemDic().Count;
                return totalGachaMachineTypeCount == 0 ? 0 : Math.Min(1, (float)totalGachaMachineTypeCount / data26.Count);

            case ChallengeType.TYPE28:
                Type28ChallengeData data28 = (Type28ChallengeData)data;
                int cleanCount = UserInfo.TotalCleanCount;
                return cleanCount == 0 ? 0 : Math.Min(1, (float)cleanCount / data28.Count);

            case ChallengeType.TYPE30:
                int dailyClearCount = UserInfo.GetClearDailyChallengeCount();
                DebugLog.Log($"{((Type30ChallengeData)data).Id} {dailyClearCount} / {_dailyChallengeDataDic.Count - 1}");
                return GetChallengeAchievementRate(dailyClearCount, _dailyChallengeDataDic.Count - 1);

            case ChallengeType.TYPE31:
                Type31ChallengeData data31 = (Type31ChallengeData)data;
                int dailyCustomerCount = UserInfo.DailyCumulativeCustomerCount;
                return GetChallengeAchievementRate(dailyCustomerCount, data31.Count);

            case ChallengeType.TYPE32:
                Type32ChallengeData data32 = (Type32ChallengeData)data;
                long dailyAddMoney = UserInfo.DailyAddMoney;
                return GetChallengeAchievementRate(dailyAddMoney, data32.Count);

            case ChallengeType.TYPE33:
                Type33ChallengeData data33 = (Type33ChallengeData)data;
                int dailyCookCount = UserInfo.DailyCookCount;
                return GetChallengeAchievementRate(dailyCookCount, data33.Count);

            case ChallengeType.TYPE34:
                Type34ChallengeData data34 = (Type34ChallengeData)data;
                int dailyCleanCount = UserInfo.DailyCleanCount;
                return GetChallengeAchievementRate(dailyCleanCount, data34.Count);

            case ChallengeType.TYPE35:
                Type35ChallengeData data35 = (Type35ChallengeData)data;
                int dailyAdvertisingViewCount = UserInfo.DailyAdvertisingViewCount;
                return GetChallengeAchievementRate(dailyAdvertisingViewCount, data35.Count);

            case ChallengeType.TYPE36:
                int weeklyClearCount = UserInfo.GetClearWeeklyChallengeCount();
                return GetChallengeAchievementRate(weeklyClearCount, _weeklyChallengeDataDic.Count - 1);

            case ChallengeType.TYPE37:
                Type37ChallengeData data37 = (Type37ChallengeData)data;
                int weeklyCustomerCount = UserInfo.WeeklyCumulativeCustomerCount;
                return GetChallengeAchievementRate(weeklyCustomerCount, data37.Count);

            case ChallengeType.TYPE38:
                Type38ChallengeData data38 = (Type38ChallengeData)data;
                long weeklyAddMoney = UserInfo.WeeklyAddMoney;
                return GetChallengeAchievementRate(weeklyAddMoney, data38.Count);

            case ChallengeType.TYPE39:
                Type39ChallengeData data39 = (Type39ChallengeData)data;
                int weeklyCookCount = UserInfo.WeeklyCookCount;
                return GetChallengeAchievementRate(weeklyCookCount, data39.Count);

            case ChallengeType.TYPE40:
                Type40ChallengeData data40 = (Type40ChallengeData)data;
                int weeklyCleanCount = UserInfo.WeeklyCleanCount;
                return GetChallengeAchievementRate(weeklyCleanCount, data40.Count);

            case ChallengeType.TYPE41:
                Type41ChallengeData data41 = (Type41ChallengeData)data;
                int weeklyGatecrasherCustomerCount = UserInfo.WeeklyExterminationGatecrasherCustomerCount;
                return GetChallengeAchievementRate(weeklyGatecrasherCustomerCount, data41.Count);

            default: return 0;
        }
    }


    public string GetChallengeCountStr(ChallengeData data)
    {
        int clearCount = 0;
        switch (data.Type)
        {
            case ChallengeType.TYPE01:
                Type01ChallengeData data01 = (Type01ChallengeData)data;
                for (int i = 0, cnt = data01.NeedFurnitureIds.Length; i < cnt; i++)
                {
                    if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data01.NeedFurnitureIds[i]))
                        clearCount++;
                }
                return $"{clearCount} / {data01.NeedFurnitureIds.Length}";

            case ChallengeType.TYPE02:
                Type02ChallengeData data02 = (Type02ChallengeData)data;
                for (int i = 0, cnt = data02.NeedKitchenUtensilId.Length; i < cnt; i++)
                {
                    if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data02.NeedKitchenUtensilId[i]))
                        clearCount++;
                }
                return $"{clearCount} / {data02.NeedKitchenUtensilId.Length}";

            case ChallengeType.TYPE03:
                Type03ChallengeData data03 = (Type03ChallengeData)data;
                if (UserInfo.IsGiveRecipe(data03.BuyRecipeId))
                    clearCount++;
                return $"{clearCount} / 1";

            case ChallengeType.TYPE04:
                Type04ChallengeData data04 = (Type04ChallengeData)data;
                if (UserInfo.IsGiveRecipe(data04.NeedRecipeId))
                    clearCount++;
                return $"{clearCount} / 1";

            case ChallengeType.TYPE05:
                Type05ChallengeData data05 = (Type05ChallengeData)data;
                if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data05.NeedStaffId))
                    clearCount++;
                return $"{clearCount} / 1";

            case ChallengeType.TYPE06:
                Type06ChallengeData data06 = (Type06ChallengeData)data;
                int recipeCount = UserInfo.GetRecipeCount();
                return $"{recipeCount} / {data06.RecipeCount}";

            case ChallengeType.TYPE07:
                Type07ChallengeData data07 = (Type07ChallengeData)data;
                int cookCount = UserInfo.GetCookCount(data07.Id);
                return $"{cookCount} / {data07.CookCount}";

            case ChallengeType.TYPE08:
                Type08ChallengeData data08 = (Type08ChallengeData)data;
                long socre = UserInfo.Score;
                return $"{Utility.ConvertToMoney(socre)} / {Utility.ConvertToMoney(data08.Rank)}";

            case ChallengeType.TYPE09:
                Type09ChallengeData data09 = (Type09ChallengeData)data;
                int customerCount = UserInfo.TotalCumulativeCustomerCount;
                return $"{customerCount} / {data09.CustomerCount}";

            case ChallengeType.TYPE10:
                Type10ChallengeData data10 = (Type10ChallengeData)data;
                int visitedCustomerCount = UserInfo.GetVisitedCustomerTypeCount();
                return $"{visitedCustomerCount} / {data10.CustomerCount}";

            case ChallengeType.TYPE11:
                Type11ChallengeData data11 = (Type11ChallengeData)data;
                long moneyCount = UserInfo.TotalAddMoney;
                return $"{Utility.ConvertToMoney(moneyCount)} / {Utility.ConvertToMoney(data11.MoneyCount)}";

            case ChallengeType.TYPE12:
                Type12ChallengeData data12 = (Type12ChallengeData)data;
                int promotionCount = UserInfo.PromotionCount;
                return $"{promotionCount} / {data12.PromotionCount}";

            case ChallengeType.TYPE13:
                Type13ChallengeData data13 = (Type13ChallengeData)data;
                int furnitureAndKitchenUtensilCount = UserInfo.GetFurnitureAndKitchenUtensilCount(UserInfo.CurrentStage);
                return $"{furnitureAndKitchenUtensilCount} / {data13.Count}";

            case ChallengeType.TYPE14:
                Type14ChallengeData data14 = (Type14ChallengeData)data;
                int collectFurnitureSetCount = UserInfo.GetCollectFurnitureSetDataList(UserInfo.CurrentStage).Count;
                return $"{collectFurnitureSetCount} / {data14.Count}";

            case ChallengeType.TYPE15:
                Type15ChallengeData data15 = (Type15ChallengeData)data;
                int collectKitchenUtensilSetCount = UserInfo.GetCollectKitchenUtensilSetDataList(UserInfo.CurrentStage).Count;
                return $"{collectKitchenUtensilSetCount} / {data15.Count}";

            case ChallengeType.TYPE16:
                Type16ChallengeData data16 = (Type16ChallengeData)data;

                List<int> _equipSetList = new List<int>();
                for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
                {
                    ERestaurantFloorType floor = (ERestaurantFloorType)i;
                    int equipFurnitureSetCount = 0;
                    for (int j = 0, cntJ = (int)FurnitureType.Length; j < cntJ; ++j)
                    {
                        FurnitureType type = (FurnitureType)j;
                        FurnitureData furnitureData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floor, type);

                        if (furnitureData == null || !furnitureData.SetId.Equals(data16.SetId))
                            continue;

                        equipFurnitureSetCount++;
                    }
                    _equipSetList.Add(equipFurnitureSetCount);
                }
                int maxFurnitureCount = _equipSetList.Max();
                return $"{maxFurnitureCount} / {ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT}";

            case ChallengeType.TYPE17:
                Type17ChallengeData data17 = (Type17ChallengeData)data;
                List<int> _equipKitchenSetList = new List<int>();
                for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
                {
                    ERestaurantFloorType floor = (ERestaurantFloorType)i;
                    int equipKitchenSetCount = 0;
                    for (int j = 0, cntJ = (int)KitchenUtensilType.Length; j < cntJ; ++j)
                    {
                        KitchenUtensilType type = (KitchenUtensilType)j;
                        KitchenUtensilData kitchenData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floor, type);

                        if (kitchenData == null || !kitchenData.SetId.Equals(data17.SetId))
                            continue;

                        equipKitchenSetCount++;
                    }
                    _equipKitchenSetList.Add(equipKitchenSetCount);
                }
                int maxKitchenCount = _equipKitchenSetList.Max();
                return $"{maxKitchenCount} / {ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT}";

            case ChallengeType.TYPE18:
                Type18ChallengeData data18 = (Type18ChallengeData)data;
                int giveFurnitureCount = UserInfo.GetGiveFurnitureCount(UserInfo.CurrentStage);
                return $"{giveFurnitureCount} / {data18.Count}";

            case ChallengeType.TYPE19:
                Type19ChallengeData data19 = (Type19ChallengeData)data;
                int giveKitchenUntensilCount = UserInfo.GetGiveKitchenUtensilCount(UserInfo.CurrentStage);
                return $"{giveKitchenUntensilCount} / {data19.Count}";

            case ChallengeType.TYPE20:
                Type20ChallengeData data20 = (Type20ChallengeData)data;
                int openFloorCount = UserInfo.IsFloorValid(UserInfo.CurrentStage, data20.FloorType) ? 1 : 0;
                return $"{openFloorCount} / 1";

            case ChallengeType.TYPE21:
                Type21ChallengeData data21 = (Type21ChallengeData)data;
                int totalVisitSpecialCustomerCount = UserInfo.TotalVisitSpecialCustomerCount;
                return $"{totalVisitSpecialCustomerCount} / {data21.Count}";

            case ChallengeType.TYPE22:
                Type22ChallengeData data22 = (Type22ChallengeData)data;
                int totalExterminationGatecrasherCustomer1Count = UserInfo.TotalExterminationGatecrasherCustomer1Count;
                return $"{totalExterminationGatecrasherCustomer1Count} / {data22.Count}";

            case ChallengeType.TYPE23:
                Type23ChallengeData data23 = (Type23ChallengeData)data;
                int totalExterminationGatecrasherCustomer2Count = UserInfo.TotalExterminationGatecrasherCustomer2Count;
                return $"{totalExterminationGatecrasherCustomer2Count} / {data23.Count}";

            case ChallengeType.TYPE24:
                Type24ChallengeData data24 = (Type24ChallengeData)data;
                int totalExterminationGatecrasherCustomerCount = UserInfo.TotalExterminationGatecrasherCustomer1Count + UserInfo.TotalExterminationGatecrasherCustomer2Count;
                return $"{totalExterminationGatecrasherCustomerCount} / {data24.Count}";

            case ChallengeType.TYPE25:
                Type25ChallengeData data25 = (Type25ChallengeData)data;
                int totalUseGachaMachineCount = UserInfo.TotalUseGachaMachineCount;
                return $"{totalUseGachaMachineCount} / {data25.Count}";

            case ChallengeType.TYPE26:
                Type26ChallengeData data26 = (Type26ChallengeData)data;
                int totalGachaMachineTypeCount = UserInfo.GetGiveGachaItemDic().Count;
                return $"{totalGachaMachineTypeCount} / {data26.Count}";

            case ChallengeType.TYPE28:
                Type28ChallengeData data28 = (Type28ChallengeData)data;
                int cleanCount = UserInfo.TotalCleanCount;
                return $"{cleanCount} / {data28.Count}";

            case ChallengeType.TYPE30:
                int dailyClearCount = UserInfo.GetClearDailyChallengeCount();
                DebugLog.Log($"{((Type30ChallengeData)data).Id} {dailyClearCount} / {_dailyChallengeDataDic.Count - 1}");
                return $"{dailyClearCount} / {_dailyChallengeDataDic.Count - 1}";

            case ChallengeType.TYPE31:
                Type31ChallengeData data31 = (Type31ChallengeData)data;
                int dailyCustomerCount = UserInfo.DailyCumulativeCustomerCount;
                return $"{dailyCustomerCount} / {data31.Count}";

            case ChallengeType.TYPE32:
                Type32ChallengeData data32 = (Type32ChallengeData)data;
                long dailyAddMoney = UserInfo.DailyAddMoney;
                return $"{Utility.ConvertToMoney(dailyAddMoney)} / {Utility.ConvertToMoney(data32.Count)}";

            case ChallengeType.TYPE33:
                Type33ChallengeData data33 = (Type33ChallengeData)data;
                int dailyCookCount = UserInfo.DailyCookCount;
                return $"{dailyCookCount} / {data33.Count}";

            case ChallengeType.TYPE34:
                Type34ChallengeData data34 = (Type34ChallengeData)data;
                int dailyCleanCount = UserInfo.DailyCleanCount;
                return $"{dailyCleanCount} / {data34.Count}";

            case ChallengeType.TYPE35:
                Type35ChallengeData data35 = (Type35ChallengeData)data;
                int dailyAdvertisingViewCount = UserInfo.DailyAdvertisingViewCount;
                return $"{dailyAdvertisingViewCount} / {data35.Count}";

            case ChallengeType.TYPE36:
                int weeklyClearCount = UserInfo.GetClearWeeklyChallengeCount();
                return $"{weeklyClearCount} / {_weeklyChallengeDataDic.Count - 1}";

            case ChallengeType.TYPE37:
                Type37ChallengeData data37 = (Type37ChallengeData)data;
                int weeklyCustomerCount = UserInfo.WeeklyCumulativeCustomerCount;
                return $"{weeklyCustomerCount} / {data37.Count}";

            case ChallengeType.TYPE38:
                Type38ChallengeData data38 = (Type38ChallengeData)data;
                long weeklyAddMoney = UserInfo.WeeklyAddMoney;
                return $"{Utility.ConvertToMoney(weeklyAddMoney)} / {Utility.ConvertToMoney(data38.Count)}";

            case ChallengeType.TYPE39:
                Type39ChallengeData data39 = (Type39ChallengeData)data;
                int weeklyCookCount = UserInfo.WeeklyCookCount;
                return $"{weeklyCookCount} / {data39.Count}";

            case ChallengeType.TYPE40:
                Type40ChallengeData data40 = (Type40ChallengeData)data;
                int weeklyCleanCount = UserInfo.WeeklyCleanCount;
                return $"{weeklyCleanCount} / {data40.Count}";

            case ChallengeType.TYPE41:
                Type41ChallengeData data41 = (Type41ChallengeData)data;
                int weeklyGatecrasherCustomerCount = UserInfo.WeeklyExterminationGatecrasherCustomerCount;
                return $"{weeklyGatecrasherCustomerCount} / {data41.Count}";

            default: return string.Empty;
        }
    }

    public void UpdateChallengeByChallenges(Challenges challenges)
    {
        switch (challenges)
        {
            case Challenges.Daily:
                OnDailyChallengeUpdateHandler?.Invoke();
                break;

            case Challenges.AllTime:
                OnAllTimeChallengeUpdateHandler?.Invoke();
                break;

            case Challenges.Weekly:
                OnWeeklyChallengeUpdateHandler?.Invoke();
                break;

            case Challenges.Main:
                OnMainChallengeUpdateHandler?.Invoke();
                break;
        }
    }



    private float GetChallengeAchievementRate(int currentValue, int targetValue)
    {
        return currentValue == 0 ? 0 : Math.Min(1, (float)currentValue / targetValue);
    }

    private float GetChallengeAchievementRate(long currentValue, long targetValue)
    {
        return currentValue == 0 ? 0 : Math.Min(1, (float)currentValue / targetValue);
    }


    private BindData<UnityAction> GetShortCutAction(string shortCutType)
    {
        shortCutType = string.Concat(shortCutType.Where(c => !Char.IsWhiteSpace(c)));
        switch (shortCutType)
        {
            case "ShortCut01":
                return DataBind.GetUnityActionBindData("ShowRestaurant");

            case "ShortCut02":
                return DataBind.GetUnityActionBindData("ShowKitchen");

            case "ShortCut03":
                return DataBind.GetUnityActionBindData("ShowFurnitureTab");

            case "ShortCut04":
                return DataBind.GetUnityActionBindData("ShowKitchenTab");

            case "ShortCut05":
                return DataBind.GetUnityActionBindData("ShowStaffTab");

            case "ShortCut06":
                return DataBind.GetUnityActionBindData("ShowRecipeTab");

            case "ShortCut07":
                return DataBind.GetUnityActionBindData("ShowManagementUI");

            case "ShortCut08":
                return DataBind.GetUnityActionBindData("ShowChallengeUI");

            case "ShortCut09":
                return DataBind.GetUnityActionBindData("UIPictorialBook");

            case "ShortCut10":
                return DataBind.GetUnityActionBindData("ShowGachaUI");

            case "ShortCut12":
                return DataBind.GetUnityActionBindData("ShowAttendanceUI");

            case "ShortCut13":
                return DataBind.GetUnityActionBindData("ShowSettingUI");

            case "ShortCut14":
                return DataBind.GetUnityActionBindData("ShowFurnitureTable1");

            case "ShortCut15":
                return DataBind.GetUnityActionBindData("ShowFurnitureTable2");

            case "ShortCut16":
                return DataBind.GetUnityActionBindData("ShowFurnitureTable3");

            case "ShortCut17":
                return DataBind.GetUnityActionBindData("ShowFurnitureTable4");

            case "ShortCut18":
                return DataBind.GetUnityActionBindData("ShowFurnitureTable5");

            case "ShortCut19":
                return DataBind.GetUnityActionBindData("ShowFurnitureCounter");

            case "ShortCut20":
                return DataBind.GetUnityActionBindData("ShowFurnitureRack");

            case "ShortCut21":
                return DataBind.GetUnityActionBindData("ShowFurnitureFrame");

            case "ShortCut22":
                return DataBind.GetUnityActionBindData("ShowFurnitureFlower");

            case "ShortCut23":
                return DataBind.GetUnityActionBindData("ShowFurnitureAcc");

            case "ShortCut24":
                return DataBind.GetUnityActionBindData("ShowFurnitureWallpaper");

            case "ShortCut25":
                return DataBind.GetUnityActionBindData("ShowKitchenBurner1");

            case "ShortCut26":
                return DataBind.GetUnityActionBindData("ShowKitchenBurner2");

            case "ShortCut27":
                return DataBind.GetUnityActionBindData("ShowKitchenBurner3");

            case "ShortCut28":
                return DataBind.GetUnityActionBindData("ShowKitchenBurner4");

            case "ShortCut29":
                return DataBind.GetUnityActionBindData("ShowKitchenBurner5");

            case "ShortCut30":
                return DataBind.GetUnityActionBindData("ShowKitchenFridge");

            case "ShortCut31":
                return DataBind.GetUnityActionBindData("ShowKitchenCabinet");

            case "ShortCut32":
                return DataBind.GetUnityActionBindData("ShowKitchenWindow");

            case "ShortCut33":
                return DataBind.GetUnityActionBindData("ShowKitchenSink");

            case "ShortCut34":
                return DataBind.GetUnityActionBindData("ShowKitchenKitchenrack");

            case "ShortCut35":
                return DataBind.GetUnityActionBindData("ShowKitchenCookingTools");

            case "ShortCut36":
                return DataBind.GetUnityActionBindData("ShowFurnitureWallpaper");

            case "ShortCut37":
                return DataBind.GetUnityActionBindData("ShowStaffManager");

            case "ShortCut38":
                return DataBind.GetUnityActionBindData("ShowStaffMarketer");

            case "ShortCut39":
                return DataBind.GetUnityActionBindData("ShowStaffWaiter");

            case "ShortCut40":
                return DataBind.GetUnityActionBindData("ShowStaffWaiter");

            case "ShortCut41":
                return DataBind.GetUnityActionBindData("ShowStaffCleaner");

            case "ShortCut42":
                return DataBind.GetUnityActionBindData("ShowStaffGuard");

            case "ShortCut43":
                return DataBind.GetUnityActionBindData("ShowStaffChef");

            default:
                return DataBind.GetUnityActionBindData("PopUI");
        }
    }


    private void Type01ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        foreach (Type01ChallengeData data in _type01ChallengeDataDic.Values)
        {
            //DebugLog.Log($"Type01ChallengeCheck : {data.Id}");
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            bool _isGives = true;
            for (int i = 0, cnt = data.NeedFurnitureIds.Length; i < cnt; i++)
            {
                //DebugLog.Log($"NeedFurnitureId : {data.NeedFurnitureIds[i]}");
                if (!UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data.NeedFurnitureIds[i]))
                {
                    //DebugLog.Log($"Not Give Furniture : {data.NeedFurnitureIds[i]}");
                    _isGives = false;
                    break;
                }

            }

            if (!_isGives) continue;

            //DebugLog.Log($"Challenge Done : {data.Id}");
            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE01);
    }

    private void Type02ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        foreach (Type02ChallengeData data in _type02ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            bool _isGives = true;

            for (int i = 0, cnt = data.NeedKitchenUtensilId.Length; i < cnt; i++)
            {
                if (!UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data.NeedKitchenUtensilId[i]))
                {
                    _isGives = false;
                    break;
                }
            }

            if (!_isGives) continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE02);
    }

    private void Type03ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type03ChallengeData data in _type03ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.BuyRecipeId))
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE03);
    }


    private void Type04ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type04ChallengeData data in _type04ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveRecipe(data.NeedRecipeId))
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE04);
    }


    private void Type05ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type05ChallengeData data in _type05ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (!UserInfo.IsGiveStaff(UserInfo.CurrentStage, data.NeedStaffId))
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE05);
    }

    private void Type06ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type06ChallengeData data in _type06ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetRecipeCount() < data.RecipeCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE06);
    }

    private void Type07ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type07ChallengeData data in _type07ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetCookCount(data.RecipeId) < data.CookCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE07);
    }

    private void Type08ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type08ChallengeData data in _type08ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.Score < data.Rank)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE08);
    }

    private void Type09ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type09ChallengeData data in _type09ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.TotalCumulativeCustomerCount < data.CustomerCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE09);
    }


    private void Type10ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type10ChallengeData data in _type10ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.GetVisitedCustomerTypeCount() < data.CustomerCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE10);
    }

    private void Type11ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type11ChallengeData data in _type11ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.TotalAddMoney < data.MoneyCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE11);
    }

    private void Type12ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type12ChallengeData data in _type12ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.PromotionCount < data.PromotionCount)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);
          
        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE12);
    }

    private void Type13ChallengeCheck()
    {
        int furnitureCount = UserInfo.GetFurnitureAndKitchenUtensilCount(UserInfo.CurrentStage);
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type13ChallengeData data in _type13ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (furnitureCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE13);
    }

    private void Type14ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        int collectFurnitureSetCount = UserInfo.GetCollectFurnitureSetDataList(UserInfo.CurrentStage).Count;
        foreach (Type14ChallengeData data in _type14ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;


            if (collectFurnitureSetCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE14);
    }

    private void Type15ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        int collectKitchenUtensilSetCount = UserInfo.GetCollectKitchenUtensilSetDataList(UserInfo.CurrentStage).Count;
        foreach (Type15ChallengeData data in _type15ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (collectKitchenUtensilSetCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE15);
    }

    private void Type16ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        Dictionary<ERestaurantFloorType, List<string>> equipSetDataDic = new Dictionary<ERestaurantFloorType, List<string>>();
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            equipSetDataDic.Add(floor, new List<string>());
            for (int j = 0, cntJ = (int)FurnitureType.Length; j < cntJ; ++j)
            {
                FurnitureType type = (FurnitureType)j;
                FurnitureData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floor, type);

                if (data == null)
                    continue;

                equipSetDataDic[floor].Add(data.SetId);
            }
        }

        foreach (Type16ChallengeData data in _type16ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            int maxSameSetIdCount = 0;

            foreach (var kvp in equipSetDataDic)
            {
                int count = kvp.Value.Count(setId => setId == data.SetId);
                maxSameSetIdCount = Mathf.Max(maxSameSetIdCount, count);
            }

            if (maxSameSetIdCount < ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE16);
    }


    private void Type17ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        Dictionary<ERestaurantFloorType, Dictionary<string, int>> equipSetDataDic = new Dictionary<ERestaurantFloorType, Dictionary<string, int>>();

        // 층별로 주방 기구 데이터 저장
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            equipSetDataDic[floor] = new Dictionary<string, int>();

            for (int j = 0, cntJ = (int)KitchenUtensilType.Length; j < cntJ; ++j)
            {
                KitchenUtensilType type = (KitchenUtensilType)j;
                KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floor, type);

                if (data == null)
                    continue;

                if (!equipSetDataDic[floor].ContainsKey(data.SetId))
                    equipSetDataDic[floor][data.SetId] = 0;

                equipSetDataDic[floor][data.SetId]++;
            }
        }

        foreach (Type17ChallengeData data in _type17ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            int maxSameSetIdCount = 0;

            // 층별로 data.SetId 개수 확인 후, 가장 큰 값 찾기
            foreach (var kvp in equipSetDataDic)
            {
                if (kvp.Value.TryGetValue(data.SetId, out int count))
                {
                    maxSameSetIdCount = Mathf.Max(maxSameSetIdCount, count);
                }
            }

            // 가장 많은 동일한 SetId 개수가 설정 값보다 작다면 스킵
            if (maxSameSetIdCount < ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT)
                continue;

            // 조건 충족 시 해당 챌린지 활성화
            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }

            UserInfo.DoneChallenge(data);
        }

        // 챌린지 업데이트
        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE17);
    }


    private void Type18ChallengeCheck()
    {
        int giveFurnitureCount = UserInfo.GetGiveFurnitureCount(UserInfo.CurrentStage);
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type18ChallengeData data in _type18ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (giveFurnitureCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE18);
    }


    private void Type19ChallengeCheck()
    {
        int giveKitchenUtensilCount = UserInfo.GetGiveKitchenUtensilCount(UserInfo.CurrentStage);
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type19ChallengeData data in _type19ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (giveKitchenUtensilCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE19);
    }

        private void Type20ChallengeCheck()
    {
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type20ChallengeData data in _type20ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (UserInfo.IsFloorValid(UserInfo.CurrentStage, data.FloorType))
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE19);
    }



    private void Type21ChallengeCheck()
    {
        int visitSpecialCustomerCount = UserInfo.TotalVisitSpecialCustomerCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type21ChallengeData data in _type21ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (visitSpecialCustomerCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE21);
    }


    private void Type22ChallengeCheck()
    {
        int exterminationGatecrasherCustomer1Count = UserInfo.TotalExterminationGatecrasherCustomer1Count;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type22ChallengeData data in _type22ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (exterminationGatecrasherCustomer1Count < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE22);
    }


    private void Type23ChallengeCheck()
    {
        int exterminationGatecrasherCustomer2Count = UserInfo.TotalExterminationGatecrasherCustomer2Count;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type23ChallengeData data in _type23ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (exterminationGatecrasherCustomer2Count < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE23);
    }

    private void Type24ChallengeCheck()
    {
        int exterminationGatecrasherCustomerCount = UserInfo.TotalExterminationGatecrasherCustomer1Count + UserInfo.TotalExterminationGatecrasherCustomer2Count;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type24ChallengeData data in _type24ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (exterminationGatecrasherCustomerCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE24);
    }

    private void Type25ChallengeCheck()
    {
        int totalUseGachaMachineCount = UserInfo.TotalUseGachaMachineCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type25ChallengeData data in _type25ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (totalUseGachaMachineCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE25);
    }

    private void Type26ChallengeCheck()
    {
        int totalGachaMachineTypeCount = UserInfo.GetGiveGachaItemDic().Count;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type26ChallengeData data in _type26ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (totalGachaMachineTypeCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE26);
    }



    private void Type28ChallengeCheck()
    {
        int cleanCount = UserInfo.TotalCleanCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type28ChallengeData data in _type28ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (cleanCount < data.Count)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE28);
    }

    private void Type30ChallengeCheck()
    {
        int count = UserInfo.GetClearDailyChallengeCount();
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        foreach (Type30ChallengeData data in _type30ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(count, _dailyChallengeDataDic.Count - 1) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE30);
    }




    private void Type31ChallengeCheck()
    {
        int dailyCustomerCount = UserInfo.DailyCumulativeCustomerCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type31ChallengeData data in _type31ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCustomerCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE31);
    }


    private void Type32ChallengeCheck()
    {
        long dailyAddMoney = UserInfo.DailyAddMoney;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type32ChallengeData data in _type32ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyAddMoney, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE32);
    }


    private void Type33ChallengeCheck()
    {
        int dailyCookCount = UserInfo.DailyCookCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type33ChallengeData data in _type33ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCookCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE33);
    }

    private void Type34ChallengeCheck()
    {
        int dailyCleanCount = UserInfo.DailyCleanCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type34ChallengeData data in _type34ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyCleanCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE34);
    }


    private void Type35ChallengeCheck()
    {
        int dailyAdvertisingViewCount = UserInfo.DailyAdvertisingViewCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type35ChallengeData data in _type35ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(dailyAdvertisingViewCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE35);
    }

    private void Type36ChallengeCheck()
    {
        int weeklyClearCount = UserInfo.GetClearWeeklyChallengeCount();
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type36ChallengeData data in _type36ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyClearCount, _weeklyChallengeDataDic.Count - 1) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE36);
    }

    private void Type37ChallengeCheck()
    {
        int weeklyCustomerCount = UserInfo.WeeklyCumulativeCustomerCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;
        foreach (Type37ChallengeData data in _type37ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyCustomerCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE37);
    }

    private void Type38ChallengeCheck()
    {
        long weeklyAddMoney = UserInfo.WeeklyAddMoney;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type38ChallengeData data in _type38ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyAddMoney, data.Count) < 1)
                continue;
            DebugLog.Log(data.Id);
            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE38);
    }

    private void Type39ChallengeCheck()
    {
        int weeklyCookCount = UserInfo.WeeklyCookCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type39ChallengeData data in _type39ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyCookCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE39);
    }

    private void Type40ChallengeCheck()
    {
        int weeklyCleanCount = UserInfo.WeeklyCleanCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type40ChallengeData data in _type40ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyCleanCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE40);
    }

    private void Type41ChallengeCheck()
    {
        int weeklyGatecrasherCustomerCount = UserInfo.WeeklyExterminationGatecrasherCustomerCount;
        bool dailyUpdateEnabled = false;
        bool weeklyUpdateEnabled = false;
        bool alltimeUpdateEnabled = false;
        bool mainUpdateEnabled = false;

        foreach (Type41ChallengeData data in _type41ChallengeDataDic.Values)
        {
            if (UserInfo.GetIsDoneChallenge(data.Id))
                continue;

            if (UserInfo.GetIsClearChallenge(data.Id))
                continue;

            if (GetChallengeAchievementRate(weeklyGatecrasherCustomerCount, data.Count) < 1)
                continue;

            switch (data.Challenges)
            {
                case Challenges.Daily:
                    dailyUpdateEnabled = true;
                    break;

                case Challenges.Weekly:
                    weeklyUpdateEnabled = true;
                    break;

                case Challenges.AllTime:
                    alltimeUpdateEnabled = true;
                    break;

                case Challenges.Main:
                    mainUpdateEnabled = true;
                    break;
            }
            UserInfo.DoneChallenge(data);
        }

        if (dailyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Daily);

        if (alltimeUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.AllTime);

        if (mainUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Main);

        if (weeklyUpdateEnabled)
            UpdateChallengeByChallenges(Challenges.Weekly);

        OnChallengePercentUpdateHandler?.Invoke(ChallengeType.TYPE41);
    }


    private Type11ChallengeData GetNextRepeatCoinChallenge()
    {
        int nextLevel = 1;

        // 첫 번째 챌린지가 아직 클리어되지 않았다면 첫 번째 챌린지 생성
        if (!UserInfo.ClearAllTimeChallengeSet.Contains("AllTimeRepeatCoin1"))
        {
            if (_challengeDataDic.ContainsKey("AllTimeRepeatCoin1") || _type11ChallengeDataDic.ContainsKey("AllTimeRepeatCoin1"))
            {
                return null; // 이미 존재하면 생성하지 않음
            }
            nextLevel = 1;
        }
        else
        {
            // 완료된 반복 챌린지 중 가장 높은 레벨 찾기
            for (int i = 1; i <= 100; i++)
            {
                string id = $"AllTimeRepeatCoin{i}";

                if (UserInfo.ClearAllTimeChallengeSet.Contains(id))
                {
                    nextLevel = i + 1;
                }
                else
                {
                    break; // 완료되지 않은 첫 번째 레벨에서 중단
                }
            }
        }

        // 최대 레벨 초과 시 null 반환
        if (nextLevel > 100)
            return null;

        // 다음 레벨 챌린지 생성 전 중복 체크
        string nextId = $"AllTimeRepeatCoin{nextLevel}";
        if (_challengeDataDic.ContainsKey(nextId) || _type11ChallengeDataDic.ContainsKey(nextId))
        {
            return null; // 이미 존재하면 생성하지 않음
        }

        // CSV 규칙에 따른 목표 계산: 10000 × 1.2^(n-1) (천 단위 반올림)
        float rawGoal = 10000 * Mathf.Pow(1.2f, nextLevel - 1);
        long nextGoal = (long)(Mathf.Round(rawGoal / 1000f) * 1000); // 천 단위 반올림
        
        // 다음 단계 목표치 계산 (천 단위 반올림)
        float rawNextGoal = 10000 * Mathf.Pow(1.2f, nextLevel);
        long nextLevelGoal = (long)(Mathf.Round(rawNextGoal / 1000f) * 1000); // 천 단위 반올림
        
        // 보상 계산
        long currentReq = nextGoal;
        long nextReq = nextLevelGoal;
        long reqDifference = nextReq - currentReq;
        
        // 평점 보상: 최소(0.1×Req(n), 내림천단위(0.01×(Req(n+1)-Req(n))))
        long scoreOption1 = (long)(currentReq * 0.1f);
        long scoreOption2 = (long)(reqDifference * 0.01f);
        
        // 내림 처리 (1~29단계: 50단위, 30~49단계: 100단위, 50단계 이상: 1000단위)
        if (nextLevel <= 29)
        {
            scoreOption2 = (scoreOption2 / 50) * 50; // 50단위 내림
        }
        else if (nextLevel <= 49)
        {
            scoreOption2 = (scoreOption2 / 100) * 100; // 100단위 내림
        }
        else
        {
            scoreOption2 = (scoreOption2 / 1000) * 1000; // 1000단위 내림
        }
        
        int scoreReward = Mathf.Max(1, (int)Mathf.Min(scoreOption1, scoreOption2));
        
        // 코인 보상: 최소(0.5×Req(n), 내림천단위(0.20×(Req(n+1)-Req(n))))
        long coinOption1 = (long)(currentReq * 0.5f);
        long coinOption2 = (long)(reqDifference * 0.20f);
        
        // 내림 처리 (1~29단계: 100단위, 30단계 이상: 1000단위)
        if (nextLevel <= 29)
        {
            coinOption2 = (coinOption2 / 100) * 100; // 100단위 내림
        }
        else
        {
            coinOption2 = (coinOption2 / 1000) * 1000; // 1000단위 내림
        }
        
        int coinReward = Mathf.Max(100, (int)Mathf.Min(coinOption1, coinOption2));

        Type11ChallengeData newData = new Type11ChallengeData(
            Challenges.AllTime,
            ChallengeType.TYPE11,
            nextId,
            $"누적 코인 수익 {Utility.ConvertToMoney(nextGoal)} 달성",
            nextGoal,
            MoneyType.Gold,
            coinReward,
            scoreReward,
            GetShortCutAction("ShortCut01")
        );
        return newData;
    }


    private Type09ChallengeData GetNextRepeatVisitChallenge()
    {
        int nextLevel = 1;

        // 첫 번째 챌린지가 아직 클리어되지 않았다면 첫 번째 챌린지 생성
        if (!UserInfo.ClearAllTimeChallengeSet.Contains("AllTimeRepeatVisit1"))
        {
            if (_challengeDataDic.ContainsKey("AllTimeRepeatVisit1") || _type09ChallengeDataDic.ContainsKey("AllTimeRepeatVisit1"))
            {
                return null; // 이미 존재하면 생성하지 않음
            }
            nextLevel = 1;
        }
        else
        {
            // 완료된 반복 챌린지 중 가장 높은 레벨 찾기
            for (int i = 1; i <= 100; i++)
            {
                string id = $"AllTimeRepeatVisit{i}";

                if (UserInfo.ClearAllTimeChallengeSet.Contains(id))
                {
                    nextLevel = i + 1;
                }
                else
                {
                    break; // 완료되지 않은 첫 번째 레벨에서 중단
                }
            }
        }

        // 최대 레벨 초과 시 null 반환
        if (nextLevel > 100)
            return null;

        // 다음 레벨 챌린지 생성 전 중복 체크
        string nextId = $"AllTimeRepeatVisit{nextLevel}";
        if (_challengeDataDic.ContainsKey(nextId) || _type09ChallengeDataDic.ContainsKey(nextId))
        {
            return null; // 이미 존재하면 생성하지 않음
        }

        // CSV 규칙에 따른 목표 계산: 현재 단계(n) × 100
        int nextGoal = nextLevel * 100;
        
        // CSV 규칙에 따른 보상 계산
        int nextLevelGoal = (nextLevel + 1) * 100;
        
        // 보상 계산: 다음 단계 목표치 기준 (다음 단계증가 요구 필요개수)
        int scoreReward = Mathf.Max(1, Mathf.RoundToInt(nextLevelGoal * 0.15f));
        int coinReward = Mathf.Max(100, Mathf.RoundToInt((nextLevelGoal * 0.3f) / 100) * 100);

        Type09ChallengeData data = new Type09ChallengeData(
            Challenges.AllTime,
            ChallengeType.TYPE09,
            nextId,
            $"누적 방문 손님 {Utility.ConvertToMoney(nextGoal)}명 달성",
            nextGoal,
            MoneyType.Gold,
            coinReward,
            scoreReward,
            GetShortCutAction("ShortCut01")
        );
        return data;
    }


    private Type08ChallengeData GetNextRepeatScoreChallenge()
    {
        int nextLevel = 1;

        // 첫 번째 챌린지가 아직 클리어되지 않았다면 첫 번째 챌린지 생성
        if (!UserInfo.ClearAllTimeChallengeSet.Contains("AllTimeRepeatScore1"))
        {
            if (_challengeDataDic.ContainsKey("AllTimeRepeatScore1") || _type08ChallengeDataDic.ContainsKey("AllTimeRepeatScore1"))
            {
                return null; // 이미 존재하면 생성하지 않음
            }
            nextLevel = 1;
        }
        else
        {
            // 완료된 반복 챌린지 중 가장 높은 레벨 찾기
            for (int i = 1; i <= 100; i++)
            {
                string id = $"AllTimeRepeatScore{i}";

                if (UserInfo.ClearAllTimeChallengeSet.Contains(id))
                {
                    nextLevel = i + 1;
                }
                else
                {
                    break; // 완료되지 않은 첫 번째 레벨에서 중단
                }
            }
        }

        // 최대 레벨 초과 시 null 반환  
        if (nextLevel > 100)
            return null;

        // 다음 레벨 챌린지 생성 전 중복 체크
        string nextId = $"AllTimeRepeatScore{nextLevel}";
        if (_challengeDataDic.ContainsKey(nextId) || _type08ChallengeDataDic.ContainsKey(nextId))
        {
            return null; // 이미 존재하면 생성하지 않음
        }

        // CSV 규칙에 따른 목표 계산: 100 × 1.2^(n-1) (100 단위 반올림)
        float rawGoal = 100 * Mathf.Pow(1.2f, nextLevel - 1);
        int nextGoal = (int)(Mathf.Round(rawGoal / 100f) * 100); // 100 단위 반올림

        // 다음 단계 목표치 계산 (100 단위 반올림)
        float rawNextGoal = 100 * Mathf.Pow(1.2f, nextLevel);
        int nextLevelGoal = (int)(Mathf.Round(rawNextGoal / 100f) * 100); // 100 단위 반올림
        
        // 보상 계산
        int currentReq = nextGoal;
        int nextReq = nextLevelGoal;
        int reqDifference = nextReq - currentReq;
        
        // 코인 보상: 최소(0.5×Req(n), 내림단위(0.50×(Req(n+1)-Req(n))))
        int coinOption1 = (int)(currentReq * 0.5f);
        int coinOption2 = (int)(reqDifference * 0.50f);
        
        // 내림 처리 (1~29단계: 100단위, 30단계 이상: 1000단위)
        if (nextLevel <= 29)
        {
            coinOption2 = (coinOption2 / 100) * 100; // 100단위 내림
        }
        else
        {
            coinOption2 = (coinOption2 / 1000) * 1000; // 1000단위 내림
        }
        
        int coinReward = Mathf.Max(100, Mathf.Min(coinOption1, coinOption2));

        Type08ChallengeData data = new Type08ChallengeData(
            Challenges.AllTime,
            ChallengeType.TYPE08,
            nextId,
            $"누적 평점 {Utility.ConvertToMoney(nextGoal)} 달성",
            nextGoal,
            MoneyType.Gold,
            coinReward,
            0, // 평점 업적은 평점 보상 없음
            GetShortCutAction("ShortCut01")
        );
        return data;
    }


    private Type25ChallengeData GetNextRepeatGatchaChallenge()
    {
        int startReward = 100;
        int repeatStep = 100;
        int nextLevel = 2; // 1번은 이미 직접 추가했으므로 2부터 시작

        // 첫 번째 챌린지가 클리어되지 않았다면 첫 번째 챌린지 생성
        if (!UserInfo.ClearAllTimeChallengeSet.Contains("AllTimeRepeatGatcha1"))
        {
            if (_challengeDataDic.ContainsKey("AllTimeRepeatGatcha1") || _type25ChallengeDataDic.ContainsKey("AllTimeRepeatGatcha1"))
            {
                Debug.LogError("이미 존재하는 챌린지 ID입니다. AllTimeRepeatGatcha1");
                return null;
            }

            int firstGoal = startReward + repeatStep * 1;
            int firstCoinReward = 10;
            int firstScoreReward = 0;

            Type25ChallengeData firstData = new Type25ChallengeData(
                Challenges.AllTime,
                ChallengeType.TYPE25,
                "AllTimeRepeatGatcha1",
                $"누적 캡슐 머신 사용 {Utility.ConvertToMoney(firstGoal)}회 달성",
                firstGoal,
                MoneyType.Dia,
                firstCoinReward,
                firstScoreReward,
                GetShortCutAction("ShortCut01")
            );
            return firstData;
        }

        // 완료된 반복 챌린지 중 가장 높은 레벨 찾기 (1번은 이미 직접 추가했으므로 2번부터 확인)
        for (int i = 2; i <= 100; i++)
        {
            string id = $"AllTimeRepeatGatcha{i}";

            if (UserInfo.ClearAllTimeChallengeSet.Contains(id))
            {
                nextLevel = i + 1;
            }
            else
            {
                break;
            }
        }

        // 최대 레벨 초과 시 null 반환
        if (nextLevel > 100)
            return null;

        if (_challengeDataDic.ContainsKey($"AllTimeRepeatGatcha{nextLevel}") || _type25ChallengeDataDic.ContainsKey($"AllTimeRepeatGatcha{nextLevel}"))
        {
            Debug.LogError("이미 존재하는 챌린지 ID입니다. AllTimeRepeatGatcha" + nextLevel);
            return null;
        }

        // 다음 레벨 챌린지 생성
        int nextGoal = startReward + repeatStep * nextLevel;
        int coinReward = 10;
        int scoreReward = 0;
        string nextId = $"AllTimeRepeatGatcha{nextLevel}";
        DebugLog.Log(nextId);
        Type25ChallengeData data = new Type25ChallengeData(
            Challenges.AllTime,
            ChallengeType.TYPE25,
            nextId,
            $"누적 캡슐 머신 사용 {Utility.ConvertToMoney(nextGoal)}회 달성",
            nextGoal,
            MoneyType.Dia,
            coinReward,
            scoreReward,
            GetShortCutAction("ShortCut01")
        );
        return data;
    }

    private void OnCheckRepeatCoinChallengeEvent()
    {
        ChallengeData challengeData = GetNextRepeatCoinChallenge();
        if (challengeData == null)
            return;

        if (_alltimeChallengeDataDic.ContainsKey(challengeData.Id))
            return;

        _challengeDataDic.Add(challengeData.Id, challengeData);
        _alltimeChallengeDataDic.Add(challengeData.Id, challengeData as Type11ChallengeData);
        _type11ChallengeDataDic.Add(challengeData.Id, challengeData as Type11ChallengeData);
        _allTimeChallengeList.Add(challengeData);
        Type11ChallengeCheck();
        OnAddChallengeHandler?.Invoke();
    }

    private void OnCheckRepeatVisitChallengeEvent()
    {
        ChallengeData challengeData = GetNextRepeatVisitChallenge();
        if (challengeData == null)
            return;

        if (_alltimeChallengeDataDic.ContainsKey(challengeData.Id))
            return;

        _challengeDataDic.Add(challengeData.Id, challengeData);
        _alltimeChallengeDataDic.Add(challengeData.Id, challengeData as Type09ChallengeData);
        _type09ChallengeDataDic.Add(challengeData.Id, challengeData as Type09ChallengeData);
        _allTimeChallengeList.Add(challengeData);
        Type09ChallengeCheck();
        OnAddChallengeHandler?.Invoke();
    }

    private void OnCheckRepeatScoreChallengeEvent()
    {
        ChallengeData challengeData = GetNextRepeatScoreChallenge();
        if (challengeData == null)
            return;

        if (_alltimeChallengeDataDic.ContainsKey(challengeData.Id))
            return;

        _challengeDataDic.Add(challengeData.Id, challengeData);
        _alltimeChallengeDataDic.Add(challengeData.Id, challengeData as Type08ChallengeData);
        _type08ChallengeDataDic.Add(challengeData.Id, challengeData as Type08ChallengeData);
        _allTimeChallengeList.Add(challengeData);
        Type08ChallengeCheck();
        OnAddChallengeHandler?.Invoke();
    }

    private void OnCheckRepeatGatchaChallengeEvent()
    {
        ChallengeData challengeData = GetNextRepeatGatchaChallenge();
        if (challengeData == null)
            return;

        if (_alltimeChallengeDataDic.ContainsKey(challengeData.Id))
            return;

        _challengeDataDic.Add(challengeData.Id, challengeData);
        _alltimeChallengeDataDic.Add(challengeData.Id, challengeData as Type25ChallengeData);
        _type25ChallengeDataDic.Add(challengeData.Id, challengeData as Type25ChallengeData);
        _allTimeChallengeList.Add(challengeData);
        Type25ChallengeCheck();
        DebugLog.Log(challengeData.Id);
        OnAddChallengeHandler?.Invoke();
    }
}
