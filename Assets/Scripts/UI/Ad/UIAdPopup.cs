using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using UnityEngine.UI;
using TMPro;
using System;
using Unity.VisualScripting.Antlr3.Runtime.Tree;

public class UIAdPopup : MobileUIView
{
    private const int _customerDia = 5;
    private const int _feverDia = 3;

    private enum AdType
    {
        None,
        Dia,
        Coin,
        Fever,
        Customer,
        Tip,
    }

    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private UIButtonAndText _adButton;
    [SerializeField] private UIButtonAndText _diaButton;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private GameObject _defaultLayout;
    [SerializeField] private GameObject _diaLayout;
    [SerializeField] private GameObject _coinLayout;
    [SerializeField] private GameObject _feverLayout;
    [SerializeField] private GameObject _customerLayout;

    [SerializeField] private Sprite _enabledDiaSprite;
    [SerializeField] private Sprite _disabledDiaSprite;
    [SerializeField] private Image _diaButtonImage;
    [SerializeField] private TextMeshProUGUI _adCountText;
    [SerializeField] private TextMeshProUGUI _diaCountText;



    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private WatchAdButton _currentWatchAdButton;
    private AdType _currentAdType = AdType.None;



    public override void Init()
    {
        _adButton.AddListener(AdButtonClicked);
        _diaButton.AddListener(OnDiaButtonClicked);
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

    public void SetAdButtonText(string text)
    {
        _adButton.SetText(text);
    }

    public void SetAdButtonInteractable(bool interactable)
    {
        _adButton.Interactable(interactable);
    }

    public void SetDiaButtonInteractable(bool interactable)
    {
        _diaButton.Interactable(interactable);
    }


    public void ShowPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _currentAdType = AdType.None;
        OnAddEvent(watchAdButton);
        _text.SetText("광고를 시청하시겠습니까?");
        _adButton.SetText("광고 시청");
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(true);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(false);
        _adCountText.gameObject.SetActive(false);
    }

