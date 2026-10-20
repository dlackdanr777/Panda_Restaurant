using TMPro;
using UnityEngine;

public class UImanagementManagementGroup : UIManagementLayoutGroup
{
    private GameManager _subscribedGameManager;

    [SerializeField] private TextMeshProUGUI _tipPerMinuteValue;
    [SerializeField] private TextMeshProUGUI _totalMoneyValue;
    [SerializeField] private TextMeshProUGUI _currentScoreValue;
    [SerializeField] private TextMeshProUGUI _totalCustomerValue;

    public override void EnableLayout(ERestaurantFloorType floor)
    {
        OnChangeTipPerMinuteEvent();
        OnChangeMoneyEvent();
        OnAddCustomerEvent();
        OnChangeCurrentScoreEvent();
    }

    public override void Init()
    {
        _subscribedGameManager = GameManager.Instance;
        _subscribedGameManager.OnChangeTipPerMinuteHandler += OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler += OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
        UserInfo.OnChangeScoreHandler += OnChangeCurrentScoreEvent;
        _subscribedGameManager.OnChangeScoreHandler += OnChangeCurrentScoreEvent;
    }


    private void OnChangeTipPerMinuteEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _tipPerMinuteValue.text = Utility.ConvertToMoney(GameManager.Instance.TipPerMinute);
    }


    private void OnAddCustomerEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _totalCustomerValue.text = Utility.ConvertToMoney(UserInfo.TotalCumulativeCustomerCount);
    }


    private void OnChangeMoneyEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _totalMoneyValue.text = Utility.ConvertToMoney(UserInfo.TotalAddMoney);
    }

    private void OnChangeCurrentScoreEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        _currentScoreValue.text = Utility.ConvertToMoney(UserInfo.Score);
    }

    private void OnDestroy()
    {
        if (_subscribedGameManager != null)
            _subscribedGameManager.OnChangeTipPerMinuteHandler -= OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler -= OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler -= OnChangeMoneyEvent;
        UserInfo.OnChangeScoreHandler -= OnChangeCurrentScoreEvent;
        if (_subscribedGameManager != null)
            _subscribedGameManager.OnChangeScoreHandler -= OnChangeCurrentScoreEvent;
        _subscribedGameManager = null;
    }   
}
