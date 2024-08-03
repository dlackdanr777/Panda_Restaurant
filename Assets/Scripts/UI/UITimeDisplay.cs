using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;

public class UITimeDisplay : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space(20)]
    [Header("Options")]
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space(5)]
    [SerializeField] private float _waitDuration;

    [Space(5)]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    private Coroutine _showCoroutine;

    public void Init()
    {
        gameObject.SetActive(false);
        _canvasGroup.alpha = 0;
    }

    public void Show(string description)
    {
        gameObject.SetActive(true);

        if (_showCoroutine != null)
            StopCoroutine(_showCoroutine);

        _showCoroutine = StartCoroutine(ShowRoutione(description));
    }

    private IEnumerator ShowRoutione(string description)
    {
        _canvasGroup.TweenStop();
        _text.text = description;

        _canvasGroup.TweenAlpha(1, _showDuration, _showTweenMode);
        yield return YieldCache.WaitForSeconds(_showDuration + _waitDuration);
        _canvasGroup.TweenAlpha(0, _hideDuration, _hideTweenMode).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });

    }



}
