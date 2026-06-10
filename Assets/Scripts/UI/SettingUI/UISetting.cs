using BackEnd;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIsetting : MobileUIView
{

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private UISettingButton _alramButton;
    [SerializeField] private UISettingButton _musicButton;
    [SerializeField] private UISettingButton _soundEffectButton;
    [SerializeField] private UISettingUserId _userId;
    [SerializeField] private Button _homePageButton;
    [SerializeField] private Button _privacyButton;
    [SerializeField] private TextMeshProUGUI _versionText;
    [SerializeField] private UIButtonAndText _googleLinkButton;
    [SerializeField] private UITextAndText _googleLinkText;
    [SerializeField] private GoogleLoginManager _googleLoginManager;


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
        _alramButton.Init(() => OnVibrationButtonClicked(false), () => OnVibrationButtonClicked(true), SoundManager.Instance.IsVibration);
        _musicButton.Init(() => SoundManager.Instance.SetVolume(0, AudioType.BackgroundAudio), () => SoundManager.Instance.SetVolume(1, AudioType.BackgroundAudio), 0 < SoundManager.Instance.GetVolume(AudioType.BackgroundAudio));
        _soundEffectButton.Init(() => SoundManager.Instance.SetVolume(0, AudioType.EffectAudio), () => SoundManager.Instance.SetVolume(1, AudioType.EffectAudio), 0 < SoundManager.Instance.GetVolume(AudioType.EffectAudio));
        _userId.Init(BackendManager.Instance.IsLogin
            ? (string.IsNullOrEmpty(UserInfo.GamerId) ? Backend.UserNickName : UserInfo.GamerId)
            : "로그인안됨");
        _homePageButton.onClick.AddListener(OnHomepageButtonClicked);
        _privacyButton.onClick.AddListener(() => PopupManager.Instance.ShowDisplayText("현재 지원하지 않는 버튼입니다."));
        if (_googleLinkButton != null)
            _googleLinkButton.AddListener(OnGoogleLinkButtonClicked);
        UpdateGoogleLinkUI();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();
        _alramButton.IsOn(SoundManager.Instance.IsVibration);
        _musicButton.IsOn(0 < SoundManager.Instance.GetVolume(AudioType.BackgroundAudio));
        _soundEffectButton.IsOn(0 < SoundManager.Instance.GetVolume(AudioType.EffectAudio));
        _versionText.SetText($"Ver: {Application.version}");
        UpdateGoogleLinkUI();
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
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void OnVibrationButtonClicked(bool value)
    {
        SoundManager.Instance.SetVibration(value);

        if(value)
            Vibration.Vibrate(500);
    }

    private void UpdateGoogleLinkUI()
    {
        bool isLinked = GoogleLoginManager.GetLoginPreference() == GoogleLoginManager.LoginPreference.Google;
        if (_googleLinkButton != null)
            _googleLinkButton.gameObject.SetActive(true);
        if (_googleLinkText != null)
        {
            _googleLinkText.gameObject.SetActive(isLinked);
            if (isLinked)
            {
                string displayName = GoogleLoginManager.GetLinkedGoogleDisplayName();
                if (!string.IsNullOrEmpty(displayName))
                    _googleLinkText.SetText2(displayName);
            }
        }

        if(_userId != null)
            _userId.gameObject.SetActive(!isLinked);
    }


    private void OnHomepageButtonClicked()
    {
        Application.OpenURL("https://cafe.naver.com/everyonesrestaurant");
    }

#if UNITY_ANDROID
    private void OnGoogleLinkButtonClicked()
    {
        if (_googleLoginManager == null)
        {
            PopupManager.Instance.ShowDisplayText("GoogleLoginManager가 연결되지 않았습니다.");
            return;
        }

        _googleLoginManager.LinkGoogleAccount(
            onNewLink: () =>
            {
                // 이 구글 계정에 연동된 뒤끝 ID 없음 → 현재 계정과 연동 여부 확인
                BackendManager.Instance.ShowPopup("구글 연동", "연동하시겠습니까?");
                BackendManager.Instance.SetPopupButton1("예", () =>
                {
                    _googleLoginManager.ConfirmNewLink();
                    UpdateGoogleLinkUI();
                    PopupManager.Instance.ShowDisplayText("구글 연동이 완료되었습니다.");
                });
                BackendManager.Instance.SetPopupButton2("아니오", () =>
                {
                    _googleLoginManager.CancelNewLink();
                });
            },
            onExistingOtherAccount: () =>
            {
                // 이 구글 계정에 연동된 다른 뒤끝 ID 있음 → 해당 계정으로 전환 여부 확인
                BackendManager.Instance.ShowPopup("구글 연동", "연동된 계정이 있습니다.\n해당 계정으로 로그인하시겠습니까?");
                BackendManager.Instance.SetPopupButton1("예", () =>
                {
                    _googleLoginManager.SwitchToLinkedAccount(
                        onSuccess: () =>
                        {
                            Application.Quit();
                        },
                        onFail: () =>
                        {
                            PopupManager.Instance.ShowDisplayText("계정 전환에 실패했습니다.\n다시 시도해주세요.");
                        }
                    );
                });
                BackendManager.Instance.SetPopupButton2("아니오", null);
            },
            onAlreadyLinked: () =>
            {
                // 이미 현재 계정과 이 구글 계정이 연동되어 있음
                PopupManager.Instance.ShowDisplayText("이미 현재 계정과 연동된 구글 계정입니다.");
            },
            onFail: () =>
            {
                PopupManager.Instance.ShowDisplayText("구글 연동에 실패했습니다.\n다시 시도해주세요.");
            }
        );
    }
#else
    private void OnGoogleLinkButtonClicked()
    {
        PopupManager.Instance.ShowDisplayText("구글 연동은 Android 기기에서만 지원됩니다.");
    }
#endif
}
