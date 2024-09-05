using UnityEngine;
using UnityEngine.UI;

public class UIGachaItemSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIItemStar _uiStar;


    public void SetData(GachaItemData data)
    {
        if(data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _itemImage.sprite = data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
        _uiStar.SetStar(data.GachaItemRank);
    }

}
