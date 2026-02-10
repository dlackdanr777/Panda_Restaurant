using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.UI;

public class WatchAdButton : MonoBehaviour
{
    private enum ShowType
    {
        Default,
        Dia,
        Coin,
        Fever,
        Customer
    }

    public event System.Action OnAdButtonClicked;   // 광고 버튼 클릭 시
    public event System.Action OnAdDisplayed;       // 광고가 실제로 표시되었을 때
    public event System.Action OnAdDisplayFailed;   // 광고 표시 실패 시
    public event System.Action OnAdRewarded;        // 광고 보상 지급 시
    public event System.Action OnDiaRewarded;            //다이아 보상 지급시
    public event System.Action OnAdClosed;          // 광고가 닫혔을 때

    [Header("Ad Type")]
    [SerializeField] private AdType _adType = AdType.Reward;
    [SerializeField] private ShowType _showType = ShowType.Coin;

 [Header("LevelPlay Ad Unit ID")]
    [SerializeField] private string _adUnitId = "crivlh2b6qazuw7n";

    [Header("UI")]
    [SerializeField] private UIAdPopup _adPopup;
    [SerializeField] private Button _adButton;
    [SerializeField] private ButtonPressEffect _buttonPressEffect;

    [Header("재시도 설정")]
    [SerializeField] private float _retryDelay = 1f; // 로드 실패 시 재시도 대기 시간
    [SerializeField] private int _maxRetryCount = 3; // 최대 재시도 횟수
    
    [Header("미리 로딩 설정")]
    [SerializeField] private bool _enablePeriodicCheck = true; // 주기적 광고 상태 체크 활성화
    [SerializeField] private float _checkInterval = 10f; // 광고 상태 체크 주기 (초)
    
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

    private void OnEnable()
    {
        // 활성화될 때마다 광고가 준비되지 않았으면 로드
        if (!AdManager.Instance.IsAdReady(_adUnitId) && !AdManager.Instance.IsLoading(_adUnitId))
        {
            Debug.Log($"[WatchAdButton] OnEnable - 광고 미리 로드 시작 - {_adUnitId}");
            AdManager.Instance.PreloadAd(_adUnitId);
        }
        
        // 주기적 광고 상태 체크 시작
        if (_enablePeriodicCheck)
        {
            InvokeRepeating(nameof(CheckAndPreloadAd), _checkInterval, _checkInterval);
        }
    }

    private void OnDisable()
    {
        // 주기적 체크 중단
        if (_enablePeriodicCheck)
        {
            CancelInvoke(nameof(CheckAndPreloadAd));
        }
    }

    private void CheckAndPreloadAd()
    {
        // 광고가 준비되지 않았고 로딩 중이 아니면 로드
        if (!AdManager.Instance.IsAdReady(_adUnitId) && !AdManager.Instance.IsLoading(_adUnitId))
        {
            Debug.Log($"[WatchAdButton] 주기적 체크 - 광고 미리 로드 시작 - {_adUnitId}");
            AdManager.Instance.PreloadAd(_adUnitId);
        }
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
        switch (_showType)
        {
            case ShowType.Dia:
                _adPopup.ShowDiaPopup(this);
                break;
            case ShowType.Coin:
                _adPopup.ShowCoinPopup(this);
                break;
            case ShowType.Fever:
                _adPopup.ShowFeverPopup(this);
                break;
            case ShowType.Customer:
                _adPopup.ShowCustomerPopup(this);
                break;
            case ShowType.Default:
                _adPopup.ShowPopup(this);
                break;
        }
    }

    public void DiaRewarded()
    {
        Debug.Log($"[WatchAdButton] ★★★ 보상 지급 요청 ★★★ - {_adUnitId}");
        OnDiaRewarded?.Invoke();
    }

