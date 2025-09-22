using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1SelectFrameItemImage : MonoBehaviour
{
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _correctImage;
    [SerializeField] private GameObject _wrongImage;

    public void Init()
    {
        _itemImage.gameObject.SetActive(false);
        _correctImage.SetActive(false);
        _wrongImage.SetActive(false);
    }

    public void SetItemImage(MiniGame1ItemData itemData)
    {
        if (itemData == null)
        {
            _itemImage.gameObject.SetActive(false);
            return;
        }

        _itemImage.gameObject.SetActive(true);
        _correctImage.SetActive(false);
        _wrongImage.SetActive(false);
        _itemImage.sprite = itemData.Sprite;
    }

    public void ShowCorrectImage()
    {
        _correctImage.TweenStop();
        _wrongImage.TweenStop();
        _correctImage.SetActive(true);
        _wrongImage.SetActive(false);

        _correctImage.transform.localScale = Vector3.one * 1.5f;
        _correctImage.TweenScale(Vector3.one, 0.15f, Ease.InBack);
    }

    public void ShowWrongImage()
    {
        _correctImage.TweenStop();
        _wrongImage.TweenStop();
        _wrongImage.SetActive(true);
        _correctImage.SetActive(false);

        _wrongImage.transform.localScale = Vector3.one * 1.5f;
        _wrongImage.TweenScale(Vector3.one, 0.15f, Ease.InBack);
    }
}
