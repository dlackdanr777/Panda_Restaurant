using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFurniture : MobileUIView
{
    private GameManager _subscribedGameManager;


    public event Action OnShowEvent;
    public event Action OnBuyEvent;

        

    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private UIFurniturePreview _uiFurniturePreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText;
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

    private FurnitureType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminFoodTypeSlot> _slots;
    private List<FurnitureData> _currentTypeDataList;

    private SlotState[] _slotStates;
    private bool _entryPrepared;
    private DirtyFlags _dirtyFlags;

    [Flags]
    private enum DirtyFlags
    {
        None = 0,
        Money = 1,
        Dia = 2,
        Score = 4,
        Ownership = 8,
        Unlock = 16,
        Placement = 32
    }

    private bool _isInitialized = false;
    private Vector3 _tmpScale;

    public FurnitureData SelectedData => _uiFurniturePreview != null ? _uiFurniturePreview.SelectedData : null;
    public RectTransform BuyButtonRect => _uiFurniturePreview != null ? _uiFurniturePreview.BuyButtonRect : null;
    public RectTransform EquipButtonRect => _uiFurniturePreview != null ? _uiFurniturePreview.EquipButtonRect : null;
    public RectTransform ScoreButtonRect => _uiFurniturePreview != null ? _uiFurniturePreview.ScoreButtonRect : null;

    public bool TrySelectFurniture(string id)
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

        _leftArrowButton.AddListener(() => ChangeFurnitureData(-1));
        _rightArrowButton.AddListener(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

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
        int maxSlotCount = 0;
        for (int type = 0; type < (int)FurnitureType.Length; type++)
        {
            for (int floor = 0; floor < (int)ERestaurantFloorType.Length; floor++)
            {
                int count = FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)type, (ERestaurantFloorType)floor).Count;
                maxSlotCount = Mathf.Max(maxSlotCount, count);
            }
        }

        // All categories use the same prefab. Prewarm the largest list before any tab opens.
        _slots = new List<UIRestaurantAdminFoodTypeSlot>(maxSlotCount);
        _slotStates = new SlotState[maxSlotCount];
        for (int i = 0; i < maxSlotCount; i++)
        {
            int slotIndex = i;
            UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
            slot.Init(() => OnSlotClicked(slotIndex));
            _slots.Add(slot);
            slot.gameObject.SetActive(false);
        }
    }

    private void SubscribeEvents()
    {
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
        UserInfo.OnGiveFurnitureHandler += OnGiveEvent;
        UserInfo.OnGiveKitchenUtensilHandler += OnUnlockEvent;
        UserInfo.OnGiveRecipeHandler += OnUnlockEvent;
        UserInfo.OnGiveGachaItemHandler += OnUnlockEvent;
        UserInfo.OnChangeFloorHandler += OnUnlockEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
        UserInfo.OnChangeDiaHandler += OnChangeDiaEvent;
        UserInfo.OnChangeScoreHandler += UpdateUIOptimized;
        _subscribedGameManager = GameManager.Instance;
        _subscribedGameManager.OnChangeScoreHandler += UpdateUIOptimized;
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        if (!_entryPrepared)
            SetFurnitureDataOptimized(FurnitureType.Table1);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenScale(_tmpScale, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
            OnShowEvent?.Invoke();
        });
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

    public void ShowUIFurniture(ERestaurantFloorType floorType, FurnitureType type)
    {
        _uiRestaurantAdmin.MainUISetActive(false);
        _uiRestaurantAdmin.ShowFurnitureTab();
        _currentFloorType = floorType;
        UpdateFloorUI();
        SetFurnitureDataOptimized(type);
        if (_uiNav.CheckActiveView("UIFurniture")) return;

        // Floor, type and selection are already bound, so Show() must not bind the default type again.
        _entryPrepared = true;
        try { _uiNav.Push("UIFurniture"); }
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
    private void SetFurnitureDataOptimized(FurnitureType type)
    {

        _currentType = type;
        _currentTypeDataList = FurnitureDataManager.Instance.GetSortFurnitureDataList(type, _currentFloorType);
        string typeLabel = Utility.FurnitureTypeStringConverter(type);
        if (_typeText.text != typeLabel)
            _typeText.text = typeLabel;

        SetPreviewOptimized();
        RefreshSlots();
        _dirtyFlags = DirtyFlags.None;
    }

    private void SetPreviewOptimized()
    {
        FurnitureData equipData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, _currentType);
        FurnitureData previewData = equipData ?? (_currentTypeDataList.Count > 0 ? _currentTypeDataList[0] : null);
        _uiFurniturePreview.SetData(_currentFloorType, previewData);
    }

    private readonly struct SlotState : IEquatable<SlotState>
    {
        public readonly FurnitureData Data;
        public readonly int State;
        public readonly int A;
        public readonly int B;

        public SlotState(FurnitureData data, int state, int a, int b)
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

    private static bool ShouldRefreshSlot(FurnitureData data, SlotState previous, DirtyFlags flags)
    {
        if (!ReferenceEquals(data, previous.Data) || (flags & DirtyFlags.Ownership) != 0)
            return true;

        bool owned = previous.State == StateUse || previous.State == StateOperate;
        if (owned)
            return (flags & DirtyFlags.Placement) != 0;
        if ((flags & DirtyFlags.Unlock) != 0)
            return true;
        if ((flags & DirtyFlags.Score) != 0 && previous.State != StateLock)
            return true;

        bool priced = previous.State == StateBuyable || previous.State == StateNoGold || previous.State == StateNoDia;
        return priced && ((data.MoneyType == MoneyType.Gold && (flags & DirtyFlags.Money) != 0)
            || (data.MoneyType == MoneyType.Dia && (flags & DirtyFlags.Dia) != 0));
    }

    private SlotState ComputeSlotState(FurnitureData data)
    {
        EStage stage = UserInfo.CurrentStage;
        if (UserInfo.IsGiveFurniture(stage, data))
        {
            ERestaurantFloorType floorType = UserInfo.GetEquipFurnitureFloorType(stage, data);
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
    private void RefreshSlots(DirtyFlags flags = DirtyFlags.None)
    {
        if (_currentTypeDataList == null)
            return;

        var currentSlots = _slots;
        var states = _slotStates;
        int dataCount = Mathf.Min(_currentTypeDataList.Count, currentSlots.Count);

        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];
            bool bindLayout = flags == DirtyFlags.None;
            if (!bindLayout && !ShouldRefreshSlot(data, states[i], flags))
                continue;

            if (bindLayout)
            {
                if (slot.transform.GetSiblingIndex() != i)
                    slot.transform.SetSiblingIndex(i);
                if (!slot.gameObject.activeSelf)
                    slot.gameObject.SetActive(true);
                slot.SetFoodType(data.FoodType);
            }

            SlotState state = ComputeSlotState(data);
            if (states[i].Equals(state))
                continue;

            states[i] = state;
            if (state.State == StateUse || state.State == StateOperate)
                ProcessEquippedSlot(data, slot, state);
            else
                ProcessUnequippedSlot(data, slot, state);
        }

        for (int i = dataCount; flags == DirtyFlags.None && i < currentSlots.Count; i++)
        {
            if (currentSlots[i].gameObject.activeSelf)
                currentSlots[i].gameObject.SetActive(false);
        }
    }

    // 간소화된 ProcessEquippedSlot
    private void ProcessEquippedSlot(FurnitureData data, UIRestaurantAdminFoodTypeSlot slot, SlotState state)
    {
        ERestaurantFloorType floorType = state.State == StateUse ? (ERestaurantFloorType)state.A : ERestaurantFloorType.Error;

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

    private void ProcessUnequippedSlot(FurnitureData data, UIRestaurantAdminFoodTypeSlot slot, SlotState state)
    {
        if (state.State == StateLock)
        {
            slot.SetLock(data.ThumbnailSprite, data.Name);
            return;
        }

        if (state.State == StateLowScore)
        {
            slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
            return;
        }

        string priceText = data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice);

        if (data.MoneyType == MoneyType.Gold)
        {
            if (state.State == StateBuyable)
                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, priceText, data.MoneyType);
            else
                slot.SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, priceText);
        }
        else if (data.MoneyType == MoneyType.Dia)
        {
            if (state.State == StateBuyable)
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
        MarkDirty(DirtyFlags.Placement);
        FlushDirty();
    }

    private bool _buyInProgress;

    private void OnBuyButtonClicked(FurnitureData data)
    {
        if (_buyInProgress) return;
        _buyInProgress = true;
        try { BuyFurniture(data); }
        finally { _buyInProgress = false; }
    }

    private void BuyFurniture(FurnitureData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Id) || data.BuyPrice < 0
            || (data.MoneyType != MoneyType.Gold && data.MoneyType != MoneyType.Dia)
            || !FurnitureDataManager.Instance.GetFurnitureDataList().Contains(data)) return;
        EStage stage = UserInfo.CurrentStage;
        if (UserInfo.IsGiveFurniture(stage, data.Id))
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

        UserInfo.GiveFurniture(stage, data);
        FlushDirty();
        CompletePurchasePresentation();
    }

    protected virtual void ShowPurchaseRejected(string error) => PopupManager.Instance.ShowDisplayText(error);

    protected virtual void CompletePurchasePresentation()
    {
        PopupManager.Instance.ShowDisplayText("새로운 가구를 구매했어요!");
        OnBuyEvent?.Invoke();
    }

    private void OnGiveEvent() => MarkDirty(DirtyFlags.Ownership);

    private void OnUnlockEvent() => MarkDirty(DirtyFlags.Unlock);

    private void OnChangeMoneyEvent() => MarkDirty(DirtyFlags.Money);

    private void OnChangeDiaEvent() => MarkDirty(DirtyFlags.Dia);

    // Score is raised by both UserInfo and GameManager; they collapse into one refresh.
    private void UpdateUIOptimized() => MarkDirty(DirtyFlags.Score);

    private void OnChangeFurnitureEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        // Placement on any floor can change this category's in-use labels.
        if (type == _currentType)
            MarkDirty(DirtyFlags.Placement);
    }
    private void OnSlotClicked(FurnitureData data)
    {
        _uiFurniturePreview.SetData(_currentFloorType, data);
    }

    // Slots read the data currently bound to their index, so pooled slots never click stale data.
    private void OnSlotClicked(int slotIndex)
    {
        if (_currentTypeDataList == null || slotIndex < 0 || slotIndex >= _currentTypeDataList.Count)
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

        RefreshSlots(flags);
        if (NeedsPreviewRefresh(flags))
            _uiFurniturePreview.UpdateUI();
    }

    // Gold/diamond affect only unowned items priced in that currency; score affects only the score gate.
    // Unlock conditions depend on floor/item/recipe/furniture/kitchen ownership, never on gold, dia or score.
    private bool NeedsPreviewRefresh(DirtyFlags flags)
    {
        FurnitureData selected = SelectedData;
        if (selected == null)
            return false;
        if ((flags & (DirtyFlags.Ownership | DirtyFlags.Unlock | DirtyFlags.Placement)) != 0)
            return true;
        if (UserInfo.IsGiveFurniture(UserInfo.CurrentStage, selected))
            return false;
        if ((flags & DirtyFlags.Score) != 0)
            return true;
        if (selected.MoneyType == MoneyType.Gold)
            return (flags & DirtyFlags.Money) != 0;
        if (selected.MoneyType == MoneyType.Dia)
            return (flags & DirtyFlags.Dia) != 0;
        return false;
    }
    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
        UserInfo.OnGiveFurnitureHandler -= OnGiveEvent;
        UserInfo.OnGiveKitchenUtensilHandler -= OnUnlockEvent;
        UserInfo.OnGiveRecipeHandler -= OnUnlockEvent;
        UserInfo.OnGiveGachaItemHandler -= OnUnlockEvent;
        UserInfo.OnChangeFloorHandler -= OnUnlockEvent;
        UserInfo.OnChangeMoneyHandler -= OnChangeMoneyEvent;
        UserInfo.OnChangeDiaHandler -= OnChangeDiaEvent;
        UserInfo.OnChangeScoreHandler -= UpdateUIOptimized;
        if (_subscribedGameManager != null)
            _subscribedGameManager.OnChangeScoreHandler -= UpdateUIOptimized;
        _subscribedGameManager = null;
    }
}
