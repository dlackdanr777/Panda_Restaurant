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
                _typeText.text = "테이블1";
                break;

            case FurnitureType.Table2:
                _typeText.text = "테이블2";
                break;

            case FurnitureType.Table3:
                _typeText.text = "테이블3";
                break;

            case FurnitureType.Table4:
                _typeText.text = "테이블4";
                break;

            case FurnitureType.Table5:
                _typeText.text = "테이블5";
                break;

            case FurnitureType.Counter:
                _typeText.text = "카운터";
                break;

            case FurnitureType.Rack:
                _typeText.text = "선반";
                break;

            case FurnitureType.Frame:
                _typeText.text = "액자";
                break;

            case FurnitureType.Flower:
                _typeText.text = "화분";
                break;

            case FurnitureType.Acc:
                _typeText.text = "조명";
                break;

            case FurnitureType.Wallpaper:
                _typeText.text = "벽지";
                break;

            default:
                _typeText.text = string.Empty;
                break;
        }
    }
}
