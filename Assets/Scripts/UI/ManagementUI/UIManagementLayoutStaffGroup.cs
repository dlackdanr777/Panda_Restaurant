using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

public class UIManagementLayoutStaffGroup : UIManagementLayoutGroup
{
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIManagementLayoutStaffSlot _staffSlotPrefab;


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
            _staffSlots[i].SetData(UserInfo.GetEquipStaff(UserInfo.CurrentStage ,floor, (EquipStaffType)i), (EquipStaffType)i);
        }
    }

    public override void Init()
    {
        _staffSlots.Clear();
        for (int i = 0; i < (int)EquipStaffType.Length; i++)
        {
            UIManagementLayoutStaffSlot slot = Instantiate(_staffSlotPrefab, _slotParent);
            _staffSlots.Add(slot);
        }
    }

}
