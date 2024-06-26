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
    [SerializeField] private UIRecipeSlot _slotPrefab;

    private UIRecipeSlot[] _slots;
    private List<FoodData> _foodDataList;

    public void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetShopFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnUpgradeButtonClicked);
        _uiRecipePreview.SetFoodData(null);

        int foodCount = FoodDataManager.Count;

        _slots = new UIRecipeSlot[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UIRecipeSlot slot = Instantiate(_slotPrefab, _slotParnet);
            slot.Init(OnSlotClicked);
            _slots[i] = slot;
        }

        UpdateSlot();

        UserInfo.OnUpgradeRecipeHandler += UpdateSlot;
        UserInfo.OnGiveRecipeHandler += UpdateSlot;
        UserInfo.OnChangeMoneyHandler += UpdateSlot;
        UserInfo.OnChangeScoreHanlder += UpdateSlot;
        GameManager.OnAppendAddScoreHandler += UpdateSlot;
    }


    private void UpdateSlot()
    {
        FoodData data;
        for(int i = 0, cnt = _foodDataList.Count; i < cnt; i++)
        {
            data = _foodDataList[i];

            if(UserInfo.IsGiveRecipe(data.Id))
            {
                _slots[i].SetOperate(data);
                continue;
            }

            if(UserInfo.Score < data.BuyMinScore)
            {
                _slots[i].SetLowReputation(data);
                continue;
            }

            _slots[i].SetBuy(data);
        }
    }


    private void OnBuyButtonClicked(FoodData data)
    {
        if (UserInfo.IsGiveRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if(UserInfo.Score < data.BuyMinScore)
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
    }
}
