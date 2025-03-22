using Muks.MobileUI;
using System.Collections.Generic;
using UnityEngine;

public class UIRecipeTab : UIRestaurantAdminTab
{
    [Header("Components")]
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private UIRecipeUpgrade _uiUpgrade;
    [SerializeField] private UIRecipePreview _uiRecipePreview;


    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminFoodTypeSlot _slotPrefab;

    private UIRestaurantAdminFoodTypeSlot[] _slots;
    private List<FoodData> _foodDataList;

    public override void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnUpgradeButtonClicked);
        _uiRecipePreview.SetData(_foodDataList[0]);
        _uiUpgrade.SetData(_foodDataList[0]);

        int foodCount = _foodDataList.Count;

        _slots = new UIRestaurantAdminFoodTypeSlot[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);

            int index = i;
            FoodData data = _foodDataList[index];
            slot.Init(() => OnSlotClicked(data));
            _slots[i] = slot;
        }

        UpdateUI();

        UserInfo.OnUpgradeRecipeHandler += UpdateUI;
        UserInfo.OnGiveRecipeHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;
    }


    public void SetView(FoodData data)
    {
        _uiRecipePreview.SetData(data);
    }


    public override void UpdateUI()
    {
        if (!gameObject.activeSelf)
            return;

        if (_foodDataList == null || _foodDataList.Count == 0)
            return;

        _uiRecipePreview.UpdateUI();

        FoodData data;
        for(int i = 0, cnt = _foodDataList.Count; i < cnt; i++)
        {
            data = _foodDataList[i];
            _slots[i].SetFoodType(data.FoodType);
            if(UserInfo.IsGiveRecipe(data.Id))
            {
                _slots[i].SetNone(data.ThumbnailSprite, data.Name);
                continue;
            }

            if (!UserInfo.IsScoreValid(data))
            {
                _slots[i].SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                continue;
            }

            if(!string.IsNullOrWhiteSpace(data.NeedItem))
            {
                if(!UserInfo.IsGiveGachaItem(data.NeedItem))
                {
                    _slots[i].SetNeedItem(data.ThumbnailSprite, data.Name, data.NeedItem);
                    continue;
                }

                _slots[i].SetMiniGame(data.ThumbnailSprite, data.Name, data.NeedItem);
                continue;
            }

            if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
            {
                _slots[i].SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                continue;
            }

            else if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
            {
                _slots[i].SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                continue;
            }

            _slots[i].SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
            continue;
        }
    }

    public override void SetAttention()
    {
    }

    public override void SetNotAttention()
    {
    }


    private void OnBuyButtonClicked(FoodData data)
    {
        if (UserInfo.IsGiveRecipe(data.Id))
        {
            PopupManager.Instance.ShowTextError();
            return;
        }

        if (!UserInfo.IsScoreValid(data))
        {
            PopupManager.Instance.ShowTextLackScore();
            return;
        }

        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
        {
            PopupManager.Instance.ShowTextLackMoney();
            return;
        }

        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
        {
            PopupManager.Instance.ShowTextLackDia();
            return;
        }

        if (data.MoneyType == MoneyType.Gold)
            UserInfo.AddMoney(-data.BuyPrice);

        else if (data.MoneyType == MoneyType.Dia)
            UserInfo.AddDia(-data.BuyPrice);

        UserInfo.GiveRecipe(data);
        PopupManager.Instance.ShowDisplayText("새로운 레시피를 배웠어요!");
    }


    private void OnUpgradeButtonClicked(FoodData data)
    {
        _uiNav.Push("UIRecipeUpgrade");
    }


    private void OnSlotClicked(FoodData data)
    {
        _uiUpgrade.SetData(data);
        _uiRecipePreview.SetData(data);
    }
}
