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

    private Vector2 _originalSize;
    private Vector3 _originalPosition;

    public void Init()
    {
        _originalSize = _itemImage.rectTransform.sizeDelta;
        _originalPosition = _itemImage.rectTransform.anchoredPosition;
    }


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
        {
            _specialFrame.gameObject.SetActive(true);
            SetScaleImage(1);
        }

        else if (data is GatecrasherCustomerData)
        {
            _gatecrasherFrame.gameObject.SetActive(true);
            SetScaleImage(1);
        }

        else
        {
            _normalFrame.gameObject.SetActive(true);
            SetScaleImage(1.3f, 12);
        }

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

    public void SetScaleImage(float scale, float offset = 0)
    {
        Vector2 newSize = _originalSize * scale;
        float heightDifference = (newSize.y - _originalSize.y) / 2;

        Vector3 newPosition = new Vector3(
            _originalPosition.x,
            _originalPosition.y + heightDifference - offset,
            _originalPosition.z
        );

        _itemImage.rectTransform.sizeDelta = newSize;
        _itemImage.rectTransform.anchoredPosition = newPosition;
    }
}
