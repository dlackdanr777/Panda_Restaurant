using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System;


/// <summary>화면 전환 효과를 주는 싱글톤 매니저</summary>
public class FadeManager : MonoBehaviour
{

    public static FadeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("FadeManager");
                _instance = obj.AddComponent<FadeManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static FadeManager _instance;

    private FadeCanvas _fadeCanvas;
    private Image _fadeImage;
    private float _fadeDuration = 0.2f;
    private float _fadeOutWaitDuration = 0.2f;
    private Ease _fadeEase = Ease.Constant;
    private bool _isLoading;

    public event Action OnFadeInHandler;
    public event Action OnEndFadeInHandler;
    public event Action OnFadeOutHandler;
    public event Action OnEndFadeOutHandler;
    public event Action OnEndFirstLoadingHandler;

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _isLoading = false;
        _fadeCanvas = Instantiate(Resources.Load<FadeCanvas>("UI/FadeCanvas"), _instance.transform);
        _fadeImage = _fadeCanvas.FadeImage;
        _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 0);
        _fadeImage.gameObject.SetActive(false);
    }


    /// <summary>FadeIn효과를 주는 함수(duration이 0일 경우 기본 값, onComplete는 FadeIn효과가 종료된 후 실행)</summary>
    public void FadeIn(float duration = 0, Action onComplete = null)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _fadeImage.gameObject.SetActive(true);

        duration = duration <= 0 ? _fadeDuration : duration;
        _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 0);
        _fadeImage.TweenAlpha(1, duration, _fadeEase).OnComplete(() =>
        {
            onComplete?.Invoke();
            OnEndFadeInHandler?.Invoke();
        });

        OnFadeInHandler?.Invoke();
    }

    public void FadeSetActive(bool value)
    {
        _fadeImage.gameObject.SetActive(value);
        _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, value ? 1 : 0);
    }

    /// <summary>FadeOut효과를 주는 함수(duration이 0일 경우 기본 값, onComplete는 FadeIn효과가 종료된 후 실행)</summary>
    public void FadeOut(float duration = 0, float waitDuration = 0, Action onComplete = null)
    {
        _fadeImage.gameObject.SetActive(true);

        duration = duration == 0 ? _fadeDuration : duration;
        waitDuration = waitDuration <= 0 ? _fadeOutWaitDuration : waitDuration;

        _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 1);
        Tween.Wait(waitDuration, () =>
        {
            _fadeImage.TweenAlpha(0, duration, _fadeEase).OnComplete(() =>
            {
                onComplete?.Invoke();
                _isLoading = false;
                _fadeImage.gameObject.SetActive(false);
                OnEndFadeOutHandler?.Invoke();
            });
        });

        OnFadeOutHandler?.Invoke();
    }
}
