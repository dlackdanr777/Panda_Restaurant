using Muks.MobileUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class UIRecipeTab : UIRestaurantAdminTab
{
    public event Action OnShowEvent;
    public event Action OnBuyEvent;

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
    private SlotDisplayState[] _slotDisplayStates;
    private bool _isInitialized = false;
    private bool _isSelected;
    private bool _needsFullRefresh = true;
    private bool _needsAvailabilityRefresh;

    private enum SlotDisplayState
    {
        Unknown,
        Owned,
        LowReputation,
        NeedItem,
        MiniGame,
        NotEnoughMoney,
        NotEnoughDia,
        Buyable
    }


    public override void Init()
    {
        if (_isInitialized) return;

        _foodDataList = FoodDataManager.Instance.GetSortFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnUpgradeButtonClicked);
        
        if (_foodDataList.Count > 0)
        {
            _uiRecipePreview.SetData(_foodDataList[0]);
            _uiUpgrade.SetData(_foodDataList[0]);
        }

        InitializeSlots();
        SubscribeEvents();

        _isInitialized = true;
    }

    private void InitializeSlots()
    {
        int foodCount = _foodDataList.Count;
        _slots = new UIRestaurantAdminFoodTypeSlot[foodCount];
        _slotDisplayStates = new SlotDisplayState[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
            int index = i;
            FoodData data = _foodDataList[index];
            slot.Init(() => OnSlotClicked(data));
            slot.SetFoodType(data.FoodType);
            slot.transform.SetSiblingIndex(i);
            _slots[i] = slot;
        }
    }

    private void SubscribeEvents()
    {
        UserInfo.OnUpgradeRecipeHandler += RequestFullRefresh;
        UserInfo.OnGiveRecipeHandler += RequestFullRefresh;
        UserInfo.OnChangeMoneyHandler += RequestAvailabilityRefresh;
        UserInfo.OnChangeScoreHandler += RequestFullRefresh;
        GameManager.Instance.OnChangeScoreHandler += RequestFullRefresh;
    }

    public void SetView(FoodData data)
    {
        _uiRecipePreview.SetData(data);
    }

    public override void UpdateUI()
    {
        RequestFullRefresh();
    }

    public void RequestFullRefresh()
    {
        _needsFullRefresh = true;
        TryApplyPendingRefresh();
    }

    private void RequestAvailabilityRefresh()
    {
        _needsAvailabilityRefresh = true;
        TryApplyPendingRefresh();
    }

    private void TryApplyPendingRefresh()
    {
        if (!IsContentVisible() || _foodDataList == null || _foodDataList.Count == 0)
            return;

        if (_needsFullRefresh)
        {
            RefreshVisibleUI("UIRecipeTab.FullRefresh", true);
            _needsFullRefresh = false;
            _needsAvailabilityRefresh = false;
            return;
        }

        if (_needsAvailabilityRefresh)
        {
            RefreshVisibleUI("UIRecipeTab.AvailabilityRefresh", false);
            _needsAvailabilityRefresh = false;
        }
    }

    private bool IsContentVisible()
    {
        return _isInitialized &&
               _isSelected &&
               _slotParnet != null &&
               _slotParnet.gameObject.activeInHierarchy;
    }

    private void RefreshVisibleUI(string profilerSampleName, bool refreshFullPreview)
    {
        Profiler.BeginSample(profilerSampleName);
        try
        {
            if (refreshFullPreview)
                _uiRecipePreview.UpdateUI();
            else
                _uiRecipePreview.UpdatePurchaseAvailability();

            int dataCount = _foodDataList.Count;
            for (int i = 0; i < dataCount; i++)
            {
                FoodData data = _foodDataList[i];
                SlotDisplayState displayState = GetSlotDisplayState(data);
                if (_slotDisplayStates[i] == displayState)
                    continue;

                ApplySlotDisplayState(data, _slots[i], displayState);
                _slotDisplayStates[i] = displayState;
            }
        }
        finally
        {
            Profiler.EndSample();
        }
    }

    private static SlotDisplayState GetSlotDisplayState(FoodData data)
    {
        if (UserInfo.IsGiveRecipe(data.Id))
            return SlotDisplayState.Owned;

        if (!UserInfo.IsScoreValid(data))
            return SlotDisplayState.LowReputation;

        if (!string.IsNullOrWhiteSpace(data.NeedItem))
            return UserInfo.IsGiveGachaItem(data.NeedItem)
                ? SlotDisplayState.MiniGame
                : SlotDisplayState.NeedItem;

        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
            return SlotDisplayState.NotEnoughMoney;

        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
            return SlotDisplayState.NotEnoughDia;

        return SlotDisplayState.Buyable;
    }

    private static void ApplySlotDisplayState(
        FoodData data,
        UIRestaurantAdminFoodTypeSlot slot,
        SlotDisplayState displayState)
    {
        string priceText = data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice);

        switch (displayState)
        {
            case SlotDisplayState.Owned:
                slot.SetNone(data.ThumbnailSprite, data.Name);
                break;
            case SlotDisplayState.LowReputation:
                slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                break;
            case SlotDisplayState.NeedItem:
                slot.SetNeedItem(data.ThumbnailSprite, data.Name, data.NeedItem);
                break;
            case SlotDisplayState.MiniGame:
                slot.SetMiniGame(data.ThumbnailSprite, data.Name, data.NeedItem);
                break;
            case SlotDisplayState.NotEnoughMoney:
                slot.SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, priceText);
                break;
            case SlotDisplayState.NotEnoughDia:
                slot.SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, priceText);
                break;
            case SlotDisplayState.Buyable:
                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
                break;
        }
    }

    public override void SetAttention()
    {
        _isSelected = true;
        TryApplyPendingRefresh();
        OnShowEvent?.Invoke();
    }

    public override void SetNotAttention()
    {
        _isSelected = false;
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
        OnBuyEvent?.Invoke();
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
    
    public override void ChangeFloorType(ERestaurantFloorType floorType)
    {
        UpdateTabBackground(floorType);
    }

    private void OnDestroy()
    {
        UserInfo.OnUpgradeRecipeHandler -= RequestFullRefresh;
        UserInfo.OnGiveRecipeHandler -= RequestFullRefresh;
        UserInfo.OnChangeMoneyHandler -= RequestAvailabilityRefresh;
        UserInfo.OnChangeScoreHandler -= RequestFullRefresh;
        GameManager.Instance.OnChangeScoreHandler -= RequestFullRefresh;
    }
}
