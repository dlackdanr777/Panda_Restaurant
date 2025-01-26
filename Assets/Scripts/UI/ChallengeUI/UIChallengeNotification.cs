using System.Collections.Generic;
using UnityEngine;

public class UIChallengeNotification : UINotificationParent
{
    private List<ChallengeData> _allTimeChallengeList;
    private List<ChallengeData> _daliyChallengeList;

    public override bool GetAlarmState()
    {
        for (int i = 0, cnt = _daliyChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_daliyChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_daliyChallengeList[i].Id))
            { 
                return true;
            }
        }

        for (int i = 0, cnt = _allTimeChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_allTimeChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_allTimeChallengeList[i].Id))
            {
                return true;
            }
        }

        return false;
    }

    protected override void Awake()
    {
        _daliyChallengeList = ChallengeManager.Instance.GetDailyChallenge();
        _allTimeChallengeList = ChallengeManager.Instance.GetAllTimeChallenge();

        ChallengeManager.Instance.OnDailyChallengeUpdateHandler += RefreshNotificationMessage;
        UserInfo.OnClearChallengeHandler += RefreshNotificationMessage;
        UserInfo.OnDoneChallengeHandler += RefreshNotificationMessage;
        base.Awake();
    }

    protected override void OnDestroy()
    {
        ChallengeManager.Instance.OnDailyChallengeUpdateHandler -= RefreshNotificationMessage;
        UserInfo.OnClearChallengeHandler -= RefreshNotificationMessage;
        UserInfo.OnDoneChallengeHandler -= RefreshNotificationMessage;
        base.OnDestroy();
    }

    protected override void RefreshNotificationMessage()
    {
        for(int i = 0, cnt = _daliyChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_daliyChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_daliyChallengeList[i].Id))
            {
                _alarmObj.SetActive(true);
                return;
            }            
        }

        for (int i = 0, cnt = _allTimeChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_allTimeChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_allTimeChallengeList[i].Id))
            {
                _alarmObj.SetActive(true);
                return;
            }
        }

        _alarmObj.SetActive(false);
        base.RefreshNotificationMessage();
    }

}
