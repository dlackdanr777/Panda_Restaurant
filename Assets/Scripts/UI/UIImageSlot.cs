using UnityEngine;
using UnityEngine.UI;

public class UIImageSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _image;

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
        Utility.ChangeImagePivot(_image);
    }

    public void SetColor(Color color)
    {
        _image.color = color;
    }

    public void SetMaterial(Material material)
    {
        _image.material = material;
    }
}
