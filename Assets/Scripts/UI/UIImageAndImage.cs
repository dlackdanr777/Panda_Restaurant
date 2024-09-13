using UnityEngine;
using UnityEngine.UI;

public class UIImageAndImage : MonoBehaviour
{
    [SerializeField] private Image _image1;
    [SerializeField] private Image _image2;

    public Color ImageColor1
    {
        get { return _image1.color;}
        set { _image1.color = value; }
    }

    public Color ImageColor2
    {
        get { return _image2.color; }
        set { _image2.color = value; }
    }

    public void SetSprite1(Sprite sprite)
    {
        _image1.sprite = sprite;
    }

    public void SetSprite2(Sprite sprite)
    {
        _image2.sprite = sprite;
    }

    public void SetImageMaterial1(Material material)
    {
        _image1.material = material;
    }

    public void SetImageMaterial2(Material material)
    {
        _image2.material = material;
    }

    public void Image1SetActive(bool active)
    {
        _image1.gameObject.SetActive(active);
    }

    public void Image2SetActive(bool active)
    {
        _image2.gameObject.SetActive(active);
    }
}
