using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;
using System;

public class UIIntroSkip : MonoBehaviour
{

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _okButton;
    [SerializeField] private Button _cancelButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    private Action _onOkButtonClicked;

    public void Init(Action onOkButtonClicked)
    {
        _onOkButtonClicked = onOkButtonClicked;
        _okButton.onClick.AddListener(OnOkButtonClicked);
        _cancelButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public void Hide()
    {
        _animeUI.gameObject.SetActive(true);
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
}
