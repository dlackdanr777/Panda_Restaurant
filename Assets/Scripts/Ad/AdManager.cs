using System.Collections.Generic;
using Unity.Services.LevelPlay;
using UnityEngine;

public enum AdType
{
    Reward,
    Interstitial
}

/// <summary>
/// 광고를 중앙에서 관리하는 싱글톤 매니저
/// 각 adUnitId별로 하나의 광고 인스턴스만 생성하여 충돌 방지
/// Reward와 Interstitial 광고 모두 지원
/// </summary>
public class AdManager : MonoBehaviour
{
    private static AdManager _instance;
    public static AdManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AdManager");
                _instance = go.AddComponent<AdManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // adUnitId별로 LevelPlayRewardedAd 인스턴스를 관리
    private Dictionary<string, LevelPlayRewardedAd> _rewardedAds = new Dictionary<string, LevelPlayRewardedAd>();
    
    // adUnitId별로 LevelPlayInterstitialAd 인스턴스를 관리
    private Dictionary<string, LevelPlayInterstitialAd> _interstitialAds = new Dictionary<string, LevelPlayInterstitialAd>();
    
    // 각 adUnitId의 광고 타입 저장
    private Dictionary<string, AdType> _adTypes = new Dictionary<string, AdType>();
    
    // 각 adUnitId별 상태 관리
    private Dictionary<string, AdState> _adStates = new Dictionary<string, AdState>();
    
    // 현재 재생 중인 adUnitId (에디터 모드에서는 하나만 재생 가능)
    private string _currentPlayingAdUnitId = null;

    public static bool HasInstance => _instance != null;
    public static bool IsAdPlaying => _instance != null && _instance._currentPlayingAdUnitId != null;

    private class AdState
    {
        public bool IsLoading;
        public bool WantToShow;
        public bool RewardGranted;
        public float LoadedTime; // 광고가 로드된 시간
        public float ShowRequestTime; // ShowAd를 호출한 시간
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        #if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log("[AdManager] 개발 빌드 또는 에디터 모드 - 디버그 활성화");
        LevelPlay.SetAdaptersDebug(true);
        LevelPlay.ValidateIntegration();
        #else
        Debug.Log("[AdManager] 릴리즈 빌드 모드");
        #endif
    }

    /// <summary>
    /// 특정 adUnitId의 광고를 등록 및 초기화
    /// </summary>
    public void RegisterAd(string adUnitId,
        AdType adType,
        System.Action<LevelPlayAdInfo> onLoaded,
        System.Action<LevelPlayAdError> onLoadFailed,
        System.Action<LevelPlayAdInfo> onDisplayed,
        System.Action<LevelPlayAdInfo, LevelPlayAdError> onDisplayFailed,
        System.Action<LevelPlayAdInfo> onClosed,
        System.Action<LevelPlayAdInfo, LevelPlayReward> onRewarded = null)
    {
        if (_rewardedAds.ContainsKey(adUnitId) || _interstitialAds.ContainsKey(adUnitId))
        {
            Debug.LogWarning($"[AdManager] adUnitId '{adUnitId}'는 이미 등록되어 있습니다.");
            return;
        }

        Debug.Log($"[AdManager] 광고 등록: {adUnitId}, 타입: {adType}");

        _adTypes[adUnitId] = adType;

        if (adType == AdType.Reward)
        {
            var rewardedAd = new LevelPlayRewardedAd(adUnitId);
            
            rewardedAd.OnAdLoaded += onLoaded;
            rewardedAd.OnAdLoadFailed += onLoadFailed;
            rewardedAd.OnAdDisplayed += onDisplayed;
            rewardedAd.OnAdDisplayFailed += onDisplayFailed;
            rewardedAd.OnAdClosed += onClosed;
            if (onRewarded != null)
                rewardedAd.OnAdRewarded += onRewarded;

            _rewardedAds[adUnitId] = rewardedAd;
        }
        else // Interstitial
        {
            var interstitialAd = new LevelPlayInterstitialAd(adUnitId);
            
            interstitialAd.OnAdLoaded += onLoaded;
            interstitialAd.OnAdLoadFailed += onLoadFailed;
            interstitialAd.OnAdDisplayed += onDisplayed;
            interstitialAd.OnAdDisplayFailed += onDisplayFailed;
            interstitialAd.OnAdClosed += onClosed;
            // Interstitial은 OnAdRewarded가 없음

            _interstitialAds[adUnitId] = interstitialAd;
        }

        _adStates[adUnitId] = new AdState();
    }

