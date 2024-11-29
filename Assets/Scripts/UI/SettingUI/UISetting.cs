using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;

public class UIsetting : MobileUIView
{

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UISettingButton _musicButton;
    [SerializeField] private UISettingButton _soundEffectButton;


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
        _musicButton.Init(() => SoundManager.Instance.SetVolume(0, AudioType.BackgroundAudio), () => SoundManager.Instance.SetVolume(1, AudioType.BackgroundAudio), 0 < SoundManager.Instance.GetVolume(AudioType.BackgroundAudio));
        _soundEffectButton.Init(() => SoundManager.Instance.SetVolume(0, AudioType.EffectAudio), () => SoundManager.Instance.SetVolume(1, AudioType.EffectAudio), 0 < SoundManager.Instance.GetVolume(AudioType.EffectAudio));
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();
        _musicButton.IsOn(0 < SoundManager.Instance.GetVolume(AudioType.BackgroundAudio));
        _soundEffectButton.IsOn(0 < SoundManager.Instance.GetVolume(AudioType.EffectAudio));
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
}
