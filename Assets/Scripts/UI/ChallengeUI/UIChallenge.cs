using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIMoney _uiMoney;
    [SerializeField] private UIDia _uiDia;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private UIChallengeTab _uiDaily;
    [SerializeField] private UIChallengeTab _uiAllTime;
    [SerializeField] private Button _uiDailyButton;
    [SerializeField] private Button _uiAllTimeButton;
    [SerializeField] private GameObject _uiDailyAlarm;
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
        _uiDaily.Init(this);
        _uiAllTime.Init(this);
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
        _uiDailyAlarm.SetActive(_uiDaily.UpdateUI());
        _uiAllTimeAlarm.SetActive(_uiAllTime.UpdateUI());
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

        _uiDailyAlarm.SetActive(_uiDaily.UpdateUI());
    }


    private void OnAllTimeUpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _uiAllTimeAlarm.SetActive(_uiAllTime.UpdateUI());
    }

    public void StartCoinAnime(int money, Vector3 coinPos)
    {
            float time = 0;
            int coinCnt = money / 1000;
            coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
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
                    time += 0.05f;
                });
            }
    }

    public void StartDiaAnime(int money, Vector3 coinPos)
    {
            float time = 0;
            int coinCnt = money / 1000;
            coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
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

                    dia.TweenJump(_uiMoney.EffectSpawnPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                    {
                        ObjectPoolManager.Instance.DespawnUIDia(dia);
                        _uiDia.StartAnime();
                    });
                    time += 0.05f;
                });
            }
    }

    private void OnDestroy()
    {
        ChallengeManager.Instance.OnDailyChallengeUpdateHandler -= OnDailyUpdateUI;
        ChallengeManager.Instance.OnAllTimeChallengeUpdateHandler -= OnAllTimeUpdateUI;
    }


}
