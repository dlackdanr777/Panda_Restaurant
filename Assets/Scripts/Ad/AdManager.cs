using System.Collections;
using System.Collections.Generic;
using Unity.Services.LevelPlay;
using UnityEngine;

public enum AdType
{
    Reward,
    Interstitial
}

/// <summary>
/// 광고 SDK 객체와 콜백을 독점 소유하는 싱글톤 매니저.
/// 
/// 핵심 원칙:
///  - LevelPlay.OnInitSuccess 이후에만 광고 객체 생성 및 LoadAd 호출
///  - RewardSession으로 OnClosed/OnRewarded 콜백 순서에 무관한 보상 처리
///  - adUnitId당 SDK 객체 1개, 콜백 소유자는 항상 AdManager
///  - 버튼(WatchAdButton)은 ShowAd() 호출 시 콜백만 전달
/// </summary>
[DefaultExecutionOrder(-100)]
public class AdManager : MonoBehaviour
{
    // ── 빌드 식별자 (로그에서 APK 버전 확인용) ──
    private const string BuildStamp = "ADS_FIX_20260729_V3";

    private static AdManager _instance;
    public static AdManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AdManager");
                _instance = go.AddComponent<AdManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;
    public static bool IsAdPlaying => _instance != null && _instance._activeSession != null;

    // ── SDK 상태 ──
    private bool _sdkReady;
    private bool _testSuiteMode;

    // ── SDK 초기화 전 대기 목록 ──
    private readonly Dictionary<string, AdType> _pendingUnits = new Dictionary<string, AdType>();

    // ── 광고 객체 (SDK 초기화 완료 후 생성) ──
    private readonly Dictionary<string, LevelPlayRewardedAd>     _rewardedAds     = new Dictionary<string, LevelPlayRewardedAd>();
    private readonly Dictionary<string, LevelPlayInterstitialAd> _interstitialAds = new Dictionary<string, LevelPlayInterstitialAd>();
    private readonly Dictionary<string, AdType>  _adTypes     = new Dictionary<string, AdType>();
    private readonly Dictionary<string, bool>    _isLoading   = new Dictionary<string, bool>();
    private readonly Dictionary<string, float>   _loadedTime  = new Dictionary<string, float>();
    private readonly Dictionary<string, int>     _retryCount  = new Dictionary<string, int>();
    private readonly Dictionary<string, float>   _lastFailedTime  = new Dictionary<string, float>();
    private readonly Dictionary<string, Coroutine> _retryCoroutines = new Dictionary<string, Coroutine>();

    // ── 재시도 상수 ──
    private const float RetryDelay   = 1f;
    private const int   MaxRetry     = 3;
    private const float LoadCooldown = 30f;
    private const float AdMaxAge     = 300f; // 5분

    // ── SDK 초기화 전 ShowAd 대기 요청 ──
    private class PendingShowRequest
    {
        public string       AdUnitId;
        public System.Action OnDisplayed;
        public System.Action OnReward;
        public System.Action OnClosed;
        public System.Action OnFailed;
    }
    private PendingShowRequest _pendingShow;
    private Coroutine          _sdkInitTimeoutCoroutine;
    private const float        SdkInitTimeoutSeconds = 15f;

    // ── 활성 광고 세션 (동시 1개) ──
    private RewardSession _activeSession;

    private class RewardSession
    {
        public string   AdUnitId;
        public bool     ClosedReceived;
        public bool     RewardGranted;
        public System.Action OnDisplayedCallback;
        public System.Action OnRewardCallback;
        public System.Action OnClosedCallback;
        public System.Action OnFailedCallback;
        public Coroutine TimeoutCoroutine;
    }

    // ── 생명주기 ──

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.LogError($"[ADS_BOOT] {BuildStamp} AdManager.Awake");

