using System;
using System.Collections.Generic;
using UnityEngine;

public class FoodData : BasicData, ShopData
{
    private static readonly int MAX_LEVEL = 10;
    private static readonly float[] COOKING_TIME_RATIO = { 1, 1f, 1f, 1f, 1f, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f };
    private static readonly float[] SELL_PRICE_RATIO = {1, 1.2f, 1.3f, 1.4f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f };
    private static readonly float[] UPGRADE_SCORE_RATIO = {1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.8f, 3.3f, 3.9f, 4.6f };
    private static readonly float[] UPGRADE_PRICE_RATIO = {1.6f, 1.6f, 1.6f, 1.6f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f };
    private static readonly int[] UPGRADE_HIDDEN_ITEM_COUNT = { 1, 2, 3, 4, 5, 5, 6, 6, 7 };

    [Space]
    [Header("FoodData")]

    private string _needItem;
    public string NeedItem => _needItem;

    protected FoodMiniGameData _foodMiniGameData;
    public FoodMiniGameData FoodMiniGameData => _foodMiniGameData;

    protected FoodType _foodType;
    public FoodType FoodType => _foodType;

    protected SalesLocationType _salesLocationType;
    public SalesLocationType SalesLocationType => _salesLocationType;
    
    protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    protected int _buyScore;
    public int BuyScore => _buyScore;

    protected int _buyPrice;
    public int BuyPrice => _buyPrice;

    private int _sellPrice;
    public int SellPrice => _sellPrice;

    private float _cookingTime;
    public float CookingTime => _cookingTime;

    private bool _needMiniGame;
    public bool MiniGameNeeded => _needMiniGame;

    public FoodData(Sprite sprite, Sprite thumbnailSprite, string name, string id, string description, FoodType foodType, MoneyType moneyType, int buyScore, int buyPrice, bool needMiniGame, string needItem, int sellPrice, float cookingTime, FoodMiniGameData foodMiniGameData)
    {
        _salesLocationType = SalesLocationType.Shop;

        _sprite = sprite;
        _thumbnailSprite = thumbnailSprite;
        _name = name;
        _id = id;
        _description = description;
        _foodType = foodType;
        _moneyType = moneyType;
        _buyScore = buyScore;
        _buyPrice = buyPrice;
        _needMiniGame = needMiniGame;
        _needItem = needItem;
        _sellPrice = sellPrice;
        _cookingTime = cookingTime;
        _foodMiniGameData = foodMiniGameData;
    }


    public int GetSellPrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, MAX_LEVEL - 1);
        return (int)(_sellPrice * SELL_PRICE_RATIO[level]);
    }

    public int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, MAX_LEVEL - 1);
        return (int)(_buyScore * UPGRADE_SCORE_RATIO[level]);
    }

    public int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, MAX_LEVEL - 1);

        int buyPrice = _buyPrice;
        for(int i = 0; i < level; i++)
        {
            buyPrice = (int)(buyPrice * UPGRADE_PRICE_RATIO[i]);
        }

        return buyPrice;
    }

    public string GetNeedItem(int level)
    {
        return NeedItem;
    }

    public int GetNeedItemCount(int level)
    {
        level = Mathf.Clamp(level - 1, 0, MAX_LEVEL - 1);
        return string.IsNullOrWhiteSpace(NeedItem) ? 0 : UPGRADE_HIDDEN_ITEM_COUNT[level];
    }

    public float GetCookingTime(int level)
    {
        level = Mathf.Clamp(level - 1, 0, MAX_LEVEL - 1);
        return _cookingTime * COOKING_TIME_RATIO[level];
        
    }

    public bool UpgradeEnable(int level)
    {
        return level < MAX_LEVEL;
    }
}
