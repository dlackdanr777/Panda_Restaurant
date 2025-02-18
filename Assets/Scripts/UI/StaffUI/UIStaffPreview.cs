using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIStaffSelectSlot _selectGroup;
    [SerializeField] private UIImageAndText _levelGroup;
    [SerializeField] private UIImageAndText _scoreGroup;
    [SerializeField] private UIImageAndText _addTipPercentGroup;
    [SerializeField] private UIImageAndImage _scoreSignGroup;
    [SerializeField] private UIImageAndImage _effectSignGroup;
    [SerializeField] private UIStaffSkillEffect _skillEffectGroup;
    [SerializeField] private GameObject _effetGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndTwoText _usingButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    [SerializeField] private GameObject _equipButtonGroup;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _1fEquipButton;
    [SerializeField] private UIButtonAndText _2fEquipButton;
    [SerializeField] private UIButtonAndText _3fEquipButton;

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
    private Action<ERestaurantFloorType, StaffData> _onEquipButtonClicked;
    private StaffData _currentData;
    private ERestaurantFloorType _currentType;



    public void Init(Action<ERestaurantFloorType, StaffData> onEquipButtonClicked, Action<StaffData> onBuyButtonClicked, Action<StaffData> onUpgradeButtonClicked)
    {
        _selectGroup.Init();
        _skillEffectGroup.Init();
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        _buyButton.AddListener(OnBuyButtonClicked);
        _usingButton.AddListener(OnUsingButtonClicked);
        _notEnoughMoneyButton.AddListener(OnBuyButtonClicked);
        _equipButton.AddListener(OnEquipButtonClicked);

        _1fEquipButton.AddListener(On1FloorEquipButtonClicked);
        _2fEquipButton.AddListener(On2FloorEquipButtonClicked);
        _3fEquipButton.AddListener(On3FloorEquipButtonClicked);

        UserInfo.OnChangeFloorHandler += OnFloorChangeEvent;
        UserInfo.OnUpgradeStaffHandler += UpdateUI;
        OnFloorChangeEvent();
    }


    public void SetData(ERestaurantFloorType type, StaffData data)
    {
        _currentData = data;
        _currentType = type;
        _selectGroup.SetData(data);
        _levelGroup.gameObject.SetActive(false);
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _equipButtonGroup.gameObject.SetActive(false);
        OnFloorChangeEvent();

        if (data == null)
        {
            _scoreGroup.gameObject.SetActive(false);
            _skillEffectGroup.gameObject.SetActive(false);
            _effetGroup.gameObject.SetActive(false);
            _addTipPercentGroup.gameObject.SetActive(false);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _scoreGroup.gameObject.SetActive(true);
            _skillEffectGroup.gameObject.SetActive(true);
            _effetGroup.gameObject.SetActive(true);
            _addTipPercentGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        int level = UserInfo.IsGiveStaff(data) ? UserInfo.GetStaffLevel(data) : 1;

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _scoreGroup.SetText("<color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.GetAddScore(level).ToString() + "</color> 점 증가");
        _addTipPercentGroup.SetText("메뉴별 팁 <color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.GetAddTipMul(level) + "%</color> 증가");
        _skillEffectGroup.SetData(data);

        StaffData equipData = UserInfo.GetEquipStaff(type, StaffDataManager.Instance.GetStaffType(data));
        int equipDataLevel = equipData == null ? 1 : UserInfo.IsGiveStaff(equipData) ? UserInfo.GetStaffLevel(equipData) : 1;
        if (equipData == null)
        {
            _scoreSignGroup.Image1SetActive(false);
            _scoreSignGroup.Image2SetActive(false);
            _effectSignGroup.Image1SetActive(false);
            _effectSignGroup.Image2SetActive(false);
        }
        else
        {
            if (equipData.GetAddScore(equipDataLevel) < data.GetAddScore(level))
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(true);
            }
            else if (data.GetAddScore(level) < equipData.GetAddScore(equipDataLevel))
            {
                _scoreSignGroup.Image1SetActive(true);
                _scoreSignGroup.Image2SetActive(false);
            }
            else
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(false);
            }

            if (equipData.GetAddTipMul(equipDataLevel) < data.GetAddTipMul(level))
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(true);
            }
            else if (data.GetAddTipMul(level) < equipData.GetAddTipMul(equipDataLevel))
            {
                _effectSignGroup.Image1SetActive(true);
                _effectSignGroup.Image2SetActive(false);
            }
            else
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(false);
            }
        }

        if (UserInfo.IsGiveStaff(data))
        {
            _levelGroup.gameObject.SetActive(true);
            _levelGroup.SetText(data.UpgradeEnable(level) ? "Lv." + level : "Lv.Max");
            ERestaurantFloorType furnitureFloorType = UserInfo.GetEquipStaffFloorType(data);
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
                _scoreGroup.SetText("???");
                _addTipPercentGroup.SetText("???");
                _skillEffectGroup.SetData(null);
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(false);
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(false);
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
        SetData(_currentType, _currentData);
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

        _equipButtonGroup.gameObject.SetActive(false);
        _onEquipButtonClicked?.Invoke(type, _currentData);
    }


    private void OnEquipButtonClicked()
    {
        bool value = !_equipButtonGroup.activeSelf;
        _equipButtonGroup.gameObject.SetActive(value);
        _equipButton.SetText(value ? "취소" : "배치하기");
        OnFloorChangeEvent();
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

    private void OnFloorChangeEvent()
    {
        if (!_equipButtonGroup.activeInHierarchy)
            return;

        ERestaurantFloorType currentFloorType = UserInfo.CurrentFloor;
        if (currentFloorType == ERestaurantFloorType.Floor3)
        {
            _3fEquipButton.gameObject.SetActive(true);
            _2fEquipButton.gameObject.SetActive(true);
            _1fEquipButton.gameObject.SetActive(true);
        }
        else if (currentFloorType == ERestaurantFloorType.Floor2)
        {
            _3fEquipButton.gameObject.SetActive(false);
            _2fEquipButton.gameObject.SetActive(true);
            _1fEquipButton.gameObject.SetActive(true);
        }
        else if (currentFloorType == ERestaurantFloorType.Floor1)
        {
            _3fEquipButton.gameObject.SetActive(false);
            _2fEquipButton.gameObject.SetActive(false);
            _1fEquipButton.gameObject.SetActive(true);
        }
        else
        {
            _3fEquipButton.gameObject.SetActive(false);
            _2fEquipButton.gameObject.SetActive(false);
            _1fEquipButton.gameObject.SetActive(false);
        }
    }

    private void OnUsingButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        ERestaurantFloorType floorType = UserInfo.GetEquipStaffFloorType(_currentData);
        _onEquipButtonClicked?.Invoke(floorType, null);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeFloorHandler -= OnFloorChangeEvent;
        UserInfo.OnUpgradeStaffHandler -= UpdateUI;
    }
}
