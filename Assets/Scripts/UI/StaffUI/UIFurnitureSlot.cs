using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIFurnitureSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _useImage;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;

    private Action<FurnitureData> _onButtonClicked;

    public void Init(Action<FurnitureData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }


    public void SetUse(FurnitureData data)
    {
        _useImage.SetActive(true);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "»ç¿ëÁß";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetOperate(FurnitureData data)
    {
        _operateImage.SetActive(true);
        _useImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = data.Name;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetEnoughMoney(FurnitureData data)
    {
        _enoughMoneyImage.SetActive(true);
        _alarmImage.SetActive(true);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = Utility.ConvertToNumber(data.BuyMinPrice);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(FurnitureData data)
    {
        _enoughMoneyImage.SetActive(true);
        _alarmImage.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = Utility.ConvertToNumber(data.BuyMinPrice);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
