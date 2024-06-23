using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIImageAndText : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetText(string text)
    {
        _text.text = text;
    }
}
