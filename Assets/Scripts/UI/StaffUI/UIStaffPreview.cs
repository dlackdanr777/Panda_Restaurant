using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIStaffSelectSlot _selectGroup;
    [SerializeField] private UIImageAndText _levelGroup;
    [SerializeField] private UITextAndText _staffEffectGroup;
    [SerializeField] private UITextAndText _skillGroup;
    [SerializeField] private UITextAndText _skillTimeGroup;
    [SerializeField] private UITextAndText _coolTimeGroup;
    [SerializeField] private UITextAndText _equipTextGroup;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private GameObject _effectGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UI3LayerButton _usingButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;
    [SerializeField] private UIButtonAndText _equipButton;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Image _notEnoughImage;
    [SerializeField] private Image _buyImage;
    [SerializeField] private Sprite _notEnoughMoneySprite;
    [SerializeField] private Sprite _notEnoughDiaSprite;
    [SerializeField] private Sprite _buyMoneySprite;
    [SerializeField] private Sprite _buyDiaSprite;
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<ERestaurantFloorType, EquipStaffType, StaffData> _onEquipButtonClicked;
    private Action<ERestaurantFloorType, StaffData> _onUsingButtonClicked;
    private StaffData _currentData;
    private ERestaurantFloorType _currentFloor;
    private EquipStaffType _equipStaffType;
    private RectTransform _actionButtonRoot;

    private const float UnownedActionButtonX = 70f;
    private const float OwnedActionButtonX = 200f;
    private const float UnownedInfoFontSize = 20f;
    private const float UnownedDescriptionFontSize = 42f;
    private const float OwnedDescriptionFontSize = 35f;
    private const float UnownedGachaFontSize = 38f;

    public void Init(Action<ERestaurantFloorType, EquipStaffType, StaffData> onEquipButtonClicked, Action<ERestaurantFloorType, StaffData> onUsingButtonClicked, Action<StaffData> onUpgradeButtonClicked)
    {
        _selectGroup.Init();
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);
        _onEquipButtonClicked = onEquipButtonClicked;
        _onUsingButtonClicked = onUsingButtonClicked;
        _usingButton.AddListener(OnUsingButtonClicked);
        _equipButton.AddListener(OnEquipButtonClicked);
        _actionButtonRoot = _buyButton.transform.parent as RectTransform;

        UserInfo.OnUpgradeStaffHandler += UpdateUI;
        UserInfo.OnChangeStaffSkinHandler += UpdateUI;
    }


    public void SetData(ERestaurantFloorType floor, EquipStaffType type, StaffData data)
    {
        _currentData = data;
        _currentFloor = floor;
        _equipStaffType = type;
        _selectGroup.SetData(data);
        _levelGroup.gameObject.SetActive(false);
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _equipTextGroup.gameObject.SetActive(false);

        if (data == null)
        {
            AlignActionButtons(false);
            SetTimeIconVisibility(false);
            _selectGroup.ClearRank();
            _effectGroup.gameObject.SetActive(false);
            _staffEffectGroup.gameObject.SetActive(false);
            _skillGroup.gameObject.SetActive(false);
            if (_skillTimeGroup != null)
                _skillTimeGroup.gameObject.SetActive(false);
            _coolTimeGroup.gameObject.SetActive(false);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            _descriptionText.SetText(string.Empty);
            return;
        }
        else
        {
            _staffEffectGroup.gameObject.SetActive(true);
            _skillGroup.gameObject.SetActive(true);
            if (_skillTimeGroup != null)
                _skillTimeGroup.gameObject.SetActive(true);
            _coolTimeGroup.gameObject.SetActive(true);
            _effectGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        bool isOwned = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data);
        AlignActionButtons(isOwned);
        int savedLevel = isOwned ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;
        int level = data.GetRuntimeLevel(savedLevel);
        Sprite thumbnailSprite = data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite;

        _selectGroup.SetRank(data.Rank);
        _selectGroup.SetSprite(thumbnailSprite);

        if (isOwned)
        {
            ApplyOwnedTextStyle();
            SetTimeIconVisibility(true);
            _selectGroup.SetText(data.Name);
            _staffEffectGroup.SetText2(Utility.GetStaffEffectDescription(data, level));
            _skillGroup.SetText2(Utility.GetStaffSkillDescription(data));
            SetRuntimeSkillTimes(data, level);
            _descriptionText.SetText(data.Description);
            _levelGroup.gameObject.SetActive(true);
            _levelGroup.SetText(data.IsMaxLevel(level) ? "Lv.Max" : "Lv." + level);
            ERestaurantFloorType furnitureFloorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data);

            if (UserInfo.IsEquipStaff(UserInfo.CurrentStage, data))
            {
                _equipTextGroup.gameObject.SetActive(true);
                EquipStaffType equipType = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data);
                _equipTextGroup.SetText1(Utility.StaffTypeStringConverter(equipType));
            }

            switch (furnitureFloorType)
            {
                case ERestaurantFloorType.Floor1:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetCenterText("배치중");
                    _usingButton.SetLeftText(Utility.GetFloorStrEngByType(ERestaurantFloorType.Floor1));
                    _usingButton.SetBackgroundColor(Utility.GetFloorColor(ERestaurantFloorType.Floor1));
                    _usingButton.SetLeftImageColor(Utility.GetFloorBoldColor(ERestaurantFloorType.Floor1));
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor2:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetCenterText("배치중");
                    _usingButton.SetLeftText(Utility.GetFloorStrEngByType(ERestaurantFloorType.Floor2));
                    _usingButton.SetBackgroundColor(Utility.GetFloorColor(ERestaurantFloorType.Floor2));
                    _usingButton.SetLeftImageColor(Utility.GetFloorBoldColor(ERestaurantFloorType.Floor2));
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor3:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetCenterText("배치중");
                    _usingButton.SetLeftText(Utility.GetFloorStrEngByType(ERestaurantFloorType.Floor3));
                    _usingButton.SetBackgroundColor(Utility.GetFloorColor(ERestaurantFloorType.Floor3));
                    _usingButton.SetLeftImageColor(Utility.GetFloorBoldColor(ERestaurantFloorType.Floor3));
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Length:
                    _equipButton.gameObject.SetActive(true);
                    _equipButton.SetText("배치하기");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);

                    break;

                case ERestaurantFloorType.Error:
                    _equipButton.gameObject.SetActive(true);
                    _equipButton.SetText("배치하기");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;
            }
        }
        else
        {
            ApplyUnownedTextStyle();
            SetTimeIconVisibility(false);
            _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
            _selectGroup.SetText("???");
            _staffEffectGroup.SetText2("???");
            _skillGroup.SetText2("???");
            if (_skillTimeGroup != null)
                _skillTimeGroup.SetText2("???");
            _coolTimeGroup.SetText2("???");
            _descriptionText.SetText("???");
            _buyButton.gameObject.SetActive(true);
            _buyButton.SetText("뽑기");
        }
    }

    private void ApplyUnownedTextStyle()
    {
        SetFixedInfoFont(_staffEffectGroup.Text2, UnownedInfoFontSize);
        SetFixedInfoFont(_skillGroup.Text2, UnownedInfoFontSize);
        if (_skillTimeGroup != null)
            SetFixedInfoFont(_skillTimeGroup.Text2, UnownedInfoFontSize);
        SetFixedInfoFont(_coolTimeGroup.Text2, UnownedInfoFontSize);

        _descriptionText.enableAutoSizing = false;
        _descriptionText.fontSize = UnownedDescriptionFontSize;
        _descriptionText.alignment = TextAlignmentOptions.Center;

        if (_buyButton.Text != null)
        {
            _buyButton.Text.enableAutoSizing = true;
            _buyButton.Text.fontSizeMin = 30f;
            _buyButton.Text.fontSizeMax = UnownedGachaFontSize;
            _buyButton.Text.fontSize = UnownedGachaFontSize;
            _buyButton.Text.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ApplyOwnedTextStyle()
    {
        SetAutoInfoFont(_staffEffectGroup.Text2, 17f, 22f);
        SetAutoInfoFont(_skillGroup.Text2, 17f, 22f);
        if (_skillTimeGroup != null)
            SetAutoInfoFont(_skillTimeGroup.Text2, 15f, 35f);
        SetAutoInfoFont(_coolTimeGroup.Text2, 15f, 35f);

        _descriptionText.enableAutoSizing = true;
        _descriptionText.fontSizeMin = 20f;
        _descriptionText.fontSizeMax = OwnedDescriptionFontSize;
        _descriptionText.fontSize = OwnedDescriptionFontSize;
        _descriptionText.alignment = TextAlignmentOptions.Center;
    }

    private static void SetFixedInfoFont(TextMeshProUGUI text, float fontSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
    }

    private static void SetAutoInfoFont(TextMeshProUGUI text, float minSize, float maxSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.fontSize = maxSize;
        text.alignment = TextAlignmentOptions.Center;
    }

    private void SetTimeIconVisibility(bool visible)
    {
        SetTimeIconVisibility(_skillTimeGroup, visible);
        SetTimeIconVisibility(_coolTimeGroup, visible);
    }

    private static void SetTimeIconVisibility(UITextAndText group, bool visible)
    {
        if (group == null || group.Text2 == null)
            return;

        Transform icon = group.Text2.transform.Find("Plus Image");
        if (icon != null)
            icon.gameObject.SetActive(visible);
    }

    private void AlignActionButtons(bool isOwned)
    {
        if (_actionButtonRoot == null)
            return;

        Vector2 position = _actionButtonRoot.anchoredPosition;
        position.x = isOwned ? OwnedActionButtonX : UnownedActionButtonX;
        _actionButtonRoot.anchoredPosition = position;
    }

    private void SetRuntimeSkillTimes(StaffData data, int level)
    {
        SkillBase skill = data.Skill;
        if (skill == null)
        {
            if (_skillTimeGroup != null)
                _skillTimeGroup.SetText2("—");
            _coolTimeGroup.SetText2("—");
            return;
        }

        if (_skillTimeGroup != null && skill.Duration > 0f)
        {
            StaffGroupType groupType = StaffDataManager.Instance.GetStaffGroupType(data);
            float permanentDurationBonusRate = GameManager.Instance.GetStaffSkillTimeMul(groupType);
            int duration = StaffSkillTimeCalculator.CalculateDurationSeconds(
                skill.Duration,
                level,
                permanentDurationBonusRate);
            _skillTimeGroup.SetText2(duration + "s");
        }
        else if (_skillTimeGroup != null)
        {
            _skillTimeGroup.SetText2("—");
        }

        if (skill.Cooldown > 0f)
        {
            int cooldown = StaffSkillTimeCalculator.CalculateCooldownSeconds(skill.Cooldown, level);
            _coolTimeGroup.SetText2(cooldown + "s");
        }
        else
        {
            _coolTimeGroup.SetText2("—");
        }
    }
    

    public void UpdateUI()
    {
        if(!gameObject.activeInHierarchy)
        {return;}

        SetData(_currentFloor, _equipStaffType, _currentData);
    }

    private void OnEquipButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        if(UserInfo.IsEquipStaff(UserInfo.CurrentStage, _currentData))
        {
            _onUsingButtonClicked?.Invoke(_currentFloor, _currentData);
        }
        else
        {
            _onEquipButtonClicked?.Invoke(_currentFloor, _equipStaffType, _currentData);
        }
    }


    private void OnUsingButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        ERestaurantFloorType floorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, _currentData);
        _onUsingButtonClicked?.Invoke(floorType, _currentData);
    }

    private void OnDestroy()
    {
        UserInfo.OnUpgradeStaffHandler -= UpdateUI;
        UserInfo.OnChangeStaffSkinHandler -= UpdateUI;
    }
}
