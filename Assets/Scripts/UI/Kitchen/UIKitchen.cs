using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchen : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIKitchenPreview _uikitchenPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText1;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Sprite _normalBackgroundSprite;
    [SerializeField] private Sprite _vipBackgroundSprite;
    [SerializeField] private GameObject _normalObject;
    [SerializeField] private GameObject _vipObject;


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

    private bool _isInitialized = false;
    private Vector3 _tmpScale;

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
        _tmpScale = _animeUI.transform.localScale;
        gameObject.SetActive(false);
    }

    private void InitializeSlots()
    {
        // 층별로 주방 기구 수가 다를 수 있으므로, 각 타입마다 충분한 슬롯 풀을 생성
        for (int i = 0; i < (int)KitchenUtensilType.Length; i++)
        {
            // 모든 층의 주방 기구를 합쳐서 최대 슬롯 수 계산
            int maxSlotCount = 0;
            for (int floor = 0; floor < (int)ERestaurantFloorType.Length; floor++)
            {
                List<KitchenUtensilData> floorDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)i, (ERestaurantFloorType)floor);
                if (floorDataList.Count > maxSlotCount)
                    maxSlotCount = floorDataList.Count;
            }

            _slots[i] = new List<UIRestaurantAdminFoodTypeSlot>(maxSlotCount);
            
            // 최대 개수만큼 슬롯 생성
            for (int j = 0; j < maxSlotCount; j++)
            {
                UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => { }); // 클릭 이벤트는 UpdateUI에서 동적으로 설정
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

        TweenData tween = _animeUI.TweenScale(_tmpScale, _showDuration, _showTweenMode);
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
        UpdateFloorUI();
        SetKitchenUtensilDataDataOptimized(type);
    }

    private void UpdateFloorUI()
    {
        if (_currentFloorType == ERestaurantFloorType.Floor1)
        {
            _backgroundImage.sprite = _normalBackgroundSprite;
            _normalObject.SetActive(true);
            _vipObject.SetActive(false);
        }
        else if (_currentFloorType == ERestaurantFloorType.Floor2)
        {
            _backgroundImage.sprite = _vipBackgroundSprite;
            _normalObject.SetActive(false);
            _vipObject.SetActive(true);
        }
        else // Floor3 이상
        {
            _backgroundImage.sprite = _vipBackgroundSprite;
            _normalObject.SetActive(false);
            _vipObject.SetActive(true);
        }
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
        // 현재 층의 주방 기구 데이터만 가져오기
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetSortKitchenUtensilDataList(type, _currentFloorType);
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

    // 대폭 최적화된 UpdateUI (정렬 없이 기존 순서대로)
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uikitchenPreview.UpdateUI();

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        int dataCount = _currentTypeDataList.Count;
        
        // 기존 리스트 순서대로 슬롯 처리
        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];

            // 슬롯 클릭 이벤트 재설정 (층이 변경될 수 있으므로)
            slot.Init(() => OnSlotClicked(data));
            
            slot.gameObject.SetActive(true);
            slot.SetFoodType(data.FoodType);
            slot.transform.SetSiblingIndex(i);

            bool isGiven = UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data);
            
            if (isGiven)
            {
                ProcessEquippedSlot(data, slot);
            }
            else
            {
                ProcessUnequippedSlot(data, slot);
            }
        }

        // 사용하지 않는 슬롯 비활성화
        for (int i = dataCount; i < currentSlots.Count; i++)
        {
            currentSlots[i].gameObject.SetActive(false);
        }
    }

    // 간소화된 ProcessEquippedSlot
    private void ProcessEquippedSlot(KitchenUtensilData data, UIRestaurantAdminFoodTypeSlot slot)
    {
        ERestaurantFloorType floorType = UserInfo.GetEquipKitchenUtensilFloorType(UserInfo.CurrentStage, data);

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

    // ProcessUnequippedSlot은 그대로 유지
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
        _animeUI.transform.localScale = _tmpScale;

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
