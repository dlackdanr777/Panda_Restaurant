using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIMoney _uiMoney;
    [SerializeField] private UIDia _uiDia;
    [SerializeField] private UIScore _uiScore;
    [SerializeField] private Image _scoreImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private UIChallengeTab _uiDaily;
    [SerializeField] private UIChallengeTab _uiWeekly;
    [SerializeField] private UIChallengeTab _uiAllTime;
    [SerializeField] private Button _uiDailyButton;
    [SerializeField] private Button _uiWeeklyButton;
    [SerializeField] private Button _uiAllTimeButton;
    [SerializeField] private GameObject _uiDailyAlarm;
    [SerializeField] private GameObject _uiWeeklyAlarm;
    [SerializeField] private GameObject _uiAllTimeAlarm;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Coin Animations")]
    [SerializeField] private int _coinMaxCount;
    [SerializeField] private float _coinDuration;
    [SerializeField] private Ease _coinEase;

    public override void Init()
    {
        // UIChallenge 참조를 먼저 설정한 뒤 슬롯 생성
        _uiDaily.Init(this, ChallengeManager.Instance.GetDailyChallenge);
        _uiAllTime.Init(this, ChallengeManager.Instance.GetAllTimeChallenge);
        _uiWeekly.Init(this, ChallengeManager.Instance.GetWeeklyChallenge);

        _uiDaily.Init(ChallengeManager.Instance.GetDailyChallenge());
        _uiAllTime.Init(ChallengeManager.Instance.GetAllTimeChallenge());
        _uiWeekly.Init(ChallengeManager.Instance.GetWeeklyChallenge());

        _uiDailyButton.onClick.AddListener(OnDailyButtonClicked);
        _uiWeeklyButton.onClick.AddListener(OnWeeklyButtonClicked);
        _uiAllTimeButton.onClick.AddListener(OnAllTimeButtonClicked);

        ChallengeManager.Instance.OnDailyChallengeUpdateHandler += OnDailyUpdateUI;
        ChallengeManager.Instance.OnAllTimeChallengeUpdateHandler += OnAllTimeUpdateUI;
        ChallengeManager.Instance.OnWeeklyChallengeUpdateHandler += OnWeeklyUpdateUI;

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _uiDailyAlarm.SetActive(_uiDaily.UpdateUI());
        _uiAllTimeAlarm.SetActive(_uiAllTime.UpdateUI());
        _uiWeeklyAlarm.SetActive(_uiWeekly.UpdateUI());
        _uiDaily.ResetScrollviewY();
        _uiAllTime.ResetScrollviewY();
        _uiWeekly.ResetScrollviewY();
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
    }


    private void OnAllTimeButtonClicked()
    {
        _uiAllTime.SetAsLastSibling();
    }

    private void OnWeeklyButtonClicked()
    {
        _uiWeekly.SetAsLastSibling();
    }


    private void OnDailyUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiDailyAlarm.SetActive(_uiDaily.UpdateUI());
    }


    private void OnAllTimeUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiAllTimeAlarm.SetActive(_uiAllTime.UpdateUI());
    }

    private void OnWeeklyUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiWeeklyAlarm.SetActive(_uiWeekly.UpdateUI());
    }

    public void StartCoinAnime(long money, Vector3 coinPos)
    {
        float time = 0;
        int coinCnt = 5;
        //int coinCnt = money / 1000;
        //coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, coinPos, Quaternion.identity);
        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            RectTransform coin = ObjectPoolManager.Instance.SpawnUICoin(coinPos, Quaternion.identity);
            Vector2 targetCoinPos = UnityEngine.Random.insideUnitCircle * 100;
            coin.TweenAnchoredPosition(coin.anchoredPosition + targetCoinPos, 0.3f, Ease.InQuad).OnComplete(() =>
            {
                float height = 100;
                if (coin.anchoredPosition.y < 0)
                    height *= -1;

                coin.TweenJump(_uiMoney.EffectSpawnPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                {
                    ObjectPoolManager.Instance.DespawnUICoin(coin);
                    _uiMoney.StartAnime();
                });
                time += 0.03f;
            });
        }
    }

    public void StartDiaAnime(long money, Vector3 coinPos)
    {
        float time = 0;
        int coinCnt = 5;
        //int coinCnt = money / 1000;
        //coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, coinPos, Quaternion.identity);
        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            RectTransform dia = ObjectPoolManager.Instance.SpawnUIDia(coinPos, Quaternion.identity);
            Vector2 targetCoinPos = UnityEngine.Random.insideUnitCircle * 100;
            dia.TweenAnchoredPosition(dia.anchoredPosition + targetCoinPos, 0.3f, Ease.InQuad).OnComplete(() =>
            {
                float height = 100;
                if (dia.anchoredPosition.y < 0)
                    height *= -1;

                dia.TweenJump(_uiDia.EffectSpawnPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                {
                    ObjectPoolManager.Instance.DespawnUIDia(dia);
                    _uiDia.StartAnime();
                });
                time += 0.03f;
            });
        }
    }


    public void StartScoreAnime(Vector3 endPos)
    {
        float time = 0;
        int coinCnt = 5;
        //int coinCnt = money / 1000;
        //coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, endPos, Quaternion.identity);
        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            RectTransform score = ObjectPoolManager.Instance.SpawnUIScore(endPos, Quaternion.identity);
            Vector2 targetPos = UnityEngine.Random.insideUnitCircle * 100;
            score.TweenAnchoredPosition(score.anchoredPosition + targetPos, 0.3f, Ease.InQuad).OnComplete(() =>
            {
                float height = 100;
                if (score.anchoredPosition.y < 0)
                    height *= -1;

                score.TweenJump(_scoreImage.transform.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                {
                    ObjectPoolManager.Instance.DespawnUIScore(score);
                    _uiScore.StartAnime();
                });
                time += 0.03f;
            });
        }
    }

    private void OnDestroy()
    {
        ChallengeManager.Instance.OnDailyChallengeUpdateHandler -= OnDailyUpdateUI;
        ChallengeManager.Instance.OnAllTimeChallengeUpdateHandler -= OnAllTimeUpdateUI;
        ChallengeManager.Instance.OnWeeklyChallengeUpdateHandler -= OnWeeklyUpdateUI;
    }


}
