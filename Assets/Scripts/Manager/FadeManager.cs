using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;
using System;
using System.Collections;
using System.Collections.Generic;


/// <summary>ȭ�� ��ȯ ȿ���� �ִ� �̱��� �Ŵ���</summary>
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

    // Scene views that are still preparing their first screen during the loading fade (e.g. UIGacha prewarm).
    // Static so releasing never creates the singleton; destroyed Unity owners are pruned automatically.
    private static readonly HashSet<object> _fadeOutHolds = new HashSet<object>();
    private static readonly Predicate<object> _isDestroyedOwner = owner => owner is UnityEngine.Object unityObject && unityObject == null;
    private const float MaxFadeOutHoldSeconds = 2f;

    public static void HoldFadeOut(object owner)
    {
        if (owner != null) _fadeOutHolds.Add(owner);
    }

    public static void ReleaseFadeOut(object owner)
    {
        if (owner != null && _fadeOutHolds.Count != 0) _fadeOutHolds.Remove(owner);
    }

    private static bool HasFadeOutHolds
    {
        get
        {
            if (_fadeOutHolds.Count != 0) _fadeOutHolds.RemoveWhere(_isDestroyedOwner);
            return _fadeOutHolds.Count != 0;
        }
    }

    /// <summary>Scene-entry FadeOut that keeps the input-blocking fade until the scene is active and registered holds
    /// are released, for at most MaxFadeOutHoldSeconds in total.</summary>
    public void FadeOutWhenReady(Func<bool> isSceneActivated = null)
    {
        StartCoroutine(FadeOutWhenReadyRoutine(isSceneActivated));
    }

    private IEnumerator FadeOutWhenReadyRoutine(Func<bool> isSceneActivated)
    {
        float deadline = Time.unscaledTime + MaxFadeOutHoldSeconds;
        while (isSceneActivated != null && !isSceneActivated() && Time.unscaledTime < deadline)
            yield return null;
        // Scene Start() (where holds are registered) has run by the next frame.
        yield return null;
        while (HasFadeOutHolds && Time.unscaledTime < deadline)
            yield return null;
        // A hold that outlived the bound must not delay any later scene entry.
        _fadeOutHolds.Clear();
        FadeOut();
    }

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


    /// <summary>FadeInȿ���� �ִ� �Լ�(duration�� 0�� ��� �⺻ ��, onComplete�� FadeInȿ���� ����� �� ����)</summary>
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

    /// <summary>FadeOutȿ���� �ִ� �Լ�(duration�� 0�� ��� �⺻ ��, onComplete�� FadeInȿ���� ����� �� ����)</summary>
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