    /// <summary>
    /// 광고 등록 해제
    /// </summary>
    public void UnregisterAd(string adUnitId,
        System.Action<LevelPlayAdInfo> onLoaded,
        System.Action<LevelPlayAdError> onLoadFailed,
        System.Action<LevelPlayAdInfo> onDisplayed,
        System.Action<LevelPlayAdInfo, LevelPlayAdError> onDisplayFailed,
        System.Action<LevelPlayAdInfo> onClosed,
        System.Action<LevelPlayAdInfo, LevelPlayReward> onRewarded = null)
    {
        if (!_adTypes.ContainsKey(adUnitId)) return;

        var adType = _adTypes[adUnitId];

        if (adType == AdType.Reward && _rewardedAds.ContainsKey(adUnitId))
        {
            var rewardedAd = _rewardedAds[adUnitId];
            
            rewardedAd.OnAdLoaded -= onLoaded;
            rewardedAd.OnAdLoadFailed -= onLoadFailed;
            rewardedAd.OnAdDisplayed -= onDisplayed;
            rewardedAd.OnAdDisplayFailed -= onDisplayFailed;
            rewardedAd.OnAdClosed -= onClosed;
            if (onRewarded != null)
                rewardedAd.OnAdRewarded -= onRewarded;

            _rewardedAds.Remove(adUnitId);
        }
        else if (adType == AdType.Interstitial && _interstitialAds.ContainsKey(adUnitId))
        {
            var interstitialAd = _interstitialAds[adUnitId];
            
            interstitialAd.OnAdLoaded -= onLoaded;
            interstitialAd.OnAdLoadFailed -= onLoadFailed;
            interstitialAd.OnAdDisplayed -= onDisplayed;
            interstitialAd.OnAdDisplayFailed -= onDisplayFailed;
            interstitialAd.OnAdClosed -= onClosed;

            _interstitialAds.Remove(adUnitId);
        }

        _adTypes.Remove(adUnitId);
        _adStates.Remove(adUnitId);

        Debug.Log($"[AdManager] 광고 등록 해제: {adUnitId}");
    }

    /// <summary>
    /// 광고 미리 로드
    /// </summary>
    public void PreloadAd(string adUnitId)
    {
        if (!_adTypes.ContainsKey(adUnitId))
        {
            Debug.LogError($"[AdManager] adUnitId '{adUnitId}'가 등록되지 않았습니다.");
            return;
        }

        var state = _adStates[adUnitId];
        if (state.IsLoading)
        {
            Debug.Log($"[AdManager] Preload 스킵 - 이미 로딩 중: {adUnitId}");
            return;
        }
        
        if (IsAdReady(adUnitId))
        {
            float timeSinceLoaded = Time.realtimeSinceStartup - state.LoadedTime;
            Debug.Log($"[AdManager] Preload 스킵 - 이미 준비됨: {adUnitId} (로드 후 {timeSinceLoaded:F1}초 경과)");
            
            // 광고가 5분 이상 오래되었으면 재로드
            if (timeSinceLoaded > 300f)
            {
                Debug.LogWarning($"[AdManager] 광고가 오래됨 ({timeSinceLoaded:F0}초) - 재로드: {adUnitId}");
                state.LoadedTime = 0;
            }
            else
            {
                return;
            }
        }

        state.IsLoading = true;
        var adType = _adTypes[adUnitId];

        Debug.Log($"[AdManager] Preload 시작: {adUnitId} ({adType})");
        
        if (adType == AdType.Reward)
            _rewardedAds[adUnitId].LoadAd();
        else
            _interstitialAds[adUnitId].LoadAd();
    }

    /// <summary>
    /// 광고가 준비되었는지 확인
    /// </summary>
    public bool IsAdReady(string adUnitId)
    {
        if (!_adTypes.ContainsKey(adUnitId)) return false;

        var adType = _adTypes[adUnitId];

        if (adType == AdType.Reward)
            return _rewardedAds.ContainsKey(adUnitId) && _rewardedAds[adUnitId].IsAdReady();
        else
            return _interstitialAds.ContainsKey(adUnitId) && _interstitialAds[adUnitId].IsAdReady();
    }

