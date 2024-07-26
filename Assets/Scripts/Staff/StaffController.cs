using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{

    [SerializeField] private CustomerController _customerController;
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private KitchenSystem _kitchenSystem;
    private Staff[] _staffs;

    public void EquipStaff(StaffData data)
    {
        StaffType type = StaffDataManager.Instance.GetStaffType(data);
        _staffs[(int)type].SetStaffData(data, _tableManager, _kitchenSystem, _customerController);
        UserInfo.SetEquipStaff(data);
    }



    private void Awake()
    {
        _staffs = new Staff[(int)StaffType.Length];
        Staff staffPrefab = Resources.Load<Staff>("Staff");

        for(int i = 0, cnt = _staffs.Length; i < cnt; ++i)
        {
            Staff staff = Instantiate(staffPrefab, transform);
            _staffs[i] = staff;
        }

/*        StaffData data1 = StaffDataManager.Instance.GetStaffData("STAFF01");
        StaffData data2 = StaffDataManager.Instance.GetStaffData("STAFF20");
        StaffData data3 = StaffDataManager.Instance.GetStaffData("STAFF27");
        StaffData data4 = StaffDataManager.Instance.GetStaffData("STAFF34");
        StaffData data5 = StaffDataManager.Instance.GetStaffData("STAFF08");

        EquipStaff(data1);
        EquipStaff(data2);
        EquipStaff(data3);
        EquipStaff(data4);
        EquipStaff(data5);

        UserInfo.GiveStaff(data1);
        UserInfo.GiveStaff(data2);
        UserInfo.GiveStaff(data3);
        UserInfo.GiveStaff(data4);
        UserInfo.GiveStaff(data5);*/
    }


    private void Update()
    {
        for (int i = 0, cnt = _staffs.Length; i < cnt; ++i)
        {
            if (_staffs[i] == null)
                continue;

            _staffs[i].StaffAction();
            _staffs[i].UsingStaffSkill(_tableManager, _kitchenSystem, _customerController);
            _staffs[i].UpdateStaffSkill(_tableManager, _kitchenSystem, _customerController);
        }
    }
}
