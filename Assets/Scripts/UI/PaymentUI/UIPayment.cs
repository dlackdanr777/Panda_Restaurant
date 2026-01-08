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
            UserInfo.AddDia(value);
        }
        else
        {
            if (!UserInfo.IsMoneyValid(price))
            {
                //골드가 부족할때
                PopupManager.Instance.ShowTextLackMoney();
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

}
