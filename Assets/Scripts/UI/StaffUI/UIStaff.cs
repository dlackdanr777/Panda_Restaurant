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
        _uiStaffPreview.Init(OnEquipButtonClicked, OnUsingButtonClicked, OnUpgradeButtonClicked);
        // Ïä¨Î°Ø ÎØ∏Î¶¨ ÏÉùÏÑ± ÏµúÏ†ÅÌôî
        InitializeSlots();

        // Ïù¥Î≤§Ìä∏ Íµ¨ÎèÖ
        SubscribeEvents();

        // Ï¥àÍ∏∞ ÏÑ§Ï†ï
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
            EquipStaffType staffType = (EquipStaffType)i;
            
            // ∏µÁ √˛¿« µ•¿Ã≈Õ∏¶ »Æ¿Œ«œø© √÷¥Î ΩΩ∑‘ ºˆ ∞ËªÍ
            int maxSlotCount = 0;
            for (int f = 0; f < (int)ERestaurantFloorType.Length; f++)
            {
                ERestaurantFloorType floorType = (ERestaurantFloorType)f;
                List<StaffData> floorDataList = StaffDataManager.Instance.GetStaffDataList(staffType, floorType);
                if (floorDataList.Count > maxSlotCount)
                    maxSlotCount = floorDataList.Count;
            }

            _slots[i] = new List<UIRestaurantAdminStaffSlot>(maxSlotCount);
            
            for (int j = 0; j < maxSlotCount; j++)
            {
                UIRestaurantAdminStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => { }); // √ ±‚»≠ Ω√ø°¥¬ ∫Û æ◊º«
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
        UpdateFloorUI();
        SetStaffDataOptimized(type);
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
    }

            
    private void SetStaffDataOptimized(EquipStaffType type)
    {

        if (_currentType != type && _slots[(int)_currentType] != null)
        {
            var currentSlots = _slots[(int)_currentType];
            for (int i = 0; i < currentSlots.Count; i++)
            {
                currentSlots[i].gameObject.SetActive(false);
            }
        }

        _currentType = type;
        _currentTypeDataList = StaffDataManager.Instance.GetSortStaffDataList(type, _currentFloorType);
        _typeText.text = Utility.StaffTypeStringConverter(type);

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

    // ÎåÄÌè≠ ÏµúÏ†ÅÌôîÎêú UpdateUI (Ï†ïÎ†¨ ÏóÜÏù¥ Í∏∞Ï°¥ ÏàúÏÑúÎåÄÎ°ú)
    private void UpdateUIOptimized()
    {
        if (!gameObject.activeInHierarchy || _currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiStaffPreview.UpdateUI();

        int slotsIndex = (int)_currentType;
        var currentSlots = _slots[slotsIndex];
        int dataCount = _currentTypeDataList.Count;
        

        r (int i = 0; i < currentSlots.Count; i++)
        {
            currentSlots[i].gameObject.SetActive(false);
        }
        

        for (int i = 0; i < dataCount; i++)
        {
            var data = _currentTypeDataList[i];
            var slot = currentSlots[i];
            
            // ΩΩ∑‘ ≈¨∏Ø ¿Ã∫•∆Æ ¿Áº≥¡§
            slot.Init(() => OnSlotClicked(data));
            slot.gameObject.SetActive(true);
            slot.EquipGroupSetActive(false);
            slot.transform.SetSiblingIndex(i);

            bool isGiven = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data);
            slot.SetFrame(data.Rank);
             
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

    // Í∞ÑÏÜåÌôîÎêú ProcessEquippedSlot
    private void ProcessEquippedSlot(StaffData data, UIRestaurantAdminStaffSlot slot)
    {
        ERestaurantFloorType floorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data);
        Sprite thumbnailSprite = data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite;
        string name = data.Name;
        slot.SetOwnedVisual();
        // Ïû•Ï∞© ÏÉÅÌÉú Ï≤òÎ¶¨
        if (UserInfo.IsEquipStaff(UserInfo.CurrentStage, data))
        {
            slot.EquipGroupSetActive(true);
            EquipStaffType equipType = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data);
            slot.SetEquipText(Utility.StaffTypeStringConverter(equipType));
        }

        // Î∞∞Ïπò ÏÉÅÌÉúÏóê Îî∞Î•∏ UI ÏÑ§Ï†ï
        string statusText = floorType switch
        {
            ERestaurantFloorType.Floor1 or ERestaurantFloorType.Floor2 or ERestaurantFloorType.Floor3 => "Î∞∞ÏπòÏ§ë",
            _ => "Î∞∞ÏπòÌïòÍ∏∞"
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
        Sprite thumbnailSprite = data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite;
        slot.SetGacha(thumbnailSprite);
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
        
        // Ï†ÑÏ≤¥ Ïû¨ÏÑ§Ï†ï ÎåÄÏã† ÏóÖÎç∞Ïù¥Ìä∏Îßå
        UpdateUIOptimized();
    }

    private void OnUsingButtonClicked(ERestaurantFloorType floorType, StaffData data)
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _dequipSound);
        UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, floorType, data);
        
        // Ï†ÑÏ≤¥ Ïû¨ÏÑ§Ï†ï ÎåÄÏã† ÏóÖÎç∞Ïù¥Ìä∏Îßå
        UpdateUIOptimized();
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
