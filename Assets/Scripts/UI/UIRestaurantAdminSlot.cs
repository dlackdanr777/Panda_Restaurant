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
    [SerializeField] private UIImageAndText _useImage;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _priceImage;
    [SerializeField] private UIImageAndText _notEnoughPriceImage;
    [SerializeField] private UIImageAndText _notEnoughDiaPriceImage;
    [SerializeField] private UIImageAndText _needItemImage;
    [SerializeField] private UIImageAndText _scoreImage;
    [SerializeField] private Sprite _questionMarkSprite;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _moneyPriceSprite;
    [SerializeField] private Sprite _diaPriceSprite;


    public void Init(UnityAction onButtonClicked)
    {
        _button.onClick.AddListener(onButtonClicked);
    }


    public void SetUse(Sprite sprite, string name, string text)
    {
        _useImage?.gameObject.SetActive(true);
        _operateImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage?.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = Utility.GetColor(ColorType.Give);
        _nameText.text = name;
        _useImage?.SetText(text);
    }


    public void SetOperate(Sprite sprite, string name, string text)
    {
        _operateImage?.gameObject.SetActive(true);
        _useImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _notEnoughPriceImage?.gameObject.SetActive(false);
        _needItemImage?.gameObject.SetActive(false);
        _notEnoughDiaPriceImage?.gameObject.SetActive(false);
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = Utility.GetColor(ColorType.Give);
        _nameText.text = name;
        _operateImage?.SetText(text);
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
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.None);
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
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(true);

        _image.color = Utility.GetColor(ColorType.None);
        _image.sprite = sprite;
        _nameText.text = name;

        GachaItemData data = ItemManager.Instance.GetGachaItemData(needItemId);
        _needItemImage?.SetText("아이템 보유");
        _needItemImage.SetSprite(data.Sprite);
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
        _lockImgae?.SetActive(false);
        _alarmImage?.SetActive(false);

        _image.color = Utility.GetColor(ColorType.None);
        _image.sprite = sprite;
        _nameText.text = name;
    }
}
