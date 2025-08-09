using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIFurniture : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private UIFurniturePreview _uiFurniturePreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText;

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

    private FurnitureType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminFoodTypeSlot>[] _slots = new List<UIRestaurantAdminFoodTypeSlot>[(int)FurnitureType.Length];
    private List<FurnitureData> _currentTypeDataList;

    private bool _isInitialized = false;

    public override void Init()
    {
        if (_isInitialized) return;

        _leftArrowButton.AddListener(() => ChangeFurnitureData(-1));
        _rightArrowButton.AddListener(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        // 슬롯 미리 생성 최적화
        InitializeSlots();

        // 이벤트 구독
        SubscribeEvents();

        // 초기 설정
        SetFurnitureDataOptimized(FurnitureType.Table1);

        _isInitialized = true;
        gameObject.SetActive(false);
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < (int)FurnitureType.Length; i++)
        {
            List<FurnitureData> typeDataList = FurnitureDataManager.Instance.GetSortFurnitureDataList((FurnitureType)i);
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
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
        UserInfo.OnGiveFurnitureHandler += UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler += UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler += UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler += UpdateUIOptimized;
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        // 데이터 설정과 UI 업데이트를 한 번에 처리
        SetFurnitureDataOptimized(FurnitureType.Table1);

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

    public void ShowUIFurniture(ERestaurantFloorType floorType, FurnitureType type)
    {
        _uiRestaurantAdmin.MainUISetActive(false);
        _uiRestaurantAdmin.ShowFurnitureTab();
        _uiNav.Push("UIFurniture");
        _currentFloorType = floorType;
        SetFurnitureDataOptimized(type);
    }

    // 최적화된 데이터 설정 메서드
    private void SetFurnitureDataOptimized(FurnitureType type)
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
        _currentTypeDataList = FurnitureDataManager.Instance.GetSortFurnitureDataList(type);
        _typeText.text = Utility.FurnitureTypeStringConverter(type);

        // 프리뷰와 UI 업데이트를 함께 처리
        SetFurniturePreviewOptimized();
        UpdateUIOptimized();
    }

    private void SetFurniturePreviewOptimized()
    {
        FurnitureData equipData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, _currentType);
        FurnitureData previewData = equipData ?? (_currentTypeDataList.Count > 0 ? _currentTypeDataList[0] : null);
        _uiFurniturePreview.SetData(_currentFloorType, previewData);
    }

    // 대폭 최적화된 UpdateUI (UIStaff와 동일한 패턴)
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiFurniturePreview.UpdateUI();

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        int dataCount = _currentTypeDataList.Count;

        // 가구 데이터를 우선순위에 따라 정렬
        var prioritizedIndices = GetPrioritizedFurnitureIndices(dataCount);

        // 정렬된 순서대로 슬롯 처리
        for (int displayIndex = 0; displayIndex < prioritizedIndices.Count; displayIndex++)
        {
            int dataIndex = prioritizedIndices[displayIndex];
            var data = _currentTypeDataList[dataIndex];
            var slot = currentSlots[dataIndex];

            slot.gameObject.SetActive(true);
            slot.SetFoodType(data.FoodType);
            slot.transform.SetSiblingIndex(displayIndex);

            bool isGiven = UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data);

            if (isGiven)
            {
                ProcessEquippedSlot(data, slot);
            }
            else
            {
                ProcessUnequippedSlot(data, slot);
            }
        }
    }

    private List<int> GetPrioritizedFurnitureIndices(int dataCount)
    {
        var equippedFurniture = new List<int>();
        var ownedUnequippedFurniture = new List<int>();
        var unownedFurniture = new List<int>();

        // 가구들을 우선순위별로 분류 (기존 순서 유지)
        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            bool isGiven = UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data);
            bool isEquipped = isGiven && UserInfo.IsEquipFurniture(UserInfo.CurrentStage, data);

            if (isEquipped)
            {
                equippedFurniture.Add(i);
            }
            else if (isGiven)
            {
                ownedUnequippedFurniture.Add(i);
            }
            else
            {
                unownedFurniture.Add(i);
            }
        }

        // 장착된 가구들을 플로어 순서로 정렬 (같은 플로어 내에서는 기존 순서 유지)
        equippedFurniture.Sort((a, b) =>
        {
            var dataA = _currentTypeDataList[a];
            var dataB = _currentTypeDataList[b];
            var floorA = UserInfo.GetEquipFurnitureFloorType(UserInfo.CurrentStage, dataA);
            var floorB = UserInfo.GetEquipFurnitureFloorType(UserInfo.CurrentStage, dataB);

            // 플로어가 다르면 플로어 순서로 정렬
            if (floorA != floorB)
                return floorA.CompareTo(floorB);

            // 같은 플로어면 기존 리스트 순서 유지 (인덱스 순서)
            return a.CompareTo(b);
        });

        // 최종 우선순위: 장착됨 → 소유하지만 미장착 → 미소유
        // 각 그룹 내에서는 기존 순서 유지
        var result = new List<int>();
        result.AddRange(equippedFurniture);                // 플로어 순서 + 기존 순서
        result.AddRange(ownedUnequippedFurniture);         // 기존 순서 유지
        result.AddRange(unownedFurniture);                 // 기존 순서 유지

        return result;
    }

    // 간소화된 ProcessEquippedSlot (SiblingIndex 처리 제거)
    private void ProcessEquippedSlot(FurnitureData data, UIRestaurantAdminFoodTypeSlot slot)
    {
        ERestaurantFloorType floorType = UserInfo.GetEquipFurnitureFloorType(UserInfo.CurrentStage, data);

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

    private void ProcessUnequippedSlot(FurnitureData data, UIRestaurantAdminFoodTypeSlot slot)
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

    private void ChangeFurnitureData(int dir)
    {
        int currentIndex = (int)_currentType;
        int maxIndex = (int)FurnitureType.Length;
        int newIndex = ((currentIndex + dir) % maxIndex + maxIndex) % maxIndex;

        SetFurnitureDataOptimized((FurnitureType)newIndex);
    }

    private void OnEquipButtonClicked(ERestaurantFloorType type, FurnitureData data)
    {
        if (data == null)
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _dequipSound);
            UserInfo.SetNullEquipFurniture(UserInfo.CurrentStage, type, _currentType);
        }
        else
        {
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _equipSound);
            UserInfo.SetEquipFurniture(UserInfo.CurrentStage, type, data);
        }

        // 전체 재설정 대신 업데이트만
        UpdateUIOptimized();
    }

    private void OnBuyButtonClicked(FurnitureData data)
    {
        if (UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data.Id))
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

        UserInfo.GiveFurniture(UserInfo.CurrentStage, data);
        PopupManager.Instance.ShowDisplayText("새로운 가구를 구매했어요!");
    }

    private void OnChangeFurnitureEvent(ERestaurantFloorType floor, FurnitureType type)
    {
        if (!gameObject.activeInHierarchy)
            return;

        UpdateUIOptimized();
    }

    private void OnSlotClicked(FurnitureData data)
    {
        _uiFurniturePreview.SetData(_currentFloorType, data);
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
        UserInfo.OnGiveFurnitureHandler -= UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler -= UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler -= UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUIOptimized;
    }
}
