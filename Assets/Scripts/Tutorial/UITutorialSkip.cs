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

    public override void Init()
    {
        _okButton.onClick.AddListener(OnOkButtonClicked);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;

        Vibration.Vibrate(500);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    public void ShowSkipUI(Action onButtonClicked = null)
    {
        _uiNav.Push("UITutorialSkip");
        _onOkButtonClicked = onButtonClicked;
    }

    private void OnOkButtonClicked()
    {
        _onOkButtonClicked?.Invoke();
        _uiNav.Pop("UITutorialSkip");
    }

}
