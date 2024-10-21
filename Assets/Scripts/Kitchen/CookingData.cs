using System;
using UnityEngine;

public struct CookingData
{
    private string _id;
    public string Id => _id;

    private float _cookingTime;
    public float CookingTime => _cookingTime;

    private float _price;
    public float Price => _price;

    private Sprite _sprite;
    public Sprite Sprite => _sprite;

    private Action _onCompleted;
    public Action OnCompleted => _onCompleted;

    public CookingData(string id, float cookingTime, float price, Sprite sprite, Action onCompleted)
    {
        _id = id;
        _cookingTime = cookingTime;
        _price = price;
        _sprite = sprite;
        _onCompleted = onCompleted;
    }

    public bool IsDefault()
    {
        return string.IsNullOrEmpty(_id);
    }
}
