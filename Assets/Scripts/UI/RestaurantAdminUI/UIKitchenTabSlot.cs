using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIKitchenTabSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _typeText;

    [Space]
    [Header("Options")]
    [SerializeField] private Sprite _defalutSprite;


    public void Init(UnityAction onButtonClicked)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(onButtonClicked);
    }


    public void SetData(KitchenUtensilType type)
    {
        KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil(type);

        if(data == null)
            _image.sprite = _defalutSprite;
        else
            _image.sprite = data.ThumbnailSPrite;

        switch (type)
        {
            case KitchenUtensilType.Burner1:
                _typeText.text = "������1";
                break;

            case KitchenUtensilType.Burner2:
                _typeText.text = "������2";
                break;

            case KitchenUtensilType.Burner3:
                _typeText.text = "������3";
                break;

            case KitchenUtensilType.Burner4:
                _typeText.text = "������4";
                break;

            case KitchenUtensilType.Burner5:
                _typeText.text = "������5";
                break;

            case KitchenUtensilType.Refrigerator:
                _typeText.text = "�����";
                break;

            case KitchenUtensilType.Cabinet:
                _typeText.text = "�����";
                break;

            case KitchenUtensilType.Window:
                _typeText.text = "â��";
                break;

            case KitchenUtensilType.Shelf:
                _typeText.text = "����";
                break;

            case KitchenUtensilType.Sink:
                _typeText.text = "��ũ��";
                break;

            case KitchenUtensilType.CookingTools:
                _typeText.text = "��������";
                break;

            default:
                _typeText.text = string.Empty;
                break;
        }
    }
}
