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


    public void Init(UnityAction onButtonClicked)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(onButtonClicked);
    }


    public void SetData(StaffType type)
    {
        StaffData data = UserInfo.GetEquipStaff(type);

        if(data == null)
            _staffImage.sprite = _defalutSprite;
        else
            _staffImage.sprite = data.Sprite;

        switch (type)
        {
            case StaffType.Manager:
                _typeText.text = "�Ŵ���";
                break;

            case StaffType.Waiter:
                _typeText.text = "������";
                break;

            case StaffType.Chef:
                _typeText.text = "����";
                break;

            case StaffType.Cleaner:
                _typeText.text = "û�Һ�";
                break;

            case StaffType.Marketer:
                _typeText.text = "������";
                break;

            case StaffType.Guard:
                _typeText.text = "����";
                break;

            case StaffType.Server:
                _typeText.text = "����";
                break;

            default:
                _typeText.text = string.Empty;
                break;
        }
    }
}
