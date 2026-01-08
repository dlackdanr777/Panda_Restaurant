using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.UI;

public class WatchAdButton : MonoBehaviour
{
    public event System.Action OnAdRewarded;

 [Header("LevelPlay Rewarded Ad Unit ID")]
    [SerializeField] private string _adUnitId = "crivlh2b6qazuw7n";

    [Header("UI")]
    [SerializeField] private UIAdPopup _adPopup;
    [SerializeField] private Button _adButton;
    [SerializeField] private ButtonPressEffect _buttonPressEffect;

    private void Awake()
    {
        if (_adButton == null)
        {
            Debug.LogError($"[WatchAdButton] _adButton이 인스펙터에 할당되지 않았습니다. (adUnitId: {_adUnitId})");
            return;
        }

        // AdManager에 광고 등록
        AdManager.Instance.RegisterAd(_adUnitId, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed, OnRewarded);

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
        AdManager.Instance.UnregisterAd(_adUnitId, OnLoaded, OnLoadFailed, OnDisplayed, OnDisplayFailed, OnClosed, OnRewarded);

        if (_adButton != null)
            _adButton.onClick.RemoveListener(ShowAdPopup);
    }

    private void ShowAdPopup()
    {
        _adPopup.ShowPopup(OnClickAd);
    }

    private void OnClickAd()
    {
        Debug.Log($"[WatchAdButton] OnClickAd - {_adUnitId}");
        AdManager.Instance.ShowAd(_adUnitId);
    }

    private void OnLoaded(LevelPlayAdInfo adInfo)
    {
        AdManager.Instance.SetLoading(_adUnitId, false);
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
        AdManager.Instance.SetWantToShow(_adUnitId, false);
        Debug.LogError($"[WatchAdButton] LoadFailed - {_adUnitId}: {error}");
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
    }

    private void OnClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[WatchAdButton] Closed - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}");
        
        // 내 광고가 아니면 무시
        if (!AdManager.Instance.IsCurrentPlayingAd(_adUnitId))
        {
            Debug.LogWarning($"[WatchAdButton] OnClosed 무시 - 다른 광고 ({_adUnitId})");
            return;
        }
        
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
        
        // 다음 광고 미리 로드
        AdManager.Instance.PreloadAd(_adUnitId);
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"[WatchAdButton] Rewarded - {_adUnitId}, isCurrentPlaying: {AdManager.Instance.IsCurrentPlayingAd(_adUnitId)}, reward: {reward.Name} x{reward.Amount}");
        
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
