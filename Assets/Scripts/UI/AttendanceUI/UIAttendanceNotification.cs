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
        return UserInfo.CheckAttendance();
    }

    protected override void RefreshNotificationMessage()
    {
        DebugLog.Log("출석 체크");
        _alarmObj.SetActive(UserInfo.CheckAttendance());
        base.RefreshNotificationMessage();
    }
}
