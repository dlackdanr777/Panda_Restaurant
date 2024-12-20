using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{

    [SerializeField] private CustomerController _customerController;
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private KitchenSystem _kitchenSystem;
    private Staff[] _staffs;

    private void Awake()
    {
        _staffs = new Staff[(int)StaffType.Length];
        Staff staffPrefab = Resources.Load<Staff>("Staff");

        for(int i = 0, cnt = _staffs.Length; i < cnt; ++i)
        {
            Staff staff = Instantiate(staffPrefab, transform);
            _staffs[i] = staff;
            staff.Init();
        }

        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            StaffData data = UserInfo.GetEquipStaff((StaffType)i);

            if (data == null)
                continue;

            _staffs[i].SetStaffData(data, _tableManager, _kitchenSystem, _customerController);
        }

        UserInfo.OnChangeStaffHandler += OnEquipEvent;
    }


    private void Update()
    {
        for (int i = 0, cnt = _staffs.Length; i < cnt; ++i)
        {
            if (_staffs[i] == null)
                continue;

            if (!_staffs[i].gameObject.activeInHierarchy)
                continue;

            _staffs[i].StaffAction();
            _staffs[i].UsingStaffSkill(_tableManager, _kitchenSystem, _customerController);
        }
    }


    private void OnEquipEvent()
    { 
        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            StaffData data = UserInfo.GetEquipStaff((StaffType)i);
            _staffs[i].SetStaffData(data, _tableManager, _kitchenSystem, _customerController);
        }
    }
}
