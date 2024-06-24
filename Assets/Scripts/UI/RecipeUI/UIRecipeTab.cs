using Muks.DataBind;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UIRecipeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIRecipePreview _uiRecipePreview;
    [SerializeField] private UIRecipeUpgrade _uiRecipeUpgrade;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRecipeSlot _slotPrefab;

    private UIRecipeSlot[] _slots;
    private List<FoodData> _foodDataList;

    public void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetShopFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnShowRecipeUpgrade);
        _uiRecipePreview.SetFoodData(null);
        _uiRecipeUpgrade.SetAction(OnUpgradeButtonClicked);

        int foodCount = FoodDataManager.Count;

        _slots = new UIRecipeSlot[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UIRecipeSlot slot = Instantiate(_slotPrefab, _slotParnet);
            slot.Init(OnSlotClicked);
            _slots[i] = slot;
        }

        UpdateSlot();
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

            if(data.BuyMinScore < GameManager.Instance.Score)
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
            return;

        UserInfo.GiveRecipe(data);
        //TODO: 돈 확인 후 스태프 획득으로 변경해야함
    }


    private void OnUpgradeButtonClicked(FoodData data)
    {
        if(!UserInfo.IsGiveRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowText("레시피를 가지고 있지 않습니다.");
            return;
        }

        int recipeLevel = UserInfo.GetRecipeLevel(data.Id);
        if(data.GetUpgradeMinScore(recipeLevel) < GameManager.Instance.Score)
        {
            TimedDisplayManager.Instance.ShowText("평점이 부족합니다.");
            return;
        }

        if(data.GetUpgradePrice(recipeLevel) < GameManager.Instance.Tip)
        {
            TimedDisplayManager.Instance.ShowText("소지금이 부족합니다.");
            return;
        }

        if(UserInfo.UpgradeRecipe(data.Id))
        {
            TimedDisplayManager.Instance.ShowText("강화 성공");
            return;
        }
    }


    private void OnSlotClicked(FoodData data)
    {
        _uiRecipePreview.SetFoodData(data);
    }

    private void OnShowRecipeUpgrade(FoodData data)
    {
        DataBind.GetUnityActionValue("ShowRecipeUpgradeUI")?.Invoke();
        _uiRecipeUpgrade.SetFoodData(data);
    }
}
