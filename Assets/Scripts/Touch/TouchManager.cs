using UnityEngine;
using UnityEngine.EventSystems;

public class TouchManager : MonoBehaviour
{
    public static TouchManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("TouchManager");
                _instance = obj.AddComponent<TouchManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static TouchManager _instance;

    private Canvas _touchCanvas;
    private UITouchImage _touchImage;
    private Camera _mainCamera;
    private bool _isTouching = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _touchCanvas = Resources.Load<Canvas>("Manager/TouchManager/TouchCanvas");
        if (_touchCanvas == null)
        {
            Debug.LogError("TouchCanvas 프리팹을 찾을 수 없습니다.");
            return;
        }

        _touchCanvas = Instantiate(_touchCanvas, transform);

        _touchImage = Resources.Load<UITouchImage>("Manager/TouchManager/UITouchImage");
        if (_touchImage == null)
        {
            Debug.LogError("UITouchImage 프리팹을 찾을 수 없습니다.");
            return;
        }

        _touchImage = Instantiate(_touchImage, _touchCanvas.transform);
        _touchImage.gameObject.SetActive(false);
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        HandleTouchInput();
    }

    private void HandleTouchInput()
    {
        // 터치 입력 처리 (모바일)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchPosition = touch.position;

            // 터치 상태에 따라 처리 (UI 체크 제거)
            if (touch.phase == TouchPhase.Began)
            {
                _touchImage.SetTouch(true);
                StartTouch(touchPosition);
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                UpdateTouchPosition(touchPosition);
                _touchImage.SetTouch(true);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                Debug.Log("Touch Ended or Canceled");
                _touchImage.SetTouch(false);
                ObjectPoolManager.SpawnTouchEffect(_touchCanvas.transform, GetTouchPosition(Input.mousePosition));
                _isTouching = false;
            }
        }
        // 마우스 입력 처리 (PC)
        else
        {
            // 마우스 상태에 따라 처리 (UI 체크 제거)
            if (Input.GetMouseButtonDown(0))
            {

                StartTouch(Input.mousePosition);
            }

            else if (Input.GetMouseButtonUp(0))
            {
                _touchImage.SetTouch(false);
                ObjectPoolManager.SpawnTouchEffect(_touchCanvas.transform, GetTouchPosition(Input.mousePosition));
                _isTouching = false;
            }


            if (Input.GetMouseButton(0))
            {
                UpdateTouchPosition(Input.mousePosition);
                _touchImage.SetTouch(true);
            }

        }
    }

    private void StartTouch(Vector2 screenPosition)
    {
        _isTouching = true;
        UpdateTouchPosition(screenPosition);
        _touchImage.SetTouch(true);
        _touchImage.StartTouch();
    }

    private void UpdateTouchPosition(Vector2 screenPosition)
    {
        if (_touchCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            _touchImage.transform.position = screenPosition;
        }
        else if (_touchCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _touchCanvas.transform as RectTransform,
                screenPosition,
                _touchCanvas.worldCamera,
                out Vector2 localPoint);
                
            _touchImage.transform.position = _touchCanvas.transform.TransformPoint(localPoint);
        }
        else if (_touchCanvas.renderMode == RenderMode.WorldSpace)
        {
            // 월드 스페이스일 경우의 처리
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                _touchImage.transform.position = hit.point;
            }
        }
    }


    private Vector2 GetTouchPosition(Vector2 screenPosition)
    {
        if (_touchCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return screenPosition;
        }
        else if (_touchCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _touchCanvas.transform as RectTransform,
                screenPosition,
                _touchCanvas.worldCamera,
                out Vector2 localPoint);

            return _touchCanvas.transform.TransformPoint(localPoint);
        }
        else if (_touchCanvas.renderMode == RenderMode.WorldSpace)
        {
            // 월드 스페이스일 경우의 처리
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                return hit.point;
            }
        }

        return Vector2.zero; // 기본값
    }

    // 터치 이미지 숨기기 메서드 (Invoke에서 사용)
    private void HideTouchImage()
    {
        if (_touchImage != null)
            _touchImage.gameObject.SetActive(false);
    }

    // UI 요소 위에 포인터가 있는지 확인 (현재는 사용하지 않음)
    private bool IsPointerOverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = position;
        System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        return results.Count > 0;
    }
}
