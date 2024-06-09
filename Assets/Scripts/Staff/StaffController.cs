using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{
    [SerializeField] private List<Staff> _staffList;

    [SerializeField] private CustomerController _customerController;
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private KitchenSystem _kitchenSystem;

    public void Awake()
    {
        _staffList[0].SetStaffData(StaffDataManager.Instance.GetStaffData("Staff01"), _tableManager, _kitchenSystem, _customerController);

        _staffList[1].SetStaffData(StaffDataManager.Instance.GetStaffData("Staff02"), _tableManager, _kitchenSystem, _customerController);

        _staffList[2].SetStaffData(StaffDataManager.Instance.GetStaffData("Staff03"), _tableManager, _kitchenSystem, _customerController);

        _staffList[3].SetStaffData(StaffDataManager.Instance.GetStaffData("Staff04"), _tableManager, _kitchenSystem, _customerController);

    }

    private void Update()
    {
        for (int i = 0, cnt = _staffList.Count; i < cnt; ++i)
        {
            _staffList[i].StaffAction();
        }
    }
}
