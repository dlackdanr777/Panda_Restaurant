using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UISlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Outline _outLine;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _alarmImage;
    [SerializeField] private GameObject _lockImgae;
    [SerializeField] private UIImageAndText _useImage;
    [SerializeField] private UIImageAndText _operateImage;
    [SerializeField] private UIImageAndText _priceImage;
    [SerializeField] private UIImageAndText _scoreImage;


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
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = new Color(1, 1, 1, 1);
        _nameText.text = name;
        _useImage?.SetText(text);
    }


    public void SetOperate(Sprite sprite, string name, string text)
    {
        _operateImage?.gameObject.SetActive(true);
        _useImage?.gameObject.SetActive(false);
        _priceImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _alarmImage?.SetActive(false);
        _lockImgae?.SetActive(false);

        _image.sprite = sprite;
        _image.color = new Color(1, 1, 1, 1);
        _nameText.text = name;
        _operateImage?.SetText(text);
    }


    public void SetEnoughMoney(Sprite sprite, string name, string text)
    {
        _priceImage?.gameObject.SetActive(true);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _scoreImage?.gameObject.SetActive(false);
        _alarmImage?.SetActive(true);
        _lockImgae?.SetActive(false);


        _image.sprite = sprite;
        _image.color = new Color(0, 0, 0, 1);
        _nameText.text = name;
        _priceImage.SetText(text);
    }


    public void SetLowReputation(Sprite sprite, string name, string text)
    {
        _scoreImage?.gameObject.SetActive(true);
        _priceImage?.gameObject.SetActive(false);
        _useImage?.gameObject.SetActive(false);
        _operateImage?.gameObject.SetActive(false);
        _lockImgae?.SetActive(true);
        _alarmImage?.SetActive(false);

        _image.sprite = sprite;
        _image.color = new Color(0, 0, 0, 1);
        _nameText.text = name;
        _scoreImage?.SetText(text);
    }


    public void SetOutline(bool value)
    {
        _outLine.enabled = value;
    }
}
