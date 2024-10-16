using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
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
    public event Action OnAppendAddScoreHandler;

    public Vector2 OutDoorPos => new Vector2(24.6f, 7.64f);


    [SerializeField] private int _totalTabCount = 8;
    public int TotalTabCount => _totalTabCount;

    [SerializeField] private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;

    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;

    private int _maxWaitCustomerCount = 10;
    public int MaxWaitCustomerCount => _maxWaitCustomerCount + _addUpgradeGachaItemWaitCustomerMaxCount;
    public float CustomerSpeedMul => 1 + (_addUpgradeGachaItemCustomerSpeedMul * 0.01f);
    public int AddSpecialCustomerMoney => _addUpgradeGachaItemSpecialCustomerMoney;
    public float SpecialCustomerSpawnPercent => 0.01f * (_addUpgradeGachaItemSpecialCustomerSpawnPercent);
    public float AddGatecrasherCustomerDamageMul => 0.01f * (_addUpgradeGachaItemGatecrasherCustomerDamageMul);
    public float AddGatecrasherCustomerSpeedDownTime => _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime;
    

    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => 1 + (_addEquipKitchenUtensilCookSpeedMul * 0.01f) + (_addEquipSetDataCookSpeedMul * 0.01f);
    public float SubCookingTime => _subUpgradeGachaItemCookingTime;
    public int AddFoodPrice => _addUpgradeGachaItemFoodPrice;
    public float AddFoodDoublePricePercent => Mathf.Clamp(_addUpgradeGachaItemFoodDoublePricePercent, 0f, 100f);
    public int AddFoodTip => _addUpgradeGachaItemFoodTip;


    public int AddSocre => _addEquipStaffScore + _addEquipFurnitureScore + _addEquipKitchenUtensilScore + _addGiveGachaItemScore + _addUpgradeGachaItemScroe;
    public float TipMul => Mathf.Clamp(_addEquipStaffTipMul * 0.01f, 0f, 10000f);

    public int TipPerMinute => _addEquipFurnitureTipPerMinute + _addEquipKitchenUtensilTipPerMinute + _addEquipSetDataTipPerMinute + _addGiveGachaItemTipPerMinute + _addUpgradeGachaItemTipPerMinute;
    public int MaxTipVolume => Mathf.FloorToInt(_addEquipFurnitureMaxTipVolume + (_addEquipFurnitureMaxTipVolume * _addUpgradeGachaItemMaxTipVolumeMul * 0.01f));


    public float AddMiniGameTime => _addUpgradeGachaItemMiniGameTime;


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

    [SerializeField] private int _addUpgradeGachaItemScroe; // 전체 평점 상승(+) 추가됨
    [SerializeField] private int _addUpgradeGachaItemTipPerMinute; //분당 팁 증가(+) 추가됨
    [SerializeField] private int _addUpgradeGachaItemFoodPrice; //음식 가격 증가(+) 추가됨
    [SerializeField] private int _addUpgradeGachaItemFoodTip; //주문 수 마다 팁 증가(+) 추가됨
    [SerializeField] private int _addUpgradeGachaItemWaitCustomerMaxCount; //최대 줄서기 손님 증가(+) 추가됨
    [SerializeField] private int _addUpgradeGachaItemSpecialCustomerMoney; //스페셜 손님 터치시 코인 획득량 증가(+) 추가됨
    [SerializeField] private float _addUpgradeGachaItemCustomerSpeedMul; //손님 스피드 상승(%) 추가됨
    [SerializeField] private float _addUpgradeGachaItemMaxTipVolumeMul; //최대 팁 보유량 증가(%) 추가됨
    [SerializeField] private float _subUpgradeGachaItemCookingTime; //요리 시간 단축(-) 추가됨
    [SerializeField] private float _addUpgradeGachaItemFoodDoublePricePercent; //음식 가격 두배 확률 증가(%) //추가됨
    [SerializeField] private float _addUpgradeGachaItemSpecialCustomerSpawnPercent; //스페셜 손님 등장 확률 증가(%) 추가됨
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerDamageMul; //진상에게 가하는 피해 증가(%) //추가됨
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime; //진상 터치시 속도 둔화 시간 증가(+) //추가됨
    [SerializeField] private float _addUpgradeGachaItemMiniGameTime; //미니 게임 제작 시간 증가(+) //추가됨

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
        UserInfo.GiveRecipe("FOOD01");
        Application.targetFrameRate = 60;
        UserInfo.DataBindTip();
        UserInfo.DataBindMoney();

        UserInfo.OnChangeStaffHandler += OnEquipStaffEffectCheck;
        UserInfo.OnUpgradeStaffHandler += OnEquipStaffEffectCheck;
        UserInfo.OnChangeFurnitureHandler += (type) => OnEquipFurnitureEffectCheck();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => OnEquipKitchenUtensilEffectCheck();
        UserInfo.OnChangeFurnitureHandler += (type) => CheckSetDataEnabled();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => CheckSetDataEnabled();
        UserInfo.OnGiveGachaItemHandler += OnGiveGachaItemEffectCheck;

        OnEquipStaffEffectCheck();
        OnEquipFurnitureEffectCheck();
        OnEquipKitchenUtensilEffectCheck();
        CheckSetDataEnabled();
        OnGiveGachaItemEffectCheck();
    }


    private void Start()
    {
        UserInfo.GiveFurniture("TABLE01_01");
        UserInfo.SetEquipFurniture("TABLE01_01");

        UserInfo.GiveFurniture("TABLE01_02");
        UserInfo.SetEquipFurniture("TABLE01_02");

        UserInfo.GiveFurniture("TABLE01_03");
        UserInfo.SetEquipFurniture("TABLE01_03");

        UserInfo.GiveFurniture("TABLE01_04");
        UserInfo.SetEquipFurniture("TABLE01_04");

        UserInfo.GiveFurniture("TABLE01_05");
        UserInfo.SetEquipFurniture("TABLE01_05");

        UserInfo.GiveFurniture("FLOWER01");
        UserInfo.SetEquipFurniture("FLOWER01");

/*        UserInfo.GiveFurniture("RACK01");
        UserInfo.SetEquipFurniture("RACK01");*/

        UserInfo.GiveFurniture("WALLPAPER01");
        UserInfo.SetEquipFurniture("WALLPAPER01");

        UserInfo.GiveFurniture("ACC01");
        UserInfo.SetEquipFurniture("ACC01");

        UserInfo.GiveFurniture("FRAME01");
        UserInfo.SetEquipFurniture("FRAME01");

        UserInfo.GiveFurniture("COUNTER01");
        UserInfo.SetEquipFurniture("COUNTER01");

        UserInfo.GiveKitchenUtensil("COOKER01_01");
        UserInfo.SetEquipKitchenUtensil("COOKER01_01");
    }


    private void Update()
    {
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
        OnAppendAddScoreHandler?.Invoke();
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
        OnAppendAddScoreHandler?.Invoke();
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
        OnAppendAddScoreHandler?.Invoke();
        OnChangeTipPerMinuteHandler?.Invoke();
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

        List<string> giveGachaItemList = UserInfo.GetGiveGachaItemList();
        for(int i = 0, cnt = giveGachaItemList.Count; i < cnt; ++i)
        {
            if (!ItemManager.Instance.IsGachaItem(giveGachaItemList[i]))
                continue;

            GachaItemData data = ItemManager.Instance.GetGachaItemData(giveGachaItemList[i]);
            addScore += data.AddScore;
            addTipPerMinute += data.TipPerMinute;
        }

        _addGiveGachaItemScore = addScore;
        _addGiveGachaItemTipPerMinute = addTipPerMinute;
        OnAppendAddScoreHandler?.Invoke();
    }

}
