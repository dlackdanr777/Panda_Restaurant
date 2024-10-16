using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedDisplayManager : MonoBehaviour
{
    public static TimedDisplayManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("TimeDisplayManager");
                _instance = obj.AddComponent<TimedDisplayManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }



    private static TimedDisplayManager _instance;
    private static UITimeDisplay _timeDisplay;


    public void ShowText(string description)
    {
        _timeDisplay.Show(description);
    }

    public void ShowTextLackMoney()
    {
        _timeDisplay.Show("골드가 부족합니다...");
    }

    public void ShowTextLackScore()
    {
        _timeDisplay.Show("평점이 부족합니다...");
    }

    public void ShowTextError()
    {
        _timeDisplay.Show("다시 시도해 주세요.");
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }


    private static void Init()
    {
        UITimeDisplay prefab = Resources.Load<UITimeDisplay>("UI/UITimeDisplay");
        _timeDisplay = Instantiate(prefab, _instance.transform);
        _timeDisplay.Init();
    }

}
