using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffSelectSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private ButtonPressEffect _buttonpressEffect;


    [Space]
    [Header("Images")]
    [SerializeField] private Image _upgradeImage;

    public Color ImageColor
    {
        get { return _image.color; }
        set { _image.color = value; }
    }

    private StaffData _currentData;
    private Action<StaffData> _onButtonClicked;

    public void Init()
    {
        _button.onClick.AddListener(OnUpgradeButtonClicked);
    }

    public void OnButtonClicked(Action<StaffData> action)
    {
        _onButtonClicked = action;
    }

    private void OnUpgradeButtonClicked()
    {
        _onButtonClicked?.Invoke(_currentData);
    }

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetText(string text)
    {
        _nameText.text = text;
    }


    public void SetData(StaffData data)
    {
        _currentData = data;
        if (data == null)
        {
            _image.gameObject.SetActive(false);
            _nameText.gameObject.SetActive(false);
            _upgradeImage.gameObject.SetActive(false);
            _button.interactable = false;
            _buttonpressEffect.Interactable = false;
            return;
        }

        _image.gameObject.SetActive(true);
        _nameText.gameObject.SetActive(true);

        if (UserInfo.IsGiveStaff(data))
        {
            int level = UserInfo.GetStaffLevel(data);
            if (data.UpgradeEnable(level))
            {
                _upgradeImage.gameObject.SetActive(true);
                _button.interactable = true;
                _buttonpressEffect.Interactable = true;
                return;
            }

            _button.interactable = false;
            _buttonpressEffect.Interactable = false;
            _upgradeImage.gameObject.SetActive(false);
            return;
        }

        else
        {
            _button.interactable = false;
            _buttonpressEffect.Interactable = false;
            _upgradeImage.gameObject.SetActive(false);
            return;
        }

    }
}
