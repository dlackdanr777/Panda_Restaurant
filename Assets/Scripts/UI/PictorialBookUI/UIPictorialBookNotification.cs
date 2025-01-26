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
        _gachaItemList = ItemManager.Instance.GetSortGachaItemDataList();
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
        foreach(GachaItemData item in _gachaItemList)
        {
            if (!UserInfo.IsAddNotification(item.Id))
                continue;

            _alarmObj.SetActive(true);
            return;
        }

        _alarmObj.SetActive(false);
        base.RefreshNotificationMessage();
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
