using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UIFurniturePreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIImageAndText _selectGroup;
    [SerializeField] private UIImageAndText _scoreGroup;
    [SerializeField] private UIImageAndText _tipPerMinuteGroup;
    [SerializeField] private UIImageAndImage _scoreSignGroup;
    [SerializeField] private UIImageAndImage _effectSignGroup;
    [SerializeField] private UITextAndText _setGroup;
    [SerializeField] private GameObject _effetGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Image _notEnoughImage;
    [SerializeField] private Image _buyImage;
    [SerializeField] private Sprite _notEnoughMoneySprite;
    [SerializeField] private Sprite _notEnoughDiaSprite;
    [SerializeField] private Sprite _buyMoneySprite;
    [SerializeField] private Sprite _buyDiaSprite;
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<FurnitureData> _onBuyButtonClicked;
    private Action<ERestaurantFloorType, FurnitureData> _onEquipButtonClicked;
    private FurnitureData _currentData;
    private ERestaurantFloorType _currentType;


    public void Init(Action<ERestaurantFloorType, FurnitureData> onEquipButtonClicked, Action<FurnitureData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        _buyButton.AddListener(OnBuyButtonClicked);
        _usingButton.AddListener(OnUsingButtonClicked);
        _notEnoughMoneyButton.AddListener(OnBuyButtonClicked);
        _equipButton.AddListener(OnEquipButtonClicked);
    }


    public void SetData(ERestaurantFloorType type, FurnitureData data)
    {
        _currentData = data;
        _currentType = type;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        if (data == null)
        {
            _scoreGroup.gameObject.SetActive(false);
            _setGroup.gameObject.SetActive(false);
            _effetGroup.gameObject.SetActive(false);
            _tipPerMinuteGroup.gameObject.SetActive(false);
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
        _tipPerMinuteGroup.SetText(effectText);

        FurnitureData equipData = UserInfo.GetEquipFurniture(type, data.Type);
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


        if (UserInfo.IsEquipFurniture(type, data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("��ġ��");
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
        }
        else
        {
            if (UserInfo.IsGiveFurniture(data))
            {
                ERestaurantFloorType furnitureFloorType = UserInfo.GetEquipFurnitureFloorType(data);
                switch (furnitureFloorType)
                {
                    case ERestaurantFloorType.Floor1:
                        _usingButton.gameObject.SetActive(true);
                        _usingButton.SetText("1�� ��ġ��");
                        _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                        break;

                    case ERestaurantFloorType.Floor2:
                        _usingButton.gameObject.SetActive(true);
                        _usingButton.SetText("2�� ��ġ��");
                        _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                        break;

                    case ERestaurantFloorType.Floor3:
                        _usingButton.gameObject.SetActive(true);
                        _usingButton.SetText("3�� ��ġ��");
                        _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                        break;

                    case ERestaurantFloorType.Length:
                        _equipButton.gameObject.SetActive(true);
                        _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                        break;

                    case ERestaurantFloorType.Error:
                        _equipButton.gameObject.SetActive(true);
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
    }

    public void UpdateUI()
    {
        SetData(_currentType, _currentData);
    }


    private void OnBuyButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }

    private void OnEquipButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        _onEquipButtonClicked?.Invoke(_currentType, _currentData);
    }

    private void OnUsingButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("���� �����Ͱ� �������� �ʽ��ϴ�.");
            return;
        }

        ERestaurantFloorType floorType = UserInfo.GetEquipFurnitureFloorType(_currentData);
        _onEquipButtonClicked?.Invoke(floorType, null);
    }
}
