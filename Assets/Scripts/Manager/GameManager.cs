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
            if (_instance == null)
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
    public event Action OnChangeStaffSkillValueHandler;
    public event Action OnChangeMaxWaitCustomerCountHandler;

    public Vector2 OutDoorPos => new Vector2(24.6f, 7.64f);

    public int FeverGaguge = 0;

    [SerializeField] private int _totalTabCount = 8;
    public int TotalTabCount => _totalTabCount;

    [SerializeField] private float _foodPriceMul = 0;

    private float[] _foodTypePriceMul = new float[(int)FoodType.Length];
    public float GetFoodTypePriceMul(FoodType type) { return 1 + _foodTypePriceMul[(int)type]; }


    [SerializeField] private float _totalAddSpeedMul = 0;

    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;

    private int _maxWaitCustomerCount = 10;
    public int MaxWaitCustomerCount => Mathf.Clamp(_maxWaitCustomerCount + _addUpgradeGachaItemWaitCustomerMaxCount /*+ _addEquipStaffMaxWaitCustomerCount*/, 0, 30);
    public float AddCustomerSpeedMul => 1 + _totalAddSpeedMul + (0.01f * _addUpgradeGachaItemCustomerSpeedPercent);
    public int AddSpecialCustomerMoney => _addUpgradeGachaItemSpecialCustomerMoney;
    public float AddSpecialCustomerSpawnMul => 1 +  0.01f * (_addUpgradeGachaItemSpecialCustomerSpawnPercent);
    public float AddGatecrasherCustomerDamageMul => 1 + 0.01f * (_addUpgradeGachaItemGatecrasherCustomerDamagePercent);
    public float AddGatecrasherCustomerSpeedDownTime => _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime;
    

    [SerializeField] private float _cookingSpeedMul = 1;
    public float SubCookingTime => _subUpgradeGachaItemCookingTime;
    public int AddFoodPrice => _addUpgradeGachaItemFoodPrice;
    public float AddFoodDoublePricePercent => Mathf.Clamp(_addUpgradeGachaItemFoodDoublePricePercent, 0f, 100f);
    public int AddFoodTip => _addUpgradeGachaItemFoodTip;


    public int AddSocre => /*_addEquipStaffScore +*/ _addEquipFurnitureScore + _addEquipKitchenUtensilScore + _addGiveGachaItemScore + _addUpgradeGachaItemScore;
    public float TipMul =>  1 /*Mathf.Clamp(_addEquipStaffTipMul * 0.01f, 0f, 10000f)*/;

    public int TipPerMinute => _addEquipFurnitureTipPerMinute + _addEquipKitchenUtensilTipPerMinute + _addGiveGachaItemTipPerMinute + _addUpgradeGachaItemTipPerMinute;
    public int MaxTipVolume => _addEquipFurnitureMaxTipVolume + _addEquipKitchenUtensilTipVolume + Mathf.FloorToInt((_addEquipKitchenUtensilTipVolume + _addEquipFurnitureMaxTipVolume) * _addUpgradeGachaItemMaxTipVolumePercent * 0.01f);


    public float AddStaffSpeedMul => 1 + _totalAddSpeedMul;
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

    [SerializeField] private int _addEquipFurnitureScore;
    [SerializeField] private int _addEquipFurnitureCookSpeedMul;
    [SerializeField] private int _addEquipFurnitureTipPerMinute;
    [SerializeField] private int _addEquipFurnitureMaxTipVolume;

    [SerializeField] private int _addEquipKitchenUtensilScore;
    [SerializeField] private float _addEquipKitchenUtensilCookSpeedMul;
    [SerializeField] private int _addEquipKitchenUtensilTipPerMinute;
    [SerializeField] private int _addEquipKitchenUtensilTipVolume;

    private float[,] _addSetFoodPriceMul = new float[(int)ERestaurantFloorType.Length, (int)FoodType.Length]; //���� ���� ���� ����
    private float[,] _addSetCookSpeedMul = new float[(int)ERestaurantFloorType.Length, (int)FoodType.Length]; //���� ���� �ӵ�


    [SerializeField] private int _addGiveGachaItemScore;
    [SerializeField] private int _addGiveGachaItemTipPerMinute;

    [SerializeField] private int _addGiveRecipeMiniGameTime;

    [SerializeField] private int _addUpgradeGachaItemScore; // ��ü ���� ���(+)
    [SerializeField] private int _addUpgradeGachaItemTipPerMinute; //�д� �� ����(+)
    [SerializeField] private int _addUpgradeGachaItemFoodPrice; //���� ���� ����(+)
    [SerializeField] private int _addUpgradeGachaItemFoodTip; //�ֹ� �� ���� �� ����(+)
    [SerializeField] private int _addUpgradeGachaItemWaitCustomerMaxCount; //�ִ� �ټ��� �մ� ����(+)
    [SerializeField] private int _addUpgradeGachaItemSpecialCustomerMoney; //����� �մ� ��ġ�� ���� ȹ�淮 ����(+)
    [SerializeField] private float _addUpgradeGachaItemCustomerSpeedPercent; //�մ� ���ǵ� ���(%)
    [SerializeField] private float _addUpgradeGachaItemMaxTipVolumePercent; //�ִ� �� ������ ����(%)
    [SerializeField] private float _subUpgradeGachaItemCookingTime; //�丮 �ð� ����(-)
    [SerializeField] private float _addUpgradeGachaItemFoodDoublePricePercent; //���� ���� �ι� Ȯ�� ����(%)
    [SerializeField] private float _addUpgradeGachaItemSpecialCustomerSpawnPercent; //����� �մ� ���� Ȯ�� ����(%)
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerDamagePercent; //���󿡰� ���ϴ� ���� ����(%)
    [SerializeField] private float _addUpgradeGachaItemGatecrasherCustomerSpeedDownTime; //���� ��ġ�� �ӵ� ��ȭ �ð� ����(+)
    [SerializeField] private float _addUpgradeGachaItemMiniGameTime; //�̴� ���� ���� �ð� ����(+)

    [SerializeField] private float _subUpgradeGachaItemStaffCoolTime; //��ü ���� ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemStaffSkillTime; //��ü ���� ��ų ���� �ð� ����(+)
    [SerializeField] private float _subUpgradeGachaItemWaiterCoolTime; //������ ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemWaiterSkillTime; //������ ��ų ���� �ð� ����(+)
    [SerializeField] private float _subUpgradeGachaItemServerCoolTime; //���� ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemServerSkillTime; //���� ��ų ���� �ð� ����(+)
    [SerializeField] private float _subUpgradeGachaItemMarketerCoolTime; //ġ��� ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemMarketerSkillTime; //ġ��� ��ų ���� �ð� ����(+)
    [SerializeField] private float _subUpgradeGachaItemGuardCoolTime; //���� ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemGuardSkillTime; //���� ��ų ���� �ð� ����(+)
    [SerializeField] private float _subUpgradeGachaItemCleanerCoolTime; //û�Һ� ��ų ��Ÿ�� ����(-)
    [SerializeField] private float _addUpgradeGachaItemCleanerSkillTime; //û�Һ� ��ų ���� �ð� ����(+)





    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul = Mathf.Clamp(_foodPriceMul + value * 0.01f, 0, 100);
    }

    public float GetCookingSpeedMul(ERestaurantFloorType floor, FoodType type)
    {
        float cookSpeedMul = 1 + _totalAddSpeedMul + (_addSetCookSpeedMul[(int)floor, (int)type] * 0.01f) + (_addEquipKitchenUtensilCookSpeedMul * 0.01f) /*+ (_addEquipStaffCookSpeedMul[(int)floor] * 0.01f)*/;
        DebugLog.Log("���� �ӵ�: "+ cookSpeedMul);
        return cookSpeedMul;
    }

    public float GetFoodPriceMul(ERestaurantFloorType floor, FoodType type)
    {
        float foodPriceMul = 1 + _foodPriceMul + (_addSetFoodPriceMul[(int)floor, (int)type] * 0.01f);
        DebugLog.Log("���� ��: "+ foodPriceMul);
        return foodPriceMul;
    }

    public void AppendPromotionCustomer(int value)
    {
        _addPromotionCustomer = Mathf.Clamp(_addPromotionCustomer + value, 1, 100);
    }

    public void SetGameSpeed(float value)
    {
        value = Mathf.Clamp(value, 0, 5);
        _totalAddSpeedMul = value;
    }


    public void SaveGameData()
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
            return;

        Param param = UserInfo.GetSaveUserData();
        BackendManager.Instance.SaveGameData("GameData", param);
        UserInfo.SaveStageData();
        DebugLog.Log("����");
    }

    public void AsyncSaveGameData()
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
            return;

        Param param = UserInfo.GetSaveUserData();
        BackendManager.Instance.SaveGameDataAsync("GameData", param, (bro) =>{
            UserInfo.SaveStageDataAsync();
            DebugLog.Log("�񵿱� ����");
        });
    }

    public void ChanceScene()
    {
        _foodPriceMul = 0;
        _totalAddSpeedMul = 0;
        _addPromotionCustomer = 1;
        //OnEquipStaffEffectCheck();
        OnEquipFurnitureEffectCheck();
        OnEquipKitchenUtensilEffectCheck();
        UserInfo.DataBindTip(UserInfo.CurrentStage);
        UserInfo.DataBindMoney();

        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            CheckSetDataEffect(floor);
        }
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        UserInfo.DataBindTip(UserInfo.CurrentStage);
        UserInfo.DataBindMoney();

        //UserInfo.OnChangeStaffHandler += (floor, type) => OnEquipStaffEffectCheck();
        //UserInfo.OnUpgradeStaffHandler += OnEquipStaffEffectCheck;
        UserInfo.OnChangeFurnitureHandler += (floor, type) => OnEquipFurnitureEffectCheck();
        UserInfo.OnChangeKitchenUtensilHandler += (floor, type) => OnEquipKitchenUtensilEffectCheck();
        UserInfo.OnChangeFurnitureHandler += (floor, type) => CheckSetDataEffect(floor);
        UserInfo.OnChangeKitchenUtensilHandler += (floor, type) => CheckSetDataEffect(floor);
        UserInfo.OnGiveRecipeHandler += OnGiveRecipeCheck;
        UserInfo.OnGiveGachaItemHandler += OnGiveGachaItemEffectCheck;
        UserInfo.OnGiveGachaItemHandler += OnUpgradeGachaItemCheck;
        UserInfo.OnUpgradeGachaItemHandler += OnUpgradeGachaItemCheck;
        BackendManager.OnExitHandler += SaveGameData;
        BackendManager.OnPauseHandler += SaveGameData;

        //OnEquipStaffEffectCheck();
        OnEquipFurnitureEffectCheck();
        OnEquipKitchenUtensilEffectCheck();
        OnGiveGachaItemEffectCheck();
        OnGiveRecipeCheck();
        OnUpgradeGachaItemCheck();
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            CheckSetDataEffect(floor);
        }
    }



    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.S))
        {
            UserInfo.LoadGameData(BackendManager.Instance.GetMyData("GameData"));
        }
