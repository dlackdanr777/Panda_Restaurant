using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class UINotificationParent : MonoBehaviour
{
    [SerializeField] protected GameObject _alarmObj;

    private event Action _onRefreshNotificationHandler;
    private List<Action> _actionList = new List<Action>();



    /// <summary> 알람의 현재 상태를 반환하는 함수 </summary>
    public abstract bool GetAlarmState();


    /// <summary> 알람의 변화가 있을 경우 호출되는 대리자를 등록시키는 함수 </summary>
    public virtual void SetAction(Action action)
    {
        _actionList.Add(action);
        _onRefreshNotificationHandler += action;
    }


    protected virtual void Awake()
    {
        RefreshNotificationMessage();
    }

    protected virtual void OnEnable()
    {
        RefreshNotificationMessage();
    }


    protected virtual void OnDestroy()
    {
        for(int i = 0, cnt = _actionList.Count; i < cnt; ++i)
        {
            _onRefreshNotificationHandler -= _actionList[i];
        }
        _actionList.Clear();
    }

    /// <summary> 알람을 확인하는 함수 </summary>
    protected virtual void RefreshNotificationMessage()
    {
        _onRefreshNotificationHandler?.Invoke();
    }
}
