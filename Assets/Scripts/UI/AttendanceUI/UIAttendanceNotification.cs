using System;
using UnityEngine;

public class UIAttendanceNotification : UINotificationParent
{

    protected override void Awake()
    {
        base.Awake();
        UserInfo.OnUpdateAttendanceDataHandler += RefreshNotificationMessage;
    }


    protected override void OnDestroy()
    {
        UserInfo.OnUpdateAttendanceDataHandler -= RefreshNotificationMessage;
        base.OnDestroy();
    }


    public override bool GetAlarmState()
    {
        return UserInfo.CheckNoAttendance();
    }

    protected override void RefreshNotificationMessage()
    {
        _alarmObj.SetActive(UserInfo.CheckNoAttendance());
        base.RefreshNotificationMessage();
    }
}
