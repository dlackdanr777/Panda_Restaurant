using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;

public class UIChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private UIChallengeTab _uiDaily;
    [SerializeField] private UIChallengeTab _uiAllTime;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;
    public override void Init()
    {
        _uiDaily.Init(ChallengeManager.Instance.GetMainChallenge(), UserInfo.GetIsDoneMainChallenge, UserInfo.GetIsClearMainChallenge);
        gameObject.SetActive(false);

        ChallengeManager.Instance.OnChallengeUpdateHandler += _uiDaily.UpdateUI;

    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
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
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }
}
