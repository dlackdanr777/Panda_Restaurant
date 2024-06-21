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
    [SerializeField] private GameObject _useImage;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;
    [SerializeField] private GameObject _lowReputationImage;

    private StaffData _staffData;

    public void SetUse(StaffData data, Action<StaffData> OnButtonClicked)
    {
        _useImage.SetActive(true);
        _operateImage.SetActive(false);
        _lowReputationImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _staffData = data;
        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "사용중";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => OnButtonClicked(data));
    }

    public void SetOperate(StaffData data, UnityAction<StaffData> OnButtonClicked)
    {
        _operateImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _useImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _staffData = data;
        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "보유중";

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => OnButtonClicked(data));
    }


    public void SetEnoughMoney(StaffData data, UnityAction<StaffData> OnButtonClicked)
    {
        _enoughMoneyImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);

        _staffData = data;
        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);

        int price = data.MoneyData.Price;
        if (1000 <= price)
        {
            int div = price / 1000;
            _text.text = div + "K";
        }
        else
        {
            _text.text = price.ToString();
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => OnButtonClicked(data));
    }

    public void SetLowReputation(StaffData data, UnityAction<StaffData> OnButtonClicked)
    {
        _lowReputationImage.SetActive(true);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _staffData = data;
        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = data.BuyMinScore.ToString();

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => OnButtonClicked(data));
    }
}
