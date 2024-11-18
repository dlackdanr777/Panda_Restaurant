using BackEnd;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("GameManager");
                _instance = obj.AddComponent<GameManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static GameManager _instance;

    public event Action OnChangeTipPerMinuteHandler;
    public event Action OnChangeScoreHandler;
    public event Action OnChgangeStaffSkillValueHandler;

    public Vector2 OutDoorPos => new Vector2(24.6f, 7.64f);


    [SerializeField] private int _totalTabCount = 8;
    public int TotalTabCount => _totalTabCount;

    [SerializeField] private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;


    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;

    private int _maxWaitCustomerCount = 10;
    public int MaxWaitCustomerCount => _maxWaitCustomerCount + _addUpgradeGachaItemWaitCustomerMaxCount;
    public float AddCustomerSpeedMul => 1 + 0.01f * (_addUpgradeGachaItemCustomerSpeedPercent);
    public int AddSpecialCustomerMoney => _addUpgradeGachaItemSpecialCustomerMoney;
    public float AddSpecialCustomerSpawnMul => 1 +  0.01f * (_addUpgradeGachaItemSpecialCustomerSpawnPercent);
    public float AddGatecrasherCustomerDamageMul => 1 + 0.01f * (_addUpgradeGachaItemGatecrasherCustomerDamagePercent);
    public float AddGatecrasherCustomerSpeedDownTime => _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime;
    

    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => 1 + (_addEquipKitchenUtensilCookSpeedMul * 0.01f) + (_addEquipSetDataCookSpeedMul * 0.01f);
    public float SubCookingTime => _subUpgradeGachaItemCookingTime;
    public int AddFoodPrice => _addUpgradeGachaItemFoodPrice;
    public float AddFoodDoublePricePercent => Mathf.Clamp(_addUpgradeGachaItemFoodDoublePricePercent, 0f, 100f);
    public int AddFoodTip => _addUpgradeGachaItemFoodTip;


    public int AddSocre => _addEquipStaffScore + _addEquipFurnitureScore + _addEquipKitchenUtensilScore + _addGiveGachaItemScore + _addUpgradeGachaItemScore;
    public float TipMul => Mathf.Clamp(_addEquipStaffTipMul * 0.01f, 0f, 10000f);

    public int TipPerMinute => _addEquipFurnitureTipPerMinute + _addEquipKitchenUtensilTipPerMinute + _addEquipSetDataTipPerMinute + _addGiveGachaItemTipPerMinute + _addUpgradeGachaItemTipPerMinute;
    public int MaxTipVolume => Mathf.FloorToInt(_addEquipFurnitureMaxTipVolume + (_addEquipFurnitureMaxTipVolume * _addUpgradeGachaItemMaxTipVolumePercent * 0.01f));


    public float AddStaffSkillTime => _addUpgradeGachaItemStaffSkillTime;
    public float AddWaiterSkillTime => _addUpgradeGachaItemWaiterSkillTime;
    public float AddServerSkillTime => _addUpgradeGachaItemServerSkillTime;
    public float AddMarketerSkillTime => _addUpgradeGachaItemMarketerSkillTime;
    public float AddGuardSkillTime => _addUpgradeGachaItemGuardSkillTime;
    public float AddCleanerSkillTime => _addUpgradeGachaItemCleanerSkillTime;

    public float SubStaffSkillCoolTime => -_subUpgradeGachaItemStaffCoolTime;
    public float SubWaiterSkillCoolTime => -_subUpgradeGachaItemWaiterCoolTime;
    public float SubServerSkillCoolTime => -_subUpgradeGachaItemServerCoolTime;
    public float SubMarketerSkillCoolTime => -_subUpgradeGachaItemMarketerCoolTime;
    public float SubGuardSkillCoolTime => -_subUpgradeGachaItemGuardCoolTime;
    public float SubCleanerSkillCoolTime => -_subUpgradeGachaItemCleanerCoolTime;


    public float AddMiniGameTime => _addUpgradeGachaItemMiniGameTime + _addGiveRecipeMiniGameTime;



    [SerializeField] private int _addEquipStaffScore;
    [SerializeField] private float _addEquipStaffTipMul;

    [SerializeField] private int _addEquipFurnitureScore;
    [SerializeField] private int _addEquipFurnitureMaxTipVolume;
    [SerializeField] private int _addEquipFurnitureTipPerMinute;

    [SerializeField] private int _addEquipKitchenUtensilScore;
    [SerializeField] private int _addEquipKitchenUtensilCookSpeedMul;
    [SerializeField] private int _addEquipKitchenUtensilTipPerMinute;

    [SerializeField] private int _addEquipSetDataTipPerMinute;
    [SerializeField] private float _addEquipSetDataCookSpeedMul;

    [SerializeField] private int _addGiveGachaItemScore;
    [SerializeField] private int _addGiveGachaItemTipPerMinute;

    [SerializeField] private int _addGiveRecipeMiniGameTime;

    [SerializeField] private int _addUpgradeGachaItemScore; // 전체 평점 상승(+)
    [SerializeField] private int _addUpgradeGachaItemTipPerMinute; //분당 팁 증가(+)
    [SerializeField] private int _addUpgradeGachaItemFoodPrice; //음식 가격 증가(+)
    [SerializeField] private int _addUpgradeGachaItemFoodTip; //주문 수 마다 팁 증가(+)
    [SerializeField] private int _addUpgradeGachaItemWaitCustomerMaxCount; //최대 줄서기 손님 증가(+)
    [SerializeField] private int _addUpgradeGachaItemSpecialCustomerMoney; //스페셜 손님 터치시 코인 획득량 증가(+)
    [SerializeField] private float _addUpgradeGachaItemCustomerSpeedPercent; //손님 스피드 상승(%)
    [SerializeField] private float _addUpgradeGachaItemMaxTipVolumePercent; //최대 팁 보유량 증가(%)
    [SerializeField] private float _subUpgradeGachaItemCookingTime; //요리 시간 단축(-)
    [SerializeField] private float _addUpgradeGachaItemFoodDoublePricePercent; //음식 가격 두배 확률 증가(%)
    [SerializeField] private float _addUpgradeGachaItemSpecialCustomerSpawnPercent; //스페셜 손님 등장 확률 증가(%)
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerDamagePercent; //진상에게 가하는 피해 증가(%)
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime; //진상 터치시 속도 둔화 시간 증가(+)
    [SerializeField] private float _addUpgradeGachaItemMiniGameTime; //미니 게임 제작 시간 증가(+)

    [SerializeField] private float _subUpgradeGachaItemStaffCoolTime; //전체 스탭 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemStaffSkillTime; //전체 스탭 스킬 유지 시간 증가(+)
    [SerializeField] private float _subUpgradeGachaItemWaiterCoolTime; //웨이터 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemWaiterSkillTime; //웨이터 스킬 유지 시간 증가(+)
    [SerializeField] private float _subUpgradeGachaItemServerCoolTime; //서버 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemServerSkillTime; //서버 스킬 유지 시간 증가(+)
    [SerializeField] private float _subUpgradeGachaItemMarketerCoolTime; //치어리더 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemMarketerSkillTime; //치어리더 스킬 유지 시간 증가(+)
    [SerializeField] private float _subUpgradeGachaItemGuardCoolTime; //가드 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemGuardSkillTime; //가드 스킬 유지 시간 증가(+)
    [SerializeField] private float _subUpgradeGachaItemCleanerCoolTime; //청소부 스킬 쿨타임 감소(-)
    [SerializeField] private float _addUpgradeGachaItemCleanerSkillTime; //청소부 스킬 유지 시간 증가(+)


    private float _updateTimer;


    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul += value * 0.01f;
    }


    public void AddCookingSpeedMul(float value)
    {
        _cookingSpeedMul += value * 0.01f;
        _cookingSpeedMul = Mathf.Clamp(_cookingSpeedMul, 1, 100);
    }


    public void AppendPromotionCustomer(int value)
    {
        _addPromotionCustomer = Mathf.Clamp(_addPromotionCustomer + value, 1, 100);
    }

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
        UserInfo.DataBindTip();
        UserInfo.DataBindMoney();

        UserInfo.OnChangeStaffHandler += OnEquipStaffEffectCheck;
        UserInfo.OnUpgradeStaffHandler += OnEquipStaffEffectCheck;
        UserInfo.OnChangeFurnitureHandler += (type) => OnEquipFurnitureEffectCheck();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => OnEquipKitchenUtensilEffectCheck();
        UserInfo.OnChangeFurnitureHandler += (type) => CheckSetDataEnabled();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => CheckSetDataEnabled();
        UserInfo.OnGiveRecipeHandler += OnGiveRecipeCheck;
        UserInfo.OnGiveGachaItemHandler += OnGiveGachaItemEffectCheck;
        UserInfo.OnGiveGachaItemHandler += OnUpgradeGachaItemCheck;
        UserInfo.OnUpgradeGachaItemHandler += OnUpgradeGachaItemCheck;

        OnEquipStaffEffectCheck();
        OnEquipFurnitureEffectCheck();
        OnEquipKitchenUtensilEffectCheck();
        CheckSetDataEnabled();
        OnGiveGachaItemEffectCheck();
        OnGiveRecipeCheck();
        OnUpgradeGachaItemCheck();
    }


    private void Start()
    {
        UserInfo.GiveFurniture("TABLE08_01");
        UserInfo.SetEquipFurniture("TABLE08_01");

        UserInfo.GiveFurniture("TABLE08_02");
        UserInfo.SetEquipFurniture("TABLE08_02");

        UserInfo.GiveFurniture("TABLE08_03");
        UserInfo.SetEquipFurniture("TABLE08_03");

        UserInfo.GiveFurniture("TABLE08_04");
        UserInfo.SetEquipFurniture("TABLE08_04");

        UserInfo.GiveFurniture("TABLE08_05");
        UserInfo.SetEquipFurniture("TABLE08_05");

        UserInfo.GiveFurniture("FLOWER08");
        UserInfo.SetEquipFurniture("FLOWER08");

        UserInfo.GiveFurniture("RACK08");
        UserInfo.SetEquipFurniture("RACK08");

        UserInfo.GiveFurniture("WALLPAPER08");
        UserInfo.SetEquipFurniture("WALLPAPER08");

        UserInfo.GiveFurniture("ACC08");
        UserInfo.SetEquipFurniture("ACC08");

        UserInfo.GiveFurniture("FRAME08");
        UserInfo.SetEquipFurniture("FRAME08");

        UserInfo.GiveFurniture("COUNTER08");
        UserInfo.SetEquipFurniture("COUNTER08");

        UserInfo.GiveKitchenUtensil("COOKER01_01");
        UserInfo.SetEquipKitchenUtensil("COOKER01_01");
    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.A))
        {
            BackendManager.Instance.GetMyData("Test", 10, UserInfo.LoadSaveData);
        }

        _updateTimer += Time.deltaTime;

        if(60 <= _updateTimer)
        {
            _updateTimer = 0;
            UserInfo.AddTip(TipPerMinute);
        }
    }


    private void OnEquipStaffEffectCheck()
    {
        _addEquipStaffScore = 0;
        _addEquipStaffTipMul = 0;
        int addScore = 0;
        float addTipMul = 0;

        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            StaffData data = UserInfo.GetEquipStaff((StaffType)i);

            if (data == null)
                continue;

            int level = UserInfo.GetStaffLevel(data);
            addScore += data.GetAddScore(level);
            addTipMul += data.GetAddTipMul(level);
        }

        _addEquipStaffScore = addScore;
        _addEquipStaffTipMul = addTipMul;
        OnChangeScoreHandler?.Invoke();
    }


    private void OnEquipFurnitureEffectCheck()
    {
        _addEquipFurnitureScore = 0;
        _addEquipFurnitureMaxTipVolume = 0;
        _addEquipFurnitureTipPerMinute = 0;
        int addScore = 0;
        int maxTipVolume = 0;
        int tipPerMinute = 0;

        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            FurnitureData data = UserInfo.GetEquipFurniture((FurnitureType)i);

            if (data == null)
                continue;

            addScore += data.AddScore;

            if (data.EffectData == null)
                continue;

            if (data.EffectData is MaxTipVolumeEquipEffectData)
                maxTipVolume += data.EffectData.EffectValue;

            else if (data.EffectData is TipPerMinuteEquipEffectData)
                tipPerMinute += data.EffectData.EffectValue;
        }

        _addEquipFurnitureScore = addScore;
        _addEquipFurnitureMaxTipVolume = maxTipVolume;
        _addEquipFurnitureTipPerMinute = tipPerMinute;
        OnChangeScoreHandler?.Invoke();
        OnChangeTipPerMinuteHandler?.Invoke();
    }


    private void OnEquipKitchenUtensilEffectCheck()
    {
        _addEquipKitchenUtensilScore = 0;
        _addEquipKitchenUtensilCookSpeedMul = 0;
        _addEquipKitchenUtensilTipPerMinute = 0;
        int addScore = 0;
        int cookSpeedMul = 0;
        int tipPerMinute = 0;

        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil((KitchenUtensilType)i);

            if (data == null)
                continue;

            addScore += data.AddScore;

            if (data.EffectData == null)
                continue;

            if (data.EffectData is CookingSpeedUpEquipEffectData)
                cookSpeedMul += data.EffectData.EffectValue;

            else if (data.EffectData is TipPerMinuteEquipEffectData)
                tipPerMinute += data.EffectData.EffectValue;
        }

        _addEquipKitchenUtensilScore = addScore;
        _addEquipKitchenUtensilCookSpeedMul = cookSpeedMul;
        _addEquipKitchenUtensilTipPerMinute = tipPerMinute;
        OnChangeScoreHandler?.Invoke();
        OnChangeTipPerMinuteHandler?.Invoke();
    }


    private void OnGiveRecipeCheck()
    {
        _addGiveRecipeMiniGameTime = 0;
        List<string> giveRecipeList = UserInfo.GetGiveRecipeList();
        FoodData foodData;
        for(int i = 0, cnt = giveRecipeList.Count; i < cnt; ++i)
        {
            foodData = FoodDataManager.Instance.GetFoodData(giveRecipeList[i]);

            if(foodData == null)
                throw new Exception("보유중인 레시피이지만 데이터베이스에 등록되어있지 않습니다: " + giveRecipeList[i]);

            if(!FoodDataManager.Instance.IsNeedMiniGame(giveRecipeList[i]))
                continue;

            _addGiveRecipeMiniGameTime += 5;
        }
    }


    private  void CheckSetDataEnabled()
    {
        _addEquipSetDataTipPerMinute = 0;
        _addEquipSetDataCookSpeedMul = 0;

        UserInfo.SetEquipFurnitureSetData(GetEquipFurnitureSetData());
        UserInfo.SetEquipKitchenUntensilSetData(GetEquipKitchenUtensilSetData());

        OnChangeTipPerMinuteHandler?.Invoke();

        SetData GetEquipFurnitureSetData()
        {
            string setId = string.Empty;

            for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
            {
                FurnitureData data = UserInfo.GetEquipFurniture((FurnitureType)i);
                if (data == null)
                    return null;

                if (string.IsNullOrEmpty(setId))
                {
                    setId = data.SetId;
                    continue;
                }

                if (setId != data.SetId)
                    return null;
            }

            SetData setData = SetDataManager.Instance.GetSetData(setId);
            if (setData == null)
            {
                DebugLog.LogError("해당 세트옵션 데이터가 데이터베이스에 존재하지 않습니다. 확인해주세요: " + setId);
                return null;
            }

            if (setData is TipPerMinuteSetData)
                _addEquipSetDataTipPerMinute += (int)setData.Value;

            else if (setData is CookingSpeedUpSetData)
                _addEquipSetDataCookSpeedMul += setData.Value;

            return setData;
        }

        SetData GetEquipKitchenUtensilSetData()
        {
            string setId = string.Empty;

            for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
            {
                KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil((KitchenUtensilType)i);
                if (data == null)
                    return null;

                if (string.IsNullOrEmpty(setId))
                {
                    setId = data.SetId;
                    continue;
                }

                if (setId != data.SetId)
                    return null;
            }

            SetData setData = SetDataManager.Instance.GetSetData(setId);
            if (setData == null)
            {
                DebugLog.LogError("해당 세트옵션 데이터가 데이터베이스에 존재하지 않습니다. 확인해주세요: " + setId);
                return null;
            }

            if (setData is TipPerMinuteSetData)
                _addEquipSetDataTipPerMinute += (int)setData.Value;

            else if (setData is CookingSpeedUpSetData)
                _addEquipSetDataCookSpeedMul += setData.Value;

            return setData;
        }
    }

    
    private void OnGiveGachaItemEffectCheck()
    {
        _addGiveGachaItemScore = 0;
        _addGiveGachaItemTipPerMinute = 0;
        int addScore = 0;
        int addTipPerMinute = 0;

        Dictionary<string, int> giveGachaItemDic = UserInfo.GetGiveGachaItemDic();

        foreach(string id in giveGachaItemDic.Keys)
        {
            if (!ItemManager.Instance.IsGachaItem(id))
                continue;

            GachaItemData data = ItemManager.Instance.GetGachaItemData(id);
            addScore += data.AddScore;
            addTipPerMinute += data.TipPerMinute;
        }

        _addGiveGachaItemScore = addScore;
        _addGiveGachaItemTipPerMinute = addTipPerMinute;
        OnChangeScoreHandler?.Invoke();
    }


    private void OnUpgradeGachaItemCheck()
    {
        _addUpgradeGachaItemScore = 0;
        _addUpgradeGachaItemScore = 0;
        _addUpgradeGachaItemTipPerMinute = 0;
        _addUpgradeGachaItemFoodPrice = 0;
        _addUpgradeGachaItemFoodTip = 0;
        _addUpgradeGachaItemWaitCustomerMaxCount = 0;
        _addUpgradeGachaItemSpecialCustomerMoney = 0;
        _addUpgradeGachaItemCustomerSpeedPercent = 0;
        _addUpgradeGachaItemMaxTipVolumePercent = 0;
        _subUpgradeGachaItemCookingTime = 0;
        _addUpgradeGachaItemFoodDoublePricePercent = 0;
        _addUpgradeGachaItemSpecialCustomerSpawnPercent = 0;
        _addUpgradeGachaItemGatecrasherCustomerDamagePercent = 0;
        _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime = 0;
        _addUpgradeGachaItemMiniGameTime = 0;
        _subUpgradeGachaItemStaffCoolTime = 0;
        _addUpgradeGachaItemStaffSkillTime = 0;
        _subUpgradeGachaItemWaiterCoolTime = 0;
        _addUpgradeGachaItemWaiterSkillTime = 0;
        _subUpgradeGachaItemServerCoolTime = 0;
        _addUpgradeGachaItemServerSkillTime = 0;
        _subUpgradeGachaItemMarketerCoolTime = 0;
        _addUpgradeGachaItemMarketerSkillTime = 0;
        _subUpgradeGachaItemGuardCoolTime = 0;
        _addUpgradeGachaItemGuardSkillTime = 0;
        _subUpgradeGachaItemCleanerCoolTime = 0;
        _addUpgradeGachaItemCleanerSkillTime = 0;

        Dictionary<string, int> giveGachaItemLevelDic = UserInfo.GetGiveGachaItemLevelDic();
        GachaItemData gachaItemData;

        foreach (var data in giveGachaItemLevelDic)
        {
            gachaItemData = ItemManager.Instance.GetGachaItemData(data.Key);
            int itemLevel = data.Value;
            if (gachaItemData == null)
            {
                DebugLog.LogError("아이템 정보가 데이터베이스에 존재하지 않습니다: " + data.Key);
                continue;
            }

            if (!UserInfo.IsGiveGachaItem(gachaItemData))
            {
                DebugLog.LogError("해당 아이템의 레벨은 존재하나 보유중이지 않습니다: " + data.Key);
                continue;
            }

            float addValue = gachaItemData.DefaultValue + ((itemLevel - 1) * gachaItemData.UpgradeValue);

            switch (gachaItemData.UpgradeType)
            {
                case UpgradeType.UPGRADE01:
                    _addUpgradeGachaItemScore += (int)addValue;
                    break;

                case UpgradeType.UPGRADE02:
                    _addUpgradeGachaItemCustomerSpeedPercent += addValue;
                    break;

                case UpgradeType.UPGRADE03:
                    _addUpgradeGachaItemTipPerMinute += (int)addValue;
                    break;

                case UpgradeType.UPGRADE04:
                    _addUpgradeGachaItemMaxTipVolumePercent += addValue;
                    break;

                case UpgradeType.UPGRADE05:
                    _addUpgradeGachaItemFoodPrice += (int)addValue;
                    break;

                case UpgradeType.UPGRADE06:
                    _subUpgradeGachaItemCookingTime += addValue;
                    break;

                case UpgradeType.UPGRADE07:
                    _addUpgradeGachaItemFoodDoublePricePercent += addValue;
                    break;

                case UpgradeType.UPGRADE08:
                    _addUpgradeGachaItemFoodTip += (int)addValue;
                    break;

                case UpgradeType.UPGRADE09:
                    _subUpgradeGachaItemWaiterCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE10:
                    _addUpgradeGachaItemWaiterSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE11:
                    _subUpgradeGachaItemServerCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE12:
                    _addUpgradeGachaItemServerSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE13:
                    _subUpgradeGachaItemMarketerCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE14:
                    _addUpgradeGachaItemMarketerSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE15:
                    _subUpgradeGachaItemCleanerCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE16:
                    _addUpgradeGachaItemCleanerSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE17:
                    _subUpgradeGachaItemGuardCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE18:
                    _addUpgradeGachaItemGuardSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE19:
                    _addUpgradeGachaItemGatecrasherCustomerDamagePercent += addValue;
                    break;

                case UpgradeType.UPGRADE20:
                    _addUpgradeGachaItemSpecialCustomerMoney += (int)addValue;
                    break;

                case UpgradeType.UPGRADE21:
                    _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime += addValue;
                    break;

                case UpgradeType.UPGRADE22:
                    _subUpgradeGachaItemStaffCoolTime += addValue;
                    break;

                case UpgradeType.UPGRADE23:
                    _addUpgradeGachaItemStaffSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE24:
                    _addUpgradeGachaItemMiniGameTime += addValue;
                    break;

                case UpgradeType.UPGRADE25:
                    _addUpgradeGachaItemSpecialCustomerSpawnPercent += addValue;
                    break;

                case UpgradeType.UPGRADE26:
                    _addUpgradeGachaItemWaitCustomerMaxCount += (int)addValue;
                    break;
            }
        }
        OnChangeScoreHandler?.Invoke();
        OnChgangeStaffSkillValueHandler?.Invoke();
    }


    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        Param param = UserInfo.GetSaveData();
        BackendManager.Instance.SaveGameData("Test", 3, param);
        DebugLog.Log("저장");
    }

    private void OnApplicationQuit()
    {
        Param param = UserInfo.GetSaveData();
        BackendManager.Instance.SaveGameData("Test", 3, param);
        DebugLog.Log("저장");
    }

}
