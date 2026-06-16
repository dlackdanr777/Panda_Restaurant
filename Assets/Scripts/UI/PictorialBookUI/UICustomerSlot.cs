using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UICustomerSlot : MonoBehaviour
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private UINotificationMessage _alarm;
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _normalSkinFrame;
    [SerializeField] private GameObject _rareSkinFrame;
    [SerializeField] private GameObject _uniqueSkinFrame;
    [SerializeField] private GameObject _specialSkinFrame;
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
        if (data == null)
        {
            _data = null;
            _itemImage.gameObject.SetActive(false);
            _alarm.ChangeAlarmId(string.Empty);
            return;
        }

        _data = data;
        CustomerSkinData skinData = UserInfo.GetEquipCustomerSkin(data.Id);
        _itemImage.sprite = skinData == null ? data.ThumbnailSprite : skinData.Sprite;
        _itemImage.color = UserInfo.GetCustomerEnableState(data) && 0 < UserInfo.GetVisitedCustomerCount(data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        _alarm.ChangeAlarmId(data.Id);

        UpdateFrame(data, skinData);
    }


    public void SetButtonEvent(UnityAction<CustomerData> action)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(() =>
        {
            if (_data == null)
                return;

            UserInfo.RemoveNotification(_data.Id);
            action?.Invoke(_data);
        });
    }

    public void UpdateUI()
    {
        CustomerSkinData skinData = UserInfo.GetEquipCustomerSkin(_data.Id);
        _itemImage.sprite = skinData == null ?  _data.ThumbnailSprite : skinData.Sprite;
        _itemImage.color = UserInfo.GetCustomerEnableState(_data) && 0 < UserInfo.GetVisitedCustomerCount(_data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        UpdateFrame(_data, skinData);
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


    private void UpdateFrame(CustomerData data, SkinData skinData)
    {
           _normalSkinFrame.SetActive(false);
        _rareSkinFrame.SetActive(false);
        _uniqueSkinFrame.SetActive(false);
        _specialSkinFrame.SetActive(false);
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
            if (skinData == null)
            {
                _normalSkinFrame.gameObject.SetActive(true);
            }
            else
            {
                switch (skinData.Rank)
                {
                    case Rank.Normal1:
                    case Rank.Normal2:
                        _normalSkinFrame.gameObject.SetActive(true);
                        break;

                    case Rank.Rare:
                        _rareSkinFrame.gameObject.SetActive(true);
                        break;
                    case Rank.Unique:
                        _uniqueSkinFrame.gameObject.SetActive(true);
                        break;
                    case Rank.Special:
                        _normalSkinFrame.SetActive(false);
                        _specialSkinFrame.gameObject.SetActive(true);
                        break;
                }
            }
            SetScaleImage(1.3f, 12);
        }
    }
}