        // SDK 초기화 완료 이벤트 구독 (LevelPlay.Init() 호출 전에 등록해야 함)
        LevelPlay.OnInitSuccess += OnSdkInitSuccess;
        LevelPlay.OnInitFailed  += OnSdkInitFailed;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            LevelPlay.OnInitSuccess -= OnSdkInitSuccess;
            LevelPlay.OnInitFailed  -= OnSdkInitFailed;
        }
    }

    // ── SDK 초기화 콜백 ──

    /// <summary>LevelPlayBoot에서 Init 전에 호출 — Test Suite 모드 설정</summary>
    public void SetTestSuiteMode(bool enabled)
    {
        _testSuiteMode = enabled;
        Debug.Log($"[AdManager] TestSuiteMode = {enabled}");
    }

    private void OnSdkInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[AdManager] SDK 초기화 완료 — 광고 객체 생성 시작");
        _sdkReady = true;

        // 타임아웃 코루틴 취소
        if (_sdkInitTimeoutCoroutine != null)
        {
            StopCoroutine(_sdkInitTimeoutCoroutine);
            _sdkInitTimeoutCoroutine = null;
        }

        if (_testSuiteMode)
        {
            Debug.Log("[AdManager] Test Suite 모드 — 광고 로드 건너뜀");
            _pendingUnits.Clear();
            CancelPendingShow();
            return;
        }

        foreach (var kv in _pendingUnits)
            CreateAndPreloadUnit(kv.Key, kv.Value);
        _pendingUnits.Clear();

        // SDK 초기화 전에 요청된 ShowAd 실행 (한 프레임 후)
        if (_pendingShow != null)
        {
            var ps = _pendingShow;
            _pendingShow = null;
            StartCoroutine(ExecutePendingShow(ps));
        }
    }

    private void OnSdkInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[AdManager] SDK 초기화 실패 — 대기 요청 취소: [{error.ErrorCode}] {error.ErrorMessage}");
        CancelPendingShow();
    }

    private void CancelPendingShow()
    {
        if (_sdkInitTimeoutCoroutine != null)
        {
            StopCoroutine(_sdkInitTimeoutCoroutine);
            _sdkInitTimeoutCoroutine = null;
        }
        if (_pendingShow != null)
        {
            var ps = _pendingShow;
            _pendingShow = null;
            ps.OnFailed?.Invoke();
        }
    }

    private IEnumerator SdkInitTimeout()
    {
        yield return new WaitForSeconds(SdkInitTimeoutSeconds);
        Debug.LogWarning($"[AdManager] SDK 초기화 타임아웃 ({SdkInitTimeoutSeconds}초) — 광고 대기 취소");
        _sdkInitTimeoutCoroutine = null;
        CancelPendingShow();
    }

    private IEnumerator ExecutePendingShow(PendingShowRequest ps)
    {
        yield return null; // 한 프레임 대기 (광고 객체 초기화 완료 보장)
        ShowAd(ps.AdUnitId, ps.OnDisplayed, ps.OnReward, ps.OnClosed, ps.OnFailed);
    }

    // ── Public API ──

    /// <summary>
    /// WatchAdButton.Awake()에서 호출.
    /// SDK 준비 전이면 대기열에 등록, 준비됐으면 즉시 생성.
    /// </summary>
    public void PrepareAdUnit(string adUnitId, AdType adType)
    {
        if (_adTypes.ContainsKey(adUnitId)) return; // 이미 등록됨

        if (_sdkReady && !_testSuiteMode)
            CreateAndPreloadUnit(adUnitId, adType);
        else
            _pendingUnits[adUnitId] = adType;
    }

    /// <summary>
    /// 광고 버튼 클릭 시 호출. 콜백은 이 시점에 전달.
    /// onDisplayed: 광고가 화면에 표시됐을 때
    /// onReward   : 보상 지급 시 (OnAdRewarded)
    /// onClosed   : 팝업 닫기 트리거 (OnAdClosed 또는 스킵 확정 후)
    /// onFailed   : 로드·표시 실패 시
    /// </summary>
    public void ShowAd(string adUnitId,
        System.Action onDisplayed,
        System.Action onReward,
        System.Action onClosed,
        System.Action onFailed)
    {
        if (!_sdkReady)
        {
            // SDK가 아직 초기화되지 않았으면 재시도를 요청합니다.
            if (LevelPlayBoot.Instance != null)
                LevelPlayBoot.Instance.EnsureInitialized();
            else
                Debug.LogError("[AdManager] LevelPlayBoot 인스턴스가 없습니다.");

            if (_pendingShow != null)
            {
                Debug.LogWarning("[AdManager] ShowAd 실패 — SDK 초기화 대기 요청이 이미 있음");
                onFailed?.Invoke();
                return;
            }
            Debug.LogWarning($"[AdManager] ShowAd — SDK 초기화 대기 중 (최대 {SdkInitTimeoutSeconds}초): {adUnitId}");
            _pendingShow = new PendingShowRequest
            {
                AdUnitId    = adUnitId,
                OnDisplayed = onDisplayed,
                OnReward    = onReward,
                OnClosed    = onClosed,
                OnFailed    = onFailed,
            };
            _sdkInitTimeoutCoroutine = StartCoroutine(SdkInitTimeout());
            return;
        }
        if (_testSuiteMode)
        {
            Debug.LogWarning("[AdManager] ShowAd 실패 — Test Suite 모드");
            onFailed?.Invoke(); return;
        }
        if (_activeSession != null)
        {
            Debug.LogWarning("[AdManager] ShowAd 실패 — 이미 광고 재생 중");
            onFailed?.Invoke(); return;
        }
        if (!_adTypes.ContainsKey(adUnitId))
        {
            Debug.LogError($"[AdManager] ShowAd 실패 — 미등록 adUnitId: {adUnitId}");
            onFailed?.Invoke(); return;
        }

        // 쿨다운 중이면 즉시 실패
        if (_lastFailedTime.TryGetValue(adUnitId, out var ft) && Time.time - ft < LoadCooldown)
        {
            Debug.LogWarning($"[AdManager] ShowAd 실패 — 로드 쿨다운 중: {adUnitId}");
            onFailed?.Invoke(); return;
        }

        _activeSession = new RewardSession
        {
            AdUnitId          = adUnitId,
            OnDisplayedCallback = onDisplayed,
            OnRewardCallback  = onReward,
            OnClosedCallback  = onClosed,
            OnFailedCallback  = onFailed,
        };

        if (IsAdReady(adUnitId))
            ShowAdInternal(adUnitId);
        else
        {
            if (!_isLoading[adUnitId])
                LoadAd(adUnitId);
            Debug.Log($"[AdManager] ShowAd — 로드 완료 후 자동 표시 대기: {adUnitId}");
        }
    }

    public bool IsAdReady(string adUnitId)
    {
        if (!_adTypes.ContainsKey(adUnitId)) return false;

        bool ready = _adTypes[adUnitId] == AdType.Reward
            ? _rewardedAds.TryGetValue(adUnitId, out var ra) && ra.IsAdReady()
            : _interstitialAds.TryGetValue(adUnitId, out var ia) && ia.IsAdReady();

        if (ready && _loadedTime.TryGetValue(adUnitId, out var lt) && lt > 0f)
        {
            float age = Time.realtimeSinceStartup - lt;
            if (age > AdMaxAge)
            {
                Debug.Log($"[AdManager] 광고 만료 ({age:F0}초 경과) — 재로드: {adUnitId}");
                return false;
            }
        }
        return ready;
    }

    public bool IsLoading(string adUnitId)
        => _isLoading.TryGetValue(adUnitId, out var v) && v;

    // ── 내부 로드·표시 ──

    private void CreateAndPreloadUnit(string adUnitId, AdType adType)
    {
        if (_adTypes.ContainsKey(adUnitId)) return;

        _adTypes[adUnitId]       = adType;
        _isLoading[adUnitId]     = false;
        _loadedTime[adUnitId]    = 0f;
        _retryCount[adUnitId]    = 0;
        _lastFailedTime[adUnitId] = -999f;

        if (adType == AdType.Reward)
        {
            var ad = new LevelPlayRewardedAd(adUnitId);
            ad.OnAdLoaded       += info        => OnAdLoaded(adUnitId, info);
            ad.OnAdLoadFailed   += err         => OnAdLoadFailed(adUnitId, err);
            ad.OnAdDisplayed    += info        => OnAdDisplayed(adUnitId, info);
            ad.OnAdDisplayFailed += (info, err) => OnAdDisplayFailed(adUnitId, info, err);
            ad.OnAdClosed       += info        => OnAdClosed(adUnitId, info);
            ad.OnAdRewarded     += (info, rew) => OnAdRewarded(adUnitId, info, rew);
            _rewardedAds[adUnitId] = ad;
        }
        else
        {
            var ad = new LevelPlayInterstitialAd(adUnitId);
            ad.OnAdLoaded       += info        => OnAdLoaded(adUnitId, info);
            ad.OnAdLoadFailed   += err         => OnAdLoadFailed(adUnitId, err);
            ad.OnAdDisplayed    += info        => OnAdDisplayed(adUnitId, info);
            ad.OnAdDisplayFailed += (info, err) => OnAdDisplayFailed(adUnitId, info, err);
            ad.OnAdClosed       += info        => OnAdClosed(adUnitId, info);
            _interstitialAds[adUnitId] = ad;
        }

        Debug.Log($"[AdManager] 광고 객체 생성: {adUnitId} ({adType})");
        LoadAd(adUnitId);
    }

    private void LoadAd(string adUnitId)
    {
        if (!_adTypes.ContainsKey(adUnitId)) return;
        if (_lastFailedTime.TryGetValue(adUnitId, out var ft) && Time.time - ft < LoadCooldown) return;
        if (_isLoading.TryGetValue(adUnitId, out var loading) && loading) return;

        _isLoading[adUnitId] = true;
        Debug.Log($"[AdManager] LoadAd: {adUnitId}");

        if (_adTypes[adUnitId] == AdType.Reward)
            _rewardedAds[adUnitId].LoadAd();
        else
            _interstitialAds[adUnitId].LoadAd();
    }

    private void ShowAdInternal(string adUnitId)
    {
        Debug.Log($"[AdManager] ShowAdInternal: {adUnitId}");
        try
        {
            if (_adTypes[adUnitId] == AdType.Reward)
                _rewardedAds[adUnitId].ShowAd();
            else
                _interstitialAds[adUnitId].ShowAd();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdManager] ShowAd 예외: {e.Message}");
            var s = _activeSession;
            _activeSession = null;
            s?.OnFailedCallback?.Invoke();
            LoadAd(adUnitId);
        }
    }

    // ── SDK 콜백 (AdManager 독점 소유) ──

    private void OnAdLoaded(string adUnitId, LevelPlayAdInfo info)
    {
        _isLoading[adUnitId]      = false;
        _loadedTime[adUnitId]     = Time.realtimeSinceStartup;
        _retryCount[adUnitId]     = 0;
        _lastFailedTime[adUnitId] = -999f;
        Debug.Log($"[AdManager] 광고 로드 완료: {adUnitId} ({info.AdNetwork})");

        // 이 유닛을 기다리는 활성 세션이 있으면 바로 표시
        if (_activeSession?.AdUnitId == adUnitId)
            ShowAdInternal(adUnitId);
    }

    private void OnAdLoadFailed(string adUnitId, LevelPlayAdError error)
    {
        _isLoading[adUnitId] = false;
        Debug.LogError($"[AdManager] 광고 로드 실패: {adUnitId} [{error.ErrorCode}] {error.ErrorMessage}");

        // 세션이 이 유닛을 기다리던 중이면 즉시 실패 콜백
        if (_activeSession?.AdUnitId == adUnitId)
        {
            var s = _activeSession;
            _activeSession = null;
            s.OnFailedCallback?.Invoke();
            return;
        }

        // 백그라운드 프리로드 재시도
        _retryCount[adUnitId]++;
        if (_retryCount[adUnitId] <= MaxRetry)
        {
            if (_retryCoroutines.TryGetValue(adUnitId, out var prev) && prev != null)
                StopCoroutine(prev);
            _retryCoroutines[adUnitId] = StartCoroutine(RetryLoad(adUnitId));
        }
        else
        {
            _retryCount[adUnitId]     = 0;
            _lastFailedTime[adUnitId] = Time.time;
            Debug.Log($"[AdManager] 최대 재시도 초과 — {LoadCooldown}초 쿨다운: {adUnitId}");
        }
    }

    private IEnumerator RetryLoad(string adUnitId)
    {
        yield return new WaitForSeconds(RetryDelay);
        _retryCoroutines.Remove(adUnitId);
        LoadAd(adUnitId);
    }

    private void OnAdDisplayed(string adUnitId, LevelPlayAdInfo info)
    {
        Debug.Log($"[AdManager] 광고 표시됨: {adUnitId}");
        if (_activeSession?.AdUnitId == adUnitId)
            _activeSession.OnDisplayedCallback?.Invoke();
    }

    private void OnAdDisplayFailed(string adUnitId, LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogError($"[AdManager] 광고 표시 실패: {adUnitId} — {error}");
        if (_activeSession?.AdUnitId == adUnitId)
        {
            var s = _activeSession;
            _activeSession = null;
            s.OnFailedCallback?.Invoke();
        }
        LoadAd(adUnitId);
    }

    private void OnAdClosed(string adUnitId, LevelPlayAdInfo info)
    {
        Debug.Log($"[AdManager] 광고 닫힘: {adUnitId}");
        if (_activeSession?.AdUnitId != adUnitId) return;

        _activeSession.ClosedReceived = true;

        if (_adTypes[adUnitId] == AdType.Interstitial)
        {
            // Interstitial은 OnClosed에서 보상 처리
            if (!_activeSession.RewardGranted)
            {
                _activeSession.RewardGranted = true;
                _activeSession.OnRewardCallback?.Invoke();
                UserInfo.AddAdvertisingViewCount();
            }
            _activeSession.OnClosedCallback?.Invoke();
            FinalizeSession();
        }
        else // Reward
        {
            // UI 잠금 해제 (보상 지급 여부와 무관하게 팝업 닫기)
            _activeSession.OnClosedCallback?.Invoke();

            if (_activeSession.RewardGranted)
            {
                // OnRewarded가 이미 처리됨 → 세션 정리
                FinalizeSession();
            }
            else
            {
                // OnRewarded가 OnClosed보다 늦게 올 수 있으므로 잠깐 대기
                // (LevelPlay 공식 문서: OnAdClosed → OnAdRewarded 순서도 정상)
                _activeSession.TimeoutCoroutine = StartCoroutine(WaitForRewardTimeout(adUnitId));
            }
        }

        // 다음 광고 프리로드
        LoadAd(adUnitId);
    }

    private IEnumerator WaitForRewardTimeout(string adUnitId)
    {
        yield return new WaitForSeconds(1f);

        if (_activeSession?.AdUnitId == adUnitId && !_activeSession.RewardGranted)
        {
            Debug.Log($"[AdManager] 보상 없이 닫힘 확정 (타임아웃): {adUnitId}");
            FinalizeSession();
        }
    }

    private void OnAdRewarded(string adUnitId, LevelPlayAdInfo info, LevelPlayReward reward)
    {
        Debug.Log($"[AdManager] 보상 수신: {adUnitId} — {reward.Name} x{reward.Amount}");

        if (_activeSession?.AdUnitId != adUnitId) return;
        if (_activeSession.RewardGranted) return; // 중복 방지

        _activeSession.RewardGranted = true;

        // 타임아웃 코루틴 취소
        if (_activeSession.TimeoutCoroutine != null)
        {
            StopCoroutine(_activeSession.TimeoutCoroutine);
            _activeSession.TimeoutCoroutine = null;
        }

        // 보상 콜백 → WatchAdButton.OnAdRewarded → UIPaymentAdSlot 등에서 재화 지급
        _activeSession.OnRewardCallback?.Invoke();
        UserInfo.AddAdvertisingViewCount();

        if (_activeSession.ClosedReceived)
        {
            // OnClosed가 먼저 왔던 경우 → 여기서 세션 정리
            FinalizeSession();
        }
        // else: 아직 OnClosed 미수신 → OnAdClosed 콜백에서 정리
    }

    private void FinalizeSession()
    {
        if (_activeSession?.TimeoutCoroutine != null)
            StopCoroutine(_activeSession.TimeoutCoroutine);
        _activeSession = null;
    }
}
