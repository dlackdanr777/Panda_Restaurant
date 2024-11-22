using Muks.MobileUI;
using System.Collections.Generic;
using UnityEngine;

public class UIRecipeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private UIRecipeUpgrade _uiUpgrade;
    [SerializeField] private UIRecipePreview _uiRecipePreview;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminSlot _slotPrefab;

    private UIRestaurantAdminSlot[] _slots;
    private List<FoodData> _foodDataList;
    private Dictionary<FoodData, UIRestaurantAdminSlot> _slotDic = new Dictionary<FoodData, UIRestaurantAdminSlot>();

    public void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, OnUpgradeButtonClicked);
        _uiRecipePreview.SetData(_foodDataList[0]);
        _uiUpgrade.SetData(_foodDataList[0]);

        int foodCount = _foodDataList.Count;

        _slots = new UIRestaurantAdminSlot[foodCount];

        for (int i = 0; i < foodCount; ++i)
        {
            UIRestaurantAdminSlot slot = Instantiate(_slotPrefab, _slotParnet);

            int index = i;
            FoodData data = _foodDataList[index];
            slot.Init(() => OnSlotClicked(data));
            _slots[i] = slot;
            _slotDic.Add(data, slot);
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


    public void UpdateUI()
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

            if (!UserInfo.IsMoneyValid(data))
            {
                _slots[i].SetNotEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                continue;
            }

            _slots[i].SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
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

        if (!UserInfo.IsScoreValid(data))
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (!UserInfo.IsMoneyValid(data))
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AddMoney(-data.BuyPrice);
        UserInfo.GiveRecipe(data);
        TimedDisplayManager.Instance.ShowText("새로운 레시피를 배웠어요!");
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
