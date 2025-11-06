using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookCustomerSkinSlot : MonoBehaviour
{
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private Image _normalLayer;
    [SerializeField] private Image _rareLayer;
    [SerializeField] private Image _uniqueLayer;
    [SerializeField] private Image[] _specialLayers;

    private CustomerSkinData _skinData;
    private NormalCustomerData _customerData;

    private Vector2 _originalSize;
    private Vector3 _originalPosition;

    private Action<NormalCustomerData, CustomerSkinData> _onClickAction;

    public void Init(Action<NormalCustomerData, CustomerSkinData> onClickAction)
    {
        _originalSize = _skinImage.rectTransform.sizeDelta;
        _originalPosition = _skinImage.rectTransform.anchoredPosition;

        _onClickAction = onClickAction;
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void SetData(NormalCustomerData customerData)
    {
        if (customerData == null)
        {
            _customerData = null;
            _skinImage.sprite = null;
            _nameText.text = string.Empty;
            return;
        }

        _customerData = customerData;
        _skinImage.sprite = customerData.Sprite;
        _skinImage.color = Utility.GetColor(ColorType.None);
        _nameText.text = customerData.Name;
    }

    public void SetData(NormalCustomerData customerData, CustomerSkinData skinData)
    {
        _normalLayer.gameObject.SetActive(false);
        _rareLayer.gameObject.SetActive(false);
        foreach (var layer in _specialLayers)
        {
            layer.gameObject.SetActive(false);
        }
        _uniqueLayer.gameObject.SetActive(false);

        SetScaleImage(1.3f, 13);
        if (skinData == null)
        {
            _normalLayer.gameObject.SetActive(true);
            _skinData = null;
            SetData(customerData);
            return;
        }
        _customerData = customerData;
        _skinData = skinData;
        _skinImage.sprite = skinData.Sprite;
        _skinImage.color = UserInfo.IsGiveCustomerSkin(skinData.Id) ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
        _nameText.text = skinData.Name;

        switch (skinData.Rank)
        {
            case Rank.Normal1:
                _normalLayer.gameObject.SetActive(true);
                break;
            case Rank.Normal2:
                _normalLayer.gameObject.SetActive(true);
                break;
            case Rank.Rare:
                _rareLayer.gameObject.SetActive(true);
                break;
            case Rank.Unique:
                _uniqueLayer.gameObject.SetActive(true);
                break;
            case Rank.Special:
                foreach (var layer in _specialLayers)
                {
                    layer.gameObject.SetActive(true);
                }
                break;
        }
    }

    private void OnButtonClicked()
    {
        _onClickAction?.Invoke(_customerData, _skinData);
    }

    public void SetScaleImage(float scale, float offset = 0)
    {
        Vector2 newSize = _originalSize * scale;
        float heightDifference = (newSize.y - _originalSize.y) / 2;

        Vector3 newPosition = new Vector3(
            _originalPosition.x,
            _originalPosition.y + heightDifference - offset,
            _originalPosition.z
        );

        _skinImage.rectTransform.sizeDelta = newSize;
        _skinImage.rectTransform.anchoredPosition = newPosition;
    }
}
