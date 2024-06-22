using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;

public class UIRecipe : MobileUIView
{
    [Header("Components")]
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIRecipePreview _uiRecipePreview;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRecipeSlot _slotPrefab;

    private StaffType _currentType;
    private UIRecipeSlot[] _slots;
    private List<FoodData> _foodDataList;

    public override void Init()
    {
        _foodDataList = FoodDataManager.Instance.GetFoodDataList();
        _uiRecipePreview.Init(OnBuyButtonClicked, null);
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
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _uiRecipePreview.SetFoodData(null);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
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
        //TODO: µ· È®ÀÎ ÈÄ ½ºÅÂÇÁ È¹µæÀ¸·Î º¯°æÇØ¾ßÇÔ
    }

    private void OnSlotClicked(FoodData data)
    {
        _uiRecipePreview.SetFoodData(data);
    }
}
