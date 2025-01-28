using System.Collections.Generic;

public class UIPictorialBookNotification : UINotificationParent
{
    private List<GachaItemData> _gachaItemList;
    private List<CustomerData> _customerDataList;

    public override bool GetAlarmState()
    {
        foreach (GachaItemData item in _gachaItemList)
        {
            if (!UserInfo.IsAddNotification(item.Id))
                continue;

            return true;
        }

        foreach (CustomerData data in _customerDataList)
        {
            if (!UserInfo.IsAddNotification(data.Id))
                continue;

            return true;
        }

        return false;
    }

    protected override void Awake()
    {
        _gachaItemList = ItemManager.Instance.GetGachaItemDataList();
        _customerDataList = CustomerDataManager.Instance.GetCustomerDataList();

        UserInfo.OnAddNotificationHandler += OnAddNotificationEvent;
        UserInfo.OnRemoveNotificationHandler += OnRemoveNotificationEvent;
        base.Awake();
    }

    protected override void OnDestroy()
    {
        UserInfo.OnAddNotificationHandler -= OnAddNotificationEvent;
        UserInfo.OnRemoveNotificationHandler -= OnRemoveNotificationEvent;
        base.OnDestroy();
    }

    protected override void RefreshNotificationMessage()
    {
        bool active = false;
        foreach(GachaItemData item in _gachaItemList)
        {
            if (!UserInfo.IsAddNotification(item.Id))
                continue;

            active = true;
            if (active != _alarmObj.activeSelf)
                base.RefreshNotificationMessage();

            _alarmObj.SetActive(active);
            return;
        }

        foreach(CustomerData data in _customerDataList)
        {
            if (!UserInfo.IsAddNotification(data.Id))
                continue;

            active = true;
            if (active != _alarmObj.activeSelf)
                base.RefreshNotificationMessage();

            _alarmObj.SetActive(active);
            return;
        }


        active = false;
        if (active != _alarmObj.activeSelf)
            base.RefreshNotificationMessage();

        _alarmObj.SetActive(active);
    }

    private void OnAddNotificationEvent(string id)
    {
        RefreshNotificationMessage();
    }

    private void OnRemoveNotificationEvent(string id)
    {
        RefreshNotificationMessage();
    }

}
