using System;
using UnityEngine;

public struct CookingData
{
    private FoodData _foodData;
    public FoodData FoodData => _foodData;

    private TableData _tableData;
    public TableData TableData => _tableData;

    private float _cookTime;
    public float CookTime => _cookTime;

    private float _price;
    public float Price => _price;


    private Action _onCompleted;
    public Action OnCompleted => _onCompleted;

    public CookingData(FoodData foodData, TableData data, float cookTime, float cellPrice, Action onCompleted)
    {
        _foodData = foodData;
        _tableData = data;
        _cookTime = cookTime;
        _price = cellPrice;
        _onCompleted = onCompleted;
    }

    public bool IsDefault()
    {
        return _foodData == null;
    }

    public void SetDefault()
    {
        _foodData = null;
    }
}
