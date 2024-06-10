using System.Collections;
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

    public Vector2 OutDoorPos => new Vector2(30f, 3.32f);
    [SerializeField] private int _tip;
    [SerializeField] private float _cookingSpeedMul = 1;
    public float CookingSpeedMul => _cookingSpeedMul;
    [SerializeField] private float _tipMul = 1;
    public float TipMul => _tipMul;
    [SerializeField] private float _foodPriceMul = 1;
    public float FoodPriceMul => _foodPriceMul;
    [SerializeField] private int _addScore = 1;
    public float AddSocre => _addScore;


    public void AddFoodPriceMul(float value)
    {
        _foodPriceMul += value;
    }

    public void AddScore(int value)
    {
        _addScore += value;

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

    public void AppendTip(int value)
    { 
        _tip += (int)(value * _tipMul);
    }




    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

}
