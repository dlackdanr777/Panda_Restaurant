using BackEnd;
using Muks.BackEnd;
using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

public class UIPayment : MobileUIView
{
    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private UIPaymentGold _goldUI;
    [SerializeField] private UIPaymentDia _diaUI;
    [SerializeField] private Button _goldButton;
    [SerializeField] private Button _diaButton;

    [SerializeField] private Button _showGoldButton;
    [SerializeField] private Button _showDiaButton;


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
    
    [SerializeField] private UIMoney _uiMoney;
    [SerializeField] private UIDia _uiDia;
    [SerializeField] private Transform _centerPos;
    [SerializeField] private int _coinMaxCount;
    [SerializeField] private float _coinDuration;
    [SerializeField] private Ease _coinEase;


    public override void Init()
    {
        _dontTouchArea.transform.SetAsLastSibling();

        _goldButton.onClick.AddListener(OnGoldButtonClicked);
        _diaButton.onClick.AddListener(OnDiaButtonClicked);
        _showGoldButton.onClick.AddListener(ShowGoldUI);
        _showDiaButton.onClick.AddListener(ShowDiaUI);


        _goldUI.Init(this);
        _diaUI.Init(this);
        _diaUI.SetActive(false);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(true);
        _diaUI.SetActive(false);
        _goldUI.SetActive(true);
        _goldUI.Show();
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _dontTouchArea.SetActive(false);
        });
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _dontTouchArea.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    public void OnSlotButtonClicked(MoneyType moneyType, int value, int price)
    {
        if (moneyType == MoneyType.Dia)
        {
            return;
        }
        else
        {
            if (!UserInfo.IsDiaValid(price))
            {
                //골드가 부족할때
                PopupManager.Instance.ShowTextLackDia();
                return;
            }
            else
            {
                UserInfo.AddDia(-price);
                UserInfo.AddMoney(value);
            }
        }
        GameManager.Instance.SaveGameData();

        PaymentInfo.AddPaymentData($"MoenyType: {moneyType} | Value: {value} | Price: {price}");
        PaymentInfo.SavePaymentData();
        if (moneyType == MoneyType.Gold)
        {
            StartCoinAnime(value / 10000);
        }
    }

    public void AddDia(int dia)
    {
        UserInfo.AddDia(dia);
        GameManager.Instance.SaveGameData();
        PaymentInfo.AddPaymentData($"MoenyType: {MoneyType.Dia} | Value: {dia}");
        PaymentInfo.SavePaymentData();

        StartDiaAnime(dia / 10);
    }

    public void ShowGoldUI()
    {
        _uiNav.Push("UIPayment");
        _goldUI.SetActive(true);
        _diaUI.SetActive(false);
    }

    public void ShowDiaUI()
    {
        _uiNav.Push("UIPayment");
        _goldUI.SetActive(false);
        _diaUI.SetActive(true);
    }



    private void OnGoldButtonClicked()
    {
        _goldUI.SetActive(true);
        _diaUI.SetActive(false);
    }

    private void OnDiaButtonClicked()
    {
        _goldUI.SetActive(false);
        _diaUI.SetActive(true);
    }


    public void StartCoinAnime(int count)
    {
        float time = 0;
        int coinCnt = Math.Clamp(count, 1, _coinMaxCount);
        //int coinCnt = money / 1000;
        //coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
        SoundManager.Instance.PlayEffectAudio(EffectType.None, SoundEffectType.GoldSound);
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, _centerPos.position, Quaternion.identity);
        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            RectTransform coin = ObjectPoolManager.Instance.SpawnUICoin(_centerPos.position, Quaternion.identity);
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

    public void StartDiaAnime(int count)
    {
        float time = 0;
        int coinCnt = Math.Clamp(count, 1, _coinMaxCount);
        //int coinCnt = money / 1000;
        //coinCnt = coinCnt <= 5 ? 5 : _coinMaxCount < coinCnt ? _coinMaxCount : coinCnt;
        SoundManager.Instance.PlayEffectAudio(EffectType.None, SoundEffectType.DiaSound);
        ObjectPoolManager.Instance.SpawnUIEffect(UIEffectType.Type1, _centerPos.position, Quaternion.identity);
        for (int i = 0, cnt = coinCnt; i < cnt; ++i)
        {
            int index = i;
            RectTransform dia = ObjectPoolManager.Instance.SpawnUIDia(_centerPos.position, Quaternion.identity);
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

}
