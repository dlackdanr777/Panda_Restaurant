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

    private FeverSystem _feverSystem;

    private Dictionary<EquipStaffType, Staff> _staffDic = new Dictionary<EquipStaffType, Staff>();



    public void Init(CustomerController customerController, TableManager tableManager, KitchenSystem kitchenSystem, FeverSystem feverStstem)
    {
        _customerController = customerController;
        _tableManager = tableManager;
        _kitchenSystem = kitchenSystem;
        _feverSystem = feverStstem;
        for (int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            EquipStaffType type = (EquipStaffType)i;
            Staff staff = ObjectPoolManager.Instance.SpawnStaff(type, transform.position, transform);;
            staff.name = _floorType.ToString() + "_" + (EquipStaffType)i;
            staff.Init(type, tableManager, kitchenSystem, customerController, feverStstem);

            _staffDic.Add(type, staff);
            StaffData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);

            if (data == null)
                continue;

            if (type == EquipStaffType.Marketer)
            {
                StaffMarketer marketer = (StaffMarketer)_staffDic[type];
                marketer.SetSkillEffect(_customerController.MarketerSkillEffect);
            }
            
            staff.SetStaffData(data, _floorType);
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


    private void OnEquipEvent(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (floorType != _floorType)
            return;

        StaffData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);
        _staffDic[type].SetStaffData(data, _floorType);
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
