using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGachaItemView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _star1;
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;
    [SerializeField] private GameObject _star4;
    [SerializeField] private GameObject _star5;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _addScoreText;
    [SerializeField] private TextMeshProUGUI _addScoreDescription;
    [SerializeField] private TextMeshProUGUI _tipPerMinuteText;
    [SerializeField] private TextMeshProUGUI _tipPerMinuteDescription;

    private GachaItemData _data;


    public void SetData(GachaItemData data)
    {
        if (data == _data)
            return;

        if (data == null)
        {
            _itemImage.gameObject.SetActive(false);
            _addScoreText.gameObject.SetActive(false);
            _tipPerMinuteText.gameObject.SetActive(false);
            SetStar(GachaItemRank.Length);
            _itemNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _addScoreDescription.text = string.Empty;
            _tipPerMinuteDescription.text = string.Empty;
            _data = null;
            return;
        }

        _data = data;
        _itemImage.gameObject.SetActive(true);
        _addScoreText.gameObject.SetActive(true);
        _tipPerMinuteText.gameObject.SetActive(true);
        SetStar(data.GachaItemRank);

        _itemImage.sprite = data.Sprite;
        _itemImage.TweenStop();
        _itemImage.color = new Color(_itemImage.color.r, _itemImage.color.g, _itemImage.color.b, 0);
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);

        _itemNameText.text = data.Name;
        _descriptionText.text = data.Description;
        _addScoreDescription.text = Utility.ConvertToNumber(data.AddScore);
        _tipPerMinuteDescription.text = Utility.ConvertToNumber(data.TipPerMinute);
    }

    public void ChoiceView()
    {
        if (_data == null)
            return;

        _itemImage.sprite = _data.Sprite;
        _itemImage.TweenStop();
        _itemImage.color = new Color(_itemImage.color.r, _itemImage.color.g, _itemImage.color.b, 0);
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
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
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Normal2:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Rare:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(true);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Unique:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(false);
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(true);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Special:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(true);
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(true);
                break;

            default:
                _star1.SetActive(false);
                _star2.SetActive(false);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

        }

    }
}
