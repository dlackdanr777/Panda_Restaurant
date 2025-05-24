using Muks.Tween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFirstLoadingScene : MonoBehaviour
{
    [SerializeField] private Image _title;
    [SerializeField] private TextMeshProUGUI _errorText;

    public void Init()
    {
        _title.color = new Color(_title.color.r, _title.color.g, _title.color.b, 0);
        _errorText.gameObject.SetActive(false);
    }


    public void ShowErrorText(string errorText)
    {
        _errorText.gameObject.SetActive(true);
        _errorText.text = errorText;
    }

    public void ShowTitle(Action onCompleted = null)
    {
        _title.color = new Color(_title.color.r, _title.color.g, _title.color.b, 0);
        _title.TweenAlpha(1, 0.7f, Ease.Smoothstep).OnComplete(onCompleted);
    }

    public void HideTitle(Action onCompleted = null)
    {
        _title.color = new Color(_title.color.r, _title.color.g, _title.color.b, 1);
        _title.TweenAlpha(0, 0.5f, Ease.Smoothstep).OnComplete(onCompleted);
    }
}
