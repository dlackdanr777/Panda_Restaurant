using System.Collections.Generic;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>
/// 광고를 중앙에서 관리하는 싱글톤 매니저
/// 각 adUnitId별로 하나의 LevelPlayRewardedAd 인스턴스만 생성하여 충돌 방지
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
    
    // 각 adUnitId별 상태 관리
    private Dictionary<string, AdState> _adStates = new Dictionary<string, AdState>();
    
    // 현재 재생 중인 adUnitId (에디터 모드에서는 하나만 재생 가능)
    private string _currentPlayingAdUnitId = null;

    private class AdState
    {
        public bool IsLoading;
        public bool WantToShow;
        public bool RewardGranted;
    }

    void Awake()
    {
        #if DEVELOPMENT_BUILD || UNITY_EDITOR
        LevelPlay.SetAdaptersDebug(true);
        LevelPlay.ValidateIntegration();
        #endif
    }

    /// <summary>
    /// 특정 adUnitId의 광고를 등록 및 초기화
    /// </summary>
    public void RegisterAd(string adUnitId, 
        System.Action<LevelPlayAdInfo> onLoaded,
        System.Action<LevelPlayAdError> onLoadFailed,
        System.Action<LevelPlayAdInfo> onDisplayed,
        System.Action<LevelPlayAdInfo, LevelPlayAdError> onDisplayFailed,
        System.Action<LevelPlayAdInfo> onClosed,
        System.Action<LevelPlayAdInfo, LevelPlayReward> onRewarded)
    {
        if (_rewardedAds.ContainsKey(adUnitId))
        {
            Debug.LogWarning($"[AdManager] adUnitId '{adUnitId}'는 이미 등록되어 있습니다.");
            return;
        }

        Debug.Log($"[AdManager] 광고 등록: {adUnitId}");

        var rewardedAd = new LevelPlayRewardedAd(adUnitId);
        
        rewardedAd.OnAdLoaded += onLoaded;
        rewardedAd.OnAdLoadFailed += onLoadFailed;
        rewardedAd.OnAdDisplayed += onDisplayed;
        rewardedAd.OnAdDisplayFailed += onDisplayFailed;
        rewardedAd.OnAdClosed += onClosed;
        rewardedAd.OnAdRewarded += onRewarded;

        _rewardedAds[adUnitId] = rewardedAd;
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
        System.Action<LevelPlayAdInfo, LevelPlayReward> onRewarded)
    {
        if (!_rewardedAds.ContainsKey(adUnitId)) return;

        var rewardedAd = _rewardedAds[adUnitId];
        
        rewardedAd.OnAdLoaded -= onLoaded;
        rewardedAd.OnAdLoadFailed -= onLoadFailed;
        rewardedAd.OnAdDisplayed -= onDisplayed;
        rewardedAd.OnAdDisplayFailed -= onDisplayFailed;
        rewardedAd.OnAdClosed -= onClosed;
        rewardedAd.OnAdRewarded -= onRewarded;

        _rewardedAds.Remove(adUnitId);
        _adStates.Remove(adUnitId);

        Debug.Log($"[AdManager] 광고 등록 해제: {adUnitId}");
    }

    /// <summary>
    /// 광고 미리 로드
    /// </summary>
    public void PreloadAd(string adUnitId)
    {
        if (!_rewardedAds.ContainsKey(adUnitId))
        {
            Debug.LogError($"[AdManager] adUnitId '{adUnitId}'가 등록되지 않았습니다.");
            return;
        }

        var state = _adStates[adUnitId];
        if (state.IsLoading) return;
        if (IsAdReady(adUnitId)) return;

        state.IsLoading = true;
        _rewardedAds[adUnitId].LoadAd();
        Debug.Log($"[AdManager] Preload: {adUnitId}");
    }

    /// <summary>
    /// 광고가 준비되었는지 확인
    /// </summary>
    public bool IsAdReady(string adUnitId)
    {
        if (!_rewardedAds.ContainsKey(adUnitId)) return false;
        return _rewardedAds[adUnitId].IsAdReady();
    }

    /// <summary>
    /// 광고 표시
    /// </summary>
    public void ShowAd(string adUnitId)
    {
        if (!_rewardedAds.ContainsKey(adUnitId))
        {
            Debug.LogError($"[AdManager] adUnitId '{adUnitId}'가 등록되지 않았습니다.");
            return;
        }

        var state = _adStates[adUnitId];
        state.WantToShow = true;

        if (IsAdReady(adUnitId))
        {
            Debug.Log($"[AdManager] Ready -> Show: {adUnitId}");
            _rewardedAds[adUnitId].ShowAd();
            return;
        }

        // 준비 안 됐으면 로드부터
        if (state.IsLoading) return;

        state.IsLoading = true;
        Debug.Log($"[AdManager] Not ready -> Load: {adUnitId}");
        _rewardedAds[adUnitId].LoadAd();
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
        }
    }

    // 상태 관리 메서드들
    public void SetLoading(string adUnitId, bool isLoading)
    {
        if (_adStates.ContainsKey(adUnitId))
            _adStates[adUnitId].IsLoading = isLoading;
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
}
