using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Object/FoodData")]
public class FoodData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _description;
    public string Description => _description;

    [SerializeField] private int _buyMinScore;
    public int BuyMinScore => _buyMinScore;

    [SerializeField] private int _buyPrice;
    public int BuyPrice => _buyPrice;

    [SerializeField] private bool _miniGameNeeded;
    public bool MiniGameNeeded => _miniGameNeeded;

    [SerializeField] private FoodLevelData[] _foodLevelData;


    public int GetSellPrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelData.Length - 1);
        return _foodLevelData[level].SellPrice;
    }

    public int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelData.Length - 1);
        return _foodLevelData[level].UpgradeMinScore;
    }

    public int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelData.Length - 1);
        return _foodLevelData[level].UpgradePrice;
    }

    public float GetCookingTime(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _foodLevelData.Length - 1);
        return _foodLevelData[level].CookingTime;
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

    [SerializeField] private float _cookingTime;
    public float CookingTime => _cookingTime;
}
