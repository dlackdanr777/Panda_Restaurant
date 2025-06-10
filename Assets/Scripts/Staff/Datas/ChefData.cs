using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ChefData", menuName = "Scriptable Object/Staff/Chef")]
public class ChefData : StaffData
{
    [Header("셰프 데이터")]
    [SerializeField] private Sprite _backSprite;
    public Sprite BackSprite => _backSprite;

    [SerializeField] private Sprite _handSprite;
    public Sprite HandSprite => _handSprite;

    [SerializeField] private Vector2 _handOffset = new Vector2(0, 0.588f);
    public Vector2 HandOffset => _handOffset;

    [SerializeField] private ChefLevelData[] _chefLevelData;
    public override float SecondValue => _chefLevelData[0].FoodSpeedAddPercent;
    public override int MaxLevel => _chefLevelData.Length;

    public override float GetSpeed(int level) => _speed;

    public override float GetActionValue(int level)
    {
        if (_chefLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _chefLevelData[level - 1].FoodSpeedAddPercent;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ChefAction(staff, tableManager, kitchenSystem);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _chefLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(1);
        staff.SetSpriteDir(-1);
        EquipStaffType type = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, staff.StaffData);
        staff.transform.position = tableManager.GetStaffPos(staff.EquipFloorType, type);
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.SetAlpha(0);
    }


    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _chefLevelData.Length - 1);
        return _chefLevelData[level].UpgradeMoneyData;
    }
}


[Serializable]
public class ChefLevelData : StaffLevelData
{
    [Range(0f, 200f)] [SerializeField] private float _foodSpeedAddPercent;
    public float FoodSpeedAddPercent => _foodSpeedAddPercent;
}
