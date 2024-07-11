using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRecipeSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Outline _outLine;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _lockImage;
    [SerializeField] private UIImageAndText _buyMinScoreImage;
    [SerializeField] private UIImageAndText _buyMinMoneyImage;

    private Action<FoodData> _onButtonClicked;

    public void Init(Action<FoodData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetOperate(FoodData data)
    {
        _buyMinScoreImage.gameObject.SetActive(false);
        _lockImage.gameObject.SetActive(false);
        _alarmImage.SetActive(false);
        _buyMinMoneyImage.gameObject.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetBuy(FoodData data)
    {
        _buyMinMoneyImage.gameObject.SetActive(true);
        _alarmImage.SetActive(true);
        _buyMinScoreImage.gameObject.SetActive(false);
        _lockImage.gameObject.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _buyMinMoneyImage.SetText(Utility.ConvertToNumber(data.BuyPrice));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(FoodData data)
    {
        _buyMinScoreImage.gameObject.SetActive(true);
        _lockImage.gameObject.SetActive(true);
        _buyMinMoneyImage.gameObject.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _buyMinScoreImage.SetText(Utility.ConvertToNumber(data.BuyScore));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetOutline(bool value)
    {
        _outLine.enabled = value;
    }
}
