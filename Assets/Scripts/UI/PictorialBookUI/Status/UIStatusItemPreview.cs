using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatusItemPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIItemStar _itemStar;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectDescriptionText;
    [SerializeField] private UIImageAndText _addScoreLayout;
    [SerializeField] private UIImageAndText _tipPerMinuteLayout;




    private GachaItemData _data;


    public void Init()
    {
        _closeButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }


    public void Show(GachaItemData data)
    {
        _data = data;
        UpdateData();
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }




    private void UpdateData()
    {
        _itemNameText.text = _data.Name;
        _descriptionText.text = _data.Description;
        _addScoreLayout.SetText(_data.AddScore.ToString());
        _tipPerMinuteLayout.SetText(Utility.ConvertToMoney(_data.TipPerMinute));
        _effectDescriptionText.text = Utility.GetGachaItemEffectDescription(_data);
        SetStar(_data.Rank);
        _itemImage.sprite = _data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
    }


    private void SetStar(Rank rank)
    {
        _itemStar.SetStar(rank);
        switch (rank)
        {
            case Rank.Normal1:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case Rank.Normal2:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case Rank.Rare:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(true);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case Rank.Unique:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(true);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case Rank.Special:
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
