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
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }
        _currentAdType = AdType.None;
        OnAddEvent(watchAdButton);
        _text.SetText("광고�??�청?�시겠습?�까?");
        _adButton.SetText("광고 ?�청");
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
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
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Dia;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 ?�청");
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
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
            _text.SetText($"광고�??�청?�시�?n{Utility.SetStringColor("보상", ColorType.Positive)}??받으?�겠?�니�?");
            _adButton.gameObject.SetActive(true);
        }
        else
        {
            _text.SetText("?�늘 보상??모두 ?�득?�어??");
            _adButton.gameObject.SetActive(false);
        }
    }


     public void ShowTipPopup(WatchAdButton watchAdButton)
    {
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Tip;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 ?�청");
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
        _defaultLayout.SetActive(false);
        _customerLayout.SetActive(false);
        _diaLayout.SetActive(false);
        _coinLayout.SetActive(true);
        _feverLayout.SetActive(false);
        _diaButton.gameObject.SetActive(false);
        _adCountText.gameObject.SetActive(false);
        _text.SetText($"광고�??�청?�시�?n{Utility.SetStringColor("팁", ColorType.Positive)}???�배�?받으?�겠?�니�?");
        _adButton.gameObject.SetActive(true);
    }

    public void ShowCoinPopup(WatchAdButton watchAdButton)
    {
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Coin;

        OnAddEvent(watchAdButton);
        _adButton.SetText("광고 ?�청");
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
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
            _text.SetText($"광고�??�청?�시�?n{Utility.SetStringColor("보상", ColorType.Positive)}??받으?�겠?�니�?");
            _adButton.gameObject.SetActive(true);
        }
        else
        {
            _text.SetText("?�늘 보상??모두 ?�득?�어??");
            _adButton.gameObject.SetActive(false);
        }
    }

    public void ShowFeverPopup(WatchAdButton watchAdButton)
    {
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Fever;

        OnAddEvent(watchAdButton);
        _text.SetText($"{Utility.SetStringColor("?�버 게이지", ColorType.Positive)}�?n?��? 충전?�시겠습?�까?");
        _adButton.SetText("광고 ?�청");
        _diaButton.SetText(_feverDia.ToString());
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
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
            _text.SetText("?�늘 보상??모두 ?�득?�어??");
            _adButton.gameObject.SetActive(false);
            _diaButton.gameObject.SetActive(false);
        }
        else
        {
            _text.SetText($"{Utility.SetStringColor("?�버 게이지", ColorType.Positive)}�?n?��? 충전?�시겠습?�까?");
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
        // ?��? ?�업???�려?�으�?중복 Push 방�?
        if (VisibleState == VisibleState.Appearing || VisibleState == VisibleState.Appeared)
        {
            return;
        }

        _currentAdType = AdType.Customer;

        OnAddEvent(watchAdButton);
        _text.SetText($"{Utility.SetStringColor("?�님", ColorType.Positive)}???�꺼번에\n?�출 ?�시겠습?�까?");
        _adButton.SetText("광고 ?�청");
        _diaButton.SetText(_customerDia.ToString());
        _currentWatchAdButton = watchAdButton;
        PushUIAd();
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
            _text.SetText("?�늘 보상??모두 ?�득?�어??");
            _adButton.gameObject.SetActive(false);
            _diaButton.gameObject.SetActive(false);
        }
        else
        {
            _text.SetText($"{Utility.SetStringColor("?�님", ColorType.Positive)}???�꺼번에\n?�출 ?�시겠습?�까?");
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
                _adButton.SetText("광고 ?�청");
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
                _adButton.SetText("광고 ?�청");

                _diaButton.Interactable(true);
                _diaButton.SetText(_customerDia.ToString());
            }
        }

        else
        {
            _adButton.Interactable(true);
            _adButton.SetText("광고 ?�청");
        }
    }

    private void PushUIAd()
    {
        if (_uiNav != null)
        {
            _uiNav.Push("UIAd");
        }
        else
        {
            DebugLog.LogWarning("[UIAdPopup] _uiNav??null?�니?? MobileUINavigation??UIAd가 ?�록?��? ?�았?�니?? Show()�?직접 ?�출?�니??");
            Show();
        }
    }

    private void PopUIAd()
    {
        if (_uiNav != null)
            _uiNav.PopNoAnime("UIAd");
        else
            Hide();
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
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한 횟수를 모두 사용했습니다.");
            return;
        }
        else if (_currentAdType == AdType.Customer && ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerAdCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한 횟수를 모두 사용했습니다.");
            return;
        }
        else if (_currentAdType == AdType.Dia && ConstValue.DAILY_AD_DIA_REWARD_COUNT <= UserInfo.DailyAdDiaRewardCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한 횟수를 모두 사용했습니다.");
            return;
        }
        else if (_currentAdType == AdType.Coin && ConstValue.DAILY_AD_GOLD_REWARD_COUNT <= UserInfo.DailyAdGoldRewardCount)
        {
            PopupManager.Instance.ShowDisplayText("오늘 시청 가능한 횟수를 모두 사용했습니다.");
            return;
        }

        if (_currentAdType == AdType.Fever && FeverSystem.CurrentMaxFeverGauge <= FeverSystem.FeverGauge)
        {
            PopupManager.Instance.ShowDisplayText("?�버게이지가 가??�??�습?�다.");
            return;
        }

        else if(_currentAdType == AdType.Customer && CustomerController.IsMaxCount)
        {
            PopupManager.Instance.ShowDisplayText("?�님??가??�??�습?�다.");
            return;
        }

        if (_currentWatchAdButton == null)
        {
            Debug.LogError("[UIAdPopup] _currentWatchAdButton가 null?�니??");
            return;
        }

        PopEnabled = false;
        _dontTouchArea.SetActive(true); // OnClickAd ?�전??블로�??�성??(콜백 ?�서 ?�전)
        _currentWatchAdButton.OnClickAd();
    }

    private void OnAdRewarded()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        OnRemoveEvent();
        PopUIAd();
    }

    private void OnAdFailed()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        OnRemoveEvent();
        PopUIAd();
        
        // 광고 로드 ?�패 메시지 ?�시
        PopupManager.Instance?.ShowDisplayText("광고�?불러?????�습?�다.\n?�시 ???�시 ?�도?�주?�요.");
    }

    private void OnAdClosed()
    {
        PopEnabled = true;
        _dontTouchArea.SetActive(false);
        OnRemoveEvent();
        PopUIAd();
    }

    private void OnDiaButtonClicked()
    {
        DebugLog.Log(UserInfo.FeverDiaCount);
        if (_currentAdType == AdType.Fever && ConstValue.AD_FEVER_COUNT <= UserInfo.FeverDiaCount)
        {
            PopupManager.Instance.ShowDisplayText("?�늘 ?�용?????�는\n?�이???�용 ?�수�?모두 ?�용?�어??");
            return;
        }
        else if (_currentAdType == AdType.Customer && ConstValue.AD_CUSTOMER_COUNT <= UserInfo.AddCustomerDiaCount)
        {
            PopupManager.Instance.ShowDisplayText("?�늘 ?�용?????�는\n?�이???�용 ?�수�?모두 ?�용?�어??");
            return;
        }
              
        if (_currentAdType == AdType.Fever && FeverSystem.CurrentMaxFeverGauge <= FeverSystem.FeverGauge)
        {
            PopupManager.Instance.ShowDisplayText("?�버게이지가 가??�??�습?�다.");
            return;
        }
        else if(_currentAdType == AdType.Customer && CustomerController.IsMaxCount)
        {
            PopupManager.Instance.ShowDisplayText("?�님??가??�??�습?�다.");
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
        PopUIAd();
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }
}
