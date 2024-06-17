using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffTab : MonoBehaviour
{
    [SerializeField] private UIStaff _uiStaff;

    [Header("Slots")]
    [SerializeField] private UIStaffTabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UIStaffTabSlot[] _slots;

    public void Init()
    {
        _slots = new UIStaffTabSlot[(int)StaffType.Length];
        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; i++)
        {
            int index = i;
            UIStaffTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(StaffType.Manager + index));
            slot.SetData(StaffType.Manager + index);
        }

        UserInfo.OnChangeStaffHandler += SlotUpdate;
    }


    public void SlotUpdate()
    {
        for(int i = 0, cnt = _slots.Length; i < cnt; ++i)
        {
            _slots[i].SetData(StaffType.Manager + i);
        }
    }


    private void OnSlotClicked(StaffType type)
    {
        switch (type)
        {
            case StaffType.Manager:
                _uiStaff.ShowUIStaffManager();
                break;

            case StaffType.Waiter:
                _uiStaff.ShowUIStaffWaiter();
                break;

            case StaffType.Chef:
                _uiStaff.ShowUIStaffChef();
                break;

            case StaffType.Cleaner:
                _uiStaff.ShowUIStaffCleaner();
                break;

            case StaffType.Marketer:
                _uiStaff.ShowUIStaffMarketer();
                break;

            case StaffType.Guard:
                _uiStaff.ShowUIStaffGuard();
                break;

            case StaffType.Server:
                _uiStaff.ShowUIStaffServer();
                break;
        }
    }

}
