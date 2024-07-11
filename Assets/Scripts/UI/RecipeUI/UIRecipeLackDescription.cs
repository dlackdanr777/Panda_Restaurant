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
        _cookTimeDescription.text = Utility.ConvertToNumber(data.GetCookingTime(1));
        _sellPriceDescription.text = Utility.ConvertToNumber(data.GetSellPrice(1));

        _needItemDescription.text = string.Empty;
        _upgradeMinScoreDescription.text = Utility.ConvertToNumber(data.BuyScore);

        _buyButton.RemoveAllListeners();
        _buyButton.AddListener(() => onBuyButtonClicked(data));
        _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
    }
}
