using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipePreview : MonoBehaviour
{
    [SerializeField] private Image _foodImage;
    [SerializeField] private GameObject _descriptions;
    [SerializeField] private TextMeshProUGUI _foodNameText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _cookTimeDescription;
    [SerializeField] private TextMeshProUGUI _priceDescription;
    [SerializeField] private TextMeshProUGUI _scoreDescription;

    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _upgradeButton;

    private Action<FoodData> _onBuyButtonClicked;
    private Action<FoodData> _onUpgradeButtonClicked;
    private FoodData _currentFoodData;

    public void Init(Action<FoodData> onBuyButonClicked, Action<FoodData> onUpgradeButtonClicked)
    {
        _onBuyButtonClicked = onBuyButonClicked;
        _onUpgradeButtonClicked = onUpgradeButtonClicked;
        UserInfo.OnUpgradeRecipeHandler += RecipeUpdate;
        UserInfo.OnGiveRecipeHandler += RecipeUpdate;
        UserInfo.OnChangeMoneyHandler += RecipeUpdate;
        UserInfo.OnChangeScoreHanlder += RecipeUpdate;
        GameManager.OnAppendAddScoreHandler += RecipeUpdate;
    }


    public void SetFoodData(FoodData data)
    {
        _buyButton.RemoveAllListeners();
        _upgradeButton.RemoveAllListeners();
        _currentFoodData = data;
        if (data == null)
        {
            _foodImage.gameObject.SetActive(false);
            _foodNameText.gameObject.SetActive(false);
            _description.gameObject.SetActive(false);
            _buyButton.gameObject.SetActive(false);
            _upgradeButton.gameObject.SetActive(false);
            _descriptions.SetActive(false);
            return;
        }

        _foodImage.gameObject.SetActive(true);
        _foodNameText.gameObject.SetActive(true);
        _description.gameObject.SetActive(true);
        _descriptions.SetActive(true);
        _foodImage.sprite = data.Sprite;
        _description.text = data.Description;
        _foodNameText.text = data.Name;

        if (UserInfo.IsGiveRecipe(data.Id))
        {
            _upgradeButton.gameObject.SetActive(true);
            _buyButton.gameObject.SetActive(false);
            _upgradeButton.AddListener(() => _onUpgradeButtonClicked(data));

            int level = UserInfo.GetRecipeLevel(data);

            _cookTimeDescription.text = data.GetCookingTime(level).ToString() + 's';

            if (data.UpgradeEnable(level))
            {
                int upgradeMoney = data.GetUpgradePrice(level);
                _priceDescription.text = Utility.ConvertToNumber(data.GetSellPrice(level));
                _scoreDescription.text = Utility.ConvertToNumber(data.GetUpgradeMinScore(level));

                _upgradeButton.SetText(Utility.ConvertToNumber(upgradeMoney));
                _upgradeButton.Interactable(true);
            }
            else
            {
                _upgradeButton.SetText("최대 강화");
                _priceDescription.text = "최대 강화";
                _scoreDescription.text = "최대 강화";
                _upgradeButton.Interactable(false);
            }
        }
        else
        {
            _buyButton.gameObject.SetActive(true);
            _upgradeButton.gameObject.SetActive(false);

            _cookTimeDescription.text = data.GetCookingTime(1).ToString() + 's';
            _priceDescription.text = Utility.ConvertToNumber(data.GetSellPrice(1));
            _scoreDescription.text = Utility.ConvertToNumber(data.BuyMinScore);

            _upgradeButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
            _buyButton.AddListener(() => _onBuyButtonClicked(data));
            _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
        }
    }


    private void RecipeUpdate()
    {
        SetFoodData(_currentFoodData);
    }

}
