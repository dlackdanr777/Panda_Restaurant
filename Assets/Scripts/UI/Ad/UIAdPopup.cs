using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using UnityEngine.UI;
using TMPro;
using System;

public class UIAdPopup : MobileUIView
{

    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button _adButton;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private GameObject _defaultLayout;
    [SerializeField] private GameObject _diaLayout;
    [SerializeField] private GameObject _coinLayout;
    [SerializeField] private GameObject _feverLayout;
    [SerializeField] private GameObject _customerLayout;



    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private Action _onAdButtonClicked;

    public override void Init()
    {
        _adButton.onClick.AddListener(AdButtonClicked);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
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


    public void ShowPopup(Action onButtonClicked)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _text.SetText("광고를 시청하시겠습니까?");
        _onAdButtonClicked = onButtonClicked;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(true);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
    }

    public void ShowDiaPopup(Action onButtonClicked)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _text.SetText($"광고를 시청하고\n보상을 {Utility.SetStringColor("두배로", ColorType.Positive)} 받으시겠습니까?");
        _onAdButtonClicked = onButtonClicked;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(true);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
    }

    public void ShowCoinPopup(Action onButtonClicked)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _text.SetText($"광고를 시청하고\n보상을 {Utility.SetStringColor("두배로", ColorType.Positive)} 받으시겠습니까?");
        _onAdButtonClicked = onButtonClicked;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(true);
        _feverLayout.SetActive(false);
    }

    public void ShowFeverPopup(Action onButtonClicked)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _text.SetText($"광고를 시청하고\n{Utility.SetStringColor("피버 타임", ColorType.Positive)}을 충전하시겠습니까?");
        _onAdButtonClicked = onButtonClicked;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(true);
    }
    
    public void ShowCustomerPopup(Action onButtonClicked)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _text.SetText($"광고를 시청하고\n{Utility.SetStringColor("손님을", ColorType.Positive)} 호출 하시겠습니까?");
        _onAdButtonClicked = onButtonClicked;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(true);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
    }


    private void AdButtonClicked()
    {
        _onAdButtonClicked?.Invoke();
        _uiNav.Pop("UIAd");
    }


}
