using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchenSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Outline _outLine;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _lockImgae;
    [SerializeField] private GameObject _useImage;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;

    private Action<KitchenUtensilData> _onButtonClicked;

    public void Init(Action<KitchenUtensilData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }


    public void SetUse(KitchenUtensilData data)
    {
        _useImage.SetActive(true);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);
        _lockImgae.SetActive(false);

        _image.sprite = data.ThumbnailSPrite;
        _image.color = new Color(1, 1, 1, 1);
        _nameText.text = data.Name;
        _text.text = "사용중";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetOperate(KitchenUtensilData data)
    {
        _operateImage.SetActive(true);
        _useImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);
        _lockImgae.SetActive(false);

        _image.sprite = data.ThumbnailSPrite;
        _image.color = new Color(1, 1, 1, 1);
        _nameText.text = data.Name;
        _text.text = "사용";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetEnoughMoney(KitchenUtensilData data)
    {
        _enoughMoneyImage.SetActive(true);
        _alarmImage.SetActive(true);
        _lockImgae.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.ThumbnailSPrite;
        _image.color = new Color(0, 0, 0, 1);
        _nameText.text = data.Name;
        _text.text = Utility.ConvertToNumber(data.BuyMinPrice);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(KitchenUtensilData data)
    {
        _enoughMoneyImage.SetActive(true);
        _lockImgae.SetActive(true);
        _alarmImage.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.ThumbnailSPrite;
        _image.color = new Color(0, 0, 0, 1);
        _nameText.text = data.Name;
        _text.text = Utility.ConvertToNumber(data.BuyMinPrice);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetOutline(bool value)
    {
        _outLine.enabled = value;
    }
}
