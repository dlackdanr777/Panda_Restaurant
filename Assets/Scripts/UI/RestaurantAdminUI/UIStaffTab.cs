using UnityEngine;

public class UIStaffTab : MonoBehaviour
{
    [SerializeField] private UIStaff _uiStaff;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;

    public void Init()
    {


        _slots = new UITabSlot[(int)StaffType.Length];
        for(int i = 0, cnt = (int)StaffType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => _uiStaff.ShowUIStaff((StaffType)index));
            BasicData data = UserInfo.GetEquipStaff((StaffType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.StaffTypeStringConverter((StaffType)index));
            slot.name = "StaffTabSlot" + (i + 1);
        }

        UserInfo.OnChangeStaffHandler += UpdateUI;
    }

    public void UpdateUI()
    {
        for (int i = 0, cnt = (int)StaffType.Length; i < cnt; ++i)
        {
            UpdateUI((StaffType)i);
        }
    }

    private void UpdateUI(StaffType type)
    {
        BasicData data = UserInfo.GetEquipStaff(type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.StaffTypeStringConverter(type));
    }


}
