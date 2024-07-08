using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIFurnitureTabSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _furnitureImage;
    [SerializeField] private TextMeshProUGUI _typeText;

    [Space]
    [Header("Options")]
    [SerializeField] private Sprite _defalutSprite;


    public void Init(UnityAction onButtonClicked)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(onButtonClicked);
    }


    public void SetData(FurnitureType type)
    {
        FurnitureData data = UserInfo.GetEquipFurniture(type);

        if(data == null)
            _furnitureImage.sprite = _defalutSprite;
        else
            _furnitureImage.sprite = data.ThumbnailSPrite;

        switch (type)
        {
            case FurnitureType.Table1:
                _typeText.text = "���̺�1";
                break;

            case FurnitureType.Table2:
                _typeText.text = "���̺�2";
                break;

            case FurnitureType.Table3:
                _typeText.text = "���̺�3";
                break;

            case FurnitureType.Table4:
                _typeText.text = "���̺�4";
                break;

            case FurnitureType.Table5:
                _typeText.text = "���̺�5";
                break;

            case FurnitureType.Counter:
                _typeText.text = "ī����";
                break;

            case FurnitureType.Rack:
                _typeText.text = "����";
                break;

            case FurnitureType.Frame:
                _typeText.text = "����";
                break;

            case FurnitureType.Flower:
                _typeText.text = "ȭ��";
                break;

            case FurnitureType.Acc:
                _typeText.text = "����";
                break;

            case FurnitureType.Wallpaper:
                _typeText.text = "����";
                break;

            default:
                _typeText.text = string.Empty;
                break;
        }
    }
}
