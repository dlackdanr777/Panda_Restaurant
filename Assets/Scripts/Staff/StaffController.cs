using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{
    [SerializeField] private List<Staff> _staffList;
    [SerializeField] private StaffData _staffData;

    [SerializeField] private TableManager _tableManager;

    public void Awake()
    {
        for(int i = 0, cnt = _staffList.Count; i < cnt; ++i)
        {
            _staffList[i].SetStaffData(_staffData);
            _staffList[i].SetAlpha(0);
        }
    }

    private void Update()
    {
        for (int i = 0, cnt = _staffList.Count; i < cnt; ++i)
        {
            if (_staffList[i].ActionEnabled && !_staffList[i].IsUsed)
                _tableManager.StaffAction(_staffList[i]);
        }
    }
}
