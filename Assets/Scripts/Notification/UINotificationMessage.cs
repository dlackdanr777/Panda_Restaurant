using UnityEngine;

public class UINotificationMessage : UINotificationParent
{
    [SerializeField] private string _alarmId;


    public void ChangeAlarmId(string alarmId)
    {
        _alarmId = alarmId;
        RefreshNotificationMessage();
    }


    public override bool GetAlarmState()
    {
        return UserInfo.IsAddNotification(_alarmId);
    }


    protected override void Awake()
    {
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
        if(string.IsNullOrWhiteSpace(_alarmId))
        {
            if (_alarmObj.activeSelf)
                base.RefreshNotificationMessage();

            _alarmObj.SetActive(false);
            return;
        }

        bool active = UserInfo.IsAddNotification(_alarmId);
        if (active != _alarmObj.activeSelf)
            base.RefreshNotificationMessage();

        _alarmObj.SetActive(active);
    }

    private void OnAddNotificationEvent(string id)
    {
        if(string.IsNullOrWhiteSpace(_alarmId))
        {
            return;
        }

        if (!_alarmId.Equals(id))
            return;

        _alarmObj.SetActive(true);
    }

    private void OnRemoveNotificationEvent(string id)
    {
        if (string.IsNullOrWhiteSpace(_alarmId))
        {
            return;
        }

        if (!_alarmId.Equals(id))
            return;

        _alarmObj.SetActive(false);
    }
}
