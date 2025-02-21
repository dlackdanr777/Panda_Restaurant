using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchenPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIImageAndText _selectGroup;
    [SerializeField] private UIImageAndText _scoreGroup;
    [SerializeField] private UIImageAndText _tipPerMinuteGroup;
    [SerializeField] private UIImageAndText _cookSpeedGroup;
    [SerializeField] private UIImageAndImage _scoreSignGroup;
    [SerializeField] private UIImageAndImage _effectSignGroup;
    [SerializeField] private UITextAndText _setGroup;
    [SerializeField] private GameObject _effetGroup;

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

    private Action<KitchenUtensilData> _onBuyButtonClicked;
    private Action<ERestaurantFloorType, KitchenUtensilData> _onEquipButtonClicked;
    private KitchenUtensilData _currentData;
    private ERestaurantFloorType _floorType;


    public void Init(Action<ERestaurantFloorType, ShopData> onEquipButtonClicked, Action<ShopData> onBuyButtonClicked)
    {
        _equipButtonGroup.Init(On1FloorEquipButtonClicked, On2FloorEquipButtonClicked, On3FloorEquipButtonClicked, OnEquipCancelButtonClicked);

        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        _buyButton.AddListener(OnBuyButtonClicked);
        _usingButton.AddListener(OnUsingButtonClicked);
        _notEnoughMoneyButton.AddListener(OnBuyButtonClicked);
        _equipButton.AddListener(OnEquipButtonClicked);
    }


    public void SetData(ERestaurantFloorType type, KitchenUtensilData data)
    {
        _currentData = data;
        _floorType = type;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _equipButtonGroup.HideNoAnime();

        if (data == null)
        {
            _scoreGroup.gameObject.SetActive(false);
            _setGroup.gameObject.SetActive(false);
            _effetGroup.gameObject.SetActive(false);
            _tipPerMinuteGroup.gameObject.SetActive(false);
            _cookSpeedGroup.gameObject.SetActive(false);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _scoreGroup.gameObject.SetActive(true);
            _setGroup.gameObject.SetActive(true);
            _effetGroup.gameObject.SetActive(true);
            _tipPerMinuteGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _scoreGroup.SetText("<color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.AddScore.ToString() + "</color> �� ����");

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setGroup.SetText1(setData.Name);
        _setGroup.SetText2(setData != null ? Utility.GetSetEffectDescription(setData) : string.Empty);

        string effectText = Utility.GetEquipEffectDescription(data.EquipEffectType, data.EffectValue);
        if (data.EquipEffectType == EquipEffectType.AddCookSpeed)
        {
            _cookSpeedGroup.gameObject.SetActive(true);
            _tipPerMinuteGroup.gameObject.SetActive(false);
            _cookSpeedGroup.SetText(effectText);
        }
        else
        {
            _cookSpeedGroup.gameObject.SetActive(false);
            _tipPerMinuteGroup.gameObject.SetActive(true);
            _tipPerMinuteGroup.SetText(effectText);
        }

        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _floorType, data.Type);
        if (equipData == null)
        {
            _scoreSignGroup.Image1SetActive(false);
            _scoreSignGroup.Image2SetActive(false);
            _effectSignGroup.Image1SetActive(false);
            _effectSignGroup.Image2SetActive(false);
        }
        else
        {
            if (equipData.AddScore < data.AddScore)
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(true);
            }
            else if (data.AddScore < equipData.AddScore)
            {
                _scoreSignGroup.Image1SetActive(true);
                _scoreSignGroup.Image2SetActive(false);
            }
            else
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(false);
            }

            if (equipData.EffectValue < data.EffectValue)
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(true);
            }
            else if (data.EffectValue < equipData.EffectValue)
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

        if (UserInfo.IsGiveKitchenUtensil(UserInfo.CurrentStage, data))
        {
            ERestaurantFloorType furnitureFloorType = UserInfo.GetEquipKitchenUtensilFloorType(UserInfo.CurrentStage, data);
            switch (furnitureFloorType)
            {
                case ERestaurantFloorType.Floor1:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("��ġ��");
                    _usingButton.SetText2("1f");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor2:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("��ġ��");
                    _usingButton.SetText2("2f");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Floor3:
                    _usingButton.gameObject.SetActive(true);
                    _usingButton.SetText1("��ġ��");
                    _usingButton.SetText2("3f");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Length:
                    _equipButton.gameObject.SetActive(true);
                    _equipButton.SetText("��ġ�ϱ�");
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                    break;

                case ERestaurantFloorType.Error:
                    _equipButton.gameObject.SetActive(true);
                    _equipButton.SetText("��ġ�ϱ�");
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
                _tipPerMinuteGroup.SetText("???");
                _setGroup.SetText1("???");
                _setGroup.SetText2("???");
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
                _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
                return;
            }

            else if (moneyType == MoneyType.Dia && !UserInfo.IsDiaValid(price))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughImage.sprite = _notEnoughDiaSprite;
                _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
                return;
            }


            _buyButton.gameObject.SetActive(true);
            _buyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
            _buyImage.sprite = moneyType == MoneyType.Gold ? _buyMoneySprite : _buyDiaSprite;
        }
    }
    

    public void UpdateUI()
    {
        SetData(_floorType, _currentData);
    }


    private void OnBuyButtonClicked()
    {
        if(_currentData == null)
        {
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }

    private void OnEquipEvent(ERestaurantFloorType type)
    {
        if (_currentData == null)
        {
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        _equipButtonGroup.HideNoAnime();
        _equipButton.gameObject.SetActive(false);
        _onEquipButtonClicked?.Invoke(type, _currentData);
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
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        ERestaurantFloorType floorType = UserInfo.GetEquipKitchenUtensilFloorType(UserInfo.CurrentStage, _currentData);
        _onEquipButtonClicked?.Invoke(floorType, null);
    }
}
