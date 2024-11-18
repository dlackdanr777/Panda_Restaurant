using Muks.Tween;
using System;
using TMPro;
using UnityEngine;

public class UIFirstLoadingScene : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI _titleText;
    public void Init()
    {
        _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, 0);
    }

    public void ShowTitle(Action onCompleted = null)
    {
        _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, 0);
        _titleText.TweenAlpha(1, 1f, Ease.Smoothstep).OnComplete(onCompleted);
    }

    public void HideTitle(Action onCompleted = null)
    {
        _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, 1);
        _titleText.TweenAlpha(0, 1f, Ease.Smoothstep).OnComplete(onCompleted);
    }
}
