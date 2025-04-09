using UnityEngine;
using UnityEngine.UI;

public class UIOrderFoodSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private UIImageAndText _lockGroup;

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
        Utility.ChangeImagePivot(_image);
    }

    public void SetMaterial(Material material)
    {
        _image.material = material;
    }

    public void SetColor(Color color)
    {
        _image.color = color;
    }

    public void EnableLockGroup(string text)
    {
        _lockGroup.gameObject.SetActive(true);
        _lockGroup.SetText(text);
    }

    public void DisableLockGroup()
    {
        _lockGroup.gameObject.SetActive(false);
    }
}
