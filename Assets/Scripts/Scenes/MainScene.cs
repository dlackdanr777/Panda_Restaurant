using BackEnd;
using Muks.BackEnd;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MainScene : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private AudioClip _mainSceneMusic;


    [Space]
    [Header("Areas")]
    [SerializeField] private DropCoinArea[] _dropCoinAreas;
    public DropCoinArea[] DropCoinAreas => _dropCoinAreas;

    [SerializeField] private DropGarbageArea[] _dropGarbageAreas;
    public DropGarbageArea[] DropGarbageAreas => _dropGarbageAreas;

    private float _updateTimer;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SoundManager.Instance.PlayBackgroundAudio(_mainSceneMusic, 0.5f);
        UpdateArea();
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

/*            if (UserInfo.IsPreviousDay())
            {
                DebugLog.Log("´ÙÀ½³¯ÀÌ µÊ");
                UserInfo.UpdateLastAccessTime();
                UserInfo.ResetDailyChallenges();

                if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
                    return;

                Param param = UserInfo.GetSaveGameData();
                BackendManager.Instance.SaveGameData("GameData", 3, param);
            }*/

        }
    }


    private void UpdateArea()
    {
        List<SaveCoinAreaData> coinAreaDataList = UserInfo.SaveCounAreaDataList;
        List<SaveGarbageAreaData> garbageAreaDataList = UserInfo.SaveGarbageAreaDataList;

        if(coinAreaDataList != null)
        {
            for(int i = 0; i < coinAreaDataList.Count; ++i)
            {
                if (_dropCoinAreas.Length <= i)
                    break;

                SaveCoinAreaData coinAreaData = coinAreaDataList[i];
                _dropCoinAreas[i].LoadData(coinAreaData.CoinCount, coinAreaData.Money);
            }
        }

        if(garbageAreaDataList != null)
        {
            for (int i = 0; i < garbageAreaDataList.Count; ++i)
            {
                if (_dropGarbageAreas.Length <= i)
                    break;

                SaveGarbageAreaData garbageAreaData = garbageAreaDataList[i];
                _dropGarbageAreas[i].LoadData(garbageAreaData.Count);
            }
        }
    }
}
