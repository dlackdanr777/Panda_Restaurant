using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private UIChallengeTab _uiDaily;
    [SerializeField] private UIChallengeTab _uiAllTime;
    [SerializeField] private Button _uiDailyButton;
    [SerializeField] private Button _uiAllTimeButton;

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
        _uiDaily.Init(ChallengeManager.Instance.GetDailyChallenge());
        _uiAllTime.Init(ChallengeManager.Instance.GetAllTimeChallenge());

        _uiDailyButton.onClick.AddListener(OnDailyButtonClicked);
        _uiAllTimeButton.onClick.AddListener(OnAllTimeButtonClicked);

        ChallengeManager.Instance.OnDailyChallengeUpdateHandler += OnDailyUpdateUI;
        ChallengeManager.Instance.OnAllTimeChallengeUpdateHandler += OnAllTimeUpdateUI;



        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _uiDaily.UpdateUI();
        _uiAllTime.UpdateUI();
        _uiDaily.ResetScrollviewY();
        _uiAllTime.ResetScrollviewY();
        OnDailyButtonClicked();
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


    private void OnDailyButtonClicked()
    {
        _uiDaily.SetAsLastSibling();
        _uiAllTime.SetAsFirstSibling();
    }


    private void OnAllTimeButtonClicked()
    {
        _uiDaily.SetAsFirstSibling();
        _uiAllTime.SetAsLastSibling();
    }


    private void OnDailyUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiDaily.UpdateUI();
    }


    private void OnAllTimeUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiAllTime.UpdateUI();
    }


}
