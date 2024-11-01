using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System;

public class UIImageAndText : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    public TextMeshProUGUI Text => _text;


    public Color TextColor
    {
        get { return _text.color;}
        set { _text.color = value; }
    }

    public Color ImageColor
    {
        get { return _image.color;}
        set { _image.color = value; }
    }

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetText(string text)
    {
        _text.text = text;
    }

    public void SetImageMaterial(Material material)
    {
        _image.material = material;
    }


    public void TweenStop()
    {
        _text.TweenStop();
    }

    public TweenData TweenText(string str, float duration, Ease ease = Ease.Constant)
    {
        _text.TweenStop();
        TweenData tween = _text.TweenText(str, duration, ease);
        return tween;
    }

    public TweenData TweenCharacter(string str, float characterInterval, Ease ease = Ease.Constant)
    {
        _text.TweenStop();
        TweenData tween = _text.TweenCharacter(str, characterInterval, ease);
        return tween;
    }
}
