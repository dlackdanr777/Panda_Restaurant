using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ManagerData", menuName = "Scriptable Object/Staff/Manager")]
public class ManagerData : StaffData
{
    [SerializeField] private ManagerLevelData[] _managerLevelData;
    public override float SecondValue => 0;
    public override int MaxLevel => _managerLevelData.Length;


    private bool _isSubscribed;
    private Staff _staff;
    private TableManager _tableManager;

    public override float GetActionValue(int level)
    {
        if (_managerLevelData.Length < level - 1 || level < 0)
            throw new ArgumentOutOfRangeException("레벨의 범위를 넘어섰습니다.");

        return _managerLevelData[level - 1].MaxWaitCustomerCount;
    }

    public override IStaffAction GetStaffAction(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        return new ManagerAction(tableManager);
    }

    public override bool UpgradeEnable(int level)
    {
        return level < _managerLevelData.Length;
    }

    public override void AddSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        _staff = staff;
        _tableManager = tableManager;
        staff.SetAlpha(1);
        staff.SetLayer("Manager", 0);

        Vector3 pos = tableManager.GetStaffPos(staff.EquipFloorType, StaffType.Manager);
        staff.transform.position = UserInfo.IsEquipFurniture(UserInfo.CurrentStage, staff.EquipFloorType, FurnitureType.Counter) ? pos : pos - new Vector3(0, 1.75f, 0);

        if(!_isSubscribed)
        {
            UserInfo.OnChangeFurnitureHandler += OnChangeCounterEvent;
            _isSubscribed = true;
        }
    }

    public override void RemoveSlot(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        staff.transform.position = Vector3.zero;
        staff.SetAlpha(0);

        if (_isSubscribed)
        {
            UserInfo.OnChangeFurnitureHandler -= OnChangeCounterEvent;
            _isSubscribed = false;
        }
    }


    public override void Destroy()
    {
        if (_isSubscribed)
        {
            UserInfo.OnChangeFurnitureHandler -= OnChangeCounterEvent;
            _isSubscribed = false;
        }
    }


    public override int GetUpgradeMinScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMinScore;
    }


    public override UpgradeMoneyData GetUpgradeMoneyData(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].UpgradeMoneyData;
    }

    public override int GetAddScore(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].ScoreIncrement;
    }

    public override float GetAddTipMul(int level)
    {
        level = Mathf.Clamp(level - 1, 0, _managerLevelData.Length - 1);
        return _managerLevelData[level].TipAddPercent;
    }


    private void OnChangeCounterEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (floorType != _staff.EquipFloorType || type != FurnitureType.Counter)
            return;

        Vector3 pos = _tableManager.GetStaffPos(_staff.EquipFloorType, StaffType.Manager);
        _staff.transform.position = UserInfo.IsEquipFurniture(UserInfo.CurrentStage, _staff.EquipFloorType, FurnitureType.Counter) ? pos : pos - new Vector3(0, 1.75f, 0);
    }
}


[Serializable]
public class ManagerLevelData : StaffLevelData
{
    [Range(0, 100)] [SerializeField] private int _maxWaitCustomerCount;
    public int MaxWaitCustomerCount => _maxWaitCustomerCount;
}
