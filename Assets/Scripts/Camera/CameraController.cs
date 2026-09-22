using Muks.Tween;
using Muks.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Profiling;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
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

    [Space]
    [Header("Camera Bounds")]
    [SerializeField] private Vector3 _boundsOffset = Vector3.zero; // 경계 영역의 중심 오프셋
    [SerializeField] private Vector3 _boundsSize = new Vector3(20f, 15f, 1f); // 경계 영역의 크기
    [SerializeField] private bool _showBoundsGizmo = true; // 기즈모 표시 여부



    private Vector2 _startTouchPos;
    private Vector2 _currentTouchPos;
    private Vector3 _startCamPos;
    private bool _isDragging = false;
    private bool _isDraggingEnabled = false;
    private bool _isMoveAction = false;
    private bool _isStopAction = false;
    private bool _moveHorizontally; // X축 이동 여부 결정 변수
    private float _initialTouchThreshold = 0.1f; // 0.5cm 이내에서는 이동 X
    private List<GraphicRaycaster> _graphicRaycasters = new List<GraphicRaycaster>(); // UI 감지용

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int CameraComparisonCycles = 3;
    private Coroutine _cameraComparisonRoutine;
    private UIMarketerImage.PresentationParticleState[] _activePresentationParticleStates;
#endif


    public RestaurantType CurrentRestaurant => _mainScene.CurrentRestaurantType;
    public ERestaurantFloorType CurrentFloor => _mainScene.CurrentFloor;

    private float _targetAspect = 2.3333f;
    private Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>> _targetPosDic = new Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>>();


    public void MoveCamera(ERestaurantFloorType floor, RestaurantType moveType)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _mainScene.SetFloor(floor);
        _mainScene.SetRestaurantType(moveType);
        _cam.TweenStop();
        float dis = Vector3.Distance(_cam.transform.position, _targetPosDic[CurrentFloor][CurrentRestaurant]);
        float duration = Mathf.Clamp((dis / 25) * _duration, 0.12f, _duration);

        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][CurrentRestaurant], duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, CurrentRestaurant);
        });
    }

    public void MoveCamera(ERestaurantFloorType floor)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _mainScene.SetFloor(floor);

        _cam.TweenStop();
        float dis = Vector3.Distance(_cam.transform.position, _targetPosDic[CurrentFloor][CurrentRestaurant]);
        float duration = Mathf.Clamp((dis / 25) * _duration, 0.12f, _duration);
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][CurrentRestaurant], duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, CurrentRestaurant);
        });
    }

    public void MoveCamera(RestaurantType moveType)
    {
        OnStartMoveCameraHandler?.Invoke();
        _isMoveAction = true;
        _mainScene.SetRestaurantType(moveType);
        _cam.TweenStop();

        float dis = Vector3.Distance(_cam.transform.position, _targetPosDic[CurrentFloor][CurrentRestaurant]);
        float duration = Mathf.Clamp((dis / 25) * _duration, 0.12f, _duration);
        TweenData tween;
        tween = _cam.TweenMove(_targetPosDic[CurrentFloor][CurrentRestaurant], duration, _ease);
        tween.OnComplete(() =>
        {
            _isMoveAction = false;
            OnEndMoveCameraHandler?.Invoke(CurrentFloor, CurrentRestaurant);
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
        _mainScene.SetRestaurantType(RestaurantType.Hall);

        MoveCamera(CurrentFloor, CurrentRestaurant);
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

        foreach (GraphicRaycaster gr in FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
        {
            _graphicRaycasters.Add(gr);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F8) && _cameraComparisonRoutine == null)
        {
            _cameraComparisonRoutine = StartCoroutine(RunPresentationParticleComparison());
            return;
        }
#endif

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
                Vector3 targetPos = _targetPosDic[CurrentFloor][CurrentRestaurant];
                if (_cam.transform.position != targetPos)
                {
                    MoveCamera(CurrentFloor, CurrentRestaurant);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator RunPresentationParticleComparison()
    {
        if (_isMoveAction || UserInfo.IsTutorialStart || _navigationCoordinator.GetOpenViewCount() != 0)
        {
            Debug.LogWarning("[PERF-CAMERA-01A] 카메라 이동 중, 튜토리얼 중 또는 UI가 열린 상태라 비교를 시작하지 않습니다.");
            _cameraComparisonRoutine = null;
            yield break;
        }

        UIMarketerImage[] owners = FindObjectsByType<UIMarketerImage>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        UIMarketerImage.PresentationParticleState[] states =
            new UIMarketerImage.PresentationParticleState[owners.Length];
        _activePresentationParticleStates = states;

        int particleSystemCount = 0;
        for (int i = 0; i < owners.Length; i++)
        {
            states[i] = owners[i].CapturePresentationParticleState();
            particleSystemCount += states[i].ParticleSystemCount;
            Debug.Log("[PERF-CAMERA-01A] Baseline particle state: " + states[i].Describe());
        }

        if (particleSystemCount == 0)
        {
            Debug.LogWarning("[PERF-CAMERA-01A] UIMarketerImage 연출 그룹에서 ParticleSystem을 찾지 못했습니다.");
            _cameraComparisonRoutine = null;
            yield break;
        }

        RestaurantType initialRestaurant = CurrentRestaurant;
        CameraComparisonMetrics baselineMetrics = new CameraComparisonMetrics();
        yield return RunComparisonPhase("BASELINE", initialRestaurant, baselineMetrics);

        for (int i = 0; i < states.Length; i++)
        {
            states[i].Exclude();
            Debug.Log(
                "[PERF-CAMERA-01A] Presentation particle excluded: activeSelf=false, previous="
                + states[i].Describe());
        }

        yield return null;
        CameraComparisonMetrics excludedMetrics = new CameraComparisonMetrics();
        yield return RunComparisonPhase(
            "PRESENTATION_PARTICLE_EXCLUDED",
            initialRestaurant,
            excludedMetrics);

        RestorePresentationParticleStates();

        Debug.Log(
            "[PERF-CAMERA-01A] DELTA excluded vs baseline"
            + FormatDelta("frameAvg", baselineMetrics.FrameAverageMilliseconds, excludedMetrics.FrameAverageMilliseconds)
            + FormatDelta("frameMax", baselineMetrics.FrameMaxMilliseconds, excludedMetrics.FrameMaxMilliseconds)
            + FormatDelta("ParticleSystem.UpdateAvg", baselineMetrics.ParticleUpdate.AverageMilliseconds, excludedMetrics.ParticleUpdate.AverageMilliseconds)
            + FormatDelta("TransparentRenderAvg", baselineMetrics.TransparentRender.AverageMilliseconds, excludedMetrics.TransparentRender.AverageMilliseconds)
            + FormatDelta("RendererBoundsAvg", baselineMetrics.RendererBounds.AverageMilliseconds, excludedMetrics.RendererBounds.AverageMilliseconds));

        if (CurrentRestaurant != initialRestaurant)
        {
            MoveCamera(initialRestaurant);
            while (_isMoveAction)
                yield return null;
        }

        _cameraComparisonRoutine = null;
        Debug.Log("[PERF-CAMERA-01A] A/B comparison completed.");
    }

    private void OnDisable()
    {
        RestorePresentationParticleStates();
        _cameraComparisonRoutine = null;
    }

    private void RestorePresentationParticleStates()
    {
        if (_activePresentationParticleStates == null)
            return;

        for (int i = 0; i < _activePresentationParticleStates.Length; i++)
        {
            UIMarketerImage.PresentationParticleState state =
                _activePresentationParticleStates[i];
            if (state == null)
                continue;

            state.Restore();
            Debug.Log("[PERF-CAMERA-01A] Restored particle state: " + state.Describe());
        }

        _activePresentationParticleStates = null;
    }

    private IEnumerator RunComparisonPhase(
        string phaseName,
        RestaurantType initialRestaurant,
        CameraComparisonMetrics metrics)
    {
        using (ProfilerRecorder particleUpdate =
               ProfilerRecorder.StartNew(ProfilerCategory.Particles, "ParticleSystem.Update"))
        using (ProfilerRecorder transparentRender =
               ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render.TransparentGeometry"))
        using (ProfilerRecorder rendererBounds =
               ProfilerRecorder.StartNew(ProfilerCategory.Render, "UpdateRendererBoundingVolumes"))
        {
            RestaurantType otherRestaurant = initialRestaurant == RestaurantType.Hall
                ? RestaurantType.Kitchen
                : RestaurantType.Hall;

            for (int cycle = 0; cycle < CameraComparisonCycles; cycle++)
            {
                yield return MeasureMove(otherRestaurant, particleUpdate, transparentRender, rendererBounds, metrics);
                yield return MeasureMove(initialRestaurant, particleUpdate, transparentRender, rendererBounds, metrics);
            }

            Debug.Log(
                "[PERF-CAMERA-01A] " + phaseName
                + ": moves=" + CameraComparisonCycles * 2
                + ", frames=" + metrics.FrameCount
                + ", frameAvg=" + metrics.FrameAverageMilliseconds.ToString("F3") + "ms"
                + ", frameMax=" + metrics.FrameMaxMilliseconds.ToString("F3") + "ms"
                + FormatRecorderMetric("ParticleSystem.Update", particleUpdate.Valid, metrics.ParticleUpdate)
                + FormatRecorderMetric("Render.TransparentGeometry", transparentRender.Valid, metrics.TransparentRender)
                + FormatRecorderMetric("UpdateRendererBoundingVolumes", rendererBounds.Valid, metrics.RendererBounds));
        }
    }

    private static string FormatDelta(string metricName, double baseline, double excluded)
    {
        double difference = excluded - baseline;
        if (baseline <= 0d)
        {
            return ", " + metricName
                + "=" + difference.ToString("+0.000;-0.000;0.000") + "ms (percent unavailable)";
        }

        double percent = difference / baseline * 100d;
        return ", " + metricName
            + "=" + difference.ToString("+0.000;-0.000;0.000") + "ms"
            + " (" + percent.ToString("+0.0;-0.0;0.0") + "%)";
    }

    private IEnumerator MeasureMove(
        RestaurantType target,
        ProfilerRecorder particleUpdate,
        ProfilerRecorder transparentRender,
        ProfilerRecorder rendererBounds,
        CameraComparisonMetrics metrics)
    {
        MoveCamera(target);
        while (_isMoveAction)
        {
            yield return null;
            metrics.RecordFrame(
                Time.unscaledDeltaTime * 1000d,
                particleUpdate.Valid ? particleUpdate.LastValue : 0,
                transparentRender.Valid ? transparentRender.LastValue : 0,
                rendererBounds.Valid ? rendererBounds.LastValue : 0);
        }
    }

    private static string FormatRecorderMetric(
        string markerName,
        bool recorderValid,
        CameraMarkerMetrics metrics)
    {
        if (!recorderValid)
            return ", " + markerName + "=unavailable";

        return ", " + markerName
            + "Avg=" + metrics.AverageMilliseconds.ToString("F3") + "ms"
            + ", " + markerName
            + "Max=" + metrics.MaxMilliseconds.ToString("F3") + "ms";
    }

    private sealed class CameraComparisonMetrics
    {
        internal readonly CameraMarkerMetrics ParticleUpdate = new CameraMarkerMetrics();
        internal readonly CameraMarkerMetrics TransparentRender = new CameraMarkerMetrics();
        internal readonly CameraMarkerMetrics RendererBounds = new CameraMarkerMetrics();

        internal int FrameCount { get; private set; }
        internal double FrameAverageMilliseconds =>
            FrameCount == 0 ? 0d : _frameTotalMilliseconds / FrameCount;
        internal double FrameMaxMilliseconds { get; private set; }

        private double _frameTotalMilliseconds;

        internal void RecordFrame(
            double frameMilliseconds,
            long particleUpdateNanoseconds,
            long transparentRenderNanoseconds,
            long rendererBoundsNanoseconds)
        {
            FrameCount++;
            _frameTotalMilliseconds += frameMilliseconds;
            FrameMaxMilliseconds = Math.Max(FrameMaxMilliseconds, frameMilliseconds);
            ParticleUpdate.Record(particleUpdateNanoseconds);
            TransparentRender.Record(transparentRenderNanoseconds);
            RendererBounds.Record(rendererBoundsNanoseconds);
        }
    }

    private sealed class CameraMarkerMetrics
    {
        internal double AverageMilliseconds =>
            _sampleCount == 0 ? 0d : _totalNanoseconds / _sampleCount / 1000000d;
        internal double MaxMilliseconds => _maxNanoseconds / 1000000d;

        private int _sampleCount;
        private double _totalNanoseconds;
        private long _maxNanoseconds;

        internal void Record(long nanoseconds)
        {
            _sampleCount++;
            _totalNanoseconds += nanoseconds;
            _maxNanoseconds = Math.Max(_maxNanoseconds, nanoseconds);
        }
    }
#endif

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

        // 🔹 이동 거리 기반으로 속도 조절 (주석처리 - 스무스한 이동을 위해)
        float speedFactor = 1.0f;
        /*
        float camDistance = (_cam.transform.position - _startCamPos).magnitude;
        if (camDistance > _moveThreshold)
        {
            float normalizedDistance = Mathf.Clamp01((camDistance - _moveThreshold) / (_moveThreshold * 0.9f));
            speedFactor = Mathf.Lerp(1.0f, 0.05f, normalizedDistance); // 점진적 속도 감소
        }
        */

        // 🔹 최종 이동 적용 (현재 위치에서 이동값 추가)
        Vector3 moveAmount = new Vector3(-moveDelta.x, -moveDelta.y, 0) * Time.fixedDeltaTime * _dragSpeed * speedFactor;
        Vector3 newPosition = _cam.transform.position + moveAmount;
        
        // 🔹 카메라 위치를 경계 내로 제한
        newPosition = ClampCameraPosition(newPosition);
        _cam.transform.position = newPosition;

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
        Vector3 targetPos = _targetPosDic[CurrentFloor][CurrentRestaurant];

        float xDiff = currentPos.x - targetPos.x; // X축 이동 거리
        float yDiff = currentPos.y - targetPos.y; // Y축 이동 거리

        ERestaurantFloorType nextFloor = CurrentFloor;
        RestaurantType nextRestaurant = CurrentRestaurant;

        // 🔹 Hall ↔ Kitchen 전환
        if (Mathf.Abs(xDiff) >= _moveThreshold)
        {
            nextRestaurant = (xDiff > 0) ? RestaurantType.Hall : RestaurantType.Kitchen;
        }

        // 🔹 층 이동 (Floor 증가 & 감소)
        if (Mathf.Abs(yDiff) >= _moveThreshold) // 변수화된 거리 사용
        {
            int nextFloorIndex = (int)CurrentFloor + (yDiff > 0 ? 1 : -1);


            //TODO:층수 3층제한에서 길이로 변경필요
            // 범위를 벗어나면 원래 위치로 복귀
            if (nextFloorIndex >= (int)ERestaurantFloorType.Length|| nextFloorIndex < (int)ERestaurantFloorType.Floor1)
            {
                MoveCamera(CurrentFloor, CurrentRestaurant);
                return;
            }

            nextFloor = (ERestaurantFloorType)nextFloorIndex;
        }

        // 🔹 최종 이동 결정
        if (nextFloor != CurrentFloor || nextRestaurant != CurrentRestaurant)
        {
            MoveCamera(nextFloor, nextRestaurant);
        }
        else
        {
            MoveCamera(CurrentFloor, CurrentRestaurant);
        }
    }

    // 📌 카메라 위치를 경계 내로 제한하는 메서드
    private Vector3 ClampCameraPosition(Vector3 position)
    {
        Vector3 boundsCenter = _boundsOffset;
        Vector3 halfSize = _boundsSize * 0.5f;
        
        float clampedX = Mathf.Clamp(position.x, boundsCenter.x - halfSize.x, boundsCenter.x + halfSize.x);
        float clampedY = Mathf.Clamp(position.y, boundsCenter.y - halfSize.y, boundsCenter.y + halfSize.y);
        return new Vector3(clampedX, clampedY, position.z);
    }

    // 📌 기즈모로 카메라 경계 영역 표시
    private void OnDrawGizmos()
    {
        if (!_showBoundsGizmo) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(_boundsOffset, _boundsSize);
        
        // 반투명한 영역도 표시
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawCube(_boundsOffset, _boundsSize);
    }
}
