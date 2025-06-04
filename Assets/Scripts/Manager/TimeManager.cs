using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public event Action<string> OnAddTimeHandler;
    public event  Action<string> OnRemoveTimeHandler;
    public event Action OnUpdateTimeHandler;

    public static TimeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("TimeManager");
                _instance = obj.AddComponent<TimeManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static TimeManager _instance;
    private Dictionary<string, int> _timeDic = new Dictionary<string, int>();
    private List<string> _removeList = new List<string>();


    public void SetTime(string key, int time)
    {
        DebugLog.Log($"AddTime: {key}, Time: {time}");
        if (_timeDic.ContainsKey(key))
            _timeDic[key] = time;
        else
            _timeDic.Add(key, time);

        OnAddTimeHandler?.Invoke(key);
    }

    public void RemoveTime(string key, int time)
    {
        if (_timeDic.ContainsKey(key))
        {
            _timeDic[key] -= time;
            if (_timeDic[key] <= 0)
                RemoveKey(key);
        }
    }

    public void RemoveTime(string key)
    {
        if (_timeDic.ContainsKey(key))
        {
            RemoveKey(key);
        }
    }

    public int GetTime(string key)
    {
        if (_timeDic.ContainsKey(key))
        {
            return _timeDic[key];
        }

        return 0;
    }

    public void ResetTime()
    {
        _timeDic.Clear();
        _removeList.Clear();
    }

    public Dictionary<string, int> GetTimeDic()
    {
        return _timeDic;
    }

    public bool IsAddTime(string key)
    {
        return !_timeDic.ContainsKey(key);
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(UpdateTime());
    }


    private IEnumerator UpdateTime()
    {
        WaitForSeconds wait = new WaitForSeconds(1); // 캐싱하여 가비지 감소
        
        while (true) // 지속적으로 실행
        {
            // 1. 복사본 생성하여 안전하게 순회
            string[] keys = new string[_timeDic.Count];
            _timeDic.Keys.CopyTo(keys, 0);
            
            // 2. 시간 감소 및 만료된 타이머 표시
            foreach (var key in keys)
            {
                Debug.Log($"Key: {key}, Time: {_timeDic[key]}");
                if (_timeDic[key] <= 0)
                {
                    _removeList.Add(key);
                    continue;
                }
                _timeDic[key] -= 1;
            }
            
            // 3. 만료된 타이머 제거
            for (int i = 0, cnt = _removeList.Count; i < cnt; i++)
            {
                if (_timeDic.ContainsKey(_removeList[i]))
                {
                    // 필요한 경우 여기에 타이머 만료 이벤트 트리거
                    RemoveKey(_removeList[i]);
                }
            }
            
            // 4. 리스트 비우기
            _removeList.Clear();
            OnUpdateTimeHandler?.Invoke();
            // 5. 다음 업데이트까지 대기
            yield return wait;
        }
    }

    private void RemoveKey(string key)
    {
        if (_timeDic.ContainsKey(key))
            _timeDic.Remove(key);

        OnRemoveTimeHandler?.Invoke(key);
    }

}
