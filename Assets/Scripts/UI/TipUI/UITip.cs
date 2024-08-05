using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITip : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _collectTipButton;
    [SerializeField] private Button _advertisingButton;
    [SerializeField] private TextMeshProUGUI _tipAnimeTmp;

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
    [SerializeField] private RectTransform _coinParent;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private RectTransform _coinPrefab;
    [SerializeField] private Transform _coinTargetPos;
    [SerializeField] private int _coinCount;
    [SerializeField] private float _coinDuration;
    [SerializeField] private Ease _coinEase;



    private RectTransform[] _coins;
    private Vector2 _tipAnimeTmpPos;
    private Color _tipAnimeTmpColor;
    private Vector3 _startPos;
    private Vector3 _tmpCoinScale;

    public override void Init()
    {
        _coins = new RectTransform[_coinCount];
        for (int i = 0; i < _coinCount; ++i)
        {
            _coins[i] = Instantiate(_coinPrefab, _coinParent);
            _coins[i].gameObject.SetActive(false);
        }

        if (_coins[0] != null)
            _tmpCoinScale = _coins[0].transform.localScale;

        _dontTouchArea.SetAsLastSibling();

        _collectTipButton.onClick.AddListener(OnCollectTipButtonClicked);
        _advertisingButton.onClick.AddListener(OnAdvertisingButtonClicked);

        _tipAnimeTmpPos = _tipAnimeTmp.rectTransform.anchoredPosition;
        _tipAnimeTmpColor = _tipAnimeTmp.color;
        _tipAnimeTmp.gameObject.SetActive(false);

        gameObject.SetActive(false);
        UserInfo.AppendTip(5000);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        for (int i = 0, cnt = _coins.Length; i < cnt; ++i)
        {
            _coins[i].gameObject.SetActive(false);
        }

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


    private void OnCollectTipButtonClicked()
    {
        if (UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        //TMPAnime("+ " + UserInfo.Tip.ToString("N0"));
        //UserInfo.TipCollection();
        CoinAnimeTest(false);
    }

    private void OnAdvertisingButtonClicked()
    {
        if (UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        //TMPAnime("+ " + (UserInfo.Tip * 2).ToString("N0"));
        //UserInfo.TipCollection(true);
        CoinAnimeTest(true);
    }


    private void TMPAnime(string text)
    {
        _tipAnimeTmp.gameObject.SetActive(true);
        _tipAnimeTmp.rectTransform.TweenStop();
        _tipAnimeTmp.TweenStop();
        _tipAnimeTmp.text = text;
        _tipAnimeTmp.rectTransform.anchoredPosition = _tipAnimeTmpPos;
        _tipAnimeTmp.color = _tipAnimeTmpColor;
        _tipAnimeTmp.rectTransform.TweenAnchoredPosition(_tipAnimeTmpPos + new Vector2(0, 50), 1f, Ease.InQuad);
        _tipAnimeTmp.TweenAlpha(0, 1, Ease.Smoothstep).OnComplete(() => _tipAnimeTmp.gameObject.SetActive(false));
    }


    private void CoinAnime(bool isAds)
    {
        _dontTouchArea.gameObject.SetActive(true);
        float time = 0.01f;

        int coinCnt = UserInfo.Tip / 100;
        coinCnt = coinCnt == 0 ? 1 : _coins.Length < coinCnt ? _coins.Length : coinCnt;
        int tipValue = UserInfo.Tip / coinCnt;
        int lastTipValue = UserInfo.Tip % coinCnt;

        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            _coins[index].anchoredPosition = new Vector2(0, 0);
            Vector2 targetPos1 = (Vector2)_coins[index].transform.position + Random.insideUnitCircle * 150;
            Vector2 startScale = _tmpCoinScale * 0.9f;
            Tween.Wait(time, () =>
            {
                _coins[index].gameObject.SetActive(true);
                _coins[index].TweenStop();

                _coins[index].localScale = startScale;
                _coins[index].TweenScale(_tmpCoinScale, 0.35f, Ease.OutCubic);
                _coins[index].TweenMove(targetPos1, 0.35f, Ease.OutCubic)
                .OnComplete(() =>
                {
                    Tween.Wait(0.25f, () =>
                    {
                        _coins[index].TweenScale(startScale, _coinDuration, _coinEase);
                        _coins[index].TweenMove(_coinTargetPos.position, _coinDuration, _coinEase).OnComplete(() =>
                        {

                            _coins[index].gameObject.SetActive(false);

                            if (index == cnt - 1)
                            {
                                UserInfo.TipCollection(tipValue + lastTipValue, isAds);
                                _dontTouchArea.gameObject.SetActive(false);
                            }

                            else
                            {
                                UserInfo.TipCollection(tipValue, isAds);
                            }

                        });
                    });
                });
            });
            time += 0.02f;
        }
    }


    private void CoinAnimeTest(bool isAds)
    {
        _dontTouchArea.gameObject.SetActive(true);
        float time = 0.05f;

        int coinCnt = UserInfo.Tip / 100;
        coinCnt = coinCnt == 0 ? 1 : _coins.Length < coinCnt ? _coins.Length : coinCnt;
        int tipValue = UserInfo.Tip / coinCnt;
        int lastTipValue = UserInfo.Tip % coinCnt;

        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            Vector2 coinPos = Vector2.zero + Random.insideUnitCircle * 50;
            _coins[index].anchoredPosition = coinPos;
            Vector2 startScale = _tmpCoinScale * 0.9f;

            _coins[index].gameObject.SetActive(true);
            _coins[index].TweenStop();
            _coins[index].localScale = _tmpCoinScale;

            Tween.Wait(0.25f + time, () =>
            {
                _coins[index].TweenScale(startScale, _coinDuration, _coinEase);
                _coins[index].TweenMove(_coinTargetPos.position, _coinDuration, _coinEase).OnComplete(() =>
                {

                    _coins[index].gameObject.SetActive(false);

                    if (index == cnt - 1)
                    {
                        UserInfo.TipCollection(tipValue + lastTipValue, isAds);
                        _dontTouchArea.gameObject.SetActive(false);
                    }

                    else
                    {
                        UserInfo.TipCollection(tipValue, isAds);
                    }

                });
            });
            time += 0.02f;
        }
    }
}
