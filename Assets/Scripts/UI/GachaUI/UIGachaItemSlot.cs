using UnityEngine;
using UnityEngine.UI;
using Muks.RecyclableScrollView;

public class UIGachaItemSlot : RecyclableScrollSlot<GachaData>
{
    [Header("Components")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIItemStar _uiStar;

    private UIGachaCard _card;
    GachaData _data;
    public override void Init()
    {
        _button.onClick.AddListener(OnButtonClicked);
    }

    public void SetCard(UIGachaCard card)
    {
        _card = card;
    }



    public override void UpdateSlot(GachaData data)
    {
        if (data == null)
        {
            _data = null;
            gameObject.SetActive(false);
            return;
        }

        if (_data == data)
            return;

        _data = data;
        gameObject.SetActive(true);
        _itemImage.sprite = data.ThumbnailSprite;
        _uiStar.SetStar(data.Rank);

    }

    public void ChangeImagePivot()
    {
        Utility.ChangeImagePivot(_itemImage);
    }

    private void OnButtonClicked()
    {
        if (_data == null)
            return;

        _card.gameObject.SetActive(true);
        _card.SetData(_data);
    }
}