    public void ShowDiaPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Dia;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 시청");
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(true);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(false);
        _adCountText.gameObject.SetActive(true);
        _adCountText.SetText((ConstValue.DAILY_AD_DIA_REWARD_COUNT - UserInfo.DailyAdDiaRewardCount) + "/" + ConstValue.DAILY_AD_DIA_REWARD_COUNT.ToString());
        if (UserInfo.DailyAdDiaRewardCount < ConstValue.DAILY_AD_DIA_REWARD_COUNT)
        {
            _text.SetText($"광고를 시청하시고\n{Utility.SetStringColor("보상", ColorType.Positive)}을 받으시겠습니까?");
            _adButton.gameObject.SetActive(true);
        }
        else
        {
            _text.SetText("오늘 보상을 모두 획득했어요!");
            _adButton.gameObject.SetActive(false);
        }
    }


     public void ShowTipPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Tip;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 시청");
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(true);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(false);
        _adCountText.gameObject.SetActive(false);
        _text.SetText($"광고를 시청하시고\n{Utility.SetStringColor("팁", ColorType.Positive)}을 두배로 받으시겠습니까?");
        _adButton.gameObject.SetActive(true);
    }

    public void ShowCoinPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Coin;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 시청");
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(true);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(false);
        _adCountText.gameObject.SetActive(true);
        _adCountText.SetText((ConstValue.DAILY_AD_GOLD_REWARD_COUNT - UserInfo.DailyAdGoldRewardCount) + "/" + ConstValue.DAILY_AD_GOLD_REWARD_COUNT.ToString());
        if(UserInfo.DailyAdGoldRewardCount < ConstValue.DAILY_AD_GOLD_REWARD_COUNT)
        {
            _text.SetText($"광고를 시청하시고\n{Utility.SetStringColor("보상", ColorType.Positive)}을 받으시겠습니까?");
            _adButton.gameObject.SetActive(true);
        }
        else
        {
            _text.SetText("오늘 보상을 모두 획득했어요!");
            _adButton.gameObject.SetActive(false);
        }
    }

    public void ShowFeverPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Fever;

        OnAddEvent(watchAdButton);
        _text.SetText($"{Utility.SetStringColor("피버 게이지", ColorType.Positive)}를\n전부 충전하시겠습니까?");
        _adButton.SetText("광고 시청");
        _diaButton.SetText(_feverDia.ToString());
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(true);
        _diaButton.gameObject.SetActive(true);
        _adCountText.gameObject.SetActive(true);
        _diaCountText.gameObject.SetActive(true);
        _adCountText.SetText((ConstValue.AD_FEVER_COUNT - UserInfo.FeverAdCount) + "/" + ConstValue.AD_FEVER_COUNT.ToString());
        _diaCountText.SetText((ConstValue.AD_FEVER_COUNT - UserInfo.FeverDiaCount) + "/" + ConstValue.AD_FEVER_COUNT.ToString());


        if (ConstValue.AD_FEVER_COUNT <= UserInfo.FeverDiaCount && ConstValue.AD_FEVER_COUNT <= UserInfo.FeverAdCount)
        {
            _text.SetText("오늘 보상을 모두 획득했어요!");
            _adButton.gameObject.SetActive(false);
            _diaButton.gameObject.SetActive(false);
        }
        else
        {
            _text.SetText($"{Utility.SetStringColor("피버 게이지", ColorType.Positive)}를\n전부 충전하시겠습니까?");
            _adButton.gameObject.SetActive(true);
            _diaButton.gameObject.SetActive(true);
        }

        if (UserInfo.IsDiaValid(_feverDia))
        {
            _diaButtonImage.sprite = _enabledDiaSprite;
            _diaButton.Interactable(true);
        }
        else
        {
            _diaButtonImage.sprite = _disabledDiaSprite;
            _diaButton.Interactable(false);
        }
    }

    public void ShowCustomerPopup(WatchAdButton watchAdButton)
    {
        // 이미 팝업이 열려있으면 중복 Push 방지
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Customer;

        OnAddEvent(watchAdButton);
        _text.SetText($"{Utility.SetStringColor("손님", ColorType.Positive)}을 한꺼번에\n호출 하시겠습니까?");
        _adButton.SetText("광고 시청");
        _diaButton.SetText(_customerDia.ToString());
        _currentWatchAdButton = watchAdButton;
        _uiNav.Push("UIAd");
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(true);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(false);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(true);
        _adCountText.gameObject.SetActive(true);
        _diaCountText.gameObject.SetActive(true);
        _adCountText.SetText((ConstValue.AD_CUSTOMER_COUNT - UserInfo.AddCustomerAdCount) + "/" + ConstValue.AD_CUSTOMER_COUNT.ToString());
        _diaCountText.SetText((ConstValue.AD_CUSTOMER_COUNT - UserInfo.AddCustomerDiaCount) + "/" + ConstValue.AD_CUSTOMER_COUNT.ToString());

        if (ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerAdCount && ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerDiaCount)
        {
            _text.SetText("오늘 보상을 모두 획득했어요!");
            _adButton.gameObject.SetActive(false);
            _diaButton.gameObject.SetActive(false);
        }
        else
        {
            _text.SetText($"{Utility.SetStringColor("손님", ColorType.Positive)}을 한꺼번에\n호출 하시겠습니까?");
            _adButton.gameObject.SetActive(true);
            _diaButton.gameObject.SetActive(true);
        }
            

        if(UserInfo.IsDiaValid(_customerDia))
        {
            _diaButtonImage.sprite = _enabledDiaSprite;
            _diaButton.Interactable(true);
        }
        else
        {
            _diaButtonImage.sprite = _disabledDiaSprite;
            _diaButton.Interactable(false);
        }
    }

    private void FixedUpdate()
    {
        if (_currentAdType == AdType.Fever)
        {
            if (!TimeManager.Instance.IsAddTime(ConstValue.AD_TIME_FEVER))
            {
                SetAdButtonInteractable(false);
                SetDiaButtonInteractable(false);
                int seconds = TimeManager.Instance.GetTime(ConstValue.AD_TIME_FEVER);
                int minutes = seconds / 60;
                int remainingSeconds = seconds % 60;
                _adButton.SetText(string.Format("{0:D2}:{1:D2}", minutes, remainingSeconds));
                _diaButton.SetText(string.Format("{0:D2}:{1:D2}", minutes, remainingSeconds));
            }
            else
            {
                _diaButton.Interactable(true);
                _adButton.Interactable(true);
                _adButton.SetText("광고 시청");
                _diaButton.SetText(_feverDia.ToString());
            }
        }

        else if (_currentAdType == AdType.Customer)
        {
            if (!TimeManager.Instance.IsAddTime(ConstValue.AD_TIME_CUSTOMER))
            {
                SetDiaButtonInteractable(false);
                SetAdButtonInteractable(false);
                int seconds = TimeManager.Instance.GetTime(ConstValue.AD_TIME_CUSTOMER);
                int minutes = seconds / 60;
                int remainingSeconds = seconds % 60;
                _adButton.SetText(string.Format("{0:D2}:{1:D2}", minutes, remainingSeconds));
                _diaButton.SetText(string.Format("{0:D2}:{1:D2}", minutes, remainingSeconds));
            }
            else
            {
                _adButton.Interactable(true);
                _adButton.SetText("광고 시청");

                _diaButton.Interactable(true);
                _diaButton.SetText(_customerDia.ToString());
            }
        }

        else
        {
            _adButton.Interactable(true);
            _adButton.SetText("광고 시청");
        }
    }

    private void OnAddEvent(WatchAdButton watchAdButton)
    {
        OnRemoveEvent();
        watchAdButton.OnAdRewarded += OnAdRewarded;
        watchAdButton.OnAdDisplayFailed += OnAdFailed;
        watchAdButton.OnAdClosed += OnAdClosed;
    }
    
    private void OnRemoveEvent()
    {
        if(_currentWatchAdButton == null)
            return;

        _currentWatchAdButton.OnAdRewarded -= OnAdRewarded;
        _currentWatchAdButton.OnAdDisplayFailed -= OnAdFailed;
        _currentWatchAdButton.OnAdClosed -= OnAdClosed;
    }


    private void AdButtonClicked()
    {
        if (_currentAdType == AdType.Fever && ConstValue.AD_FEVER_COUNT <= UserInfo.FeverAdCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한\n 광고를 모두 사용했어요");
            return;
        }

        else if (_currentAdType == AdType.Customer && ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerAdCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한\n 광고를 모두 사용했어요");
            return;
        }
        else if (_currentAdType == AdType.Dia && ConstValue.DAILY_AD_DIA_REWARD_COUNT <= UserInfo.DailyAdDiaRewardCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한\n 광고를 모두 사용했어요");
            return;
        }
        else if (_currentAdType == AdType.Coin && ConstValue.DAILY_AD_GOLD_REWARD_COUNT <= UserInfo.DailyAdGoldRewardCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한\n 광고를 모두 사용했어요");
            return;

        }

        PopEnabled = false;
        _currentWatchAdButton.OnClickAd();
        _dontTouchArea.SetActive(true);
    }

    private void OnAdRewarded()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        _uiNav.PopNoAnime("UIAd");
    }

    private void OnAdFailed()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        _uiNav.PopNoAnime("UIAd");
        
        // 광고 로드 실패 메시지 표시
        PopupManager.Instance?.ShowDisplayText("광고를 불러올 수 없습니다.\n잠시 후 다시 시도해주세요.");
    }

    private void OnAdClosed()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        _uiNav.PopNoAnime("UIAd");
    }

    private void OnDiaButtonClicked()
    {
        DebugLog.Log(UserInfo.FeverDiaCount);
        if (_currentAdType == AdType.Fever && ConstValue.AD_FEVER_COUNT <= UserInfo.FeverDiaCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 사용할 수 있는\n다이아 사용 횟수를 모두 사용했어요.");
            return;
        }
        
        else if(_currentAdType == AdType.Customer && ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerDiaCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 사용할 수 있는\n다이아 사용 횟수를 모두 사용했어요.");
            return;
        }

        int needDia = int.Parse(_diaButton.GetText());
        if (!UserInfo.IsDiaValid(needDia))
        {
            PopupManager.Instance.ShowTextLackDia();
            return;
        }
        PopEnabled = true;
        UserInfo.AddDia(-needDia);
        _currentWatchAdButton.DiaRewarded();
        _uiNav.PopNoAnime("UIAd");
    }

}
