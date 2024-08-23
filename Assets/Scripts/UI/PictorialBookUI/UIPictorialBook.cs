using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;

public class UIPictorialBook : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIGachaItem _uiGachaItem;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;
    [SerializeField] private RectTransform _showTargetPos;


    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;
    [SerializeField] private RectTransform _hideTargetPos;


    public override void Init()
    {
        _uiGachaItem.Init();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.anchoredPosition = _hideTargetPos.anchoredPosition;

        TweenData tween = _animeUI.TweenAnchoredPosition(_showTargetPos.anchoredPosition, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

        _uiGachaItem.ResetData();
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.anchoredPosition = _showTargetPos.anchoredPosition;

        TweenData tween = _animeUI.TweenAnchoredPosition(_hideTargetPos.anchoredPosition, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }
}
