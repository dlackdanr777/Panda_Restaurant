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
    [SerializeField] private TextMeshProUGUI _tipText;
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

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _collectTipSound;


    private Coroutine _moneyAnimeRoutine;
    private int _currentTip;

    public override void Init()
    {
        _dontTouchArea.SetAsLastSibling();

        _collectTipButton.onClick.AddListener(OnCollectTipButtonClicked);
        _advertisingButton.onClick.AddListener(OnAdvertisingButtonClicked);
        UserInfo.OnChangeTipHandler += OnChangeMoneyEvent;

        int tip = UserInfo.GetTip(UserInfo.CurrentStage);
        _tipText.text = Utility.ConvertToMoney(tip);
        _currentTip = tip;

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;

        int tip = UserInfo.GetTip(UserInfo.CurrentStage);
        _tipText.text = Utility.ConvertToMoney(tip);
        _currentTip = tip;

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
        if (UserInfo.GetTip(UserInfo.CurrentStage) <= 0)
        {
            PopupManager.Instance.ShowDisplayText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        GiveTipAnime(false);
    }

    private void OnAdvertisingButtonClicked()
    {
        if (UserInfo.GetTip(UserInfo.CurrentStage) <= 0)
        {
            PopupManager.Instance.ShowDisplayText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        //CoinAnime(true);
        GiveTipAnime(true);
    }


    private void GiveTipAnime(bool isAds)
    {
        _dontTouchArea.gameObject.SetActive(true);

        _uiNav.Pop("UITip");
        Tween.Wait(_hideDuration, () =>
        {
            float time = 0;
            int tip = isAds ? UserInfo.GetTip(UserInfo.CurrentStage) * 2 : UserInfo.GetTip(UserInfo.CurrentStage);
            int coinCnt = tip / 500;
            coinCnt = coinCnt <= 10 ? 10 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
            UserInfo.TipCollection(UserInfo.CurrentStage, isAds);
            ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, _coinPos.transform.position, Quaternion.identity);
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _collectTipSound);
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
                        }
                    });
                    time += 0.05f;
                });
            }
        });
    }


    private void OnChangeMoneyEvent()
    {
        if (VisibleState == VisibleState.Disappeared || VisibleState == VisibleState.Disappearing)
        {
            _currentTip = UserInfo.GetTip(UserInfo.CurrentStage);
            return;
        }


        int addMoney = UserInfo.GetTip(UserInfo.CurrentStage) - _currentTip;

        if (addMoney == 0)
            return;

        _currentTip = UserInfo.GetTip(UserInfo.CurrentStage);

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));
    }

    private IEnumerator AddMoneyAnime(int addMoney)
    {
        int startMoney = UserInfo.GetTip(UserInfo.CurrentStage) - addMoney;
        int targetMoney = UserInfo.GetTip(UserInfo.CurrentStage);
        float time = 0;

        while (time < 1)
        {
            _tipText.SetText(Utility.ConvertToMoney(Mathf.FloorToInt(Mathf.Lerp(startMoney, targetMoney, time))));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _tipText.SetText(Utility.ConvertToMoney(UserInfo.GetTip(UserInfo.CurrentStage)));
    }
}
