using System;
using TMPro;
using UnityEngine;

public class UIRecipeLackDescription : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _sellPriceDescription;
    [SerializeField] private TextMeshProUGUI _cookTimeDescription;
    [SerializeField] private TextMeshProUGUI _needItemDescription;
    [SerializeField] private TextMeshProUGUI _upgradeMinScoreDescription;
    [SerializeField] private UIButtonAndText _buyButton;


    public void SetFoodData(FoodData data, Action<FoodData> onBuyButtonClicked)
    {
        _cookTimeDescription.text = Utility.ConvertToMoney(data.GetCookingTime(1));
        _sellPriceDescription.text = Utility.ConvertToMoney(data.GetSellPrice(1));

        _needItemDescription.text = string.Empty;
        _upgradeMinScoreDescription.text = Utility.ConvertToMoney(data.BuyScore);

        _buyButton.RemoveAllListeners();
        _buyButton.AddListener(() => onBuyButtonClicked(data));
        _buyButton.SetText(Utility.ConvertToMoney(data.BuyPrice));
    }
}
