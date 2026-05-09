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
    private string _productId; // Google Play 상품 ID (Dia 구매 시 사용)

    /// <summary>골드 구매 슬롯 초기화 (다이아로 골드 구매)</summary>
    public void Init(UIPayment uIPayment, MoneyType moneyType, int value, int price)
    {
        _uIPayment = uIPayment;
        _moneyType = moneyType;
        _value = value;
        _price = price;
        _productId = null;

        _valueText.SetText(Utility.ConvertToMoney(value));
        string symbol = moneyType == MoneyType.Dia ? "₩ " : string.Empty;
        _priceText.SetText($"{symbol}{Utility.ConvertToMoney(price)}");

        _button.onClick.AddListener(OnButtonClicked);
    }

    /// <summary>다이아 구매 슬롯 초기화 (실제 결제 IAP)</summary>
    public void Init(UIPayment uIPayment, MoneyType moneyType, int value, int price, string productId)
    {
        Init(uIPayment, moneyType, value, price);
        _productId = productId;
    }

    private void OnButtonClicked()
    {
        DebugLog.Log($"[UIPaymentSlot] OnButtonClicked : {_moneyType}, {_value}, {_price}, productId={_productId}");

        // 실제 결제 (Google Play IAP)
        if (_moneyType == MoneyType.Dia && !string.IsNullOrEmpty(_productId))
        {
            IAPManager.Instance.BuyProduct(_productId, diaAmount =>
            {
                _uIPayment.AddDia(diaAmount);
            });
            return;
        }

        // 다이아로 골드 구매
        _uIPayment.OnSlotButtonClicked(_moneyType, _value, _price);
    }
}
