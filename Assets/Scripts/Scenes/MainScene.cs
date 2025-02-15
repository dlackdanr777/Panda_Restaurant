using System.Collections.Generic;
using UnityEngine;
using Muks.MobileUI;
using Muks.UI;
using System.Collections;
using BackEnd;
using Muks.BackEnd;

public class MainScene : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UINavigation _uiMainNav;
    [SerializeField] private UIFever _uiFever;
    [SerializeField] private AudioClip _mainSceneMusic;
    [SerializeField] private AudioClip _feverMusic;


    private float _updateTimer;

    public void PlayMainMusic()
    {
        if(!_uiFever.IsFeverStart)
            SoundManager.Instance.PlayBackgroundAudio(_mainSceneMusic, 0.5f);

        else
            SoundManager.Instance.PlayBackgroundAudio(_feverMusic, 0.5f);
    }


    void Start()
    {
        PlayMainMusic();
        StartCoroutine(CheckAttendanceRoutine());

#if UNITY_EDITOR
        UserInfo.AddDia(1000);
        UserInfo.AddMoney(10000000);

        UserInfo.GiveFurniture("TABLE08_01");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "TABLE08_01");

        UserInfo.GiveFurniture("TABLE08_02");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "TABLE08_02");

        UserInfo.GiveFurniture("TABLE08_03");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "TABLE08_03");

        UserInfo.GiveFurniture("TABLE08_04");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "TABLE08_04");

        UserInfo.GiveFurniture("TABLE08_05");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "TABLE08_05");

        UserInfo.GiveFurniture("FLOWER08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "FLOWER08");

        UserInfo.GiveFurniture("RACK08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "RACK08");

        UserInfo.GiveFurniture("WALLPAPER08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "WALLPAPER08");

        UserInfo.GiveFurniture("ACC08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "ACC08");

        UserInfo.GiveFurniture("FRAME08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "FRAME08");

        UserInfo.GiveFurniture("COUNTER08");
        UserInfo.SetEquipFurniture(ERestaurantFloorType.Floor1, "COUNTER08");

        UserInfo.GiveKitchenUtensil("COOKER01_01");
        UserInfo.SetEquipKitchenUtensil(ERestaurantFloorType.Floor1, "COOKER01_01");
#endif
    }


    void Update()
    {
        _updateTimer += Time.deltaTime;

        if (60 <= _updateTimer)
        {
            _updateTimer = 0;
            UserInfo.AddTip(GameManager.Instance.TipPerMinute);
            GameManager.Instance.AsyncSaveGameData();
            if (UserInfo.CheckLastAccessTime())
            {
                UserInfo.UpdateLastAccessTime();
                UserInfo.ResetDailyChallenges();

                if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
                    return;

                GameManager.Instance.AsyncSaveGameData();
            }

        }
    }


    private IEnumerator CheckAttendanceRoutine()
    {
        yield return YieldCache.WaitForSeconds(0.1f);

        if (!UserInfo.CheckAttendance())
        {
            DebugLog.Log("이미 출석함");
            yield break;
        }


        while(UserInfo.IsTutorialStart)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(0.5f);
        while(!_uiMainNav.ViewsVisibleStateCheck())
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiMainNav.Push("UIAttendance");

    }
}
