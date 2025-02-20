using UnityEngine;
using UnityEngine.UI;

public class UIStaffTab : MonoBehaviour
{
    [SerializeField] private UIStaff _uiStaff;
    [SerializeField] private Button _floor1Button;
    [SerializeField] private Button _floor2Button;
    [SerializeField] private Button _floor3Button;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;


    public void Init()
    {
        _slots = new UITabSlot[(int)StaffType.Length];
        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            BasicData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, (StaffType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.StaffTypeStringConverter((StaffType)index));
            slot.name = "StaffTabSlot" + (i + 1);
        }

        _floor1Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor1));
        _floor2Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor2));
        _floor3Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor3));

        UserInfo.OnChangeStaffHandler += UpdateUI;
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        UpdateUI();
    }

    public void UpdateUI()
    {
        for (int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            UpdateUI(_floorType, (StaffType)i);
        }
    }

    private void UpdateUI(ERestaurantFloorType floorType, StaffType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.StaffTypeStringConverter(type));
    }

    private void OnSlotClicked(int index)
    {
        _uiStaff.ShowUIStaff(_floorType, (StaffType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= UpdateUI;
    }
}
