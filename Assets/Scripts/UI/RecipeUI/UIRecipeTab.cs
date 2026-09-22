using Muks.MobileUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private bool _isInitialized = false;

    public FoodData SelectedData => _uiRecipePreview != null ? _uiRecipePreview.SelectedData : null;
    public RectTransform BuyButtonRect => _uiRecipePreview != null ? _uiRecipePreview.BuyButtonRect : null;
    public RectTransform MiniGameButtonRect => _uiRecipePreview != null ? _uiRecipePreview.MiniGameButtonRect : null;
    public event Action BeforeMiniGameNavigation
    {
        add { if (_uiRecipePreview != null) _uiRecipePreview.BeforeMiniGameNavigation += value; }
        remove { if (_uiRecipePreview != null) _uiRecipePreview.BeforeMiniGameNavigation -= value; }
    }

    public bool TrySelectRecipe(string id)
    {
        if (!_isInitialized || string.IsNullOrEmpty(id) || _foodDataList == null) return false;
        var data = _foodDataList.Find(item => item.Id == id);
        if (data == null) return false;
        OnSlotClicked(data);
        return true;
    }


    /// <summary>Focuses a recipe in the native list without purchasing it.</summary>
    public bool FocusRecipe(string id)
    {
        if (!_isInitialized || string.IsNullOrEmpty(id) || _foodDataList == null) return false;
        int index = _foodDataList.FindIndex(item => item.Id == id);
        if (index < 0) return false;
        if (!TrySelectRecipe(id)) return false;
        if (_slots != null && index < _slots.Length && _slots[index] != null)
        {
            var scroll = _slots[index].GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.StopMovement();
                var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                var target = _slots[index].transform as RectTransform;
                if (viewport != null && target != null && scroll.content != null && scroll.vertical)
                {
                    // Recipes use a grid. Item index is not a vertical row, so
                    // resolve the actual laid-out slot bounds in viewport space.
                    var contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, scroll.content);
                    var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
                    float scrollableHeight = contentBounds.size.y - viewport.rect.height;
                    if (scrollableHeight > 0.01f)
                        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition
                            + (targetBounds.center.y - viewport.rect.center.y) / scrollableHeight);
                }
            }
        }
        return true;
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
        if (data != null) TrySelectRecipe(data.Id);
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
        OnShowEvent?.Invoke();
    }

    public override void SetNotAttention()
    {
        // 필요시 구현
    }

    private bool _buyInProgress;

    private void OnBuyButtonClicked(FoodData data)
    {
        if (_buyInProgress) return;
        _buyInProgress = true;
        try { BuyRecipe(data); }
        finally { _buyInProgress = false; }
    }

    private void BuyRecipe(FoodData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Id) || data.BuyPrice < 0
            || (data.MoneyType != MoneyType.Gold && data.MoneyType != MoneyType.Dia)
            || !FoodDataManager.Instance.GetFoodDataList().Contains(data)) return;
        if (UserInfo.IsGiveRecipe(data.Id))
        {
            ShowPurchaseRejected("다시 시도해 주세요.");
            return;
        }

        if (!UserInfo.IsScoreValid(data))
        {
            ShowPurchaseRejected("평점이 부족합니다...");
            return;
        }

        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
        {
            ShowPurchaseRejected("골드가 부족합니다...");
            return;
        }

        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
        {
            ShowPurchaseRejected("다이아가 부족합니다...");
            return;
        }

        if (data.MoneyType == MoneyType.Gold)
            UserInfo.AddMoney(-data.BuyPrice);
        else if (data.MoneyType == MoneyType.Dia)
        {
            if (!UserInfo.TrySpendDia(data.BuyPrice, out string error))
            {
                ShowPurchaseRejected(error);
                return;
            }
        }

        UserInfo.GiveRecipe(data);
        CompletePurchasePresentation();
    }

    protected virtual void ShowPurchaseRejected(string error) => PopupManager.Instance.ShowDisplayText(error);

    protected virtual void CompletePurchasePresentation()
    {
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
        UserInfo.OnUpgradeRecipeHandler -= UpdateUIOptimized;
        UserInfo.OnGiveRecipeHandler -= UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler -= UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler -= UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUIOptimized;
    }
}