    public void OnClickAd()
    {
        Debug.Log($"[WatchAdButton] ★★★ 광고 버튼 클릭 ★★★ - {_adUnitId}");
        OnAdButtonClicked?.Invoke();
        
        bool isReady = AdManager.Instance.IsAdReady(_adUnitId);
        bool isLoading = AdManager.Instance.IsLoading(_adUnitId);
        Debug.Log($"[WatchAdButton] OnClickAd - {_adUnitId}, IsReady: {isReady}, IsLoading: {isLoading}");
        
        if (!isReady)
        {
            if (isLoading)
            {
                Debug.LogWarning($"[WatchAdButton] 광고 로딩 중 - {_adUnitId}");
                // TODO: 로딩 인디케이터 표시
                // ToastManager.Instance?.ShowToast("광고를 불러오는 중입니다...");
                return;
            }
            
            Debug.LogWarning($"[WatchAdButton] 광고가 준비되지 않음 - 로드 시작 - {_adUnitId}");
            AdManager.Instance.ShowAd(_adUnitId); // ShowAd 내부에서 로드 처리
            // TODO: 로딩 인디케이터 표시
            // ToastManager.Instance?.ShowToast("광고를 불러오는 중입니다...");
            return;
        }
        
        AdManager.Instance.ShowAd(_adUnitId);
    }

    private void OnLoaded(LevelPlayAdInfo adInfo)
    {
        AdManager.Instance.SetLoading(_adUnitId, false);
        AdManager.Instance.SetLoadedTime(_adUnitId);
        _currentRetryCount = 0; // 로드 성공 시 재시도 카운트 초기화
        
        Debug.Log($"[WatchAdButton] ★★★ ad loaded ★★★ - {_adUnitId}");
        Debug.Log($"[WatchAdButton] Ad Network: {adInfo.AdNetwork}");
        Debug.Log($"[WatchAdButton] Instance Id: {adInfo.InstanceId}");

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
        
        // 에러 코드 분석 및 상세 로그
        if (error.ErrorCode == 509)
        {
            Debug.Log($"[WatchAdButton] ● no fill ● - {_adUnitId} (광고 재고 부족 - 정상 상황)");
        }
        else if (error.ErrorCode == 1037)
        {
            Debug.LogWarning($"[WatchAdButton] ● ad not available ● - {_adUnitId}");
        }
        else
        {
            Debug.LogError($"[WatchAdButton] Error Code: {error.ErrorCode}, Message: {error.ErrorMessage}");
        }
        
        // 재시도 카운트 증가
        _currentRetryCount++;
        
        if (_currentRetryCount <= _maxRetryCount)
        {
            Debug.Log($"[WatchAdButton] {_retryDelay}초 후 재시도 ({_currentRetryCount}/{_maxRetryCount}) - {_adUnitId}");
            StartCoroutine(RetryLoadAfterDelay());
        }
        else
        {
            Debug.LogError($"[WatchAdButton] ✖✖✖ 최대 재시도 횟수 초과 - 광고 로드 실패 ✖✖✖ {_adUnitId}");
            AdManager.Instance.SetWantToShow(_adUnitId, false);
            _currentRetryCount = 0; // 재시도 카운트 리셋
            
            // 광고 로드 실패 이벤트 호출 (UI 팝업 닫기 등)
            OnAdDisplayFailed?.Invoke();
            
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
        Debug.Log($"[WatchAdButton] ★★★ 광고 실제 표시됨 ★★★ - {_adUnitId}");
        
        // 광고 표시 이벤트 호출
        OnAdDisplayed?.Invoke();
        
        // OnDisplayed가 호출되었다는 것은 이 광고가 실제로 표시되고 있다는 의미
        // 현재 재생 중인 광고로 설정 (이것만이 보상 지급의 유일한 조건)
        AdManager.Instance.SetCurrentPlayingAd(_adUnitId);
        AdManager.Instance.SetRewardGranted(_adUnitId, false);
        Debug.Log($"[WatchAdButton] 광고 재생 시작 - 보상 지급 가능 상태로 설정 - {_adUnitId}");
    }

    private void OnDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"[WatchAdButton] ✖✖✖ 광고 표시 실패 ✖✖✖ - {_adUnitId}: {error}");
        
        // 광고 실패 이벤트 호출
        OnAdDisplayFailed?.Invoke();
        
