using UnityEngine;
using UnityEngine.UI;

public class UIStaffTab : UIRestaurantAdminTab
{
    [SerializeField] private UIStaff _uiStaff;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;


    public override void Init()
    {
        _slots = new UITabSlot[(int)EquipStaffType.Length];
        for(int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            BasicData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, (EquipStaffType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.StaffTypeStringConverter((EquipStaffType)index));
            slot.name = "StaffTabSlot" + (i + 1);
        }

        UserInfo.OnChangeStaffHandler += UpdateUI;
    }


    public override void UpdateUI()
    {
        for (int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            UpdateUI(_floorType, (EquipStaffType)i);
        }
    }

    public override void SetAttention()
    {

    }

    public override void SetNotAttention()
    {

    }

    public void ShowUIStaff(EquipStaffType type)
    {
        _uiStaff.ShowUIStaff(_floorType, type);
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        UpdateUI();
    }


    private void UpdateUI(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.StaffTypeStringConverter(type));
    }

    private void OnSlotClicked(int index)
    {
        _uiStaff.ShowUIStaff(_floorType, (EquipStaffType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= UpdateUI;
    }
}
