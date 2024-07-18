using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIRecipeOwnDescription : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _needItemDescription;
    [SerializeField] private TextMeshProUGUI _upgradeMinScoreDescription;
    [SerializeField] private TextMeshProUGUI _sellPriceDescription1;
    [SerializeField] private TextMeshProUGUI _sellPriceDescription2;
    [SerializeField] private TextMeshProUGUI _upgradeSellPriceDescription;
    [SerializeField] private TextMeshProUGUI _cookTimeDescription1;
    [SerializeField] private TextMeshProUGUI _cookTimeDescription2;
    [SerializeField] private TextMeshProUGUI _upgradeCookTimeDescription;

    [SerializeField] private UIButtonAndText _upgradeButton;


    public void SetFoodData(FoodData data, Action<FoodData> onUpgradeButtonClicked)
    {
        int level = UserInfo.GetRecipeLevel(data);

        _cookTimeDescription1.text = data.GetCookingTime(level) + "s";
        _cookTimeDescription2.text = data.GetCookingTime(level) + "s";

        _sellPriceDescription1.text = Utility.ConvertToNumber(data.GetSellPrice(level));
        _sellPriceDescription2.text = Utility.ConvertToNumber(data.GetSellPrice(level));

        if (data.UpgradeEnable(level))
        {
            _upgradeCookTimeDescription.gameObject.SetActive(true);
            _upgradeSellPriceDescription.gameObject.SetActive(true);
            _upgradeButton.RemoveAllListeners();
            _upgradeButton.AddListener(() => onUpgradeButtonClicked?.Invoke(data));
            _upgradeButton.Interactable(true);
            _upgradeButton.SetText(Utility.ConvertToNumber(data.GetUpgradePrice(level)));

            _needItemDescription.text = string.Empty;
            _upgradeMinScoreDescription.text = Utility.ConvertToNumber(data.GetUpgradeMinScore(level));
            _upgradeSellPriceDescription.text = Utility.ConvertToNumber(data.GetSellPrice(level + 1));
            _upgradeCookTimeDescription.text = data.GetCookingTime(level + 1).ToString() + "s";
        }

        else
        {
            _upgradeCookTimeDescription.gameObject.SetActive(false);
            _upgradeSellPriceDescription.gameObject.SetActive(false);
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("최대 강화");

            _needItemDescription.text = string.Empty;
            _upgradeMinScoreDescription.text = "최대 강화";
        }
    }
}
