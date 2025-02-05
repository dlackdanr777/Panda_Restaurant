using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIStaffTabSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _typeText;

    [Space]
    [Header("Options")]
    [SerializeField] private Sprite _defalutSprite;

    private ERestaurantFloorType _floorType;
    private StaffData _staffData;

    public void Init(UnityAction onButtonClicked)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(onButtonClicked);
    }


    public void SetData(ERestaurantFloorType floorType, StaffType type)
    {
        _staffData = UserInfo.GetEquipStaff(floorType, type);
        _floorType = floorType;

        if(_staffData == null)
            _staffImage.sprite = _defalutSprite;
        else
            _staffImage.sprite = _staffData.Sprite;

        string typeStr = Utility.StaffTypeStringConverter(type);
        _typeText.text = typeStr;
    }
}
