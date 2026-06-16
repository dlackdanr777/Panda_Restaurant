using UnityEngine;
using UnityEngine.UI;
using Unity.Services.LevelPlay;

public class TestAdButton : MonoBehaviour
{
    [Header("LevelPlay Rewarded Ad Unit ID")]
    [SerializeField] private string adUnitId = "crivlh2b6qazuw7n";

    [Header("UI")]
    [SerializeField] private Button _adButton;

    private LevelPlayRewardedAd rewardedAd;

    private bool _isLoading;     // 로드 중복 방지
    private bool _wantToShow;    // 유저가 '보려고 눌렀는지' 플래그
    private bool _rewardGranted; // 보상 중복 지급 방지

    private void Awake()
    {
        // 안전장치
        if (_adButton == null)
        {
            Debug.LogError("[RV] _adButton이 인스펙터에 할당되지 않았습니다.");
            return;
        }

        rewardedAd = new LevelPlayRewardedAd(adUnitId);

        // 이벤트 구독
        rewardedAd.OnAdLoaded += OnLoaded;
        rewardedAd.OnAdLoadFailed += OnLoadFailed;

        rewardedAd.OnAdDisplayed += OnDisplayed;
        rewardedAd.OnAdDisplayFailed += OnDisplayFailed;

        rewardedAd.OnAdClosed += OnClosed;
        rewardedAd.OnAdRewarded += OnRewarded;

        _adButton.onClick.AddListener(OnClickAd);

        // (선택) 시작하자마자 한 번 미리 로드해두면 UX가 좋아요.
        Preload();
    }

    private void OnDestroy()
    {
        if (rewardedAd == null) return;

        rewardedAd.OnAdLoaded -= OnLoaded;
        rewardedAd.OnAdLoadFailed -= OnLoadFailed;

        rewardedAd.OnAdDisplayed -= OnDisplayed;
        rewardedAd.OnAdDisplayFailed -= OnDisplayFailed;

        rewardedAd.OnAdClosed -= OnClosed;
        rewardedAd.OnAdRewarded -= OnRewarded;

        if (_adButton != null)
            _adButton.onClick.RemoveListener(OnClickAd);
    }

    private void Preload()
    {
        if (_isLoading) return;
        if (rewardedAd.IsAdReady()) return;

        _isLoading = true;
        rewardedAd.LoadAd();
        Debug.Log("[RV] Preload()");
    }

    private void OnClickAd()
    {
        _wantToShow = true;

        // 이미 준비되어 있으면 바로 보여주기
        if (rewardedAd.IsAdReady())
        {
            Debug.Log("[RV] Ready -> Show()");
            rewardedAd.ShowAd();
            return;
        }

        // 준비 안 됐으면 로드부터
        if (_isLoading) return;

        _isLoading = true;
        Debug.Log("[RV] Not ready -> Load()");
        rewardedAd.LoadAd();
    }

    private void OnLoaded(LevelPlayAdInfo adInfo)
    {
        _isLoading = false;
        Debug.Log("[RV] Loaded");

        // ✅ 유저가 보려고 눌렀을 때만 Show (자동 연쇄 재생 방지)
        if (_wantToShow)
        {
            _wantToShow = false;
            Debug.Log("[RV] Show() after Loaded");
            rewardedAd.ShowAd();
        }
    }

    private void OnLoadFailed(LevelPlayAdError error)
    {
        _isLoading = false;
        _wantToShow = false;
        Debug.LogError($"[RV] LoadFailed: {error}");
    }

    private void OnDisplayed(LevelPlayAdInfo adInfo)
    {
        _rewardGranted = false; // 광고 시작 시 보상 플래그 초기화
        Debug.Log("[RV] Displayed");
    }

    private void OnDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        _wantToShow = false;
        Debug.LogError($"[RV] DisplayFailed: {error}");
    }

    private void OnClosed(LevelPlayAdInfo adInfo)
    {
        _wantToShow = false;
        Debug.Log("[RV] Closed -> Preload next");

        // ✅ 닫힌 뒤에는 다음 광고를 위해 로드만 (Show는 절대 자동으로 하지 않음)
        Preload();
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        if (_rewardGranted) return; // ✅ 중복 지급 방지
        _rewardGranted = true;

        Debug.Log($"[RV] Rewarded: {reward.Name} x{reward.Amount}");

        // TODO: 여기서 코인/다이아 지급
        // GiveDailyCoin((int)reward.Amount);
    }
}
