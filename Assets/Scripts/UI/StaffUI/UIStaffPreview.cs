using JetBrains.Annotations;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
    [SerializeField] private UITextAndText _coolTimeGroup;
    [SerializeField] private UITextAndText _equipTextGroup;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private GameObject _effectGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndTwoText _usingButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIEquipButtonGroup _equipButtonGroup;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Image _notEnoughImage;
    [SerializeField] private Image _buyImage;
    [SerializeField] private Sprite _notEnoughMoneySprite;
    [SerializeField] private Sprite _notEnoughDiaSprite;
    [SerializeField] private Sprite _buyMoneySprite;
    [SerializeField] private Sprite _buyDiaSprite;
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<StaffData> _onBuyButtonClicked;
    private Action<ERestaurantFloorType, EquipStaffType, StaffData> _onEquipButtonClicked;
    private Action<ERestaurantFloorType, StaffData> _onUsingButtonClicked;
    private StaffData _currentData;
    private ERestaurantFloorType _currentFloor;
    private EquipStaffType _equipStaffType;

    public void Init(Action<ERestaurantFloorType, EquipStaffType, StaffData> onEquipButtonClicked, Action<ERestaurantFloorType, StaffData> onUsingButtonClicked, Action<StaffData> onBuyButtonClicked, Action<StaffData> onUpgradeButtonClicked)
    {
        _selectGroup.Init();
        _equipButtonGroup.Init(On1FloorEquipButtonClicked, On2FloorEquipButtonClicked, On3FloorEquipButtonClicked, OnEquipCancelButtonClicked);
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
        _onUsingButtonClicked = onUsingButtonClicked;
        _buyButton.AddListener(OnBuyButtonClicked);
        _usingButton.AddListener(OnUsingButtonClicked);
        _notEnoughMoneyButton.AddListener(OnBuyButtonClicked);
        _equipButton.AddListener(OnEquipButtonClicked);

        UserInfo.OnUpgradeStaffHandler += UpdateUI;
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
        _equipButtonGroup.HideNoAnime();

        if (data == null)
        {
            _effectGroup.gameObject.SetActive(false);
            _staffEffectGroup.gameObject.SetActive(false);
            _skillGroup.gameObject.SetActive(false);
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
            _coolTimeGroup.gameObject.SetActive(true);
            _effectGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        int level = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, data) : 1;

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);

        _staffEffectGroup.SetText2(Utility.GetStaffEffectDescription(data));
        _skillGroup.SetText2(Utility.GetStaffSkillDescription(data));
        _coolTimeGroup.SetText2(data.Skill.Cooldown + "s");
        _descriptionText.SetText(data.Description);


        StaffData equipData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, floor, _equipStaffType);
        int equipDataLevel = equipData == null ? 1 : UserInfo.IsGiveStaff(UserInfo.CurrentStage, equipData) ? UserInfo.GetStaffLevel(UserInfo.CurrentStage, equipData) : 1;

        if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data))
        {
            _levelGroup.gameObject.SetActive(true);
            _levelGroup.SetText(data.UpgradeEnable(level) ? "Lv." + level : "Lv.Max");
            ERestaurantFloorType furnitureFloorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data);
        
            if(UserInfo.IsEquipStaff(UserInfo.CurrentStage, data))
            {
                _equipTextGroup.gameObject.SetActive(true);
                EquipStaffType equipType = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data);
                _equipTextGroup.SetText1(Utility.StaffTypeStringConverter(equipType));
            }

            switch (furnitureFloorType)
            {
                case ERestaurantFloorType.Floor1:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("배치중");
                    _usingButton.SetText2("1f");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor2:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("배치중");
                    _usingButton.SetText2("2f");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor3:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("배치중");
                    _usingButton.SetText2("3f");
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
            if (!UserInfo.IsScoreValid(data))
            {
                _selectGroup.ImageColor = Utility.GetColor(ColorType.None);
                _scoreButton.gameObject.SetActive(true);
                _scoreButton.SetText(data.BuyScore.ToString());
                _selectGroup.SetSprite(_questionMarkSprite);
                _staffEffectGroup.SetText2("???");
                _skillGroup.SetText2("???");
                _coolTimeGroup.SetText2("???");
                _descriptionText.SetText("???");
                return;
            }

            _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
            MoneyType moneyType = data.MoneyType;
            int price = data.BuyPrice;

            if (moneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(price))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughImage.sprite = _notEnoughMoneySprite;
                _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                return;
            }

            else if (moneyType == MoneyType.Dia && !UserInfo.IsDiaValid(price))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughImage.sprite = _notEnoughDiaSprite;
                _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                return;
            }


            _buyButton.gameObject.SetActive(true);
            _buyButton.SetText(data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
            _buyImage.sprite = moneyType == MoneyType.Gold ? _buyMoneySprite : _buyDiaSprite;
        }
    }
    

    public void UpdateUI()
    {
        SetData(_currentFloor, _equipStaffType, _currentData);
    }

    private void OnBuyButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }


    private void OnEquipEvent(ERestaurantFloorType type)
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _equipButtonGroup.HideNoAnime();
        _equipButton.gameObject.SetActive(false);

        if(UserInfo.IsEquipStaff(UserInfo.CurrentStage, _currentData))
        {
            _onUsingButtonClicked?.Invoke(_currentFloor, _currentData);
        }
        else
        {
            _onEquipButtonClicked?.Invoke(type, _equipStaffType, _currentData);
        }

    }


    private void OnEquipButtonClicked()
    {
        _equipButtonGroup.Show();
        _equipButton.gameObject.SetActive(false);
    }

    private void OnEquipCancelButtonClicked()
    {
        _equipButtonGroup.Hide(() => _equipButton.gameObject.SetActive(true));
    }


    private void On1FloorEquipButtonClicked()
    {
        OnEquipEvent(ERestaurantFloorType.Floor1);
    }

    private void On2FloorEquipButtonClicked()
    {
        OnEquipEvent(ERestaurantFloorType.Floor2);
    }

    private void On3FloorEquipButtonClicked()
    {
        OnEquipEvent(ERestaurantFloorType.Floor3);
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
    }
}
