using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookCustomerSkinSlot : MonoBehaviour
{
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;

    private CustomerSkinData _skinData;
    private NormalCustomerData _customerData;


    private Action<NormalCustomerData, CustomerSkinData> _onClickAction;

    public void Init(Action<NormalCustomerData, CustomerSkinData> onClickAction)
    {
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
        _nameText.text = customerData.Name;
    }

    public void SetData(NormalCustomerData customerData, CustomerSkinData skinData)
    {
        if (skinData == null)
        {
            _skinData = null;
            SetData(customerData);
            return;
        }
        _customerData = customerData;
        _skinData = skinData;
        _skinImage.sprite = skinData.Sprite;
        _nameText.text = skinData.Name;
    }

    private void OnButtonClicked()
    {
            _onClickAction?.Invoke(_customerData, _skinData);
    }
}
