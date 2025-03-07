using Muks.DataBind;
using Muks.Tween;
using Muks.UI;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.UI;

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

        _leftArrowButton.onClick.AddListener(() =>
        {
            _leftArrowButton.gameObject.SetActive(false);
            _rightArrowButton.gameObject.SetActive(false);
            _cameraController.MoveCamera(CameraController.RestaurantType.Kitchen, () => _rightArrowButton.gameObject.SetActive(true));
        });

        _rightArrowButton.onClick.AddListener(() =>
        {
            _leftArrowButton.gameObject.SetActive(false);
            _rightArrowButton.gameObject.SetActive(false);
            _cameraController.MoveCamera(CameraController.RestaurantType.Hall, () => _leftArrowButton.gameObject.SetActive(true));
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

        MoveHall();
    }

    private void MoveFloor(ERestaurantFloorType floor)
    {
        if (_cameraController.CurrentFloor == floor)
            return;

        _leftArrowButton.gameObject.SetActive(false);
        _rightArrowButton.gameObject.SetActive(false);
        _floorButtonGroup.SetFloorText(floor);
        _cameraController.MoveCamera(floor, () =>
        {
            bool isLeftButtonActive = _cameraController.CurrentRestaurant == CameraController.RestaurantType.Hall;
            _leftArrowButton.gameObject.SetActive(isLeftButtonActive);
            _rightArrowButton.gameObject.SetActive(!isLeftButtonActive);
            _floorButtonGroup.SetFloorText(_cameraController.CurrentFloor);
        });
    }

    private void MoveKitchen()
    {
        if (_cameraController.CurrentRestaurant == CameraController.RestaurantType.Kitchen)
            return;

        _leftArrowButton.gameObject.SetActive(false);
        _rightArrowButton.gameObject.SetActive(false);
        _cameraController.MoveCamera(CameraController.RestaurantType.Kitchen, () => _rightArrowButton.gameObject.SetActive(true));
    }


    private void MoveHall()
    {
        if (_cameraController.CurrentRestaurant == CameraController.RestaurantType.Hall)
            return;

        _leftArrowButton.gameObject.SetActive(false);
        _rightArrowButton.gameObject.SetActive(false);
        _cameraController.MoveCamera(CameraController.RestaurantType.Hall, () => _leftArrowButton.gameObject.SetActive(true));
    }

}
