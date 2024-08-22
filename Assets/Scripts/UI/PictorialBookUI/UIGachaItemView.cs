using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGachaItemView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _itemImage;
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _itemNameText;

    private GachaItemData _data;


    public void SetData(GachaItemData data)
    {
        if (data == null)
        {
            _itemImage.gameObject.SetActive(false);
            _specialFrameImage.gameObject.SetActive(false);
            _normalFrameImage.gameObject.SetActive(true);
            _rankText.text = string.Empty;
            _itemNameText.text = string.Empty;
            _data = null;
            return;
        }

        _data = data;
        _itemImage.gameObject.SetActive(true);
        _itemImage.sprite = data.Sprite;
        _itemImage.TweenStop();
        _itemImage.color = new Color(_itemImage.color.r, _itemImage.color.g, _itemImage.color.b, 0);
        _itemImage.transform.localScale = Vector3.one;

        _itemImage.TweenAlpha(1, 0.3f, Ease.Constant);
        _itemImage.TweenScale(new Vector3(1.2f, 1.2f, 1.2f), 0.3f, Ease.Spike);
        _itemNameText.text = data.Name;

        if (GachaItemRank.Unique <= data.GachaItemRank)
        {
            _specialFrameImage.gameObject.SetActive(true);
            _normalFrameImage.gameObject.SetActive(false);
        }
        else
        {
            _normalFrameImage.gameObject.SetActive(true);
            _specialFrameImage.gameObject.SetActive(false);
        }

        _rankText.text = Utility.GachaItemRankStringConverter(data.GachaItemRank);
    }
}
