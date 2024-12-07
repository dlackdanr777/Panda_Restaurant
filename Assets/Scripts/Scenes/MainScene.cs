using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

public class MainScene : MonoBehaviour
{
    [SerializeField] private AudioClip _mainSceneMusic;
    private float _updateTimer;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SoundManager.Instance.PlayBackgroundAudio(_mainSceneMusic, 0.5f);
        UserInfo.AddDia(100);
    }

    // Update is called once per frame
    void Update()
    {
        _updateTimer += Time.deltaTime;

        if (60 <= _updateTimer)
        {
            _updateTimer = 0;
            UserInfo.AddTip(GameManager.Instance.TipPerMinute);

            if (UserInfo.IsPreviousDay())
            {
                DebugLog.Log("�������� ��");
                UserInfo.UpdateLastAccessTime();
                UserInfo.ResetDailyChallenges();

                if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
                    return;

                Param param = UserInfo.GetSaveGameData();
                BackendManager.Instance.SaveGameData("GameData", 3, param);
            }

        }
    }
}
