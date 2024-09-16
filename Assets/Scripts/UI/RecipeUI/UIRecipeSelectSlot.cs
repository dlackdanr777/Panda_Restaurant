using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipeSelectSlot : MonoBehaviour
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

    private FoodData _currentData;
    private Action<FoodData> _onButtonClicked;

    public void OnButtonClicked(Action<FoodData> action)
    {
        _onButtonClicked = action;
    }

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetText(string text)
    {
        _nameText.text = text;
    }


    public void SetData(FoodData data)
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

        _button.onClick.RemoveAllListeners();
        if (UserInfo.IsGiveRecipe(data))
        {
            int level = UserInfo.GetRecipeLevel(data);
            if (data.UpgradeEnable(level))
            {
                _upgradeImage.gameObject.SetActive(true);
                _button.interactable = true;
                _buttonpressEffect.Interactable = true;
                _button.onClick.AddListener(() => _onButtonClicked(_currentData));
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
