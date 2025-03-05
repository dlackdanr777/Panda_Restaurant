using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // UI 감지용

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private static bool _canMove = true; // 이동 가능 여부 (true = 이동 가능, false = 이동 불가)

    [SerializeField] private Camera _cam;
    [SerializeField] private int _currentBoundIndex = 0;
    [SerializeField] private float _moveSpeed = 1.25f;
    [SerializeField] private float _slowMoveSpeed = 0f;

    [Space]
    [Header("Draggable Layer Mask")]
    [SerializeField] private LayerMask _draggableLayerMask; // 특정 레이어만 감지

    [Space]
    [Header("Camera Move Area")]
    [SerializeField] private List<Rect> _cameraBounds;



    private Vector2 _lastTouchPosition;
    private Vector2 _startTouchPosition;
    private bool _isDragging = false;
    private bool _isTouchOnDraggable = false; // 특정 스프라이트에서 터치 시작 여부
    private float _slowMoveThreshold = 0.1f; // 0.5cm
    private float _targetAspect = 2.3333f;


    public static void SetCameraMove(bool value)
    {
        _canMove = value;
    }


    private void Awake()
    {
        AdjustCamera();
    }

    private void AdjustCamera()
    {
        Camera camera = GetComponent<Camera>();
        float deviceAspect = (float)Screen.width / Screen.height;
        float scaleHeight = deviceAspect / _targetAspect;

        if (scaleHeight < 1.0f)
            camera.orthographicSize = camera.orthographicSize / scaleHeight;
    }

    private void Update()
    {
        if (!_canMove || UserInfo.IsTutorialStart) return; // 이동이 불가능하면 바로 리턴

        if (Application.isMobilePlatform)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseInput();
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchWorldPos = Camera.main.ScreenToWorldPoint(touch.position);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (!IsPointerOverUI() && IsTouchingDraggableSprite(touchWorldPos)) // UI 위가 아니면서 특정 레이어 감지
                    {
                        _isTouchOnDraggable = true;
                        _startTouchPosition = touch.position;
                        _lastTouchPosition = touch.position;
                        _isDragging = false;
                    }
                    break;

                case TouchPhase.Moved:
                    if (_isTouchOnDraggable && _canMove) // 이동 가능할 때만 실행
                    {
                        ProcessMovement(touch.position);
                    }
                    break;

                case TouchPhase.Ended:
                    _isTouchOnDraggable = false;
                    break;
            }
        }
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (!IsPointerOverUI() && IsTouchingDraggableSprite(mouseWorldPos)) // UI 위가 아니면서 특정 레이어 감지
            {
                _isTouchOnDraggable = true;
                _startTouchPosition = Input.mousePosition;
                _lastTouchPosition = Input.mousePosition;
                _isDragging = false;
            }
        }

        if (Input.GetMouseButton(0) && _isTouchOnDraggable && _canMove && !UserInfo.IsTutorialStart) // 이동 가능할 때만 실행
        {
            ProcessMovement(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isTouchOnDraggable = false;
        }
    }

    private void ProcessMovement(Vector2 currentPosition)
    {
        if (!_canMove || UserInfo.IsTutorialStart) return; // 이동 중이라도 _canMove가 false가 되면 즉시 멈춤

        Vector2 delta = currentPosition - _lastTouchPosition;
        float totalDistanceMoved = Mathf.Abs(currentPosition.x - _startTouchPosition.x) + Mathf.Abs(currentPosition.y - _startTouchPosition.y);

        if (!_isDragging && totalDistanceMoved >= _slowMoveThreshold * Screen.dpi)
        {
            _isDragging = true;
        }

        MoveCamera(delta * (_isDragging ? _moveSpeed : _slowMoveSpeed));
        _lastTouchPosition = currentPosition;
    }

    private void MoveCamera(Vector2 delta)
    {
        if (!_canMove || UserInfo.IsTutorialStart ||  _cameraBounds.Count == 0) return;

        Vector3 move = new Vector3(-delta.x, -delta.y, 0) * Time.deltaTime;
        Vector3 newPosition = _cam.transform.position + move;

        Rect currentBounds = _cameraBounds[_currentBoundIndex];

        float clampedX = Mathf.Clamp(newPosition.x, currentBounds.xMin, currentBounds.xMax);
        float clampedY = Mathf.Clamp(newPosition.y, currentBounds.yMin, currentBounds.yMax);

        _cam.transform.position = new Vector3(clampedX, clampedY, _cam.transform.position.z);
    }

    public void ChangeBounds(int newIndex)
    {
        if (newIndex >= 0 && newIndex < _cameraBounds.Count)
        {
            _currentBoundIndex = newIndex;
        }
    }

    private void OnDrawGizmos()
    {
        if (_cameraBounds == null || _cameraBounds.Count == 0) return;

        Gizmos.color = Color.green;

        foreach (Rect bounds in _cameraBounds)
        {
            Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, 0);
            Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, 0);
            Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, 0);
            Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, 0);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }

    private bool IsTouchingDraggableSprite(Vector2 touchWorldPosition)
    {
        Vector3 rayOrigin = new Vector3(touchWorldPosition.x, touchWorldPosition.y, _cam.transform.position.z);
        RaycastHit hit;

        // 카메라 방향으로 Z축을 따라 특정 레이어만 감지
        if (Physics.Raycast(rayOrigin, Vector3.forward, out hit, 20f, _draggableLayerMask))
        {
            return true;
        }

        return false;
    }

    private bool IsPointerOverUI()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0; // UI 요소가 감지되면 true 반환
    }
}
