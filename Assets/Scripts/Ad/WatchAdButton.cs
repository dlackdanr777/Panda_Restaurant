using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.UI;

public class WatchAdButton : MonoBehaviour
{
    public event System.Action OnAdRewarded;

 [Header("Ad Type")]
    [SerializeField] private AdType _adType = AdType.Reward;

 [Header("LevelPlay Ad Unit ID")]
    [SerializeField] private string _adUnitId = "crivlh2b6qazuw7n";

    [Header("UI")]
    [SerializeField] private UIAdPopup _adPopup;
    [SerializeField] private Button _adButton;
    [SerializeField] private ButtonPressEffect _buttonPressEffect;

    [Header("재시도 설정")]
    [SerializeField] private float _retryDelay = 3f; // 로드 실패 시 재시도 대기 시간
    [SerializeField] private int _maxRetryCount = 3; // 최대 재시도 횟수
    
    private int _currentRetryCount = 0;

    private void Awake()
    {
        if (_adButton == null)
        {
            Debug.LogError($"[WatchAdButton] _adButton이 인스펙터에 할당되지 않았습니다. (adUnitId: {_adUnitId})");
            return;
        }

        Debug.Log($"[WatchAdButton] === 광고 초기화 시작 ===");
        Debug.Log($"[WatchAdButton] Ad Unit ID: {_adUnitId}");
        Debug.Log($"[WatchAdButton] Ad Type: {_adType}");

        // AdManager에 광고 등록
        if (_adType == AdType.Reward)
        {
            AdManager.Instance.RegisterAd(_adUnitId, _adType, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed, OnRewarded);
        }
        else // Interstitial
        {
            AdManager.Instance.RegisterAd(_adUnitId, _adType, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed);
        }

        _adButton.onClick.AddListener(ShowAdPopup);

        // 시작 시 미리 로드
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    public void Interactable(bool value)
    {
        _buttonPressEffect.Interactable = value;
        _adButton.interactable = value;
    }

    private void OnDestroy()
    {
        // AdManager에서 광고 등록 해제
        if (_adType == AdType.Reward)
        {
            AdManager.Instance.UnregisterAd(_adUnitId, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed, OnRewarded);
        }
        else
        {
            AdManager.Instance.UnregisterAd(_adUnitId, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed);
        }

        if (_adButton != null)
            _adButton.onClick.RemoveListener(ShowAdPopup);
    }

    private void ShowAdPopup()
    {
        _adPopup.ShowPopup(OnClickAd);
    }

    private void OnClickAd()
    {
        bool isReady = AdManager.Instance.IsAdReady(_adUnitId);
        Debug.Log($"[WatchAdButton] OnClickAd - {_adUnitId}, IsReady: {isReady}");
        
        if (!isReady)
        {
            Debug.LogWarning($"[WatchAdButton] 광고가 아직 준비되지 않음 - 로드 시도 - {_adUnitId}");
            // TODO: 로딩 인디케이터 표시
            // ToastManager.Instance?.ShowToast("광고를 불러오는 중입니다...");
        }
        
        AdManager.Instance.ShowAd(_adUnitId);
    }

    private void OnLoaded(LevelPlayAdInfo adInfo)
    {
        AdManager.Instance.SetLoading(_adUnitId, false);
        _currentRetryCount = 0; // 로드 성공 시 재시도 카운트 초기화
        Debug.Log($"[WatchAdButton] Loaded - {_adUnitId}");

        // 유저가 보려고 눌렀을 때만 Show
        if (AdManager.Instance.GetWantToShow(_adUnitId))
        {
            AdManager.Instance.SetWantToShow(_adUnitId, false);
            Debug.Log($"[WatchAdButton] Show after Loaded - {_adUnitId}");
            AdManager.Instance.ShowAd(_adUnitId);
        }
    }

    private void OnLoadFailed(LevelPlayAdError error)
    {
        AdManager.Instance.SetLoading(_adUnitId, false);
        Debug.LogError($"[WatchAdButton] LoadFailed - {_adUnitId}: {error}");
        
        // 재시도 카운트 증가
        _currentRetryCount++;
        
        if (_currentRetryCount <= _maxRetryCount)
        {
            Debug.Log($"[WatchAdButton] {_retryDelay}초 후 재시도 ({_currentRetryCount}/{_maxRetryCount}) - {_adUnitId}");
            StartCoroutine(RetryLoadAfterDelay());
        }
        else
        {
            Debug.LogError($"[WatchAdButton] 최대 재시도 횟수 초과 - {_adUnitId}");
            AdManager.Instance.SetWantToShow(_adUnitId, false);
            
            // TODO: 토스트 메시지 표시
            // ToastManager.Instance?.ShowToast("광고를 불러올 수 없습니다. 나중에 다시 시도해주세요.");
        }
    }

    private System.Collections.IEnumerator RetryLoadAfterDelay()
    {
        yield return new WaitForSeconds(_retryDelay);
        Debug.Log($"[WatchAdButton] 재시도 시작 - {_adUnitId}");
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    private void OnDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[WatchAdButton] Displayed - {_adUnitId}");
        
        // OnDisplayed가 호출되었다는 것은 이 광고가 실제로 표시되고 있다는 의미
        // 현재 재생 중인 광고로 설정
        AdManager.Instance.SetCurrentPlayingAd(_adUnitId);
        AdManager.Instance.SetRewardGranted(_adUnitId, false);
        Debug.Log($"[WatchAdButton] 내 광고 재생 시작 - {_adUnitId}");
    }

    private void OnDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        AdManager.Instance.SetWantToShow(_adUnitId, false);
        Debug.LogError($"[WatchAdButton] DisplayFailed - {_adUnitId}: {error}");
        // TODO: 토스트 메시지 표시
        // ToastManager.Instance?.ShowToast("광고를 재생할 수 없습니다. 잠시 후 다시 시도해주세요.");
        
        // 표시 실패 시 다시 로드
        Debug.Log($"[WatchAdButton] DisplayFailed 후 재로드 시도 - {_adUnitId}");
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    private void OnClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[WatchAdButton] Closed - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}, AdType: {_adType}");
        
