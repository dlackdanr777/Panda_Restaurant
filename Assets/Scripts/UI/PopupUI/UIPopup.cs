using Muks.Tween;
using System;
using UnityEngine;


public class UIPopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private Action _onOkButtonClicked;
    private Action _onExitButtonClicked;


    public void Init()
    {
        gameObject.SetActive(false);
    }


    public void Show(Action okButtonClicked, Action exitButtonClicked)
    {
        gameObject.SetActive(true);
        _onOkButtonClicked = okButtonClicked;
        _onExitButtonClicked = exitButtonClicked;

        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            _canvasGroup.blocksRaycasts = true;
        });

    }


    public void Hide()
    {
        _animeUI.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }



    private void OnOkButtonClicked()
    {
        _onOkButtonClicked?.Invoke();
    }


    private void OnExitButtonClicked()
    {
        _onExitButtonClicked?.Invoke();
    }

}
