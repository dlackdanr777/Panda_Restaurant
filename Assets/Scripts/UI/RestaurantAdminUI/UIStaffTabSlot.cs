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
                _typeText.text = "매니저";
                break;

            case StaffType.Waiter:
                _typeText.text = "웨이터";
                break;

            case StaffType.Chef:
                _typeText.text = "셰프";
                break;

            case StaffType.Cleaner:
                _typeText.text = "청소부";
                break;

            case StaffType.Marketer:
                _typeText.text = "마케터";
                break;

            case StaffType.Guard:
                _typeText.text = "가드";
                break;

            case StaffType.Server:
                _typeText.text = "서버";
                break;

            default:
                _typeText.text = string.Empty;
                break;
        }
    }
}
