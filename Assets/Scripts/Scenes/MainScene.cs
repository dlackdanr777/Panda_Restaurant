using Muks.UI;
using System;
using System.Collections.Generic;
using UnityEngine;


public class MainScene : MonoBehaviour
{
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
    [SerializeField] private FeverSystem _feverSystem;
    [SerializeField] private AudioClip _mainSceneMusic;
    [SerializeField] private AudioClip _feverMusic;
    [SerializeField] private BackgroundData[] _backgroundDatas;

    private ERestaurantFloorType _currentFloor;
    public ERestaurantFloorType CurrentFloor => _currentFloor;

    private RestaurantType _restaurantType;
    public RestaurantType CurrentRestaurantType => _restaurantType;

    private float _updateTimer;
    



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
    }

    public void SetRestaurantType(RestaurantType type)
    {
        if(_restaurantType == type)
            return;

        _restaurantType = type;
        if(SoundManager.Instance.EffectType != EffectType.UI)
            SoundManager.Instance.ChangePlayEffectType(_restaurantType == RestaurantType.Hall ? EffectType.Hall : EffectType.Kitchen, 0.1f);
    }

    private void Awake()
    {
        UserInfo.ChangeStage(_stage);
        SoundManager.Instance.ChangePlayEffectType(EffectType.Hall);
        SetBackground();
        _uiNavCoordinator.OnShowUIHandler += OnUIEvent;
        _uiNavCoordinator.OnHideUIHandler += OnUIEvent;
    }


    void Start()
    {
        PlayMainMusic();
        OnUIEvent();
        if (UserInfo.CheckNoAttendance())
        {
            SequentialCommandManager.Instance.EnqueueCommand(() =>  _uiMainNav.Push("UIAttendance"), () => _uiMainNav.ViewsVisibleStateCheck(), () => !_uiMainNav.CheckActiveView("UIAttendance"), 1, 0.5f);
        }


#if UNITY_EDITOR
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
#endif

        GameManager.Instance.ChanceScene();
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
            SoundManager.Instance.ChangePlayEffectType(_restaurantType == RestaurantType.Hall ? EffectType.Hall : EffectType.Kitchen, 0.1f);
        }
        else
        {
            SoundManager.Instance.ChangePlayEffectType(EffectType.UI, 0.1f);
        }
    }
}