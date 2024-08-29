using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections;
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
    [SerializeField] private TextMeshProUGUI _tipText;
    [SerializeField] private GameObject _bigCoinImage;
    [SerializeField] private UIMoney _uiMoney;

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
    [SerializeField] private RectTransform _coinPos;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private Transform _coinTargetPos;
    [SerializeField] private float _coinDuration;
    [SerializeField] private Ease _coinEase;


    private Coroutine _moneyAnimeRoutine;
    private Color _tipAnimeTmpColor;
    private Vector2 _tipAnimeTmpPos;
    private Vector3 _startPos;
    private Vector3 _tmpCoinScale;
    private Vector3 _tmpBigCoinScale;
    private Vector3 _currentBigCoinScale;
    private float _currentScaleMul;
    private int _currentTip;

    public override void Init()
    {
        _tmpBigCoinScale = _bigCoinImage.transform.localScale;
        _dontTouchArea.SetAsLastSibling();

        _collectTipButton.onClick.AddListener(OnCollectTipButtonClicked);
        _advertisingButton.onClick.AddListener(OnAdvertisingButtonClicked);
        UserInfo.OnChangeTipHandler += OnChangeMoneyEvent;

        _tipAnimeTmpPos = _tipAnimeTmp.rectTransform.anchoredPosition;
        _tipAnimeTmpColor = _tipAnimeTmp.color;
        _tipText.text = Utility.ConvertToNumber(UserInfo.Tip);
        _currentTip = UserInfo.Tip;

        _tipAnimeTmp.gameObject.SetActive(false);
        gameObject.SetActive(false);
        UserInfo.AddTip(5000);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;

        _bigCoinImage.gameObject.SetActive(true);
        _tipText.text = Utility.ConvertToNumber(UserInfo.Tip);
        _currentTip = UserInfo.Tip;
        float scaleMul = 0.5f + Mathf.Clamp((UserInfo.Tip / 100) * 0.1f, 0, 1);
        _currentBigCoinScale = _tmpBigCoinScale * scaleMul;
        _bigCoinImage.transform.localScale = _currentBigCoinScale;

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


    private void OnCollectTipButtonClicked()
    {
        if (UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        //TMPAnime("+ " + UserInfo.Tip.ToString("N0"));
        //UserInfo.TipCollection();
        //CoinAnime(false);
        GiveTipAnime(false);
    }

    private void OnAdvertisingButtonClicked()
    {
        if (UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        //CoinAnime(true);
        GiveTipAnime(true);
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


    private void GiveTipAnime(bool isAds)
    {
        _dontTouchArea.gameObject.SetActive(true);

        _bigCoinImage.gameObject.SetActive(true);
        _bigCoinImage.transform.localScale = _currentBigCoinScale;
        _bigCoinImage.TweenStop();
        _uiNav.Pop("UITip");
        Tween.Wait(_hideDuration, () =>
        {
            float time = 0;
            int coinCnt = UserInfo.Tip / 1000;
            coinCnt = coinCnt <= 10 ? 10 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
            int tipValue = UserInfo.Tip / coinCnt;
            int lastTipValue = UserInfo.Tip % coinCnt;
            UserInfo.TipCollection(isAds);
            for (int i = 0, cnt = coinCnt; i < cnt; ++i)
            {
                int index = i;
                RectTransform coin = ObjectPoolManager.Instance.SpawnUICoin(_coinPos.transform.position, Quaternion.identity);
                Vector2 coinPos = UnityEngine.Random.insideUnitCircle * 300;

                coin.TweenAnchoredPosition(coinPos, 0.4f, Ease.InQuad).OnComplete(() =>
                {
                        float height = 100;
                        if (coin.anchoredPosition.y < 0)
                            height *= -1;

                        coin.TweenJump(_coinTargetPos.position, height, _coinDuration + time, _coinEase).OnComplete(() =>
                        {

                            ObjectPoolManager.Instance.DespawnUICoin(coin);
                            _uiMoney.StartAnime();
                            if (index == cnt - 1)
                            {
                                _dontTouchArea.gameObject.SetActive(false);
                                _currentBigCoinScale = Vector3.one;
                                _bigCoinImage.transform.localScale = _currentBigCoinScale;
                            }
                        });
                    time += 0.04f;
                });
            }
        });     
    }


    private void OnChangeMoneyEvent()
    {
        if (VisibleState == VisibleState.Disappeared || VisibleState == VisibleState.Disappearing)
            return;

        int addMoney = UserInfo.Tip - _currentTip;

        if (addMoney == 0)
            return;

        _currentTip = UserInfo.Tip;

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));
        CheckBigCoin();
    }

        private IEnumerator AddMoneyAnime(int addMoney)
    {
        int startMoney = UserInfo.Tip - addMoney;
        int targetMoney = UserInfo.Tip;
        float time = 0;

        while (time < 1)
        {
            _tipText.text = Utility.ConvertToNumber(Mathf.Lerp(startMoney, targetMoney, time));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _tipText.text = Utility.ConvertToNumber(UserInfo.Tip);       
    }

    private void CheckBigCoin()
    {
        if (!_bigCoinImage.gameObject.activeSelf)
            return;

        float tip = UserInfo.Tip;
        _bigCoinImage.TweenStop();

        if (_currentBigCoinScale == Vector3.zero)
            _currentBigCoinScale = Vector3.one;

        _bigCoinImage.transform.localScale = _currentBigCoinScale;

        float scaleMul = 1 + Mathf.Clamp((tip / 1000) * 0.1f, 0, 1);
        _currentBigCoinScale = _tmpBigCoinScale * scaleMul;

        if (scaleMul == _currentScaleMul)
        {
            _bigCoinImage.TweenScale(_currentBigCoinScale + new Vector3(0.1f, 0.1f, 0.1f), 0.2f, Ease.Spike);
            return;
        }

        else
        {
            _bigCoinImage.TweenScale(_currentBigCoinScale, 0.2f, Ease.OutBack);
            return;
        }
    }
}
