using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRecipeSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Button _button;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _enoughMoneyImage;
    [SerializeField] private UIImageAndText _lowReputationImage;

    private FoodData _foodData;
    private Action<FoodData> _onButtonClicked;

    public void Init(Action<FoodData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetOperate(FoodData data)
    {
        _operateImage.gameObject.SetActive(true);
        _lowReputationImage.gameObject.SetActive(false);
        _enoughMoneyImage.gameObject.SetActive(false);

        _foodData = data;
        _image.sprite = data.Sprite;
        _operateImage.SetText("º¸À¯Áß");
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetBuy(FoodData data)
    {
        _enoughMoneyImage.gameObject.SetActive(true);
        _operateImage.gameObject.SetActive(false);
        _lowReputationImage.gameObject.SetActive(false);


        _foodData = data;
        _image.sprite = data.Sprite;
        _enoughMoneyImage.SetText(Utility.ConvertToNumber(data.BuyPrice));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(FoodData data)
    {
        _lowReputationImage.gameObject.SetActive(true);
        _operateImage.gameObject.SetActive(false);
        _enoughMoneyImage.gameObject.SetActive(false);

        _foodData = data;
        _image.sprite = data.Sprite;
        _lowReputationImage.SetText(Utility.ConvertToNumber(data.BuyMinScore));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
