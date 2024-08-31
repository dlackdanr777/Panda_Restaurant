using UnityEngine;
using UnityEngine.UI;

public class UIGachaItemSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _star1;
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;
    [SerializeField] private GameObject _star4;
    [SerializeField] private GameObject _star5;


    public void SetData(GachaItemData data)
    {
        if(data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _itemImage.sprite = data.Sprite;

        switch(data.GachaItemRank)
        {
            case GachaItemRank.Normal1:
                _star1.gameObject.SetActive(true);
                _star2.gameObject.SetActive(false);
                _star3.gameObject.SetActive(false);
                _star4.gameObject.SetActive(false);
                _star5.gameObject.SetActive(false);
                break;

                case GachaItemRank.Normal2:
                _star1.gameObject.SetActive(true);
                _star2.gameObject.SetActive(true);
                _star3.gameObject.SetActive(false);
                _star4.gameObject.SetActive(false);
                _star5.gameObject.SetActive(false);
                break;

            case GachaItemRank.Rare:
                _star1.gameObject.SetActive(true);
                _star2.gameObject.SetActive(true);
                _star3.gameObject.SetActive(true);
                _star4.gameObject.SetActive(false);
                _star5.gameObject.SetActive(false);
                break;

            case GachaItemRank.Unique:
                _star1.gameObject.SetActive(true);
                _star2.gameObject.SetActive(true);
                _star3.gameObject.SetActive(true);
                _star4.gameObject.SetActive(true);
                _star5.gameObject.SetActive(false);
                break;

            case GachaItemRank.Special:
                _star1.gameObject.SetActive(true);
                _star2.gameObject.SetActive(true);
                _star3.gameObject.SetActive(true);
                _star4.gameObject.SetActive(true);
                _star5.gameObject.SetActive(true);
                break;
        }
    }

}
