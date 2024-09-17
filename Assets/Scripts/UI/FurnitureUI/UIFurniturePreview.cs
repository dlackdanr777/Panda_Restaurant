using JetBrains.Annotations;
using System;
using UnityEngine;

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
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<FurnitureData> _onBuyButtonClicked;
    private Action<FurnitureData> _onEquipButtonClicked;
    private FurnitureData _currentData;

    public void Init(Action<FurnitureData> onEquipButtonClicked, Action<FurnitureData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
    }


    public void SetData(FurnitureData data)
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
        _scoreGroup.SetText("<color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.AddScore.ToString() + "</color> ¡° ¡ı∞°");

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setGroup.SetText1(setData.Name);
        _setGroup.SetText2(setData != null ? Utility.GetSetEffectDescription(setData) : string.Empty);

        string effectText = Utility.GetEquipEffectDescription(data.EffectData);
        _tipPerMinuteGroup.SetText(effectText);

        FurnitureData equipData = UserInfo.GetEquipFurniture(data.Type);
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

            if (equipData.EffectData.EffectValue < data.EffectData.EffectValue)
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(true);
            }
            else if (data.EffectData.EffectValue < equipData.EffectData.EffectValue)
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


        if (UserInfo.IsEquipFurniture(data))
        {
            _usingButton.gameObject.SetActive(true);
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
        }
        else
        {
            if (UserInfo.IsGiveFurniture(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
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
                if (!UserInfo.IsMoneyValid(data))
                {
                    _notEnoughMoneyButton.gameObject.SetActive(true);
                    _notEnoughMoneyButton.RemoveAllListeners();
                    _notEnoughMoneyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                    _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(data.BuyPrice));
                    return;
                }

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToMoney(data.BuyPrice));
            }
        }
    }

    public void UpdateUI()
    {
        SetData(_currentData);
    }
}
