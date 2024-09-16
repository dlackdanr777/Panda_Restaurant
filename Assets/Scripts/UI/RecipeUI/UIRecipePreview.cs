using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipePreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIRecipeSelectSlot _selectGroup;
    [SerializeField] private UIImageAndText _levelGroup;
    [SerializeField] private UIImageAndText _priceGroup;
    [SerializeField] private UIImageAndText _cookSpeedGroup;
    [SerializeField] private UIImageAndText _totalSellGroup;
    [SerializeField] private UITextAndText _descriptionGroup;
    [SerializeField] private GameObject _priceTextGroup;
    [SerializeField] private GameObject _cookSpeedTextGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<FoodData> _onBuyButtonClicked;
    private Action<FoodData> _onUpgradeButtonClicked;
    private FoodData _currentData;

    public void Init(Action<FoodData> onBuyButonClicked, Action<FoodData> onUpgradeButtonClicked)
    {
        _onBuyButtonClicked = onBuyButonClicked;
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);
        UserInfo.OnUpgradeRecipeHandler += RecipeUpdate;
        UserInfo.OnGiveRecipeHandler += RecipeUpdate;
        UserInfo.OnChangeMoneyHandler += RecipeUpdate;
        UserInfo.OnChangeScoreHandler += RecipeUpdate;
        UserInfo.OnAddCookCountHandler += RecipeUpdate;
        GameManager.Instance.OnAppendAddScoreHandler += RecipeUpdate;
    }


    public void SetData(FoodData data)
    {
        _currentData = data;
        _selectGroup.SetData(data);
        _buyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _levelGroup.gameObject.SetActive(false);
        _totalSellGroup.gameObject.SetActive(false);

        if (data == null)
        {
            _priceGroup.gameObject.SetActive(false);
            _cookSpeedGroup.gameObject.SetActive(false);
            _descriptionGroup.gameObject.SetActive(false);
            _priceTextGroup.gameObject.SetActive(false);
            _cookSpeedTextGroup.gameObject.SetActive(false);
            _descriptionGroup.SetText1(string.Empty);
            _descriptionGroup.SetText2(string.Empty);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _priceGroup.gameObject.SetActive(true);
            _cookSpeedGroup.gameObject.SetActive(true);
            _descriptionGroup.gameObject.SetActive(true);
            _priceTextGroup.gameObject.SetActive(true);
            _cookSpeedTextGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        int level = UserInfo.IsGiveRecipe(data) ? UserInfo.GetRecipeLevel(data) : 1;

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _priceGroup.SetText(Utility.ConvertToMoney(data.GetSellPrice(level)));
        _cookSpeedGroup.SetText(data.GetCookingTime(level) + "s");
        _descriptionGroup.SetText1("Ό³Έν");
        _descriptionGroup.SetText2(data.Description);

        if (UserInfo.IsGiveRecipe(data))
        {
            _levelGroup.gameObject.SetActive(true);
            _totalSellGroup.gameObject.SetActive(true);
            _totalSellGroup.SetText(UserInfo.GetCookCount(data).ToString());
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
            _levelGroup.SetText("LV." + level + " / " + data.MaxLevel);
        }
        else
        {
            if (!UserInfo.IsScoreValid(data))
            {
                _selectGroup.ImageColor = Utility.GetColor(ColorType.None);
                _selectGroup.SetSprite(_questionMarkSprite);
                _scoreButton.gameObject.SetActive(true);
                _scoreButton.SetText(data.BuyScore.ToString());
                _priceGroup.SetText("???");
                _cookSpeedGroup.SetText("???");
                return;
            }

            _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
            if (!UserInfo.IsMoneyValid(data))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughMoneyButton.RemoveAllListeners();
                _notEnoughMoneyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(data.BuyPrice));
                return;
            }

            _buyButton.gameObject.SetActive(true);
            _buyButton.RemoveAllListeners();
            _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
            _buyButton.SetText(Utility.ConvertToMoney(data.BuyPrice));

        }

    }


    private void RecipeUpdate()
    {
        if (!gameObject.activeSelf)
            return;

        SetData(_currentData);
    }

}
