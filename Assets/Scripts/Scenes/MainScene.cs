using Muks.DataBind;
using Muks.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MainScene : MonoBehaviour
{
#if UNITY_EDITOR
    // Set before loading the scene for an isolated real-account QA run. Normal Editor defaults are unchanged.
    public static bool SuppressEditorDebugGrantsForTesting = false;
#endif

    [Serializable]
    private struct BackgroundData
    {
        [SerializeField] private AudioClip _backgroundMusic;
        public AudioClip BackgroundMusic => _backgroundMusic;

        [SerializeField] private GameObject _background;
        public GameObject Background => _background;
    }

    [Header("Option")]
    [SerializeField] private EStage _stage;

    [Space]
    [Header("Components")]
    [SerializeField] private UINavigationCoordinator _uiNavCoordinator;
    [SerializeField] private UINavigation _uiMainNav;
    [SerializeField] private UINavigation _uiTutorialNav;
    [SerializeField] private FeverSystem _feverSystem;
    [SerializeField] private AudioClip _mainSceneMusic;
    [SerializeField] private AudioClip _feverMusic;
    [SerializeField] private BackgroundData[] _backgroundDatas;

    private ERestaurantFloorType _currentFloor;
    public ERestaurantFloorType CurrentFloor => _currentFloor;

    private RestaurantType _restaurantType;
    public RestaurantType CurrentRestaurantType => _restaurantType;

    private float _updateTimer;
    private bool _attendanceScheduled;
    



    public void PlayMainMusic()
    {
        if (!_feverSystem.IsFeverStart)
            SoundManager.Instance.PlayBackgroundAudio(_mainSceneMusic, 0.5f);

        else
            SoundManager.Instance.PlayBackgroundAudio(_feverMusic, 0.5f);
    }

    public void SetFloor(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        EffectType effectType = SoundManager.Instance.GetHallEffectType(_currentFloor, _restaurantType);
        // OnUIEvent와 동일한 실시간 출처를 사용해 SoundManager 내부 상태와의 어긋남을 방지한다
        if (_uiNavCoordinator.GetOpenViewCount() <= 0)
            SoundManager.Instance.ChangePlayEffectType(effectType, 0.1f);
    }

    public void SetRestaurantType(RestaurantType type)
    {
        if(_restaurantType == type)
            return;

        _restaurantType = type;
        EffectType effectType = SoundManager.Instance.GetHallEffectType(_currentFloor, _restaurantType);
        // OnUIEvent와 동일한 실시간 출처를 사용해 SoundManager 내부 상태와의 어긋남을 방지한다
        if (_uiNavCoordinator.GetOpenViewCount() <= 0)
            SoundManager.Instance.ChangePlayEffectType(effectType, 0.1f);
    }

    private void Awake()
    {
        UserInfo.ChangeStage(_stage);
        SoundManager.Instance.ChangePlayEffectType(EffectType.Restaurant);
        SetBackground();
        _uiNavCoordinator.OnShowUIHandler += OnUIEvent;
        _uiNavCoordinator.OnHideUIHandler += OnUIEvent;

        DataBind.SetUnityActionValue("ChangeBackground", () =>
        {
            SetBackground();
            PlayMainMusic();
        });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DataBind.SetUnityActionValue("ShowMeTheMoney", () => UserInfo.AddMoney(1000000));
        DataBind.SetUnityActionValue("ShowMeTheDia", () => UserInfo.AddDia(1000));
#endif

        UserInfo.CheckFurnitureFoodType(UserInfo.CurrentStage);
        UserInfo.CheckKitchenUtensilFoodType(UserInfo.CurrentStage);
    }


    void Start()
    {
        PlayMainMusic();
        OnUIEvent();
        
        StartCoroutine(WaitToScheduleAttendance());
        SoundManager.Instance.LoadSoundData();
        ChallengeManager.Instance.UpdateChallenge();
        GameManager.Instance.SetMiniGameStart(false);

#if UNITY_EDITOR
        if (!SuppressEditorDebugGrantsForTesting)
        {
        UserInfo.AddDia(1000);
        UserInfo.AddMoney(10000000);

        UserInfo.GiveFurniture(EStage.Stage1, "TABLE08_01");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "TABLE08_01");

        UserInfo.GiveFurniture(EStage.Stage1, "TABLE08_02");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "TABLE08_02");

        UserInfo.GiveFurniture(EStage.Stage1, "TABLE08_03");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "TABLE08_03");

        UserInfo.GiveFurniture(EStage.Stage1, "TABLE08_04");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "TABLE08_04");

        UserInfo.GiveFurniture(EStage.Stage1, "TABLE08_05");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "TABLE08_05");

        UserInfo.GiveFurniture(EStage.Stage1, "FLOWER08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "FLOWER08");

        UserInfo.GiveFurniture(EStage.Stage1, "RACK08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "RACK08");

        UserInfo.GiveFurniture(EStage.Stage1, "WALLPAPER08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "WALLPAPER08");

        UserInfo.GiveFurniture(EStage.Stage1, "ACC08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "ACC08");

        UserInfo.GiveFurniture(EStage.Stage1, "FRAME08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "FRAME08");

        UserInfo.GiveFurniture(EStage.Stage1, "COUNTER08");
        UserInfo.SetEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, "COUNTER08");

        UserInfo.GiveKitchenUtensil(EStage.Stage1, "COOKER01_01");
        UserInfo.SetEquipKitchenUtensil(EStage.Stage1, ERestaurantFloorType.Floor1, "COOKER01_01");

        //Test Code
        var list = SkinDataManager.Instance.GetSortSkinDataList();
        for(int i = 0, cnt = list.Count; i < cnt; i++)
        {
            UserInfo.GiveSkin(list[i]);
        }
        //
        }
#endif

        GameManager.Instance.ChanceScene();
    }

    private IEnumerator WaitToScheduleAttendance()
    {
        // Preparation can await Stage data or saves before IsTutorialStart becomes
        // true. Never occupy the sequential queue while that work is pending.
        while (true)
        {
            while (!IsAttendanceReady())
                yield return null;

            if (_attendanceScheduled || !UserInfo.CheckNoAttendance()) yield break;
            _attendanceScheduled = true;
            bool handled = false;
            bool retryWhenReady = false;
            SequentialCommandManager.Instance.EnqueueCommand(
                () =>
                {
                    if (handled) return;
                    handled = true;
                    if (this == null) return;
                    // A tutorial that began after reservation must not leave this
                    // command holding the queue needed by the tutorial itself.
                    if (!IsAttendanceReady())
                    {
                        retryWhenReady = true;
                        return;
                    }
                    // Showing the window never grants rewards.
                    if (UserInfo.CheckNoAttendance() && !_uiMainNav.CheckActiveView("UIAttendance"))
                        _uiMainNav.Push("UIAttendance");
                },
                () => this == null || _uiMainNav == null || _uiTutorialNav == null
                    || !UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart || AreAttendanceViewsReady(),
                () => handled && (retryWhenReady || this == null || _uiMainNav == null
                    || !_uiMainNav.CheckActiveView("UIAttendance")),
                1, 0.5f);

            while (!handled) yield return null;
            if (!retryWhenReady) yield break;
            _attendanceScheduled = false;
        }
    }

    private bool IsAttendanceReady()
        => UserInfo.IsFirstTutorialClear && !UserInfo.IsTutorialStart && AreAttendanceViewsReady();

    private bool AreAttendanceViewsReady()
    {
        // Tutorial views belong to their own navigation, not the main popup map.
        // Count alone is insufficient: Pop removes a view before its hide animation ends.
        return _uiMainNav != null && _uiTutorialNav != null
            && _uiMainNav.Count == 0 && _uiMainNav.ViewsVisibleStateCheck()
            && _uiTutorialNav.Count == 0 && _uiTutorialNav.ViewsVisibleStateCheck();
    }


    private void SetBackground()
    {
        int randInt = UnityEngine.Random.Range(0, _backgroundDatas.Length);
        _mainSceneMusic = _backgroundDatas[randInt].BackgroundMusic;

        for(int i = 0; i < _backgroundDatas.Length; i++)
        {
            if (i == randInt)
            {
                _backgroundDatas[i].Background.SetActive(true);
            }
            else
            {
                _backgroundDatas[i].Background.SetActive(false);
            }
        }
    }


    void Update()
    {
        if(Input.GetKeyDown(KeyCode.K))
        {
            UserInfo.SaveStageData(UserInfo.CurrentStage);
        }

        if(Input.GetKeyDown(KeyCode.L))
        {
            UserInfo.LoadStageData(UserInfo.CurrentStage);
        }

        if(Input.GetKeyDown(KeyCode.J))
        {
            UserInfo.AddSinkBowlCount(UserInfo.CurrentStage, CurrentFloor);
        }

        _updateTimer += Time.deltaTime;

        if (60 <= _updateTimer)
        {
            _updateTimer = 0;
            UserInfo.AddTip(UserInfo.CurrentStage, GameManager.Instance.TipPerMinute);
            GameManager.Instance.AsyncSaveGameData();
            if (UserInfo.CheckLastAccessTime())
            {
                UserInfo.UpdateLastAccessTime();
                UserInfo.ResetDailyChallenges();
                UserInfo.ResetAdCount();

                if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
                    return;

                GameManager.Instance.AsyncSaveGameData();
            }

            if (UserInfo.CheckLastWeeklyAccessTime())
            {
                UserInfo.ResetWeeklyChallenges();

                if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
                    return;

                GameManager.Instance.AsyncSaveGameData();
            }

        }
    }


    private void OnUIEvent()
    {
        if(_uiNavCoordinator.GetOpenViewCount() <= 0)
        {
            EffectType effectType = SoundManager.Instance.GetHallEffectType(_currentFloor, _restaurantType);
            SoundManager.Instance.ChangePlayEffectType(effectType, 0.1f);
        }
        else
        {
            SoundManager.Instance.ChangePlayEffectType(EffectType.UI, 0.1f);
        }
    }
}
