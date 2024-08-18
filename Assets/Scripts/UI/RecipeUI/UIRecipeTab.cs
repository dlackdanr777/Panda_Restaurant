using System.Collections.Generic;
using UnityEngine;

public class UIRecipeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIRecipePreview _uiRecipePreview;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UISlot _slotPrefab;

    private UISlot[] _slots;
    private List<FoodData> _foodDataList;

    public void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetShopFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnUpgradeButtonClicked);
        _uiRecipePreview.SetFoodData(null);

        int foodCount = FoodDataManager.Count;

        _slots = new UISlot[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UISlot slot = Instantiate(_slotPrefab, _slotParnet);

            int index = i;
            FoodData data = _foodDataList[index];
            slot.Init(() => OnSlotClicked(data));
            slot.SetOutline(false);
            _slots[i] = slot;
        }

        UpdateUI();

        UserInfo.OnUpgradeRecipeHandler += UpdateUI;
        UserInfo.OnGiveRecipeHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnAppendAddScoreHandler += UpdateUI;
    }


    public void UpdateUI()
    {
        if (_foodDataList == null || _foodDataList.Count == 0)
            return;

        FoodData data;
        for(int i = 0, cnt = _foodDataList.Count; i < cnt; i++)
        {
            data = _foodDataList[i];

            if(UserInfo.IsGiveRecipe(data.Id))
            {
                _slots[i].SetOperate(data.ThumbnailSprite, data.Name, string.Empty);
                continue;
            }

            if(data.BuyScore <= UserInfo.Score && data.BuyPrice <= UserInfo.Money)
            {
                _slots[i].SetEnoughMoney(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyPrice));
                continue;
            }
            _slots[i].SetLowReputation(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyScore));
            continue;
        }
    }


    private void OnBuyButtonClicked(FoodData data)
    {
        if (UserInfo.IsGiveRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if(UserInfo.Score < data.BuyScore)
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if(UserInfo.Money < data.BuyPrice)
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AppendMoney(-data.BuyPrice);
        UserInfo.GiveRecipe(data);
        TimedDisplayManager.Instance.ShowText("새로운 레시피를 배웠어요!");
    }


    private void OnUpgradeButtonClicked(FoodData data)
    {
        if(!UserInfo.IsGiveRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        int recipeLevel = UserInfo.GetRecipeLevel(data.Id);
        if(UserInfo.Score < data.GetUpgradeMinScore(recipeLevel))
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if(UserInfo.Money < data.GetUpgradePrice(recipeLevel))
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        if(UserInfo.UpgradeRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowText("강화 성공!");
            return;
        }
    }


    private void OnSlotClicked(FoodData data)
    {
        _uiRecipePreview.SetFoodData(data);

        for(int i = 0, cnt = _slots.Length; i < cnt; i++)
        {
            bool outlineEnabled = _foodDataList[i] == data ? true : false;
            _slots[i].SetOutline(outlineEnabled);
        }
    }
}
