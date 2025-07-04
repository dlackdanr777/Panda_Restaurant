using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIKitchen : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIKitchenPreview _uikitchenPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText1;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminFoodTypeSlot _slotPrefab;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;

    private KitchenUtensilType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminFoodTypeSlot>[] _slots = new List<UIRestaurantAdminFoodTypeSlot>[(int)KitchenUtensilType.Length];
    private List<KitchenUtensilData> _currentTypeDataList;

    // 성능 최적화를 위한 캐시 변수들
    private readonly (ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)[] _equipSlotCache = 
        new (ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)[(int)ERestaurantFloorType.Length];
    private readonly List<int> _siblingIndexCache = new List<int>(32);
    private bool _isInitialized = false;

    public override void Init()
    {
        if (_isInitialized) return;

        _leftArrowButton.AddListener(() => ChangeKitchenData(-1));
        _rightArrowButton.AddListener(() => ChangeKitchenData(1));
        _uikitchenPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        // 슬롯 미리 생성 최적화
        InitializeSlots();

        // 이벤트 구독
        SubscribeEvents();

        _isInitialized = true;
        gameObject.SetActive(false);
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < (int)KitchenUtensilType.Length; i++)
        {
            List<KitchenUtensilData> typeDataList = KitchenUtensilDataManager.Instance.GetSortKitchenUtensilDataList((KitchenUtensilType)i);
            _slots[i] = new List<UIRestaurantAdminFoodTypeSlot>(typeDataList.Count);
            
            for (int j = 0; j < typeDataList.Count; j++)
            {
                int dataIndex = j;
                UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[dataIndex]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void SubscribeEvents()
    {
        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenEvent;
        UserInfo.OnGiveKitchenUtensilHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        // 데이터 설정과 UI 업데이트를 한 번에 처리
        SetKitchenUtensilDataDataOptimized(KitchenUtensilType.Burner1);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() => 
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true; 
        });
    }

    public void ShowUIKitchen(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        _uiRestaurantAdmin.MainUISetActive(false);
        _uiRestaurantAdmin.ShowKitchenTab();
        _uiNav.Push("UIKitchen");
        _currentFloorType = floorType;
        SetKitchenUtensilDataDataOptimized(type);
    }

    // 최적화된 데이터 설정 메서드
    private void SetKitchenUtensilDataDataOptimized(KitchenUtensilType type)
    {
        // 이전 타입의 슬롯들 비활성화 (배치 최적화)
        if (_currentType != type && _slots[(int)_currentType] != null)
        {
            var currentSlots = _slots[(int)_currentType];
            for (int i = 0; i < currentSlots.Count; i++)
            {
                currentSlots[i].gameObject.SetActive(false);
            }
        }

        _currentType = type;
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetSortKitchenUtensilDataList(type);
        _typeText1.text = Utility.KitchenUtensilTypeStringConverter(type);

        // 프리뷰와 UI 업데이트를 함께 처리
        SetKitchenPreviewOptimized();
        UpdateUIOptimized();
    }

    private void SetKitchenPreviewOptimized()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _currentFloorType, _currentType);
        KitchenUtensilData previewData = equipData ?? _currentTypeDataList[0];
        _uikitchenPreview.SetData(_currentFloorType, previewData);
    }

    // 대폭 최적화된 UpdateUI
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uikitchenPreview.UpdateUI();

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        int dataCount = _currentTypeDataList.Count;
        int equipSlotCount = 0;

        // SiblingIndex 업데이트 최소화를 위한 캐시
        _siblingIndexCache.Clear();

        // 메인 루프 최적화
        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];
            
            slot.gameObject.SetActive(true);
            slot.SetFoodType(data.FoodType);

            bool isGiven = UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data);
            
            if (isGiven)
            {
                ProcessEquippedSlot(data, slot, ref equipSlotCount, i);
            }
            else
            {
                ProcessUnequippedSlot(data, slot);
                _siblingIndexCache.Add(i);
            }
        }

        // 배치된 슬롯들 정렬 및 SiblingIndex 일괄 업데이트
        OptimizeSiblingIndexes(equipSlotCount);
    }

    private void ProcessEquippedSlot(KitchenUtensilData data, UIRestaurantAdminFoodTypeSlot slot, ref int equipSlotCount, int originalIndex)
    {
        ERestaurantFloorType floorType = UserInfo.GetEquipKitchenUtensilFloorType(UserInfo.CurrentStage, data);
        
        if (floorType < ERestaurantFloorType.Length)
        {
            _equipSlotCache[equipSlotCount++] = (floorType, slot);
        }

        string statusText = floorType switch
        {
            ERestaurantFloorType.Floor1 or ERestaurantFloorType.Floor2 or ERestaurantFloorType.Floor3 => "배치중",
            _ => "배치하기"
        };

        if (floorType <= ERestaurantFloorType.Floor3)
        {
            slot.SetUse(data.ThumbnailSprite, data.Name, statusText, floorType);
        }
        else
        {
            slot.SetOperate(data.ThumbnailSprite, data.Name, statusText);
        }
    }

    private void ProcessUnequippedSlot(KitchenUtensilData data, UIRestaurantAdminFoodTypeSlot slot)
    {
        if (!UnlockConditionManager.GetConditionEnabled(data.UnlockData))
        {
            slot.SetLock(data.ThumbnailSprite, data.Name);
            return;
        }

        if (!UserInfo.IsScoreValid(data))
        {
            slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
            return;
        }

        string priceText = data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice);

        if (data.MoneyType == MoneyType.Gold)
        {
            if (UserInfo.IsMoneyValid(data))
                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
            else
                slot.SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, priceText);
        }
        else if (data.MoneyType == MoneyType.Dia)
        {
            if (UserInfo.IsDiaValid(data))
                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
            else
                slot.SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, priceText);
        }
        else
        {
            slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
        }
    }

    private void OptimizeSiblingIndexes(int equipSlotCount)
    {
        if (equipSlotCount > 0)
        {
            // 장착된 슬롯들만 정렬 (Array.Sort 대신 간단한 정렬 사용)
            Array.Sort(_equipSlotCache, 0, equipSlotCount, 
                Comparer<(ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));

            // SiblingIndex 일괄 업데이트
            for (int i = 0; i < equipSlotCount; i++)
            {
                _equipSlotCache[i].Item2.transform.SetSiblingIndex(i);
            }
        }

        // 나머지 슬롯들의 SiblingIndex 업데이트 (필요한 경우만)
        for (int i = 0; i < _siblingIndexCache.Count; i++)
        {
            _slots[(int)_currentType][_siblingIndexCache[i]].transform.SetSiblingIndex(equipSlotCount + i);
        }
    }

    private void ChangeKitchenData(int dir)
    {
        int currentIndex = (int)_currentType;
        int maxIndex = (int)KitchenUtensilType.Length;
        int newIndex = ((currentIndex + dir) % maxIndex + maxIndex) % maxIndex;
        
        SetKitchenUtensilDataDataOptimized((KitchenUtensilType)newIndex);
    }

    // 기존 메서드들 유지
 private void OnEquipButtonClicked(ERestaurantFloorType type, ShopData data)
    {
        if (data == null)
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _dequipSound);
            UserInfo.SetNullEquipKitchenUtensil(UserInfo.CurrentStage, type, _currentType);
        }
        else
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _equipSound);
            UserInfo.SetEquipKitchenUtensil(UserInfo.CurrentStage, type, data.Id);
        }
        
        // 전체 재설정 대신 업데이트만
        UpdateUIOptimized();
    }

    private void OnBuyButtonClicked(ShopData data)
    {
        if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data.Id))
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

        UserInfo.GiveKitchenUtensil(UserInfo.CurrentStage, data.Id);
        PopupManager.Instance.ShowDisplayText("새로운 주방 기구를 구매했어요!");
    }

    // 호환성을 위한 기존 메서드들
    private void SetKitchenUtensilDataData(KitchenUtensilType type) => SetKitchenUtensilDataDataOptimized(type);
    private void SetKitchenPreview() => SetKitchenPreviewOptimized();
    private void UpdateUI() => UpdateUIOptimized();

    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(_currentFloorType, data);
    }

    private void OnChangeKitchenEvent(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        UpdateUIOptimized();
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _uiRestaurantAdmin.MainUISetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= OnChangeKitchenEvent;
        UserInfo.OnGiveKitchenUtensilHandler -= UpdateUI;
        UserInfo.OnChangeMoneyHandler -= UpdateUI;
        UserInfo.OnChangeScoreHandler -= UpdateUI;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUI;
    }
}
