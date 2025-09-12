using System.Collections.Generic;
using UnityEngine;

public class UIChallengeNotification : UINotificationParent
{
    private List<ChallengeData> _allTimeChallengeList;
    private List<ChallengeData> _dailyChallengeList;

    public override bool GetAlarmState()
    {
        InitializeChallengeLists();
        
        for (int i = 0, cnt = _dailyChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_dailyChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_dailyChallengeList[i].Id))
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
        base.Awake();
    }

    private void InitializeChallengeLists()
    {
        if (_allTimeChallengeList == null || _dailyChallengeList == null)
        {
            _allTimeChallengeList = ChallengeManager.Instance.GetAllTimeChallenge();
            _dailyChallengeList = ChallengeManager.Instance.GetDailyChallenge();
        }
    }

    protected void Start()
    {
        ChallengeManager.Instance.OnDailyChallengeUpdateHandler += RefreshNotificationMessage;
        UserInfo.OnClearChallengeHandler += RefreshNotificationMessage;
        UserInfo.OnDoneChallengeHandler += RefreshNotificationMessage;
        RefreshNotificationMessage();
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
        bool active = false;
        InitializeChallengeLists();
        for(int i = 0, cnt = _dailyChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_dailyChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_dailyChallengeList[i].Id))
            {
                active = true;
                if (active != _alarmObj.activeSelf)
                    base.RefreshNotificationMessage();

                _alarmObj.SetActive(active);
                return;
            }            
        }

        for (int i = 0, cnt = _allTimeChallengeList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetIsClearChallenge(_allTimeChallengeList[i].Id) && UserInfo.GetIsDoneChallenge(_allTimeChallengeList[i].Id))
            {
                active = true;
                if (active != _alarmObj.activeSelf)
                    base.RefreshNotificationMessage();

                _alarmObj.SetActive(active);
                return;
            }
        }

        active = false;
        if (active != _alarmObj.activeSelf)
            base.RefreshNotificationMessage();

        _alarmObj.SetActive(active);
    }

}
