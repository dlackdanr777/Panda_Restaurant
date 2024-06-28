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

    [SerializeField] private int _maxTipValue;
    public int MaxTipValue => _maxTipValue;

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


    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul += value * 0.01f;
    }

    public void AddMaxTipValue(int value)
    {
        _maxTipValue += value;
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
        UserInfo.GiveFurniture("TABLE011");
        UserInfo.SetEquipFurniture("TABLE011");

        UserInfo.GiveFurniture("TABLE012");
        UserInfo.SetEquipFurniture("TABLE012");

        UserInfo.GiveFurniture("TABLE013");
        UserInfo.SetEquipFurniture("TABLE013");
    }


    private void Test()
    {
        UserInfo.DisarmEquipFurniture(FurnitureType.Table1);
    }

}
