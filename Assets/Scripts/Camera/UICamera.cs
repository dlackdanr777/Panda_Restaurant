using Muks.DataBind;
using Muks.Tween;
using Muks.UI;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.UI;
using static CameraController;

public class UICamera : MonoBehaviour 
{

    [Header("Components")]
    [SerializeField] private UINavigation _uiNav;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;
    [SerializeField] private UIFloorButtonGroup _floorButtonGroup;

    private CameraController _cameraController;

    public void Init(CameraController cameraController)
    {
        _cameraController = cameraController;

        _leftArrowButton.gameObject.SetActive(true);
        _rightArrowButton.gameObject.SetActive(false);

        _leftArrowButton.onClick.AddListener(MoveKitchen);

        _rightArrowButton.onClick.AddListener(() =>
        {
            _leftArrowButton.gameObject.SetActive(false);
            _rightArrowButton.gameObject.SetActive(false);
            _cameraController.MoveCamera(CameraController.RestaurantType.Hall);
        });


        _floorButtonGroup.Init(() => MoveFloor(ERestaurantFloorType.Floor1), () => MoveFloor(ERestaurantFloorType.Floor2), () => MoveFloor(ERestaurantFloorType.Floor3));


        DataBind.SetUnityActionValue("ShowRestaurant", () => 
        {
            _uiNav.AllPop();
            MoveHall();
            });

        DataBind.SetUnityActionValue("ShowKitchen", () =>
        {
            _uiNav.AllPop();
            MoveKitchen();
        });

        _cameraController.OnStartMoveCameraHandler += OnStartMoveCameraEvent;
        _cameraController.OnEndMoveCameraHandler += OnEndMoveCameraEvent;
        MoveHall();
    }

    private void MoveFloor(ERestaurantFloorType floor)
    {
        if (_cameraController.CurrentFloor == floor)
            return;

        _cameraController.MoveCamera(floor);
    }

    private void MoveKitchen()
    {
        if (_cameraController.CurrentRestaurant == CameraController.RestaurantType.Kitchen)
            return;

        _cameraController.MoveCamera(CameraController.RestaurantType.Kitchen);
    }


    private void MoveHall()
    {
        if (_cameraController.CurrentRestaurant == CameraController.RestaurantType.Hall)
            return;

        _cameraController.MoveCamera(CameraController.RestaurantType.Hall);
    }


    private void OnStartMoveCameraEvent()
    {
        _rightArrowButton.gameObject.SetActive(false);
        _leftArrowButton.gameObject.SetActive(false);
    }

    private void OnEndMoveCameraEvent(ERestaurantFloorType floor, RestaurantType restaurant)
    {
        bool isLeftArrowButtonEnabled = restaurant == RestaurantType.Hall;
        _leftArrowButton.gameObject.SetActive(isLeftArrowButtonEnabled);
        _rightArrowButton.gameObject.SetActive(!isLeftArrowButtonEnabled);
        _floorButtonGroup.SetFloorText(floor);
    }
}