        // 광고 표시 실패 시 현재 재생 중인 광고 초기화 (보상 지급 차단)
        AdManager.Instance.OnAdPlayFinished(_adUnitId);
        AdManager.Instance.SetWantToShow(_adUnitId, false);
        AdManager.Instance.SetRewardGranted(_adUnitId, false);
        
        // TODO: 토스트 메시지 표시
        // ToastManager.Instance?.ShowToast("광고를 재생할 수 없습니다. 잠시 후 다시 시도해주세요.");
        
        // 표시 실패 시 다시 로드
        Debug.Log($"[WatchAdButton] DisplayFailed 후 재로드 시도 - {_adUnitId}");
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    private void OnClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[WatchAdButton] Closed - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}, AdType: {_adType}");
        
        // ★★★ OnDisplayed가 호출되지 않았으면 광고가 실제로 표시되지 않은 것 ★★★
        if (!AdManager.Instance.IsCurrentPlayingAd(_adUnitId))
        {
            Debug.LogError($"[WatchAdButton] ✖✖✖ OnClosed 무시 ✖✖✖ - 광고가 실제로 표시되지 않음 (OnDisplayed 미호출) - {_adUnitId}");
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
                Debug.Log($"[WatchAdButton] ★★★ Interstitial 보상 지급 ★★★");
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
        
        // 다음 광고 미리 로드 (이미 로딩 중이거나 준비된 경우 스킵)
        if (!AdManager.Instance.IsLoading(_adUnitId) && !AdManager.Instance.IsAdReady(_adUnitId))
        {
            Debug.Log($"[WatchAdButton] OnClosed 후 다음 광고 로드 시작 - {_adUnitId}");
            AdManager.Instance.PreloadAd(_adUnitId);
        }
        else
        {
            Debug.Log($"[WatchAdButton] OnClosed - 광고 이미 준비 중/완료됨, 재로드 스킵 - {_adUnitId}");
        }
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"[WatchAdButton] Rewarded 콜백 - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}, reward: {reward.Name} x{reward.Amount}, AdType: {_adType}");
        
        // Interstitial은 OnRewarded가 호출되지 않으므로 여기서 처리하지 않음
        if (_adType != AdType.Reward)
        {
            Debug.LogWarning($"[WatchAdButton] OnRewarded 호출되었지만 AdType이 Reward가 아님: {_adType}");
            return;
        }
        
        // ★★★ 핵심: OnDisplayed가 호출되지 않았으면 광고가 실제로 표시되지 않은 것 ★★★
        if (!AdManager.Instance.IsCurrentPlayingAd(_adUnitId))
        {
            Debug.LogError($"[WatchAdButton] ✖✖✖ 보상 지급 차단 ✖✖✖ - 광고가 실제로 표시되지 않음 (OnDisplayed 미호출) - {_adUnitId}");
            return;
        }
        
        // 중복 지급 방지
        if (AdManager.Instance.GetRewardGranted(_adUnitId))
        {
            Debug.LogWarning($"[WatchAdButton] OnRewarded 무시 - 이미 보상 지급됨 ({_adUnitId})");
            return;
        }
        
        AdManager.Instance.SetRewardGranted(_adUnitId, true);
        Debug.Log($"[WatchAdButton] ★★★ 보상 지급 시작 ★★★ {_adUnitId}: {reward.Name} x{reward.Amount}");

        if (reward.Name == "게임머니")
        {
            UserInfo.AddMoney(reward.Amount);
            Debug.Log($"[WatchAdButton] 게임머니 {reward.Amount} 지급 완료");
        }
        else if (reward.Name == "다이아")
        {
            UserInfo.AddDia(reward.Amount);
            Debug.Log($"[WatchAdButton] 다이아 {reward.Amount} 지급 완료");
        }

        OnAdRewarded?.Invoke();
        
        // 보상 지급 후 정리 작업
        AdManager.Instance.OnAdPlayFinished(_adUnitId);
        Debug.Log($"[WatchAdButton] OnRewarded 완료, 정리 작업 완료");
    }
}
