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
            slot.Init(() => _uiStaff.ShowUIStaff((StaffType)index));
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
}
