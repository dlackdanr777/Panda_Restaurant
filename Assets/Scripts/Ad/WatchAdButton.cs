using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광고 버튼 컴포넌트.
/// SDK 객체와 콜백 소유권은 AdManager에 있음.
/// 이 컴포넌트는 클릭 시 ShowAd()를 호출하고, 결과를 이벤트로 전파하는 역할만 담당.
/// </summary>
public class WatchAdButton : MonoBehaviour
{
    private enum ShowType
    {
        Default,
        Dia,
        Coin,
        Fever,
        Customer,
        Tip,
    }

    public event System.Action OnAdButtonClicked;   // 광고 버튼 클릭 시
    public event System.Action OnAdDisplayed;       // 광고가 실제로 표시되었을 때
    public event System.Action OnAdDisplayFailed;   // 광고 표시 실패 시
    public event System.Action OnAdRewarded;        // 보상 지급 시
    public event System.Action OnDiaRewarded;       // 다이아 결제 보상 시
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

    private void Awake()
    {
        if (_adButton == null)
        {
            Debug.LogError($"[WatchAdButton] _adButton 미할당 (adUnitId: {_adUnitId})");
            enabled = false;
            return;
        }

        _adButton.onClick.AddListener(ShowAdPopup);

        // SDK 준비 전이면 AdManager가 대기열에 보관하고, 초기화 완료 후 생성·로드
        AdManager.Instance.PrepareAdUnit(_adUnitId, _adType);
    }

    private void OnDestroy()
    {
        if (_adButton != null)
            _adButton.onClick.RemoveListener(ShowAdPopup);
        // AdManager는 DontDestroyOnLoad이므로 광고 객체를 계속 보유
    }

    // ── UI 제어 ──

    public void Interactable(bool value)
    {
        if (_buttonPressEffect != null)
            _buttonPressEffect.Interactable = value;
        if (_adButton != null)
            _adButton.interactable = value;
    }

    private void ShowAdPopup()
    {
        if (_adPopup == null)
        {
            Debug.LogError($"[WatchAdButton] _adPopup 미할당 (adUnitId: {_adUnitId})");
            return;
        }

        switch (_showType)
        {
            case ShowType.Dia:      _adPopup.ShowDiaPopup(this);      break;
            case ShowType.Coin:     _adPopup.ShowCoinPopup(this);     break;
            case ShowType.Fever:    _adPopup.ShowFeverPopup(this);    break;
            case ShowType.Customer: _adPopup.ShowCustomerPopup(this); break;
            case ShowType.Tip:      _adPopup.ShowTipPopup(this);      break;
            default:                _adPopup.ShowPopup(this);         break;
        }
    }

    /// <summary>다이아 결제 경로 보상 (광고 시청 없이 다이아 차감 시)</summary>
    public void DiaRewarded()
    {
        OnDiaRewarded?.Invoke();
    }

    /// <summary>
    /// UIAdPopup에서 "광고 요청" 버튼 클릭 시 호출.
    /// AdManager에 ShowAd를 요청하고, SDK 콜백 결과를 이벤트로 전파.
    /// </summary>
    public void OnClickAd()
    {
        Debug.Log($"[WatchAdButton] 광고 버튼 클릭: {_adUnitId}");
        OnAdButtonClicked?.Invoke();

        AdManager.Instance.ShowAd(
            _adUnitId,
            onDisplayed: () => OnAdDisplayed?.Invoke(),
            onReward:    () => OnAdRewarded?.Invoke(),
            onClosed:    () => OnAdClosed?.Invoke(),
            onFailed:    () => OnAdDisplayFailed?.Invoke()
        );
    }
}

