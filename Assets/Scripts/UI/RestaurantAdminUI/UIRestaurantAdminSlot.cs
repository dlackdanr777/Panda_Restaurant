using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRestaurantAdminSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _lockImgae;
    [SerializeField] private UITextAndText _useImage;
    [SerializeField] private Image _useImageSprite;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _priceImage;
    [SerializeField] private UIImageAndText _notEnoughPriceImage;
    [SerializeField] private UIImageAndText _notEnoughDiaPriceImage;
    [SerializeField] private UIImageAndText _needItemImage;
    [SerializeField] private UIImageAndText _scoreImage;
    [SerializeField] private UIImageAndText _unlockImage;
    [SerializeField] private Sprite _questionMarkSprite;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _moneyPriceSprite;
    [SerializeField] private Sprite _diaPriceSprite;


    public void Init(UnityAction onButtonClicked)
    {
        _button.onClick.AddListener(onButtonClicked);
    }


    public void SetUse(Sprite sprite, string name, string text, ERestaurantFloorType floor)
    {
        SetPlacementState(true);
        SetPortrait(sprite, name, Utility.GetColor(ColorType.Give));
        if (_useImage != null)
        {
            if (_useImage.Text1.text != text) _useImage.SetText1(text);
            string floorText = Utility.GetFloorStrEngByType(floor);
            if (_useImage.Text2.text != floorText) _useImage.SetText2(floorText);
        }
        Color color = Utility.GetFloorColor(floor);
        if (_useImageSprite.color != color) _useImageSprite.color = color;
    }


    public void SetOperate(Sprite sprite, string name, string text)
    {
        SetOperate(sprite, name, text, Utility.GetColor(ColorType.Give));
    }

    protected void SetOperate(Sprite sprite, string name, string text, Color portraitColor)
    {
        SetPlacementState(false);
        SetPortrait(sprite, name, portraitColor);
        if (_operateImage != null && _operateImage.Text.text != text) _operateImage.SetText(text);
    }

    private void SetPlacementState(bool inUse)
    {
        SetActive(_useImage, inUse);
        SetActive(_operateImage, !inUse);
        SetActive(_priceImage, false);
        SetActive(_scoreImage, false);
        SetActive(_notEnoughPriceImage, false);
        SetActive(_needItemImage, false);
        SetActive(_notEnoughDiaPriceImage, false);
        SetActive(_unlockImage, false);
        if (_alarmImage != null && _alarmImage.activeSelf) _alarmImage.SetActive(false);
        if (_lockImgae != null && _lockImgae.activeSelf) _lockImgae.SetActive(false);
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null && component.gameObject.activeSelf != active)
            component.gameObject.SetActive(active);
    }

    private void SetPortrait(Sprite sprite, string name, Color color)
    {
        if (_image.sprite != sprite) _image.sprite = sprite;
        if (_image.color != color) _image.color = color;
        if (_nameText.text != name) _nameText.text = name;
    }


    public void SetEnoughPrice(Sprite sprite, string name, string text, MoneyType type)
    {
        _priceImage?.gameObject.SetActive(true);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage?.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _alarmImage?.SetActive(true);
        _lockImgae?.SetActive(false);
        _priceImage.SetSprite(type == MoneyType.Gold ? _moneyPriceSprite : _diaPriceSprite);

        _image.sprite = sprite;
        _image.color = Utility.GetColor(ColorType.NoGive);
        _nameText.text = name;
        _priceImage.SetText(text);
    }


    public void SetNotEnoughMoneyPrice(Sprite sprite, string name, string text)
    {
        _notEnoughPriceImage?.gameObject.SetActive(true);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _needItemImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = Utility.GetColor(ColorType.NoGive);
        _nameText.text = name;
        _notEnoughPriceImage.SetText(text);
    }


    public void SetNotEnoughDiaPrice(Sprite sprite, string name, string text)
    {
        _notEnoughDiaPriceImage?.gameObject.SetActive(true);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _needItemImage.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = Utility.GetColor(ColorType.NoGive);
        _nameText.text = name;
        _notEnoughDiaPriceImage.SetText(text);
    }


    public void SetLowReputation(Sprite sprite, string name, string text)
    {
        _scoreImage?.gameObject.SetActive(true);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _lockImgae?.SetActive(true);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.None);
        _image.sprite = _questionMarkSprite;
        _nameText.text = name;
        _scoreImage?.SetText(text);
    }

    public void SetNeedItem(Sprite sprite, string name, string needItemId)
    {
        _needItemImage.gameObject.SetActive(true);
        _scoreImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.NoGive);
        _image.sprite = sprite;
        _nameText.text = name;

        GachaItemData data = ItemManager.Instance.GetGachaItemData(needItemId);
        _needItemImage?.SetText("아이템 필요");
        _needItemImage.SetSprite(data.Sprite);
    }

    public void SetMiniGame(Sprite sprite, string name, string needItemId)
    {
        _needItemImage.gameObject.SetActive(true);
        _scoreImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(true);

        _image.color = Utility.GetColor(ColorType.NoGive);
        _image.sprite = sprite;
        _nameText.text = name;

        GachaItemData data = ItemManager.Instance.GetGachaItemData(needItemId);
        _needItemImage?.SetText("아이템 보유");
        _needItemImage.SetSprite(data.Sprite);
    }


    public void SetLock(Sprite sprite, string name)
    {
        _scoreImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(true);
        _lockImgae?.SetActive(true);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.None);
        _image.sprite = _questionMarkSprite;
        _nameText.text = name;
    }


    public void SetNone(Sprite sprite, string name)
    {
        _scoreImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _unlockImage.gameObject.SetActive(false);
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.None);
        _image.sprite = sprite;
        _nameText.text = name;
    }
}
