using Muks.DataBind;
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

    public static event Action OnAppendAddScoreHandler;

    public Vector2 OutDoorPos => new Vector2(24.6f, 7.64f);

    [SerializeField] private int _totalTabCount = 5;
    public int TotalTabCount => _totalTabCount;

    [SerializeField] private int _maxTipVolume;
    public int MaxTipVolume => _maxTipVolume;

    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => _cookingSpeedMul;

    [SerializeField] private float _tipMul = 1;
    public float TipMul => _tipMul;

    [SerializeField] private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;

    [SerializeField] private int _addScore = 0;
    public int AddSocre => _addScore;

    private int _addPromotionCustomer = 1;
    public int AddPromotionCustomer => _addPromotionCustomer;


    [SerializeField] private int _moneyPerMinute;
    public int MoneyPerMinute => _moneyPerMinute;

    [SerializeField] private int _tipPerMinute;
    public int TipPerMinute => _tipPerMinute;


    private float _updateTimer;


    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul += value * 0.01f;
    }

    public void SetMaxTipVolume(int value)
    {
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
    }

    public void AddMoneyPerMinute(int value)
    {
        _moneyPerMinute += value;
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        UserInfo.GiveRecipe("FOOD01");
        DataBind.SetUnityActionValue("Test", Test);
    }


    private void Start()
    {
        UserInfo.GiveFurniture("TABLE01_1");
        UserInfo.SetEquipFurniture("TABLE01_1");

        UserInfo.GiveFurniture("TABLE01_2");
        UserInfo.SetEquipFurniture("TABLE01_2");

        UserInfo.GiveFurniture("TABLE01_3");
        UserInfo.SetEquipFurniture("TABLE01_3");

        UserInfo.GiveFurniture("TABLE01_4");
        UserInfo.SetEquipFurniture("TABLE01_4");

/*        UserInfo.GiveFurniture("TABLE01_5");
        UserInfo.SetEquipFurniture("TABLE01_5");*/

        UserInfo.GiveFurniture("FLOWER01");
        UserInfo.SetEquipFurniture("FLOWER01");

        UserInfo.GiveFurniture("RACK01");
        UserInfo.SetEquipFurniture("RACK01");

        UserInfo.GiveFurniture("WALLPAPER01");
        UserInfo.SetEquipFurniture("WALLPAPER01");

        UserInfo.GiveFurniture("ACC01");
        UserInfo.SetEquipFurniture("ACC01");

        UserInfo.GiveFurniture("FRAME01");
        UserInfo.SetEquipFurniture("FRAME01");

/*        UserInfo.GiveFurniture("COUNTER01");
        UserInfo.SetEquipFurniture("COUNTER01");*/
    }


    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if(60 <= _updateTimer)
        {
            _updateTimer = 0;
            UserInfo.AppendTip(_tipPerMinute);
            UserInfo.AppendMoney(_moneyPerMinute);
        }

    }


    private void Test()
    {
        UserInfo.DisarmEquipFurniture(FurnitureType.Table1);
    }

}