    /// <summary>
    /// 광고 표시
    /// </summary>
    public void ShowAd(string adUnitId)
    {
        if (!_adTypes.ContainsKey(adUnitId))
        {
            Debug.LogError($"[AdManager] adUnitId '{adUnitId}'가 등록되지 않았습니다.");
            return;
        }

        var state = _adStates[adUnitId];
        state.WantToShow = true;
        var adType = _adTypes[adUnitId];
        bool isReady = IsAdReady(adUnitId);

        Debug.Log($"[AdManager] ShowAd 호출: {adUnitId} ({adType}), IsReady: {isReady}, IsLoading: {state.IsLoading}");

        if (isReady)
        {
            float timeSinceLoaded = Time.realtimeSinceStartup - state.LoadedTime;
            Debug.Log($"[AdManager] 광고 표시 시도: {adUnitId} (로드 후 {timeSinceLoaded:F1}초 경과)");
            
            state.ShowRequestTime = Time.realtimeSinceStartup;
            
            try
            {
                if (adType == AdType.Reward)
                    _rewardedAds[adUnitId].ShowAd();
                else
                    _interstitialAds[adUnitId].ShowAd();
                
                Debug.Log($"[AdManager] ShowAd() 호출 완료: {adUnitId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AdManager] ShowAd() 예외 발생: {adUnitId} - {e.Message}");
                state.WantToShow = false;
                // 실패 시 재로드
                state.LoadedTime = 0;
                PreloadAd(adUnitId);
            }
            
            return;
        }

        // 준비 안 됐으면 로드부터
        if (state.IsLoading)
        {
            Debug.Log($"[AdManager] 이미 로딩 중 - 로드 완료 후 자동 표시: {adUnitId}");
            return;
        }

        state.IsLoading = true;
        Debug.Log($"[AdManager] 광고 미준비 - 로드 시작: {adUnitId} ({adType})");
        
        if (adType == AdType.Reward)
            _rewardedAds[adUnitId].LoadAd();
        else
            _interstitialAds[adUnitId].LoadAd();
    }

    /// <summary>
    /// 현재 재생 중인 광고인지 확인
    /// </summary>
    public bool IsCurrentPlayingAd(string adUnitId)
    {
        return _currentPlayingAdUnitId == adUnitId;
    }

    /// <summary>
    /// 현재 재생 중인 광고 설정 (OnDisplayed에서 호출)
    /// </summary>
    public void SetCurrentPlayingAd(string adUnitId)
    {
        _currentPlayingAdUnitId = adUnitId;
        Debug.Log($"[AdManager] SetCurrentPlayingAd: {adUnitId}");
    }

    /// <summary>
    /// 광고 재생 종료 처리
    /// </summary>
    public void OnAdPlayFinished(string adUnitId)
    {
        if (_currentPlayingAdUnitId == adUnitId)
        {
            _currentPlayingAdUnitId = null;
        }

        if (_adStates.ContainsKey(adUnitId))
        {
            _adStates[adUnitId].WantToShow = false;
            _adStates[adUnitId].RewardGranted = false; // 다음 광고를 위해 보상 플래그 초기화
        }
        
        Debug.Log($"[AdManager] OnAdPlayFinished - 상태 초기화 완료: {adUnitId}");
    }

    // 상태 관리 메서드들
    public void SetLoading(string adUnitId, bool isLoading)
    {
        if (_adStates.ContainsKey(adUnitId))
            _adStates[adUnitId].IsLoading = isLoading;
    }

    public bool IsLoading(string adUnitId)
    {
        return _adStates.ContainsKey(adUnitId) && _adStates[adUnitId].IsLoading;
    }

    public bool GetWantToShow(string adUnitId)
    {
        return _adStates.ContainsKey(adUnitId) && _adStates[adUnitId].WantToShow;
    }

    public void SetWantToShow(string adUnitId, bool wantToShow)
    {
        if (_adStates.ContainsKey(adUnitId))
            _adStates[adUnitId].WantToShow = wantToShow;
    }

    public bool GetRewardGranted(string adUnitId)
    {
        return _adStates.ContainsKey(adUnitId) && _adStates[adUnitId].RewardGranted;
    }

    public void SetRewardGranted(string adUnitId, bool granted)
    {
        if (_adStates.ContainsKey(adUnitId))
            _adStates[adUnitId].RewardGranted = granted;
    }

    public void SetLoadedTime(string adUnitId)
    {
        if (_adStates.ContainsKey(adUnitId))
            _adStates[adUnitId].LoadedTime = Time.realtimeSinceStartup;
    }

    public float GetTimeSinceLoaded(string adUnitId)
    {
        if (_adStates.ContainsKey(adUnitId) && _adStates[adUnitId].LoadedTime > 0)
            return Time.realtimeSinceStartup - _adStates[adUnitId].LoadedTime;
        return -1f;
    }
}
