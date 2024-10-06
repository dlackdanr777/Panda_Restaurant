using UnityEngine;
using UnityEngine.UI;
using Muks.RecyclableScrollView;

public class UIGachaItemSlot : RecyclableScrollSlot<GachaItemData>
{
    [Header("Components")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIItemStar _uiStar;

    GachaItemData _data;
    public override void Init()
    {
    }

    public override void UpdateSlot(GachaItemData data)
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
        _itemImage.sprite = data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
        _uiStar.SetStar(data.GachaItemRank);
    }
}
