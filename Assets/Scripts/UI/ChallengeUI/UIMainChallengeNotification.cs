using UnityEngine;

public class UIMainChallengeNotification : UINotificationParent
{
    private ChallengeData _currentData;

    public override bool GetAlarmState()
    {
        _currentData = ChallengeManager.Instance.GetCurrentMainChallengeData();
        if (_currentData == null)
        {
            return false;
        }

        if (UserInfo.GetIsDoneChallenge(_currentData))
        {
            return true;
        }

        return false;
    }

    protected override void Awake()
    {
        ChallengeManager.Instance.OnMainChallengeUpdateHandler += RefreshNotificationMessage;
        UserInfo.OnClearChallengeHandler += RefreshNotificationMessage;
        base.Awake();
    }


    protected override void OnDestroy()
    {
        ChallengeManager.Instance.OnMainChallengeUpdateHandler -= RefreshNotificationMessage;
        UserInfo.OnClearChallengeHandler -= RefreshNotificationMessage;
        base.OnDestroy();
    }

    protected override void RefreshNotificationMessage()
    {
        _currentData = ChallengeManager.Instance.GetCurrentMainChallengeData();
        if(_currentData == null)
        {
            _alarmObj.SetActive(false);
            return;
        }

        if(UserInfo.GetIsDoneChallenge(_currentData))
        {
            _alarmObj.SetActive(true);
            return;
        }

        _alarmObj.SetActive(false);
        base.RefreshNotificationMessage();
    }

}
