using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRecipeSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _enoughMoneyImage;

    private Action<FoodData> _onButtonClicked;

    public void Init(Action<FoodData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetOperate(FoodData data)
    {
        _operateImage.gameObject.SetActive(true);
        _alarmImage.SetActive(false);
        _enoughMoneyImage.gameObject.SetActive(false);

        _image.sprite = data.Sprite;
        _operateImage.SetText(data.Name);
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetBuy(FoodData data)
    {
        _enoughMoneyImage.gameObject.SetActive(true);
        _operateImage.gameObject.SetActive(false);
        _alarmImage.SetActive(true);

        _image.sprite = data.Sprite;
        _enoughMoneyImage.SetText(Utility.ConvertToNumber(data.BuyPrice));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(FoodData data)
    {
        _enoughMoneyImage.gameObject.SetActive(true);
        _operateImage.gameObject.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _enoughMoneyImage.SetText(Utility.ConvertToNumber(data.BuyPrice));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
