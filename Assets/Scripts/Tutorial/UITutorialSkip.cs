using Muks.Tween;
using UnityEngine;
using Muks.MobileUI;
using UnityEngine.UI;
using System;

public class UITutorialSkip : MobileUIView
{

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _okButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    private Action _onOkButtonClicked;
    private int _animationLifetime;

    public override void Init()
    {
        _okButton.onClick.AddListener(OnOkButtonClicked);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        int lifetime = ++_animationLifetime;
        _animeUI.TweenStop();
        VisibleState = VisibleState.Appearing;

        Vibration.Vibrate(500);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            if (this == null || lifetime != _animationLifetime || !gameObject.activeInHierarchy) return;
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        int lifetime = ++_animationLifetime;
        _animeUI.TweenStop();
        VisibleState = VisibleState.Disappearing;
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            if (this == null || lifetime != _animationLifetime) return;
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    public void ShowSkipUI(Action onButtonClicked = null)
    {
        _onOkButtonClicked = onButtonClicked;
        _uiNav.Push("UITutorialSkip");
    }

    // Completion may be synchronous, or arrive while the confirmation is hiding.
    // Remove both the view and its transition before invoking the tutorial owner.
    public void CloseImmediately()
    {
        ++_animationLifetime;
        _onOkButtonClicked = null;
        _animeUI.TweenStop();
        _canvasGroup.blocksRaycasts = false;
        if (_uiNav != null) _uiNav.PopNoAnime("UITutorialSkip");
        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        ++_animationLifetime;
        if (_animeUI != null) _animeUI.TweenStop();
        VisibleState = VisibleState.Disappeared;
    }

    private void OnOkButtonClicked()
    {
        var confirmed = _onOkButtonClicked;
        CloseImmediately();
        confirmed?.Invoke();
    }

}
