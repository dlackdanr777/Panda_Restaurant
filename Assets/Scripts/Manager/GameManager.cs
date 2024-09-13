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
     
    [SerializeField] private int _maxTipVolume;
    public int MaxTipVolume => _maxTipVolume;

    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => _cookingSpeedMul;

    [SerializeField] private float _tipMul = 0;
    public float TipMul => _tipMul;

    [SerializeField] private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;

    [SerializeField] private int _addScore = 0;
    public int AddSocre => _addScore;

    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;

    [SerializeField] private int _tipPerMinute;
    public int TipPerMinute => _tipPerMinute;


    private float _updateTimer;


    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul += value * 0.01f;
    }

    public void SetMaxTipVolume(int value)
    {
        DebugLog.Log("최대치 설정: " + value);
        _maxTipVolume = value;
    }

    public void AppendAddScore(int value)
    {
        _addScore += value;
        OnAppendAddScoreHandler?.Invoke();
    }

    public void AddCookingSpeedMul(float value)
    {
        _cookingSpeedMul += value * 0.01f;
        _cookingSpeedMul = Mathf.Clamp(_cookingSpeedMul, 1, 100);
    }

    public void AddTipMul(float value)
    {
        _tipMul += value * 0.01f;
        _tipMul = Mathf.Clamp(_tipMul, 1, 100);
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
}
