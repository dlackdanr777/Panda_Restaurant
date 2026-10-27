using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchen : MobileUIView
{
    private GameManager _subscribedGameManager;

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

    private SlotState[][] _slotStates = new SlotState[(int)KitchenUtensilType.Length][];
    private bool _entryPrepared;
    private DirtyFlags _dirtyFlags;

    [Flags]
    private enum DirtyFlags
    {
        None = 0,
        Money = 1,
        Dia = 2,
        Score = 4,
        Ownership = 8
    }

    private bool _isInitialized = false;
    private Vector3 _tmpScale;

    public KitchenUtensilData SelectedData => _uikitchenPreview != null ? _uikitchenPreview.SelectedData : null;
    public RectTransform BuyButtonRect => _uikitchenPreview != null ? _uikitchenPreview.BuyButtonRect : null;

    public bool TrySelectKitchen(string id)
    {
        if (!_isInitialized || string.IsNullOrEmpty(id) || _currentTypeDataList == null) return false;
        var data = _currentTypeDataList.Find(item => item.Id == id);
        if (data == null || data.Type != _currentType || data.FloorType != _currentFloorType) return false;
        OnSlotClicked(data);
        return true;
    }

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
            _slotStates[i] = new SlotState[maxSlotCount];
            
            // 최대 개수만큼 슬롯 생성
            for (int j = 0; j < maxSlotCount; j++)
            {
                int typeIndex = i;
                int slotIndex = j;
                UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeIndex, slotIndex));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void SubscribeEvents()
    {
        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenEvent;
        UserInfo.OnGiveKitchenUtensilHandler += OnGiveEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
        UserInfo.OnChangeDiaHandler += OnChangeDiaEvent;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        _subscribedGameManager = GameManager.Instance;
        _subscribedGameManager.OnChangeScoreHandler += UpdateUI;
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        if (!_entryPrepared)
            SetKitchenUtensilDataDataOptimized(KitchenUtensilType.Burner1);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

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
        _currentFloorType = floorType;
        UpdateFloorUI();
        SetKitchenUtensilDataDataOptimized(type);
        if (_uiNav.CheckActiveView("UIKitchen")) return;

        // Floor, type and selection are already bound, so Show() must not bind the default type again.
        _entryPrepared = true;
        try { _uiNav.Push("UIKitchen"); }
        finally { _entryPrepared = false; }
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
        if (_currentType != type && _slots[(int)_currentType] != null)
        {
            var currentSlots = _slots[(int)_currentType];
            for (int i = 0; i < currentSlots.Count; i++)
                currentSlots[i].gameObject.SetActive(false);
        }

        _currentType = type;
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetSortKitchenUtensilDataList(type, _currentFloorType);
        string typeLabel = Utility.KitchenUtensilTypeStringConverter(type);
        if (_typeText1.text != typeLabel)
            _typeText1.text = typeLabel;

        SetPreviewOptimized();
        RefreshSlots();
        _dirtyFlags = DirtyFlags.None;
    }

    private void SetPreviewOptimized()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _currentFloorType, _currentType);
        KitchenUtensilData previewData = equipData ?? (_currentTypeDataList.Count > 0 ? _currentTypeDataList[0] : null);
        _uikitchenPreview.SetData(_currentFloorType, previewData);
    }

    private readonly struct SlotState : IEquatable<SlotState>
    {
        public readonly KitchenUtensilData Data;
        public readonly int State;
        public readonly int A;
        public readonly int B;

        public SlotState(KitchenUtensilData data, int state, int a, int b)
        {
            Data = data;
            State = state;
            A = a;
            B = b;
        }

        public bool Equals(SlotState other)
            => ReferenceEquals(Data, other.Data) && State == other.State && A == other.A && B == other.B;
    }

    private const int StateUse = 1;
    private const int StateOperate = 2;
    private const int StateLock = 3;
    private const int StateLowScore = 4;
    private const int StateBuyable = 5;
    private const int StateNoGold = 6;
    private const int StateNoDia = 7;

    private SlotState ComputeSlotState(KitchenUtensilData data)
    {
        EStage stage = UserInfo.CurrentStage;
        if (UserInfo.IsGiveKitchenUtensil(stage, data))
        {
            ERestaurantFloorType floorType = UserInfo.GetEquipKitchenUtensilFloorType(stage, data);
            return floorType <= ERestaurantFloorType.Floor3
                ? new SlotState(data, StateUse, (int)floorType, 0)
                : new SlotState(data, StateOperate, 0, 0);
        }

        if (!UnlockConditionManager.GetConditionEnabled(data.UnlockData))
            return new SlotState(data, StateLock, 0, 0);

        if (!UserInfo.IsScoreValid(data))
            return new SlotState(data, StateLowScore, data.BuyScore, 0);

        int price = data.BuyPrice;
        int moneyType = (int)data.MoneyType;
        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
            return new SlotState(data, StateNoGold, price, moneyType);
        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
            return new SlotState(data, StateNoDia, price, moneyType);
        return new SlotState(data, StateBuyable, price, moneyType);
    }

    // Layout, food type and per-slot state are applied only when they differ from what the slot already shows.
    private void RefreshSlots()
    {
        if (_currentTypeDataList == null)
            return;

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        var states = _slotStates[slotsIndex];
        int dataCount = Mathf.Min(_currentTypeDataList.Count, currentSlots.Count);

        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];

            if (slot.transform.GetSiblingIndex() != i)
                slot.transform.SetSiblingIndex(i);
            if (!slot.gameObject.activeSelf)
                slot.gameObject.SetActive(true);
            slot.SetFoodType(data.FoodType);

            SlotState state = ComputeSlotState(data);
            if (states[i].Equals(state))
                continue;

            states[i] = state;
            if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data))
                ProcessEquippedSlot(data, slot);
            else
                ProcessUnequippedSlot(data, slot);
        }

        for (int i = dataCount; i < currentSlots.Count; i++)
        {
            if (currentSlots[i].gameObject.activeSelf)
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
        MarkDirty(DirtyFlags.Ownership);
        FlushDirty();
    }

    private bool _buyInProgress;

    private void OnBuyButtonClicked(ShopData data)
    {
        if (_buyInProgress) return;
        _buyInProgress = true;
        try { BuyKitchen(data); }
        finally { _buyInProgress = false; }
    }

    private void BuyKitchen(ShopData data)
    {
        if (!(data is KitchenUtensilData kitchen) || string.IsNullOrWhiteSpace(data.Id) || data.BuyPrice < 0
            || (data.MoneyType != MoneyType.Gold && data.MoneyType != MoneyType.Dia)
            || !KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList().Contains(kitchen)) return;
        EStage stage = UserInfo.CurrentStage;
        if (UserInfo.IsGiveKitchenUtensil(stage, data.Id))
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

        UserInfo.GiveKitchenUtensil(stage, kitchen);
        FlushDirty();
        CompletePurchasePresentation();
    }

    protected virtual void ShowPurchaseRejected(string error) => PopupManager.Instance.ShowDisplayText(error);

    protected virtual void CompletePurchasePresentation()
    {
        PopupManager.Instance.ShowDisplayText("새로운 주방 기구를 구매했어요!");
    }

    private void OnGiveEvent() => MarkDirty(DirtyFlags.Ownership);

    private void OnChangeMoneyEvent() => MarkDirty(DirtyFlags.Money);

    private void OnChangeDiaEvent() => MarkDirty(DirtyFlags.Dia);

    // Score is raised by both UserInfo and GameManager; they collapse into one refresh.
    private void UpdateUI() => MarkDirty(DirtyFlags.Score);

    private void OnChangeKitchenEvent(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        // Other floors' placement changes alter this list's "in use" labels, so no floor filtering.
        MarkDirty(DirtyFlags.Ownership);
    }

    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(_currentFloorType, data);
    }

    // Slots read the data currently bound to their index, so pooled slots never click stale data.
    private void OnSlotClicked(int typeIndex, int slotIndex)
    {
        if (typeIndex != (int)_currentType || _currentTypeDataList == null
            || slotIndex < 0 || slotIndex >= _currentTypeDataList.Count)
            return;

        OnSlotClicked(_currentTypeDataList[slotIndex]);
    }

    private void MarkDirty(DirtyFlags flags)
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null)
            return;

        _dirtyFlags |= flags;
    }

    private void LateUpdate()
    {
        if (_dirtyFlags != DirtyFlags.None)
            FlushDirty();
    }

    private void FlushDirty()
    {
        DirtyFlags flags = _dirtyFlags;
        _dirtyFlags = DirtyFlags.None;
        if (flags == DirtyFlags.None || !gameObject.activeInHierarchy || _currentTypeDataList == null)
            return;

        RefreshSlots();
        if (NeedsPreviewRefresh(flags))
            _uikitchenPreview.UpdateUI();
    }

    // Gold/diamond affect only unowned items priced in that currency; score affects only the score gate.
    // Unlock conditions depend on floor/item/recipe/furniture/kitchen ownership, never on gold, dia or score.
    private bool NeedsPreviewRefresh(DirtyFlags flags)
    {
        KitchenUtensilData selected = SelectedData;
        if (selected == null)
            return false;
        if ((flags & DirtyFlags.Ownership) != 0)
            return true;
        if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, selected))
            return false;
        if ((flags & DirtyFlags.Score) != 0)
            return true;
        if (selected.MoneyType == MoneyType.Gold)
            return (flags & DirtyFlags.Money) != 0;
        if (selected.MoneyType == MoneyType.Dia)
            return (flags & DirtyFlags.Dia) != 0;
        return false;
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
        UserInfo.OnGiveKitchenUtensilHandler -= OnGiveEvent;
        UserInfo.OnChangeMoneyHandler -= OnChangeMoneyEvent;
        UserInfo.OnChangeDiaHandler -= OnChangeDiaEvent;
        UserInfo.OnChangeScoreHandler -= UpdateUI;
        if (_subscribedGameManager != null)
            _subscribedGameManager.OnChangeScoreHandler -= UpdateUI;
        _subscribedGameManager = null;
    }
}