#endif
    }


    private void OnEquipFurnitureEffectCheck()
    {
        _addEquipFurnitureScore = 0;
        _addEquipFurnitureMaxTipVolume = 0;
        _addEquipFurnitureTipPerMinute = 0;
        int addScore = 0;
        int maxTipVolume = 0;
        int tipPerMinute = 0;
        int cookSpeedMul = 0;

        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            for (int j = 0, cntJ = (int)FurnitureType.Length; j < cntJ; ++j)
            {
                FurnitureData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, (ERestaurantFloorType)i, (FurnitureType)j);

                if (data == null)
                    continue;

                addScore += data.AddScore;

                if (data.EquipEffectType == EquipEffectType.AddMaxTip)
                    maxTipVolume += data.EffectValue;

                else if (data.EquipEffectType == EquipEffectType.AddTipPerMinute)
                    tipPerMinute += data.EffectValue;

                else if (data.EquipEffectType == EquipEffectType.AddCookSpeed)
                    cookSpeedMul += data.EffectValue;
            }
        }
        

        _addEquipFurnitureScore = addScore;
        _addEquipFurnitureMaxTipVolume = maxTipVolume;
        _addEquipFurnitureTipPerMinute = tipPerMinute;
        _addEquipFurnitureCookSpeedMul = cookSpeedMul;
        OnChangeScoreHandler?.Invoke();
        OnChangeTipPerMinuteHandler?.Invoke();
    }


    private void OnEquipKitchenUtensilEffectCheck()
    {
        _addEquipKitchenUtensilScore = 0;
        _addEquipKitchenUtensilCookSpeedMul = 0;
        _addEquipKitchenUtensilTipPerMinute = 0;
        int addScore = 0;
        int maxTipVolume = 0;
        int cookSpeedMul = 0;
        int tipPerMinute = 0;

        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            for (int j = 0, cntJ = (int)KitchenUtensilType.Length; j < cntJ; ++j)
            {
                KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, (ERestaurantFloorType)i, (KitchenUtensilType)j);

                if (data == null)
                    continue;

                addScore += data.AddScore;

                if (data.EquipEffectType == EquipEffectType.AddMaxTip)
                    maxTipVolume += data.EffectValue;

                else if (data.EquipEffectType == EquipEffectType.AddCookSpeed)
                    cookSpeedMul += data.EffectValue;

                else if (data.EquipEffectType == EquipEffectType.AddTipPerMinute)
                    tipPerMinute += data.EffectValue;
            }
        }
      

        _addEquipKitchenUtensilScore = addScore;
        _addEquipKitchenUtensilTipVolume = tipPerMinute;
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
                throw new Exception("�������� ������������ �����ͺ��̽��� ��ϵǾ����� �ʽ��ϴ�: " + giveRecipeList[i]);

            if(!FoodDataManager.Instance.IsNeedMiniGame(giveRecipeList[i]))
                continue;

            _addGiveRecipeMiniGameTime += 5;
        }
    }


    private void CheckSetDataEffect(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        for(int i = 0, cnt = (int)FoodType.Length; i < cnt; ++i)
        {
            int foodTypeIndex = i;
            _addSetCookSpeedMul[floorIndex, foodTypeIndex] = 0;
            _addSetFoodPriceMul[floorIndex, foodTypeIndex] = 0;
        }

        FoodType kitchenFoodType = UserInfo.GetEquipKitchenUtensilFoodType(UserInfo.CurrentStage, floor);
        FoodType furnitureFoodType = UserInfo.GetEquipFurnitureFoodType(UserInfo.CurrentStage, floor);

        if(kitchenFoodType != FoodType.None)
        {
            _addSetCookSpeedMul[floorIndex, (int)kitchenFoodType] += 10;
        }

        if(furnitureFoodType != FoodType.None)
        {
            _addSetFoodPriceMul[floorIndex, (int)furnitureFoodType] += 10;
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
                DebugLog.LogError("������ ������ �����ͺ��̽��� �������� �ʽ��ϴ�: " + data.Key);
                continue;
            }

            if (!UserInfo.IsGiveGachaItem(gachaItemData))
            {
                DebugLog.LogError("�ش� �������� ������ �����ϳ� ���������� �ʽ��ϴ�: " + data.Key);
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
        OnChangeStaffSkillValueHandler?.Invoke();
        OnChangeMaxWaitCustomerCountHandler?.Invoke();
    }
}
