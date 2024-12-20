using System;
using System.Collections.Generic;
using UnityEngine;

public class FoodData : ShopData
{
    [Space]
    [Header("FoodData")]

    private string _needItem;
    public string NeedItem => _needItem;

    [SerializeField] private List<FoodLevelData> _foodLevelDataList;
    public int MaxLevel => _foodLevelDataList.Count;

    private FoodMiniGameData _foodMiniGameData;
    public FoodMiniGameData FoodMiniGameData => _foodMiniGameData;

    public bool MiniGameNeeded => _foodMiniGameData != null ? true : false;

    public FoodData(Sprite sprite, Sprite thumbnailSprite, string name, string id, MoneyType moneyType, int buyScore, int buyPrice, string needItem, List<FoodLevelData> foodLevelDataList, FoodMiniGameData foodMiniGameData)
    {
        _sprite = sprite;
        _thumbnailSPrite = thumbnailSprite;
        _name = name;
        _id = id;
        _moneyType = moneyType;
        _buyScore = buyScore;
        _buyPrice = buyPrice;
        _needItem = needItem;
        _foodLevelDataList = foodLevelDataList;
        _foodMiniGameData = foodMiniGameData;
    }


    public int GetSellPrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelDataList.Count - 1);
        return _foodLevelDataList[level].SellPrice;
    }

    public int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelDataList.Count - 1);
        return _foodLevelDataList[level].UpgradeMinScore;
    }

    public int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelDataList.Count - 1);
        return _foodLevelDataList[level].UpgradePrice;
    }

    public string GetNeedItem(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelDataList.Count - 1);
        return _foodLevelDataList[level].NeedItem;
    }

    public float GetCookingTime(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelDataList.Count - 1);
        return _foodLevelDataList[level].CookingTime;
        
    }

    public bool UpgradeEnable(int level)
    {
        return level < _foodLevelDataList.Count;
    }
}


[Serializable]
public class FoodLevelData
{
    [SerializeField] private int _sellPrice;
    public int SellPrice => _sellPrice;

    [SerializeField] private int _upgradeMinScore;
    public int UpgradeMinScore => _upgradeMinScore;

    [SerializeField] private int _upgradePrice;
    public int UpgradePrice => _upgradePrice;

    private string _needItem;
    public string NeedItem => _needItem;

    [SerializeField] private float _cookingTime;
    public float CookingTime => _cookingTime;

    public FoodLevelData(int sellPrice, int upgradeMinScore, int upgradePrice, string needItem, float cookingTime)
    {
        _sellPrice = sellPrice;
        _upgradeMinScore = upgradeMinScore;
        _upgradePrice = upgradePrice;
        _needItem = needItem;
        _cookingTime = cookingTime;
    }
}


public class FoodMiniGameData
{
    private string _id;
    public string Id => _id;

    private int _successCount;
    public int SuccessCount => _successCount;

    private int _maxHealth;
    public int MaxHealth => _maxHealth;

    private int _firstHealth;
    public int FirstHealth => _firstHealth;

    private int _addHealth;
    public int AddHealth => _addHealth;

    private int _clearAddTime;
    public int ClearAddTime => _clearAddTime;

    public FoodMiniGameData(string id, int successCount, int maxHealth, int firstHealth, int addHealth, int clearAddTime)
    {
        _id = id;
        _successCount = successCount;
        _maxHealth = maxHealth;
        _firstHealth = firstHealth;
        _addHealth = addHealth;
        _clearAddTime = clearAddTime;
    }
}
