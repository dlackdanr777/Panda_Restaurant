using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPaymentAdSlot : MonoBehaviour
{
    public const int DiamondRewardAmount = 3;
    [Header("Components")]
    [SerializeField] private WatchAdButton _watchAdButton;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _countText;


    private UIPayment _uIPayment;
    private MoneyType _moneyType;
    private bool _diamondRewardPending;

    public void Init(UIPayment uIPayment, MoneyType moneyType)
    {
        _uIPayment = uIPayment;
        _moneyType = moneyType;

        string valueText = DiamondRewardAmount.ToString();
        if (_moneyType == MoneyType.Gold)
        {
            long hourMoney = GameManager.Instance.AddScore <= 200 ? 2000 : GameManager.Instance.TipPerMinute * 50;
            valueText = Utility.ConvertToMoney(hourMoney);
        }
        _valueText.SetText(valueText);
        // Reopening/reinitializing the shop must not accumulate reward listeners.
        _watchAdButton.OnAdRewarded -= OnAdRewerded;
        _watchAdButton.OnAdRewarded += OnAdRewerded;
        _watchAdButton.OnAdButtonClicked -= OnAdRequested;
        _watchAdButton.OnAdButtonClicked += OnAdRequested;
        _watchAdButton.OnAdDisplayFailed -= OnAdFailed;
        _watchAdButton.OnAdDisplayFailed += OnAdFailed;
        Show();
    }

    private void OnAdRequested() => _diamondRewardPending = _moneyType == MoneyType.Dia
        && UserInfo.DailyAdDiaRewardCount < ConstValue.DAILY_AD_DIA_REWARD_COUNT;
    private void OnAdFailed() => _diamondRewardPending = false;

    private void OnDestroy()
    {
        if (_watchAdButton == null) return;
        _watchAdButton.OnAdRewarded -= OnAdRewerded;
        _watchAdButton.OnAdButtonClicked -= OnAdRequested;
        _watchAdButton.OnAdDisplayFailed -= OnAdFailed;
    }

    public void Show()
    {
        string countText = _moneyType == MoneyType.Gold ?
            $"{ConstValue.DAILY_AD_GOLD_REWARD_COUNT - UserInfo.DailyAdGoldRewardCount}/{ConstValue.DAILY_AD_GOLD_REWARD_COUNT}" :
            $"{ConstValue.DAILY_AD_DIA_REWARD_COUNT - UserInfo.DailyAdDiaRewardCount}/{ConstValue.DAILY_AD_DIA_REWARD_COUNT}";
        _countText.SetText(countText);

        // bool canWatchAd = _moneyType == MoneyType.Gold ?
        //     UserInfo.DailyAdGoldRewardCount < ConstValue.DAILY_AD_GOLD_REWARD_COUNT :
        //     UserInfo.DailyAdDiaRewardCount < ConstValue.DAILY_AD_DIA_REWARD_COUNT;
        // _watchAdButton.Interactable(canWatchAd);
    }

    private void OnAdRewerded()
    {
        if(_moneyType == MoneyType.Gold)
        {
            UserInfo.AddDailyAdGoldRewardCount();
            long hourMoney = GameManager.Instance.AddScore <= 200 ? 2000 :  GameManager.Instance.TipPerMinute * 50; // 50 minutes worth of tips
            UserInfo.AddMoney(hourMoney);
            _uIPayment.StartCoinAnime((int)hourMoney / 10000);
        }

        else if(_moneyType == MoneyType.Dia)
        {
            if (!_diamondRewardPending || UserInfo.DailyAdDiaRewardCount >= ConstValue.DAILY_AD_DIA_REWARD_COUNT) return;
            // Close can arrive before Rewarded. Only failure or the first reward consumes this latch.
            _diamondRewardPending = false;
            UserInfo.AddDailyAdDiaRewardCount();
            UserInfo.AddDia(DiamondRewardAmount);
            CompleteDiamondReward(DiamondRewardAmount);
            Show();
            return;
        }
        // 보상 연출 중 동기 저장으로 인한 끊김 방지 (비동기 저장으로 전환)
        GameManager.Instance.AsyncSaveGameData();
        Show();
    }

    protected virtual void CompleteDiamondReward(int amount)
    {
        _uIPayment.StartDiaAnime(amount);
        GameManager.Instance.AsyncSaveGameData();
    }
}
