using TMPro;
using UnityEngine;

public class UImanagementManagementGroup : UIManagementLayoutGroup
{
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
        GameManager.Instance.OnChangeTipPerMinuteHandler += OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler += OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
        UserInfo.OnChangeScoreHandler += OnChangeCurrentScoreEvent;
        GameManager.Instance.OnChangeScoreHandler += OnChangeCurrentScoreEvent;
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
        GameManager.Instance.OnChangeTipPerMinuteHandler -= OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler -= OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler -= OnChangeMoneyEvent;
        UserInfo.OnChangeScoreHandler -= OnChangeCurrentScoreEvent;
        GameManager.Instance.OnChangeScoreHandler -= OnChangeCurrentScoreEvent;
    }   
}
