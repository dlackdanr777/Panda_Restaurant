using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System.Runtime.CompilerServices;
using TMPro;

public class UITimeDisplay : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space(20)]
    [Header("Options")]
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space(5)]
    [SerializeField] private float _waitDuration;

    [Space(5)]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;


    private TweenData _tweenData;

    public void Init()
    {
        gameObject.SetActive(false);
        _canvasGroup.alpha = 0;
    }

    public void Show(string description)
    {
        gameObject.SetActive(true);
        _canvasGroup.TweenStop();
        _text.text = description;

        _tweenData = _canvasGroup.TweenAlpha(1, _showDuration, _showTweenMode);
        _tweenData.OnComplete(() => Tween.Wait(_waitDuration, () =>
        {
            _canvasGroup.TweenAlpha(0, _hideDuration, _hideTweenMode).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }));
    }



}
