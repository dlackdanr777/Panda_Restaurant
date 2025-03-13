using System;
using UnityEngine;

public struct CookingData
{
    private FoodData _foodData;
    public FoodData FoodData => _foodData;

    private float _cookTime;
    public float CookTime => _cookTime;

    private float _price;
    public float Price => _price;


    private Action _onCompleted;
    public Action OnCompleted => _onCompleted;

    public CookingData(FoodData foodData, float cookTime, float cellPrice, Action onCompleted)
    {
        _foodData = foodData;
        _cookTime = cookTime;
        _price = cellPrice;
        _onCompleted = onCompleted;
    }

    public bool IsDefault()
    {
        return _foodData == null;
    }
}
