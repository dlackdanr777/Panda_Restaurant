using UnityEngine;

public class UIPaymentGold : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIPaymentAdSlot _coin01Slot;
    [SerializeField] private UIPaymentSlot _coin02Slot;
    [SerializeField] private UIPaymentSlot _coin03Slot;
    [SerializeField] private UIPaymentSlot _coin04Slot;
    [SerializeField] private UIPaymentSlot _coin05Slot;
    [SerializeField] private UIPaymentSlot _coin06Slot;

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
    
    public void Show()
    {
        _coin01Slot.Show();
    }

    public void Init(UIPayment uIPayment)
    {
        _coin01Slot.Init(uIPayment, MoneyType.Gold);
        _coin02Slot.Init(uIPayment, MoneyType.Gold, 100000,  10);
        _coin03Slot.Init(uIPayment, MoneyType.Gold, 600000, 50);
        _coin04Slot.Init(uIPayment, MoneyType.Gold, 1500000, 100);
        _coin05Slot.Init(uIPayment, MoneyType.Gold, 5000000, 300);
        _coin06Slot.Init(uIPayment, MoneyType.Gold, 10000000, 500);
    }
}
