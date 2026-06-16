using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPaymentAdSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private WatchAdButton _watchAdButton;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _countText;


    private UIPayment _uIPayment;
    private MoneyType _moneyType;

    public void Init(UIPayment uIPayment, MoneyType moneyType)
    {
        _uIPayment = uIPayment;
        _moneyType = moneyType;

        long hourMoney = GameManager.Instance.AddScore <= 200 ? 2000 :  GameManager.Instance.TipPerMinute * 50; // 50 minutes worth of tips
        string valueText = _moneyType == MoneyType.Gold ? Utility.ConvertToMoney(hourMoney) : "1";
        _valueText.SetText(valueText);
        _watchAdButton.OnAdRewarded += OnAdRewerded;
        Show();
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
            UserInfo.AddDailyAdDiaRewardCount();
            UserInfo.AddDia(1);            
            _uIPayment.StartDiaAnime(1);
        }
        GameManager.Instance.SaveGameData();
        Show();
    }
}
