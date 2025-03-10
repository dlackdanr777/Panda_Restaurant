using Muks.Tween;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public enum RestaurantType
    {
        Hall,
        Kitchen,
    }

    [Header("Components")]
    [SerializeField] private Camera _cam;
    [SerializeField] private UICamera _uiCamera;

    [Space]
    [Header("Option")]
    [SerializeField] private float _duration;
    [SerializeField] private Ease _ease;

    [Space]
    [Header("Pos")]
    [SerializeField] private float _floor1Pos_Y;
    [SerializeField] private float _floor2Pos_Y;
    [SerializeField] private float _floor3Pos_Y;

    [SerializeField] private float _hallPos_X;
    [SerializeField] private float _kitchenPos_X;


    private RestaurantType _currentRestaurant;
    public RestaurantType CurrentRestaurant => _currentRestaurant;

    private ERestaurantFloorType _currentFloor;
    public ERestaurantFloorType CurrentFloor => _currentFloor;

    private float _targetAspect = 2.3333f;
    private Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>> _targetPosDic = new Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>>();


    public void MoveCamera(ERestaurantFloorType floor, RestaurantType moveType, Action onCompleted = null)
    {
        if (_currentFloor == floor && _currentRestaurant == moveType)
            return;

        _currentFloor = floor;
        _currentRestaurant = moveType;

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[_currentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(onCompleted);
    }

    public void MoveCamera(ERestaurantFloorType floor, Action onCompleted = null)
    {
        if (_currentFloor == floor)
            return;

        _currentFloor = floor;

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[_currentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(onCompleted);
    }

    public void MoveCamera(RestaurantType moveType, Action onCompleted = null)
    {
        if (_currentRestaurant == moveType)
            return;

        _currentRestaurant = moveType;

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[_currentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(onCompleted);
    }


    private void Awake()
    {
        AdjustCamera();
        _uiCamera.Init(this);

        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            _targetPosDic.Add((ERestaurantFloorType)i, new Dictionary<RestaurantType, Vector3>());
        }

        float cameraPosZ = _cam.transform.position.z;
        _targetPosDic[ERestaurantFloorType.Floor1].Add(RestaurantType.Hall, new Vector3(_hallPos_X,  _floor1Pos_Y, cameraPosZ));
        _targetPosDic[ERestaurantFloorType.Floor1].Add(RestaurantType.Kitchen, new Vector3(_kitchenPos_X, _floor1Pos_Y, cameraPosZ));

        _targetPosDic[ERestaurantFloorType.Floor2].Add(RestaurantType.Hall, new Vector3(_hallPos_X, _floor2Pos_Y, cameraPosZ));
        _targetPosDic[ERestaurantFloorType.Floor2].Add(RestaurantType.Kitchen, new Vector3(_kitchenPos_X, _floor2Pos_Y, cameraPosZ));

        _targetPosDic[ERestaurantFloorType.Floor3].Add(RestaurantType.Hall, new Vector3(_hallPos_X, _floor3Pos_Y, cameraPosZ));
        _targetPosDic[ERestaurantFloorType.Floor3].Add(RestaurantType.Kitchen, new Vector3(_kitchenPos_X, _floor3Pos_Y, cameraPosZ));

        _currentFloor = ERestaurantFloorType.Floor1;
        _currentRestaurant = RestaurantType.Hall;
    }


    private void AdjustCamera()
    {
        float deviceAspect = (float)Screen.width / Screen.height;
        float scaleHeight = deviceAspect / _targetAspect;

        if (scaleHeight < 1.0f)
            _cam.orthographicSize = _cam.orthographicSize / scaleHeight;
    }

    
}
