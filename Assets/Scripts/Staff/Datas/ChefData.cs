using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ChefData", menuName = "Scriptable Object/Staff/Chef")]
public class ChefData : StaffData
{
    [SerializeField] private ChefLevelData[] _chefLevelData;
    public override float SecondValue => _chefLevelData[0].FoodSpeedAddPercent;
    public override int MaxLevel => _chefLevelData.Length;

    public override float GetActionValue(int level)
    {
        if (_chefLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _chefLevelData[level - 1].FoodSpeedAddPercent;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ChefAction(kitchenSystem);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _chefLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        GameManager.Instance.AddCookingSpeedMul(_chefLevelData[staff.Level - 1].FoodSpeedAddPercent);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
        GameManager.Instance.AddCookingSpeedMul(-_chefLevelData[staff.Level - 1].FoodSpeedAddPercent);
    }

    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].UpgradeMinScore;
    }


    public override int GetUpgradePrice(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].UpgradeMoneyData.Price;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].TipAddPercent;
    }
}


[Serializable]
public class ChefLevelData : StaffLevelData
{
    [Range(0f, 200f)] [SerializeField] private float _foodSpeedAddPercent;
    public float FoodSpeedAddPercent => _foodSpeedAddPercent;
}