        // 내 광고가 아니면 무시
        if (!AdManager.Instance.IsCurrentPlayingAd(_adUnitId))
        {
            Debug.LogWarning($"[WatchAdButton] OnClosed 무시 - 다른 광고 ({_adUnitId})");
            return;
        }
        
        // Interstitial의 경우 OnClosed에서 보상 처리
        if (_adType == AdType.Interstitial)
        {
            Debug.Log($"[WatchAdButton] Interstitial OnClosed - 보상 처리 시작");
            
            // 중복 방지
            if (!AdManager.Instance.GetRewardGranted(_adUnitId))
            {
                AdManager.Instance.SetRewardGranted(_adUnitId, true);
                Debug.Log($"[WatchAdButton] Interstitial 보상 지급!");
                OnAdRewarded?.Invoke();
            }
            
            AdManager.Instance.OnAdPlayFinished(_adUnitId);
        }
        else // Reward
        {
            // OnRewarded가 먼저 호출되지 않은 경우에만 정리
            // (에디터에서는 OnClosed -> OnRewarded 순서로 호출될 수 있음)
            if (AdManager.Instance.GetRewardGranted(_adUnitId))
            {
                Debug.Log($"[WatchAdButton] OnClosed - 보상 지급 완료됨, 정리 작업");
                AdManager.Instance.OnAdPlayFinished(_adUnitId);
            }
            else
            {
                Debug.Log($"[WatchAdButton] OnClosed - 보상 대기 중, OnRewarded에서 정리 예정");
            }
        }
        
        // 다음 광고 미리 로드
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"[WatchAdButton] Rewarded - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}, reward: {reward.Name} x{reward.Amount}, AdType: {_adType}");
        
        // Interstitial은 OnRewarded가 호출되지 않으므로 여기서 처리하지 않음
        if (_adType != AdType.Reward)
        {
            Debug.LogWarning($"[WatchAdButton] OnRewarded 호출되었지만 AdType이 Reward가 아님: {_adType}");
            return;
        }
        
        // 내 광고가 아니면 무시
        if (!AdManager.Instance.IsCurrentPlayingAd(_adUnitId))
        {
            Debug.LogWarning($"[WatchAdButton] OnRewarded 무시 - 다른 광고 ({_adUnitId})");
            return;
        }
        
        // 중복 지급 방지
        if (AdManager.Instance.GetRewardGranted(_adUnitId))
        {
            Debug.LogWarning($"[WatchAdButton] OnRewarded 무시 - 이미 보상 지급됨 ({_adUnitId})");
            return;
        }
        
        AdManager.Instance.SetRewardGranted(_adUnitId, true);
        Debug.Log($"[WatchAdButton] 보상 지급! {_adUnitId}: {reward.Name} x{reward.Amount}");

        if (reward.Name == "게임머니")
        {
            UserInfo.AddMoney(reward.Amount);
        }
        else if (reward.Name == "다이아")
        {
            UserInfo.AddDia(reward.Amount);
        }

        OnAdRewarded?.Invoke();
        
        // 보상 지급 후 정리 작업
        AdManager.Instance.OnAdPlayFinished(_adUnitId);
        Debug.Log($"[WatchAdButton] OnRewarded 완료, 정리 작업 완료");
    }
}
