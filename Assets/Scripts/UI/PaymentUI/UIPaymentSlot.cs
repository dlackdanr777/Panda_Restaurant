using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPaymentSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _priceText;



    private UIPayment _uIPayment;
    private MoneyType _moneyType;
    private int _value;
    private int _price;

    public void Init(UIPayment uIPayment, MoneyType moneyType, int value, int price)
    {
        _uIPayment = uIPayment;
        _moneyType = moneyType;
        _value = value;
        _price = price;

        _valueText.SetText(Utility.ConvertToMoney(value));
        string symbol = moneyType == MoneyType.Dia ? "₩ " : string.Empty;
        _priceText.SetText($"{symbol}{Utility.ConvertToMoney(price)}");

        _button.onClick.AddListener(OnButtonClicked);
    }
    
    private void OnButtonClicked()
    {
        DebugLog.Log($"[UIPaymentSlot] OnButtonClicked : {_moneyType}, {_value}, {_price}");
        _uIPayment.OnSlotButtonClicked(_moneyType, _value, _price);
    }
}
