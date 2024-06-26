using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIStaffSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _useImage;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;

    private Action<StaffData> _onButtonClicked;

    public void Init(Action<StaffData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
    }

    public void SetUse(StaffData data)
    {
        _useImage.SetActive(true);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "»ç¿ëÁß";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetOperate(StaffData data)
    {
        _operateImage.SetActive(true);
        _useImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _alarmImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = data.Name;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }


    public void SetEnoughMoney(StaffData data)
    {
        _enoughMoneyImage.SetActive(true);
        _alarmImage.SetActive(true);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = Utility.ConvertToNumber(data.MoneyData.Price);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }

    public void SetLowReputation(StaffData data)
    {
        _enoughMoneyImage.SetActive(true);
        _alarmImage.SetActive(true);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = Utility.ConvertToNumber(data.MoneyData.Price);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onButtonClicked(data));
    }
}
