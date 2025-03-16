using Muks.Tween;
using Muks.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public enum RestaurantType
    {
        Hall,
        Kitchen,
    }

    public Action OnStartMoveCameraHandler;
    public Action<ERestaurantFloorType, RestaurantType> OnEndMoveCameraHandler;

    [Header("Components")]
    [SerializeField] private Camera _cam;
    [SerializeField] private UICamera _uiCamera;
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UINavigationCoordinator _navigationCoordinator;

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

    [Space]
    [Header("Drag Settings")]
    [SerializeField] private float _dragSpeed = 2;
    [SerializeField] private float _moveThreshold = 2f; // 층 이동 감지 거리
    [SerializeField] private LayerMask _draggableLayerMask; // 감지할 레이어
    [SerializeField] private LayerMask _ignoredLayer; // 무시할 레이어



    private Vector2 _startTouchPos;
    private Vector2 _currentTouchPos;
    private Vector3 _startCamPos;
    private bool _isDragging = false;
    private bool _isDraggingEnabled = false;
    private bool _isMoveAction = false;
    private bool _isStopAction = false;
    private bool _moveHorizontally; // X축 이동 여부 결정 변수
    private float _initialTouchThreshold = 0.5f; // 0.5cm 이내에서는 이동 X
    private List<GraphicRaycaster> _graphicRaycasters = new List<GraphicRaycaster>(); // UI 감지용




    private RestaurantType _currentRestaurant;
    public RestaurantType CurrentRestaurant => _currentRestaurant;
    public ERestaurantFloorType CurrentFloor => _mainScene.CurrentFloor;

    private float _targetAspect = 2.3333f;
    private Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>> _targetPosDic = new Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>>();


    public void MoveCamera(ERestaurantFloorType floor, RestaurantType moveType)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _mainScene.SetFloor(floor);
        _currentRestaurant = moveType;

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, _currentRestaurant);
        });
    }

    public void MoveCamera(ERestaurantFloorType floor)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _mainScene.SetFloor(floor);

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, _currentRestaurant);
        });
    }

    public void MoveCamera(RestaurantType moveType)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _currentRestaurant = moveType;

        _cam.TweenStop();
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][_currentRestaurant], _duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, _currentRestaurant);
        });
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

        _mainScene.SetFloor(ERestaurantFloorType.Floor1);
        _currentRestaurant = RestaurantType.Hall;

        MoveCamera(CurrentFloor, _currentRestaurant);
    }

    private void Start()
    {
        RefreshGraphicRaycasters();
    }


    private void AdjustCamera()
    {
        float deviceAspect = (float)Screen.width / Screen.height;
        float scaleHeight = deviceAspect / _targetAspect;

        if (scaleHeight < 1.0f)
            _cam.orthographicSize = _cam.orthographicSize / scaleHeight;
    }

    private void RefreshGraphicRaycasters()
    {
        _graphicRaycasters.Clear();

        foreach (GraphicRaycaster gr in FindObjectsOfType<GraphicRaycaster>())
        {
            _graphicRaycasters.Add(gr);
        }
    }

    private void Update()
    {
        if (_isMoveAction)
            return;


        //DOTO: GetOpenViewCount로 인해 렉이 생길 수 있으니 테스트 해봐야할듯합니다.
        if(UserInfo.IsTutorialStart || _navigationCoordinator.GetOpenViewCount() != 0)
        {
            if(!_isStopAction)
            {
                _isStopAction = true;
                _isDragging = false;
                _isDraggingEnabled = false;
                Vector3 targetPos = _targetPosDic[CurrentFloor][_currentRestaurant];
                if (_cam.transform.position != targetPos)
                {
                    MoveCamera(CurrentFloor, _currentRestaurant);
                }
            }

            return;
        }

        _isStopAction = false;
#if UNITY_EDITOR
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    // 📌 터치 입력 처리
    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchWorldPos = _cam.ScreenToWorldPoint(touch.position);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (IsPointerOverUI() || !IsTouchingDraggableSprite(touchWorldPos))
                    {
                        _isDragging = false; // ❌ 드래그 시작 X
                        return;
                    }

                    _startTouchPos = touch.position;
                    _startCamPos = _cam.transform.position;
                    _isDragging = true;
                    break;

                case TouchPhase.Moved:
                    if (_isDragging)
                    {
                        ProcessDrag(touch.position);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (!_isDragging) // 🔹 드래그가 시작되지 않았다면 이동함수 실행 안 함
                        return;

                    _isDragging = false;
                    _isDraggingEnabled = false;
                    HandleCameraSnapBackOrMove();
                    break;
            }
        }
    }

    // 📌 마우스 입력 처리 (Unity 에디터 전용)
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mouseWorldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            if (IsPointerOverUI() || !IsTouchingDraggableSprite(mouseWorldPos))
            {
                _isDragging = false; // ❌ 드래그 시작 X
                return;
            }

            _startTouchPos = Input.mousePosition;
            _startCamPos = _cam.transform.position;
            _isDragging = true;
        }

        if (Input.GetMouseButton(0) && _isDragging)
        {
            ProcessDrag(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (!_isDragging) // 🔹 드래그가 시작되지 않았다면 이동함수 실행 안 함
                return;

            _isDragging = false;
            _isDraggingEnabled = false;
            HandleCameraSnapBackOrMove();
        }
    }

    private void ProcessDrag(Vector2 currentPos)
    {
        _currentTouchPos = currentPos;
        Vector2 delta = _currentTouchPos - _startTouchPos;
        float distance = delta.magnitude;

        // 🔹 초기 일정 거리 이내에서는 이동 금지 & 이동 방향 결정
        if (!_isDraggingEnabled)
        {
            if (distance < _initialTouchThreshold * Screen.dpi / 2.54f) // cm -> pixels 변환
            {
                return;
            }

            // 🔹 처음 드래그 시 X축 또는 Y축 이동 방향 결정
            _moveHorizontally = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            _isDraggingEnabled = true; // 이동 가능 상태 활성화
        }

        // 🔹 X축 또는 Y축 이동만 허용 (초기 결정된 방향 유지)
        Vector2 moveDelta = delta;
        if (_moveHorizontally)
        {
            moveDelta.y = 0; // X축 이동만 허용 (Y축 무시)
        }
        else
        {
            moveDelta.x = 0; // Y축 이동만 허용 (X축 무시)
        }

        // 🔹 이동 거리 기반으로 속도 조절
        float speedFactor = 1.0f;
        float camDistance = (_cam.transform.position - _startCamPos).magnitude;
        if (camDistance > _moveThreshold)
        {
            float normalizedDistance = Mathf.Clamp01((camDistance - _moveThreshold) / (_moveThreshold * 0.9f));
            speedFactor = Mathf.Lerp(1.0f, 0.05f, normalizedDistance); // 점진적 속도 감소
        }

        // 🔹 최종 이동 적용 (현재 위치에서 이동값 추가)
        Vector3 moveAmount = new Vector3(-moveDelta.x, -moveDelta.y, 0) * Time.fixedDeltaTime * _dragSpeed * speedFactor;
        _cam.transform.position += moveAmount;  // ❗ 현재 위치에서 이동값을 더함 (덮어씌우는 문제 해결)

        // 🔹 새로운 기준점 설정 (이전 터치 위치 업데이트)
        _startTouchPos = currentPos;  // ✅ 터치 이동량을 누적하지 않고 갱신
    }



    // 📌 터치/마우스가 특정 레이어에서 감지되었는지 확인
    private bool IsTouchingDraggableSprite(Vector2 touchWorldPosition)
    {
        Vector3 rayOrigin = _cam.ScreenToWorldPoint(new Vector3(touchWorldPosition.x, touchWorldPosition.y, _cam.nearClipPlane));
        Vector3 rayDirection = _cam.transform.forward;

        int ignoredLayers = _ignoredLayer.value; // 무시할 레이어
        int detectionMask = _draggableLayerMask & ~ignoredLayers; // 무시할 레이어 제외

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 30f, detectionMask))
        {
            return true;
        }
        return false;
    }

    // 📌 UI 위에서 터치 감지 방지
    private bool IsPointerOverUI()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (((1 << result.gameObject.layer) & _ignoredLayer) != 0)
            {
                continue;
            }

            return true;
        }

        return IsPointerOverNonInteractableUI(eventData);
    }

    private bool IsPointerOverNonInteractableUI(PointerEventData eventData)
    {
        foreach (GraphicRaycaster gr in _graphicRaycasters)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            gr.Raycast(eventData, results);

            foreach (var result in results)
            {
                if (((1 << result.gameObject.layer) & _ignoredLayer) != 0)
                {
                    continue;
                }
                return true;
            }
        }
        return false;
    }


    private void HandleCameraSnapBackOrMove()
    {
        Vector3 currentPos = _cam.transform.position;
        Vector3 targetPos = _targetPosDic[CurrentFloor][_currentRestaurant];

        float xDiff = currentPos.x - targetPos.x; // X축 이동 거리
        float yDiff = currentPos.y - targetPos.y; // Y축 이동 거리

        ERestaurantFloorType nextFloor = CurrentFloor;
        RestaurantType nextRestaurant = _currentRestaurant;

        // 🔹 Hall ↔ Kitchen 전환
        if (Mathf.Abs(xDiff) >= _moveThreshold)
        {
            nextRestaurant = (xDiff > 0) ? RestaurantType.Hall : RestaurantType.Kitchen;
        }

        // 🔹 층 이동 (Floor 증가 & 감소)
        if (Mathf.Abs(yDiff) >= _moveThreshold) // 변수화된 거리 사용
        {
            int nextFloorIndex = (int)CurrentFloor + (yDiff > 0 ? 1 : -1);

            // 범위를 벗어나면 원래 위치로 복귀
            if (nextFloorIndex > (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage) || nextFloorIndex < (int)ERestaurantFloorType.Floor1)
            {
                MoveCamera(CurrentFloor, _currentRestaurant);
                return;
            }

            nextFloor = (ERestaurantFloorType)nextFloorIndex;
        }

        // 🔹 최종 이동 결정
        if (nextFloor != CurrentFloor || nextRestaurant != _currentRestaurant)
        {
            MoveCamera(nextFloor, nextRestaurant);
        }
        else
        {
            MoveCamera(CurrentFloor, _currentRestaurant);
        }
    }
}
