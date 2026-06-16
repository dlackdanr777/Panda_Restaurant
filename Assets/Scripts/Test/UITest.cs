using Muks.DataBind;
using UnityEngine;
using UnityEngine.UI;

public class UITest : MonoBehaviour
{
    [Header("Test MiniGame")]
    [SerializeField] private Button _miniGameButton;
    [SerializeField] private UIMiniGame _miniGame;
    [SerializeField] private UIButtonAndText _changeFloorButton;
    [SerializeField] private Button _addClearButton;
    [SerializeField] private Button _addCustomerButton;
    [SerializeField] private Button _addCookButton;

    [Space]
    [Header("Satisfaction System")]
    [SerializeField] private Button _satisfactionUpButton;
    [SerializeField] private Button _satisfactionDownButton;

    private void Awake()
    {
        // #if !UNITY_EDITOR
        //             gameObject.SetActive(false);
        //             return;
        // #endif

        FoodMiniGameData data = FoodDataManager.Instance.GetFoodData("FOOD08").FoodMiniGameData;
        _miniGameButton.onClick.AddListener(() => _miniGame.StartMiniGame(data));

        _addClearButton.onClick.AddListener(() => UserInfo.AddCleanCount(100));
        _addCookButton.onClick.AddListener(() => UserInfo.AddCookCount("COOK01", 100));
        _addCustomerButton.onClick.AddListener(() => UserInfo.AddCustomerCount(100));

        _satisfactionUpButton.onClick.AddListener(() => UserInfo.AddSatisfaction(UserInfo.CurrentStage, 10));
        _satisfactionDownButton.onClick.AddListener(() => UserInfo.AddSatisfaction(UserInfo.CurrentStage, -10));

        DataBind.SetUnityActionValue("SetActiveTestUI", SetActiveTestUI);
    }

    private void Start()
    {
        _changeFloorButton.AddListener(OnChgangeFloorButtonClicked);
                ERestaurantFloorType nextFloor = UserInfo.GetUnlockFloor(UserInfo.CurrentStage) == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1;
        _changeFloorButton.SetText(Utility.GetFloorStrKrByType(nextFloor)  + "변환");
    }

    private void OnChgangeFloorButtonClicked()
    {
        ERestaurantFloorType currentFloor = UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        ERestaurantFloorType nextFloor = currentFloor == ERestaurantFloorType.Floor1 ? ERestaurantFloorType.Floor2 : ERestaurantFloorType.Floor1;

        UserInfo.ChangeUnlockFloor(UserInfo.CurrentStage, nextFloor);
        _changeFloorButton.SetText(Utility.GetFloorStrKrByType(currentFloor) + "변환");
    }

    private void SetActiveTestUI()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
