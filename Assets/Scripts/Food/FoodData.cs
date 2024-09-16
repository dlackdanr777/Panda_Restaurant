using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Object/FoodData")]
public class FoodData : BasicData
{
    [Space]
    [Header("FoodData")]

    [SerializeField] private bool _miniGameNeeded;
    public bool MiniGameNeeded => _miniGameNeeded;

    [SerializeField] private FoodLevelData[] _foodLevelData;
    public int MaxLevel => _foodLevelData.Length;


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

    public bool UpgradeEnable(int level)
    {
        return level < _foodLevelData.Length;
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
