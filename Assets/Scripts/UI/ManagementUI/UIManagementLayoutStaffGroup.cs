using System.Collections.Generic;
using UnityEngine;

public class UIManagementLayoutStaffGroup : UIManagementLayoutGroup
{
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIManagementLayoutStaffSlot _staffSlotPrefab;
    [SerializeField] private UIManagementStaffPreview _staffPreview;

    private List<UIManagementLayoutStaffSlot> _staffSlots = new List<UIManagementLayoutStaffSlot>();

    public override void EnableLayout(ERestaurantFloorType floor)
    {
        if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, floor))
        {
            DebugLog.LogError($"Invalid floor: {floor}");
            return;
        }

        for (int i = 0; i < _staffSlots.Count; i++)
        {
            _staffSlots[i].SetData(UserInfo.GetEquipStaff(UserInfo.CurrentStage, floor, (EquipStaffType)i), (EquipStaffType)i);
        }

        _staffPreview.Hide();
    }

    public override void Init()
    {
        for (int i = _staffSlots.Count - 1; i >= 0; i--)
            Destroy(_staffSlots[i].gameObject);
        _staffSlots.Clear();

        for (int i = 0; i < (int)EquipStaffType.Length; i++)
        {
            UIManagementLayoutStaffSlot slot = Instantiate(_staffSlotPrefab, _slotParent);
            slot.Init(OnClickStaffSlot);
            _staffSlots.Add(slot);
        }

        _staffPreview.Init();
    }


    private void OnClickStaffSlot(StaffData data)
    {
        _staffPreview.Show(data);
    }

}
