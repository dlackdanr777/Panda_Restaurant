using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UICustomerSlot : MonoBehaviour
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _itemImage;

    private CustomerData _data;


    public void SetData(CustomerData data)
    {
        if(data == null)
        {
            _data = null;
            _itemImage.gameObject.SetActive(false);
            return;
        }

        _data = data;
        _itemImage.sprite = data.Sprite;
        _itemImage.color = UserInfo.IsCustomerVisitEnabled(data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
    }


    public void SetButtonEvent(UnityAction<CustomerData> action)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(() =>
        {
            if (_data == null)
                return;

            action?.Invoke(_data);
        });
    }

    public void UpdateUI()
    {
        _itemImage.color = UserInfo.IsCustomerVisitEnabled(_data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
    }
}
