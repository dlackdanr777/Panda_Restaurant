using JetBrains.Annotations;
using System;
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


    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => _cookingSpeedMul + (_addEquipKitchenUtensilCookSpeedMul * 0.01f) + (_addEquipSetDataCookSpeedMul * 0.01f);

    public int AddSocre => _addEquipStaffScore + _addEquipFurnitureScore + _addEquipKitchenUtensilScore;
    public float TipMul => 1 + (_addEquipStaffTipMul * 0.01f);

    [SerializeField] private int _tipPerMinute;
    public int TipPerMinute => _tipPerMinute + _addEquipFurnitureTipPerMinute + _addEquipKitchenUtensilTipPerMinute + _addEquipSetDataTipPerMinute;
    public int MaxTipVolume => _addEquipFurnitureMaxTipVolume;



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

    public void AddTipPerMinute(int value)
    {
        _tipPerMinute += value;
        OnChangeTipPerMinuteHandler?.Invoke();
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

        UserInfo.OnChangeStaffHandler += OnEquipStaffEvent;
        UserInfo.OnUpgradeStaffHandler += OnEquipStaffEvent;
        UserInfo.OnChangeFurnitureHandler += (type) => OnEquipFurnitureEvent();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => OnEquipKitchenUtensilEvent();
        UserInfo.OnChangeFurnitureHandler += (type) => CheckSetDataEnabled();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => CheckSetDataEnabled();

        OnEquipStaffEvent();
        OnEquipFurnitureEvent();
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
            UserInfo.AddTip(_tipPerMinute);
        }
    }


    private void OnEquipStaffEvent()
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

    private void OnEquipFurnitureEvent()
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

    private void OnEquipKitchenUtensilEvent()
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
}
