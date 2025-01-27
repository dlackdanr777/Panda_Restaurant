using UnityEngine;

public class UIDropDownMenuNotification : UINotificationParent
{
    [SerializeField] private UINotificationParent[] _uiNotifications;

    public override bool GetAlarmState()
    {
        for(int i = 0, cnt = _uiNotifications.Length; i < cnt; ++i)
        {
            if (!_uiNotifications[i].GetAlarmState())
                continue;

            return true;
        }

        return false;
    }

    protected override void Awake()
    {
        base.Awake();
        for(int i = 0, cnt = _uiNotifications.Length; i < cnt; ++i)
        {
            _uiNotifications[i].SetAction(RefreshNotificationMessage);
        }
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void RefreshNotificationMessage()
    {
        DebugLog.Log(GetAlarmState());
        _alarmObj.SetActive(GetAlarmState());
        base.RefreshNotificationMessage();
    }
}
