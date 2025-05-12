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

    [SerializeField] private int _totalTabCount = 8;
    public int TotalTabCount => _totalTabCount;

    [SerializeField] private float _foodPriceMul = 0;

    private float[] _foodTypePriceMul = new float[(int)FoodType.Length];
    public float GetFoodTypePriceMul(FoodType type) { return 1 + _foodTypePriceMul[(int)type]; }


    [SerializeField] private float _totalAddSpeedMul = 0;

    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;

    private int _maxWaitCustomerCount = 10;
    public int MaxWaitCustomerCount => Mathf.Clamp(_maxWaitCustomerCount + _addGachaItemWaitCustomerMaxCount /*+ _addEquipStaffMaxWaitCustomerCount*/, 0, 30);
    public float AddCustomerSpeedMul => 1 + _totalAddSpeedMul + (0.01f * _addGachaItemCustomerSpeedPercent);
    public float AddSpecialCustomerSpawnMul => 1;
    public float AddGatecrasherCustomerDamageMul => 1;
    

    [SerializeField] private float _cookingSpeedMul = 1;
    public float SubCookingTime => _subGachaItemAllCookingTime;
    public int AddFoodPrice => _addGachaItemAllFoodPriceMul;


    public int AddSocre => _addEquipFurnitureScore + _addEquipKitchenUtensilScore + _addGiveGachaItemScore;
    public float TipMul =>  1 /*Mathf.Clamp(_addEquipStaffTipMul * 0.01f, 0f, 10000f)*/;

    public int TipPerMinute => _addEquipFurnitureTipPerMinute + _addEquipKitchenUtensilTipPerMinute + _addGiveGachaItemTipPerMinute;
    public int MaxTipVolume => _addEquipFurnitureMaxTipVolume + _addEquipKitchenUtensilTipVolume + Mathf.FloorToInt(_addEquipKitchenUtensilTipVolume + _addEquipFurnitureMaxTipVolume);

    public float AddFerverTime => _addGachaItemFeverTime;

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


    [SerializeField] private float _addGachaItemCustomerSpeedPercent; //�մ� ���ǵ� ��� n% (UPGRADE01)

    [SerializeField] private float _subGachaItemAllCookingTime; //��ü �丮 �ð� -n�� ���� (UPGRADE02)
    [SerializeField] private Dictionary<FoodType, float> _subGachaItemFoodCookingTimeMulDic = new Dictionary<FoodType, float>(); //�Ӽ� �丮 �ð� -n�� ���� (UPGRADE03 ~ 09)

    [SerializeField] private int _addGachaItemAllFoodPriceMul; //��ü ���� ���� ���� n% (UPGRADE10)
    private Dictionary<FoodType, int> _addGachaItemFoodPriceMulDic = new Dictionary<FoodType, int>(); //�Ӽ� ���� ���� ���� n% (UPGRADE11 ~ 17)

    [SerializeField] private float _addGachaItemStaffSkillTime; //��ü ���� ��ų ���� �ð� ����(+) (UPGRADE18)
    [SerializeField] private Dictionary<StaffGroupType, float> _addGachaItemStaffSkillTimeDic = new Dictionary<StaffGroupType, float>(); //���� ��Ÿ�� ���� n% (UPGRADE18 ~ 24)

    [SerializeField] private Dictionary<StaffGroupType, float> _addGachaItemStaffSpeedMulDic = new Dictionary<StaffGroupType, float>(); //���� ���ǵ� ���� n% (UPGRADE25 ~ 28)

    [SerializeField] private float _addGachaItemFeverTime; //�ǹ� Ÿ�� �ð� ���� + (UPGRADE29)

    [SerializeField] private int _addGachaItemWaitCustomerMaxCount; //�ִ� �ټ��� �մ� ���� + (UPGRADE30)


    public float GetStaffSpeedMul(StaffGroupType type)
    {
        if (_addGachaItemStaffSpeedMulDic.TryGetValue(type, out float value))
            return  _totalAddSpeedMul + (value * 0.01f);

        DebugLog.LogError("���� ���ǵ� ���� ȿ���� ã�� �� �����ϴ�: " + type);
        return _totalAddSpeedMul;
    }

    public float GetStaffSkillTimeMul(StaffGroupType type)
    {
        if (_addGachaItemStaffSkillTimeDic.TryGetValue(type, out float value))
            return (_addGachaItemStaffSkillTime * 0.01f) + (value * 0.01f);

        DebugLog.LogError("���� ��ų �ð� ���� ȿ���� ã�� �� �����ϴ�: " + type);
        return 0;
    }

    public float GetSubCookingTimeMul(FoodType type)
    {
        if (_subGachaItemFoodCookingTimeMulDic.TryGetValue(type, out float value))
            return (_subGachaItemAllCookingTime * 0.01f) + (value * 0.01f);

        DebugLog.LogError("�丮 �ð� ���� ȿ���� ã�� �� �����ϴ�: " + type);
        return 0;
    }

    public float GetAddFoodPriceMul(FoodType type)
    {
        if (_addGachaItemFoodPriceMulDic.TryGetValue(type, out int value))
            return (_addGachaItemAllFoodPriceMul * 0.01f) + (value * 0.01f);

        DebugLog.LogError("���� ���� ���� ȿ���� ã�� �� �����ϴ�: " + type);
        return 0;
    }


    private void OnUpgradeGachaItemCheck()
    {
        _addGachaItemCustomerSpeedPercent = 0;
        _subGachaItemAllCookingTime = 0;
        _addGachaItemAllFoodPriceMul = 0;
        _addGachaItemStaffSkillTime = 0;
        _addGachaItemFeverTime = 0;
        _addGachaItemWaitCustomerMaxCount = 0;

        for(int i = 0, cnt = (int)FoodType.Length; i < cnt; ++i)
        {
            _subGachaItemFoodCookingTimeMulDic[(FoodType)i] = 0;
            _addGachaItemFoodPriceMulDic[(FoodType)i] = 0;
        }

        for(int i = 0, cnt = (int)StaffGroupType.Length; i < cnt; ++i)
        {
            _addGachaItemStaffSkillTimeDic[(StaffGroupType)i] = 0;
            _addGachaItemStaffSpeedMulDic[(StaffGroupType)i] = 0;
        }

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
                    _addGachaItemCustomerSpeedPercent += addValue;
                    break;

                case UpgradeType.UPGRADE02:
                    _subGachaItemAllCookingTime += addValue;
                    break;

                case UpgradeType.UPGRADE03:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Natural] += addValue;
                    break;

                case UpgradeType.UPGRADE04:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Modern] += addValue;
                    break;

                case UpgradeType.UPGRADE05:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Vintage] += addValue;
                    break;

                case UpgradeType.UPGRADE06:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Traditional] += addValue;
                    break;

                case UpgradeType.UPGRADE07:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Tropical] += addValue;
                    break;

                case UpgradeType.UPGRADE08:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Luxury] += addValue;
                    break;

                case UpgradeType.UPGRADE09:
                    _subGachaItemFoodCookingTimeMulDic[FoodType.Cozy] += addValue;
                    break;

                case UpgradeType.UPGRADE10:
                    _addGachaItemAllFoodPriceMul += (int)addValue;
                    break;

                case UpgradeType.UPGRADE11:
                    _addGachaItemFoodPriceMulDic[FoodType.Natural] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE12:
                    _addGachaItemFoodPriceMulDic[FoodType.Modern] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE13:
                    _addGachaItemFoodPriceMulDic[FoodType.Vintage] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE14:
                    _addGachaItemFoodPriceMulDic[FoodType.Traditional] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE15:
                    _addGachaItemFoodPriceMulDic[FoodType.Tropical] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE16:
                    _addGachaItemFoodPriceMulDic[FoodType.Luxury] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE17:
                    _addGachaItemFoodPriceMulDic[FoodType.Cozy] += (int)addValue;
                    break;

                case UpgradeType.UPGRADE18:
                    _addGachaItemStaffSkillTime += addValue;
                    break;

                case UpgradeType.UPGRADE19:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Manager] += addValue;
                    break;

                case UpgradeType.UPGRADE20:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Waiter] += addValue;
                    break;

                case UpgradeType.UPGRADE21:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Chef] += addValue;
                    break;

                case UpgradeType.UPGRADE22:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Marketer] += addValue;
                    break;

                case UpgradeType.UPGRADE23:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Cleaner] += addValue;
                    break;

                case UpgradeType.UPGRADE24:
                    _addGachaItemStaffSkillTimeDic[StaffGroupType.Guard] += addValue;
                    break;

                case UpgradeType.UPGRADE25:
                    _addGachaItemStaffSpeedMulDic[StaffGroupType.Manager] += addValue;
                    break;

                case UpgradeType.UPGRADE26:
                    _addGachaItemStaffSpeedMulDic[StaffGroupType.Chef] += addValue;
                    break;

                case UpgradeType.UPGRADE27:
                    _addGachaItemStaffSpeedMulDic[StaffGroupType.Cleaner] += addValue;
                    break;

                case UpgradeType.UPGRADE28:
                    _addGachaItemStaffSpeedMulDic[StaffGroupType.Guard] += addValue;
                    break;

                case UpgradeType.UPGRADE29:
                    _addGachaItemFeverTime += addValue;
                    break;

                case UpgradeType.UPGRADE30:
                    _addGachaItemWaitCustomerMaxCount += (int)addValue;
                    break;

            }
        }
        
        OnChangeScoreHandler?.Invoke();
        OnChangeStaffSkillValueHandler?.Invoke();
        OnChangeMaxWaitCustomerCountHandler?.Invoke();
    }





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

        if(Input.GetKeyDown(KeyCode.A))
        {
            TimeManager.Instance.AddTime("Test", 100);
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
        _addEquipKitchenUtensilTipVolume = maxTipVolume;
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

            //_addGiveRecipeMiniGameTime += 5;
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

        float effectValue = kitchenFoodType != FoodType.None && furnitureFoodType != FoodType.None && kitchenFoodType == furnitureFoodType ? 15f : 10f;
        if(kitchenFoodType != FoodType.None)
        {
            _addSetCookSpeedMul[floorIndex, (int)kitchenFoodType] += effectValue;
        }

        if(furnitureFoodType != FoodType.None)
        {
            _addSetFoodPriceMul[floorIndex, (int)furnitureFoodType] += effectValue;
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
}
