using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaff : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIStaffUpgrade _uiStaffUpgrade;
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIStaffPreview _uiStaffPreview;
    [SerializeField] private UIStaffSkin _uiSkin;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText;
    [SerializeField] private Button _showSkinButton;

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
    [SerializeField] private int _createSlotValue;
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminStaffSlot _slotPrefab;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;


    private StaffData _previewStaffData;
    private EquipStaffType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminStaffSlot>[] _slots = new List<UIRestaurantAdminStaffSlot>[(int)EquipStaffType.Length];
    private List<StaffData> _currentTypeDataList;

    private bool _isInitialized = false;
    private Vector3 _tmpScale;
    private static readonly Vector3 _hideScale = new Vector3(0.3f, 0.3f, 0.3f);

    public override void Init()
    {
        if (_isInitialized) return;

        _uiSkin.Init();
        _leftArrowButton.AddListener(() => ChangeStaffData(-1));
        _rightArrowButton.AddListener(() => ChangeStaffData(1));
        _uiStaffPreview.Init(OnEquipButtonClicked, OnUsingButtonClicked, OnBuyButtonClicked, OnUpgradeButtonClicked);
        // 슬롯 미리 생성 최적화
        InitializeSlots();

        // 이벤트 구독
        SubscribeEvents();

        // 초기 설정
        SetStaffDataOptimized(EquipStaffType.Manager);

        _showSkinButton.onClick.AddListener(OnShowSkinButtonClicked);
        _isInitialized = true;
        _tmpScale = _animeUI.transform.localScale;
        gameObject.SetActive(false);
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < (int)EquipStaffType.Length; i++)
        {
            List<StaffData> typeDataList = StaffDataManager.Instance.GetSortStaffDataList((EquipStaffType)i);
            _slots[i] = new List<UIRestaurantAdminStaffSlot>(typeDataList.Count);
            
            for (int j = 0; j < typeDataList.Count; j++)
            {
                int dataIndex = j;
                UIRestaurantAdminStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[dataIndex]));
                slot.SetFrame(Rank.Normal1);
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void SubscribeEvents()
    {
        UserInfo.OnChangeStaffHandler += OnChangeStaffEvent;
        UserInfo.OnGiveStaffHandler += UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler += UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler += UpdateUIOptimized;
        UserInfo.OnChangeStaffSkinHandler += UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler += UpdateUIOptimized;
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _uiSkin.Hide();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        // 데이터 설정과 UI 업데이트를 한 번에 처리
        SetStaffDataOptimized(EquipStaffType.Manager);

        TweenData tween = _animeUI.TweenScale(_tmpScale, _showDuration, _showTweenMode);
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
        _uiSkin.Hide();
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = _tmpScale;

        TweenData tween = _animeUI.TweenScale(_hideScale, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }

    public void ShowUIStaff(ERestaurantFloorType floorType, EquipStaffType type)
    {
        _uiRestaurantAdmin.MainUISetActive(false);
        _uiRestaurantAdmin.ShowStaffTab();
        _uiNav.Push("UIStaff");
        _currentFloorType = floorType;
        SetStaffDataOptimized(type);
    }

    // 최적화된 데이터 설정 메서드
    private void SetStaffDataOptimized(EquipStaffType type)
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
        _currentTypeDataList = StaffDataManager.Instance.GetSortStaffDataList(type);
        _typeText.text = Utility.StaffTypeStringConverter(type);

        // 프리뷰와 UI 업데이트를 함께 처리
        SetStaffPreviewOptimized();
        UpdateUIOptimized();
    }

    private void SetStaffPreviewOptimized()
    {
        StaffData equipStaffData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _currentFloorType, _currentType);
        StaffData previewData = equipStaffData ?? (_currentTypeDataList.Count > 0 ? _currentTypeDataList[0] : null);
        _previewStaffData = previewData;
        
        _uiStaffPreview.SetData(_currentFloorType, _currentType, previewData);
    }

    // 대폭 최적화된 UpdateUI (정렬 없이 기존 순서대로)
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiStaffPreview.UpdateUI();

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        int dataCount = _currentTypeDataList.Count;
        
        // 기존 리스트 순서대로 슬롯 처리
        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];
            
            slot.gameObject.SetActive(true);
            slot.EquipGroupSetActive(false);
            slot.transform.SetSiblingIndex(i);

            bool isGiven = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data);
            StaffSkinData equipSkinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, data);
            slot.SetFrame(equipSkinData != null ? equipSkinData.Rank : Rank.Normal1);
             
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

    // 간소화된 ProcessEquippedSlot
    private void ProcessEquippedSlot(StaffData data, UIRestaurantAdminStaffSlot slot)
    {
        ERestaurantFloorType floorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data);
        StaffSkinData skinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, data);
        Sprite thumbnailSprite = skinData == null ? (data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite) : skinData.ThumbnailSprite;
        string name = skinData == null ? data.Name : skinData.Name;
        // 장착 상태 처리
        if (UserInfo.IsEquipStaff(UserInfo.CurrentStage, data))
        {
            slot.EquipGroupSetActive(true);
            EquipStaffType equipType = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data);
            slot.SetEquipText(Utility.StaffTypeStringConverter(equipType));
        }

        // 배치 상태에 따른 UI 설정
        string statusText = floorType switch
        {
            ERestaurantFloorType.Floor1 or ERestaurantFloorType.Floor2 or ERestaurantFloorType.Floor3 => "배치중",
            _ => "배치하기"
        };

        if (floorType <= ERestaurantFloorType.Floor3)
        {
            slot.SetUse(thumbnailSprite, name, statusText, floorType);
        }
        else
        {
            slot.SetOperate(thumbnailSprite, name, statusText);
        }
    }

    private void ProcessUnequippedSlot(StaffData data, UIRestaurantAdminStaffSlot slot)
    {
        StaffSkinData skinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, data);
        Sprite thumbnailSprite = skinData == null ? (data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite) : skinData.ThumbnailSprite;
        string name = skinData == null ? data.Name : skinData.Name;
        if (!UserInfo.IsScoreValid(data))
        {
            slot.SetLowReputation(thumbnailSprite, name, data.BuyScore.ToString());
            return;
        }

        string priceText = data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice);

        if (data.MoneyType == MoneyType.Gold)
        {
            if (UserInfo.IsMoneyValid(data))
                slot.SetEnoughPrice(thumbnailSprite, name, priceText, data.MoneyType);
            else
                slot.SetNotEnoughMoneyPrice(thumbnailSprite, name, priceText);
        }
        else if (data.MoneyType == MoneyType.Dia)
        {
            if (UserInfo.IsDiaValid(data))
                slot.SetEnoughPrice(thumbnailSprite, name, priceText, data.MoneyType);
            else
                slot.SetNotEnoughDiaPrice(thumbnailSprite, name, priceText);
        }
        else
        {
            slot.SetEnoughPrice(thumbnailSprite, name, priceText, data.MoneyType);
        }
    }

    private void ChangeStaffData(int dir)
    {
        int currentIndex = (int)_currentType;
        int maxIndex = (int)EquipStaffType.Length;
        int newIndex = ((currentIndex + dir) % maxIndex + maxIndex) % maxIndex;
        
        SetStaffDataOptimized((EquipStaffType)newIndex);
    }

    private void OnEquipButtonClicked(ERestaurantFloorType floorType, EquipStaffType type, StaffData data)
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _equipSound);
        UserInfo.SetEquipStaff(UserInfo.CurrentStage, floorType, type, data);
        
        // 전체 재설정 대신 업데이트만
        UpdateUIOptimized();
    }

    private void OnUsingButtonClicked(ERestaurantFloorType floorType, StaffData data)
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _dequipSound);
        UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, floorType, data);
        
        // 전체 재설정 대신 업데이트만
        UpdateUIOptimized();
    }

    private void OnBuyButtonClicked(StaffData data)
    {
        if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data.Id))
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

        UserInfo.GiveStaff(UserInfo.CurrentStage, data);
        PopupManager.Instance.ShowDisplayText("새로운 직원을 채용했어요!");
    }

    private void OnUpgradeButtonClicked(StaffData data)
    {
        _uiStaffUpgrade.SetData(data);
        _uiNav.Push("UIStaffUpgrade");
    }

    public void OnChangeStaffEvent(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (!gameObject.activeInHierarchy)
            return;

        UpdateUIOptimized();
    }

    private void OnSlotClicked(StaffData data)
    {
        _previewStaffData = data;
        _uiStaffPreview.SetData(_currentFloorType, _currentType, data);
    }

    private void OnShowSkinButtonClicked()
    {
        if (_previewStaffData == null)
            return;

        _uiSkin.Show(_previewStaffData);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= OnChangeStaffEvent;
        UserInfo.OnGiveStaffHandler -= UpdateUIOptimized;
        UserInfo.OnChangeMoneyHandler -= UpdateUIOptimized;
        UserInfo.OnChangeScoreHandler -= UpdateUIOptimized;
        UserInfo.OnChangeStaffSkinHandler -= UpdateUIOptimized;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUIOptimized;
    }
}
