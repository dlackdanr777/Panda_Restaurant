using UnityEngine;

public class UIPaymentDia : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIPaymentAdSlot _dia01Slot;
    [SerializeField] private UIPaymentSlot _dia02Slot;
    [SerializeField] private UIPaymentSlot _dia03Slot;
    [SerializeField] private UIPaymentSlot _dia04Slot;
    [SerializeField] private UIPaymentSlot _dia05Slot;
    [SerializeField] private UIPaymentSlot _dia06Slot;
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
    
    public void Show()
    {
        _dia01Slot.Show();
    }

    public void Init(UIPayment uIPayment)
    {
        _dia01Slot.Init(uIPayment, MoneyType.Dia);
        _dia02Slot.Init(uIPayment, MoneyType.Dia, 12,  1100);
        _dia03Slot.Init(uIPayment, MoneyType.Dia, 65, 5500);
        _dia04Slot.Init(uIPayment, MoneyType.Dia, 140, 11000);
        _dia05Slot.Init(uIPayment, MoneyType.Dia, 450, 33000);
        _dia06Slot.Init(uIPayment, MoneyType.Dia, 800, 55000);
    }
}
