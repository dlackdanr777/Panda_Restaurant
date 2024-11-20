using Muks.Tween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFirstLoadingScene : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _errorText;

    public void Init()
    {
        _titleText.color = new Color(_titleText.color.r, _titleText.color.g, _titleText.color.b, 0);
        _errorText.gameObject.SetActive(false);
    }


    public void ShowErrorText(string errorText)
    {
        _errorText.gameObject.SetActive(true);
        _errorText.text = errorText;
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
