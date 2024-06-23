using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipePreview : MonoBehaviour
{
    [SerializeField] private Image _foodImage;
    [SerializeField] private TextMeshProUGUI _foodNameText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private UIButtonAndText _lowReputationButton;

    private Action<FoodData> _onBuyButtonClicked;
    private Action<FoodData> _onUpgradeButtonClicked;
    private FoodData _currentFoodData;

    public void Init(Action<FoodData> onBuyButonClicked, Action<FoodData> onUpgradeButtonClicked)
    {
        _onBuyButtonClicked = onBuyButonClicked;
        _onUpgradeButtonClicked = onUpgradeButtonClicked;
        UserInfo.OnUpgradeRecipeHandler += UpgradeRecipeEvent;
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
            _lowReputationButton.gameObject.SetActive(false);
            return;
        }

        _foodImage.gameObject.SetActive(true);
        _foodNameText.gameObject.SetActive(true);
        _foodImage.sprite = data.Sprite;
        _description.gameObject.SetActive(true);
        _description.text = data.Description;
        _foodNameText.text = data.Name;


        if (UserInfo.IsGiveRecipe(data.Id))
        {
            _upgradeButton.gameObject.SetActive(true);
            _lowReputationButton.gameObject.SetActive(false);
            _buyButton.gameObject.SetActive(false);

            int recipeLevel = UserInfo.GetRecipeLevel(data.Id);

            if (data.UpgradeEnable(recipeLevel))
            {

                _upgradeButton.Interactable(true);
                _upgradeButton.SetText(recipeLevel + 1 + "단계 강화");
                _upgradeButton.AddListener(() => _onUpgradeButtonClicked(data));
            }
            else
            {
                _upgradeButton.Interactable(false);
                _upgradeButton.SetText("최대 강화");
            }


        }
        else
        {
            if(GameManager.Instance.Score < data.BuyMinScore)
            {
                _lowReputationButton.gameObject.SetActive(true);
                _buyButton.gameObject.SetActive(false);
                _upgradeButton.gameObject.SetActive(false);
                _lowReputationButton.SetText(Utility.ConvertToNumber(data.BuyMinScore));
            }
            else
            {
                _buyButton.gameObject.SetActive(true);
                _upgradeButton.gameObject.SetActive(false);
                _lowReputationButton.gameObject.SetActive(false);

                _buyButton.AddListener(() => _onBuyButtonClicked(data));
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
            }
        }
    }


    private void UpgradeRecipeEvent()
    {
        SetFoodData(_currentFoodData);
    }

}
