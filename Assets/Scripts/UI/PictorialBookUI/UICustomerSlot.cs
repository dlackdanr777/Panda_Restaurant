using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UICustomerSlot : MonoBehaviour
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _normalFrame;
    [SerializeField] private GameObject _gatecrasherFrame;
    [SerializeField] private GameObject _specialFrame;

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

        _normalFrame.SetActive(false);
        _specialFrame.SetActive(false);
        _gatecrasherFrame.SetActive(false);

        if (data is SpecialCustomerData)
            _specialFrame.gameObject.SetActive(true);

        else if (data is GatecrasherCustomerData)
            _gatecrasherFrame.gameObject.SetActive(true);

        else
            _normalFrame.gameObject.SetActive(true);
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
