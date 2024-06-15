using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{
    [SerializeField] private List<Staff> _staffList;

    [SerializeField] private CustomerController _customerController;
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private KitchenSystem _kitchenSystem;

    private float[] _staffActionTimers;
    private float[] _staffSkillTimers;

    public void Awake()
    {
        StaffData data1 = StaffDataManager.Instance.GetStaffData("Staff01");
        StaffData data2 = StaffDataManager.Instance.GetStaffData("Staff02");
        StaffData data3 = StaffDataManager.Instance.GetStaffData("Staff03");
        StaffData data4 = StaffDataManager.Instance.GetStaffData("Staff04");
        StaffData data5 = StaffDataManager.Instance.GetStaffData("Staff05");
        _staffList[0].SetStaffData(data1, _tableManager, _kitchenSystem, _customerController);
        _staffList[1].SetStaffData(data2, _tableManager, _kitchenSystem, _customerController);
        _staffList[2].SetStaffData(data3, _tableManager, _kitchenSystem, _customerController);
        _staffList[3].SetStaffData(data4, _tableManager, _kitchenSystem, _customerController);
        _staffList[4].SetStaffData(data5, _tableManager, _kitchenSystem, _customerController);

        UserInfo.SetEquipStaff(data1);
        UserInfo.SetEquipStaff(data2);
        UserInfo.SetEquipStaff(data3);
        UserInfo.SetEquipStaff(data4);
        UserInfo.SetEquipStaff(data5);

    }

    private void Update()
    {
        for (int i = 0, cnt = _staffList.Count; i < cnt; ++i)
        {
            _staffList[i].StaffAction();
            _staffList[i].StaffSkill(_tableManager, _kitchenSystem, _customerController);
        }
    }
}
