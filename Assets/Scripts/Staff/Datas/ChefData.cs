using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ChefData", menuName = "Scriptable Object/Staff/Chef")]
public class ChefData : StaffData
{
    [Range(0f, 2f)] [SerializeField] private float _cookingSpeedMul;
    [SerializeField] private ChefLevelData[] _chefLevelData;
    public override float SecondValue => 0;

    public override float GetActionValue(int level)
    {
        if (_chefLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _chefLevelData[level - 1].FoodPriceMultiple;
    }

    public override IStaffAction GetStaffAction(TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ChefAction(kitchenSystem);
    }


    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        GameManager.Instance.AddCookingSpeedMul(_cookingSpeedMul);

        GameManager.Instance.AddScore(_chefLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(_chefLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
        GameManager.Instance.AddCookingSpeedMul(-_cookingSpeedMul);

        GameManager.Instance.AddScore(-_chefLevelData[staff.Level - 1].ScoreIncrement);
        GameManager.Instance.AddTipMul(-_chefLevelData[staff.Level - 1].TipAddPercent);
    }

    public override void UseSkill()
    {
        throw new NotImplementedException();
    }
}


[Serializable]
public class ChefLevelData
{
    [SerializeField] private float _foodPriceMultiple;
    public float FoodPriceMultiple => _foodPriceMultiple;
    [Range(0f, 200f)] [SerializeField] private float _tipAddPercent;
    public float TipAddPercent => _tipAddPercent;

    [SerializeField] private int _scoreIncrement;
    public int ScoreIncrement => _scoreIncrement;

    [SerializeField] private int _nextLevelUpgradeMoney;
    public int NextLevelUpgradeMoney => _nextLevelUpgradeMoney;
}
