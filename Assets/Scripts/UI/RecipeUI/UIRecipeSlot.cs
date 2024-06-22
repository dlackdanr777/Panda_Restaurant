using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRecipeSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;
    [SerializeField] private GameObject _lowReputationImage;

    private FoodData _foodData;
    private Action<FoodData> _onButtonClicked;

    public void Init(Action<FoodData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetOperate(FoodData data)
    {
        _operateImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _foodData = data;
        _image.sprite = data.Sprite;
        _text.text = "º¸À¯Áß";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetBuy(FoodData data)
    {
        _enoughMoneyImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _operateImage.SetActive(false);

        _foodData = data;
        _image.sprite = data.Sprite;
        _text.text = Utility.ConvertToNumber(data.BuyPrice);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(FoodData data)
    {
        _lowReputationImage.SetActive(true);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _foodData = data;
        _image.sprite = data.Sprite;
        _text.text = Utility.ConvertToNumber(data.BuyMinScore);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
