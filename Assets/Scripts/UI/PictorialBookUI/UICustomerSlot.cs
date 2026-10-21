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
    private bool _initialized;

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _originalSize = _itemImage.rectTransform.sizeDelta;
        _originalPosition = _itemImage.rectTransform.anchoredPosition;
    }


    public void SetData(CustomerData data, bool refresh = true)
    {
        if (data == null)
        {
            _data = null;
            if (_itemImage.gameObject.activeSelf) _itemImage.gameObject.SetActive(false);
            _alarm.ChangeAlarmId(string.Empty);
            return;
        }

        _data = data;
        _alarm.ChangeAlarmId(data.Id);
        if (refresh) UpdateUI();
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
        if (_data == null) return;
        CustomerSkinData skinData = UserInfo.GetEquipCustomerSkin(_data.Id);
        Sprite sprite = skinData == null ? _data.ThumbnailSprite : skinData.Sprite;
        Color color = UserInfo.GetCustomerEnableState(_data) && 0 < UserInfo.GetVisitedCustomerCount(_data.Id)
            ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        if (!_itemImage.gameObject.activeSelf) _itemImage.gameObject.SetActive(true);
        if (_itemImage.sprite != sprite) _itemImage.sprite = sprite;
        if (_itemImage.color != color) _itemImage.color = color;
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

        if (_itemImage.rectTransform.sizeDelta != newSize) _itemImage.rectTransform.sizeDelta = newSize;
        if (_itemImage.rectTransform.anchoredPosition != (Vector2)newPosition)
            _itemImage.rectTransform.anchoredPosition = newPosition;
    }


    private void UpdateFrame(CustomerData data, SkinData skinData)
    {
        GameObject frame = data is SpecialCustomerData ? _specialFrame
            : data is GatecrasherCustomerData ? _gatecrasherFrame
            : skinData == null ? _normalSkinFrame
            : skinData.Rank == Rank.Rare ? _rareSkinFrame
            : skinData.Rank == Rank.Unique ? _uniqueSkinFrame
            : skinData.Rank == Rank.Special ? _specialSkinFrame : _normalSkinFrame;
        SetFrameActive(_normalSkinFrame, frame);
        SetFrameActive(_rareSkinFrame, frame);
        SetFrameActive(_uniqueSkinFrame, frame);
        SetFrameActive(_specialSkinFrame, frame);
        SetFrameActive(_specialFrame, frame);
        SetFrameActive(_gatecrasherFrame, frame);
        if (data is SpecialCustomerData || data is GatecrasherCustomerData) SetScaleImage(1);
        else SetScaleImage(1.3f, 12);
    }

    private static void SetFrameActive(GameObject target, GameObject selected)
    {
        bool active = target == selected;
        if (target.activeSelf != active) target.SetActive(active);
    }
}
