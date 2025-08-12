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
    private bool _isInitialized = false;


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
        UpdateUIOptimized();

        _isInitialized = true;
    }

    private void InitializeSlots()
    {
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
    }

    private void SubscribeEvents()
    {
        UserInfo.OnUpgradeRecipeHandler += UpdateUIOptimized;
        UserInfo.OnGiveRecipeHandler += UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler += UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler += UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler += UpdateUIOptimized;
    }

    public void SetView(FoodData data)
    {
        _uiRecipePreview.SetData(data);
    }

    public override void UpdateUI()
    {
        UpdateUIOptimized();
    }

    // 대폭 최적화된 UpdateUI (정렬 없이 기존 순서대로)
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeSelf || _foodDataList == null || _foodDataList.Count == 0)
            return;

        _uiRecipePreview.UpdateUI();

        int dataCount = _foodDataList.Count;
        
        // 기존 리스트 순서대로 슬롯 처리
        for (int i = 0; i < dataCount; i++)
        {
            var data = _foodDataList[i];
            var slot = _slots[i];
            
            slot.SetFoodType(data.FoodType);
            slot.transform.SetSiblingIndex(i);

            if (UserInfo.IsGiveRecipe(data.Id))
            {
                slot.SetNone(data.ThumbnailSprite, data.Name);
            }
            else
            {
                ProcessBuyableSlot(data, slot);
            }
        }
    }

    private void ProcessBuyableSlot(FoodData data, UIRestaurantAdminFoodTypeSlot slot)
    {
        // 평판 체크
        if (!UserInfo.IsScoreValid(data))
        {
            slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
            return;
        }

        // 필요 아이템 체크
        if (!string.IsNullOrWhiteSpace(data.NeedItem))
        {
            if (!UserInfo.IsGiveGachaItem(data.NeedItem))
            {
                slot.SetNeedItem(data.ThumbnailSprite, data.Name, data.NeedItem);
            }
            else
            {
                slot.SetMiniGame(data.ThumbnailSprite, data.Name, data.NeedItem);
            }
            return;
        }

        // 가격 체크 및 슬롯 설정
        string priceText = data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice);

        switch (data.MoneyType)
        {
            case MoneyType.Gold when !UserInfo.IsMoneyValid(data):
                slot.SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, priceText);
                break;
            
            case MoneyType.Dia when !UserInfo.IsDiaValid(data):
                slot.SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, priceText);
                break;
            
            default:
                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
                break;
        }
    }

    public override void SetAttention()
    {
        UpdateUI();
    }

    public override void SetNotAttention()
    {
        // 필요시 구현
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

    private void OnDestroy()
    {
        UserInfo.OnUpgradeRecipeHandler -= UpdateUIOptimized;
        UserInfo.OnGiveRecipeHandler -= UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler -= UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler -= UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUIOptimized;
    }
}
