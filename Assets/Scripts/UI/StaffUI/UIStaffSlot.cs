using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _lockImage;
    [SerializeField] private UIImageAndText _useImage;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _buyMinScoreImage;
    [SerializeField] private UIImageAndText _buyMinMoneyImage;

    private Action<StaffData> _onButtonClicked;

    public void Init(Action<StaffData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetUse(StaffData data)
    {
        _useImage.gameObject.SetActive(true);
        _operateImage.gameObject.SetActive(false);
        _buyMinMoneyImage.gameObject.SetActive(false);
        _buyMinScoreImage.gameObject.SetActive(false);
        _alarmImage.SetActive(false);
        _lockImage.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _image.color = new Color(1, 1, 1, 1);
        _useImage.SetText("사용중");

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetOperate(StaffData data)
    {
        _operateImage.gameObject.SetActive(true);
        _useImage.gameObject.SetActive(false);
        _buyMinMoneyImage.gameObject.SetActive(false);
        _buyMinScoreImage.gameObject.SetActive(false);
        _alarmImage.SetActive(false);
        _lockImage.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _image.color = new Color(1, 1, 1, 1);
        _operateImage.SetText("사용");

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetEnoughMoney(StaffData data)
    {
        _buyMinMoneyImage.gameObject.SetActive(true);
        _alarmImage.SetActive(true);
        _lockImage.SetActive(false);
        _useImage.gameObject.SetActive(false);
        _operateImage.gameObject.SetActive(false);
        _buyMinScoreImage.gameObject.SetActive(false);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _image.color = new Color(0, 0, 0, 1);
        _buyMinMoneyImage.SetText(Utility.ConvertToNumber(data.MoneyData.Price));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(StaffData data)
    {
        _buyMinScoreImage.gameObject.SetActive(true);
        _lockImage.SetActive(true);
        _alarmImage.SetActive(false);
        _useImage.gameObject.SetActive(false);
        _operateImage.gameObject.SetActive(false);
        _buyMinMoneyImage.gameObject.SetActive(true);

        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _image.color = new Color(0, 0, 0, 1);
        _buyMinScoreImage.SetText(Utility.ConvertToNumber(data.BuyMinScore));

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
