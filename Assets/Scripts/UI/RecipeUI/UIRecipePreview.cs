using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipePreview : MonoBehaviour
{
    [SerializeField] private Image _foodImage;
    [SerializeField] private GameObject[] _hideObjs;
    [SerializeField] private UIRecipeOwnDescription _ownDescription;
    [SerializeField] private UIRecipeLackDescription _lackDescription;
    [SerializeField] private TextMeshProUGUI _foodNameText;

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
        _currentFoodData = data;

        if (data == null)
        {
            for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
            {
                _hideObjs[i].SetActive(false);
            }
            return;
        }

        for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
        {
            _hideObjs[i].SetActive(true);
        }

        _foodImage.sprite = data.Sprite;
        _foodNameText.text = data.Name;

        if (UserInfo.IsGiveRecipe(data.Id))
        {
            _ownDescription.gameObject.SetActive(true);
            _lackDescription.gameObject.SetActive(false);
            _ownDescription.SetFoodData(data, _onUpgradeButtonClicked);
        }
        else
        {
            _lackDescription.gameObject.SetActive(true);
            _ownDescription.gameObject.SetActive(false);
            _lackDescription.SetFoodData(data, _onBuyButtonClicked);
        }
    }


    private void RecipeUpdate()
    {
        SetFoodData(_currentFoodData);
    }

}
