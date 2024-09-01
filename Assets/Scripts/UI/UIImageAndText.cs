using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System;

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

    public void TweenText(string str, float duration, Ease ease = Ease.Constant, Action onCompleted = null)
    {
        _text.TweenStop();
        TweenData tween = _text.TweenText(str, duration, ease);
        if (onCompleted != null)
            tween.OnComplete(onCompleted);
    }
}
