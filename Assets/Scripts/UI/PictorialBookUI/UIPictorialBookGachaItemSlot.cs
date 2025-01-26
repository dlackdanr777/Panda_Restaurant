using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIPictorialBookGachaItemSlot : MonoBehaviour
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private UINotificationMessage _alarm;
    [SerializeField] private Image _itemImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private UIItemStar _itemStar;



    private GachaItemData _data;


    public void SetData(GachaItemData data)
    {
        if(data == null)
        {
            _data = null;
            _itemImage.gameObject.SetActive(false);
            SetStar(GachaItemRank.Length);
            _alarm.ChangeAlarmId(string.Empty);
            return;
        }

        _itemImage.color = UserInfo.IsGiveGachaItem(data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);

        if (_data == data)
            return;

        _data = data;
        _itemImage.sprite = data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
        SetStar(data.GachaItemRank);
        _alarm.ChangeAlarmId(data.Id);
    }


    public void SetButtonEvent(UnityAction<GachaItemData> action)
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
        if (_data == null)
            return;

        _itemImage.color = UserInfo.IsGiveGachaItem(_data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
    }


    private void SetStar(GachaItemRank rank)
    {
        _itemStar.SetStar(rank);
        switch (rank)
        {
            case GachaItemRank.Normal1:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

                case GachaItemRank.Normal2:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Rare:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(true);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Unique:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(true);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Special:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(true);
                break;

            default:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

        }

    }
}
