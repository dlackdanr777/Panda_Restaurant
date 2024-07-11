using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UITabSlot : MonoBehaviour
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


    public void UpdateUI(Sprite sprite, string typeText)
    {
        if(sprite == null)
            _image.sprite = _defalutSprite;
        else
            _image.sprite = sprite;

        _typeText.text = typeText;
    }
}
