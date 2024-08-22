using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIGachaItemSlot : MonoBehaviour
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _itemImage;
    [SerializeField] private Image _frameImage;
    [SerializeField] private GameObject _star1;
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;
    [SerializeField] private GameObject _star4;
    [SerializeField] private GameObject _star5;

    [SerializeField] private Sprite _normalFrameSprite;
    [SerializeField] private Sprite _specialFrameSprite;

    private GachaItemData _data;


    public void SetData(GachaItemData data)
    {
        if(data == null)
        {
            _data = null;
            _itemImage.gameObject.SetActive(false);
            SetStar(GachaItemRank.Length);
            return;
        }

        _data = data;
        _itemImage.sprite = data.Sprite;
        SetStar(data.GachaItemRank);
    }


    public void SetButtonEvent(UnityAction<GachaItemData> action)
    {
        _slotButton.onClick.RemoveAllListeners();
        _slotButton.onClick.AddListener(() =>
        {
            if (_data == null)
                return;

            action?.Invoke(_data);
        });
    }


    private void SetStar(GachaItemRank rank)
    {
        switch (rank)
        {
            case GachaItemRank.Normal1:
                _star1.SetActive(true);
                _star2.SetActive(false);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _frameImage.sprite = _normalFrameSprite;
                break;

                case GachaItemRank.Normal2:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _frameImage.sprite = _normalFrameSprite;
                break;

            case GachaItemRank.Rare:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _frameImage.sprite = _normalFrameSprite;
                break;

            case GachaItemRank.Unique:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(false);
                _frameImage.sprite = _specialFrameSprite;
                break;

            case GachaItemRank.Special:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(true);
                _frameImage.sprite = _specialFrameSprite;
                break;

            default:
                _star1.SetActive(false);
                _star2.SetActive(false);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _frameImage.sprite = _normalFrameSprite;
                break;

        }

    }
}
