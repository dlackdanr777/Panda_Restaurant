using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class UINotificationParent : MonoBehaviour
{
    [SerializeField] protected GameObject _alarmObj;

    private event Action _onRefreshNotificationHandler;
    private List<Action> _actionList = new List<Action>();



    /// <summary> �˶��� ���� ���¸� ��ȯ�ϴ� �Լ� </summary>
    public abstract bool GetAlarmState();


    /// <summary> �˶��� ��ȭ�� ���� ��� ȣ��Ǵ� �븮�ڸ� ��Ͻ�Ű�� �Լ� </summary>
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

    /// <summary> �˶��� Ȯ���ϴ� �Լ� </summary>
    protected virtual void RefreshNotificationMessage()
    {
        _onRefreshNotificationHandler?.Invoke();
    }
}
