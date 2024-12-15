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

    private Action<KitchenUtensilData> _onBuyButtonClicked;
    private Action<KitchenUtensilData> _onEquipButtonClicked;
    private KitchenUtensilData _currentData;

    public void Init(Action<ShopData> onEquipButtonClicked, Action<ShopData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        _buyButton.AddListener(OnBuyEvent);
        _notEnoughMoneyButton.AddListener(OnBuyEvent);
        _equipButton.AddListener(OnEquipEvent);
    }


    public void SetData(KitchenUtensilData data)
    {
        _currentData = data;
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
        _scoreGroup.SetText("<color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.AddScore.ToString() + "</color> 점 증가");

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setGroup.SetText1(setData.Name);
        _setGroup.SetText2(setData != null ? Utility.GetSetEffectDescription(setData) : string.Empty);

        string effectText = Utility.GetEquipEffectDescription(data.EquipEffectType, data.EffectValue);
        if(data.EquipEffectType == EquipEffectType.AddCookSpeed)
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

        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(data.Type);
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


        if (UserInfo.IsEquipKitchenUtensil(data))
        {
            _usingButton.gameObject.SetActive(true);
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
        }
        else
        {
            if (UserInfo.IsGiveKitchenUtensil(data))
            {
                _equipButton.gameObject.SetActive(true);
                _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
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
    }

    public void UpdateUI()
    {
        SetData(_currentData);
    }


    private void OnBuyEvent()
    {
        if(_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }

    private void OnEquipEvent()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _onEquipButtonClicked?.Invoke(_currentData);
    }
}
