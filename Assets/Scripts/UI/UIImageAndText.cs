using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System;

public class UIImageAndText : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    public Color TextColor
    {
        get { return _text.color;}
        set { _text.color = value; }
    } 

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetText(string text)
    {
        _text.text = text;
    }

    public void TweenStop()
    {
        _text.TweenStop();
    }

    public void TweenText(string str, float duration, Ease ease = Ease.Constant, Action onCompleted = null)
    {
        _text.TweenStop();
        TweenData tween = _text.TweenText(str, duration, ease);
        if (onCompleted != null)
            tween.OnComplete(onCompleted);
    }

    public void TweenCharacter(string str, float characterInterval, Ease ease = Ease.Constant, Action onCompleted = null)
    {
        _text.TweenStop();
        TweenData tween = _text.TweenCharacter(str, characterInterval, ease);
        if (onCompleted != null)
            tween.OnComplete(onCompleted);
    }
}
