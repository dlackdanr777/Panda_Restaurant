using System.Collections.Generic;

public class UIPictorialBookNotification : UINotificationParent
{
    private List<GachaItemData> _gachaItemList;

    public override bool GetAlarmState()
    {
        foreach (GachaItemData item in _gachaItemList)
        {
            if (!UserInfo.IsAddNotification(item.Id))
                continue;

            return true;
        }

       return false;
    }

    protected override void Awake()
    {
        _gachaItemList = ItemManager.Instance.GetGachaItemDataList();
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
