using Muks.UI;
using UnityEngine;

public class MainScene : MonoBehaviour
{


    [Header("Option")]
    [SerializeField] private EStage _stage;

    [Space]
    [Header("Components")]
    [SerializeField] private UINavigation _uiMainNav;
    [SerializeField] private UIFever _uiFever;
    [SerializeField] private AudioClip _mainSceneMusic;
    [SerializeField] private AudioClip _feverMusic;

    private ERestaurantFloorType _currentFloor;
    public ERestaurantFloorType CurrentFloor => _currentFloor;
    private float _updateTimer;

    public void PlayMainMusic()
    {
        if(!_uiFever.IsFeverStart)
            SoundManager.Instance.PlayBackgroundAudio(_mainSceneMusic, 0.5f);

        else
            SoundManager.Instance.PlayBackgroundAudio(_feverMusic, 0.5f);
    }

    public void SetFloor(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
    }

    private void Awake()
    {
        UserInfo.ChangeStage(_stage);
    }


    void Start()
    {
        PlayMainMusic();

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
}
