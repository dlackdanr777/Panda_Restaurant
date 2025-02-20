using System.Collections.Generic;
using UnityEngine;

public class StaffGroup : MonoBehaviour
{
    [Header("Option")]
    [SerializeField] private ERestaurantFloorType _floorType;
    public ERestaurantFloorType FloorType => _floorType;



    private CustomerController _customerController;
    private TableManager _tableManager;
    private KitchenSystem _kitchenSystem;

    private Dictionary<StaffType, Staff> _staffDic = new Dictionary<StaffType, Staff>();

    public void Init(CustomerController customerController, TableManager tableManager, KitchenSystem kitchenSystem)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _kitchenSystem = kitchenSystem;

        Staff staffPrefab = Resources.Load<Staff>("Staff");
        for (int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            StaffType type = (StaffType)i;
            Staff staff = Instantiate(staffPrefab, transform);
            staff.Init();

            _staffDic.Add(type, staff);
            StaffData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);

            if (data == null)
                continue;
            staff.SetStaffData(data, _floorType, _tableManager, _kitchenSystem, _customerController);
        }

        UserInfo.OnChangeStaffHandler += OnEquipEvent;
    }


    public void UpdateStaff()
    {
        foreach(Staff staff in _staffDic.Values)
        {
            if (staff == null)
                continue;

            if (!staff.gameObject.activeInHierarchy)
                continue;

            staff.StaffAction();
            staff.UsingStaffSkill(_tableManager, _kitchenSystem, _customerController);
        }
    }


    private void OnEquipEvent(ERestaurantFloorType floorType, StaffType type)
    {
        if (floorType != _floorType)
            return;

        StaffData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);
        _staffDic[type].SetStaffData(data, _floorType, _tableManager, _kitchenSystem, _customerController);
    }

    private void OnDestroy()
    {
        foreach(Staff staff in _staffDic.Values)
        {
            staff?.DestroyStaff();
        }

        UserInfo.OnChangeStaffHandler -= OnEquipEvent;
    }
}
